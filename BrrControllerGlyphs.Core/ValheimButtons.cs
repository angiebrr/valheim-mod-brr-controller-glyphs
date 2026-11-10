// ------------------------------------------------------------------------------------------------
// ValheimButtons.cs: mapping-file button names that Valheim has glyph art for.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Button names usable in the mapping file that Valheim has glyph art for, mapped to the
    /// control names Valheim's sprite table uses (ZInput.s_gamepadSpriteMap keys).
    /// Any other button name is shown as a text badge.
    /// </summary>
    public static class ValheimButtons
    {
        /// <summary>Normalized mapping-file button name to Valheim control name.</summary>
        // TODO: add stick directions (leftStick_up, rightStick_left, ...) and the PlayStation
        // touchpad (touchpadButton); Valheim has art for them but no layout has needed them yet.
        private static readonly Dictionary<string, string> Controls = new ()
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

        /// <summary>Every normalized button name with Valheim art, aliases included (for the template's list).</summary>
        public static IReadOnlyCollection<string> ButtonNames => Controls.Keys;

        /// <summary>
        /// Looks up the Valheim control name for a mapping-file button name.
        /// </summary>
        /// <param name="normalizedButton">Lowercase button name without spaces, '_' or '-'.</param>
        /// <param name="control">The Valheim control name (e.g. "buttonSouth") when found.</param>
        /// <returns>True if Valheim has art for the button.</returns>
        public static bool TryGetControl(string normalizedButton, out string control)
        {
            return Controls.TryGetValue(normalizedButton, out control);
        }
    }
}
