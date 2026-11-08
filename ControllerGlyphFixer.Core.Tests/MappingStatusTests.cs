// ------------------------------------------------------------------------------------------------
// MappingStatusTests.cs: unit tests for the mapping status lines shown in the settings (F9).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="MappingStatus"/>.
/// </summary>
public class MappingStatusTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>A loaded file reports how many keys it maps, in the right number.</summary>
    /// <param name="count">Mapped keys.</param>
    /// <param name="expected">Status line.</param>
    [Theory]
    [InlineData(20, "Loaded 20 keys")]
    [InlineData(1, "Loaded 1 key")]
    public void Loaded_counts_the_keys(int count, string expected)
    {
        Assert.Equal(expected, MappingStatus.Loaded(count));
    }

    /// <summary>A file with nothing mapped (the untouched template) says what to do next.</summary>
    [Fact]
    public void Loaded_with_no_keys_points_to_the_file()
    {
        Assert.Equal("No keys mapped yet: edit the mapping file to add your buttons", MappingStatus.Loaded(0));
    }

    /// <summary>An error while a good mapping is loaded says the old one is still in use.</summary>
    [Fact]
    public void Failed_reload_keeps_the_last_good_mapping()
    {
        Assert.Equal(
            "Error: 'q' (line 9) is the same key as 'Q' (line 4). Still using the last good mapping (20 keys)",
            MappingStatus.Failed("'q' (line 9) is the same key as 'Q' (line 4)", lastGoodCount: 20));
    }

    /// <summary>An error with no good mapping before it (at startup) says nothing is mapped.</summary>
    [Fact]
    public void Failed_first_load_maps_nothing()
    {
        Assert.Equal(
            "Error: Invalid YAML at line 3: While scanning a plain scalar. No keys are mapped until it's fixed",
            MappingStatus.Failed("Invalid YAML at line 3: While scanning a plain scalar", lastGoodCount: null));
    }

    /// <summary>No missing keys reads as "None".</summary>
    [Fact]
    public void Missing_keys_none()
    {
        Assert.Equal("None", MappingStatus.MissingKeys(new Dictionary<string, string>(), SteamController));
    }

    /// <summary>Missing keys list each key with its hint (bracketed key dropped), sorted by key.</summary>
    [Fact]
    public void Missing_keys_list_each_key_with_its_hint()
    {
        var missing = new Dictionary<string, string>
        {
            { "T", "[T] Toggle crafting chest" },
            { "E", "[E] Fill all" },
        };

        Assert.Equal("E (Fill all), T (Toggle crafting chest)", MappingStatus.MissingKeys(missing, SteamController));
    }

    /// <summary>Keys the mapping now covers (added and reloaded) drop off the list.</summary>
    [Fact]
    public void Missing_keys_skip_keys_the_mapping_now_has()
    {
        var missing = new Dictionary<string, string>
        {
            { "B", "[B] AutoFuel: On" },
            { "E", "[E] Fill all" },
        };

        Assert.Equal("E (Fill all)", MappingStatus.MissingKeys(missing, SteamController));
    }

    /// <summary>A hint that is only the key (nothing else on the line) lists the key alone.</summary>
    [Fact]
    public void Missing_key_with_no_other_text()
    {
        var missing = new Dictionary<string, string> { { "E", "[E]" } };

        Assert.Equal("E", MappingStatus.MissingKeys(missing, SteamController));
    }
}
