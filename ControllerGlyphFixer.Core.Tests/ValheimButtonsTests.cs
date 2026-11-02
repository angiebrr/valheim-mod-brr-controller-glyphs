// ------------------------------------------------------------------------------------------------
// ValheimButtonsTests.cs: unit tests for mapping-file button names that have Valheim glyph art.
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="ValheimButtons.TryGetControl"/>.
/// </summary>
public class ValheimButtonsTests
{
    /// <summary>Every button name with Valheim art maps to its s_gamepadSpriteMap control name.</summary>
    /// <param name="button">Normalized mapping-file button name.</param>
    /// <param name="expected">Valheim control name.</param>
    [Theory]
    [InlineData("a", "buttonSouth")]
    [InlineData("b", "buttonEast")]
    [InlineData("x", "buttonWest")]
    [InlineData("y", "buttonNorth")]
    [InlineData("lb", "leftShoulder")]
    [InlineData("rb", "rightShoulder")]
    [InlineData("lt", "leftTrigger")]
    [InlineData("rt", "rightTrigger")]
    [InlineData("ls", "leftStickPress")]
    [InlineData("rs", "rightStickPress")]
    [InlineData("back", "select")]
    [InlineData("select", "select")]
    [InlineData("start", "start")]
    [InlineData("dpadup", "dpad_up")]
    [InlineData("dpadright", "dpad_right")]
    [InlineData("dpaddown", "dpad_down")]
    [InlineData("dpadleft", "dpad_left")]
    public void Buttons_with_art_map_to_valheim_controls(string button, string expected)
    {
        Assert.True(ValheimButtons.TryGetControl(button, out string control));
        Assert.Equal(expected, control);
    }

    /// <summary>Steam-only buttons (back grips, trackpads) have no Valheim art.</summary>
    /// <param name="button">Normalized mapping-file button name.</param>
    [Theory]
    [InlineData("r4")]
    [InlineData("l5")]
    [InlineData("lpadup")]
    public void Steam_only_buttons_have_no_art(string button)
    {
        Assert.False(ValheimButtons.TryGetControl(button, out _));
    }
}
