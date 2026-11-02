using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine.InputSystem;

namespace ControllerGlyphFixer
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class ControllerGlyphFixer : BaseUnityPlugin
    {
        public const string PluginGUID = "com.angiebrr.controllerglyphfixer";
        public const string PluginName = "ControllerGlyphFixer";
        public const string PluginVersion = "0.0.1";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> KeyPrompts;

        internal static readonly Harmony Harmony = new Harmony(PluginGUID);

        private static string MappingPath => Path.Combine(Paths.ConfigPath, PluginGUID + ".mapping.yaml");

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When off, prompts behave like vanilla.");
            KeyPrompts = Config.Bind("General", "KeyPrompts", true,
                "While a gamepad is connected, show controller glyphs in $KEY_ prompts even after keyboard input, " +
                "and show keyboard-only keys as the Steam Input buttons from the mapping file.");

            Glyphs.KeyMap = LoadKeyMap();

            ApplyPatch(typeof(BoundKeyStringPatch));
            CommandManager.Instance.AddConsoleCommand(new SelfTestCommand());

            LogGamepads();
            Jotunn.Logger.LogInfo("ControllerGlyphFixer has landed");
        }

        private static KeyMap LoadKeyMap()
        {
            try
            {
                if (!File.Exists(MappingPath))
                {
                    File.WriteAllText(MappingPath, KeyMapTemplate.Yaml);
                    Jotunn.Logger.LogInfo($"Wrote an empty key mapping template to {MappingPath}; edit it to add your buttons");
                }

                KeyMap map = KeyMap.Parse(File.ReadAllText(MappingPath));
                Jotunn.Logger.LogInfo($"Loaded {map.Count} key mappings from {MappingPath}");
                return map;
            }
            catch (Exception ex) when (ex is KeyMapException || ex is IOException)
            {
                Jotunn.Logger.LogError($"Couldn't load key mapping {MappingPath}: {ex.Message}. " +
                                       "Controller glyphs still work; keyboard-only keys stay as keys.");
                return KeyMap.Parse("");
            }
        }

        // A missing patch target (e.g. after a Valheim update) disables that feature instead of the whole mod
        private static void ApplyPatch(Type patchClass)
        {
            try
            {
                Harmony.CreateClassProcessor(patchClass).Patch();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"{patchClass.Name} not applied, feature disabled: {ex.Message}");
            }
        }

        private static void LogGamepads()
        {
            var gamepads = InputSystem.devices.OfType<Gamepad>().ToList();
            if (gamepads.Count == 0)
            {
                Jotunn.Logger.LogInfo("No gamepad devices found; controller glyphs stay off until one connects");
                return;
            }

            foreach (Gamepad gamepad in gamepads)
            {
                Jotunn.Logger.LogInfo($"Gamepad: {gamepad.displayName} ({gamepad.layout})");
            }
        }
    }
}
