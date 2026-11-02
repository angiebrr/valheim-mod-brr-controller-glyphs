namespace ControllerGlyphFixer.Core
{
    public static class GlyphMode
    {
        /// <summary>
        /// Whether prompts should show controller glyphs regardless of the last input device.
        /// </summary>
        /// <param name="enabled">Mod config master switch.</param>
        /// <param name="gamepadEnabled">Gamepad not disabled in Valheim's settings.</param>
        /// <param name="gamepadPresent">A gamepad device is connected.</param>
        /// <param name="settingsOpen">The Settings screen is open, where the keyboard rebinding
        /// UI must keep showing real keyboard keys.</param>
        public static bool Decide(bool enabled, bool gamepadEnabled, bool gamepadPresent, bool settingsOpen)
        {
            return enabled && gamepadEnabled && gamepadPresent && !settingsOpen;
        }
    }
}
