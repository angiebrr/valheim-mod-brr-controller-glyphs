// ------------------------------------------------------------------------------------------------
// ModGlyphs.cs: mapping-file button names that the mod's own Steam Deck glyphs cover (F5).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// The mod's Steam Deck glyphs (made from Kenney's CC0 Input Prompts by scripts/make-glyphs.ps1):
    /// button names to sprite names in the plugin's <see cref="AssetName"/> sprite asset.
    /// </summary>
    public static class ModGlyphs
    {
        /// <summary>Name of the TextMeshPro sprite asset the plugin builds and registers.</summary>
        public const string AssetName = NamePrefix.Value + "deck";

        /// <summary>Size of each glyph image (make-glyphs.ps1): the glyph plus room for its shadow.</summary>
        public const int ImageSize = 144;

        /// <summary>Size of the glyph itself, centred in each image.</summary>
        public const int GlyphSize = 128;

        /// <summary>Transparent room around the glyph on each side, for its shadow.</summary>
        public const int ShadowPad = (ImageSize - GlyphSize) / 2;

        /// <summary>Normalized mapping-file button name (aliases included) to sprite name.</summary>
        // Sticks are missing: Kenney has no outline variant to recolor, and Valheim has stick art
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

        /// <summary>Every normalized button name with a mod glyph, aliases included (for the template's list).</summary>
        public static IReadOnlyCollection<string> ButtonNames => Sprites.Keys;

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
        /// <returns>A tag such as &lt;sprite="brrcg_deck" name="r4"&gt;, or null if there's no glyph.</returns>
        public static string Tag(string normalizedButton)
        {
            return TryGetSprite(normalizedButton, out string sprite) ? SpriteStyle.Tag(AssetName, sprite) : null;
        }

        /// <summary>
        /// Returns the tag showing the mod's glyph at a percentage of its size, inside a TMP size tag.
        /// </summary>
        /// <param name="normalizedButton">Lowercase button name without spaces, '_' or '-'.</param>
        /// <param name="sizePercent">Glyph size in percent; 100 gives the plain <see cref="Tag(string)"/>.</param>
        /// <param name="sidePaddingEm">Space added on each side, in em, so a smaller glyph takes the room
        /// of a full-size one; 0 adds none.</param>
        /// <returns>A tag such as &lt;size=85%&gt;&lt;sprite="brrcg_deck" name="r4"&gt;&lt;/size&gt;, or null if there's no glyph.</returns>
        public static string Tag(string normalizedButton, int sizePercent, float sidePaddingEm = 0f)
        {
            string tag = Tag(normalizedButton);
            if (tag == null || sizePercent == 100)
            {
                return tag;
            }

            string sized = "<size=" + sizePercent + "%>" + tag + "</size>";
            if (sidePaddingEm <= 0f)
            {
                return sized;
            }

            string space = "<space=" + sidePaddingEm.ToString("0.###", CultureInfo.InvariantCulture) + "em>";
            return space + sized + space;
        }
    }
}
