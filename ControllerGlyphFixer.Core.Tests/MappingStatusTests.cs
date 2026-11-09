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
}
