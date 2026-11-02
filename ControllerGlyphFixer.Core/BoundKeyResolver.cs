using System;

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Decides what ZInput.GetBoundKeyString should return for a binding (features F1 and F2).
    /// </summary>
    public static class BoundKeyResolver
    {
        private const string ControllerPrefix = "Joy";

        /// <param name="name">Binding name Valheim asked for (e.g. "Use", "JoyUse").</param>
        /// <param name="originalResult">What Valheim returned for it.</param>
        /// <param name="showGlyphs">Result of <see cref="GlyphMode.Decide"/>.</param>
        /// <param name="lookupJoy">Returns the display string of a controller binding
        /// ("Joy" + name), or empty if there is none.</param>
        /// <param name="translateKey">Returns the mapped display string for a keyboard key,
        /// or null if the key isn't mapped.</param>
        public static string Resolve(
            string name,
            string originalResult,
            bool showGlyphs,
            Func<string, string> lookupJoy,
            Func<string, string> translateKey)
        {
            if (!showGlyphs || string.IsNullOrEmpty(name) || name.StartsWith(ControllerPrefix, StringComparison.Ordinal))
            {
                return originalResult;
            }

            string controller = lookupJoy(ControllerPrefix + name);
            if (!string.IsNullOrEmpty(controller))
            {
                return controller;
            }

            return translateKey(originalResult) ?? originalResult;
        }
    }
}
