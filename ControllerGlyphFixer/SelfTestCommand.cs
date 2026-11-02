// ------------------------------------------------------------------------------------------------
// SelfTestCommand.cs: "cgf_selftest" console command, the in-game integration check.
// ------------------------------------------------------------------------------------------------

using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
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
            var patchInfo = Harmony.GetPatchInfo(AccessTools.Method(typeof(ZInput), nameof(ZInput.GetBoundKeyString)));
            Check("GetBoundKeyString postfix applied",
                patchInfo != null && patchInfo.Postfixes.Any(p => p.owner == ControllerGlyphFixer.PluginGUID));

            Gamepad gamepad = Gamepad.current;
            Check("Gamepad device present", gamepad != null,
                gamepad != null ? $"{gamepad.displayName} ({gamepad.layout})" : "Gamepad.current is null");

            Info($"Key mapping has {Glyphs.KeyMap.Count} keys");

            bool showGlyphs = Glyphs.ShowKeyPrompts;
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

            string expectedHotbar1 = Glyphs.TranslateKey("1");
            if (expectedHotbar1 == null)
            {
                Info($"Key 1 isn't in the mapping; $KEY_Hotbar1 = {hotbar1}");
                return;
            }

            Check("$KEY_Hotbar1 shows the mapped buttons", hotbar1 == expectedHotbar1,
                $"got {hotbar1}, expected {expectedHotbar1}");
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
