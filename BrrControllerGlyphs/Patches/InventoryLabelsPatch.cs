// ------------------------------------------------------------------------------------------------
// InventoryLabelsPatch.cs: postfix on InventoryGui.UpdateInventory for inventory slot labels (F3).
// ------------------------------------------------------------------------------------------------

using System;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Patches
{
    /// <summary>
    /// F3: Valheim labels the inventory's top row "1".."8" when the grid is built, and AzuEPI labels
    /// its quick-slot cells in its own postfix on this method. Low priority so this runs after
    /// AzuEPI's postfix and can find its cells by their text.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateInventory))]
    internal static class InventoryLabelsPatch
    {
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfix does nothing while it's null.
        /// </summary>
        internal static InventoryLabelWriter Writer;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(InventoryLabelsPatch)} failed, leaving inventory labels unchanged");

        /// <summary>
        /// After UpdateInventory (every frame while the inventory is open): writes labels only if the
        /// grid was rebuilt or glyphs toggled.
        /// </summary>
        /// <param name="__instance">The inventory screen.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(InventoryGui __instance)
        {
            InventoryLabelWriter writer = Writer;
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Update(__instance);
            }
            catch (Exception ex)
            {
                s_errors.LogOnce(ex);
            }
        }
    }
}
