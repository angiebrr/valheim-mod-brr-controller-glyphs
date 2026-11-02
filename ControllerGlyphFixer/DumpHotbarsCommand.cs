// ------------------------------------------------------------------------------------------------
// DumpHotbarsCommand.cs: "cgf_dumphotbars" dev command that logs every hotbar and who patches it.
// ------------------------------------------------------------------------------------------------

using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
using TMPro;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Development aid for F3. Run "cgf_dumphotbars" in the F5 console; it logs every HotkeyBar on
    /// screen (Valheim's hotbar and copies such as AzuEPI's quick-slot bar) with each slot's label
    /// text, plus every Harmony patch on HotkeyBar methods and which mod owns it.
    /// </summary>
    // TODO(phase 2): remove or fold into cgf_selftest once F3's patch targets are settled.
    internal class DumpHotbarsCommand : ConsoleCommand
    {
        /// <summary>Prefix on every output line, for grepping LogOutput.log.</summary>
        private const string Prefix = "[cgf-dumphotbars] ";

        /// <summary>Command name typed in the console.</summary>
        public override string Name => "cgf_dumphotbars";

        /// <summary>Help text shown by the console's help command.</summary>
        public override string Help => "ControllerGlyphFixer: log every hotbar, its slot labels, and mods patching HotkeyBar";

        /// <summary>
        /// Logs the patches on HotkeyBar, then each hotbar and its slot labels.
        /// </summary>
        /// <param name="args">Command arguments (unused).</param>
        public override void Run(string[] args)
        {
            Report($"Input mode is gamepad: {ZInput.IsGamepadActive()}");

            foreach (var method in Harmony.GetAllPatchedMethods().Where(m => m.DeclaringType == typeof(HotkeyBar)))
            {
                // Qualified: our own ControllerGlyphFixer.Patches namespace shadows HarmonyLib.Patches
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                Report($"Patched HotkeyBar.{method.Name}: " +
                       $"prefixes [{Owners(patches.Prefixes)}], postfixes [{Owners(patches.Postfixes)}], " +
                       $"transpilers [{Owners(patches.Transpilers)}]");
            }

            foreach (HotkeyBar bar in Object.FindObjectsByType<HotkeyBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Report($"HotkeyBar '{bar.name}' (active: {bar.isActiveAndEnabled}), {bar.m_elements.Count} slots, path {PathOf(bar.transform)}");
                for (int i = 0; i < bar.m_elements.Count; i++)
                {
                    GameObject slot = bar.m_elements[i].m_go;
                    Transform binding = slot ? slot.transform.Find("binding") : null;
                    TMP_Text text = binding ? binding.GetComponent<TMP_Text>() : null;
                    Report(text == null
                        ? $"  slot {i}: no binding label"
                        : $"  slot {i}: \"{text.text}\" (shown: {text.isActiveAndEnabled})");
                }
            }
        }

        /// <summary>
        /// Lists the owners (Harmony IDs) of a set of patches.
        /// </summary>
        /// <param name="patches">Prefixes, postfixes or transpilers of one method.</param>
        /// <returns>Comma-separated "owner (priority)" entries.</returns>
        private static string Owners(System.Collections.Generic.IEnumerable<Patch> patches)
        {
            return string.Join(", ", patches.Select(p => $"{p.owner} ({p.priority})"));
        }

        /// <summary>
        /// Builds the scene path of a transform, e.g. "_GameMain/LoadingGUI/.../HotKeyBar".
        /// </summary>
        /// <param name="transform">The transform.</param>
        /// <returns>Names from the root down, separated by '/'.</returns>
        private static string PathOf(Transform transform)
        {
            return transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
        }

        /// <summary>
        /// Writes a line to LogOutput.log and the in-game console.
        /// </summary>
        /// <param name="line">The line without the prefix.</param>
        private static void Report(string line)
        {
            Jotunn.Logger.LogInfo(Prefix + line);
            Console.instance?.Print(Prefix + line);
        }
    }
}
