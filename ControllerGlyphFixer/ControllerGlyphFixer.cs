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
        /// <summary>BepInEx plugin ID; also names the config and mapping files. Don't change it.</summary>
        public const string PluginGUID = "com.angiebrr.controllerglyphfixer";

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

        /// <summary>Config General.PrefixColors; kept to unsubscribe on destroy.</summary>
        private ConfigEntry<PrefixColorMode> m_prefixColors;

        /// <summary>Config General.PrefixBackground; kept to unsubscribe on destroy.</summary>
        private ConfigEntry<Color> m_prefixBackground;

        /// <summary>The rounded prefix backing, created in Awake and destroyed with the plugin.</summary>
        private Sprite m_backingSprite;

        /// <summary>Config General.ButtonGlyphs, kept to unsubscribe in OnDestroy.</summary>
        private ConfigEntry<ButtonGlyphOrder> m_buttonGlyphs;

        /// <summary>The mod's glyph sprite asset (F5); null if it couldn't be built.</summary>
        private ModSpriteAsset m_modSprites;

        /// <summary>Moves Valheim's top-left messages below prefixes under AzuEPI's bar; undone in OnDestroy.</summary>
        private MessageRoom m_messageRoom;

        /// <summary>
        /// Seconds to wait after the mapping file changes before reading it: editors often save in
        /// several writes (truncate, write, rename), and reading in between sees a partial file.
        /// </summary>
        private const float MappingReloadDelaySeconds = 0.5f;

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

        /// <summary>Other mods' shortcut settings, read once at the first main menu; null until then.</summary>
        private List<ModShortcut> m_modShortcuts;

        /// <summary>Path of the YAML key mapping file next to the plugin's .cfg.</summary>
        private string MappingPath => Path.Combine(Paths.ConfigPath, PluginGUID + ".mapping.yaml");

        /// <summary>
        /// Called by Unity when BepInEx loads the plugin: builds every object with its
        /// dependencies, then hands them to the patches and commands that use them.
        /// </summary>
        private void Awake()
        {
            // Order counts down so Mod Config lists each section's settings as written here
            ConfigEntry<bool> enabled = Config.Bind(SettingMoves.General, "Enabled", true, Describe(
                "Master switch. When off, prompts behave like vanilla.", order: 4));
            ConfigEntry<bool> keyPrompts = Config.Bind(SettingMoves.General, "KeyPrompts", true, Describe(
                "While a gamepad is connected, show controller glyphs in $KEY_ prompts and hint panels even after " +
                "keyboard input, and show keyboard-only keys as the Steam Input buttons from the mapping file.", order: 3));
            ConfigEntry<bool> hoverText = Config.Bind(SettingMoves.General, "HoverText", true, Describe(
                "While controller glyphs are on, show key names that mods write in hover hints (e.g. [B] AutoFuel) as " +
                "the Steam Input buttons from the mapping file. Needs KeyPrompts on. Keys missing from the mapping are " +
                "listed under Mapping > MissingKeys.", order: 2));
            ConfigEntry<ButtonGlyphOrder> buttonGlyphs = Config.Bind(SettingMoves.General, "ButtonGlyphs",
                ButtonGlyphOrder.ValheimFirst, Describe(
                "Which art shows for controller buttons in hotbar labels, prefixes and hover hints. ValheimFirst: " +
                "Valheim's glyphs where it has them, this mod's Steam Deck glyphs for the rest (R4/R5, L4/L5, " +
                "trackpads). ModGlyphsForAll: this mod's glyphs for every button. Buttons with no art show as [text].",
                order: 1));

            ConfigEntry<bool> hotbarLabels = Config.Bind(SettingMoves.HotbarLabels, "HotbarLabels", true, Describe(
                "While a gamepad is connected, label hotbar and AzuExtendedPlayerInventory quick slots with the " +
                "Steam Input buttons from the mapping file instead of keyboard keys or nothing.", order: 3));
            ConfigEntry<PrefixColorMode> prefixColors = Config.Bind(SettingMoves.HotbarLabels, "PrefixColors",
                PrefixColorMode.Neutral, Describe(
                "Colour of the prefix and line beside each group of slots. Neutral: the slot-label " +
                "colour. Palette: a built-in colour per prefix button. Custom: the mapping file's colors section.",
                order: 2));
            ConfigEntry<Color> prefixBackground = Config.Bind(SettingMoves.HotbarLabels, "PrefixBackground",
                new Color(0f, 0f, 0f, 0.7f), Describe(
                "Colour and transparency of the rounded backing behind each group prefix (and its line, for a row). " +
                "Set the alpha to 0 to hide it.", order: 1));

            CarryOverMovedSettings();

            m_prefixColors = prefixColors;
            m_prefixBackground = prefixBackground;
            m_buttonGlyphs = buttonGlyphs;

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

            var hoverTextWriter = new HoverTextWriter(glyphs);
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

            buttonGlyphs.SettingChanged += RedrawGlyphs;

            CommandManager.Instance.AddConsoleCommand(
                new SelfTestCommand(glyphs, labels, inventoryLabels, quickSlots, prefixLabels, hoverTextWriter, m_modSprites,
                    m_mappingFile));
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

            if (m_buttonGlyphs != null)
            {
                m_buttonGlyphs.SettingChanged -= RedrawGlyphs;
            }

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
        /// Redraws everything showing button glyphs after ButtonGlyphs changes: bars and prefixes, and
        /// the hover text (its writer caches the last rewrite).
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
        /// Reads the other mods' shortcut settings once, when the main menu first opens (every
        /// plugin has loaded and bound its config by then), logs each key the mapping lacks and
        /// shows them in Mapping.UnmappedShortcuts.
        /// </summary>
        private void AuditModShortcuts()
        {
            GUIManager.OnCustomGUIAvailable -= AuditModShortcuts;
            m_modShortcuts = ModShortcuts.Read(PluginGUID);

            IReadOnlyList<UnmappedShortcutKey> unmapped = ShortcutAudit.Unmapped(m_modShortcuts, m_glyphs.KeyMap);
            Jotunn.Logger.LogInfo($"Read {m_modShortcuts.Count} keyboard shortcut settings from other mods; " +
                                  $"{unmapped.Count} of their keys aren't in your mapping");
            foreach (UnmappedShortcutKey key in unmapped)
            {
                Jotunn.Logger.LogInfo(ShortcutAudit.LogLine(key));
            }

            m_mappingFile.ShowUnmappedShortcuts(unmapped);
        }

        /// <summary>
        /// Updates Mapping.UnmappedShortcuts against the mapping in use, from the shortcuts read at
        /// startup (not read again).
        /// </summary>
        private void ShowUnmappedShortcuts()
        {
            if (m_modShortcuts != null)
            {
                m_mappingFile.ShowUnmappedShortcuts(ShortcutAudit.Unmapped(m_modShortcuts, m_glyphs.KeyMap));
            }
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
                ShowUnmappedShortcuts();
            }

            return m_mappingFile.Status;
        }

        /// <summary>
        /// Updates Mapping.MissingKeys from the hover text writer and the mapping in use.
        /// </summary>
        private void ShowMissingKeys()
        {
            if (m_hoverTextWriter != null)
            {
                m_mappingFile.ShowMissingKeys(m_hoverTextWriter.MissingKeys, m_glyphs.KeyMap);
            }
        }

        /// <summary>
        /// Binds the read-only Mapping settings and creates the mapping file handler on them.
        /// </summary>
        /// <returns>The mapping file handler (not loaded yet).</returns>
        private MappingFile BindMappingFile()
        {
            ConfigEntry<string> file = Config.Bind(SettingMoves.Mapping, "File", "", Describe(
                "The key mapping file: which controller buttons send each keyboard key in your Steam Input layout. " +
                "Edit it in a text editor (or ConfigurationManager's File Editor); saves apply while playing. " +
                "Set by the mod.", order: 4, readOnly: true));
            ConfigEntry<string> status = Config.Bind(SettingMoves.Mapping, "Status", "", Describe(
                "Whether the mapping file loaded, or what's wrong with it and on which line. Set by the mod.",
                order: 3, readOnly: true, drawer: DrawMappingStatus));
            ConfigEntry<string> missingKeys = Config.Bind(SettingMoves.Mapping, "MissingKeys", MappingStatus.None, Describe(
                "Keys that hover hints used this session but your mapping doesn't have, with the hint they're in. " +
                "Add the ones your Steam layout sends to show their buttons (Steam's controller configurator shows " +
                "which key each button sends). Set by the mod.", order: 2, readOnly: true));
            ConfigEntry<string> unmappedShortcuts = Config.Bind(SettingMoves.Mapping, "UnmappedShortcuts",
                MappingStatus.None, Describe(
                "Keys that other mods' keyboard shortcut settings use but your mapping doesn't have, with the mods " +
                "using them. Read once when the main menu first opens; the log names each setting. Most are " +
                "keyboard-only conveniences: add only the ones your Steam layout sends. Set by the mod.",
                order: 1, readOnly: true));
            return new MappingFile(MappingPath, file, status, missingKeys, unmappedShortcuts);
        }

        /// <summary>
        /// Draws Mapping.Status in shudnal's ConfigurationManager: the status line and a Reload button.
        /// Jotunn's Mod Settings ignores custom drawers and shows the plain read-only text.
        /// </summary>
        /// <param name="entry">The Status entry.</param>
        // ConfigurationManager disables the GUI for read-only entries (its "Style readonly entries"
        // setting), and its edit window passes a copy of the entry made when the window opened. So
        // the label reads the live status, and the button turns the GUI back on for itself.
        private void DrawMappingStatus(ConfigEntryBase entry)
        {
            UnityEngine.GUILayout.BeginVertical();
            UnityEngine.GUILayout.Label(m_mappingFile.Status);

            bool wasEnabled = UnityEngine.GUI.enabled;
            UnityEngine.GUI.enabled = true;
            if (UnityEngine.GUILayout.Button("Reload mapping"))
            {
                ReloadMapping();
            }

            UnityEngine.GUI.enabled = wasEnabled;
            UnityEngine.GUILayout.EndVertical();
        }

        /// <summary>
        /// Builds a setting's description with its place in Mod Config.
        /// </summary>
        /// <param name="text">Description shown in the settings UIs and the .cfg.</param>
        /// <param name="order">Higher is listed first within the section.</param>
        /// <param name="readOnly">True for values the mod sets (no editing, no reset button).</param>
        /// <param name="drawer">Custom drawer for ConfigurationManager, or null for the default.</param>
        /// <returns>The description.</returns>
        private static ConfigDescription Describe(string text, int order, bool readOnly = false,
            Action<ConfigEntryBase> drawer = null)
        {
            return new (text, null, new ConfigurationManagerAttributes
            {
                Order = order,
                ReadOnly = readOnly,
                HideDefaultButton = readOnly,
                CustomDrawer = drawer,
            });
        }

        /// <summary>
        /// Copies the values of settings that moved to another section (see <see cref="SettingMoves"/>)
        /// from their old .cfg entries, which BepInEx keeps as orphans, then drops the old entries.
        /// Runs once: after the save no old entries are left.
        /// </summary>
        // BepInEx's OrphanedEntries is private (contract-tested); without it, moved settings reset once
        private void CarryOverMovedSettings()
        {
            try
            {
                var orphans = (Dictionary<ConfigDefinition, string>)AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")
                    .GetValue(Config);
                IReadOnlyList<SettingMove> pending = SettingMoves.Pending(orphans.Keys.Select(definition => (definition.Section, definition.Key)));
                if (pending.Count == 0)
                {
                    return;
                }

                foreach (SettingMove move in pending)
                {
                    var oldDefinition = new ConfigDefinition(move.From, move.Key);
                    Config[move.To, move.Key].SetSerializedValue(orphans[oldDefinition]);
                    orphans.Remove(oldDefinition);
                    Jotunn.Logger.LogInfo($"Moved setting {move.Key} from [{move.From}] to [{move.To}], keeping its value");
                }

                Config.Save();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Couldn't carry moved settings over, they start at their defaults: {ex.Message}");
            }
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
            }
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                Jotunn.Logger.LogInfo($"Gamepad disconnected: {device.displayName} ({device.layout})");
            }
        }
    }
}
