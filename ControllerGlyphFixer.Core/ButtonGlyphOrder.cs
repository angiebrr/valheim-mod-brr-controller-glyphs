// ------------------------------------------------------------------------------------------------
// ButtonGlyphOrder.cs: which art a mapped button shows first, Valheim's or the mod's (F5).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Config General.ButtonGlyphs: the art tried first for each mapped button. A button neither has
    /// art for shows as a [badge].
    /// </summary>
    public enum ButtonGlyphOrder
    {
        /// <summary>Valheim's glyph where it has one, else the mod's Steam Deck glyph.</summary>
        ValheimFirst,

        /// <summary>The mod's Steam Deck glyph where it has one, else Valheim's.</summary>
        ModGlyphsForAll,
    }
}
