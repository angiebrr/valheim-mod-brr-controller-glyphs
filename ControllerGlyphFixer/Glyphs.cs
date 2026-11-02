using System.Collections.Generic;
using ControllerGlyphFixer.Core;
using UnityEngine.InputSystem;

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Reads game state for the Core decisions. All logic lives in Core; keep this thin.
    /// </summary>
    internal static class Glyphs
    {
        internal static KeyMap KeyMap = KeyMap.Parse("");

        internal static bool ShowKeyPrompts => GlyphMode.Decide(
            enabled: ControllerGlyphFixer.Enabled.Value && ControllerGlyphFixer.KeyPrompts.Value,
            gamepadEnabled: ZInput.IsGamepadEnabled(),
            gamepadPresent: Gamepad.current != null,
            settingsOpen: Settings.instance != null);

        internal static string TranslateKey(string key)
        {
            return KeyMap.Translate(key, ValheimGlyph);
        }

        /// <summary>
        /// Valheim's sprite tag for a control name (e.g. "buttonSouth"), in the same style the game uses.
        /// </summary>
        internal static string ValheimGlyph(string control)
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
