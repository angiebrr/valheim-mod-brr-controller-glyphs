// ------------------------------------------------------------------------------------------------
// KeyFinder.cs: names the keys pressed while cgf_keylog is on, as the mapping file spells them (F9).
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Answers "which key does this controller button send?" in game: turns a Unity Input System
    /// key into the name to write in the mapping file, and says what the mapping has for it.
    /// </summary>
    public static class KeyFinder
    {
        /// <summary>Input System top-row digits ("Digit1"), written as the digit in the file.</summary>
        private static readonly Regex DigitKey = new (@"^Digit(\d)$");

        /// <summary>Input System numpad digits ("Numpad1"), written as "Keypad1" in the file.</summary>
        private static readonly Regex NumpadKey = new (@"^Numpad(\d)$");

        /// <summary>
        /// Returns the spelling the mapping file and its template use for a key.
        /// </summary>
        /// <param name="inputSystemKey">Name of a UnityEngine.InputSystem.Key value (e.g. "Numpad1").</param>
        /// <returns>"1" for Digit1, "Keypad1" for Numpad1, otherwise the name unchanged.</returns>
        public static string FileName(string inputSystemKey)
        {
            Match digit = DigitKey.Match(inputSystemKey);
            if (digit.Success)
            {
                return digit.Groups[1].Value;
            }

            Match numpad = NumpadKey.Match(inputSystemKey);
            return numpad.Success ? "Keypad" + numpad.Groups[1].Value : inputSystemKey;
        }

        /// <summary>
        /// Describes a pressed key: its buttons as written in the mapping, or that it's missing.
        /// </summary>
        /// <param name="inputSystemKey">Name of a UnityEngine.InputSystem.Key value.</param>
        /// <param name="map">The current mapping.</param>
        /// <returns>"Keypad1: R5 + DPadUp", or "Numpad7: Keypad7 isn't in your mapping".</returns>
        public static string Describe(string inputSystemKey, KeyMap map)
        {
            string fileName = FileName(inputSystemKey);
            var buttons = map.Buttons(fileName);
            if (buttons != null)
            {
                return $"{fileName}: {string.Join(" + ", buttons)}";
            }

            // Unity's name first when it differs, so users can tell which key was pressed
            string prefix = fileName == inputSystemKey ? "" : inputSystemKey + ": ";
            return $"{prefix}{fileName} isn't in your mapping";
        }
    }
}
