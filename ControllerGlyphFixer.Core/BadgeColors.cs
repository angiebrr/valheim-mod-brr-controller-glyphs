// ------------------------------------------------------------------------------------------------
// BadgeColors.cs: picks the colour of a group's hold badge and line (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// How hold badges and their lines are coloured (config General.HoldBadgeColors).
    /// </summary>
    public enum BadgeColorMode
    {
        /// <summary>The slot-label colour, like the rest of the hotbar.</summary>
        Neutral,

        /// <summary>A built-in colour per hold button, in name order.</summary>
        Palette,

        /// <summary>Colours from the mapping file's colors section; others stay neutral.</summary>
        Custom,
    }

    /// <summary>
    /// Picks the colour of a group's badge and line from its hold button. The colour follows the
    /// button, not the position, so R4 looks the same on every bar.
    /// </summary>
    public static class BadgeColors
    {
        /// <summary>Built-in colours, readable on Valheim's dark HUD, assigned in hold-button name order.</summary>
        public static IReadOnlyList<string> Palette { get; } = new[]
        {
            "#9B8CFF", "#5DCAA5", "#F0997B", "#ED93B1", "#85B7EB", "#C0DD97", "#FAC775", "#F09595",
        };

        /// <summary>
        /// Returns the colour for a group's badge and line.
        /// </summary>
        /// <param name="mode">The colouring mode.</param>
        /// <param name="prefix">The group's hold buttons; the first one decides the colour.</param>
        /// <param name="keyMap">The mapping (hold buttons for the palette, colors section for custom).</param>
        /// <returns>A colour (#RRGGBB or #RRGGBBAA), or null for the neutral slot-label colour.</returns>
        public static string For(BadgeColorMode mode, IReadOnlyList<string> prefix, KeyMap keyMap)
        {
            if (prefix == null || prefix.Count == 0)
            {
                return null;
            }

            switch (mode)
            {
                case BadgeColorMode.Palette:
                    int index = keyMap.HoldButtons.ToList().IndexOf(KeyMap.Normalize(prefix[0]));
                    return index < 0 ? null : Palette[index % Palette.Count];
                case BadgeColorMode.Custom:
                    return keyMap.Color(prefix[0]);
                default:
                    return null;
            }
        }
    }
}
