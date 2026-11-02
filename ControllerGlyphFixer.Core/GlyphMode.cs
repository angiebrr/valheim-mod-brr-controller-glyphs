// ------------------------------------------------------------------------------------------------
// GlyphMode.cs: decides when prompts show controller glyphs instead of keyboard keys.
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Decides whether prompts should show controller glyphs, independent of the device that
    /// sent the last input (Valheim's own input mode is never changed).
    /// </summary>
    public static class GlyphMode
    {
        /// <summary>
        /// Whether prompts should show controller glyphs regardless of the last input device.
        /// </summary>
        /// <param name="enabled">Mod config switch for the feature (and the master switch).</param>
        /// <param name="gamepadEnabled">Gamepad not disabled in Valheim's settings.</param>
        /// <param name="gamepadPresent">A gamepad device is connected.</param>
        /// <param name="settingsOpen">The Settings screen is open, where the keyboard rebinding
        /// UI must keep showing real keyboard keys.</param>
        /// <returns>True when all conditions allow controller glyphs.</returns>
        public static bool Decide(bool enabled, bool gamepadEnabled, bool gamepadPresent, bool settingsOpen)
        {
            return enabled && gamepadEnabled && gamepadPresent && !settingsOpen;
        }
    }
}
