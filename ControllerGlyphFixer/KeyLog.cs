// ------------------------------------------------------------------------------------------------
// KeyLog.cs: the key finder; shows each keyboard key the game receives while it's on (F9).
// ------------------------------------------------------------------------------------------------

using System;
using ControllerGlyphFixer.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Answers "which key does this controller button send?": while on, each keyboard key the game
    /// receives shows as a top-left message (and a log line) with the name to write in the mapping
    /// file and the buttons the mapping has for it. Off by default; does nothing while off.
    /// </summary>
    internal sealed class KeyLog
    {
        /// <summary>Prefix on each log line, for grepping LogOutput.log.</summary>
        private const string LogPrefix = "[cgf-keylog] ";

        /// <summary>The mapping in use (read on each press, so reloads apply).</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>
        /// Creates the key finder, switched off.
        /// </summary>
        /// <param name="glyphs">Holds the mapping in use.</param>
        internal KeyLog(GlyphService glyphs)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        }

        /// <summary>Whether pressed keys are being shown.</summary>
        internal bool IsOn { get; private set; }

        /// <summary>
        /// Switches the key finder on or off.
        /// </summary>
        /// <returns>The new state.</returns>
        internal bool Toggle()
        {
            IsOn = !IsOn;
            return IsOn;
        }

        /// <summary>
        /// Called every frame by the plugin: reports keys pressed this frame. Returns at once while off.
        /// </summary>
        internal void Update()
        {
            if (!IsOn)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key != null && key.wasPressedThisFrame)
                {
                    Report(key.keyCode);
                }
            }
        }

        /// <summary>
        /// Shows one pressed key.
        /// </summary>
        /// <param name="key">The key.</param>
        private void Report(Key key)
        {
            string text = KeyFinder.Describe(key.ToString(), m_glyphs.KeyMap);
            Jotunn.Logger.LogInfo(LogPrefix + text);
            if (MessageHud.instance)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
            }
        }
    }
}
