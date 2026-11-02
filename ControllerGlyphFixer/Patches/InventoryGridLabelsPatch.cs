// ------------------------------------------------------------------------------------------------
// InventoryGridLabelsPatch.cs: postfix on InventoryGrid.UpdateGui for the top-row labels (F3).
// ------------------------------------------------------------------------------------------------

using System;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Patches
{
    /// <summary>
    /// F3: Valheim labels the inventory's top row "1".."8" when the grid is built, in both input
    /// modes. This postfix hands the grid to <see cref="InventoryLabelWriter"/> afterwards.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    internal static class InventoryGridLabelsPatch
    {
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfix does nothing while it's null.
        /// </summary>
        internal static InventoryLabelWriter Writer;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(InventoryGridLabelsPatch)} failed, leaving inventory labels unchanged");

        /// <summary>
        /// After UpdateGui (every frame while the inventory is open): writes labels only if the grid
        /// was rebuilt or glyphs toggled.
        /// </summary>
        /// <param name="__instance">The inventory grid.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(InventoryGrid __instance)
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
