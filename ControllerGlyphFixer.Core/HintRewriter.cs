// ------------------------------------------------------------------------------------------------
// HintRewriter.cs: translates key names in hover-text hints like "[B] AutoFuel" (F4).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// A key name a hint uses that the mapping doesn't have, and where it was seen.
    /// </summary>
    public sealed class UnmappedKey
    {
        /// <summary>
        /// Creates the report.
        /// </summary>
        /// <param name="key">The key name as written.</param>
        /// <param name="line">The hint's line, without formatting tags.</param>
        public UnmappedKey(string key, string line)
        {
            Key = key;
            Line = line;
        }

        /// <summary>The key name as written (e.g. "E").</summary>
        public string Key { get; }

        /// <summary>The hint's line without formatting tags, to tell users where it came from.</summary>
        public string Line { get; }
    }

    /// <summary>
    /// The result of <see cref="HintRewriter.Rewrite"/>.
    /// </summary>
    public sealed class HintRewrite
    {
        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="text">The rewritten text.</param>
        /// <param name="unmappedKeys">Key names found in hints that the mapping doesn't have.</param>
        public HintRewrite(string text, IReadOnlyList<UnmappedKey> unmappedKeys)
        {
            Text = text;
            UnmappedKeys = unmappedKeys;
        }

        /// <summary>The rewritten text.</summary>
        public string Text { get; }

        /// <summary>Key names found in hints that the mapping doesn't have, in order (may repeat).</summary>
        public IReadOnlyList<UnmappedKey> UnmappedKeys { get; }
    }

    /// <summary>
    /// Translates key names that mods write into hover-text hints ("[B] AutoFuel",
    /// "[&lt;color=yellow&gt;&lt;b&gt;E + LeftShift&lt;/b&gt;&lt;/color&gt;]") through the key mapping.
    /// Only text inside "[ … ]" whose every part is a key name, a glyph or formatting counts as a
    /// hint; anything else is left alone. Keys are just text here: actions ($KEY_Use) are resolved
    /// earlier by Valheim's bindings (F1).
    /// </summary>
    public static class HintRewriter
    {
        /// <summary>Separates the keys of a combo ("E + LeftShift").</summary>
        private const char ComboSeparator = '+';

        /// <summary>Start of a TextMeshPro sprite tag, which is a glyph.</summary>
        private const string SpriteTagStart = "<sprite";

        /// <summary>Start of a TextMeshPro color tag.</summary>
        private const string ColorTagStart = "<color=";

        /// <summary>TextMeshPro's bold tag.</summary>
        private const string BoldTag = "<b>";

        /// <summary>Characters that end a run of plain text inside a hint: a tag or a nested bracket.</summary>
        private static readonly char[] RunEnds = { '<', '[' };

        /// <summary>TextMeshPro tags, removed from report lines.</summary>
        private static readonly Regex Tags = new ("<[^>]*>");

        /// <summary>
        /// Rewrites every key hint in a text.
        /// </summary>
        /// <param name="text">Hover text; null or empty passes through.</param>
        /// <param name="translateKey">Returns a key's buttons as display text, or null if unmapped.</param>
        /// <param name="isKeyName">Whether a word is a keyboard key name (Unity KeyCode names).</param>
        /// <returns>The text with mapped keys replaced (and brackets dropped from fully translated
        /// hints), plus the unmapped key names found.</returns>
        public static HintRewrite Rewrite(string text, Func<string, string> translateKey, Func<string, bool> isKeyName)
        {
            var unmapped = new List<UnmappedKey>();
            if (string.IsNullOrEmpty(text))
            {
                return new HintRewrite(text, unmapped);
            }

            var output = new StringBuilder(text.Length);
            int index = 0;
            while (index < text.Length)
            {
                int close = text[index] == '[' ? FindClosing(text, index) : -1;
                if (close < 0)
                {
                    // An unclosed "[" whose opening tags run straight into a glyph is what Valheim's
                    // gamepad cleanup leaves of a hint not colored "yellow": drop it like vanilla does
                    int glyph = text[index] == '[' ? LeftoverOpeningEnd(text, index) : -1;
                    if (glyph >= 0)
                    {
                        index = glyph;
                        continue;
                    }

                    output.Append(text[index]);
                    index++;
                    continue;
                }

                string inner = text.Substring(index + 1, close - index - 1);
                if (TryRewriteHint(inner, translateKey, isKeyName, out string rewritten, out List<string> hintUnmapped))
                {
                    // Fully translated hints lose their brackets, like vanilla's gamepad-mode hints
                    output.Append(hintUnmapped.Count == 0 ? rewritten : "[" + rewritten + "]");
                    if (hintUnmapped.Count > 0)
                    {
                        string line = PlainLine(text, index);
                        unmapped.AddRange(hintUnmapped.Select(key => new UnmappedKey(key, line)));
                    }
                }
                else
                {
                    output.Append(text, index, close - index + 1);
                }

                index = close + 1;
            }

            return new HintRewrite(output.ToString(), unmapped);
        }

        /// <summary>
        /// Finds where the glyph starts after a leftover "[&lt;color=…&gt;&lt;b&gt;". Valheim's gamepad
        /// mode turns "[&lt;color=yellow&gt;&lt;b&gt;&lt;sprite=" into "&lt;sprite=" and "\"&gt;&lt;/b&gt;&lt;/color&gt;]"
        /// into "\"&gt;"; with any other color only the second matches, leaving the opening behind
        /// (MyLittleUI's chest hints use #ffff00ff).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="open">Index of an unclosed "[".</param>
        /// <returns>Index of the sprite tag right after "[" and opening color/bold tags, or -1.</returns>
        // TODO: report to MyLittleUI's author (shudnal, ChestHoverText): its chest hints use
        // <color=#ffff00ff>, which Valheim's gamepad cleanup doesn't match; <color=yellow> fixes it at
        // the source. Once fixed upstream this repair finds nothing to do.
        private static int LeftoverOpeningEnd(string text, int open)
        {
            int index = open + 1;
            while (index < text.Length && text[index] == '<')
            {
                if (StartsWithAt(text, index, SpriteTagStart))
                {
                    return index;
                }

                bool opening = StartsWithAt(text, index, ColorTagStart) || StartsWithAt(text, index, BoldTag);
                int end = text.IndexOf('>', index);
                if (!opening || end < 0)
                {
                    return -1;
                }

                index = end + 1;
            }

            return -1;
        }

        /// <summary>
        /// Whether a text has a given string at a position (ordinal, no substring allocated).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="index">The position.</param>
        /// <param name="value">The string to look for.</param>
        /// <returns>True if the text continues with it there.</returns>
        private static bool StartsWithAt(string text, int index, string value)
        {
            return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }

        /// <summary>
        /// The line around a position, without TextMeshPro tags, for reports.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="index">A position on the line.</param>
        /// <returns>The trimmed line with tags removed.</returns>
        private static string PlainLine(string text, int index)
        {
            int start = text.LastIndexOf('\n', index) + 1;
            int end = text.IndexOf('\n', index);
            string line = text.Substring(start, (end < 0 ? text.Length : end) - start);
            return Tags.Replace(line, "").Trim();
        }

        /// <summary>
        /// Finds the "]" matching a "[", on the same line, counting nested brackets.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="open">Index of the "[".</param>
        /// <returns>Index of the matching "]", or -1 if there's none on the line.</returns>
        private static int FindClosing(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        if (depth == 0)
                        {
                            return i;
                        }

                        break;
                    case '\n':
                        return -1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Rewrites the inside of one "[ … ]" if it's a key hint.
        /// </summary>
        /// <param name="inner">Text between the brackets.</param>
        /// <param name="translateKey">See <see cref="Rewrite"/>.</param>
        /// <param name="isKeyName">See <see cref="Rewrite"/>.</param>
        /// <param name="rewritten">The inside with mapped keys replaced.</param>
        /// <param name="unmapped">Unmapped key names in this hint.</param>
        /// <returns>False if it isn't a key hint (other text, or nothing but formatting).</returns>
        private static bool TryRewriteHint(string inner, Func<string, string> translateKey, Func<string, bool> isKeyName,
            out string rewritten, out List<string> unmapped)
        {
            rewritten = null;
            unmapped = new List<string>();
            var output = new StringBuilder(inner.Length);
            int tokens = 0;
            int index = 0;
            while (index < inner.Length)
            {
                char current = inner[index];
                int end;
                if (current == '<' && (end = inner.IndexOf('>', index)) >= 0)
                {
                    // A tag: formatting, or a glyph if it's a sprite
                    string tag = inner.Substring(index, end - index + 1);
                    if (tag.StartsWith(SpriteTagStart, StringComparison.OrdinalIgnoreCase))
                    {
                        tokens++;
                    }

                    output.Append(tag);
                    index = end + 1;
                }
                else if (current == '[' && (end = FindClosing(inner, index)) >= 0)
                {
                    // A nested badge such as "[R4]" from the mapping: already a glyph
                    tokens++;
                    output.Append(inner, index, end - index + 1);
                    index = end + 1;
                }
                else
                {
                    end = NextSpecial(inner, index);
                    if (!TryRewriteText(inner.Substring(index, end - index), translateKey, isKeyName, output, unmapped, ref tokens))
                    {
                        return false;
                    }

                    index = end;
                }
            }

            rewritten = output.ToString();
            return tokens > 0;
        }

        /// <summary>
        /// Rewrites a run of text between tags: each "+"-separated piece must be a key name.
        /// </summary>
        /// <param name="run">The text run.</param>
        /// <param name="translateKey">See <see cref="Rewrite"/>.</param>
        /// <param name="isKeyName">See <see cref="Rewrite"/>.</param>
        /// <param name="output">Receives the rewritten run.</param>
        /// <param name="unmapped">Receives unmapped key names.</param>
        /// <param name="tokens">Incremented per key found.</param>
        /// <returns>False if a piece isn't a key name (so this isn't a key hint).</returns>
        private static bool TryRewriteText(string run, Func<string, string> translateKey, Func<string, bool> isKeyName,
            StringBuilder output, List<string> unmapped, ref int tokens)
        {
            string[] pieces = run.Split(ComboSeparator);
            for (int i = 0; i < pieces.Length; i++)
            {
                if (i > 0)
                {
                    output.Append(ComboSeparator);
                }

                string piece = pieces[i];
                string key = piece.Trim();
                if (key.Length == 0)
                {
                    output.Append(piece);
                    continue;
                }

                string translated = translateKey(key);
                if (translated == null && !isKeyName(key))
                {
                    return false;
                }

                if (translated == null)
                {
                    unmapped.Add(key);
                }

                // Keep the spacing around the key ("E + LeftShift")
                int start = piece.IndexOf(key, StringComparison.Ordinal);
                output.Append(piece, 0, start)
                    .Append(translated ?? key)
                    .Append(piece, start + key.Length, piece.Length - start - key.Length);
                tokens++;
            }

            return true;
        }

        /// <summary>
        /// Finds where a text run ends: the next tag or nested bracket, or the end.
        /// </summary>
        /// <param name="inner">Text between a hint's brackets.</param>
        /// <param name="start">Where the run starts.</param>
        /// <returns>Index just past the run.</returns>
        private static int NextSpecial(string inner, int start)
        {
            int next = inner.IndexOfAny(RunEnds, start + 1);
            return next < 0 ? inner.Length : next;
        }
    }
}
