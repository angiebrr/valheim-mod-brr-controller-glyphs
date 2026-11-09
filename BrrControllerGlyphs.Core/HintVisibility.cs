// ------------------------------------------------------------------------------------------------
// HintVisibility.cs: decides which version of a UIInputHint (gamepad or keyboard) to show (F6).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Which of a Valheim UIInputHint's three versions to show. Valheim picks by input mode, so after
    /// keyboard input it shows the keyboard version (key-cap boxes), which then wrongly holds our
    /// controller glyphs. With glyphs on, we show the gamepad version instead.
    /// </summary>
    public readonly struct HintVisibility
    {
        /// <summary>Show the gamepad version (m_gamepadHint).</summary>
        public bool Gamepad { get; }

        /// <summary>Show the keyboard/mouse version (m_mouseKeyboardHint).</summary>
        public bool KeyboardMouse { get; }

        /// <summary>Show the gamepad+mouse version (m_gamepadMouseHint).</summary>
        public bool GamepadMouse { get; }

        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="gamepad">Show the gamepad version.</param>
        /// <param name="keyboardMouse">Show the keyboard/mouse version.</param>
        /// <param name="gamepadMouse">Show the gamepad+mouse version.</param>
        private HintVisibility(bool gamepad, bool keyboardMouse, bool gamepadMouse)
        {
            Gamepad = gamepad;
            KeyboardMouse = keyboardMouse;
            GamepadMouse = gamepadMouse;
        }

        /// <summary>
        /// Decides which hint versions to show.
        /// </summary>
        /// <param name="visible">Whether the hint should show at all (vanilla's button/group check).</param>
        /// <param name="gamepadActive">ZInput.IsGamepadActive(): Valheim's real input mode.</param>
        /// <param name="mouseActive">ZInput.IsMouseActive().</param>
        /// <param name="hasGamepadHint">The hint has a gamepad version; without one, keep vanilla
        /// behaviour so the hint doesn't vanish.</param>
        /// <param name="hasGamepadMouseHint">The hint has a gamepad+mouse version.</param>
        /// <param name="showGlyphs">Result of <see cref="GlyphMode.Decide"/>.</param>
        /// <returns>Which versions to set active.</returns>
        // FIXME: the glyphs-off branch copies vanilla UIInputHint.UpdateInputHints. If Valheim changes
        // that logic, update it here (tests spell out the vanilla conditions).
        public static HintVisibility Decide(
            bool visible,
            bool gamepadActive,
            bool mouseActive,
            bool hasGamepadHint,
            bool hasGamepadMouseHint,
            bool showGlyphs)
        {
            // Controller glyphs on: only the gamepad version, whatever the last input was
            if (showGlyphs && hasGamepadHint)
            {
                return new HintVisibility(gamepad: visible, keyboardMouse: false, gamepadMouse: false);
            }

            // Vanilla: gamepad version for gamepad input, unless the mouse is active and there's a
            // gamepad+mouse version to show instead
            bool showGamepad = visible && gamepadActive && (!hasGamepadMouseHint || !mouseActive);

            // Vanilla: keyboard version for keyboard/mouse input
            bool showKeyboardMouse = visible && !gamepadActive && mouseActive;

            // Vanilla: gamepad+mouse version when both are active (e.g. trackpad cursor in gamepad mode)
            bool showGamepadMouse = visible && gamepadActive && mouseActive;

            return new HintVisibility(showGamepad, showKeyboardMouse, showGamepadMouse);
        }
    }
}
