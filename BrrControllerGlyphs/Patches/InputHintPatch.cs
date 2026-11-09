// ------------------------------------------------------------------------------------------------
// InputHintPatch.cs: postfix on UIInputHint.UpdateInputHints to show gamepad hint versions (F6).
// ------------------------------------------------------------------------------------------------

using System;
using BrrControllerGlyphs.Core;
using HarmonyLib;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Patches
{
    /// <summary>
    /// F6: Valheim's UIInputHint shows a gamepad or keyboard version of each hint based on the last
    /// input. After keyboard input it showed the keyboard version (key-cap boxes) holding our
    /// controller glyphs. This re-picks the version with <see cref="HintVisibility.Decide"/>; it only
    /// toggles which version is visible and never changes Valheim's input mode.
    /// </summary>
    [HarmonyPatch(typeof(UIInputHint), nameof(UIInputHint.UpdateInputHints))]
    internal static class InputHintPatch
    {
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfix does nothing while it's null.
        /// </summary>
        internal static GlyphService Glyphs;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(InputHintPatch)} failed, leaving hints as Valheim set them");

        /// <summary>
        /// Shows the gamepad version of the hint while controller glyphs are on. Does nothing when
        /// they're off, so vanilla's choice stands.
        /// </summary>
        /// <param name="__instance">The hint being updated.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(UIInputHint __instance)
        {
            GlyphService glyphs = Glyphs;
            if (glyphs == null || !glyphs.ShowKeyPrompts)
            {
                return;
            }

            try
            {
                HintVisibility visibility = HintVisibility.Decide(
                    IsHintVisible(__instance),
                    ZInput.IsGamepadActive(),
                    ZInput.IsMouseActive(),
                    hasGamepadHint: __instance.m_gamepadHint != null,
                    hasGamepadMouseHint: __instance.m_gamepadMouseHint != null,
                    showGlyphs: true);

                SetActive(__instance, __instance.m_mouseKeyboardHint, visibility.KeyboardMouse);
                SetActive(__instance, __instance.m_gamepadMouseHint, visibility.GamepadMouse);
                SetActive(__instance, __instance.m_gamepadHint, visibility.Gamepad);
            }
            catch (Exception ex)
            {
                s_errors.LogOnce(ex);
            }
        }

        /// <summary>
        /// Whether the hint should show at all: its button is interactable and its UI group active.
        /// </summary>
        /// <param name="hint">The hint being updated.</param>
        /// <returns>Vanilla's visibility condition for the hint.</returns>
        // FIXME: copied from vanilla UIInputHint.UpdateInputHints; if Valheim changes it, update here.
        private static bool IsHintVisible(UIInputHint hint)
        {
            return ((hint.m_button == null || hint.m_button.IsInteractable()) &&
                    (hint.m_group == null || hint.m_group.IsActive)) ||
                   (hint.alternativeGroupHandler != null && hint.alternativeGroupHandler.IsActive);
        }

        /// <summary>
        /// Shows or hides one version through vanilla's own helper (which also refreshes the text and
        /// layout), only when its state actually changes.
        /// </summary>
        /// <param name="hint">The hint being updated.</param>
        /// <param name="version">One of the hint's versions; may be null.</param>
        /// <param name="active">Whether it should be visible.</param>
        private static void SetActive(UIInputHint hint, GameObject version, bool active)
        {
            if (version != null && version.activeSelf != active)
            {
                hint.UpdateVisiblityAndLayout(version, active);
            }
        }
    }
}
