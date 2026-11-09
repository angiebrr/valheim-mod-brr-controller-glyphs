// ------------------------------------------------------------------------------------------------
// PrefixColors.cs: picks the color of a group's prefix and line (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// How group prefixes and their lines are colored (config "Hotbar group colors").
    /// </summary>
    public enum PrefixColorMode
    {
        /// <summary>The slot-label color, like the rest of the hotbar.</summary>
        Neutral,

        /// <summary>A built-in color per prefix button, in name order.</summary>
        Palette,

        /// <summary>Colors from the mapping file's colors section; others stay neutral.</summary>
        Custom,
    }

    /// <summary>
    /// Picks the color of a group's prefix and line from its prefix button. The color follows the
    /// button, not the position, so R4 looks the same on every bar.
    /// </summary>
    public static class PrefixColors
    {
        /// <summary>Built-in colors, readable on Valheim's dark HUD, assigned in prefix-button name order.</summary>
        public static IReadOnlyList<string> Palette { get; } = new[]
        {
            "#9B8CFF", "#5DCAA5", "#F0997B", "#ED93B1", "#85B7EB", "#C0DD97", "#FAC775", "#F09595",
        };

        /// <summary>
        /// Returns the color for a group's prefix and line.
        /// </summary>
        /// <param name="mode">The coloring mode.</param>
        /// <param name="prefix">The group's prefix buttons; the first one decides the color.</param>
        /// <param name="keyMap">The mapping (prefix buttons for the palette, colors section for custom).</param>
        /// <returns>A color (#RRGGBB or #RRGGBBAA), or null for the neutral slot-label color.</returns>
        public static string For(PrefixColorMode mode, IReadOnlyList<string> prefix, KeyMap keyMap)
        {
            if (prefix == null || prefix.Count == 0)
            {
                return null;
            }

            switch (mode)
            {
                case PrefixColorMode.Palette:
                    int index = keyMap.PrefixButtons.ToList().IndexOf(KeyMap.Normalize(prefix[0]));
                    return index < 0 ? null : Palette[index % Palette.Count];
                case PrefixColorMode.Custom:
                    return keyMap.Color(prefix[0]);
                default:
                    return null;
            }
        }
    }
}
