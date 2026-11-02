using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine.InputSystem;

namespace ControllerGlyphFixer
{
    /// <summary>
    /// In-game integration check. Run "cgf_selftest" in the F5 console; results go to the console
    /// and to LogOutput.log with a [cgf-selftest] prefix.
    /// </summary>
    internal class SelfTestCommand : ConsoleCommand
    {
        private const string Prefix = "[cgf-selftest] ";

        public override string Name => "cgf_selftest";

        public override string Help => "ControllerGlyphFixer: check patches, gamepad detection and key prompts";

        public override void Run(string[] args)
        {
            var patchInfo = Harmony.GetPatchInfo(AccessTools.Method(typeof(ZInput), nameof(ZInput.GetBoundKeyString)));
            Check("GetBoundKeyString postfix applied",
                patchInfo != null && patchInfo.Postfixes.Any(p => p.owner == ControllerGlyphFixer.PluginGUID));

            Gamepad gamepad = Gamepad.current;
            Check("Gamepad device present", gamepad != null,
                gamepad != null ? $"{gamepad.displayName} ({gamepad.layout})" : "Gamepad.current is null");

            Check("Key mapping loaded", Glyphs.KeyMap.Count > 0, $"{Glyphs.KeyMap.Count} keys");

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
            Check("$KEY_Hotbar1 shows the mapped buttons", expectedHotbar1 != null && hotbar1 == expectedHotbar1,
                $"got {hotbar1}, expected {expectedHotbar1 ?? "(key 1 not mapped)"}");
        }

        private static void Check(string name, bool passed, string detail = null)
        {
            Report((passed ? "PASS " : "FAIL ") + name + (detail != null ? ": " + detail : ""));
        }

        private static void Info(string message)
        {
            Report("INFO " + message);
        }

        private static void Report(string line)
        {
            Jotunn.Logger.LogInfo(Prefix + line);
            Console.instance?.Print(Prefix + line);
        }
    }
}
