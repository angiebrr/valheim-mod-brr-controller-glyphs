namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Mirrors how ZInput.GetBoundKeyString picks Valheim's glyph sprite atlas, so our glyphs
    /// match the game's. Takes enum names as strings to stay free of Valheim types.
    /// </summary>
    public static class SpriteStyle
    {
        public const string Xbox = "xbox";
        public const string PlayStation = "ps5";
        public const string Switch = "switch2";

        /// <param name="glyphSetting">ZInput.CurrentGlyph (GamepadGlyphs) name.</param>
        /// <param name="gamepadType">ZInput.ConnectedGamepadType (GamepadType) name.</param>
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

        public static string Tag(string style, string spriteName)
        {
            return "<sprite=\"" + style + "\" name=\"" + spriteName + "\">";
        }

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
