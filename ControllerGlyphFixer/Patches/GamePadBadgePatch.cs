// ------------------------------------------------------------------------------------------------
// GamePadBadgePatch.cs: postfix on UIGamePad.Update to keep button badges after keyboard input (F6).
// ------------------------------------------------------------------------------------------------

using System;
using ControllerGlyphFixer.Core;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Patches
{
    /// <summary>
    /// F6: Valheim's UIGamePad shows a button badge (Y, LSt, RT... on inventory and crafting buttons)
    /// only in gamepad mode, so a trackpad cursor move or keyboard-mapped button hid them all. This
    /// re-decides the badge with <see cref="BadgeVisibility.Show"/>; it only toggles the badge and never
    /// changes Valheim's input mode or which button presses work.
    /// </summary>
    [HarmonyPatch(typeof(UIGamePad), nameof(UIGamePad.Update))]
    internal static class GamePadBadgePatch
    {
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfix does nothing while it's null.
        /// </summary>
        internal static GlyphService Glyphs;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(GamePadBadgePatch)} failed, leaving badges as Valheim set them");

        /// <summary>
        /// Shows the badge in keyboard/mouse mode while controller glyphs are on. In gamepad mode
        /// vanilla's choice is already ours, so it returns early (runs every frame per badge).
        /// </summary>
        /// <param name="__instance">The badge's component.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(UIGamePad __instance)
        {
            GlyphService glyphs = Glyphs;
            if (glyphs == null || !__instance.m_hint || ZInput.IsGamepadActive() || !glyphs.ShowKeyPrompts)
            {
                return;
            }

            try
            {
                UIGroupHandler alternativeGroup = __instance.alternativeGroupHandler;
                bool shown = BadgeVisibility.Show(
                    __instance.IsInteractive(),
                    gamepadActive: false,
                    ZInput.IsMouseActive(),
                    __instance.m_availableForConsoleMouse,
                    alternativeGroupActive: alternativeGroup && alternativeGroup.IsActive,
                    showGlyphs: true);

                if (__instance.m_hint.activeSelf != shown)
                {
                    __instance.m_hint.SetActive(shown);
                }
            }
            catch (Exception ex)
            {
                s_errors.LogOnce(ex);
            }
        }
    }
}
