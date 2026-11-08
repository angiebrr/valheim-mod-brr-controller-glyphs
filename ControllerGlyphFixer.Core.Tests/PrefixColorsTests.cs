// ------------------------------------------------------------------------------------------------
// PrefixColorsTests.cs: unit tests for the colour of a group's prefix and line (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="PrefixColors.For"/>.
/// </summary>
public class PrefixColorsTests
{
    /// <summary>A mapping with three prefix buttons and a custom colour for R4 only.</summary>
    private static readonly KeyMap Map = KeyMap.Parse(
        "colors:\n  R4: \"#112233\"\nkeys:\n  \"1\": [R4, A]\n  Keypad1: [R5, A]\n  Z: [L4, R4, B]\n");

    /// <summary>Neutral: no colour (the slot-label colour is used), whatever the button.</summary>
    [Fact]
    public void Neutral_has_no_colour()
    {
        Assert.Null(PrefixColors.For(PrefixColorMode.Neutral, new[] { "R4" }, Map));
    }

    /// <summary>Palette: prefix buttons take palette colours in name order (L4, R4, R5).</summary>
    [Fact]
    public void Palette_colours_follow_prefix_button_name_order()
    {
        Assert.Equal(PrefixColors.Palette[0], PrefixColors.For(PrefixColorMode.Palette, new[] { "L4", "R4" }, Map));
        Assert.Equal(PrefixColors.Palette[1], PrefixColors.For(PrefixColorMode.Palette, new[] { "R4" }, Map));
        Assert.Equal(PrefixColors.Palette[2], PrefixColors.For(PrefixColorMode.Palette, new[] { "r5" }, Map));
    }

    /// <summary>Palette: a button that isn't a prefix button in the mapping gets no colour.</summary>
    [Fact]
    public void Palette_ignores_unknown_buttons()
    {
        Assert.Null(PrefixColors.For(PrefixColorMode.Palette, new[] { "LPadUp" }, Map));
    }

    /// <summary>Palette: more prefix buttons than colours wrap around.</summary>
    [Fact]
    public void Palette_wraps_around()
    {
        string keys = string.Join("", Enumerable.Range(0, PrefixColors.Palette.Count + 1)
            .Select(i => $"  K{i}: [H{i:D2}, A]\n"));
        var map = KeyMap.Parse("keys:\n" + keys);

        Assert.Equal(PrefixColors.Palette[0],
            PrefixColors.For(PrefixColorMode.Palette, new[] { $"H{PrefixColors.Palette.Count:D2}" }, map));
    }

    /// <summary>Custom: the colors section's colour; buttons without one stay neutral.</summary>
    [Fact]
    public void Custom_uses_the_colors_section()
    {
        Assert.Equal("#112233", PrefixColors.For(PrefixColorMode.Custom, new[] { "R4" }, Map));
        Assert.Null(PrefixColors.For(PrefixColorMode.Custom, new[] { "R5" }, Map));
    }

    /// <summary>Multi-button prefixes are coloured by their first button.</summary>
    [Fact]
    public void Multi_button_prefixes_use_the_first_button()
    {
        Assert.Null(PrefixColors.For(PrefixColorMode.Custom, new[] { "L4", "R4" }, Map));
    }

    /// <summary>Every palette colour is a valid #RRGGBB colour.</summary>
    [Fact]
    public void Palette_colours_are_valid()
    {
        Assert.All(PrefixColors.Palette, color => Assert.Matches("^#[0-9A-F]{6}$", color));
        Assert.Equal(PrefixColors.Palette.Count, PrefixColors.Palette.Distinct().Count());
    }
}
