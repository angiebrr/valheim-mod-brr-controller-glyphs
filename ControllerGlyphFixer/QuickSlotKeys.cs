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
            ConfigFile config = Chainloader.PluginInfos.TryGetValue(AzuEpiQuickSlots.PluginGuid, out PluginInfo info) && info.Instance
                ? info.Instance.Config
                : null;
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
    }
}
