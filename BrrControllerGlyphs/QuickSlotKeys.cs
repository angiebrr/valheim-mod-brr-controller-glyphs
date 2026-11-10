// ------------------------------------------------------------------------------------------------
// QuickSlotKeys.cs: reads AzuExtendedPlayerInventory's quick-slot shortcuts from its config.
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs
{
    /// <summary>
    /// Reads AzuExtendedPlayerInventory's quick-slot settings ("Hotkey N", "Hotkey N Display Text",
    /// "Number of Quick Slots") at runtime, through BepInEx's plugin registry, so this mod has no
    /// compile-time dependency on AzuEPI. The names live in <see cref="AzuEpiQuickSlots"/>, where
    /// contract tests check them.
    /// </summary>
    internal sealed class QuickSlotKeys
    {
        /// <summary>Most quick slots AzuEPI supports (it keeps settings for all of them).</summary>
        internal const int MaxSlots = 8;

        /// <summary>Set after the first lookup failure so it's logged once.</summary>
        private bool m_loggedMissing;

        /// <summary>Whether AzuExtendedPlayerInventory is loaded.</summary>
        internal bool IsInstalled => Config() != null;

        /// <summary>
        /// Number of quick slots AzuEPI shows: its "Number of Quick Slots" (at most
        /// <see cref="MaxSlots"/>); <see cref="MaxSlots"/> if that can't be read; 0 without AzuEPI.
        /// Only these count: unused slots keep default shortcuts (Alt + 7, ...) that would split groups.
        /// </summary>
        internal int SlotCount
        {
            get
            {
                ConfigFile config = Config();
                if (config == null)
                {
                    return 0;
                }

                return config.TryGetEntry(AzuEpiQuickSlots.CountSection, AzuEpiQuickSlots.CountKey, out ConfigEntry<int> count)
                    ? Math.Max(0, Math.Min(count.Value, MaxSlots))
                    : MaxSlots;
            }
        }

        /// <summary>
        /// Gets the keyboard shortcut for a quick slot.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <param name="mainKey">The shortcut's main key (KeyCode name, e.g. "Keypad1").</param>
        /// <param name="modifiers">Modifier keys (KeyCode names), possibly empty.</param>
        /// <returns>True if AzuEPI is installed and has a shortcut for the slot.</returns>
        internal bool TryGet(int slot, out string mainKey, out IReadOnlyList<string> modifiers)
        {
            mainKey = null;
            modifiers = null;
            if (!TryGetEntry(slot, AzuEpiQuickSlots.KeyName(slot), out ConfigEntry<KeyboardShortcut> shortcut))
            {
                return false;
            }

            mainKey = shortcut.Value.MainKey.ToString();
            modifiers = shortcut.Value.Modifiers.Select(key => key.ToString()).ToArray();
            return true;
        }

        /// <summary>
        /// Gets the text AzuEPI shows on a quick slot's inventory cell.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <param name="name">Its display text, or its shortcut when that's blank.</param>
        /// <returns>True if AzuEPI is installed and has the slot's settings.</returns>
        internal bool TryGetCellName(int slot, out string name)
        {
            name = null;
            if (!TryGetEntry(slot, AzuEpiQuickSlots.KeyName(slot), out ConfigEntry<KeyboardShortcut> shortcut) ||
                !TryGetEntry(slot, AzuEpiQuickSlots.DisplayTextKeyName(slot), out ConfigEntry<string> displayText))
            {
                return false;
            }

            // Same ToString AzuEPI uses for the cell name
            name = AzuEpiQuickSlots.CellName(displayText.Value, shortcut.Value.ToString());
            return true;
        }

        /// <summary>
        /// Gets one of AzuEPI's quick-slot settings, warning once if AzuEPI is installed but the
        /// setting is missing (it was renamed).
        /// </summary>
        /// <typeparam name="T">The setting's type.</typeparam>
        /// <param name="slot">Zero-based quick-slot index, for the warning.</param>
        /// <param name="key">The setting's key in AzuEPI's quick-slot section.</param>
        /// <param name="entry">The setting when found.</param>
        /// <returns>True if found.</returns>
        private bool TryGetEntry<T>(int slot, string key, out ConfigEntry<T> entry)
        {
            entry = null;
            ConfigFile config = Config();
            if (config == null)
            {
                return false;
            }

            if (config.TryGetEntry(AzuEpiQuickSlots.Section, key, out entry))
            {
                return true;
            }

            if (!m_loggedMissing)
            {
                m_loggedMissing = true;
                Jotunn.Logger.LogWarning($"AzuEPI quick-slot setting [{AzuEpiQuickSlots.Section}] {key} (slot {slot + 1}) " +
                                         "not found; quick-slot labels keep their text");
            }

            return false;
        }

        /// <summary>
        /// AzuEPI's config file, through BepInEx's plugin registry.
        /// </summary>
        /// <returns>The config, or null if AzuEPI isn't loaded.</returns>
        private ConfigFile Config()
        {
            return Chainloader.PluginInfos.TryGetValue(AzuEpiQuickSlots.PluginGuid, out PluginInfo info) && info.Instance
                ? info.Instance.Config
                : null;
        }
    }
}
