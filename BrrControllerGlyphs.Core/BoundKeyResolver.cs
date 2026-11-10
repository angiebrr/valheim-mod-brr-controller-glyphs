// ------------------------------------------------------------------------------------------------
// BoundKeyResolver.cs: decides what ZInput.GetBoundKeyString returns for a binding (F1 + F2).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Decides what ZInput.GetBoundKeyString should return for a binding (features F1 and F2):
    /// the controller binding when one exists, else the mapped Steam button, else the original key.
    /// </summary>
    public static class BoundKeyResolver
    {
        /// <summary>Prefix Valheim uses for controller binding names (e.g. "JoyUse").</summary>
        private const string ControllerPrefix = "Joy";

        /// <summary>
        /// Keyboard bindings whose controller twin isn't "Joy" + name (from ZInput's binding list,
        /// checked 2026-11-08; contract-tested). Valheim's radial code checks both to open the menu.
        /// </summary>
        // FIXME: copied from ZInput.Reset's AddButton calls
        public static readonly IReadOnlyDictionary<string, string> ControllerNameExceptions = new Dictionary<string, string>
        {
            { "OpenRadial", "JoyRadial" },
        };

        /// <summary>
        /// The controller binding name for a keyboard binding.
        /// </summary>
        /// <param name="name">Keyboard binding name (e.g. "Use").</param>
        /// <returns>"Joy" + name, or the known exception (OpenRadial: JoyRadial).</returns>
        private static string ControllerName(string name)
        {
            return ControllerNameExceptions.TryGetValue(name, out string controller) ? controller : ControllerPrefix + name;
        }

        /// <summary>
        /// Returns the display string for a binding.
        /// </summary>
        /// <param name="name">Binding name Valheim asked for (e.g. "Use", "JoyUse").</param>
        /// <param name="originalResult">What Valheim returned for it.</param>
        /// <param name="showGlyphs">Result of <see cref="GlyphMode.Decide"/>.</param>
        /// <param name="lookupJoy">Returns the display string of a controller binding
        /// ("Joy" + name), or empty if there is none.</param>
        /// <param name="translateKey">Returns the mapped display string for a keyboard key,
        /// or null if the key isn't mapped.</param>
        /// <returns>The string to show in place of <paramref name="originalResult"/>.</returns>
        public static string Resolve(
            string name,
            string originalResult,
            bool showGlyphs,
            Func<string, string> lookupJoy,
            Func<string, string> translateKey)
        {
            // Joy names are left alone: looking one up re-enters GetBoundKeyString (recursion guard)
            if (!showGlyphs || string.IsNullOrEmpty(name) || name.StartsWith(ControllerPrefix, StringComparison.Ordinal))
            {
                return originalResult;
            }

            string controller = lookupJoy(ControllerName(name));
            if (!string.IsNullOrEmpty(controller))
            {
                return controller;
            }

            return translateKey(originalResult) ?? originalResult;
        }
    }
}
