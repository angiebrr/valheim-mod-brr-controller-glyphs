// ------------------------------------------------------------------------------------------------
// BoundKeyStringPatch.cs: postfix on ZInput.GetBoundKeyString for $KEY_ prompts (F1 + F2).
// ------------------------------------------------------------------------------------------------

using System;
using ControllerGlyphFixer.Core;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Patches
{
    /// <summary>
    /// F1 + F2: every $KEY_ token resolves through ZInput.GetBoundKeyString, so one postfix
    /// changes the key text in all prompts. Decisions are made by <see cref="BoundKeyResolver"/>.
    /// </summary>
    // TODO: Localization caches localized strings (LRU of 100). Valheim clears it when the input
    // layout changes; check in game that connecting/disconnecting a gamepad refreshes prompts.
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetBoundKeyString))]
    internal static class BoundKeyStringPatch
    {
        /// <summary>Set after the first error so a broken patch doesn't flood the log.</summary>
        private static bool s_loggedError;

        /// <summary>
        /// Replaces the key text Valheim returned with a controller glyph or mapped button.
        /// Runs at low priority so other mods' postfixes see Valheim's original text.
        /// </summary>
        /// <param name="__instance">The ZInput instance, used to look up Joy bindings.</param>
        /// <param name="name">Binding name Valheim was asked for (e.g. "Use").</param>
        /// <param name="__result">Valheim's result; replaced in place.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(ZInput __instance, string name, ref string __result)
        {
            try
            {
                __result = BoundKeyResolver.Resolve(
                    name,
                    __result,
                    Glyphs.ShowKeyPrompts,
                    joyName => __instance.GetBoundKeyString(joyName, emptyStringOnMissing: true),
                    Glyphs.TranslateKey);
            }
            catch (Exception ex)
            {
                if (!s_loggedError)
                {
                    s_loggedError = true;
                    Jotunn.Logger.LogError($"{nameof(BoundKeyStringPatch)} failed, leaving key text unchanged: {ex}");
                }
            }
        }
    }
}
