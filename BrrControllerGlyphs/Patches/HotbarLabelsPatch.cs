// ------------------------------------------------------------------------------------------------
// HotbarLabelsPatch.cs: postfixes on HotkeyBar that hand slot labels to HotbarLabelWriter (F3).
// ------------------------------------------------------------------------------------------------

using System;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Patches
{
    /// <summary>
    /// F3: Valheim writes slot labels when slots are created (UpdateIcons) and on every
    /// keyboard/gamepad switch (ToggleBindingHint: "1".."8" or blank). These postfixes rewrite them
    /// afterwards with <see cref="HotbarLabelWriter"/>. Covers AzuEPI's quick-slot bar too, since it
    /// is also a HotkeyBar (Harmony runs postfixes even when AzuEPI's prefix skips UpdateIcons).
    /// </summary>
    [HarmonyPatch]
    internal static class HotbarLabelsPatch
    {
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfixes do nothing while it's null.
        /// </summary>
        internal static HotbarLabelWriter Writer;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(HotbarLabelsPatch)} failed, leaving slot labels unchanged");

        /// <summary>
        /// After UpdateIcons (every frame): writes labels only if slots were rebuilt or glyphs toggled
        /// (see <see cref="Core.SlotLabelRefresh"/>).
        /// </summary>
        /// <param name="__instance">The hotbar.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))]
        [HarmonyPriority(Priority.Low)]
        private static void AfterUpdateIcons(HotkeyBar __instance)
        {
            Write(__instance, force: false);
        }

        /// <summary>
        /// After ToggleBindingHint (Valheim just reset the labels on a keyboard/gamepad switch).
        /// </summary>
        /// <param name="__instance">The hotbar.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.ToggleBindingHint))]
        [HarmonyPriority(Priority.Low)]
        private static void AfterToggleBindingHint(HotkeyBar __instance)
        {
            Write(__instance, force: true);
        }

        /// <summary>
        /// After OnEnable (bar shown again, e.g. after the HUD was hidden).
        /// </summary>
        /// <param name="__instance">The hotbar.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.OnEnable))]
        [HarmonyPriority(Priority.Low)]
        private static void AfterOnEnable(HotkeyBar __instance)
        {
            Write(__instance, force: true);
        }

        /// <summary>
        /// Hands the bar to the writer, logging the first error instead of breaking the HUD.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="force">Write even if nothing changed.</param>
        private static void Write(HotkeyBar bar, bool force)
        {
            HotbarLabelWriter writer = Writer;
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Update(bar, force);
            }
            catch (Exception ex)
            {
                s_errors.LogOnce(ex);
            }
        }
    }
}
