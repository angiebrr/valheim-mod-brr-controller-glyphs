// ------------------------------------------------------------------------------------------------
// BadgeVisibility.cs: decides whether a UIGamePad button badge is shown (F6).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Whether a Valheim UIGamePad badge (the button glyph on an inventory or crafting button) is
    /// shown. Vanilla hides it after any keyboard or mouse input, so the badges vanished whenever the
    /// trackpad moved the cursor. With glyphs on, keyboard/mouse input counts as gamepad input.
    /// </summary>
    public static class BadgeVisibility
    {
        /// <summary>
        /// Decides whether the badge is shown.
        /// </summary>
        /// <param name="interactive">The badge's button is interactive (vanilla's IsInteractive()).</param>
        /// <param name="gamepadActive">ZInput.IsGamepadActive(): Valheim's real input mode.</param>
        /// <param name="mouseActive">ZInput.IsMouseActive().</param>
        /// <param name="availableForConsoleMouse">The badge's button works under Valheim's gamepad cursor.</param>
        /// <param name="alternativeGroupActive">The badge's alternative UI group is active.</param>
        /// <param name="showGlyphs">Result of <see cref="GlyphMode.Decide"/>.</param>
        /// <returns>Whether to set the badge active.</returns>
        // FIXME: copies vanilla UIGamePad.Update's badge condition; if Valheim changes it, update here
        // (tests spell out the vanilla condition).
        public static bool Show(
            bool interactive,
            bool gamepadActive,
            bool mouseActive,
            bool availableForConsoleMouse,
            bool alternativeGroupActive,
            bool showGlyphs)
        {
            // Keyboard/mouse mode becomes plain gamepad mode. Valheim's gamepad cursor mode (gamepad and
            // mouse both active) stays as it is: there the button only works if marked for it.
            bool keyboardMouseMode = !gamepadActive;
            if (showGlyphs && keyboardMouseMode)
            {
                gamepadActive = true;
                mouseActive = false;
            }

            bool gamepadBadge = gamepadActive && (!mouseActive || availableForConsoleMouse);
            return interactive && (gamepadBadge || alternativeGroupActive);
        }
    }
}
