// ------------------------------------------------------------------------------------------------
// SpriteFitTests.cs: unit tests for sizing the mod's glyphs like Valheim's (F5).
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

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

    /// <summary>
    /// A size factor shrinks our glyph around the middle of Valheim's (in game 2026-11-08: at full
    /// size our square glyphs looked about 1.4 times Valheim's round ones), on both sizing paths.
    /// </summary>
    /// <param name="facePointSize">Copied face info's point size (0: TMP's ascent-fitting path).</param>
    [Theory]
    [InlineData(0f)]
    [InlineData(32f)]
    public void Size_factor_shrinks_around_the_reference_middle(float facePointSize)
    {
        SpriteFit full = SpriteFit.Match(64, 56, 1.5f, facePointSize, Image, Glyph);
        SpriteFit smaller = SpriteFit.Match(64, 56, 1.5f, facePointSize, Image, Glyph, sizeFactor: 0.8f);

        Assert.Equal(full.Scale * 0.8f, smaller.Scale, 4);
        // Middle of Valheim's glyph: 56 - 64 / 2 = 24 reference px above the baseline, which is 48 of
        // our px at full size; our glyph's middle (bearing - 8 - 64 px) must land there at either size
        float ourPixel = full.Scale / smaller.Scale;
        Assert.Equal(48f, (full.BearingY - 8 - 64), 4);
        Assert.Equal(48f * ourPixel, (smaller.BearingY - 8 - 64), 4);
    }

    /// <summary>Without Valheim's sprite, the glyph fills the font ascent with its top on the ascent line.</summary>
    [Fact]
    public void Fallback_fills_the_ascent()
    {
        SpriteFit fit = SpriteFit.Fallback(Image, Glyph);

        Assert.Equal(144f / 128, fit.Scale, 4);
        Assert.Equal(136f, fit.BearingY, 4);
    }

    /// <summary>The fallback takes the same size factor as a matched fit, so it isn't 1.4 times Valheim's size.</summary>
    [Fact]
    public void Fallback_uses_the_size_factor()
    {
        Assert.Equal(SpriteFit.Fallback(Image, Glyph).Scale * 0.7f, SpriteFit.Fallback(Image, Glyph, 0.7f).Scale, 4);
    }
}
