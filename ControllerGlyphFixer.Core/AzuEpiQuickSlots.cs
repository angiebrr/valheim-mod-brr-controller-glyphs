// ------------------------------------------------------------------------------------------------
// AzuEpiQuickSlots.cs: names AzuExtendedPlayerInventory uses for its quick-slot shortcut config.
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// AzuExtendedPlayerInventory's plugin ID and quick-slot config names, copied from AzuEPI.
    /// Contract tests check them against AzuEPI's DLL in the Gale profile.
    /// </summary>
    public static class AzuEpiQuickSlots
    {
        /// <summary>AzuExtendedPlayerInventory's BepInEx plugin ID.</summary>
        public const string PluginGuid = "Azumatt.AzuExtendedPlayerInventory";

        /// <summary>Config section holding the quick-slot shortcuts.</summary>
        public const string Section = "8 - Quick Slot Hotkeys";

        /// <summary>Config key prefix; AzuEPI appends the one-based slot number.</summary>
        public const string KeyPrefix = "Hotkey ";

        /// <summary>Config section holding the number of quick slots.</summary>
        public const string CountSection = "3 - Quick Slots";

        /// <summary>
        /// Config key for the number of quick slots (0-8). AzuEPI keeps shortcuts for all 8, so
        /// unused ones (default Alt + 7, ...) must not count when finding the shared hold button.
        /// </summary>
        public const string CountKey = "Number of Quick Slots";

        /// <summary>
        /// Returns the config key of a quick slot's shortcut.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <returns>The key name, e.g. "Hotkey 1" for slot 0.</returns>
        public static string KeyName(int slot)
        {
            return KeyPrefix + (slot + 1);
        }
    }
}
