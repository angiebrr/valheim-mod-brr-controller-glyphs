// ------------------------------------------------------------------------------------------------
// ControllerGlyphFixer.cs: BepInEx plugin entry point (config, mapping file, patches, commands).
// ------------------------------------------------------------------------------------------------

using System;
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

        /// <summary>Path of the YAML key mapping file next to the plugin's .cfg.</summary>
        private string MappingPath => Path.Combine(Paths.ConfigPath, PluginGUID + ".mapping.yaml");

        /// <summary>
        /// Called by Unity when BepInEx loads the plugin: builds every object with its
        /// dependencies, then hands them to the patches and commands that use them.
        /// </summary>
        // TODO(before sharing): sections and ConfigurationManagerAttributes.Order for Mod Config (spec: Phases).
        private void Awake()
        {
            ConfigEntry<bool> enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When off, prompts behave like vanilla.");
            ConfigEntry<bool> keyPrompts = Config.Bind("General", "KeyPrompts", true,
                "While a gamepad is connected, show controller glyphs in $KEY_ prompts and hint panels even after " +
                "keyboard input, and show keyboard-only keys as the Steam Input buttons from the mapping file.");
            ConfigEntry<bool> hotbarLabels = Config.Bind("General", "HotbarLabels", true,
                "While a gamepad is connected, label hotbar and AzuExtendedPlayerInventory quick slots with the " +
                "Steam Input buttons from the mapping file instead of keyboard keys or nothing.");
            ConfigEntry<PrefixColorMode> prefixColors = Config.Bind("General", "PrefixColors", PrefixColorMode.Neutral,
                "Colour of the prefix and line beside each group of slots. Neutral: the slot-label " +
                "colour. Palette: a built-in colour per prefix button. Custom: the mapping file's colors section.");
            ConfigEntry<Color> prefixBackground = Config.Bind("General", "PrefixBackground", new Color(0f, 0f, 0f, 0.7f),
                "Colour and transparency of the rounded backing behind each group prefix (and its line, for a row). " +
                "Set the alpha to 0 to hide it.");

            ConfigEntry<bool> hoverText = Config.Bind("General", "HoverText", true,
                "While controller glyphs are on, show key names that mods write in hover hints (e.g. [B] AutoFuel) as " +
                "the Steam Input buttons from the mapping file. Needs KeyPrompts on. Keys missing from the mapping are " +
                "listed in the log.");

            ConfigEntry<ButtonGlyphOrder> buttonGlyphs = Config.Bind("General", "ButtonGlyphs", ButtonGlyphOrder.ValheimFirst,
                "Which art shows for controller buttons in hotbar labels, prefixes and hover hints. ValheimFirst: " +
                "Valheim's glyphs where it has them, this mod's Steam Deck glyphs for the rest (R4/R5, L4/L5, " +
                "trackpads). ModGlyphsForAll: this mod's glyphs for every button. Buttons with no art show as [text].");

            m_prefixColors = prefixColors;
            m_prefixBackground = prefixBackground;
            m_buttonGlyphs = buttonGlyphs;

            // Built before the services that format buttons; without it, buttons fall back to [badges]
            m_modSprites = ModSpriteAsset.TryCreate();
            Func<string, string> modGlyph = m_modSprites != null ? ModGlyphs.Tag : null;
            Func<string, string> hintModGlyph = m_modSprites != null ? button => ModGlyphs.Tag(button, HintGlyphPercent, HintGlyphPaddingEm) : null;

            var glyphs = new GlyphService(LoadKeyMap(), enabled, keyPrompts, hotbarLabels, prefixColors, hoverText,
                buttonGlyphs, modGlyph, hintModGlyph);

            BoundKeyStringPatch.Glyphs = glyphs;
            ApplyPatch(typeof(BoundKeyStringPatch));
            InputHintPatch.Glyphs = glyphs;
            ApplyPatch(typeof(InputHintPatch));

            var hoverTextWriter = new HoverTextWriter(glyphs);
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

                labels = new HotbarLabelWriter(glyphs, quickSlots, prefixLabels);
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
                new SelfTestCommand(glyphs, labels, inventoryLabels, quickSlots, prefixLabels, hoverTextWriter, m_modSprites));

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
            HotbarLabelsPatch.Writer = null;
            InventoryLabelsPatch.Writer = null;
            HoverTextPatch.Writer = null;
            RoundedSprite.Destroy(m_backingSprite);
            m_modSprites?.Destroy();
        }

        /// <summary>
        /// Redraws everything showing button glyphs after ButtonGlyphs changes: bars and prefixes, and
        /// the hover text (its writer caches the last rewrite).
        /// </summary>
        /// <param name="sender">The setting.</param>
        /// <param name="args">Unused.</param>
        // Known limit: $KEY_ text Valheim's Localization cached updates on the next input-mode switch
        private void RedrawGlyphs(object sender, EventArgs args)
        {
            RedrawPrefixes(sender, args);
            HoverTextPatch.Writer?.Invalidate();
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
        /// Loads the mapping file, writing the commented template first if it doesn't exist.
        /// </summary>
        /// <returns>The parsed map, or an empty map if the file can't be read or parsed.</returns>
        // TODO: reload the mapping when the file changes (FileSystemWatcher) or via a console
        // command, so edits don't need a game restart. Localization caches text, so clear it too.
        private KeyMap LoadKeyMap()
        {
            try
            {
                if (!File.Exists(MappingPath))
                {
                    File.WriteAllText(MappingPath, KeyMapTemplate.Yaml);
                    Jotunn.Logger.LogInfo($"Wrote an empty key mapping template to {MappingPath}; edit it to add your buttons");
                }

                KeyMap map = KeyMap.Parse(File.ReadAllText(MappingPath));
                Jotunn.Logger.LogInfo($"Loaded {map.Count} key mappings from {MappingPath}");
                return map;
            }
            catch (Exception ex) when (ex is KeyMapException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Jotunn.Logger.LogError($"Couldn't load key mapping {MappingPath}: {ex.Message}. " +
                                       "Controller glyphs still work; keyboard-only keys stay as keys.");
                return KeyMap.Empty;
            }
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
