// ------------------------------------------------------------------------------------------------
// KeyMap.cs: parses the YAML key mapping and renders keys as the controller buttons that send them.
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Maps keyboard key names (as Valheim and mods display them) to the controller buttons
    /// my Steam Input layout presses to send that key.
    /// </summary>
    public sealed class KeyMap
    {
        /// <summary>Joins the buttons of a combo, e.g. "[R4] + (D-pad up glyph)".</summary>
        private const string ButtonSeparator = " + ";

        /// <summary>Top-row digit spellings ("Alpha1", "Digit1") that mean the key "1".</summary>
        private static readonly Regex DigitPattern = new (@"^(?:alpha|digit)(\d)$");

        /// <summary>Numpad spellings ("Num 1", "Numpad1", "KeypadNumber1") that mean "Keypad1".</summary>
        private static readonly Regex KeypadPattern = new (@"^(?:keypadnumber|keypad|numpad|num)(\d)$");

        /// <summary>Valheim's localized key tokens (ZInput.s_keyLocalizationMap) back to key names.</summary>
        // TODO(open question 2): collect the exact key strings mods write in hover text (e.g.
        // KeyCode names, "Left Shift") in game and add any that normalization doesn't cover.
        private static readonly Dictionary<string, string> BuiltInAliases = new ()
        {
            { "$buttonlshift", "leftshift" },
            { "$buttonrshift", "rightshift" },
            { "$buttonlalt", "leftalt" },
            { "$buttonralt", "rightalt" },
            { "$buttonlctrl", "leftctrl" },
            { "$buttonrctrl", "rightctrl" },
            { "$buttonspace", "space" },
            { "$buttonreturn", "enter" },
        };

        /// <summary>Normalized key name to the buttons pressed for it, in order.</summary>
        private readonly Dictionary<string, IReadOnlyList<string>> m_keys;

        /// <summary>Normalized alias to normalized key name, from the file's aliases section.</summary>
        private readonly Dictionary<string, string> m_aliases;

        /// <summary>Normalized button name to badge text, from the file's labels section.</summary>
        private readonly Dictionary<string, string> m_labels;

        /// <summary>
        /// Creates a map from parsed sections; use <see cref="Parse"/>.
        /// </summary>
        /// <param name="keys">Normalized key name to buttons.</param>
        /// <param name="aliases">Normalized alias to normalized key name.</param>
        /// <param name="labels">Normalized button name to badge text.</param>
        private KeyMap(
            Dictionary<string, IReadOnlyList<string>> keys,
            Dictionary<string, string> aliases,
            Dictionary<string, string> labels)
        {
            m_keys = keys;
            m_aliases = aliases;
            m_labels = labels;
        }

        /// <summary>A map with no keys; every key translates to null. Safe to share (maps are immutable).</summary>
        public static KeyMap Empty { get; } = new (new (), new (), new ());

        /// <summary>Number of mapped keys.</summary>
        public int Count => m_keys.Count;

        /// <summary>
        /// Parses a mapping file with optional "labels", "aliases" and "keys" sections.
        /// </summary>
        /// <param name="yaml">File contents; null or empty gives an empty map.</param>
        /// <returns>The parsed map.</returns>
        /// <exception cref="KeyMapException">The YAML is invalid or doesn't match the format.</exception>
        public static KeyMap Parse(string yaml)
        {
            var keys = new Dictionary<string, IReadOnlyList<string>>();
            var aliases = new Dictionary<string, string>();
            var labels = new Dictionary<string, string>();

            // The representation model keeps every scalar a string, so Y/N/yes/no stay key names
            var stream = new YamlStream();
            try
            {
                stream.Load(new StringReader(yaml ?? ""));
            }
            catch (YamlException ex)
            {
                throw new KeyMapException($"Invalid YAML at line {ex.Start.Line}: {ex.Message}", ex);
            }

            if (stream.Documents.Count == 0)
            {
                return new KeyMap(keys, aliases, labels);
            }

            if (!(stream.Documents[0].RootNode is YamlMappingNode root))
            {
                throw new KeyMapException("The mapping file must be a set of sections (aliases, labels, keys)");
            }

            foreach (var section in root.Children)
            {
                string sectionName = ScalarText(section.Key, "section name");
                switch (sectionName)
                {
                    case "keys":
                        foreach (var entry in Mapping(section.Value, sectionName).Children)
                        {
                            string key = ScalarText(entry.Key, "key name");
                            keys[Normalize(key)] = Buttons(entry.Value, key);
                        }
                        break;
                    case "aliases":
                        foreach (var entry in Mapping(section.Value, sectionName).Children)
                        {
                            aliases[Normalize(ScalarText(entry.Key, "alias"))] = Normalize(ScalarText(entry.Value, "alias target"));
                        }
                        break;
                    case "labels":
                        foreach (var entry in Mapping(section.Value, sectionName).Children)
                        {
                            labels[Normalize(ScalarText(entry.Key, "button name"))] = ScalarText(entry.Value, "label");
                        }
                        break;
                    default:
                        throw new KeyMapException($"Unknown section '{sectionName}' (expected aliases, labels or keys)");
                }
            }

            return new KeyMap(keys, aliases, labels);
        }

        /// <summary>
        /// Returns the display string for a key, or null if the key isn't mapped.
        /// </summary>
        /// <param name="keyName">Key name in any supported spelling (e.g. "Q", "Alpha1", "$button_lshift").</param>
        /// <param name="valheimGlyph">Returns Valheim's sprite tag for a Valheim control name
        /// (e.g. "buttonSouth"), or null if there is no art for it.</param>
        /// <returns>The buttons joined with " + ", each as a sprite tag or a [badge]; null if unmapped.</returns>
        public string Translate(string keyName, Func<string, string> valheimGlyph)
        {
            if (string.IsNullOrEmpty(keyName) || !m_keys.TryGetValue(Resolve(keyName), out var buttons))
            {
                return null;
            }

            return string.Join(ButtonSeparator, buttons.Select(button => FormatButton(button, valheimGlyph)));
        }

        /// <summary>
        /// Renders one button: Valheim's glyph when it has art, otherwise a [badge].
        /// </summary>
        /// <param name="button">Button name as written in the mapping file.</param>
        /// <param name="valheimGlyph">See <see cref="Translate"/>.</param>
        /// <returns>A sprite tag or a badge such as "[R4]" (using the label when one is set).</returns>
        // TODO(F5): show Steam's controller glyph images for buttons Valheim has no art for
        // (R4/R5, L4/L5, trackpads) instead of text badges: load the PNGs into a custom TMP sprite
        // asset in the plugin and pass a second glyph lookup here, keeping the badge as fallback.
        private string FormatButton(string button, Func<string, string> valheimGlyph)
        {
            string normalized = Normalize(button);
            if (ValheimButtons.TryGetControl(normalized, out string control))
            {
                string glyph = valheimGlyph(control);
                if (!string.IsNullOrEmpty(glyph))
                {
                    return glyph;
                }
            }

            return "[" + (m_labels.TryGetValue(normalized, out string label) ? label : button) + "]";
        }

        /// <summary>
        /// Turns any supported spelling of a key into the normalized name used in <see cref="m_keys"/>.
        /// </summary>
        /// <param name="keyName">Key name as Valheim or a mod wrote it.</param>
        /// <returns>The normalized key name (file aliases, then built-in aliases and patterns).</returns>
        private string Resolve(string keyName)
        {
            string normalized = Normalize(keyName);
            if (m_aliases.TryGetValue(normalized, out string target))
            {
                normalized = target;
            }

            if (BuiltInAliases.TryGetValue(normalized, out string builtIn))
            {
                return builtIn;
            }

            var digit = DigitPattern.Match(normalized);
            if (digit.Success)
            {
                return digit.Groups[1].Value;
            }

            var keypad = KeypadPattern.Match(normalized);
            if (keypad.Success)
            {
                return "keypad" + keypad.Groups[1].Value;
            }

            return normalized;
        }

        /// <summary>
        /// Lowercases a name and drops spaces, '_' and '-', so "Left Shift" equals "left_shift".
        /// </summary>
        /// <param name="name">Key or button name.</param>
        /// <returns>The normalized name.</returns>
        internal static string Normalize(string name)
        {
            return new string(name.Where(c => c != ' ' && c != '_' && c != '-').ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Returns a section's entries, treating an empty section ("keys:" alone) as no entries.
        /// </summary>
        /// <param name="node">The section's value node.</param>
        /// <param name="sectionName">Section name, for error messages.</param>
        /// <returns>The section as a mapping node.</returns>
        /// <exception cref="KeyMapException">The section isn't a set of "name: value" entries.</exception>
        private static YamlMappingNode Mapping(YamlNode node, string sectionName)
        {
            if (node is YamlMappingNode mapping)
            {
                return mapping;
            }

            // "keys:" with nothing after it (e.g. every example commented out)
            if (node is YamlScalarNode scalar && string.IsNullOrEmpty(scalar.Value))
            {
                return new YamlMappingNode();
            }

            throw new KeyMapException($"Section '{sectionName}' must be a set of 'name: value' entries (line {node.Start.Line})");
        }

        /// <summary>
        /// Reads a key's buttons: a single button ("Q: L5") or a non-empty list ("1": [R4, DPadUp]).
        /// </summary>
        /// <param name="node">The key's value node.</param>
        /// <param name="key">Key name, for error messages.</param>
        /// <returns>The button names in order.</returns>
        /// <exception cref="KeyMapException">The value is empty, a nested mapping, or an empty list.</exception>
        private static IReadOnlyList<string> Buttons(YamlNode node, string key)
        {
            switch (node)
            {
                case YamlScalarNode scalar when !string.IsNullOrWhiteSpace(scalar.Value):
                    return new[] { scalar.Value };
                case YamlSequenceNode sequence when sequence.Children.Count > 0:
                    return sequence.Children.Select(child => ScalarText(child, $"button for '{key}'")).ToArray();
                default:
                    throw new KeyMapException($"Key '{key}' needs a button or a list of buttons (line {node.Start.Line})");
            }
        }

        /// <summary>
        /// Returns a node's text when it's a non-empty scalar.
        /// </summary>
        /// <param name="node">The node to read.</param>
        /// <param name="what">What the value is, for error messages (e.g. "key name").</param>
        /// <returns>The scalar's text.</returns>
        /// <exception cref="KeyMapException">The node isn't a non-empty scalar.</exception>
        private static string ScalarText(YamlNode node, string what)
        {
            if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                return scalar.Value;
            }

            throw new KeyMapException($"Expected a {what} at line {node.Start.Line}");
        }
    }

    /// <summary>
    /// The mapping file is invalid; the message says what's wrong and where.
    /// </summary>
    public sealed class KeyMapException : Exception
    {
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="message">What's wrong with the file, including the line when known.</param>
        /// <param name="inner">The underlying YAML parser error, if any.</param>
        public KeyMapException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
