// ------------------------------------------------------------------------------------------------
// BadgeVisibilityTests.cs: unit tests for when a UIGamePad button badge is shown.
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="BadgeVisibility.Show"/>. With glyphs off it must match vanilla
/// UIGamePad.Update exactly; with glyphs on, keyboard/mouse input counts as gamepad input.
/// </summary>
public class BadgeVisibilityTests
{
    /// <summary>With glyphs off, the result matches vanilla's condition for every input.</summary>
    /// <param name="interactive">The badge's button is interactive.</param>
    /// <param name="gamepadActive">ZInput.IsGamepadActive().</param>
    /// <param name="mouseActive">ZInput.IsMouseActive().</param>
    /// <param name="availableForConsoleMouse">The badge's m_availableForConsoleMouse.</param>
    /// <param name="alternativeGroupActive">The badge's alternative UI group is active.</param>
    [Theory]
    //          interactive, gamepad, mouse, console mouse, alt group
    [InlineData(true, true, false, false, false)]   // gamepad input -> shown
    [InlineData(true, true, true, false, false)]    // gamepad mouse cursor -> hidden
    [InlineData(true, true, true, true, false)]     // gamepad mouse cursor, badge allows it -> shown
    [InlineData(true, false, true, false, false)]   // keyboard/mouse input -> hidden (the bug)
    [InlineData(true, false, true, true, false)]    // keyboard/mouse input, console-mouse badge -> still hidden
    [InlineData(true, false, true, false, true)]    // keyboard/mouse input, alt group active -> shown
    [InlineData(false, true, false, false, false)]  // button not interactive -> hidden
    [InlineData(false, false, true, false, true)]   // not interactive, alt group active -> hidden
    public void Glyphs_off_matches_vanilla(bool interactive, bool gamepadActive, bool mouseActive,
        bool availableForConsoleMouse, bool alternativeGroupActive)
    {
        bool shown = BadgeVisibility.Show(interactive, gamepadActive, mouseActive, availableForConsoleMouse,
            alternativeGroupActive, showGlyphs: false);

        // Vanilla UIGamePad.Update, written out
        bool vanilla = interactive &&
                       ((gamepadActive && (!mouseActive || availableForConsoleMouse)) || alternativeGroupActive);
        Assert.Equal(vanilla, shown);
    }

    /// <summary>
    /// With glyphs on, an interactive badge stays after keyboard keys or trackpad cursor moves
    /// (Valheim's keyboard/mouse mode), as it does after gamepad input.
    /// </summary>
    /// <param name="mouseActive">ZInput.IsMouseActive(): trackpad cursor moved, or only keys pressed.</param>
    /// <param name="availableForConsoleMouse">The badge's m_availableForConsoleMouse.</param>
    [Theory]
    //          mouse, console mouse
    [InlineData(true, false)]   // trackpad cursor moved (the Y / LSt / RT badges that vanished)
    [InlineData(true, true)]    // trackpad cursor moved, console-mouse badge
    [InlineData(false, false)]  // keyboard-mapped button, no mouse device
    public void Glyphs_on_shows_badges_after_keyboard_or_mouse_input(bool mouseActive, bool availableForConsoleMouse)
    {
        bool shown = BadgeVisibility.Show(interactive: true, gamepadActive: false, mouseActive,
            availableForConsoleMouse, alternativeGroupActive: false, showGlyphs: true);

        Assert.True(shown);
    }

    /// <summary>With glyphs on, a badge whose button isn't interactive stays hidden.</summary>
    [Fact]
    public void Glyphs_on_keeps_badges_of_inactive_buttons_hidden()
    {
        bool shown = BadgeVisibility.Show(interactive: false, gamepadActive: false, mouseActive: true,
            availableForConsoleMouse: false, alternativeGroupActive: false, showGlyphs: true);

        Assert.False(shown);
    }

    /// <summary>
    /// With glyphs on, Valheim's own gamepad cursor mode keeps vanilla's choice: there the badge's
    /// button only works if it's marked for the console mouse.
    /// </summary>
    /// <param name="availableForConsoleMouse">The badge's m_availableForConsoleMouse.</param>
    [Theory]
    [InlineData(false)]  // button blocked under the gamepad cursor -> hidden
    [InlineData(true)]   // button works under the gamepad cursor -> shown
    public void Glyphs_on_keeps_vanilla_in_gamepad_cursor_mode(bool availableForConsoleMouse)
    {
        bool shown = BadgeVisibility.Show(interactive: true, gamepadActive: true, mouseActive: true,
            availableForConsoleMouse, alternativeGroupActive: false, showGlyphs: true);

        Assert.Equal(availableForConsoleMouse, shown);
    }
}
