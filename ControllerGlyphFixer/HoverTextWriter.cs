// ------------------------------------------------------------------------------------------------
// HoverTextWriter.cs: rewrites key names in the hover text and reports unmapped ones (F4).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
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

        /// <summary>Finds the mods that may have written the hint being hovered.</summary>
        private readonly HoverTextMods m_hoverTextMods;

        /// <summary>Unmapped keys reported so far, first sighting each; bounded by the keys mods use.</summary>
        private readonly Dictionary<string, HintKeyUse> m_missingKeys = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>The last text Valheim set, and what it was rewritten to.</summary>
        private string m_lastInput;

        /// <summary>See <see cref="m_lastInput"/>.</summary>
        private string m_lastOutput;

        /// <summary>Set when a rewrite adds missing keys; cleared once listeners are told.</summary>
        private bool m_missingKeysAdded;

        /// <summary>Logs the first error a <see cref="MissingKeysChanged"/> listener throws.</summary>
        private readonly PatchErrorLog m_reportErrors = new ("Couldn't update the missing keys list, leaving it as it was");

        /// <summary>
        /// Creates the writer.
        /// </summary>
        /// <param name="glyphs">Glyph mode, mapping lookups and the key-name check.</param>
        /// <param name="hoverTextMods">Finds the mods that may have written the hovered hint.</param>
        internal HoverTextWriter(GlyphService glyphs, HoverTextMods hoverTextMods)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_hoverTextMods = hoverTextMods ?? throw new ArgumentNullException(nameof(hoverTextMods));
        }

        /// <summary>
        /// Unmapped keys seen in hover hints this session, each with its line and likely mods.
        /// </summary>
        internal IEnumerable<HintKeyUse> MissingKeys => m_missingKeys.Values;

        /// <summary>Raised from <see cref="Update"/> after rewrites add keys to <see cref="MissingKeys"/>.</summary>
        internal event Action MissingKeysChanged;

        /// <summary>
        /// Forgets the cached rewrite, so the next frame rewrites the hover text again (after a
        /// setting that changes glyphs).
        /// </summary>
        internal void Invalidate()
        {
            m_lastInput = null;
            m_lastOutput = null;
        }

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
                // Both cache fields set together, so a failure can never pair this input with an
                // older rewrite (which would show the previous object's hint)
                string output = Rewrite(input);
                m_lastInput = input;
                m_lastOutput = output;
            }

            if (input != m_lastOutput)
            {
                hoverName.text = m_lastOutput;
            }

            ReportNewMissingKeys();
        }

        /// <summary>
        /// Raises <see cref="MissingKeysChanged"/> once after rewrites found new keys. Runs after the
        /// hover text is set, and catches its listeners' errors (e.g. the .cfg can't be saved), so
        /// they never disturb the hover text.
        /// </summary>
        private void ReportNewMissingKeys()
        {
            if (!m_missingKeysAdded)
            {
                return;
            }

            m_missingKeysAdded = false;
            try
            {
                MissingKeysChanged?.Invoke();
            }
            catch (Exception ex)
            {
                m_reportErrors.LogOnce(ex);
            }
        }

        /// <summary>
        /// Rewrites a hover text and reports its unmapped keys.
        /// </summary>
        /// <param name="text">Hover text.</param>
        /// <returns>The rewritten text.</returns>
        internal string Rewrite(string text)
        {
            HintRewrite result = HintRewriter.Rewrite(text, m_glyphs.HintKeyTranslator, m_glyphs.KeyNameCheck);
            foreach (UnmappedKey unmapped in result.UnmappedKeys)
            {
                if (m_missingKeys.ContainsKey(unmapped.Key))
                {
                    continue;
                }

                // Only on a key's first sighting, so the patch lookup never runs per frame
                m_missingKeys[unmapped.Key] = new HintKeyUse(unmapped.Key, unmapped.Line, m_hoverTextMods.ForHoveredObject());
                m_missingKeysAdded = true;
                Jotunn.Logger.LogInfo($"Hover hint uses key {unmapped.Key}, which isn't in your mapping file, so no " +
                                      $"controller button shows for it: \"{unmapped.Line}\"");
            }

            return result.Text;
        }
    }
}
