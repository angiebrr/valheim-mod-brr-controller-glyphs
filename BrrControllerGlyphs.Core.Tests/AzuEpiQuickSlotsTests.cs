// ------------------------------------------------------------------------------------------------
// AzuEpiQuickSlotsTests.cs: unit tests for AzuEPI's quick-slot config names.
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

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

    /// <summary>Display-text keys are one-based too ("Hotkey 1 Display Text").</summary>
    [Fact]
    public void Display_text_key_names_are_one_based()
    {
        Assert.Equal("Hotkey 1 Display Text", AzuEpiQuickSlots.DisplayTextKeyName(0));
    }

    /// <summary>A quick slot's cell shows its display text, or its shortcut when that's blank.</summary>
    /// <param name="displayText">The "Display Text" setting.</param>
    /// <param name="shortcut">The shortcut's text.</param>
    /// <param name="expected">What AzuEPI shows.</param>
    [Theory]
    [InlineData("R5 Up", "Keypad1", "R5 Up")]
    [InlineData("", "Keypad1", "Keypad1")]
    [InlineData("  ", "Keypad1", "Keypad1")]
    [InlineData(null, "Keypad1", "Keypad1")]
    public void Cell_name_is_the_display_text_or_the_shortcut(string displayText, string shortcut, string expected)
    {
        Assert.Equal(expected, AzuEpiQuickSlots.CellName(displayText, shortcut));
    }
}
