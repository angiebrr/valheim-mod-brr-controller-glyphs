// ------------------------------------------------------------------------------------------------
// SelfTestCommand.cs: "cgf_selftest" console command, the in-game integration check.
// ------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// In-game integration check. Run "cgf_selftest" in the F5 console; results go to the console
    /// and to LogOutput.log with a [cgf-selftest] prefix.
    /// </summary>
    // TODO: add a "-cgf-selftest" launch flag that runs this at the main menu and quits, so a
    // script can run it unattended (see the spec's testing strategy, layer 3).
    internal class SelfTestCommand : ConsoleCommand
    {
        /// <summary>Prefix on every result line, for grepping LogOutput.log.</summary>
        private const string Prefix = "[cgf-selftest] ";

        /// <summary>The service whose decisions are checked.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>The slot label writer; null if its reverse patch didn't apply.</summary>
        private readonly HotbarLabelWriter m_labels;

        /// <summary>AzuEPI's quick-slot shortcuts.</summary>
        private readonly QuickSlotKeys m_quickSlots;

        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="glyphs">The service the patches use.</param>
        /// <param name="labels">The slot label writer, or null if hotbar labels couldn't be set up.</param>
        /// <param name="quickSlots">AzuEPI's quick-slot shortcuts.</param>
        internal SelfTestCommand(GlyphService glyphs, HotbarLabelWriter labels, QuickSlotKeys quickSlots)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_labels = labels;
            m_quickSlots = quickSlots ?? throw new ArgumentNullException(nameof(quickSlots));
        }

        /// <summary>Command name typed in the console.</summary>
        public override string Name => "cgf_selftest";

        /// <summary>Help text shown by the console's help command.</summary>
        public override string Help => "ControllerGlyphFixer: check patches, gamepad detection and key prompts";

        /// <summary>
        /// Runs the checks and reports PASS, FAIL or INFO lines.
        /// </summary>
        /// <param name="args">Command arguments (unused).</param>
        public override void Run(string[] args)
        {
            Check("GetBoundKeyString postfix applied",
                HasOurPostfix(AccessTools.Method(typeof(ZInput), nameof(ZInput.GetBoundKeyString))));
            Check("UIInputHint.UpdateInputHints postfix applied",
                HasOurPostfix(AccessTools.Method(typeof(UIInputHint), nameof(UIInputHint.UpdateInputHints))));
            Check("HotkeyBar label postfixes applied (UpdateIcons, ToggleBindingHint, OnEnable)",
                HasOurPostfix(AccessTools.Method(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))) &&
                HasOurPostfix(AccessTools.Method(typeof(HotkeyBar), nameof(HotkeyBar.ToggleBindingHint))) &&
                HasOurPostfix(AccessTools.Method(typeof(HotkeyBar), nameof(HotkeyBar.OnEnable))));
            Check("InventoryGrid.UpdateGui label postfix applied",
                HasOurPostfix(AccessTools.Method(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))));

            Gamepad gamepad = Gamepad.current;
            Check("Gamepad device present", gamepad != null,
                gamepad != null ? $"{gamepad.displayName} ({gamepad.layout})" : "Gamepad.current is null");

            Info($"Key mapping has {m_glyphs.MappedKeyCount} keys");

            CheckSlotLabels();

            bool showGlyphs = m_glyphs.ShowKeyPrompts;
            Info($"Controller glyphs on: {showGlyphs}; Valheim input mode is gamepad: {ZInput.IsGamepadActive()}; " +
                 $"glyph setting {ZInput.CurrentGlyph}, gamepad type {ZInput.ConnectedGamepadType}");

            string use = Localization.instance.Localize("$KEY_Use");
            string hotbar1 = Localization.instance.Localize("$KEY_Hotbar1");
            if (!showGlyphs)
            {
                Info($"Skipping prompt checks (controller glyphs off). $KEY_Use = {use}, $KEY_Hotbar1 = {hotbar1}");
                return;
            }

            Check("$KEY_Use shows a controller glyph", use.Contains("<sprite"), use);

            string expectedHotbar1 = m_glyphs.TranslateKey("1");
            if (expectedHotbar1 == null)
            {
                Info($"Key 1 isn't in the mapping; $KEY_Hotbar1 = {hotbar1}");
                return;
            }

            Check("$KEY_Hotbar1 shows the mapped buttons", hotbar1 == expectedHotbar1,
                $"got {hotbar1}, expected {expectedHotbar1}");
        }

        /// <summary>
        /// F3 checks: the raw key lookup works, AzuEPI's shortcuts can be read, and slot 1 of each
        /// labelled bar on screen shows the expected label.
        /// </summary>
        private void CheckSlotLabels()
        {
            if (m_labels == null)
            {
                Check("Hotbar labels set up", false, "OriginalBoundKeyString reverse patch not applied; see the log");
                return;
            }

            string rawKey;
            try
            {
                rawKey = OriginalBoundKeyString.Call(ZInput.instance, "Hotbar1", emptyStringOnMissing: true);
            }
            catch (Exception ex)
            {
                rawKey = null;
                Info($"Raw key lookup threw: {ex.Message}");
            }

            Check("Raw key for Hotbar1 readable (reverse patch)", !string.IsNullOrEmpty(rawKey), rawKey);

            if (QuickSlotKeys.IsInstalled)
            {
                bool found = m_quickSlots.TryGet(0, out string mainKey, out var modifiers);
                Check("AzuEPI quick slot 1 shortcut readable", found,
                    found ? string.Join(" + ", modifiers.Append(mainKey)) : $"[{AzuEpiQuickSlots.Section}] not found");
            }
            else
            {
                Info("AzuExtendedPlayerInventory not installed; skipping quick-slot check");
            }

            if (!m_glyphs.ShowHotbarLabels)
            {
                Info("Skipping slot label checks (hotbar labels off)");
                return;
            }

            CheckInventoryTopRow();

            var bars = UnityEngine.Object.FindObjectsByType<HotkeyBar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(bar => HotbarLabelWriter.Handles(bar.name))
                .ToList();
            if (bars.Count == 0)
            {
                Info("No hotbar on screen; load into a world to check slot labels");
            }

            foreach (HotkeyBar bar in bars)
            {
                if (bar.m_elements.Count == 0)
                {
                    Info($"{bar.name} has no slots; bind an item to slot 1 to check its label");
                    continue;
                }

                string expected = m_labels.LabelFor(bar.name, 0);
                string actual = HotbarLabelWriter.BindingText(bar.m_elements[0].m_go)?.text;
                if (expected == null)
                {
                    Info($"{bar.name} slot 1's key isn't in the mapping; label is \"{actual}\"");
                    continue;
                }

                Check($"{bar.name} slot 1 shows the mapped buttons", actual == expected,
                    $"got \"{actual}\", expected \"{expected}\"");
            }
        }

        /// <summary>
        /// Checks the inventory's first top-row label. Labels only update while the inventory is
        /// open, so this needs the inventory opened once since glyphs last turned on.
        /// </summary>
        private void CheckInventoryTopRow()
        {
            InventoryGrid grid = InventoryGui.instance ? InventoryGui.instance.m_playerGrid : null;
            if (!grid || grid.m_elements.Count == 0)
            {
                Info("Inventory grid not built yet; open the inventory once to check its top-row labels");
                return;
            }

            string expected = m_labels.HotbarSlotLabel(0);
            string actual = HotbarLabelWriter.BindingText(grid.m_elements[0].gameObject)?.text;
            if (expected == null)
            {
                Info($"Key 1 isn't in the mapping; inventory slot 1 label is \"{actual}\"");
                return;
            }

            Check("Inventory top-row slot 1 shows the mapped buttons", actual == expected,
                $"got \"{actual}\", expected \"{expected}\" (open the inventory once if glyphs just turned on)");
        }

        /// <summary>
        /// Whether this mod has a postfix on a method.
        /// </summary>
        /// <param name="method">The patched method.</param>
        /// <returns>True if one of the method's postfixes belongs to this mod.</returns>
        private static bool HasOurPostfix(System.Reflection.MethodBase method)
        {
            var patchInfo = Harmony.GetPatchInfo(method);
            return patchInfo != null && patchInfo.Postfixes.Any(p => p.owner == ControllerGlyphFixer.PluginGUID);
        }

        /// <summary>
        /// Reports a PASS or FAIL line.
        /// </summary>
        /// <param name="name">What was checked.</param>
        /// <param name="passed">Whether the check passed.</param>
        /// <param name="detail">Optional detail, such as the actual value.</param>
        private static void Check(string name, bool passed, string detail = null)
        {
            Report((passed ? "PASS " : "FAIL ") + name + (detail != null ? ": " + detail : ""));
        }

        /// <summary>
        /// Reports an INFO line (not a pass or fail).
        /// </summary>
        /// <param name="message">The information to report.</param>
        private static void Info(string message)
        {
            Report("INFO " + message);
        }

        /// <summary>
        /// Writes a result line to LogOutput.log and the in-game console.
        /// </summary>
        /// <param name="line">The line without the prefix.</param>
        private static void Report(string line)
        {
            Jotunn.Logger.LogInfo(Prefix + line);
            Console.instance?.Print(Prefix + line);
        }
    }
}
