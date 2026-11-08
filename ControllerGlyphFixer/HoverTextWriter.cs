// ------------------------------------------------------------------------------------------------
// HoverTextWriter.cs: rewrites key names in the hover text and reports unmapped ones (F4).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using ControllerGlyphFixer.Core;
using TMPro;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Rewrites the crosshair hover text with <see cref="HintRewriter"/> while glyphs are on, so key
    /// names mods write ("[B] AutoFuel") show the buttons from the mapping. Remembers the last
    /// rewrite, since Valheim sets the same text every frame. Logs each unmapped key once, so users
    /// can see what's missing from their mapping.
    /// </summary>
    internal sealed class HoverTextWriter
    {
        /// <summary>Glyph mode, mapping lookups and the key-name check.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>Unmapped key names reported so far, with the hover line they were seen in.</summary>
        private readonly Dictionary<string, string> m_missingKeys = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>The last text Valheim set, and what it was rewritten to.</summary>
        private string m_lastInput;

        /// <summary>See <see cref="m_lastInput"/>.</summary>
        private string m_lastOutput;

        /// <summary>
        /// Creates the writer.
        /// </summary>
        /// <param name="glyphs">Glyph mode, mapping lookups and the key-name check.</param>
        internal HoverTextWriter(GlyphService glyphs)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        }

        /// <summary>
        /// Unmapped key names seen in hover hints this session, each with the line it was in.
        /// </summary>
        internal IReadOnlyDictionary<string, string> MissingKeys => m_missingKeys;

        /// <summary>
        /// Rewrites the hover text Valheim just set, if glyphs are on and it has key hints.
        /// </summary>
        /// <param name="hoverName">Hud.m_hoverName.</param>
        internal void Update(TMP_Text hoverName)
        {
            if (!hoverName || !m_glyphs.ShowHoverText)
            {
                return;
            }

            string input = hoverName.text;
            if (string.IsNullOrEmpty(input))
            {
                return;
            }

            if (input != m_lastInput)
            {
                m_lastInput = input;
                m_lastOutput = Rewrite(input);
            }

            if (input != m_lastOutput)
            {
                hoverName.text = m_lastOutput;
            }
        }

        /// <summary>
        /// Rewrites a hover text and reports its unmapped keys.
        /// </summary>
        /// <param name="text">Hover text.</param>
        /// <returns>The rewritten text.</returns>
        internal string Rewrite(string text)
        {
            HintRewrite result = HintRewriter.Rewrite(text, m_glyphs.KeyTranslator, m_glyphs.KeyNameCheck);
            foreach (UnmappedKey unmapped in result.UnmappedKeys.Where(unmapped => !m_missingKeys.ContainsKey(unmapped.Key)))
            {
                m_missingKeys[unmapped.Key] = unmapped.Line;
                Jotunn.Logger.LogInfo($"Hover hint uses key {unmapped.Key}, which isn't in your mapping file, so no " +
                                      $"controller button shows for it: \"{unmapped.Line}\"");
            }

            return result.Text;
        }
    }
}
