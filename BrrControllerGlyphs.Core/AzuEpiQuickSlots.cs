// ------------------------------------------------------------------------------------------------
// AzuEpiQuickSlots.cs: names AzuExtendedPlayerInventory uses for its quick-slot shortcut config.
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
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
        /// unused ones (default Alt + 7, ...) must not count when finding the shared prefix.
        /// </summary>
        public const string CountKey = "Number of Quick Slots";

        /// <summary>Suffix of the config key for a quick slot's display text ("Hotkey 1 Display Text").</summary>
        public const string DisplayTextSuffix = " Display Text";

        /// <summary>
        /// Returns the config key of a quick slot's shortcut.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <returns>The key name, e.g. "Hotkey 1" for slot 0.</returns>
        public static string KeyName(int slot)
        {
            return KeyPrefix + (slot + 1);
        }

        /// <summary>
        /// Returns the config key of a quick slot's display text.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <returns>The key name, e.g. "Hotkey 1 Display Text" for slot 0.</returns>
        public static string DisplayTextKeyName(int slot)
        {
            return KeyName(slot) + DisplayTextSuffix;
        }

        /// <summary>
        /// Returns the text AzuEPI shows on a quick slot's inventory cell.
        /// </summary>
        /// <param name="displayText">The slot's "Display Text" setting.</param>
        /// <param name="shortcut">The slot's shortcut as text (KeyboardShortcut.ToString()).</param>
        /// <returns>The display text, or the shortcut when the display text is blank.</returns>
        // FIXME: copied from AzuEPI's quick-slot setup (RebuildQuickslots); update if AzuEPI changes it.
        public static string CellName(string displayText, string shortcut)
        {
            return string.IsNullOrWhiteSpace(displayText) ? shortcut : displayText;
        }
    }
}
