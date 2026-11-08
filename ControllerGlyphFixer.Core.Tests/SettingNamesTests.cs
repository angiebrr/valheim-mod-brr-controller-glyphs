// ------------------------------------------------------------------------------------------------
// SettingNamesTests.cs: unit tests for the config's section names.
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="SettingNames"/>.
/// </summary>
public class SettingNamesTests
{
    /// <summary>Both settings UIs sort sections by name; the names must sort in reading order.</summary>
    [Fact]
    public void Sections_sort_in_reading_order()
    {
        string[] order = { SettingNames.General, SettingNames.HotbarGlyphs, SettingNames.KeyGlyphMapping };

        Assert.Equal(order, order.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(order, order.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }
}
