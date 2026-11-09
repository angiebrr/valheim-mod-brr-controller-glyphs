// ------------------------------------------------------------------------------------------------
// ExampleMappingsTests.cs: the example mappings in docs/example-mappings group the bars as named.
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// The example mappings (used for the mod page's hotbar screenshots) load, and give Valheim's
/// hotbar and AzuEPI's six quick slots the number of groups their names promise.
/// </summary>
public class ExampleMappingsTests
{
    /// <summary>Valheim's hotbar keys, slot 1 to 8.</summary>
    private static readonly string[] HotbarKeys = { "1", "2", "3", "4", "5", "6", "7", "8" };

    /// <summary>AzuEPI's quick-slot keys in the examples, six slots.</summary>
    private static readonly string[] QuickSlotKeys = { "Keypad1", "Keypad2", "Keypad3", "Keypad4", "Keypad5", "Keypad6" };

    /// <summary>
    /// Loads an example mapping from the copy next to the test assembly.
    /// </summary>
    /// <param name="name">File name without extension.</param>
    /// <returns>The parsed mapping.</returns>
    private static KeyMap Load(string name)
    {
        return KeyMap.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ExampleMappings", name + ".yaml")));
    }

    /// <summary>
    /// Groups a bar's keys the way the hotbar writer does.
    /// </summary>
    /// <param name="map">The mapping.</param>
    /// <param name="keys">The bar's keys, in slot order.</param>
    /// <returns>The bar's labels.</returns>
    private static BarLabels Bar(KeyMap map, IEnumerable<string> keys)
    {
        return BarLabels.Split(keys.Select(key => SlotLabel.Buttons(key, null, map.Buttons)).ToArray());
    }

    /// <summary>Each example maps every hotbar and quick slot, and groups both bars as its name says.</summary>
    /// <param name="name">Example file name.</param>
    /// <param name="groups">Groups expected on each bar.</param>
    [Theory]
    [InlineData("no-groups", 0)]
    [InlineData("one-group", 1)]
    [InlineData("multiple-groups", 2)]
    public void Example_groups_both_bars(string name, int groups)
    {
        KeyMap map = Load(name);

        BarLabels hotbar = Bar(map, HotbarKeys);
        BarLabels quickSlots = Bar(map, QuickSlotKeys);

        Assert.All(hotbar.Slots, Assert.NotNull);
        Assert.All(quickSlots.Slots, Assert.NotNull);
        Assert.Equal(groups, hotbar.Groups.Count);
        Assert.Equal(groups, quickSlots.Groups.Count);
    }

    /// <summary>The one-group example is the author's layout, the same as the test fixture.</summary>
    [Fact]
    public void One_group_example_matches_the_fixture()
    {
        KeyMap example = Load("one-group");
        KeyMap fixture = KeyMap.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

        foreach (string key in HotbarKeys.Concat(QuickSlotKeys).Concat(new[] { "I", "N", "Q", "Z", "B", "LeftShift" }))
        {
            Assert.Equal(fixture.Buttons(key), example.Buttons(key));
        }
    }
}
