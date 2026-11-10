// ------------------------------------------------------------------------------------------------
// MissingKeys.cs: keys in hover hints and other mods' keybindings that the mapping lacks (F9).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// One keyboard keybinding setting of another mod (a BepInEx KeyboardShortcut or KeyCode entry).
    /// </summary>
    public sealed class ModShortcut
    {
        /// <summary>
        /// Creates the keybinding.
        /// </summary>
        /// <param name="mod">Plugin name.</param>
        /// <param name="setting">Setting name.</param>
        /// <param name="keys">KeyCode names, main key first, then modifiers.</param>
        public ModShortcut(string mod, string setting, IReadOnlyList<string> keys)
        {
            Mod = mod;
            Setting = setting;
            Keys = keys;
        }

        /// <summary>Plugin name.</summary>
        public string Mod { get; }

        /// <summary>Setting name.</summary>
        public string Setting { get; }

        /// <summary>KeyCode names, main key first, then modifiers.</summary>
        public IReadOnlyList<string> Keys { get; }
    }

    /// <summary>
    /// A key seen in a hover hint, with the mods that may have written the hint.
    /// </summary>
    public sealed class HintKeyUse
    {
        /// <summary>
        /// Creates the hint use.
        /// </summary>
        /// <param name="key">Key name as the hint wrote it.</param>
        /// <param name="line">The plain hint line (tags removed), including "[key]".</param>
        /// <param name="mods">Mods patching the hovered object's hover text; empty if none.</param>
        public HintKeyUse(string key, string line, IReadOnlyList<string> mods)
        {
            Key = key;
            Line = line;
            Mods = mods ?? Array.Empty<string>();
        }

        /// <summary>Key name as the hint wrote it.</summary>
        public string Key { get; }

        /// <summary>The plain hint line, including "[key]".</summary>
        public string Line { get; }

        /// <summary>Mods patching the hovered object's hover text; a best guess at who wrote the line.</summary>
        public IReadOnlyList<string> Mods { get; }
    }

    /// <summary>
    /// A key the mapping lacks, with where it was found.
    /// </summary>
    public sealed class MissingKey
    {
        /// <summary>
        /// Creates the entry.
        /// </summary>
        /// <param name="key">Key name as first found.</param>
        /// <param name="uses">One line per place it was found, sorted, as the tree shows them.</param>
        /// <param name="mods">Mods using it, each once, sorted ("UI Hint" for a hint no mod patches).</param>
        public MissingKey(string key, IReadOnlyList<string> uses, IReadOnlyList<string> mods)
        {
            Key = key;
            Uses = uses;
            Mods = mods;
        }

        /// <summary>Key name as first found.</summary>
        public string Key { get; }

        /// <summary>One line per place it was found, sorted alphabetically.</summary>
        public IReadOnlyList<string> Uses { get; }

        /// <summary>Mods using it, each once, for the one-line summary.</summary>
        public IReadOnlyList<string> Mods { get; }
    }

    /// <summary>
    /// Builds "Keys missing from mapping": keys hover hints used this session and keys other mods'
    /// keybindings use, that the mapping doesn't have, merged by key. Mouse and joystick buttons are
    /// skipped: a Steam layout's keyboard keys are what the mapping describes.
    /// </summary>
    public static class MissingKeys
    {
        /// <summary>KeyCode name prefixes that aren't keyboard keys.</summary>
        private static readonly string[] NonKeyboardPrefixes = { "Mouse", "Joystick" };

        /// <summary>Marks a hover-hint use; also stands in for the mod in the summary when no mod patches it.</summary>
        private const string UiHint = "UI Hint";

        /// <summary>
        /// Finds the keys the mapping lacks, sorted by key, each with its uses and mods sorted
        /// alphabetically. Every spelling of a key (KeyCode or Input System name, any case) is one
        /// entry, shown as first found (hints before keybindings).
        /// </summary>
        /// <param name="hints">Keys seen in hover hints.</param>
        /// <param name="shortcuts">Other mods' keybinding settings.</param>
        /// <param name="map">The mapping in use.</param>
        /// <returns>Each missing key once, with where it was found.</returns>
        public static IReadOnlyList<MissingKey> Find(IEnumerable<HintKeyUse> hints, IEnumerable<ModShortcut> shortcuts,
            KeyMap map)
        {
            var uses = hints
                .Select(hint => (hint.Key, Use: HintUse(hint), Mods: hint.Mods.Count > 0 ? hint.Mods : new[] { UiHint }))
                .Concat(shortcuts.SelectMany(shortcut => shortcut.Keys.Select(key =>
                    (Key: key, Use: ShortcutUse(shortcut), Mods: (IReadOnlyList<string>)new[] { shortcut.Mod }))));

            return uses
                .Where(use => IsKeyboardKey(use.Key) && map.Buttons(use.Key) == null)
                .GroupBy(use => map.KeyId(use.Key))
                .Select(group => new MissingKey(group.First().Key,
                    Sorted(group.Select(use => use.Use)),
                    Sorted(group.SelectMany(use => use.Mods))))
                .OrderBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Each text once, sorted alphabetically (case-insensitive).
        /// </summary>
        /// <param name="texts">Texts in any order.</param>
        /// <returns>The sorted, distinct texts.</returns>
        private static string[] Sorted(IEnumerable<string> texts)
        {
            return texts.Distinct().OrderBy(text => text, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// One-line form for the .cfg and Jotunn's Mod Settings: each key with the mods using it.
        /// </summary>
        /// <param name="missing">From <see cref="Find"/>.</param>
        /// <returns>"E (AzuCraftyBoxes, Epic Loot), F1 (Valheim Configuration Manager)", or "None".</returns>
        public static string Summary(IReadOnlyList<MissingKey> missing)
        {
            return missing.Count == 0
                ? MappingStatus.None
                : string.Join(", ", missing.Select(key => $"{key.Key} ({string.Join(", ", key.Mods)})"));
        }

        /// <summary>
        /// Tree form for ConfigurationManager (drawn in a monospace font): a line per key, then a
        /// branch per place it was found.
        /// </summary>
        /// <param name="missing">From <see cref="Find"/>.</param>
        /// <returns>The tree with "\n" line breaks, or "None".</returns>
        public static string Tree(IReadOnlyList<MissingKey> missing)
        {
            if (missing.Count == 0)
            {
                return MappingStatus.None;
            }

            var lines = new List<string>();
            foreach (MissingKey key in missing)
            {
                lines.Add(key.Key);
                for (int i = 0; i < key.Uses.Count; i++)
                {
                    lines.Add((i == key.Uses.Count - 1 ? "└─ " : "├─ ") + key.Uses[i]);
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Long form for the log: every place the key was found.
        /// </summary>
        /// <param name="key">One entry from <see cref="Find"/>.</param>
        /// <returns>"Key E isn't in your mapping; used by Epic Loot > Socket Overlay Shortcut, ...".</returns>
        public static string LogLine(MissingKey key)
        {
            return $"Key {key.Key} isn't in your mapping; used by {string.Join(", ", key.Uses)}";
        }

        /// <summary>
        /// A hint as the tree shows it, shaped like a keybinding line: the mods that may have written
        /// it, "UI Hint", then its text without the key.
        /// </summary>
        /// <param name="hint">The hint use.</param>
        /// <returns>"AzuCraftyBoxes > UI Hint > Fill all", "A or B > UI Hint > Fill all", or "UI Hint > Fill all".</returns>
        private static string HintUse(HintKeyUse hint)
        {
            string mods = hint.Mods.Count > 0 ? string.Join(" or ", hint.Mods) + " > " : "";
            string text = hint.Line.Replace("[" + hint.Key + "]", "").Trim();
            return mods + UiHint + (text.Length > 0 ? " > " + text : "");
        }

        /// <summary>
        /// A keybinding as the tree shows it, with the whole shortcut when it's a combo.
        /// </summary>
        /// <param name="shortcut">The keybinding.</param>
        /// <returns>"AzuCraftyBoxes > Container Toggle Key (E + LeftShift)".</returns>
        private static string ShortcutUse(ModShortcut shortcut)
        {
            string combo = shortcut.Keys.Count > 1 ? $" ({string.Join(" + ", shortcut.Keys)})" : "";
            return $"{shortcut.Mod} > {shortcut.Setting}{combo}";
        }

        /// <summary>
        /// Whether a KeyCode name is a keyboard key (not unbound, a mouse or a joystick button).
        /// </summary>
        /// <param name="key">KeyCode name.</param>
        /// <returns>True for keyboard keys.</returns>
        private static bool IsKeyboardKey(string key)
        {
            return !string.IsNullOrEmpty(key) &&
                   !string.Equals(key, "None", StringComparison.OrdinalIgnoreCase) &&
                   !NonKeyboardPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
