// ------------------------------------------------------------------------------------------------
// ControllerGlyphFixer.cs: BepInEx plugin entry point (config, mapping file, patches, commands).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine.InputSystem;
using Color = UnityEngine.Color;
using Sprite = UnityEngine.Sprite;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Plugin entry point: binds config, loads the key mapping file, applies Harmony patches and
    /// registers console commands.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Client-side only: changes prompt text on this player's screen and sends nothing over the
    // network, so servers and other players never need it. Change this only if the mod ever adds
    // RPCs or synced config.
    // Fully qualified: a "using Jotunn.Utils" would make Paths ambiguous with BepInEx.Paths
    [Jotunn.Utils.NetworkCompatibility(Jotunn.Utils.CompatibilityLevel.NotEnforced, Jotunn.Utils.VersionStrictness.None)]
    internal class ControllerGlyphFixer : BaseUnityPlugin
    {
        /// <summary>
        /// BepInEx plugin ID and Harmony ID, in the Author.ModName style most mods use; also names the
        /// config and mapping files, so changing it starts both from scratch.
        /// </summary>
        public const string PluginGUID = "angiebrr.ControllerGlyphFixer";

        /// <summary>Display name shown by BepInEx and ConfigurationManager.</summary>
        public const string PluginName = "ControllerGlyphFixer";

        /// <summary>Plugin version; also used for the assembly version.</summary>
        public const string PluginVersion = "0.0.1";

        /// <summary>Corner radius of the group prefix backings, in UI units (pixels of the sprite).</summary>
        private const int BackingCornerRadius = 7;

        /// <summary>
        /// Size of the mod's glyphs in hover hints, in percent. Hint lines are packed tightly (Valheim's
        /// own glyphs nearly touch); at full size the trackpad-up arrow reached the line above, and at
        /// 85% it was still squeezed under the D-pad glyph there (in game 2026-11-08). The size tag
        /// shrinks towards the baseline, so a smaller glyph also moves its top down.
        /// </summary>
        private const int HintGlyphPercent = 75;

        /// <summary>
        /// Space each side of a hover-hint glyph, in em: at 75% our glyph was ~35 px wide against
        /// Valheim's ~45 px (hint font ~40 px), so it sat 4 px left and the text after it ~9 px left
        /// of the lines above (in game 2026-11-08).
        /// </summary>
        private const float HintGlyphPaddingEm = 0.12f;

        /// <summary>Harmony instance that owns all of this mod's patches.</summary>
        private readonly Harmony m_harmony = new (PluginGUID);

        /// <summary>Config "Hotbar group colors"; kept to unsubscribe on destroy.</summary>
        private ConfigEntry<PrefixColorMode> m_prefixColors;

        /// <summary>Config "Hotbar group background"; kept to unsubscribe on destroy.</summary>
        private ConfigEntry<Color> m_prefixBackground;

        /// <summary>The rounded prefix backing, created in Awake and destroyed with the plugin.</summary>
        private Sprite m_backingSprite;

        /// <summary>Settings whose change redraws every glyph (see <see cref="OnSettingChanged"/>); set in Awake.</summary>
        private HashSet<ConfigEntryBase> m_glyphSettings;

        /// <summary>The mod's glyph sprite asset (F5); null if it couldn't be built.</summary>
        private ModSpriteAsset m_modSprites;

        /// <summary>Moves Valheim's top-left messages below prefixes under AzuEPI's bar; undone in OnDestroy.</summary>
        private MessageRoom m_messageRoom;

        /// <summary>
        /// Seconds to wait after the mapping file changes before reading it: editors often save in
        /// several writes (truncate, write, rename), and reading in between sees a partial file.
        /// </summary>
        private const float MappingReloadDelaySeconds = 0.5f;

        /// <summary>Starts the tooltip of every setting the mod sets itself.</summary>
        private const string ReadOnlyPrefix = "Read-only. ";

        /// <summary>The mapping file: loads, reloads, watches and reports status (F9).</summary>
        private MappingFile m_mappingFile;

        /// <summary>The glyph service, whose mapping a reload replaces.</summary>
        private GlyphService m_glyphs;

        /// <summary>The hover text writer, whose missing keys show in the settings; kept to unsubscribe.</summary>
        private HoverTextWriter m_hoverTextWriter;

        /// <summary>Unscaled time at which to reload the changed mapping file; null when none is pending.</summary>
        private float? m_reloadAt;

        /// <summary>Logs the first failure to empty Localization's text cache in <see cref="RedrawGlyphs"/>.</summary>
        private readonly PatchErrorLog m_cacheErrorLog = new (
            "Couldn't empty Localization's text cache; cached prompts keep their old glyphs until the next input switch");

        /// <summary>Draws "Keys missing from mapping" as a monospace tree in ConfigurationManager; owns its font.</summary>
        private MonoTextDrawer m_missingKeysDrawer;

        /// <summary>Other mods' keybinding settings, read once at the first main menu.</summary>
        private readonly ModShortcuts m_modShortcuts = new (PluginGUID);

        /// <summary>Path of the YAML key mapping file next to the plugin's .cfg.</summary>
        private string MappingPath => Path.Combine(Paths.ConfigPath, PluginGUID + ".mapping.yaml");

        /// <summary>
        /// Called by Unity when BepInEx loads the plugin: builds every object with its
        /// dependencies, then hands them to the patches and commands that use them.
        /// </summary>
        private void Awake()
        {
            // Order counts down so Mod Config lists each section's settings as written here
            ConfigEntry<bool> enabled = Config.Bind(SettingNames.General, SettingNames.Enabled, true, Describe(
                "Turns the whole mod on or off.", order: 4));
            ConfigEntry<bool> keyPrompts = Config.Bind(SettingNames.General, SettingNames.OverrideKeyboardPrompts, true,
                Describe(
                "With a gamepad connected, Valheim's hints (crosshair, hint bars, menu badges) keep controller " +
                "glyphs after keyboard or trackpad input, with keyboard-only keys as your mapped buttons. The " +
                "hotbar has its own switch. Input isn't changed.", order: 3));
            ConfigEntry<bool> hoverText = Config.Bind(SettingNames.General, SettingNames.GlyphsInModHints, true, Describe(
                "Shows keys in other mods' hover hints (e.g. [B] AutoFuel) as your mapped buttons. Needs " +
                $"{SettingNames.OverrideKeyboardPrompts}.", order: 2));
            ConfigEntry<ButtonGlyphOrder> buttonGlyphs = Config.Bind(SettingNames.General, SettingNames.GlyphsUsedByMod,
                ButtonGlyphOrder.Valheim, Describe(
                "Art for the glyphs this mod draws. Valheim: Valheim's, with Steam Deck art for extra buttons (R4, " +
                "L5, trackpads). SteamDeck: Steam Deck art for all. Valheim's own prompts aren't changed.", order: 1));

            ConfigEntry<bool> hotbarLabels = Config.Bind(SettingNames.HotbarGlyphs, SettingNames.ShowHotbarGlyphs, true,
                Describe(
                "Labels the hotbar and AzuExtendedPlayerInventory quick slots with your mapped buttons, grouped by " +
                "held button (e.g. R4).", order: 3));
            ConfigEntry<PrefixColorMode> prefixColors = Config.Bind(SettingNames.HotbarGlyphs,
                SettingNames.HotbarGroupColors, PrefixColorMode.Neutral, Describe(
                "Color of each group's held button and line. Neutral: label color. Palette: built-in colors. " +
                "Custom: the mapping file's colors section.", order: 2));
            ConfigEntry<Color> prefixBackground = Config.Bind(SettingNames.HotbarGlyphs,
                SettingNames.HotbarGroupBackground, new Color(0f, 0f, 0f, 0.7f), Describe(
                "Backing behind each group's held button and line. Alpha 0 hides it.", order: 1));

            m_prefixColors = prefixColors;
            m_prefixBackground = prefixBackground;

            // Reads m_mappingFile only when drawn, after BindMappingFile has set it
            m_missingKeysDrawer = new MonoTextDrawer(() => m_mappingFile.MissingKeysTree);
            m_mappingFile = BindMappingFile();

            // Built before the services that format buttons; without it, buttons fall back to [badges]
            m_modSprites = ModSpriteAsset.TryCreate();
            Func<string, string> modGlyph = m_modSprites != null ? ModGlyphs.Tag : null;
            Func<string, string> hintModGlyph = m_modSprites != null ? button => ModGlyphs.Tag(button, HintGlyphPercent, HintGlyphPaddingEm) : null;

            var glyphs = new GlyphService(m_mappingFile.LoadFirst(), enabled, keyPrompts, hotbarLabels, prefixColors,
                hoverText, buttonGlyphs, modGlyph, hintModGlyph);
            m_glyphs = glyphs;

            BoundKeyStringPatch.Glyphs = glyphs;
            ApplyPatch(typeof(BoundKeyStringPatch));
            InputHintPatch.Glyphs = glyphs;
            ApplyPatch(typeof(InputHintPatch));
            GamePadBadgePatch.Glyphs = glyphs;
            ApplyPatch(typeof(GamePadBadgePatch));

            var hoverTextMods = new HoverTextMods(PluginGUID);
            var hoverTextWriter = new HoverTextWriter(glyphs, hoverTextMods);
            m_hoverTextWriter = hoverTextWriter;
            hoverTextWriter.MissingKeysChanged += ShowMissingKeys;
            HoverTextPatch.Writer = hoverTextWriter;
            ApplyPatch(typeof(HoverTextPatch));

            // Hotbar labels need the raw key lookup, so only patch them if the reverse patch applied
            var quickSlots = new QuickSlotKeys();
            HotbarLabelWriter labels = null;
            InventoryLabelWriter inventoryLabels = null;
            PrefixLabels prefixLabels = null;
            if (ApplyPatch(typeof(OriginalBoundKeyString)))
            {
                m_backingSprite = RoundedSprite.Create(BackingCornerRadius);
                prefixLabels = new PrefixLabels(m_backingSprite, prefixBackground);

                m_messageRoom = new MessageRoom();
                labels = new HotbarLabelWriter(glyphs, quickSlots, prefixLabels, m_messageRoom);
                HotbarLabelsPatch.Writer = labels;
                ApplyPatch(typeof(HotbarLabelsPatch));

                inventoryLabels = new InventoryLabelWriter(glyphs, labels, quickSlots, prefixLabels);
                InventoryLabelsPatch.Writer = inventoryLabels;
                ApplyPatch(typeof(InventoryLabelsPatch));

                prefixColors.SettingChanged += RedrawPrefixes;
                prefixBackground.SettingChanged += RedrawPrefixes;
            }

            // Every switch that changes which glyphs show: cached prompt text must be redrawn live
            m_glyphSettings = new HashSet<ConfigEntryBase> { enabled, keyPrompts, hoverText, buttonGlyphs, hotbarLabels };
            Config.SettingChanged += OnSettingChanged;

            CommandManager.Instance.AddConsoleCommand(
                new SelfTestCommand(glyphs, labels, inventoryLabels, quickSlots, prefixLabels, hoverTextWriter, m_modSprites,
                    m_mappingFile, m_modShortcuts, hoverTextMods));
            CommandManager.Instance.AddConsoleCommand(new ReloadMappingCommand(ReloadMapping));
            m_mappingFile.Watch();
            GUIManager.OnCustomGUIAvailable += AuditModShortcuts;

            LogGamepads();
            InputSystem.onDeviceChange += OnDeviceChange;
            Jotunn.Logger.LogInfo("ControllerGlyphFixer has landed");
        }

        /// <summary>
        /// Called by Unity when the plugin is destroyed (e.g. a hot reload): undoes everything Awake
        /// hooked up, so a reloaded copy starts clean.
        /// </summary>
        private void OnDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            GUIManager.OnCustomGUIAvailable -= AuditModShortcuts;
            m_mappingFile?.Dispose();
            m_missingKeysDrawer?.Destroy();
            if (m_hoverTextWriter != null)
            {
                m_hoverTextWriter.MissingKeysChanged -= ShowMissingKeys;
            }

            if (m_prefixColors != null)
            {
                m_prefixColors.SettingChanged -= RedrawPrefixes;
            }

            if (m_prefixBackground != null)
            {
                m_prefixBackground.SettingChanged -= RedrawPrefixes;
            }

            Config.SettingChanged -= OnSettingChanged;

            m_harmony.UnpatchSelf();
            BoundKeyStringPatch.Glyphs = null;
            InputHintPatch.Glyphs = null;
            GamePadBadgePatch.Glyphs = null;
            HotbarLabelsPatch.Writer = null;
            InventoryLabelsPatch.Writer = null;
            HoverTextPatch.Writer = null;
            RoundedSprite.Destroy(m_backingSprite);
            m_modSprites?.Destroy();
            m_messageRoom?.Restore();
        }

        /// <summary>
        /// Redraws everything showing button glyphs after a glyph setting, the gamepad or the mapping
        /// changes: bars, prefixes, the hover text (its writer caches the last rewrite) and Valheim's
        /// cached prompt text.
        /// </summary>
        /// <param name="sender">The setting.</param>
        /// <param name="args">Unused.</param>
        private void RedrawGlyphs(object sender, EventArgs args)
        {
            RedrawPrefixes(sender, args);
            HoverTextPatch.Writer?.Invalidate();

            // Localize caches each $KEY_ text with our glyphs in it; forget it so prompts ask again.
            // If a game update breaks this, cached prompts update on the next input switch instead.
            try
            {
                Localization.instance.m_cache.EvictAll();
            }
            catch (Exception ex)
            {
                m_cacheErrorLog.LogOnce(ex);
            }
        }

        /// <summary>
        /// Called by Unity every frame: reloads the mapping a moment after the file changed. Does
        /// nothing else per frame.
        /// </summary>
        private void Update()
        {
            if (m_mappingFile != null && m_mappingFile.TakeChange())
            {
                m_reloadAt = UnityEngine.Time.unscaledTime + MappingReloadDelaySeconds;
            }

            if (m_reloadAt.HasValue && UnityEngine.Time.unscaledTime >= m_reloadAt.Value)
            {
                m_reloadAt = null;
                ReloadMapping();
            }
        }

        /// <summary>
        /// Reads the other mods' keybinding settings once, when the main menu first opens (every
        /// plugin has loaded and bound its config by then), logs each key the mapping lacks and
        /// updates "Keys missing from mapping".
        /// </summary>
        private void AuditModShortcuts()
        {
            GUIManager.OnCustomGUIAvailable -= AuditModShortcuts;
            m_modShortcuts.Read();

            IReadOnlyList<MissingKey> missing = MissingKeys.Find(Array.Empty<HintKeyUse>(), m_modShortcuts.Items, m_glyphs.KeyMap);
            Jotunn.Logger.LogInfo($"Read {m_modShortcuts.Items.Count} keybinding settings from other mods; " +
                                  $"{missing.Count} of their keys aren't in your mapping");
            foreach (MissingKey key in missing)
            {
                Jotunn.Logger.LogInfo(MissingKeys.LogLine(key));
            }

            ShowMissingKeys();
        }

        /// <summary>
        /// Reads the mapping file again; on success swaps the mapping in and redraws everything that
        /// shows buttons. On failure the old mapping stays and the status shows the error.
        /// </summary>
        /// <returns>The new status line.</returns>
        private string ReloadMapping()
        {
            if (m_mappingFile.TryLoad(out KeyMap map))
            {
                m_glyphs.ReplaceKeyMap(map);
                RedrawGlyphs(this, EventArgs.Empty);
                ShowMissingKeys();
            }

            return m_mappingFile.Status;
        }

        /// <summary>
        /// Updates "Keys missing from mapping" from the hover hints seen so far, the keybindings read
        /// at startup, and the mapping in use.
        /// </summary>
        private void ShowMissingKeys()
        {
            IEnumerable<HintKeyUse> hints = m_hoverTextWriter != null
                ? m_hoverTextWriter.MissingKeys
                : Array.Empty<HintKeyUse>();
            m_mappingFile.ShowMissingKeys(MissingKeys.Find(hints, m_modShortcuts.Items, m_glyphs.KeyMap));
        }

        /// <summary>
        /// Binds the read-only Key Glyph Mapping settings and creates the mapping file handler on them.
        /// </summary>
        /// <returns>The mapping file handler (not loaded yet).</returns>
        private MappingFile BindMappingFile()
        {
            ConfigEntry<string> file = Config.Bind(SettingNames.KeyGlyphMapping, SettingNames.MappingFile, "", Describe(
                "Which controller buttons send each key in your Steam layout. Saved edits apply while playing.",
                order: 3, readOnly: true));
            ConfigEntry<string> status = Config.Bind(SettingNames.KeyGlyphMapping, SettingNames.MappingStatus, "",
                Describe("Whether the mapping file loaded, or its error and line.",
                order: 2, readOnly: true, drawer: DrawMappingStatus));
            ConfigEntry<string> missingKeys = Config.Bind(SettingNames.KeyGlyphMapping,
                SettingNames.KeysMissingFromMapping, MappingStatus.None, Describe(
                "Keys in hover hints and other mods' keybindings that your mapping lacks (a hint's mod is a best " +
                "guess). Add only keys your Steam layout sends.", order: 1, readOnly: true,
                drawer: m_missingKeysDrawer.Draw));
            return new MappingFile(MappingPath, file, status, missingKeys);
        }

        /// <summary>
        /// Draws "Mapping status" in shudnal's ConfigurationManager: the status line and a Reload button.
        /// Jotunn's Mod Settings ignores custom drawers and shows the plain read-only text.
        /// </summary>
        /// <param name="entry">The Status entry.</param>
        // ConfigurationManager disables the GUI for read-only entries (its "Style readonly entries"
        // setting), and its edit window passes a copy of the entry made when the window opened. So
        // the label reads the live status, and the button turns the GUI back on for itself. The
        // reload runs in the next Update, not mid-draw, so the window's layout doesn't change under it.
        private void DrawMappingStatus(ConfigEntryBase entry)
        {
            UnityEngine.GUILayout.BeginVertical();
            UnityEngine.GUILayout.Label(m_mappingFile.Status);

            bool wasEnabled = UnityEngine.GUI.enabled;
            UnityEngine.GUI.enabled = true;
            if (UnityEngine.GUILayout.Button("Reload mapping"))
            {
                m_reloadAt = UnityEngine.Time.unscaledTime;
            }

            UnityEngine.GUI.enabled = wasEnabled;
            UnityEngine.GUILayout.EndVertical();
        }

        /// <summary>
        /// Builds a setting's description with its place in Mod Config.
        /// </summary>
        /// <param name="text">Description shown in the settings UIs and the .cfg.</param>
        /// <param name="order">Higher is listed first within the section.</param>
        /// <param name="readOnly">True for values the mod sets (no editing, no reset button, and the
        /// tooltip starts with "Read-only." since the .cfg and some UIs can't enforce it).</param>
        /// <param name="drawer">Custom drawer for ConfigurationManager, or null for the default.</param>
        /// <returns>The description.</returns>
        private static ConfigDescription Describe(string text, int order, bool readOnly = false,
            Action<ConfigEntryBase> drawer = null)
        {
            return new (readOnly ? ReadOnlyPrefix + text : text, null, new ConfigurationManagerAttributes
            {
                Order = order,
                ReadOnly = readOnly,
                HideDefaultButton = readOnly,
                CustomDrawer = drawer,
            });
        }

        /// <summary>
        /// Redraws the prefixes after a prefix setting changes; they're otherwise only redrawn when
        /// the slots or glyph state change.
        /// </summary>
        /// <param name="sender">The setting.</param>
        /// <param name="args">Unused.</param>
        private void RedrawPrefixes(object sender, EventArgs args)
        {
            HotbarLabelsPatch.Writer?.Invalidate();
            InventoryLabelsPatch.Writer?.Invalidate();
        }

        /// <summary>
        /// Applies one patch class; a missing target (e.g. after a Valheim update) disables only
        /// that feature, with a warning, instead of the whole mod.
        /// </summary>
        /// <param name="patchClass">A class with [HarmonyPatch] attributes.</param>
        /// <returns>True if the patch applied.</returns>
        private bool ApplyPatch(Type patchClass)
        {
            try
            {
                m_harmony.CreateClassProcessor(patchClass).Patch();
                return true;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"{patchClass.Name} not applied, feature disabled: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logs gamepads already connected when the plugin loads. Steam Input's virtual controller
        /// usually connects later (around character selection); <see cref="OnDeviceChange"/> logs that.
        /// </summary>
        private void LogGamepads()
        {
            var gamepads = InputSystem.devices.OfType<Gamepad>().ToList();
            if (gamepads.Count == 0)
            {
                Jotunn.Logger.LogInfo("No gamepad connected yet; controller glyphs turn on when one connects");
                return;
            }

            foreach (Gamepad gamepad in gamepads)
            {
                Jotunn.Logger.LogInfo($"Gamepad: {gamepad.displayName} ({gamepad.layout})");
            }
        }

        /// <summary>
        /// Logs gamepads connecting and disconnecting, so the log shows when glyphs turn on or off.
        /// </summary>
        /// <param name="device">The device that changed.</param>
        /// <param name="change">What happened to it.</param>
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Gamepad)
            {
                return;
            }

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                Jotunn.Logger.LogInfo($"Gamepad connected: {device.displayName} ({device.layout})");
                RedrawGlyphs(this, EventArgs.Empty);
            }
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                Jotunn.Logger.LogInfo($"Gamepad disconnected: {device.displayName} ({device.layout})");
                RedrawGlyphs(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Redraws every glyph when a setting that decides which glyphs show changes, so text Valheim
        /// cached (chest and fire hints) follows it at once instead of at the next input switch.
        /// </summary>
        /// <param name="sender">The config file.</param>
        /// <param name="args">Which setting changed.</param>
        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (m_glyphSettings.Contains(args.ChangedSetting))
            {
                RedrawGlyphs(sender, args);
            }
        }
    }
}
