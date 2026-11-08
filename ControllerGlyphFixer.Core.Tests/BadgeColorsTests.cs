// ------------------------------------------------------------------------------------------------
// BadgeColorsTests.cs: unit tests for the colour of a group's hold badge and line (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="BadgeColors.For"/>.
/// </summary>
public class BadgeColorsTests
{
    /// <summary>A mapping with three hold buttons and a custom colour for R4 only.</summary>
    private static readonly KeyMap Map = KeyMap.Parse(
        "colors:\n  R4: \"#112233\"\nkeys:\n  \"1\": [R4, A]\n  Keypad1: [R5, A]\n  Z: [L4, R4, B]\n");

    /// <summary>Neutral: no colour (the slot-label colour is used), whatever the button.</summary>
    [Fact]
    public void Neutral_has_no_colour()
    {
        Assert.Null(BadgeColors.For(BadgeColorMode.Neutral, new[] { "R4" }, Map));
    }

    /// <summary>Palette: hold buttons take palette colours in name order (L4, R4, R5).</summary>
    [Fact]
    public void Palette_colours_follow_hold_button_name_order()
    {
        Assert.Equal(BadgeColors.Palette[0], BadgeColors.For(BadgeColorMode.Palette, new[] { "L4", "R4" }, Map));
        Assert.Equal(BadgeColors.Palette[1], BadgeColors.For(BadgeColorMode.Palette, new[] { "R4" }, Map));
        Assert.Equal(BadgeColors.Palette[2], BadgeColors.For(BadgeColorMode.Palette, new[] { "r5" }, Map));
    }

    /// <summary>Palette: a button that isn't a hold button in the mapping gets no colour.</summary>
    [Fact]
    public void Palette_ignores_unknown_buttons()
    {
        Assert.Null(BadgeColors.For(BadgeColorMode.Palette, new[] { "LPadUp" }, Map));
    }

    /// <summary>Palette: more hold buttons than colours wrap around.</summary>
    [Fact]
    public void Palette_wraps_around()
    {
        string keys = string.Join("", Enumerable.Range(0, BadgeColors.Palette.Count + 1)
            .Select(i => $"  K{i}: [H{i:D2}, A]\n"));
        var map = KeyMap.Parse("keys:\n" + keys);

        Assert.Equal(BadgeColors.Palette[0],
            BadgeColors.For(BadgeColorMode.Palette, new[] { $"H{BadgeColors.Palette.Count:D2}" }, map));
    }

    /// <summary>Custom: the colors section's colour; buttons without one stay neutral.</summary>
    [Fact]
    public void Custom_uses_the_colors_section()
    {
        Assert.Equal("#112233", BadgeColors.For(BadgeColorMode.Custom, new[] { "R4" }, Map));
        Assert.Null(BadgeColors.For(BadgeColorMode.Custom, new[] { "R5" }, Map));
    }

    /// <summary>Multi-button prefixes are coloured by their first button.</summary>
    [Fact]
    public void Multi_button_prefixes_use_the_first_button()
    {
        Assert.Null(BadgeColors.For(BadgeColorMode.Custom, new[] { "L4", "R4" }, Map));
    }

    /// <summary>Every palette colour is a valid #RRGGBB colour.</summary>
    [Fact]
    public void Palette_colours_are_valid()
    {
        Assert.All(BadgeColors.Palette, color => Assert.Matches("^#[0-9A-F]{6}$", color));
        Assert.Equal(BadgeColors.Palette.Count, BadgeColors.Palette.Distinct().Count());
    }
}
