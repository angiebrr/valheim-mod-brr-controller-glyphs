using System;
using ControllerGlyphFixer.Core;
using HarmonyLib;

namespace ControllerGlyphFixer.Patches
{
    /// <summary>
    /// F1 + F2: every $KEY_ token resolves through ZInput.GetBoundKeyString.
    /// </summary>
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetBoundKeyString))]
    internal static class BoundKeyStringPatch
    {
        private static bool s_loggedError;

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
