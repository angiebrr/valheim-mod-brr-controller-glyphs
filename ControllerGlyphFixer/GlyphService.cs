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

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="keyMap">The loaded key mapping (<see cref="KeyMap.Empty"/> if none).</param>
        /// <param name="enabled">Config master switch; read live so changes apply immediately.</param>
        /// <param name="keyPrompts">Config switch for F1 + F2; read live.</param>
        internal GlyphService(KeyMap keyMap, ConfigEntry<bool> enabled, ConfigEntry<bool> keyPrompts)
        {
            m_keyMap = keyMap ?? throw new ArgumentNullException(nameof(keyMap));
            m_enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
            m_keyPrompts = keyPrompts ?? throw new ArgumentNullException(nameof(keyPrompts));
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
        internal bool ShowKeyPrompts => GlyphMode.Decide(
            enabled: m_enabled.Value && m_keyPrompts.Value,
            gamepadEnabled: ZInput.IsGamepadEnabled(),
            gamepadPresent: Gamepad.current != null,
            settingsOpen: Settings.instance != null);

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
