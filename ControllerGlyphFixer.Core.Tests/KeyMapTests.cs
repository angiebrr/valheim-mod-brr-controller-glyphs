using ControllerGlyphFixer.Core;

namespace ControllerGlyphFixer.Core.Tests;

public class KeyMapTests
{
    // Stands in for Valheim's sprite lookup: known Valheim control names render as <control>
    private static string FakeGlyph(string control) => $"<{control}>";

    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

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

    [Fact]
    public void Scalar_and_list_button_values_are_both_accepted()
    {
        var map = KeyMap.Parse("keys:\n  Q: L5\n  E: [RB, A]\n");

        Assert.Equal("[L5]", map.Translate("Q", FakeGlyph));
        Assert.Equal("<rightShoulder> + <buttonSouth>", map.Translate("E", FakeGlyph));
    }

    [Fact]
    public void Yaml_boolean_lookalikes_stay_key_names()
    {
        // YAML 1.1 treats Y/N/yes/no as booleans; key and button names must stay strings
        var map = KeyMap.Parse("keys:\n  N: Y\n  Y: N\n");

        Assert.Equal("<buttonNorth>", map.Translate("N", FakeGlyph));
        Assert.Equal("[N]", map.Translate("Y", FakeGlyph));
    }

    [Fact]
    public void Labels_override_badge_text()
    {
        var map = KeyMap.Parse("labels:\n  R4: Back R\nkeys:\n  Q: R4\n");

        Assert.Equal("[Back R]", map.Translate("Q", FakeGlyph));
    }

    [Fact]
    public void Button_names_are_case_insensitive()
    {
        var map = KeyMap.Parse("keys:\n  Q: [dpadup, a]\n");

        Assert.Equal("<dpad_up> + <buttonSouth>", map.Translate("Q", FakeGlyph));
    }

    [Fact]
    public void Missing_valheim_art_falls_back_to_a_badge()
    {
        var map = KeyMap.Parse("keys:\n  Q: A\n");

        Assert.Equal("[A]", map.Translate("Q", _ => null));
    }

    [Fact]
    public void Yaml_aliases_add_spellings()
    {
        var map = KeyMap.Parse("aliases:\n  Pickup: Q\nkeys:\n  Q: L5\n");

        Assert.Equal("[L5]", map.Translate("Pickup", FakeGlyph));
    }

    [Theory]
    [InlineData("keys: [")]
    [InlineData("keys:\n  Q: { a: b }\n")]
    [InlineData("keys:\n  Q: []\n")]
    [InlineData("- just\n- a list\n")]
    public void Invalid_yaml_throws_with_a_clear_message(string yaml)
    {
        var ex = Assert.Throws<KeyMapException>(() => KeyMap.Parse(yaml));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Empty_yaml_gives_an_empty_map()
    {
        var map = KeyMap.Parse("");

        Assert.Null(map.Translate("Q", FakeGlyph));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void Count_is_the_number_of_mapped_keys()
    {
        Assert.Equal(20, SteamController.Count);
    }

    [Fact]
    public void Template_parses_and_maps_nothing()
    {
        var map = KeyMap.Parse(KeyMapTemplate.Yaml);

        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void Uncommenting_the_template_examples_gives_a_working_map()
    {
        // Users enable an example by deleting its leading '#', nothing else.
        // Example lines are "#  entry" (two spaces, then the entry); header text isn't.
        string uncommented = string.Join("\n", KeyMapTemplate.Yaml.Split('\n')
            .Select(line => line.Length > 3 && line.StartsWith("#  ") && line[3] != ' ' ? line.Substring(1) : line));

        var map = KeyMap.Parse(uncommented);

        Assert.Equal(4, map.Count);
        Assert.Equal("[R4] + <dpad_up>", map.Translate("1", FakeGlyph));
        Assert.Equal("[L5]", map.Translate("Pickup", FakeGlyph));
        Assert.Equal("[LPad Up]", map.Translate("LeftShift", FakeGlyph));
    }

    [Fact]
    public void Sections_with_no_entries_are_allowed()
    {
        var map = KeyMap.Parse("labels:\naliases:\nkeys:\n");

        Assert.Equal(0, map.Count);
    }
}
