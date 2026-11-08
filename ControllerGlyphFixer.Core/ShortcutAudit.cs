// ------------------------------------------------------------------------------------------------
// ShortcutAudit.cs: finds keys other mods' shortcut settings use that the mapping lacks (F9).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// One keyboard shortcut setting of another mod (a BepInEx KeyboardShortcut or KeyCode entry).
    /// </summary>
    public sealed class ModShortcut
    {
        /// <summary>
        /// Creates the shortcut.
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
    /// A key the mapping lacks, with the shortcuts that use it.
    /// </summary>
    public sealed class UnmappedShortcutKey
    {
        /// <summary>
        /// Creates the entry.
        /// </summary>
        /// <param name="key">Key name as the first shortcut spells it.</param>
        /// <param name="usedBy">Shortcuts using the key, in the order read.</param>
        public UnmappedShortcutKey(string key, IReadOnlyList<ModShortcut> usedBy)
        {
            Key = key;
            UsedBy = usedBy;
        }

        /// <summary>Key name as the first shortcut spells it.</summary>
        public string Key { get; }

        /// <summary>Shortcuts using the key, in the order read.</summary>
        public IReadOnlyList<ModShortcut> UsedBy { get; }
    }

    /// <summary>
    /// Compares other mods' keyboard shortcuts with the mapping, so users see which keys they may
    /// still need to map. Mouse and joystick buttons are skipped: a Steam layout's keyboard keys are
    /// what the mapping describes.
    /// </summary>
    public static class ShortcutAudit
    {
        /// <summary>KeyCode name prefixes that aren't keyboard keys.</summary>
        private static readonly string[] NonKeyboardPrefixes = { "Mouse", "Joystick" };

        /// <summary>
        /// Returns the keys used by the shortcuts that the mapping doesn't have, sorted by key.
        /// </summary>
        /// <param name="shortcuts">Other mods' shortcut settings.</param>
        /// <param name="map">The mapping in use.</param>
        /// <returns>Each unmapped key once, with the shortcuts using it.</returns>
        public static IReadOnlyList<UnmappedShortcutKey> Unmapped(IEnumerable<ModShortcut> shortcuts, KeyMap map)
        {
            return shortcuts
                .SelectMany(shortcut => shortcut.Keys.Select(key => (Key: key, Shortcut: shortcut)))
                .Where(use => IsKeyboardKey(use.Key) && map.Buttons(use.Key) == null)
                .GroupBy(use => use.Key, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new UnmappedShortcutKey(group.First().Key,
                    group.Select(use => use.Shortcut).Distinct().ToArray()))
                .ToArray();
        }

        /// <summary>
        /// Short form for the settings: each key with the mods that use it.
        /// </summary>
        /// <param name="unmapped">From <see cref="Unmapped"/>.</param>
        /// <returns>"E (EpicLoot, AzuCraftyBoxes), F1 (ConfigurationManager)", or "None".</returns>
        public static string Summary(IReadOnlyList<UnmappedShortcutKey> unmapped)
        {
            if (unmapped.Count == 0)
            {
                return MappingStatus.None;
            }

            return string.Join(", ", unmapped.Select(key =>
                $"{key.Key} ({string.Join(", ", key.UsedBy.Select(shortcut => shortcut.Mod).Distinct())})"));
        }

        /// <summary>
        /// Long form for the log: every setting that uses the key, with its whole shortcut.
        /// </summary>
        /// <param name="key">One entry from <see cref="Unmapped"/>.</param>
        /// <returns>"Key E isn't in your mapping; used by EpicLoot > Socket Overlay Shortcut (E), ...".</returns>
        public static string LogLine(UnmappedShortcutKey key)
        {
            return $"Key {key.Key} isn't in your mapping; used by " + string.Join(", ", key.UsedBy.Select(shortcut =>
                $"{shortcut.Mod} > {shortcut.Setting} ({string.Join(" + ", shortcut.Keys)})"));
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
