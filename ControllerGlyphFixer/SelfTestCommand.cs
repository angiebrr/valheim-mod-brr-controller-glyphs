// ------------------------------------------------------------------------------------------------
// SelfTestCommand.cs: "cgf_selftest" console command, the in-game integration check.
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
using HarmonyLib;
using Jotunn.Entities;
using TMPro;
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

        /// <summary>The inventory label writer; null if the reverse patch didn't apply.</summary>
        private readonly InventoryLabelWriter m_inventoryLabels;

        /// <summary>AzuEPI's quick-slot shortcuts.</summary>
        private readonly QuickSlotKeys m_quickSlots;

        /// <summary>Draws the group prefixes; null if the reverse patch didn't apply.</summary>
        private readonly PrefixLabels m_prefixLabels;

        /// <summary>The hover text writer (its missing keys and a sample rewrite).</summary>
        private readonly HoverTextWriter m_hoverText;

        /// <summary>The mod's glyph sprite asset (F5); null if it couldn't be built.</summary>
        private readonly ModSpriteAsset m_modSprites;

        /// <summary>The mapping file handler (F9).</summary>
        private readonly MappingFile m_mappingFile;

        /// <summary>Other mods' keybinding settings, read at the first main menu (F9).</summary>
        private readonly ModShortcuts m_modShortcuts;

        /// <summary>Names the mods patching the hovered object's hover text (F9).</summary>
        private readonly HoverTextMods m_hoverTextMods;

        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="glyphs">The service the patches use.</param>
        /// <param name="labels">The slot label writer, or null if hotbar labels couldn't be set up.</param>
        /// <param name="inventoryLabels">The inventory label writer, or null if it couldn't be set up.</param>
        /// <param name="quickSlots">AzuEPI's quick-slot shortcuts.</param>
        /// <param name="prefixLabels">Draws the group prefixes, or null if it couldn't be set up.</param>
        /// <param name="hoverText">The hover text writer.</param>
        /// <param name="modSprites">The mod's glyph sprite asset, or null if it couldn't be built.</param>
        /// <param name="mappingFile">The mapping file handler (status and watcher).</param>
        /// <param name="modShortcuts">Other mods' keybinding settings.</param>
        /// <param name="hoverTextMods">Names the mods patching the hovered object's hover text.</param>
        internal SelfTestCommand(GlyphService glyphs, HotbarLabelWriter labels, InventoryLabelWriter inventoryLabels,
            QuickSlotKeys quickSlots, PrefixLabels prefixLabels, HoverTextWriter hoverText, ModSpriteAsset modSprites,
            MappingFile mappingFile, ModShortcuts modShortcuts, HoverTextMods hoverTextMods)
        {
            m_mappingFile = mappingFile ?? throw new ArgumentNullException(nameof(mappingFile));
            m_modShortcuts = modShortcuts ?? throw new ArgumentNullException(nameof(modShortcuts));
            m_hoverTextMods = hoverTextMods ?? throw new ArgumentNullException(nameof(hoverTextMods));
            m_modSprites = modSprites;
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_labels = labels;
            m_inventoryLabels = inventoryLabels;
            m_quickSlots = quickSlots ?? throw new ArgumentNullException(nameof(quickSlots));
            m_prefixLabels = prefixLabels;
            m_hoverText = hoverText ?? throw new ArgumentNullException(nameof(hoverText));
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
            Check("UIGamePad.Update badge postfix applied",
                HasOurPostfix(AccessTools.Method(typeof(UIGamePad), nameof(UIGamePad.Update))));
            Check("HotkeyBar label postfixes applied (UpdateIcons, ToggleBindingHint, OnEnable)",
                HasOurPostfix(AccessTools.Method(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))) &&
                HasOurPostfix(AccessTools.Method(typeof(HotkeyBar), nameof(HotkeyBar.ToggleBindingHint))) &&
                HasOurPostfix(AccessTools.Method(typeof(HotkeyBar), nameof(HotkeyBar.OnEnable))));
            Check("InventoryGui.UpdateInventory label postfix applied",
                HasOurPostfix(AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.UpdateInventory))));
            Check("Hud.UpdateCrosshair hover text postfix applied",
                HasOurPostfix(AccessTools.Method(typeof(Hud), nameof(Hud.UpdateCrosshair))));

            Gamepad gamepad = Gamepad.current;
            Check("Gamepad device present", gamepad != null,
                gamepad != null ? $"{gamepad.displayName} ({gamepad.layout})" : "Gamepad.current is null");

            Info($"Key mapping has {m_glyphs.MappedKeyCount} keys");
            CheckMappingFile();

            CheckModGlyphs();
            CheckSlotLabels();
            CheckInventoryLabels();
            CheckHoverText();

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
        /// F9 checks: the mapping file loaded, saves are watched, and Localization's cache (emptied
        /// on reload) can be reached.
        /// </summary>
        private void CheckMappingFile()
        {
            string status = m_mappingFile.Status;
            Check("Mapping file loaded", !status.StartsWith("Error", StringComparison.Ordinal), status);
            Check("Mapping file watched for saves", m_mappingFile.IsWatching,
                m_mappingFile.IsWatching ? null : "use cgf_reloadmapping after editing; see the log");
            Info(m_modShortcuts.HasRead
                ? $"Read {m_modShortcuts.Items.Count} keybinding settings from other mods"
                : "Other mods' keybinding settings not read yet (they're read when the main menu first opens)");
            IReadOnlyList<string> hoverMods = m_hoverTextMods.ForHoveredObject();
            Info(hoverMods.Count > 0
                ? $"Mods patching the hovered object's hover text: {string.Join(", ", hoverMods)}"
                : "No hovered object, or no mod patches its hover text (look at a chest and run this again)");
            Info($"Keys missing from mapping (hover hints and mod keybindings): {m_mappingFile.MissingKeysSummary}");

            try
            {
                Localization.instance.m_cache.EvictAll();
                Check("Localization cache can be emptied (for reloads)", true);
            }
            catch (Exception ex)
            {
                Check("Localization cache can be emptied (for reloads)", false, ex.Message);
            }
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
            Check("Prefix backing image loaded", m_prefixLabels != null && m_prefixLabels.BackingSpriteLoaded,
                "if this fails, backings draw as plain squares");

            if (m_quickSlots.IsInstalled)
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

            var bars = UnityEngine.Object.FindObjectsByType<HotkeyBar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(bar => m_labels.Handles(bar.name))
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

                BarLabels labels = m_labels.LabelsFor(bar.name);
                string expected = m_labels.SlotText(labels, 0);
                string actual = BindingLabels.Text(bar.m_elements[0].m_go);
                if (expected == null)
                {
                    Info($"{bar.name} slot 1's key isn't in the mapping; label is \"{actual}\"");
                    continue;
                }

                Check($"{bar.name} slot 1 shows its own button", actual == expected,
                    $"got \"{actual}\", expected \"{expected}\"");

                string expectedPrefixes = string.Join(", ", m_labels.PrefixTexts(labels, bar.m_elements.Count));
                string actualPrefixes = string.Join(", ", m_prefixLabels.Texts(bar.transform));
                Check($"{bar.name} shows a prefix per group", actualPrefixes == expectedPrefixes,
                    $"got [{actualPrefixes}], expected [{expectedPrefixes}]");
            }

            Info($"Top-left messages moved down {m_labels.MessageShift:0.#} units for prefixes under the quick-slot bar");
        }

        /// <summary>
        /// Inventory checks: the top row's slot 1 and quick slot 1's cell show their own button, and
        /// each shows one prefix per group. Labels only update while the inventory is open, so this
        /// needs it opened once since glyphs last turned on.
        /// </summary>
        private void CheckInventoryLabels()
        {
            InventoryGrid grid = InventoryGui.instance ? InventoryGui.instance.m_playerGrid : null;
            if (m_inventoryLabels == null || m_labels == null || !m_glyphs.ShowHotbarLabels)
            {
                Info("Skipping inventory label checks (not set up or hotbar labels off)");
                return;
            }

            if (!grid || grid.m_elements.Count == 0)
            {
                Info("Inventory not built yet; open the inventory once to check its labels");
                return;
            }

            InventoryElement topSlot = grid.m_elements[0];
            CheckCell("Inventory top-row slot 1", topSlot, m_inventoryLabels.TopRowLabel(0));
            CheckPrefixes("Inventory top row", topSlot.transform.parent, InventoryLabelWriter.TopRowContainer,
                m_labels.PrefixTexts(m_labels.HotbarLabels(), Math.Min(grid.m_width, grid.m_elements.Count)));

            if (!m_quickSlots.IsInstalled)
            {
                return;
            }

            InventoryElement quickSlot = m_inventoryLabels.QuickSlotCell(0);
            if (!quickSlot)
            {
                Check("Inventory quick slot 1 cell found", false, "no cell showed AzuEPI's name for it");
                return;
            }

            CheckCell("Inventory quick slot 1", quickSlot, m_inventoryLabels.QuickSlotLabel(0));
            CheckPrefixes("Inventory quick slots", quickSlot.transform.parent, InventoryLabelWriter.QuickSlotContainer,
                m_labels.PrefixTexts(m_labels.QuickSlotLabels(), int.MaxValue));
        }

        /// <summary>
        /// Checks that an inventory cell shows the expected label.
        /// </summary>
        /// <param name="what">The cell, for the report.</param>
        /// <param name="cell">The cell.</param>
        /// <param name="expected">Expected label; null if the slot's key isn't mapped.</param>
        private void CheckCell(string what, InventoryElement cell, string expected)
        {
            string actual = BindingLabels.Text(cell.gameObject);
            if (expected == null)
            {
                Info($"{what}'s key isn't in the mapping; label is \"{actual}\"");
                return;
            }

            Check($"{what} shows its own button", actual == expected, $"got \"{actual}\", expected \"{expected}\"");
        }

        /// <summary>
        /// Checks that a set of slots shows one prefix per group.
        /// </summary>
        /// <param name="what">The slots, for the report.</param>
        /// <param name="parent">The slots' parent.</param>
        /// <param name="containerName">The prefixes' container name.</param>
        /// <param name="expected">Expected prefix texts, in order.</param>
        private void CheckPrefixes(string what, Transform parent, string containerName, IReadOnlyList<string> expected)
        {
            string expectedText = string.Join(", ", expected);
            string actualText = string.Join(", ", m_prefixLabels.Texts(parent, containerName));
            Check($"{what} show a prefix per group", actualText == expectedText, $"got [{actualText}], expected [{expectedText}]");
        }

        /// <summary>
        /// F4 checks: a sample hint is rewritten the way hover text will be, and the keys hover
        /// hints used this session that the mapping doesn't have are listed.
        /// </summary>
        private void CheckHoverText()
        {
            if (!m_glyphs.ShowHoverText)
            {
                Info("Skipping hover text checks (glyphs or a Hint Glyphs setting off)");
                return;
            }

            const string glyph = "<sprite=\"xbox\" name=\"button_a\">";
            string sample = $"[<b>{glyph}</b>] Open [Zebra]";
            string expected = $"<b>{glyph}</b> Open [Zebra]";
            string actual = m_hoverText.Rewrite(sample);
            Check("Hover hint rewriter drops brackets around glyphs and leaves other text", actual == expected,
                $"got \"{actual}\"");
        }

        /// <summary>
        /// F5 checks: the glyph sprite asset was built, survived Valheim's asset sweeps, and renders
        /// as a sprite; saves our atlas and Valheim's xbox sheet next to LogOutput.log to compare.
        /// </summary>
        private void CheckModGlyphs()
        {
            Check("Mod glyph sprite asset built", m_modSprites != null);
            if (m_modSprites == null)
            {
                return;
            }

            Check("Mod glyph atlas, material and asset still loaded", m_modSprites.IsLoaded);
            Info("Mod glyph size and baseline from " + m_modSprites.MetricsSource);
            if (!m_modSprites.IsLoaded)
            {
                return;
            }

            CheckGlyphRenders();
            Info($"ButtonGlyphs = {m_glyphs.ButtonGlyphs}; R4 + D-pad Up shows as {m_glyphs.FormatButtons(new[] { "R4", "DPadUp" })}");

            SaveTexture(m_modSprites.Atlas, "cgf-glyphs-ours.png");
            TMP_SpriteAsset xbox = ModSpriteAsset.FindValheimXbox();
            if (xbox && xbox.spriteSheet)
            {
                SaveTexture(xbox.spriteSheet, "cgf-glyphs-xbox.png");
            }
            else
            {
                Info("Valheim's xbox sprite asset isn't loaded; its sheet wasn't saved");
            }
        }

        /// <summary>
        /// Resolves our R4 tag the way TMP_Text does for &lt;sprite="cgf_deck" name="r4"&gt;: the asset
        /// by its name's hash in MaterialReferenceManager, then the sprite by name.
        /// </summary>
        // A TextMeshProUGUI made here has no font or canvas (NullReferenceException in game 2026-11-08),
        // so this checks the lookups instead of laying out text
        private void CheckGlyphRenders()
        {
            bool registered = MaterialReferenceManager.TryGetSpriteAsset(
                TMP_TextUtilities.GetHashCode(ModGlyphs.AssetName), out TMP_SpriteAsset asset) && asset == m_modSprites.Asset;
            ModGlyphs.TryGetSprite("r4", out string sprite);
            int index = registered ? asset.GetSpriteIndexFromName(sprite) : -1;
            Check("Mod glyph tag resolves to our sprite asset and its R4 sprite", registered && index >= 0,
                registered ? $"sprite index {index}" : "cgf_deck isn't registered with TMP");
        }

        /// <summary>
        /// Saves a texture as a PNG in the BepInEx folder (next to LogOutput.log) and reports the path.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="fileName">File name.</param>
        private void SaveTexture(Texture texture, string fileName)
        {
            string path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, fileName);
            try
            {
                System.IO.File.WriteAllBytes(path, PngFiles.Encode(texture));
                Info($"Saved {texture.width} x {texture.height} sheet to {path}");
            }
            catch (Exception ex)
            {
                Info($"Couldn't save {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Whether this mod has a postfix on a method.
        /// </summary>
        /// <param name="method">The patched method.</param>
        /// <returns>True if one of the method's postfixes belongs to this mod.</returns>
        private bool HasOurPostfix(System.Reflection.MethodBase method)
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
        private void Check(string name, bool passed, string detail = null)
        {
            Report((passed ? "PASS " : "FAIL ") + name + (detail != null ? ": " + detail : ""));
        }

        /// <summary>
        /// Reports an INFO line (not a pass or fail).
        /// </summary>
        /// <param name="message">The information to report.</param>
        private void Info(string message)
        {
            Report("INFO " + message);
        }

        /// <summary>
        /// Writes a result line to LogOutput.log and the in-game console.
        /// </summary>
        /// <param name="line">The line without the prefix.</param>
        private void Report(string line)
        {
            Jotunn.Logger.LogInfo(Prefix + line);
            if (Console.instance)
            {
                Console.instance.Print(Prefix + line);
            }
        }
    }
}
