// ------------------------------------------------------------------------------------------------
// GlyphService.cs: reads game state for the Core glyph decisions (the thin shell around Core).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ControllerGlyphFixer.Core;
using UnityEngine.InputSystem;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Reads game state for the Core decisions. All logic lives in Core; keep this thin.
    /// </summary>
    internal sealed class GlyphService
    {
        /// <summary>The loaded key mapping.</summary>
        private readonly KeyMap m_keyMap;

        /// <summary>Config General.Enabled: master switch.</summary>
        private readonly ConfigEntry<bool> m_enabled;

        /// <summary>Config General.KeyPrompts: controller glyphs in $KEY_ prompts (F1 + F2).</summary>
        private readonly ConfigEntry<bool> m_keyPrompts;

        /// <summary>Config General.HotbarLabels: mapped buttons on hotbar and quick slots (F3).</summary>
        private readonly ConfigEntry<bool> m_hotbarLabels;

        /// <summary>Config General.PrefixColors: how group prefixes and lines are coloured (F3).</summary>
        private readonly ConfigEntry<PrefixColorMode> m_prefixColors;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="keyMap">The loaded key mapping (<see cref="KeyMap.Empty"/> if none).</param>
        /// <param name="enabled">Config master switch; read live so changes apply immediately.</param>
        /// <param name="keyPrompts">Config switch for F1 + F2; read live.</param>
        /// <param name="hotbarLabels">Config switch for F3; read live.</param>
        /// <param name="prefixColors">Config colour mode for group prefixes; read live.</param>
        internal GlyphService(KeyMap keyMap, ConfigEntry<bool> enabled, ConfigEntry<bool> keyPrompts,
            ConfigEntry<bool> hotbarLabels, ConfigEntry<PrefixColorMode> prefixColors)
        {
            m_keyMap = keyMap ?? throw new ArgumentNullException(nameof(keyMap));
            m_enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
            m_keyPrompts = keyPrompts ?? throw new ArgumentNullException(nameof(keyPrompts));
            m_hotbarLabels = hotbarLabels ?? throw new ArgumentNullException(nameof(hotbarLabels));
            m_prefixColors = prefixColors ?? throw new ArgumentNullException(nameof(prefixColors));
        }

        /// <summary>Number of keys in the loaded mapping.</summary>
        internal int MappedKeyCount => m_keyMap.Count;

        /// <summary>
        /// Whether $KEY_ prompts should show controller glyphs right now (F1 + F2). Computed on each
        /// read, not set at construction, because its inputs change while the game runs (controller
        /// plugged in, Settings opened, config toggled).
        /// </summary>
        // Steam Input exposes a virtual XInput controller ("Xbox Controller"), but it connects after
        // plugins load, so Gamepad.current is null at startup and becomes set later (checked 2026-11-02).
        internal bool ShowKeyPrompts => ShowGlyphs(m_keyPrompts.Value);

        /// <summary>
        /// Whether hotbar and quick-slot labels should show mapped controller buttons right now (F3).
        /// Computed on each read, like <see cref="ShowKeyPrompts"/>.
        /// </summary>
        internal bool ShowHotbarLabels => ShowGlyphs(m_hotbarLabels.Value);

        /// <summary>
        /// Shared live check for every feature: master switch, the feature's switch, and game state.
        /// </summary>
        /// <param name="featureEnabled">The feature's own config switch.</param>
        /// <returns>Result of <see cref="GlyphMode.Decide"/>.</returns>
        private bool ShowGlyphs(bool featureEnabled)
        {
            return GlyphMode.Decide(
                enabled: m_enabled.Value && featureEnabled,
                gamepadEnabled: ZInput.IsGamepadEnabled(),
                gamepadPresent: Gamepad.current != null,
                settingsOpen: Settings.instance != null);
        }

        /// <summary>
        /// Translates a keyboard key through the mapping, using Valheim's glyphs where it has art.
        /// </summary>
        /// <param name="key">Key name as Valheim returned it (e.g. "Q", "$button_lshift").</param>
        /// <returns>The mapped display string, or null if the key isn't mapped.</returns>
        internal string TranslateKey(string key)
        {
            return m_keyMap.Translate(key, ValheimGlyph);
        }

        /// <summary>
        /// Returns the buttons pressed for a keyboard key, as written in the mapping file.
        /// </summary>
        /// <param name="key">Key name in any supported spelling.</param>
        /// <returns>The button names, or null if the key isn't mapped.</returns>
        internal IReadOnlyList<string> ButtonsFor(string key)
        {
            return m_keyMap.Buttons(key);
        }

        /// <summary>
        /// Renders buttons with Valheim's glyphs where it has art, like <see cref="TranslateKey"/>.
        /// </summary>
        /// <param name="buttons">Button names as written in the mapping file.</param>
        /// <returns>The display string, or null for a null or empty list.</returns>
        internal string FormatButtons(IReadOnlyList<string> buttons)
        {
            return m_keyMap.Format(buttons, ValheimGlyph);
        }

        /// <summary>
        /// Returns the colour of a group's prefix and line, per the current colour mode.
        /// </summary>
        /// <param name="prefix">The group's prefix buttons.</param>
        /// <returns>A colour (#RRGGBB or #RRGGBBAA), or null for the slot-label colour.</returns>
        internal string PrefixColor(IReadOnlyList<string> prefix)
        {
            return PrefixColors.For(m_prefixColors.Value, prefix, m_keyMap);
        }

        /// <summary>
        /// Returns Valheim's sprite tag for a control name, in the same style the game uses.
        /// </summary>
        /// <param name="control">Valheim control name (e.g. "buttonSouth").</param>
        /// <returns>A sprite tag, or null if Valheim has no sprite for the control.</returns>
        private static string ValheimGlyph(string control)
        {
            string style = SpriteStyle.For(ZInput.CurrentGlyph.ToString(), ZInput.ConnectedGamepadType.ToString());
            if (ZInput.s_gamepadSpriteMap.TryGetValue(control, out Dictionary<string, string> styles) &&
                styles.TryGetValue(style, out string spriteName))
            {
                return SpriteStyle.Tag(style, spriteName);
            }

            return null;
        }
    }
}
