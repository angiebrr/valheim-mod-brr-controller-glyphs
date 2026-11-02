// ------------------------------------------------------------------------------------------------
// QuickSlotKeys.cs: reads AzuExtendedPlayerInventory's quick-slot shortcuts from its config.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Reads AzuExtendedPlayerInventory's "Hotkey N" quick-slot shortcuts at runtime, through
    /// BepInEx's plugin registry, so this mod has no compile-time dependency on AzuEPI.
    /// </summary>
    internal sealed class QuickSlotKeys
    {
        /// <summary>AzuExtendedPlayerInventory's BepInEx plugin ID.</summary>
        private const string AzuEpiGuid = "Azumatt.AzuExtendedPlayerInventory";

        /// <summary>Config section holding the quick-slot shortcuts.</summary>
        // FIXME: copied from AzuEPI's config ("8 - Quick Slot Hotkeys", "Hotkey N"); if AzuEPI renames
        // them, quick-slot labels silently stay as AzuEPI/Valheim set them (a warning is logged once).
        private const string Section = "8 - Quick Slot Hotkeys";

        /// <summary>Set after the first lookup failure so it's logged once.</summary>
        private bool m_loggedMissing;

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

            ConfigFile config = Chainloader.PluginInfos.TryGetValue(AzuEpiGuid, out PluginInfo info) && info.Instance
                ? info.Instance.Config
                : null;
            if (config == null || !config.TryGetEntry(Section, $"Hotkey {slot + 1}", out ConfigEntry<KeyboardShortcut> entry))
            {
                if (!m_loggedMissing)
                {
                    m_loggedMissing = true;
                    Jotunn.Logger.LogWarning($"AzuEPI quick-slot shortcut [{Section}] Hotkey {slot + 1} not found; " +
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
