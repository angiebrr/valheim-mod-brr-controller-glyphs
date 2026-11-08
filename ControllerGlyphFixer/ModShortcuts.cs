// ------------------------------------------------------------------------------------------------
// ModShortcuts.cs: reads the keyboard keybinding settings of the other loaded mods (F9).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ControllerGlyphFixer.Core;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Other mods' KeyboardShortcut and KeyCode settings, read once and read-only from each loaded
    /// plugin's BepInEx config, for <see cref="MissingKeys"/>. Settings a mod keeps in its own config
    /// file, and keys it hard-codes, aren't seen (hover hints still report those).
    /// </summary>
    internal sealed class ModShortcuts
    {
        /// <summary>This plugin's GUID, skipped.</summary>
        private readonly string m_ownGuid;

        /// <summary>
        /// Creates the list, empty until <see cref="Read"/>.
        /// </summary>
        /// <param name="ownGuid">This plugin's GUID, skipped when reading.</param>
        internal ModShortcuts(string ownGuid)
        {
            m_ownGuid = ownGuid ?? throw new ArgumentNullException(nameof(ownGuid));
        }

        /// <summary>The keybindings read, by plugin in load order; empty until <see cref="Read"/>.</summary>
        internal IReadOnlyList<ModShortcut> Items { get; private set; } = Array.Empty<ModShortcut>();

        /// <summary>Whether <see cref="Read"/> has run.</summary>
        internal bool HasRead { get; private set; }

        /// <summary>
        /// Reads the keybindings of all loaded plugins except this one, replacing <see cref="Items"/>.
        /// </summary>
        internal void Read()
        {
            var shortcuts = new List<ModShortcut>();
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values)
            {
                if (!plugin.Instance || plugin.Metadata.GUID == m_ownGuid)
                {
                    continue;
                }

                try
                {
                    foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entry in plugin.Instance.Config)
                    {
                        IReadOnlyList<string> keys = Keys(entry.Value.BoxedValue);
                        if (keys != null)
                        {
                            shortcuts.Add(new ModShortcut(plugin.Metadata.Name, entry.Key.Key, keys));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogWarning($"Couldn't read {plugin.Metadata.Name}'s keybinding settings: {ex.Message}");
                }
            }

            Items = shortcuts;
            HasRead = true;
        }

        /// <summary>
        /// The keys of a keybinding setting's value.
        /// </summary>
        /// <param name="value">A setting's value.</param>
        /// <returns>KeyCode names (main key first, then modifiers), or null if it isn't a keybinding.</returns>
        private static IReadOnlyList<string> Keys(object value)
        {
            switch (value)
            {
                case KeyboardShortcut shortcut:
                    return new[] { shortcut.MainKey }.Concat(shortcut.Modifiers).Select(key => key.ToString()).ToArray();
                case KeyCode key:
                    return new[] { key.ToString() };
                default:
                    return null;
            }
        }
    }
}
