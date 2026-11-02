using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Maps keyboard key names (as Valheim and mods display them) to the controller buttons
    /// my Steam Input layout presses to send that key.
    /// </summary>
    public sealed class KeyMap
    {
        private const string ButtonSeparator = " + ";

        private static readonly Regex DigitPattern = new Regex(@"^(?:alpha|digit)(\d)$");
        private static readonly Regex KeypadPattern = new Regex(@"^(?:keypadnumber|keypad|numpad|num)(\d)$");

        // Valheim's localized key tokens (ZInput.s_keyLocalizationMap) back to key names
        private static readonly Dictionary<string, string> BuiltInAliases = new Dictionary<string, string>
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

        private readonly Dictionary<string, IReadOnlyList<string>> m_keys;
        private readonly Dictionary<string, string> m_aliases;
        private readonly Dictionary<string, string> m_labels;

        private KeyMap(
            Dictionary<string, IReadOnlyList<string>> keys,
            Dictionary<string, string> aliases,
            Dictionary<string, string> labels)
        {
            m_keys = keys;
            m_aliases = aliases;
            m_labels = labels;
        }

        /// <summary>Number of mapped keys.</summary>
        public int Count => m_keys.Count;

        public static KeyMap Parse(string yaml)
        {
            var keys = new Dictionary<string, IReadOnlyList<string>>();
            var aliases = new Dictionary<string, string>();
            var labels = new Dictionary<string, string>();

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
        /// <param name="valheimGlyph">Returns Valheim's sprite tag for a Valheim control name
        /// (e.g. "buttonSouth"), or null if there is no art for it.</param>
        public string Translate(string keyName, Func<string, string> valheimGlyph)
        {
            if (string.IsNullOrEmpty(keyName) || !m_keys.TryGetValue(Resolve(keyName), out var buttons))
            {
                return null;
            }

            return string.Join(ButtonSeparator, buttons.Select(button => FormatButton(button, valheimGlyph)));
        }

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

        internal static string Normalize(string name)
        {
            return new string(name.Where(c => c != ' ' && c != '_' && c != '-').ToArray()).ToLowerInvariant();
        }

        private static YamlMappingNode Mapping(YamlNode node, string sectionName)
        {
            if (node is YamlMappingNode mapping)
            {
                return mapping;
            }

            throw new KeyMapException($"Section '{sectionName}' must be a set of 'name: value' entries (line {node.Start.Line})");
        }

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

        private static string ScalarText(YamlNode node, string what)
        {
            if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                return scalar.Value;
            }

            throw new KeyMapException($"Expected a {what} at line {node.Start.Line}");
        }
    }

    public sealed class KeyMapException : Exception
    {
        public KeyMapException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
