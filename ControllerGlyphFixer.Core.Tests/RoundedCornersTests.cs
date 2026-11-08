// ------------------------------------------------------------------------------------------------
// RoundedCornersTests.cs: unit tests for the rounded backing image's pixel coverage (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="RoundedCorners.Coverage"/>, on a 12-pixel image with a 4-pixel radius.
/// </summary>
public class RoundedCornersTests
{
    /// <summary>Image size in pixels.</summary>
    private const int Size = 12;

    /// <summary>Corner radius in pixels.</summary>
    private const float Radius = 4f;

    /// <summary>The middle and the middle of each edge are fully covered.</summary>
    /// <param name="x">Pixel column.</param>
    /// <param name="y">Pixel row.</param>
    [Theory]
    [InlineData(6, 6)]   // centre
    [InlineData(0, 6)]   // left edge
    [InlineData(11, 6)]  // right edge
    [InlineData(6, 0)]   // bottom edge
    [InlineData(6, 11)]  // top edge
    public void Middle_and_edges_are_covered(int x, int y)
    {
        Assert.Equal(1f, RoundedCorners.Coverage(x, y, Size, Radius));
    }

    /// <summary>The outermost corner pixels are outside the rounded corner.</summary>
    /// <param name="x">Pixel column.</param>
    /// <param name="y">Pixel row.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(11, 0)]
    [InlineData(0, 11)]
    [InlineData(11, 11)]
    public void Outer_corners_are_empty(int x, int y)
    {
        Assert.Equal(0f, RoundedCorners.Coverage(x, y, Size, Radius));
    }

    /// <summary>Pixels on the curve are partly covered (smooth edge).</summary>
    [Fact]
    public void Curve_pixels_are_partly_covered()
    {
        float coverage = RoundedCorners.Coverage(1, 0, Size, Radius);

        Assert.InRange(coverage, 0.01f, 0.99f);
    }

    /// <summary>The image is symmetric: every corner looks the same.</summary>
    [Fact]
    public void Corners_are_symmetric()
    {
        float bottomLeft = RoundedCorners.Coverage(1, 1, Size, Radius);

        Assert.Equal(bottomLeft, RoundedCorners.Coverage(Size - 2, 1, Size, Radius), 5);
        Assert.Equal(bottomLeft, RoundedCorners.Coverage(1, Size - 2, Size, Radius), 5);
        Assert.Equal(bottomLeft, RoundedCorners.Coverage(Size - 2, Size - 2, Size, Radius), 5);
    }
}
