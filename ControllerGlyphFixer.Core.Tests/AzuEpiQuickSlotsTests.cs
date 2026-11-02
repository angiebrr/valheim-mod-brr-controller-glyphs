// ------------------------------------------------------------------------------------------------
// AzuEpiQuickSlotsTests.cs: unit tests for AzuEPI's quick-slot config names.
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="AzuEpiQuickSlots"/>. The contract tests check the names against AzuEPI's DLL.
/// </summary>
public class AzuEpiQuickSlotsTests
{
    /// <summary>Zero-based slots map to AzuEPI's one-based "Hotkey N" entries.</summary>
    /// <param name="slot">Zero-based slot index.</param>
    /// <param name="expected">AzuEPI's config key name.</param>
    [Theory]
    [InlineData(0, "Hotkey 1")]
    [InlineData(5, "Hotkey 6")]
    public void Key_names_are_one_based(int slot, string expected)
    {
        Assert.Equal(expected, AzuEpiQuickSlots.KeyName(slot));
    }
}
