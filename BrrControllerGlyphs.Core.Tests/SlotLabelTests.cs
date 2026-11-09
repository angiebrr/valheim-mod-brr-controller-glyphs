// ------------------------------------------------------------------------------------------------
// SlotLabelTests.cs: unit tests for the buttons that use a hotbar or quick slot (F3).
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="SlotLabel.Buttons"/>, using the fixture mapping of my Steam Controller layout.
/// </summary>
public class SlotLabelTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>No modifier keys.</summary>
    private static readonly string[] NoModifiers = Array.Empty<string>();

    /// <summary>Valheim hotbar slots (keys 1-8) use the mapped R4 combos.</summary>
    /// <param name="key">The slot's keyboard key.</param>
    /// <param name="expected">Expected buttons, comma-separated.</param>
    [Theory]
    [InlineData("1", "R4,DPadUp")]
    [InlineData("8", "R4,X")]
    public void Hotbar_slots_use_the_mapped_buttons(string key, string expected)
    {
        Assert.Equal(expected.Split(','), SlotLabel.Buttons(key, NoModifiers, SteamController.Buttons));
    }

    /// <summary>AzuEPI quick slots (Keypad1-6) use the mapped R5 combos.</summary>
    /// <param name="key">The slot's keyboard key, as AzuEPI's KeyCode name.</param>
    /// <param name="expected">Expected buttons, comma-separated.</param>
    [Theory]
    [InlineData("Keypad1", "R5,DPadUp")]
    [InlineData("Keypad6", "R5,B")]
    public void Quick_slots_use_the_mapped_buttons(string key, string expected)
    {
        Assert.Equal(expected.Split(','), SlotLabel.Buttons(key, NoModifiers, SteamController.Buttons));
    }

    /// <summary>Modifier buttons come first; unmapped modifiers keep their key name.</summary>
    [Fact]
    public void Modifiers_come_before_the_main_key()
    {
        var buttons = SlotLabel.Buttons("Keypad1", new[] { "LeftShift", "LeftAlt" }, SteamController.Buttons);

        Assert.Equal(new[] { "LPadDown", "LeftAlt", "R5", "DPadUp" }, buttons);
    }

    /// <summary>A null modifier list is treated as no modifiers.</summary>
    [Fact]
    public void Null_modifiers_mean_none()
    {
        Assert.Equal(new[] { "R5", "DPadUp" }, SlotLabel.Buttons("Keypad1", null, SteamController.Buttons));
    }

    /// <summary>A mapped modifier doesn't make up for an unmapped main key.</summary>
    [Fact]
    public void Unmapped_main_key_with_mapped_modifier_gives_null()
    {
        Assert.Null(SlotLabel.Buttons("9", new[] { "LeftShift" }, SteamController.Buttons));
    }

    /// <summary>An unmapped main key leaves the label as Valheim/AzuEPI set it.</summary>
    [Fact]
    public void Unmapped_main_key_gives_null()
    {
        Assert.Null(SlotLabel.Buttons("9", NoModifiers, SteamController.Buttons));
    }

    /// <summary>A slot with no key (null or empty) is left alone.</summary>
    /// <param name="key">Null or empty key.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_key_gives_null(string key)
    {
        Assert.Null(SlotLabel.Buttons(key, NoModifiers, SteamController.Buttons));
    }
}
