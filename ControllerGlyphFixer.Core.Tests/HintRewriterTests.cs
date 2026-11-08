// ------------------------------------------------------------------------------------------------
// HintRewriterTests.cs: unit tests for translating key names in hover-text hints (F4).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="HintRewriter.Rewrite"/>, using my Steam Controller mapping and the hint
/// strings mods actually write (decompiled 2026-11-08).
/// </summary>
public class HintRewriterTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>Stands in for Unity's KeyCode names (any case).</summary>
    private static readonly HashSet<string> KeyNames = new (StringComparer.OrdinalIgnoreCase)
    {
        "E", "B", "Q", "LeftShift", "Tab", "Space", "Alpha1", "Keypad1",
    };

    /// <summary>Valheim's X glyph, as $KEY_Use resolves with F1.</summary>
    private const string UseGlyph = "<sprite=\"xbox\" name=\"button_x\">";

    /// <summary>
    /// Rewrites with the fixture mapping and fake glyphs (&lt;control&gt;).
    /// </summary>
    /// <param name="text">Hover text.</param>
    /// <returns>The rewrite result.</returns>
    private static HintRewrite Rewrite(string text) =>
        HintRewriter.Rewrite(text, key => SteamController.Translate(key, control => $"<{control}>"), KeyNames.Contains);

    /// <summary>AutomaticFuel: a mapped key alone in a hint becomes its button; brackets go.</summary>
    [Fact]
    public void Mapped_key_hint_becomes_its_button()
    {
        HintRewrite result = Rewrite("Fire\n[<color=yellow><b>B</b></color>] AutoFuel: On");

        Assert.Equal("Fire\n<color=yellow><b>[LPad Up]</b></color> AutoFuel: On", result.Text);
        Assert.Empty(result.UnmappedKeys);
    }

    /// <summary>A plain bracketed key (no tags) works too, in any case.</summary>
    /// <param name="key">The key as written.</param>
    [Theory]
    [InlineData("B")]
    [InlineData("b")]
    public void Plain_bracketed_key_is_translated(string key)
    {
        Assert.Equal("[LPad Up] AutoFuel", Rewrite($"[{key}] AutoFuel").Text);
    }

    /// <summary>AzuCraftyBoxes fill all: key + glyph, with tags around each part.</summary>
    [Fact]
    public void Key_plus_glyph_combo_is_translated()
    {
        string text = $"[<b><color=yellow>LeftShift</color> + <color=yellow>{UseGlyph}</color></b>] Fill all";

        HintRewrite result = Rewrite(text);

        Assert.Equal($"<b><color=yellow>[LPad Down]</color> + <color=yellow>{UseGlyph}</color></b> Fill all", result.Text);
        Assert.Empty(result.UnmappedKeys);
    }

    /// <summary>AzuCraftyBoxes chest toggle: E isn't mapped, so brackets stay and E is reported.</summary>
    [Fact]
    public void Unmapped_key_keeps_brackets_and_is_reported()
    {
        HintRewrite result = Rewrite("[<color=yellow><b>E + LeftShift</b></color>] $hud_crafting");

        Assert.Equal("[<color=yellow><b>E + [LPad Down]</b></color>] $hud_crafting", result.Text);
        Assert.Equal(new[] { "E" }, result.UnmappedKeys.Select(unmapped => unmapped.Key));
    }

    /// <summary>
    /// An unmapped key is reported with the line its hint is on, without formatting tags; other
    /// lines that merely contain the letter (Chest ( EMPTY )) aren't it.
    /// </summary>
    [Fact]
    public void Unmapped_key_is_reported_with_its_hints_line()
    {
        HintRewrite result = Rewrite("Chest ( EMPTY )\n[<color=yellow><b>E + LeftShift</b></color>] Crafting Chest");

        UnmappedKey unmapped = Assert.Single(result.UnmappedKeys);
        Assert.Equal("E", unmapped.Key);
        Assert.Equal("[E + LeftShift] Crafting Chest", unmapped.Line);
    }

    /// <summary>Vanilla hints in keyboard mode (glyph inside brackets) lose their brackets, like gamepad mode.</summary>
    [Fact]
    public void Glyph_only_hint_drops_its_brackets()
    {
        Assert.Equal($"<color=yellow><b>{UseGlyph}</b></color> Open",
            Rewrite($"[<color=yellow><b>{UseGlyph}</b></color>] Open").Text);
    }

    /// <summary>A badge from the mapping nested in a hint counts as a glyph.</summary>
    [Fact]
    public void Nested_badge_counts_as_a_glyph()
    {
        Assert.Equal("<b>[R4] + <dpad_up></b> Slot 1", Rewrite("[<b>[R4] + <dpad_up></b>] Slot 1").Text);
    }

    /// <summary>Brackets that aren't key hints, and free text, are left alone.</summary>
    /// <param name="text">Hover text without key hints.</param>
    [Theory]
    [InlineData("Zone [Zebra]")]
    [InlineData("Stack [1/5]")]
    [InlineData("Press B to open")]
    [InlineData("[<b>Open B</b>]")]
    [InlineData("[<b></b>]")]
    [InlineData("[]")]
    [InlineData("[B")]
    [InlineData("B]")]
    [InlineData("[B\n]")]
    public void Non_hints_are_untouched(string text)
    {
        HintRewrite result = Rewrite(text);

        Assert.Equal(text, result.Text);
        Assert.Empty(result.UnmappedKeys);
    }

    /// <summary>Every hint in a text is rewritten, each on its own merits.</summary>
    [Fact]
    public void Several_hints_are_each_rewritten()
    {
        HintRewrite result = Rewrite("[Q] Pick up\n[E] Use\n[Zebra]");

        Assert.Equal("[L5] Pick up\n[E] Use\n[Zebra]", result.Text);
        Assert.Equal(new[] { "E" }, result.UnmappedKeys.Select(unmapped => unmapped.Key));
        Assert.Equal("[E] Use", result.UnmappedKeys[0].Line);
    }

    /// <summary>Null and empty text pass through.</summary>
    /// <param name="text">Null or empty.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_text_passes_through(string text)
    {
        Assert.Equal(text, Rewrite(text).Text);
    }

    /// <summary>Rewriting the output again changes nothing (the patch may see its own text).</summary>
    /// <param name="text">Hover text.</param>
    [Theory]
    [InlineData("[<color=yellow><b>B</b></color>] AutoFuel")]
    [InlineData("[<color=yellow><b>E + LeftShift</b></color>] Crafting")]
    [InlineData("[<b><color=yellow>LeftShift</color> + <color=yellow><sprite=\"xbox\" name=\"button_x\"></color></b>] Fill")]
    [InlineData("[Q] Pick up\n[Zebra]")]
    public void Rewriting_is_idempotent(string text)
    {
        string once = Rewrite(text).Text;

        Assert.Equal(once, Rewrite(once).Text);
    }
}
