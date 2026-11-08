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
        /// <summary>
        /// Set by the plugin before patching. Harmony patch methods must be static, so this is the
        /// one static seam; the postfix does nothing while it's null.
        /// </summary>
        internal static GlyphService Glyphs;

        /// <summary>Logs the first error only.</summary>
        private static readonly PatchErrorLog s_errors = new ($"{nameof(BoundKeyStringPatch)} failed, leaving key text unchanged");

        /// <summary>
        /// Replaces the key text Valheim returned with a controller glyph or mapped button.
        /// Runs at low priority so other mods' postfixes see Valheim's original text.
        /// </summary>
        /// <param name="name">Binding name Valheim was asked for (e.g. "Use").</param>
        /// <param name="__result">Valheim's result; replaced in place.</param>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(string name, ref string __result)
        {
            GlyphService glyphs = Glyphs;
            if (glyphs == null)
            {
                return;
            }

            try
            {
                // Cached delegates: this runs whenever $KEY_ text is localized, often every frame
                __result = BoundKeyResolver.Resolve(name, __result, glyphs.ShowKeyPrompts, glyphs.JoyLookup, glyphs.KeyTranslator);
            }
            catch (Exception ex)
            {
                s_errors.LogOnce(ex);
            }
        }
    }
}
