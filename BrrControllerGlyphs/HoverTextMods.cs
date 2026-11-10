// ------------------------------------------------------------------------------------------------
// HoverTextMods.cs: names the mods that patch the hovered object's hover text (F9).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs
{
    /// <summary>
    /// A best guess at which mod wrote a hover hint: hover text doesn't record who added each line,
    /// so this lists the mods with Harmony patches on the hovered object's GetHoverText. Harmony IDs
    /// are shown as the plugin's name when a loaded plugin has that GUID. Results are cached per
    /// hoverable type (a few dozen at most); only called when a hint shows a new unmapped key.
    /// </summary>
    internal sealed class HoverTextMods
    {
        /// <summary>This plugin's Harmony ID, left out of the results.</summary>
        private readonly string m_ownId;

        /// <summary>Mods found per hoverable type.</summary>
        private readonly Dictionary<Type, IReadOnlyList<string>> m_byType = new ();

        /// <summary>
        /// Creates the lookup.
        /// </summary>
        /// <param name="ownId">This plugin's GUID (its Harmony ID).</param>
        internal HoverTextMods(string ownId)
        {
            m_ownId = ownId ?? throw new ArgumentNullException(nameof(ownId));
        }

        /// <summary>
        /// The mods patching the hover text of what the local player is looking at.
        /// </summary>
        /// <returns>Plugin names, or none when nothing is hovered, no mod patches it, or the lookup fails.</returns>
        internal IReadOnlyList<string> ForHoveredObject()
        {
            try
            {
                Player player = Player.m_localPlayer;
                GameObject hovered = player ? player.GetHoverObject() : null;
                Hoverable hoverable = hovered ? hovered.GetComponentInParent<Hoverable>() : null;
                if (hoverable == null)
                {
                    return Array.Empty<string>();
                }

                Type type = hoverable.GetType();
                if (!m_byType.TryGetValue(type, out IReadOnlyList<string> mods))
                {
                    mods = PatchOwners(AccessTools.Method(type, nameof(Hoverable.GetHoverText)));
                    m_byType[type] = mods;
                }

                return mods;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Couldn't tell which mods wrote a hover hint: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// The mods with patches on a method, as plugin names where known.
        /// </summary>
        /// <param name="method">The hoverable's GetHoverText.</param>
        /// <returns>Names in patch order, each once.</returns>
        private IReadOnlyList<string> PatchOwners(MethodBase method)
        {
            HarmonyLib.Patches patches = method != null ? Harmony.GetPatchInfo(method) : null;
            if (patches == null)
            {
                return Array.Empty<string>();
            }

            return patches.Owners
                .Where(owner => owner != m_ownId)
                .Select(owner => Chainloader.PluginInfos.TryGetValue(owner, out var plugin) ? plugin.Metadata.Name : owner)
                .Distinct()
                .ToArray();
        }
    }
}
