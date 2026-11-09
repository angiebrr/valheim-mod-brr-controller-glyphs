// ------------------------------------------------------------------------------------------------
// MonoTextDrawer.cs: draws a read-only setting as monospace text with a Copy button.
// ------------------------------------------------------------------------------------------------

using System;
using BepInEx.Configuration;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs
{
    /// <summary>
    /// A ConfigurationManager custom drawer for multi-line text that lines up, such as the
    /// unmapped-shortcut tree: a monospace label plus a Copy button. The font comes from the OS
    /// (Consolas, else Courier New), is made on first draw, and is destroyed with the plugin.
    /// </summary>
    internal sealed class MonoTextDrawer
    {
        /// <summary>OS fonts to try, in order.</summary>
        private static readonly string[] FontNames = { "Consolas", "Courier New" };

        /// <summary>Font size used when the skin's label has none (0 means the font's own size).</summary>
        private const int DefaultFontSize = 14;

        /// <summary>The text to show, read on every draw so it's always current.</summary>
        private readonly Func<string> m_text;

        /// <summary>The monospace font; null until the first draw.</summary>
        private Font m_font;

        /// <summary>Label style using <see cref="m_font"/>; null until the first draw (needs GUI.skin).</summary>
        private GUIStyle m_style;

        /// <summary>
        /// Creates the drawer.
        /// </summary>
        /// <param name="text">Returns the text to show.</param>
        internal MonoTextDrawer(Func<string> text)
        {
            m_text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>
        /// Draws the text and a Copy button. ConfigurationManager disables the GUI for read-only
        /// entries and passes a copy of the entry in its edit window, so the text comes from
        /// <see cref="m_text"/> and the button turns the GUI back on for itself.
        /// </summary>
        /// <param name="entry">The setting being drawn (unused).</param>
        internal void Draw(ConfigEntryBase entry)
        {
            string text = m_text();
            GUILayout.BeginVertical();
            GUILayout.Label(text, Style());

            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;
            if (GUILayout.Button("Copy"))
            {
                GUIUtility.systemCopyBuffer = text;
            }

            GUI.enabled = wasEnabled;
            GUILayout.EndVertical();
        }

        /// <summary>Destroys the font made for the label.</summary>
        internal void Destroy()
        {
            if (m_font)
            {
                UnityEngine.Object.Destroy(m_font);
            }

            m_font = null;
            m_style = null;
        }

        /// <summary>
        /// The monospace label style, made on first use (inside OnGUI, where GUI.skin is set), with
        /// the size of the current skin's labels so it matches the rest of the window.
        /// </summary>
        /// <returns>The style.</returns>
        private GUIStyle Style()
        {
            if (m_style == null)
            {
                m_font = Font.CreateDynamicFontFromOSFont(FontNames, DefaultFontSize);
                m_font.hideFlags = HideFlags.DontUnloadUnusedAsset;
                m_style = new GUIStyle(GUI.skin.label) { font = m_font, richText = false, wordWrap = true };
            }

            int size = GUI.skin.label.fontSize;
            m_style.fontSize = size > 0 ? size : DefaultFontSize;
            return m_style;
        }
    }
}
