// ------------------------------------------------------------------------------------------------
// ControllerGlyphFixer.cs: BepInEx plugin entry point (config, mapping file, patches, commands).
// ------------------------------------------------------------------------------------------------

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

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Plugin entry point: binds config, loads the key mapping file, applies Harmony patches and
    /// registers console commands.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Client-side only: changes prompt text on this player's screen and sends nothing over the
    // network, so servers and other players never need it. Change this only if the mod ever adds
    // RPCs or synced config.
    // Fully qualified: a "using Jotunn.Utils" would make Paths ambiguous with BepInEx.Paths
    [Jotunn.Utils.NetworkCompatibility(Jotunn.Utils.CompatibilityLevel.NotEnforced, Jotunn.Utils.VersionStrictness.None)]
    internal class ControllerGlyphFixer : BaseUnityPlugin
    {
        /// <summary>BepInEx plugin ID; also names the config and mapping files. Don't change it.</summary>
        public const string PluginGUID = "com.angiebrr.controllerglyphfixer";

        /// <summary>Display name shown by BepInEx and ConfigurationManager.</summary>
        public const string PluginName = "ControllerGlyphFixer";

        /// <summary>Plugin version; also used for the assembly version.</summary>
        public const string PluginVersion = "0.0.1";

        /// <summary>Harmony instance that owns all of this mod's patches.</summary>
        private readonly Harmony m_harmony = new (PluginGUID);

        /// <summary>Path of the YAML key mapping file next to the plugin's .cfg.</summary>
        private static string MappingPath => Path.Combine(Paths.ConfigPath, PluginGUID + ".mapping.yaml");

        /// <summary>
        /// Called by Unity when BepInEx loads the plugin: builds every object with its
        /// dependencies, then hands them to the patches and commands that use them.
        /// </summary>
        // TODO(phase 2): bind HotbarLabels (F3) and HoverText (F4) toggles and apply their patches.
        // TODO: give config entries ConfigurationManagerAttributes (order, sections) for the in-game UI.
        private void Awake()
        {
            ConfigEntry<bool> enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When off, prompts behave like vanilla.");
            ConfigEntry<bool> keyPrompts = Config.Bind("General", "KeyPrompts", true,
                "While a gamepad is connected, show controller glyphs in $KEY_ prompts and hint panels even after " +
                "keyboard input, and show keyboard-only keys as the Steam Input buttons from the mapping file.");

            var glyphs = new GlyphService(LoadKeyMap(), enabled, keyPrompts);

            BoundKeyStringPatch.Glyphs = glyphs;
            ApplyPatch(typeof(BoundKeyStringPatch));
            InputHintPatch.Glyphs = glyphs;
            ApplyPatch(typeof(InputHintPatch));
            CommandManager.Instance.AddConsoleCommand(new SelfTestCommand(glyphs));
            CommandManager.Instance.AddConsoleCommand(new DumpHotbarsCommand());

            LogGamepads();
            InputSystem.onDeviceChange += OnDeviceChange;
            Jotunn.Logger.LogInfo("ControllerGlyphFixer has landed");
        }

        /// <summary>
        /// Called by Unity when the plugin is destroyed: stops listening for device changes.
        /// </summary>
        private void OnDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        /// <summary>
        /// Loads the mapping file, writing the commented template first if it doesn't exist.
        /// </summary>
        /// <returns>The parsed map, or an empty map if the file can't be read or parsed.</returns>
        // TODO: reload the mapping when the file changes (FileSystemWatcher) or via a console
        // command, so edits don't need a game restart. Localization caches text, so clear it too.
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
            catch (Exception ex) when (ex is KeyMapException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Jotunn.Logger.LogError($"Couldn't load key mapping {MappingPath}: {ex.Message}. " +
                                       "Controller glyphs still work; keyboard-only keys stay as keys.");
                return KeyMap.Empty;
            }
        }

        /// <summary>
        /// Applies one patch class; a missing target (e.g. after a Valheim update) disables only
        /// that feature, with a warning, instead of the whole mod.
        /// </summary>
        /// <param name="patchClass">A class with [HarmonyPatch] attributes.</param>
        private void ApplyPatch(Type patchClass)
        {
            try
            {
                m_harmony.CreateClassProcessor(patchClass).Patch();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"{patchClass.Name} not applied, feature disabled: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs gamepads already connected when the plugin loads. Steam Input's virtual controller
        /// usually connects later (around character selection); <see cref="OnDeviceChange"/> logs that.
        /// </summary>
        private static void LogGamepads()
        {
            var gamepads = InputSystem.devices.OfType<Gamepad>().ToList();
            if (gamepads.Count == 0)
            {
                Jotunn.Logger.LogInfo("No gamepad connected yet; controller glyphs turn on when one connects");
                return;
            }

            foreach (Gamepad gamepad in gamepads)
            {
                Jotunn.Logger.LogInfo($"Gamepad: {gamepad.displayName} ({gamepad.layout})");
            }
        }

        /// <summary>
        /// Logs gamepads connecting and disconnecting, so the log shows when glyphs turn on or off.
        /// </summary>
        /// <param name="device">The device that changed.</param>
        /// <param name="change">What happened to it.</param>
        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Gamepad)
            {
                return;
            }

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                Jotunn.Logger.LogInfo($"Gamepad connected: {device.displayName} ({device.layout})");
            }
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                Jotunn.Logger.LogInfo($"Gamepad disconnected: {device.displayName} ({device.layout})");
            }
        }
    }
}
