// ------------------------------------------------------------------------------------------------
// OriginalBoundKeyString.cs: reverse patch to call ZInput.GetBoundKeyString without our postfix.
// ------------------------------------------------------------------------------------------------

using System;
using HarmonyLib;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Patches
{
    /// <summary>
    /// Harmony reverse patch: a copy of Valheim's original ZInput.GetBoundKeyString, without our
    /// <see cref="BoundKeyStringPatch"/> postfix, so slot labels can read the raw keyboard key bound
    /// to an action (e.g. "1" for "Hotbar1") and translate it themselves.
    /// </summary>
    [HarmonyPatch]
    internal static class OriginalBoundKeyString
    {
        /// <summary>
        /// Valheim's original GetBoundKeyString. Harmony replaces this body when the class is patched.
        /// </summary>
        /// <param name="instance">The ZInput instance.</param>
        /// <param name="name">Binding name (e.g. "Hotbar1").</param>
        /// <param name="emptyStringOnMissing">Return "" instead of a "MISSING" message.</param>
        /// <returns>The bound key's display string, as vanilla returns it.</returns>
        /// <exception cref="NotImplementedException">The reverse patch wasn't applied.</exception>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetBoundKeyString))]
        internal static string Call(ZInput instance, string name, bool emptyStringOnMissing)
        {
            throw new NotImplementedException("Reverse patch of ZInput.GetBoundKeyString wasn't applied");
        }
    }
}
