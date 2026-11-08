// ------------------------------------------------------------------------------------------------
// ModShortcuts.cs: reads the keyboard shortcut settings of the other loaded mods (F9).
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
    /// Reads every KeyboardShortcut and KeyCode setting the loaded plugins have bound in their
    /// BepInEx config, read-only, for <see cref="ShortcutAudit"/>. Settings a mod keeps in its own
    /// config file, or keys it hard-codes, aren't seen (hover hints still report those).
    /// </summary>
    internal static class ModShortcuts
    {
        /// <summary>
        /// Reads the shortcuts of all loaded plugins except this one.
        /// </summary>
        /// <param name="ownGuid">This plugin's GUID, skipped.</param>
        /// <returns>The shortcuts, by plugin in load order.</returns>
        internal static List<ModShortcut> Read(string ownGuid)
        {
            var shortcuts = new List<ModShortcut>();
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values)
            {
                if (!plugin.Instance || plugin.Metadata.GUID == ownGuid)
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
                    Jotunn.Logger.LogWarning($"Couldn't read {plugin.Metadata.Name}'s shortcut settings: {ex.Message}");
                }
            }

            return shortcuts;
        }

        /// <summary>
        /// The keys of a shortcut setting's value.
        /// </summary>
        /// <param name="value">A setting's value.</param>
        /// <returns>KeyCode names (main key first, then modifiers), or null if it isn't a shortcut.</returns>
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
