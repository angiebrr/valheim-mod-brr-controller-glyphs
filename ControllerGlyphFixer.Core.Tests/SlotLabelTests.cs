// ------------------------------------------------------------------------------------------------
// SlotLabelTests.cs: unit tests for hotbar and quick-slot labels (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="SlotLabel.For"/>, using the fixture mapping of my Steam Controller layout.
/// </summary>
public class SlotLabelTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>No modifier keys.</summary>
    private static readonly string[] NoModifiers = Array.Empty<string>();

    /// <summary>
    /// Translates a key through the fixture mapping with fake glyphs (&lt;control&gt;).
    /// </summary>
    /// <param name="key">Keyboard key name.</param>
    /// <returns>The mapped label, or null if unmapped.</returns>
    private static string Translate(string key) => SteamController.Translate(key, control => $"<{control}>");

    /// <summary>Valheim hotbar slots (keys 1-8) show the mapped R4 combos.</summary>
    /// <param name="key">The slot's keyboard key.</param>
    /// <param name="expected">Expected label.</param>
    [Theory]
    [InlineData("1", "[R4] + <dpad_up>")]
    [InlineData("4", "[R4] + <dpad_left>")]
    [InlineData("8", "[R4] + <buttonWest>")]
    public void Hotbar_slots_show_the_mapped_buttons(string key, string expected)
    {
        Assert.Equal(expected, SlotLabel.For(key, NoModifiers, showGlyphs: true, Translate));
    }

    /// <summary>AzuEPI quick slots (Keypad1-6) show the mapped R5 combos.</summary>
    /// <param name="key">The slot's keyboard key, as AzuEPI's KeyCode name.</param>
    /// <param name="expected">Expected label.</param>
    [Theory]
    [InlineData("Keypad1", "[R5] + <dpad_up>")]
    [InlineData("Keypad6", "[R5] + <buttonEast>")]
    public void Quick_slots_show_the_mapped_buttons(string key, string expected)
    {
        Assert.Equal(expected, SlotLabel.For(key, NoModifiers, showGlyphs: true, Translate));
    }

    /// <summary>Modifiers come first; unmapped modifiers keep their key name.</summary>
    [Fact]
    public void Modifiers_are_shown_before_the_main_key()
    {
        string label = SlotLabel.For("Keypad1", new[] { "LeftShift", "LeftAlt" }, showGlyphs: true, Translate);

        Assert.Equal("[LPad Down] + LeftAlt + [R5] + <dpad_up>", label);
    }

    /// <summary>A null modifier list is treated as no modifiers.</summary>
    [Fact]
    public void Null_modifiers_mean_none()
    {
        Assert.Equal("[R5] + <dpad_up>", SlotLabel.For("Keypad1", null, showGlyphs: true, Translate));
    }

    /// <summary>A mapped modifier doesn't make up for an unmapped main key.</summary>
    [Fact]
    public void Unmapped_main_key_with_mapped_modifier_leaves_the_label_alone()
    {
        Assert.Null(SlotLabel.For("9", new[] { "LeftShift" }, showGlyphs: true, Translate));
    }

    /// <summary>An unmapped main key leaves the label as Valheim/AzuEPI set it.</summary>
    [Fact]
    public void Unmapped_main_key_leaves_the_label_alone()
    {
        Assert.Null(SlotLabel.For("9", NoModifiers, showGlyphs: true, Translate));
    }

    /// <summary>With glyphs off, labels are left alone and nothing is translated.</summary>
    [Fact]
    public void Glyphs_off_leaves_the_label_alone()
    {
        string label = SlotLabel.For("1", NoModifiers, showGlyphs: false,
            key => throw new InvalidOperationException("should not translate " + key));

        Assert.Null(label);
    }

    /// <summary>A slot with no key (null or empty) is left alone.</summary>
    /// <param name="key">Null or empty key.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_key_leaves_the_label_alone(string key)
    {
        Assert.Null(SlotLabel.For(key, NoModifiers, showGlyphs: true, Translate));
    }
}
