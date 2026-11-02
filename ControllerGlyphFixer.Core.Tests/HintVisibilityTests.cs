// ------------------------------------------------------------------------------------------------
// HintVisibilityTests.cs: unit tests for which UIInputHint version (gamepad/keyboard) is shown.
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="HintVisibility.Decide"/>. With glyphs off it must match vanilla
/// UIInputHint.UpdateInputHints exactly; with glyphs on it shows the gamepad version.
/// </summary>
public class HintVisibilityTests
{
    /// <summary>With glyphs off, the result matches vanilla's three conditions for every input.</summary>
    /// <param name="visible">The hint should show at all.</param>
    /// <param name="gamepadActive">ZInput.IsGamepadActive().</param>
    /// <param name="mouseActive">ZInput.IsMouseActive().</param>
    /// <param name="hasGamepadMouseHint">The hint has a gamepad+mouse version.</param>
    [Theory]
    //          visible, gamepad, mouse, has gamepad+mouse version
    [InlineData(true, true, false, false)]   // gamepad input -> gamepad version
    [InlineData(true, true, false, true)]    // gamepad input -> gamepad version (gamepad+mouse version unused)
    [InlineData(true, true, true, false)]    // gamepad + mouse, no gamepad+mouse version -> gamepad version
    [InlineData(true, true, true, true)]     // gamepad + mouse -> gamepad+mouse version
    [InlineData(true, false, true, false)]   // keyboard/mouse input -> keyboard version
    [InlineData(true, false, true, true)]    // keyboard/mouse input -> keyboard version (gamepad+mouse version unused)
    [InlineData(true, false, false, false)]  // keyboard but no mouse device -> nothing shown
    [InlineData(false, true, false, false)]  // hidden hint (button disabled / group inactive), gamepad -> nothing
    [InlineData(false, false, true, true)]   // hidden hint, keyboard/mouse -> nothing
    public void Glyphs_off_matches_vanilla(bool visible, bool gamepadActive, bool mouseActive, bool hasGamepadMouseHint)
    {
        HintVisibility result = HintVisibility.Decide(visible, gamepadActive, mouseActive,
            hasGamepadHint: true, hasGamepadMouseHint, showGlyphs: false);

        // Vanilla UIInputHint.UpdateInputHints, written out
        Assert.Equal(visible && gamepadActive && (!hasGamepadMouseHint || !mouseActive), result.Gamepad);
        Assert.Equal(visible && !gamepadActive && mouseActive, result.KeyboardMouse);
        Assert.Equal(visible && gamepadActive && mouseActive, result.GamepadMouse);
    }

    /// <summary>With glyphs on, a visible hint shows only its gamepad version, whatever the last input was.</summary>
    /// <param name="gamepadActive">ZInput.IsGamepadActive().</param>
    /// <param name="mouseActive">ZInput.IsMouseActive().</param>
    /// <param name="hasGamepadMouseHint">The hint has a gamepad+mouse version.</param>
    [Theory]
    //          gamepad, mouse, has gamepad+mouse version
    [InlineData(false, true, false)]   // after keyboard/mouse input (the black key-cap bug)
    [InlineData(false, true, true)]    // after keyboard/mouse input, hint also has a gamepad+mouse version
    [InlineData(true, true, true)]     // trackpad cursor while in gamepad mode
    [InlineData(true, false, false)]   // plain gamepad input (same as vanilla)
    public void Glyphs_on_shows_only_the_gamepad_version(bool gamepadActive, bool mouseActive, bool hasGamepadMouseHint)
    {
        HintVisibility result = HintVisibility.Decide(visible: true, gamepadActive, mouseActive,
            hasGamepadHint: true, hasGamepadMouseHint, showGlyphs: true);

        Assert.True(result.Gamepad);
        Assert.False(result.KeyboardMouse);
        Assert.False(result.GamepadMouse);
    }

    /// <summary>With glyphs on, a hint that shouldn't show stays hidden.</summary>
    [Fact]
    public void Glyphs_on_keeps_hidden_hints_hidden()
    {
        HintVisibility result = HintVisibility.Decide(visible: false, gamepadActive: false, mouseActive: true,
            hasGamepadHint: true, hasGamepadMouseHint: true, showGlyphs: true);

        Assert.False(result.Gamepad);
        Assert.False(result.KeyboardMouse);
        Assert.False(result.GamepadMouse);
    }

    /// <summary>A hint with no gamepad version keeps vanilla behaviour, so it doesn't vanish.</summary>
    [Fact]
    public void Hint_without_a_gamepad_version_falls_back_to_vanilla()
    {
        HintVisibility result = HintVisibility.Decide(visible: true, gamepadActive: false, mouseActive: true,
            hasGamepadHint: false, hasGamepadMouseHint: false, showGlyphs: true);

        Assert.True(result.KeyboardMouse);
        Assert.False(result.GamepadMouse);
    }
}
