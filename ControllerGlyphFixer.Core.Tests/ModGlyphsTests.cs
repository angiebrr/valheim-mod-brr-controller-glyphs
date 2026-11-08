// ------------------------------------------------------------------------------------------------
// ModGlyphsTests.cs: unit tests for the mod's Steam Deck glyph sprites (F5).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="ModGlyphs"/>: which mapping-file buttons have one of the mod's glyphs, and
/// that every glyph has its image in ControllerGlyphFixer/Glyphs.
/// </summary>
public class ModGlyphsTests
{
    /// <summary>Every Deck button and its common aliases map to the sprite with its image.</summary>
    /// <param name="button">Normalized mapping-file button name.</param>
    /// <param name="expected">Sprite name in the cgf_deck asset.</param>
    [Theory]
    [InlineData("a", "a")]
    [InlineData("b", "b")]
    [InlineData("x", "x")]
    [InlineData("y", "y")]
    [InlineData("l1", "l1")]
    [InlineData("lb", "l1")]
    [InlineData("r1", "r1")]
    [InlineData("rb", "r1")]
    [InlineData("l2", "l2")]
    [InlineData("lt", "l2")]
    [InlineData("r2", "r2")]
    [InlineData("rt", "r2")]
    [InlineData("l4", "l4")]
    [InlineData("l5", "l5")]
    [InlineData("r4", "r4")]
    [InlineData("r5", "r5")]
    [InlineData("view", "view")]
    [InlineData("back", "view")]
    [InlineData("select", "view")]
    [InlineData("options", "options")]
    [InlineData("start", "options")]
    [InlineData("menu", "options")]
    [InlineData("dpadup", "dpad_up")]
    [InlineData("dpaddown", "dpad_down")]
    [InlineData("dpadleft", "dpad_left")]
    [InlineData("dpadright", "dpad_right")]
    [InlineData("lpad", "lpad")]
    [InlineData("lpadclick", "lpad")]
    [InlineData("ltrackpad", "lpad")]
    [InlineData("lpadup", "lpad_up")]
    [InlineData("lpaddown", "lpad_down")]
    [InlineData("lpadleft", "lpad_left")]
    [InlineData("lpadright", "lpad_right")]
    [InlineData("rpad", "rpad")]
    [InlineData("rpadclick", "rpad")]
    [InlineData("rtrackpad", "rpad")]
    [InlineData("rpadup", "rpad_up")]
    [InlineData("rpaddown", "rpad_down")]
    [InlineData("rpadleft", "rpad_left")]
    [InlineData("rpadright", "rpad_right")]
    public void Deck_buttons_map_to_sprites(string button, string expected)
    {
        Assert.True(ModGlyphs.TryGetSprite(button, out string sprite));
        Assert.Equal(expected, sprite);
    }

    /// <summary>Buttons the Deck set has no glyph for (grips, Steam, stick presses) get none.</summary>
    /// <param name="button">Normalized mapping-file button name.</param>
    [Theory]
    [InlineData("lg")]
    [InlineData("steam")]
    [InlineData("ls")]
    [InlineData("")]
    public void Buttons_without_art_have_no_sprite(string button)
    {
        Assert.False(ModGlyphs.TryGetSprite(button, out _));
        Assert.Null(ModGlyphs.Tag(button));
    }

    /// <summary>Tags name the mod's own sprite asset, in the format TextMeshPro and Valheim use.</summary>
    [Fact]
    public void Tag_uses_the_cgf_deck_asset()
    {
        Assert.Equal("<sprite=\"cgf_deck\" name=\"r4\">", ModGlyphs.Tag("r4"));
    }

    /// <summary>
    /// Hover hints show the glyph smaller (in game 2026-11-08: the trackpad arrow reached the glyph
    /// on the line above); 100% is the plain tag, and buttons without a glyph still get none.
    /// </summary>
    [Fact]
    public void Sized_tag_wraps_the_glyph_in_a_size_tag()
    {
        Assert.Equal("<size=85%><sprite=\"cgf_deck\" name=\"lpad_up\"></size>", ModGlyphs.Tag("lpadup", 85));
        Assert.Equal(ModGlyphs.Tag("lpadup"), ModGlyphs.Tag("lpadup", 100));
        Assert.Null(ModGlyphs.Tag("lg", 85));
    }

    /// <summary>
    /// A smaller glyph takes less room than Valheim's, so hint text after it started further left and
    /// the glyph sat left of the ones above (in game 2026-11-08); equal space on both sides puts both back.
    /// </summary>
    [Fact]
    public void Sized_tag_with_padding_adds_equal_space_on_both_sides()
    {
        Assert.Equal("<space=0.12em><size=75%><sprite=\"cgf_deck\" name=\"lpad_up\"></size><space=0.12em>",
            ModGlyphs.Tag("lpadup", 75, 0.12f));
        Assert.Equal(ModGlyphs.Tag("lpadup", 75), ModGlyphs.Tag("lpadup", 75, 0f));
        Assert.Null(ModGlyphs.Tag("lg", 75, 0.12f));
    }

    /// <summary>
    /// The plugin loads one embedded image per sprite name, so the table and the Glyphs folder
    /// (made by scripts/make-glyphs.ps1) must list the same names.
    /// </summary>
    [Fact]
    public void Every_sprite_has_a_png_and_every_png_a_sprite()
    {
        string glyphs = Path.Combine(RepoRoot(), "ControllerGlyphFixer", "Glyphs");
        var pngs = Directory.GetFiles(glyphs, "*.png").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(pngs, ModGlyphs.SpriteNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// Finds the repository root by walking up from the test output folder to the solution file.
    /// </summary>
    /// <returns>The folder holding ControllerGlyphFixer.sln.</returns>
    private static string RepoRoot()
    {
        DirectoryInfo dir = new (AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ControllerGlyphFixer.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, "ControllerGlyphFixer.sln not found above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}
