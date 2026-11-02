using ControllerGlyphFixer.Core;

namespace ControllerGlyphFixer.Core.Tests;

public class GlyphModeTests
{
    [Fact]
    public void Shows_controller_glyphs_when_everything_allows_it()
    {
        Assert.True(GlyphMode.Decide(enabled: true, gamepadEnabled: true, gamepadPresent: true, settingsOpen: false));
    }

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
