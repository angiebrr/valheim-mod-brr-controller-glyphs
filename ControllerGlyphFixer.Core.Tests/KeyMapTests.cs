// ------------------------------------------------------------------------------------------------
// KeyMapTests.cs: unit tests for parsing the key mapping and translating keys to buttons.
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;
using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="KeyMap"/> and <see cref="KeyMapTemplate"/>. The mapping under test comes
/// from Fixtures/steam-controller.yaml; Core has no built-in mapping.
/// </summary>
public class KeyMapTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>
    /// Stands in for Valheim's sprite lookup: known Valheim control names render as &lt;control&gt;.
    /// </summary>
    /// <param name="control">Valheim control name (e.g. "buttonSouth").</param>
    /// <returns>The control name in angle brackets.</returns>
    private static string FakeGlyph(string control) => $"<{control}>";

    /// <summary>Every row of the fixture mapping renders as the expected glyphs and badges.</summary>
    /// <param name="key">Key name.</param>
    /// <param name="expected">Expected display string.</param>
    [Theory]
    [InlineData("1", "[R4] + <dpad_up>")]
    [InlineData("2", "[R4] + <dpad_right>")]
    [InlineData("3", "[R4] + <dpad_down>")]
    [InlineData("4", "[R4] + <dpad_left>")]
    [InlineData("5", "[R4] + <buttonNorth>")]
    [InlineData("6", "[R4] + <buttonEast>")]
    [InlineData("7", "[R4] + <buttonSouth>")]
    [InlineData("8", "[R4] + <buttonWest>")]
    [InlineData("Keypad1", "[R5] + <dpad_up>")]
    [InlineData("Keypad2", "[R5] + <dpad_right>")]
    [InlineData("Keypad3", "[R5] + <dpad_down>")]
    [InlineData("Keypad4", "[R5] + <dpad_left>")]
    [InlineData("Keypad5", "[R5] + <buttonNorth>")]
    [InlineData("Keypad6", "[R5] + <buttonEast>")]
    [InlineData("I", "[R5] + <buttonSouth>")]
    [InlineData("N", "[R5] + <buttonWest>")]
    [InlineData("Q", "[L5]")]
    [InlineData("Z", "[L4]")]
    [InlineData("B", "[LPad Up]")]
    [InlineData("LeftShift", "[LPad Down]")]
    public void Steam_controller_mapping_translates_every_row(string key, string expected)
    {
        Assert.Equal(expected, SteamController.Translate(key, FakeGlyph));
    }

    /// <summary>Spellings Valheim and mods use for a key translate the same as its canonical name.</summary>
    /// <param name="spelling">Alternative spelling.</param>
    /// <param name="canonical">The key name used in the mapping file.</param>
    [Theory]
    [InlineData("Alpha1", "1")]
    [InlineData("Digit1", "1")]
    [InlineData("Num 1", "Keypad1")]
    [InlineData("Numpad1", "Keypad1")]
    [InlineData("KeypadNumber1", "Keypad1")]
    [InlineData("Left Shift", "LeftShift")]
    [InlineData("$button_lshift", "LeftShift")]
    [InlineData("left_shift", "LeftShift")]
    [InlineData("q", "Q")]
    public void Alternative_spellings_match_the_same_key(string spelling, string canonical)
    {
        Assert.Equal(SteamController.Translate(canonical, FakeGlyph), SteamController.Translate(spelling, FakeGlyph));
    }

    /// <summary>
    /// Unity KeyCode names (what other mods' keybinding settings use) match the Input System names a
    /// mapping file uses, and the other way round.
    /// </summary>
    /// <param name="inFile">Key as written in the mapping file.</param>
    /// <param name="keyCode">The same key as a KeyCode name.</param>
    [Theory]
    [InlineData("LeftCtrl", "LeftControl")]
    [InlineData("RightCtrl", "RightControl")]
    [InlineData("Enter", "Return")]
    [InlineData("NumpadEnter", "KeypadEnter")]
    [InlineData("NumpadPlus", "KeypadPlus")]
    [InlineData("NumpadMinus", "KeypadMinus")]
    [InlineData("NumpadMultiply", "KeypadMultiply")]
    [InlineData("NumpadDivide", "KeypadDivide")]
    [InlineData("NumpadPeriod", "KeypadPeriod")]
    [InlineData("LeftMeta", "LeftWindows")]
    [InlineData("LeftMeta", "LeftCommand")]
    [InlineData("RightMeta", "RightWindows")]
    [InlineData("LeftControl", "LeftCtrl")]
    public void KeyCode_names_match_input_system_names(string inFile, string keyCode)
    {
        KeyMap map = KeyMap.Parse($"keys:\n  {inFile}: L4\n");

        Assert.Equal(new[] { "L4" }, map.Buttons(keyCode));
        Assert.Equal(map.KeyId(inFile), map.KeyId(keyCode));
    }

    /// <summary>A key and its file alias have the same identity, so lists can group them.</summary>
    [Fact]
    public void Key_id_follows_file_aliases()
    {
        KeyMap map = KeyMap.Parse("aliases:\n  Left Shift: LeftShift\nkeys:\n  LeftShift: L4\n");

        Assert.Equal(map.KeyId("LeftShift"), map.KeyId("Left Shift"));
        Assert.Equal(map.KeyId("1"), map.KeyId("Alpha1"));
        Assert.NotEqual(map.KeyId("Keypad1"), map.KeyId("1"));
    }

    /// <summary>Keys not in the mapping (and missing-binding messages) translate to null.</summary>
    /// <param name="key">Key name that isn't mapped.</param>
    [Theory]
    [InlineData("E")]
    [InlineData("Tab")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("MISSING BUTTON DEF \"Foo\"")]
    public void Unmapped_keys_return_null(string key)
    {
        Assert.Null(SteamController.Translate(key, FakeGlyph));
    }

    /// <summary>The colors section sets a button's color, matched like other button names.</summary>
    [Fact]
    public void Colors_section_sets_button_colors()
    {
        var map = KeyMap.Parse("colors:\n  R4: \"#9B8CFF\"\n  L 5: \"#5DCAA580\"\nkeys:\n  Q: L5\n");

        Assert.Equal("#9B8CFF", map.Color("r4"));
        Assert.Equal("#5DCAA580", map.Color("L5"));
        Assert.Null(map.Color("R5"));
    }

    /// <summary>A color that isn't #RRGGBB or #RRGGBBAA is an error naming the line.</summary>
    /// <param name="color">An invalid color.</param>
    [Theory]
    [InlineData("purple")]
    [InlineData("\"#12345\"")]
    [InlineData("\"#GGGGGG\"")]
    public void Invalid_colors_are_an_error(string color)
    {
        var ex = Assert.Throws<KeyMapException>(() => KeyMap.Parse($"colors:\n  R4: {color}\n"));

        Assert.Contains("line 2", ex.Message);
    }

    /// <summary>Prefix buttons are those pressed before another button in any key, sorted by name.</summary>
    [Fact]
    public void Prefix_buttons_are_sorted_and_distinct()
    {
        Assert.Equal(new[] { "r4", "r5" }, SteamController.PrefixButtons);
        Assert.Empty(KeyMap.Parse("keys:\n  Q: L5\n").PrefixButtons);
    }

    /// <summary>Buttons returns a key's buttons as written in the file; null if unmapped.</summary>
    [Fact]
    public void Buttons_returns_the_raw_button_names()
    {
        Assert.Equal(new[] { "R4", "DPadUp" }, SteamController.Buttons("Alpha1"));
        Assert.Equal(new[] { "L5" }, SteamController.Buttons("Q"));
        Assert.Null(SteamController.Buttons("E"));
    }

    /// <summary>Format renders any button list the way Translate does; empty or null gives null.</summary>
    [Fact]
    public void Format_renders_buttons_like_translate()
    {
        Assert.Equal("<dpad_up>", SteamController.Format(new[] { "DPadUp" }, FakeGlyph));
        Assert.Equal("[LPad Up] + <buttonSouth>", SteamController.Format(new[] { "LPadUp", "A" }, FakeGlyph));
        Assert.Null(SteamController.Format(Array.Empty<string>(), FakeGlyph));
        Assert.Null(SteamController.Format(null, FakeGlyph));
    }

    /// <summary>Keys written in the file with any supported spelling match every spelling.</summary>
    /// <param name="fileKey">How the key is written in the mapping file.</param>
    /// <param name="lookup">How Valheim or a mod asks for it.</param>
    [Theory]
    [InlineData("Numpad1", "Keypad1")]
    [InlineData("Numpad1", "Numpad1")]
    [InlineData("Alpha1", "1")]
    [InlineData("\"$button_lshift\"", "LeftShift")]
    [InlineData("Left Shift", "$button_lshift")]
    public void File_keys_in_any_spelling_match(string fileKey, string lookup)
    {
        var map = KeyMap.Parse($"keys:\n  {fileKey}: L5\n");

        Assert.Equal("[L5]", map.Translate(lookup, FakeGlyph));
    }

    /// <summary>Two entries for the same key (in any spelling) are an error naming both.</summary>
    /// <param name="first">First spelling.</param>
    /// <param name="second">Second spelling of the same key.</param>
    [Theory]
    [InlineData("Q", "q")]
    [InlineData("\"1\"", "Alpha1")]
    [InlineData("Keypad1", "Num 1")]
    public void Same_key_twice_is_an_error(string first, string second)
    {
        var ex = Assert.Throws<KeyMapException>(() => KeyMap.Parse($"keys:\n  {first}: L5\n  {second}: L4\n"));

        Assert.Contains("line 2", ex.Message);
        Assert.Contains("line 3", ex.Message);
    }

    /// <summary>A key's value can be one button or a list of buttons.</summary>
    [Fact]
    public void Scalar_and_list_button_values_are_both_accepted()
    {
        var map = KeyMap.Parse("keys:\n  Q: L5\n  E: [RB, A]\n");

        Assert.Equal("[L5]", map.Translate("Q", FakeGlyph));
        Assert.Equal("<rightShoulder> + <buttonSouth>", map.Translate("E", FakeGlyph));
    }

    /// <summary>YAML 1.1 boolean words (Y, N, ...) stay key and button names.</summary>
    [Fact]
    public void Yaml_boolean_lookalikes_stay_key_names()
    {
        var map = KeyMap.Parse("keys:\n  N: Y\n  Y: N\n");

        Assert.Equal("<buttonNorth>", map.Translate("N", FakeGlyph));
        Assert.Equal("[N]", map.Translate("Y", FakeGlyph));
    }

    /// <summary>The labels section changes a badge's text.</summary>
    [Fact]
    public void Labels_override_badge_text()
    {
        var map = KeyMap.Parse("labels:\n  R4: Back R\nkeys:\n  Q: R4\n");

        Assert.Equal("[Back R]", map.Translate("Q", FakeGlyph));
    }

    /// <summary>Button names match Valheim art regardless of case.</summary>
    [Fact]
    public void Button_names_are_case_insensitive()
    {
        var map = KeyMap.Parse("keys:\n  Q: [dpadup, a]\n");

        Assert.Equal("<dpad_up> + <buttonSouth>", map.Translate("Q", FakeGlyph));
    }

    /// <summary>The cgf_deck tag for a sprite name, as the mod's glyph lookup returns it.</summary>
    /// <param name="sprite">Sprite name (e.g. "r4").</param>
    /// <returns>The sprite tag.</returns>
    private static string Deck(string sprite) => $"<sprite=\"cgf_deck\" name=\"{sprite}\">";

    /// <summary>
    /// Valheim (default): Valheim's art for buttons it has, the mod's glyph for the rest; the
    /// glyph replaces the labels-section badge text (LPad Up).
    /// </summary>
    [Fact]
    public void Valheim_uses_valheim_art_then_ours()
    {
        Assert.Equal(Deck("r4") + " + <dpad_up>", SteamController.Translate("1", FakeGlyph, ModGlyphs.Tag));
        Assert.Equal(Deck("lpad_up"), SteamController.Translate("B", FakeGlyph, ModGlyphs.Tag));
    }

    /// <summary>SteamDeck: the mod's glyph for every button it has, Valheim's art for the rest.</summary>
    [Fact]
    public void SteamDeck_uses_ours_then_valheim()
    {
        var map = KeyMap.Parse("keys:\n  Q: LS\n");

        Assert.Equal(Deck("r4") + " + " + Deck("dpad_up"),
            SteamController.Translate("1", FakeGlyph, ModGlyphs.Tag, ButtonGlyphOrder.SteamDeck));
        Assert.Equal("<leftStickPress>", map.Translate("Q", FakeGlyph, ModGlyphs.Tag, ButtonGlyphOrder.SteamDeck));
    }

    /// <summary>
    /// Only the first art source that has a glyph is asked: these lookups run on the $KEY_ path,
    /// often every frame, so the other one must not build a tag that's thrown away.
    /// </summary>
    [Fact]
    public void The_second_art_source_is_asked_only_when_the_first_has_nothing()
    {
        string NoCall(string button) => throw new InvalidOperationException("asked for " + button);

        Assert.Equal("<dpad_up>", SteamController.Format(new[] { "DPadUp" }, FakeGlyph, NoCall));
        Assert.Equal(Deck("dpad_up"),
            SteamController.Format(new[] { "DPadUp" }, NoCall, ModGlyphs.Tag, ButtonGlyphOrder.SteamDeck));
    }

    /// <summary>Without the mod's glyphs (the sprite asset couldn't be built), badges stay in either order.</summary>
    /// <param name="order">Lookup order.</param>
    [Theory]
    [InlineData(ButtonGlyphOrder.Valheim)]
    [InlineData(ButtonGlyphOrder.SteamDeck)]
    public void No_mod_glyphs_keeps_badges(ButtonGlyphOrder order)
    {
        Assert.Equal("[R4] + <dpad_up>", SteamController.Translate("1", FakeGlyph, null, order));
    }

    /// <summary>A button no art covers (a back grip) keeps its badge in either order.</summary>
    /// <param name="order">Lookup order.</param>
    [Theory]
    [InlineData(ButtonGlyphOrder.Valheim)]
    [InlineData(ButtonGlyphOrder.SteamDeck)]
    public void Buttons_with_no_art_keep_their_badge(ButtonGlyphOrder order)
    {
        var map = KeyMap.Parse("keys:\n  Q: LG\n");

        Assert.Equal("[LG]", map.Translate("Q", FakeGlyph, ModGlyphs.Tag, order));
    }

    /// <summary>When Valheim returns no sprite for a button, it shows as a badge.</summary>
    [Fact]
    public void Missing_valheim_art_falls_back_to_a_badge()
    {
        var map = KeyMap.Parse("keys:\n  Q: A\n");

        Assert.Equal("[A]", map.Translate("Q", _ => null));
    }

    /// <summary>The aliases section adds spellings for a key.</summary>
    [Fact]
    public void Yaml_aliases_add_spellings()
    {
        var map = KeyMap.Parse("aliases:\n  Pickup: Q\nkeys:\n  Q: L5\n");

        Assert.Equal("[L5]", map.Translate("Pickup", FakeGlyph));
    }

    /// <summary>Invalid files throw <see cref="KeyMapException"/> with a message.</summary>
    /// <param name="yaml">An invalid mapping file.</param>
    [Theory]
    [InlineData("keys: [")]
    [InlineData("keys:\n  Q: { a: b }\n")]
    [InlineData("keys:\n  Q: []\n")]
    [InlineData("- just\n- a list\n")]
    [InlineData("keys:\n  Q: L5\n  Q: L4\n")] // duplicate key
    [InlineData("buttons:\n  Q: L5\n")]      // unknown section
    public void Invalid_yaml_throws_with_a_clear_message(string yaml)
    {
        var ex = Assert.Throws<KeyMapException>(() => KeyMap.Parse(yaml));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    /// <summary>An empty file is a valid, empty map.</summary>
    [Fact]
    public void Empty_yaml_gives_an_empty_map()
    {
        var map = KeyMap.Parse("");

        Assert.Null(map.Translate("Q", FakeGlyph));
        Assert.Equal(0, map.Count);
    }

    /// <summary>The shared empty map has no keys and translates nothing.</summary>
    [Fact]
    public void Empty_map_has_no_keys()
    {
        Assert.Equal(0, KeyMap.Empty.Count);
        Assert.Null(KeyMap.Empty.Translate("Q", FakeGlyph));
    }

    /// <summary>Count is the number of entries in the keys section.</summary>
    [Fact]
    public void Count_is_the_number_of_mapped_keys()
    {
        Assert.Equal(20, SteamController.Count);
    }

    /// <summary>
    /// The template's button list names every button that has art, from Valheim or the mod's Steam
    /// Deck glyphs (aliases included), so it can't go stale when glyphs are added.
    /// </summary>
    [Fact]
    public void Template_lists_every_button_with_art()
    {
        string yaml = KeyMapTemplate.Yaml;
        int start = yaml.IndexOf("# Buttons with art", StringComparison.Ordinal);
        int end = yaml.IndexOf("# Any other button", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "button list block not found");

        var listed = new HashSet<string>(
            Regex.Split(yaml.Substring(start, end - start), "[^A-Za-z0-9]+").Select(word => word.ToLowerInvariant()));

        Assert.All(ModGlyphs.ButtonNames.Concat(ValheimButtons.ButtonNames), button => Assert.Contains(button, listed));
    }

    /// <summary>The template written on first run is valid and maps nothing.</summary>
    [Fact]
    public void Template_parses_and_maps_nothing()
    {
        var map = KeyMap.Parse(KeyMapTemplate.Yaml);

        Assert.Equal(0, map.Count);
    }

    /// <summary>Deleting the '#' in front of the template's examples is enough to enable them.</summary>
    [Fact]
    public void Uncommenting_the_template_examples_gives_a_working_map()
    {
        // Example lines are "#  entry" (two spaces, then the entry); header text isn't
        string uncommented = string.Join("\n", KeyMapTemplate.Yaml.Split('\n')
            .Select(line => line.Length > 3 && line.StartsWith("#  ") && line[3] != ' ' ? line.Substring(1) : line));

        var map = KeyMap.Parse(uncommented);

        Assert.Equal(4, map.Count);
        Assert.Equal("[R4] + <dpad_up>", map.Translate("1", FakeGlyph));
        Assert.Equal("[L5]", map.Translate("Pickup", FakeGlyph));
        Assert.Equal("[LPad Up]", map.Translate("LeftShift", FakeGlyph));
        Assert.Equal("#9B8CFF", map.Color("R4"));
    }

    /// <summary>A section header with nothing under it ("keys:") is allowed.</summary>
    [Fact]
    public void Sections_with_no_entries_are_allowed()
    {
        var map = KeyMap.Parse("labels:\naliases:\nkeys:\n");

        Assert.Equal(0, map.Count);
    }
}
