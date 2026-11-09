// ------------------------------------------------------------------------------------------------
// HoverTextPatch.cs: postfix on Hud.UpdateCrosshair to translate key names in hover text (F4).
// ------------------------------------------------------------------------------------------------

using System;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Patches
{
    /// <summary>
    /// F4: Hud.UpdateCrosshair is the one place all hover text (vanilla and every mod's additions)
    /// is assigned to m_hoverName, every frame. This postfix hands it to <see cref="HoverTextWriter"/>.
    /// </summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
    internal static class HoverTextPatch
    {
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfix does nothing while it's null.
        /// </summary>
        internal static HoverTextWriter Writer;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(HoverTextPatch)} failed, leaving hover text unchanged");

        /// <summary>
        /// After UpdateCrosshair (every frame): rewrites key hints in the hover text.
        /// </summary>
        /// <param name="__instance">The HUD.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(Hud __instance)
        {
            HoverTextWriter writer = Writer;
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Update(__instance.m_hoverName);
            }
            catch (Exception ex)
            {
                s_errors.LogOnce(ex);
            }
        }
    }
}
