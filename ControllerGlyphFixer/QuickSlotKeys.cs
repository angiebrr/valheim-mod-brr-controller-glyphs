// ------------------------------------------------------------------------------------------------
// QuickSlotKeys.cs: reads AzuExtendedPlayerInventory's quick-slot shortcuts from its config.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Reads AzuExtendedPlayerInventory's "Hotkey N" quick-slot shortcuts at runtime, through
    /// BepInEx's plugin registry, so this mod has no compile-time dependency on AzuEPI. The config
    /// names live in <see cref="AzuEpiQuickSlots"/>, where contract tests check them.
    /// </summary>
    internal sealed class QuickSlotKeys
    {
        /// <summary>Set after the first lookup failure so it's logged once.</summary>
        private bool m_loggedMissing;

        /// <summary>Whether AzuExtendedPlayerInventory is loaded.</summary>
        internal static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(AzuEpiQuickSlots.PluginGuid);

        /// <summary>
        /// Number of quick slots AzuEPI shows, from its config; null if it can't be read.
        /// </summary>
        internal static int? Count
        {
            get
            {
                ConfigFile config = Config();
                return config != null && config.TryGetEntry(AzuEpiQuickSlots.CountSection, AzuEpiQuickSlots.CountKey,
                    out ConfigEntry<int> entry)
                    ? entry.Value
                    : (int?)null;
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

            string keyName = AzuEpiQuickSlots.KeyName(slot);
            ConfigFile config = Config();
            if (config == null || !config.TryGetEntry(AzuEpiQuickSlots.Section, keyName, out ConfigEntry<KeyboardShortcut> entry))
            {
                if (!m_loggedMissing)
                {
                    m_loggedMissing = true;
                    Jotunn.Logger.LogWarning($"AzuEPI quick-slot shortcut [{AzuEpiQuickSlots.Section}] {keyName} not found; " +
                                             "quick-slot labels keep their text");
                }

                return false;
            }

            mainKey = entry.Value.MainKey.ToString();
            modifiers = entry.Value.Modifiers.Select(key => key.ToString()).ToArray();
            return true;
        }

        /// <summary>
        /// Gets the text AzuEPI shows on a quick slot's inventory cell.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <param name="name">Its display text, or its shortcut when that's blank.</param>
        /// <returns>True if AzuEPI is installed and has the slot's settings.</returns>
        internal static bool TryGetCellName(int slot, out string name)
        {
            name = null;
            ConfigFile config = Config();
            if (config == null ||
                !config.TryGetEntry(AzuEpiQuickSlots.Section, AzuEpiQuickSlots.KeyName(slot), out ConfigEntry<KeyboardShortcut> shortcut) ||
                !config.TryGetEntry(AzuEpiQuickSlots.Section, AzuEpiQuickSlots.DisplayTextKeyName(slot), out ConfigEntry<string> displayText))
            {
                return false;
            }

            // Same ToString AzuEPI uses for the cell name
            name = AzuEpiQuickSlots.CellName(displayText.Value, shortcut.Value.ToString());
            return true;
        }

        /// <summary>
        /// AzuEPI's config file, through BepInEx's plugin registry.
        /// </summary>
        /// <returns>The config, or null if AzuEPI isn't loaded.</returns>
        private static ConfigFile Config()
        {
            return Chainloader.PluginInfos.TryGetValue(AzuEpiQuickSlots.PluginGuid, out PluginInfo info) && info.Instance
                ? info.Instance.Config
                : null;
        }
    }
}
