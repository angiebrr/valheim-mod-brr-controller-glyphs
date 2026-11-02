// ------------------------------------------------------------------------------------------------
// Glyphs.cs: reads game state for the Core glyph decisions (the thin shell around Core).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using ControllerGlyphFixer.Core;
using UnityEngine.InputSystem;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Reads game state for the Core decisions. All logic lives in Core; keep this thin.
    /// </summary>
    internal static class Glyphs
    {
        /// <summary>The loaded key mapping; empty until the plugin loads the mapping file.</summary>
        internal static KeyMap KeyMap = KeyMap.Parse("");

        /// <summary>Whether $KEY_ prompts should show controller glyphs right now (F1 + F2).</summary>
        // FIXME(open question 1): if Steam Input doesn't expose a virtual gamepad, Gamepad.current is
        // null and glyphs stay off. Check the startup log / cgf_selftest; if so, switch to another
        // signal such as "gamepad input seen this session".
        internal static bool ShowKeyPrompts => GlyphMode.Decide(
            enabled: ControllerGlyphFixer.Enabled.Value && ControllerGlyphFixer.KeyPrompts.Value,
            gamepadEnabled: ZInput.IsGamepadEnabled(),
            gamepadPresent: Gamepad.current != null,
            settingsOpen: Settings.instance != null);

        /// <summary>
        /// Translates a keyboard key through the mapping, using Valheim's glyphs where it has art.
        /// </summary>
        /// <param name="key">Key name as Valheim returned it (e.g. "Q", "$button_lshift").</param>
        /// <returns>The mapped display string, or null if the key isn't mapped.</returns>
        internal static string TranslateKey(string key)
        {
            return KeyMap.Translate(key, ValheimGlyph);
        }

        /// <summary>
        /// Returns Valheim's sprite tag for a control name, in the same style the game uses.
        /// </summary>
        /// <param name="control">Valheim control name (e.g. "buttonSouth").</param>
        /// <returns>A sprite tag, or null if Valheim has no sprite for the control.</returns>
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
