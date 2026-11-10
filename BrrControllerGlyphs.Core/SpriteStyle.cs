// ------------------------------------------------------------------------------------------------
// SpriteStyle.cs: picks Valheim's glyph sprite atlas (xbox, ps5, switch2) and builds sprite tags.
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Mirrors how ZInput.GetBoundKeyString picks Valheim's glyph sprite atlas, so our glyphs
    /// match the game's. Takes enum names as strings to stay free of Valheim types.
    /// </summary>
    // FIXME: this copies a switch from ZInput.GetBoundKeyString. Contract tests check the enum
    // names still exist, but not that Valheim still maps them the same way; re-check after updates.
    public static class SpriteStyle
    {
        /// <summary>Atlas name for Xbox glyphs (also Valheim's fallback).</summary>
        public const string Xbox = "xbox";

        /// <summary>Atlas name for PlayStation glyphs.</summary>
        public const string PlayStation = "ps5";

        /// <summary>Atlas name for Nintendo Switch glyphs.</summary>
        public const string Switch = "switch2";

        /// <summary>
        /// Returns the sprite atlas name Valheim would use for the current glyph setting.
        /// </summary>
        /// <param name="glyphSetting">ZInput.CurrentGlyph (GamepadGlyphs) name.</param>
        /// <param name="gamepadType">ZInput.ConnectedGamepadType (GamepadType) name, used when
        /// the setting is "Auto".</param>
        /// <returns>"xbox", "ps5" or "switch2"; "xbox" for anything unknown.</returns>
        // CurrentGlyph and ConnectedGamepadType stay correct while Valheim is in keyboard mode
        // (Auto + XInput -> xbox; checked in game 2026-11-02).
        public static string For(string glyphSetting, string gamepadType)
        {
            switch (glyphSetting)
            {
                case "Xbox":
                    return Xbox;
                case "Playstation":
                    return PlayStation;
                case "Switch":
                    return Switch;
                case "Auto":
                    return ForGamepad(gamepadType);
                default:
                    return Xbox;
            }
        }

        /// <summary>
        /// Builds a TextMeshPro sprite tag in the same format Valheim uses.
        /// </summary>
        /// <param name="style">Atlas name from <see cref="For"/>.</param>
        /// <param name="spriteName">Sprite name inside the atlas (e.g. "button_a").</param>
        /// <returns>A tag such as &lt;sprite="xbox" name="button_a"&gt;.</returns>
        public static string Tag(string style, string spriteName)
        {
            return "<sprite=\"" + style + "\" name=\"" + spriteName + "\">";
        }

        /// <summary>
        /// Returns the atlas for the connected gamepad type when the glyph setting is "Auto".
        /// </summary>
        /// <param name="gamepadType">GamepadType enum name.</param>
        /// <returns>The matching atlas name; "xbox" for unknown or generic gamepads.</returns>
        private static string ForGamepad(string gamepadType)
        {
            switch (gamepadType)
            {
                case "DualSense":
                case "DualShock":
                    return PlayStation;
                case "SwitchPro":
                case "JoyCon":
                case "JoyCon2Mouse":
                    return Switch;
                default:
                    return Xbox;
            }
        }
    }
}
