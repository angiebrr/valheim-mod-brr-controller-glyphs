// ------------------------------------------------------------------------------------------------
// ModGlyphs.cs: mapping-file button names that the mod's own Steam Deck glyphs cover (F5).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// The mod's Steam Deck glyphs (made from Kenney's CC0 Input Prompts by scripts/make-glyphs.ps1):
    /// button names to sprite names in the plugin's <see cref="AssetName"/> sprite asset.
    /// </summary>
    public static class ModGlyphs
    {
        /// <summary>Name of the TextMeshPro sprite asset the plugin builds and registers.</summary>
        public const string AssetName = "cgf_deck";

        /// <summary>Normalized mapping-file button name (aliases included) to sprite name.</summary>
        // Sticks are missing: Kenney has no outline variant to recolour, and Valheim has stick art
        private static readonly Dictionary<string, string> Sprites = new ()
        {
            { "a", "a" }, { "b", "b" }, { "x", "x" }, { "y", "y" },
            { "l1", "l1" }, { "lb", "l1" }, { "r1", "r1" }, { "rb", "r1" },
            { "l2", "l2" }, { "lt", "l2" }, { "r2", "r2" }, { "rt", "r2" },
            { "l4", "l4" }, { "l5", "l5" }, { "r4", "r4" }, { "r5", "r5" },
            { "view", "view" }, { "back", "view" }, { "select", "view" },
            { "options", "options" }, { "start", "options" }, { "menu", "options" },
            { "dpadup", "dpad_up" }, { "dpaddown", "dpad_down" }, { "dpadleft", "dpad_left" }, { "dpadright", "dpad_right" },
            { "lpad", "lpad" }, { "lpadclick", "lpad" }, { "ltrackpad", "lpad" },
            { "lpadup", "lpad_up" }, { "lpaddown", "lpad_down" }, { "lpadleft", "lpad_left" }, { "lpadright", "lpad_right" },
            { "rpad", "rpad" }, { "rpadclick", "rpad" }, { "rtrackpad", "rpad" },
            { "rpadup", "rpad_up" }, { "rpaddown", "rpad_down" }, { "rpadleft", "rpad_left" }, { "rpadright", "rpad_right" },
        };

        /// <summary>Every sprite name once, sorted: one embedded image each.</summary>
        public static IReadOnlyList<string> SpriteNames { get; } =
            Sprites.Values.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Looks up the mod's sprite for a mapping-file button name.
        /// </summary>
        /// <param name="normalizedButton">Lowercase button name without spaces, '_' or '-'.</param>
        /// <param name="sprite">The sprite name (e.g. "lpad_up") when found.</param>
        /// <returns>True if the mod has a glyph for the button.</returns>
        public static bool TryGetSprite(string normalizedButton, out string sprite)
        {
            return Sprites.TryGetValue(normalizedButton ?? "", out sprite);
        }

        /// <summary>
        /// Returns the TextMeshPro tag showing the mod's glyph for a button.
        /// </summary>
        /// <param name="normalizedButton">Lowercase button name without spaces, '_' or '-'.</param>
        /// <returns>A tag such as &lt;sprite="cgf_deck" name="r4"&gt;, or null if there's no glyph.</returns>
        public static string Tag(string normalizedButton)
        {
            return TryGetSprite(normalizedButton, out string sprite) ? SpriteStyle.Tag(AssetName, sprite) : null;
        }
    }
}
