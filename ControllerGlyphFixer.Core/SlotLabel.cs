// ------------------------------------------------------------------------------------------------
// SlotLabel.cs: decides the label of a hotbar or quick slot from its keyboard shortcut (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Builds the label for a hotbar slot (Valheim's or AzuEPI's quick slots) from the keyboard
    /// shortcut that uses it, translated through the key mapping.
    /// </summary>
    public static class SlotLabel
    {
        /// <summary>Joins modifiers and the main key, e.g. "LeftAlt + [R5] + (glyph)".</summary>
        private const string KeySeparator = " + ";

        /// <summary>
        /// Returns the label for a slot, or null to leave the label Valheim or AzuEPI set.
        /// </summary>
        /// <param name="mainKey">The slot's key name (e.g. "1", "Keypad1"); null or empty if none.</param>
        /// <param name="modifiers">Modifier keys held with it (e.g. "LeftAlt"); may be empty or null.</param>
        /// <param name="showGlyphs">Whether controller glyphs are on for slot labels.</param>
        /// <param name="translateKey">Returns the mapped display string for a key, or null if unmapped.</param>
        /// <returns>The label, or null when glyphs are off, there's no key, or the main key isn't mapped.</returns>
        public static string For(
            string mainKey,
            IReadOnlyList<string> modifiers,
            bool showGlyphs,
            Func<string, string> translateKey)
        {
            if (!showGlyphs || string.IsNullOrEmpty(mainKey))
            {
                return null;
            }

            string main = translateKey(mainKey);
            if (main == null)
            {
                return null;
            }

            // Unmapped modifiers keep their key name so the label still says what to press
            IEnumerable<string> parts = (modifiers ?? Array.Empty<string>()).Select(modifier => translateKey(modifier) ?? modifier).Append(main);
            return string.Join(KeySeparator, parts);
        }
    }
}
