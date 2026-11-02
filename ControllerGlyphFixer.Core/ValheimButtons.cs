using System.Collections.Generic;

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Button names usable in the mapping file that Valheim has glyph art for, mapped to the
    /// control names Valheim's sprite table uses (ZInput.s_gamepadSpriteMap keys).
    /// Any other button name is shown as a text badge.
    /// </summary>
    public static class ValheimButtons
    {
        private static readonly Dictionary<string, string> Controls = new Dictionary<string, string>
        {
            { "a", "buttonSouth" },
            { "b", "buttonEast" },
            { "x", "buttonWest" },
            { "y", "buttonNorth" },
            { "lb", "leftShoulder" },
            { "rb", "rightShoulder" },
            { "lt", "leftTrigger" },
            { "rt", "rightTrigger" },
            { "ls", "leftStickPress" },
            { "rs", "rightStickPress" },
            { "back", "select" },
            { "select", "select" },
            { "start", "start" },
            { "dpadup", "dpad_up" },
            { "dpadright", "dpad_right" },
            { "dpaddown", "dpad_down" },
            { "dpadleft", "dpad_left" },
        };

        /// <param name="normalizedButton">Lowercase button name without spaces, '_' or '-'.</param>
        public static bool TryGetControl(string normalizedButton, out string control)
        {
            return Controls.TryGetValue(normalizedButton, out control);
        }
    }
}
