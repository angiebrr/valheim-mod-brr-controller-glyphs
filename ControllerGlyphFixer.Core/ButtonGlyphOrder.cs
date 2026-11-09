// ------------------------------------------------------------------------------------------------
// ButtonGlyphOrder.cs: which art the mod's own glyphs use, Valheim's or the mod's Steam Deck art (F5).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Config "Glyphs used by mod": the art tried first for each button the mod draws. Valheim's own
    /// prompts keep Valheim's art either way. A button neither has art for shows as a [badge].
    /// Value names are what users see.
    /// </summary>
    public enum ButtonGlyphOrder
    {
        /// <summary>Valheim's glyph where it has one, else the mod's Steam Deck glyph.</summary>
        Valheim,

        /// <summary>The mod's Steam Deck glyph where it has one, else Valheim's.</summary>
        SteamDeck,
    }
}
