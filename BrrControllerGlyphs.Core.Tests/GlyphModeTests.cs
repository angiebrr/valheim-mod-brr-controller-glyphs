// ------------------------------------------------------------------------------------------------
// GlyphModeTests.cs: unit tests for when controller glyphs are shown.
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="GlyphMode.Decide"/>: the full truth table of its four inputs.
/// </summary>
public class GlyphModeTests
{
    /// <summary>Glyphs show when every input allows it.</summary>
    [Fact]
    public void Shows_controller_glyphs_when_everything_allows_it()
    {
        Assert.True(GlyphMode.Decide(enabled: true, gamepadEnabled: true, gamepadPresent: true, settingsOpen: false));
    }

    /// <summary>Any single blocker turns glyphs off.</summary>
    /// <param name="enabled">Config switch.</param>
    /// <param name="gamepadEnabled">Gamepad enabled in Valheim settings.</param>
    /// <param name="gamepadPresent">Gamepad device connected.</param>
    /// <param name="settingsOpen">Settings screen open.</param>
    [Theory]
    [InlineData(false, true, true, false)]  // turned off in config
    [InlineData(true, false, true, false)]  // gamepad disabled in Valheim settings
    [InlineData(true, true, false, false)]  // no gamepad device
    [InlineData(true, true, true, true)]    // Settings screen open (keyboard rebinding UI)
    public void Any_blocker_turns_controller_glyphs_off(bool enabled, bool gamepadEnabled, bool gamepadPresent, bool settingsOpen)
    {
        Assert.False(GlyphMode.Decide(enabled, gamepadEnabled, gamepadPresent, settingsOpen));
    }
}
