// ------------------------------------------------------------------------------------------------
// SpriteFitTests.cs: unit tests for sizing the mod's glyphs like Valheim's (F5).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="SpriteFit"/>. Our images are 144 px with the 128 px glyph inside; the
/// glyph should match Valheim's reference sprite in size and top edge on both TMP sizing paths.
/// </summary>
public class SpriteFitTests
{
    /// <summary>Image size from make-glyphs.ps1.</summary>
    private const int Image = 144;

    /// <summary>Glyph size inside the image.</summary>
    private const int Glyph = 128;

    /// <summary>
    /// Face info with a point size (TMP scales metrics as given): our 128 px must cover Valheim's
    /// glyph height, so the scale shrinks by Valheim's height over ours.
    /// </summary>
    [Fact]
    public void With_a_point_size_our_glyph_matches_the_reference_height()
    {
        SpriteFit fit = SpriteFit.Match(referenceHeight: 64, referenceBearingY: 56, referenceScale: 1.5f, facePointSize: 32,
            Image, Glyph);

        Assert.Equal(1.5f * 64 / 128, fit.Scale, 4);
        Assert.Equal(56f * 128 / 64 + 8, fit.BearingY, 4);
    }

    /// <summary>
    /// Face info without a point size (Valheim's xbox asset, seen in game 2026-11-08: glyphs came
    /// out tiny): TMP fits every sprite's own height to the font ascent, so only the image's
    /// padding and the reference's scale matter, not its pixel height.
    /// </summary>
    [Fact]
    public void Without_a_point_size_the_scale_ignores_pixel_heights()
    {
        SpriteFit fit = SpriteFit.Match(referenceHeight: 64, referenceBearingY: 56, referenceScale: 1.5f, facePointSize: 0,
            Image, Glyph);

        Assert.Equal(1.5f * 144 / 128, fit.Scale, 4);
        Assert.Equal(56f * 128 / 64 + 8, fit.BearingY, 4);
    }

    /// <summary>Without Valheim's sprite, the glyph fills the font ascent with its top on the ascent line.</summary>
    [Fact]
    public void Fallback_fills_the_ascent()
    {
        SpriteFit fit = SpriteFit.Fallback(Image, Glyph);

        Assert.Equal(144f / 128, fit.Scale, 4);
        Assert.Equal(136f, fit.BearingY, 4);
    }
}
