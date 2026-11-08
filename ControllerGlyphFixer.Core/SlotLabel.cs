// ------------------------------------------------------------------------------------------------
// SlotLabel.cs: finds the controller buttons that use a hotbar or quick slot (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Finds the controller buttons for a hotbar slot (Valheim's or AzuEPI's quick slots) from the
    /// keyboard shortcut that uses it. <see cref="BarLabels"/> then splits them per bar.
    /// </summary>
    public static class SlotLabel
    {
        /// <summary>
        /// Returns the buttons for a slot's shortcut, or null to leave the slot's label alone.
        /// </summary>
        /// <param name="mainKey">The slot's key name (e.g. "1", "Keypad1"); null or empty if none.</param>
        /// <param name="modifiers">Modifier keys held with it (e.g. "LeftAlt"); may be empty or null.</param>
        /// <param name="buttonsFor">Returns a key's buttons from the mapping, or null if unmapped
        /// (<see cref="KeyMap.Buttons"/>).</param>
        /// <returns>Modifier buttons then the main key's buttons; null when there's no key or the
        /// main key isn't mapped.</returns>
        public static IReadOnlyList<string> Buttons(
            string mainKey,
            IReadOnlyList<string> modifiers,
            Func<string, IReadOnlyList<string>> buttonsFor)
        {
            if (string.IsNullOrEmpty(mainKey))
            {
                return null;
            }

            IReadOnlyList<string> main = buttonsFor(mainKey);
            if (main == null)
            {
                return null;
            }

            // Unmapped modifiers keep their key name (shown as a badge) so the label still says what to press
            return (modifiers ?? Array.Empty<string>())
                .SelectMany(modifier => buttonsFor(modifier) ?? new[] { modifier })
                .Concat(main)
                .ToArray();
        }
    }
}
