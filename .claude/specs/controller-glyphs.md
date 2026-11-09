# Spec: Controller glyphs for a keyboard-emitting Steam Input layout

Status: draft, approved direction, not implemented
Last updated: 2026-11-08
Mod: BrrControllerGlyphs (`angiebrr.BrrControllerGlyphs`; was ControllerGlyphFixer, `com.angiebrr.controllerglyphfixer` then `angiebrr.ControllerGlyphFixer`, renamed 2026-11-08), BepInEx + Jotunn, Valheim 1.0

## Problem

My Steam Controller layout sends **real gamepad buttons for core actions** (A/B/X/Y, bumpers, triggers, sticks, D-pad) and **keyboard keys for extras** (hotbar, mod hotkeys). Every keyboard key flips Valheim into keyboard/mouse mode, so:

1. Prompts switch from controller glyphs to keyboard keys after any extra-button press.
2. Hotbar/quick-slot labels and mod hover hints show keyboard keys (`1`, `Z`, `LeftShift`) instead of the Steam Controller button I actually press.

## Goal

While a gamepad is connected, every button prompt shows what I press on the controller: Valheim's own glyph for real gamepad buttons, and my Steam binding (e.g. `R4 + D-pad Up`) for keyboard-emitted keys. Game input behaviour (mouse cursor, run toggle, UI navigation) stays vanilla.

## Non-goals

- Changing which device the game thinks is active (`ZInput.m_inputSource`). Rejected: gamepad mode also hides the mouse cursor (`ZCursor`), changes run toggle (`ShouldDefaultToggleRun`), drops some hover lines, and other mods read `IsGamepadActive()` for behaviour.
- Rebinding anything. Steam Input stays the source of truth for bindings.
- Supporting other people's layouts in v1 (mapping is personal; data-driven so it could be later).

## Design rules (breakage and compatibility)

1. **Postfixes only in v1.** No prefixes that skip the original (`return false`), no transpilers.
2. **Never mutate `ZInput` state.** `IsGamepadActive()` keeps telling the truth.
3. **Prefer one public choke point** over many private methods.
4. **Each feature has its own config toggle and guarded patch.** Missing target method → feature disables itself with a log warning. Postfix bodies are wrapped in try/catch and log an error once.
5. **Text rewrites run at low Harmony priority** (`Priority.Low`) so other mods' postfixes have already written their text.
6. **Translation logic is a pure function** (no Unity/Valheim types) with xUnit tests outside Unity. Patches only call it.

## Shared pieces

### Controller glyphs on (`GlyphService.ShowKeyPrompts`, decided by `GlyphMode.Decide`)
Read live on every prompt. True when all of:
- config `General.Enabled` and `General.KeyPrompts` are true,
- `ZInput.IsGamepadEnabled()` (gamepad not disabled in Valheim settings),
- a gamepad device is present (`UnityEngine.InputSystem.Gamepad.current != null`),
- the Settings screen is not open (`Settings.instance == null`), so the keyboard rebinding UI keeps showing real keyboard keys.

Log connected gamepads (name + layout) on startup and whenever one connects or disconnects. Steam Input's virtual controller connects after plugins load, so the startup line alone shows none (open question 1, answered).

### Mapping table (YAML)
- File: `<profile>/BepInEx/config/BrrControllerGlyphs.mapping.yaml` (named after the mod, not the plugin ID, so Gale sorts it next to the mod).
- **No mapping is built into the mod.** If the file is missing, the plugin writes `KeyMapTemplate` (format notes plus commented-out examples; maps nothing). Controller glyphs (F1) still work; keyboard-only keys stay as keys until the file is edited. Uncommenting an example (deleting its `#`) must be enough to enable it (tested).
- My layout lives in the test fixture `BrrControllerGlyphs.Core.Tests/Fixtures/steam-controller.yaml` and, as my real config, in the profile's mapping file. Later the VDF generator produces it.
- Maps a key name to display output: Valheim key names (`Q`, `LeftShift`, `Keypad1`, `1`–`8`), plus aliases for spellings mods use (`Alpha1`, `KeypadNumber1`, `Left Shift`).
- Output is a TMP string: a Valheim sprite tag for buttons Valheim has art for (A/B/X/Y, D-pad, bumpers), a text badge for buttons it doesn't (`[R4]`, `[L5]`, trackpad). Combos join parts: `[R4] + <sprite …dpad_up>`.
- Parsed with YamlDotNet from the shared `ValheimModding-YamlDotNet` package (see Dependencies). Parsing lives in the pure library.

My mapping (test fixture and profile config):

| Key | My button |
|---|---|
| `1`–`4` | hold R4 + D-pad Up / Right / Down / Left |
| `5`–`8` | hold R4 + Y / B / A / X |
| `Keypad1`–`Keypad4` | hold R5 + D-pad Up / Right / Down / Left |
| `Keypad5`, `Keypad6` | hold R5 + Y / B |
| `I` | R5 + A (backpack) |
| `N` | R5 + X (ImpactfulSkills AOE toggle) |
| `Q` | L5 (pickup) |
| `Z` | L4 (AzuAutoStore favorite) |
| `B` | left trackpad click up (AutomaticFuel torch mode) |
| `LeftShift` | left trackpad click down (AzuCraftyBoxes fill all) |

### Pure library: `BrrControllerGlyphs.Core`
- Separate `netstandard2.0` class library with no Unity/BepInEx references. The plugin compiles Core's source files into `BrrControllerGlyphs.dll` (linked `Compile` items), so only one DLL is deployed and `publish.ps1` needs no changes. xUnit test project `BrrControllerGlyphs.Core.Tests` references the Core project and targets a modern .NET.
- Holds **all decision logic** ("functional core"); Harmony patches are a thin "imperative shell" that gathers inputs from the game, calls Core, and applies the result.
- API sketch:
  - `KeyMap.Parse(string yaml) -> KeyMap` (throws `KeyMapException`)
  - `KeyMap.Translate(string keyName, Func<string, string> valheimGlyph) -> string` (null if unmapped; case-insensitive, aliases; `valheimGlyph` turns a Valheim control name such as `buttonSouth` into a sprite tag, else the button shows as a `[badge]`)
  - `ValheimButtons.TryGetControl` (A/B/X/Y/LB/... → Valheim control names)
  - `SpriteStyle.For(string glyphSetting, string gamepadType)`, `SpriteStyle.Tag(style, sprite)` (mirrors ZInput's xbox/ps5/switch2 choice)
  - `GlyphMode.Decide(bool enabled, bool gamepadEnabled, bool gamepadPresent, bool settingsOpen) -> bool`
  - `BoundKeyResolver.Resolve(string name, string originalResult, bool showGlyphs, Func<string, string> lookupJoy, Func<string, string> translateKey) -> string` (F1/F2)
  - `KeyMap.Buttons(string keyName) -> IReadOnlyList<string>`, `KeyMap.Format(buttons, valheimGlyph) -> string`
  - `SlotLabel.Buttons(string mainKey, IReadOnlyList<string> modifiers, Func<string, IReadOnlyList<string>> buttonsFor) -> IReadOnlyList<string>` (F3)
  - `BarLabels.Split(IReadOnlyList<IReadOnlyList<string>> slots) -> BarLabels { Groups, Slots }` (F3)
  - `PrefixColors.For(PrefixColorMode mode, IReadOnlyList<string> prefix, KeyMap map) -> string` (F3); `KeyMap.Color(button)`, `KeyMap.PrefixButtons`
  - `SlotLabelRefresh.Decide(SlotBarState? last, SlotBarState current, bool force) -> Skip | Write | Restore` (F3)
  - `AzuEpiQuickSlots` (AzuEPI's plugin ID and quick-slot config names; checked by contract tests)
  - `HintRewriter.Rewrite(string text, Func<string, string> translateKey, Func<string, bool> isKeyName) -> HintRewrite { Text, UnmappedKeys }` (F4; see rules below)
  - `ModGlyphs.TryGetSprite(string normalizedButton, out string sprite)` (F5)
  - `HintVisibility.Decide(...)`, `BadgeVisibility.Show(...)` (F6)
- See **Testing strategy** for what the tests cover.

## Dependencies (reuse what the profile already has)

Installed profile versions (QoL Valheim, 2026-11-01) and how we use them:

| Package | Version | Use |
|---|---|---|
| `denikson-BepInExPack_Valheim` | 5.4.2360 | Runtime. Manifest dependency (stub says 5.4.2333; bump). |
| `ValheimModding-Jotunn` | 2.30.2 | Runtime + `Jotunn.Logger`, later KeyHintManager (F7). Bump `JotunnLib` NuGet from 2.27.1 to 2.30.2 to compile against what runs; manifest says 2.26.1, bump too. |
| `ValheimModding-YamlDotNet` | 16.3.1 (YamlDotNet 16.3.0, assembly version 16.0.0) | Mapping file parsing. Core library references NuGet `YamlDotNet` 16.3.0 (same assembly version, so it binds to the shared DLL at runtime). Plugin must **not** ship or deploy `YamlDotNet.dll` (`PrivateAssets`/`ExcludeAssets="runtime"` or `Private=false`). Manifest dependency `ValheimModding-YamlDotNet-16.3.1`. |
| `shudnal-ConfigurationManager` | 1.1.27 | In-game config UI for free. Give config entries descriptions and `ConfigurationManagerAttributes` ordering; no hard dependency. |
| `sinai-dev-UnityExplorer` | 4.8.2 | Dev only: inspect UI objects (F3, F6), test snippets in its C# console, check `Gamepad.current`. Not a dependency. |
| `ValheimModding-JsonDotNET` | 13.0.4 | Not needed (YAML chosen). |

### Mods whose prompts we expect to translate (F2/F4 test targets)
AzuAutoStore (`Z` favorite), AzuCraftyBoxes (`LeftShift` fill all), AutomaticFuel (`B` torch mode), ImpactfulSkills (`N` AOE toggle), Vapok AdventureBackpacks (`I` backpack), PlantEasily, EpicLoot.

### Mods that may touch the same UI (compat checks)
- **MyLittleUI** (shudnal): changes hover text and item/hotbar UI. Our F3/F4 postfixes run at low priority so they see its final text; verify both still look right.
- **AzuExtendedPlayerInventory**: own quick-slot bar and labels (F3 note).
- **TradersExtended**, **EpicLoot**: own UI panels; only affected through `$KEY_` (F1/F2).

## Features

### F1. Controller glyphs in `$KEY_` prompts regardless of last input
- **Patch:** postfix on `ZInput.GetBoundKeyString(string name, bool emptyStringOnMissing)` (public instance method, `assembly_utils`). Every `$KEY_` token resolves through it via `Localization.Translate` → `Localization.GetBoundKeyString` → this method.
- **Behaviour:** if `ShowKeyPrompts` and `name` does not start with `Joy`, look up `"Joy" + name` (with `emptyStringOnMissing: true`, guarded against recursion). If non-empty, return it (Valheim's own glyph for the current `ZInput.CurrentGlyph` style). Otherwise fall through to F2.
- **Why this works:** vanilla `Translate` only tries `Joy…` when `IsGamepadActive()`; we make the keyboard path return the same answer, so the glyph no longer depends on last input.
- **Risk:** low (one public method, return value only).

### F2. Keyboard-only keys shown as my Steam buttons
- **Patch:** same postfix as F1. When there is no `Joy…` binding, translate the returned keyboard key string through `KeyMap.TryTranslateKey`. Unmapped keys stay as-is.
- **Note:** `GetBoundKeyString` returns localized display names for some keys (`s_keyLocalizationMap`, e.g. `$button_lshift`) before `Localization` resolves them. The mapping must match on those tokens too; verify the exact strings in game.
- **Risk:** low.

### F3. Hotbar slot labels
- **Covers:** Valheim's hotbar, AzuExtendedPlayerInventory's quick-slot bar, and in the inventory the top row and AzuEPI's quick-slot cells. Config `General.HotbarLabels`; prefix colors `General.PrefixColors`.
- **Patches:** postfixes on `HotkeyBar.UpdateIcons(Player)`, `ToggleBindingHint(bool)` and `OnEnable()` (all private, `assembly_valheim`). Rewrite each slot's `binding` text; the only objects added are the group prefixes and lines under one container per bar (`brrcg_PrefixLabels`, a child of the bar), rebuilt on each write and removed when glyphs turn off.
- **When:** `SlotLabelRefresh.Decide` (Core). These methods run every frame, so labels are written only when the slots were rebuilt (count or first slot object changed), glyphs toggled, or Valheim just reset them; when glyphs turn off, Valheim's labels are restored once with `ToggleBindingHint` (checked in game 2026-11-07: Settings shows `1`, `2`, ...).
- **Risk:** medium (private but long-stable).
- **Compat:** AzuEPI's quick-slot bar is a second `HotkeyBar`; AzuEPI rebuilds its slots with the same count after Settings closes, which is why rebuilds are detected by the first slot object, not only the count. AzuEPI keeps shortcuts for 8 quick slots whatever its `[3 - Quick Slots] Number of Quick Slots` is; unused ones keep defaults (`Alt + 7`, ...), so only the first "Number of Quick Slots" count when finding the shared prefix.
- **Inventory (2026-11-07):** first tried full-size glyphs on the top row and dropped them; now smaller glyphs (`<size=70%>`) with the same prefix groups. `InventoryLabelsPatch` postfixes `InventoryGui.UpdateInventory(Player)` at low priority, after AzuEPI's postfix there. `InventoryLabelWriter` labels the player grid's first row (vanilla writes `1`–`8` once when the grid is built, both input modes) and AzuEPI's quick-slot cells, found by the name AzuEPI writes on them (`QuickSlotCells.Find`; `AzuEpiQuickSlots.CellName`: its "Hotkey N Display Text", or the shortcut when blank) and remembered until the grid is rebuilt (AzuEPI only rewrites a cell's text when its name or cell changes). Prefixes sit above each group: a line across a row, down a column's left side, or an outline when a group spans rows and columns (`GroupShapes`). Glyphs off: top row back to `1`–`8`, quick-slot cells back to AzuEPI's text.
- **In-game finding (2026-11-02):** after v1, hotbar and AzuExtendedPlayerInventory extra-slot labels still show keyboard keys (expected: not covered by F1). Wanted: both show the mapped buttons (e.g. `[R4] + D-pad up` on slot 1, `[R5] + ...` on extra slots).
- **Limit:** Valheim can't see Steam action-set modifiers (holding R4/R5 switches layers inside Steam; the game only sees the key sent by the second button). Labels can always show the combo, but can't appear only while R4/R5 is held unless the hold itself sends a key.
- **Found with a `brrcg_dumphotbars` dev command (2026-11-02; removed once F3 settled):** labels are written in two places, and both apply to AzuEPI's quick-slot bar too (it's a second `HotkeyBar` named `AzuEPI_QuickAccessBar`): `UpdateIcons` when slots are created (AzuEPI sets its "Display Text" there), and `HotkeyBar.ToggleBindingHint`, which Valheim calls on every keyboard/gamepad switch and which overwrites every label with `1`.. or blank. That's why AzuEPI's names (`R5 Up`) disappear. Mods patching `HotkeyBar.UpdateIcons`: MyLittleUI (prefix 800, 2 postfixes), AzuEPI (prefix and postfix, priority 0), EpicLoot (transpiler); none of their postfixes write labels.
- **Implemented:** `SlotLabel.Buttons` (Core, tested) finds a slot's buttons from its keyboard shortcut (modifiers + main key) through the mapping; null leaves the slot alone (no key, unmapped). `BarLabels.Split` (Core, tested) groups runs of neighbouring slots that start with the same button (the R4/R5 hold) and moves their shared leading buttons into the group; unmapped slots split groups, one-button slots aren't grouped, every slot keeps a button. `HotbarLabelsPatch` postfixes `UpdateIcons`, `ToggleBindingHint` and `OnEnable` and calls `HotbarLabelWriter`, which only rewrites when slots were rebuilt, glyphs toggled, or Valheim just reset the labels, and restores Valheim's labels when glyphs turn off. Keys: Valheim's bar asks the original `GetBoundKeyString("HotbarN")` through a Harmony reverse patch (`OriginalBoundKeyString`, raw key, respects rebinding); AzuEPI's bar reads AzuEPI's `[8 - Quick Slot Hotkeys] Hotkey N` config at runtime (`QuickSlotKeys`, no compile reference). Config `General.HotbarLabels`. AzuEPI's own display names are ignored.
- **Size / presentation (in-game look 2026-11-07):** the full `[R4] + (glyph)` label wrapped a character per line in the hotbar's one-digit label box (`[` and `R` above the slot) and put the glyph over the item icon on AzuEPI's bar. Each slot now shows only its own button (one glyph, like vanilla's digit); a prefix right of the last slot looked good (2026-11-07).
- **Groups (2026-11-07):** each prefix group gets its prefix at its left end with a thin line to its last slot (`PrefixLabels`): above the hotbar (8 UI units of space; 2 looked cramped), below AzuEPI's bar (the hotbar is right above it). The bars start ~20 px from the screen edge, so nothing fits directly to their left. Mixed prefix buttons in one bar give one prefix per run.
- **Naming:** "prefix", not "hold": the shared leading buttons are an action-set hold in my layout, but Steam Input also has chords and mode shifts, so the mod doesn't assume how they're pressed. A *prefix group* is a run of slots; its *prefix* is the label beside it. ("Badge" still means the `[R4]` text shown for buttons Valheim has no art for.)
- **Colors:** `General.PrefixColors` = `Neutral` (default; slot-label color), `Palette` (built-in color per prefix button, numbered in prefix-button name order) or `Custom` (mapping file `colors:` section, `"#RRGGBB"`/`"#RRGGBBAA"`; unlisted buttons stay neutral). Keyed by the group's first prefix button, so R4 looks the same everywhere. Changing the setting rewrites the bars (`HotbarLabelWriter.Invalidate`).
- **Backings (2026-11-07):** the inventory panel's gamepad highlight ran through `[R4]`. Each prefix now sits on a rounded, translucent backing (`General.PrefixBackground`, default black at 70% (was 55%), alpha 0 hides it; changes redraw live). For a row it spans the prefix and its line; for a column or outlined group, the prefix only. Corners: a 7-unit radius from a small sprite built once at startup (`RoundedSprite`, pixels from `RoundedCorners.Coverage` in Core) and stretched with fixed corners; Unity has no rounded shape mods can use at runtime. The texture and sprite are marked `DontUnloadUnusedAsset`: in game the corners first showed square, most likely because Valheim's unused-asset sweep on world load freed the texture made at the main menu. The self-test checks the sprite is still loaded.
- **Attached look (2026-11-07):** with backings, the hotbar's prefix sits 2 units above the slots (like the quick-slot bar's below), so the backing meets the bar.
- **Alignment (2026-11-07):** backings started a few units right of the first slot: the slot's layout box differs from what's drawn. Bounds now come from each slot's visible background (the largest active image on the slot or its direct children, ignoring the selected, equipped and queued overlays), measured in world space.
- **Inventory spacing (2026-11-07):** tried 24 units above the slots to clear the panel border: far too high, and it showed the HUD hotbar's own prefix peeking out from under the inventory panel. Back to the attached 2 units, with the default backing darker (70%) to read over the highlight. Revisit once F5's glyph images replace the `[R4]` text.
- **Edges flush (2026-11-07):** the backing's left edge sits on the first slot's and, for rows, its right edge on the last slot's; padding goes inside, around the text and line. Outside padding showed on one side only wherever the measured and drawn edges differed slightly.
- **Inventory follow-ups:** check the 70% size, the prefix positions over the panel edge, and whether the grid clips anything drawn above its first row. **Checked with F5's glyphs (2026-11-08):** the inventory looked good; nothing clipped.
- **Bug (found 2026-11-08, for later): AzuEPI's other inventory layouts.** Everything above was built and checked in AzuEPI's *legacy* layout. In its "equipment slots and quick slots" layout, AzuEPI moves the quick-slot cells into their own panel under the equipment: those cells keep AzuEPI's own names (`R5 Up`, `R5 Right`, ...) instead of our glyphs, and our R5 outline is drawn around an empty column at the right of the equipment panel, where the cells were in legacy mode. To check: which layout setting AzuEPI uses and its values; how `QuickSlotCells.Find` and the group bounds pick the cells' parent and positions when the cells live in a separate panel; and each layout's look (outline vs a row prefix). Add the setting's name to `AzuEpiQuickSlots` with a contract test.
  - The setting: `[5 - UI Features] Inventory Layout` (type `InventoryLayout`, default `Default`; mine is `Legacy`). Values, from its config description:
    - `Default`: player preview and slots in their own panel beside the crafting panel.
    - `Tabbed`: preview and slots on top of the crafting panel, like the vanity, loadout and stats panels (needs `Display Equipment in Separate Panel` on).
    - `Legacy`: the original slot panel next to the inventory, no preview (what F3 was built for).
    - `LegacyDetached`: as Legacy, in its own panel set apart from the inventory.
    - `ExtraSlots`: slots next to the inventory in a 3-row grid with quick slots underneath.
    - `EquipmentAndQuickSlots`: staggered equipment block with quick slots underneath (the screenshot above).
  - Screenshots of each layout: ask the user when fixing this.
- **Later idea:** highlight the bar's slots while R4/R5 is held. Needs the Steam layout to also send a spare key while the action set is active (e.g. F13 on layer activation).

### F4. Hover text with key names written directly by mods
- **Where:** Valheim rebuilds the hover text every frame (`hoverable.GetHoverText()`, which mods extend with postfixes), then in gamepad mode only strips `[<color=yellow><b>` / `</b></color>]` around a single `<sprite=…>`, and assigns it to `Hud.m_hoverName.text` in `Hud.UpdateCrosshair(Player, float)` (private, `assembly_valheim`). One postfix there covers vanilla and every mod.
- **What mods write** (decompiled 2026-11-08):

  | Mod | Hint | Written as |
  |---|---|---|
  | AzuCraftyBoxes chest toggle | `[E + LeftShift]` | `KeyboardShortcut.ToString()` |
  | AzuCraftyBoxes fill all | `[LeftShift + $KEY_Use]` | shortcut text + `$KEY_Use` |
  | AutomaticFuel | `[B] AutoFuel: …` | shortcut text (`{togglekey}` placeholder) |
  | AzuAutoStore backpack | `Press [$KEY_Use] to open` | `$KEY_Use` (F1 already covers it) |

- **Keys, not actions (decided 2026-11-08):** the mapping file maps keyboard keys to the buttons Steam presses to send them; it knows nothing about actions. Valheim actions (`$KEY_Use`) are F1's job: they resolve through Valheim's bindings (Joy glyph, else the bound key through the mapping). Raw key names in mod text have lost their action, so they're translated only through the mapping. The spec's earlier idea (E -> Use -> X) is dropped: AzuCraftyBoxes reads its toggle with `Input.GetKey` (real keyboard keys only) and has no controller path, so showing X would be wrong. If a key really is pressed with a button for some mod, the user says so in the mapping (`E: X`); the mod never guesses.
- **`HintRewriter.Rewrite` (Core, TDD):**
  - Only looks inside `[ … ]` hints (outermost brackets; a nested `[R4]` badge counts as a glyph). Inside, TMP tags (`<color>`, `<b>`, `<sprite>`) are kept; the text between tags is split on `+`, and each trimmed piece is a token.
  - Token kinds: mapped key (replaced with its buttons), unmapped key name (left as is, reported), glyph (sprite tag or nested badge), other text.
  - Any "other" token means it isn't a key hint: the whole `[ … ]` is left untouched (`[Zebra]`, `[1/5]`, free text). Free text outside brackets is never touched.
  - When every token is a mapped key or a glyph, the outer `[ ]` are dropped, like vanilla in gamepad mode (also makes keyboard mode match gamepad mode for vanilla hints and avoids `[[L5]]`). Partly translated hints keep their brackets.
  - Idempotent: rewriting its own output changes nothing.
  - **Leftover repair (2026-11-08):** an unclosed `[` whose opening `<color=…>`/`<b>` tags run straight into a `<sprite=` is removed with those tags. That's what Valheim's gamepad cleanup leaves of a hint colored anything but `yellow` (it strips `"></b></color>]` but only matches `[<color=yellow><b><sprite=`). Other unclosed brackets stay. TODO in code: report it to MyLittleUI's author; once fixed upstream the repair finds nothing to do.
  - Whether a piece is a key name is a predicate from the plugin (Unity `KeyCode` names, any case), so Core stays free of Unity.
- **Patch:** postfix on `Hud.UpdateCrosshair`, low priority; runs only when `ShowKeyPrompts` and config `General.HoverText` are on. Caches the last input and output, so an unchanged hover costs one string compare per frame. Known cost: while a hint is rewritten, Valheim sets the original text every frame and we set ours back, so TMP re-lays out that one label each frame. Avoiding it would need a prefix or transpiler, which the design rules rule out; it's small.
- **Missing keys (2026-11-08):** each unmapped key name the rewriter meets is logged once per session ("Hover hint uses key E, which isn't in your mapping file: no controller button shows for it"), and `brrcg_selftest` lists the ones seen so far. Later, F9's mapping status in Mod Config can show them too, so users find gaps in their mapping without reading code.
- **Expected results with my mapping:** `[B] AutoFuel: …` -> `[LPad Up] AutoFuel: …`; `[LeftShift + (Use)]` -> `[LPad Down] + (Use glyph)`; `[E + LeftShift]` -> `[E + [LPad Down]]` with `E` reported (no button in my layout sends E).
- **In-game check (2026-11-08):** AutomaticFuel's `[LPad Up] AutoFuel` and AzuCraftyBoxes' `[E + [LPad Down]]` show as designed; vanilla chest hints match in both modes in keyboard mode. Two problems come from other mods, not ours:
  - **Smelter:** no AutomaticFuel line. MyLittleUI's postfix on `Smelter.OnHoverAddFuel` / `OnHoverAddOre` replaces the whole `__result`, dropping AutomaticFuel's appended line.
  - **Chest in gamepad mode:** a stray `[` and yellow text to the end of the line. MyLittleUI writes chest hints as `[<color=#ffff00ff><b>…`; vanilla's gamepad cleanup only matches `<color=yellow>`, so it removes the closing `</b></color>]` but not the opening. The hint has no closing bracket left, so the bracket rewrite can't see it; the leftover repair above fixes it (added after this check).
  - Missing-key reports first named the wrong line (substring match: "Chest ( EMPTY )" contains `E`); `UnmappedKey.Line` now carries the hint's own line, tags removed.
- **Out of scope:** ImpactfulSkills' crafting-panel hint (`m_recipeDecription`, not hover text); `<b>KEY</b>` hints without brackets (none of my mods use them).
- **Risk:** medium (one private method; mod-agnostic). Contract test for the method and `Hud.m_hoverName : TMP_Text`.

### F5. Glyph appearance
Designed 2026-11-08. Buttons Valheim has no art for (R4/R5, L4/L5, trackpads) show as glyph images instead of `[R4]`-style text badges. No patch: it changes what the mapping produces.

- **Art (decided 2026-11-08):** Kenney's *Input Prompts* 1.5A, Steam Deck set (CC0; https://kenney.nl/assets/input-prompts). It has every Deck button, both trackpads in every direction, and a plain and an `_outline` variant per glyph. Rejected:
  - Steam's own glyphs (`<Steam>\controller_base\images\api\{knockout,dark,light}`, `sd_*` / `sc_*`): no open licence found, so they could only be read from the user's Steam install at runtime, never shipped.
  - The CC0 OpenGameArt Steam Controller pack (2016): original Steam Controller only, no back buttons.
- **Set: Steam Deck** (chosen over Steam's `sc_` set: square back buttons, separate left/right trackpad arrows).
- **Look: dark fill, light rim, light letters, soft shadow** (chosen from mockups 2026-11-08). Valheim's glyphs are dark; dark alone vanished on the HUD, and white looked foreign. Made from Kenney's art: the plain glyph (letters cut out) plus its `_outline` variant (rim + letters) is the solid shape; fill that dark, draw the outline variant over it in light, add a shadow.
- **Build-time script:** `scripts/make-glyphs.ps1` reads the unpacked Kenney zip (path as a parameter; the zip isn't committed, the script says where to download it), recolors the 128 px `Double` PNGs, pads them for the shadow and writes `BrrControllerGlyphs/Glyphs/*.png`. The outputs are committed and embedded in the DLL as resources, with Kenney's `License.txt` beside them and a credit in the README. 28 images: A/B/X/Y, D-pad, bumpers, triggers, View/Options, R4/R5/L4/L5, both trackpads and their directions. Sticks are left out (Kenney has no outline variant to recolor them from), so under `ModGlyphsForAll` they keep Valheim's art.
- **Which art wins (decided 2026-11-08):** config `General.ButtonGlyphs`:
  - `ValheimFirst` (default): Valheim's sprite where it has one (A/B/X/Y, D-pad, bumpers; follows Valheim's Xbox/PlayStation glyph setting), else ours, else the `[badge]`.
  - `ModGlyphsForAll`: ours for every button in mapped output (hotbar labels, prefixes, hover hints), Valheim's for none, so labels look uniform. Valheim's own `$KEY_` prompts (F1) keep Valheim's art either way. Built in this phase: it's only the lookup order in `KeyMap.Format`.
  - The `[badge]` (with the `labels:` section's text) stays the last fallback in both.
- **Core (TDD):** `ModGlyphs.TryGetSprite(normalizedButton, out sprite)`, a table like `ValheimButtons`: `R4` → `r4`, `LPadUp` → `lpad_up`, `LPad` (click) → `lpad`, plus aliases (`L1`/`LB`, `L2`/`LT`, `View`/`Back`, `Options`/`Start`, ...). `KeyMap.Format` takes a second lookup for our sprites and the `ButtonGlyphs` order; tests cover both orders and the fallback. A test checks every sprite name in the table has an embedded PNG (reads the plugin's `Glyphs/` folder).
- **Sprite set (plugin, `ModSpriteAsset`):** at startup, load the embedded PNGs, pack them into one texture (`Texture2D.PackTextures`) and build a `TMP_SpriteAsset` named `brrcg_deck` with TMP's sprite shader (a glyph and a character per image, lookup tables updated). Register it with `MaterialReferenceManager.AddSpriteAsset(asset)` (keyed by the hash of its name) so `<sprite="brrcg_deck" name="r4">` finds it: TMP checks that table before `Resources.Load` (decompiled `TMP_Text` 2026-11-08).
  - Size and baseline copied from Valheim's `xbox` sprite asset (its `button_a` glyph metrics, scaled to our image size), so our glyphs line up with Valheim's in the same label.
  - Texture, material and asset marked `DontUnloadUnusedAsset` (Valheim's unused-asset sweep; see F3's backing sprite) and destroyed in `OnDestroy`.
  - TMP has no way to unregister, so after `OnDestroy` the lookup keeps a dead entry. Plugins are destroyed only when the game quits, so this is accepted, not worked around.
  - If the set can't be built, the plugin logs once and labels fall back to `[badge]` text.
- **Effects on F3/F4:**
  - Prefixes show a glyph instead of `[R4]`. `PrefixColors` keeps coloring the prefix's line and text; the glyph itself is not tinted (TMP sprites ignore the text color unless `tint=1`).
  - Recheck in game what F3 postponed: inventory prefix spacing over the panel edge, the 70% label size, and the backing width now that the prefix is one glyph.
  - `HintRewriter` already treats `<sprite…>` as a glyph: `[LPad Up] AutoFuel` becomes the glyph without brackets. Add a test with our sprite tag.
- **Self-test:** the sprite set is registered and its texture still loaded; formatting `R4` gives our sprite tag, and a TMP text with it has a sprite character in its text info (not the letters `[R4]`); dump Valheim's `xbox` sprite sheet to a PNG next to `LogOutput.log` (INFO) to compare the looks.
- **Contract tests:** `MaterialReferenceManager.AddSpriteAsset(TMP_SpriteAsset)` / `TryGetSpriteAsset`, `TMP_Asset.m_Version` / `faceInfo` / `hashCode`, `TMP_SpriteAsset` tables and `UpdateLookupTables()`, the `TMP_SpriteGlyph` / `TMP_SpriteCharacter` constructors, the `TMP_CharacterInfo` fields prefixes are measured with, `ImageConversion.LoadImage` / `EncodeToPNG` (called by reflection), and `MessageHud.m_messageText`.
- **Out of scope:** coloring glyphs per prefix; other controllers' sets (Xbox/PlayStation art for `ModGlyphsForAll` users on other pads); a setting for the rim and shadow colors.
- **In game (2026-11-08), tuned over five rounds of screenshots:**
  - Valheim's `xbox` sprite asset has no point size (`button_a`: height 32, bearing 20, scale 2.5, point size 0). TMP then fits every sprite's own height to the font ascent, so scaling by Valheim's pixel height made ours tiny. `SpriteFit` (Core, tested) handles both TMP paths.
  - Valheim's sprites carry transparent padding, so our 128 px glyph at its full size looked about 1.4 times Valheim's round glyphs. Glyphs are drawn at 0.7 of Valheim's sprite, centred on it.
  - Hover-hint lines are packed tightly (Valheim's own glyphs nearly touch), and the trackpad-up arrow was squeezed under the glyph on the line above: hover hints use their own translator with the glyph in `<size=75%>` (85% was still crowded). `$KEY_` prompts keep the full size.
  - Prefix glyphs hang below the text line and ran into the slots: prefixes are now placed by TMP's drawn bounds (`PrefixLabels.PlaceByDrawnBounds`), not the line box.
  - Kenney's 6 px rim looked thick next to Valheim's thin one: the script keeps the outer 3 px. The thin rim made the trackpad arrows dark and invisible at hint size: small separate shapes are drawn fully light.
  - Valheim's own glyphs are dark with a light rim and a soft shadow (its sheet, dumped by the self-test), so the chosen look matches.
  - Group alignment looked slightly off from a distance in full screen; up close it's the selected slot's white frame, which is larger than the slot. Left as is.
  - Prefix layering: prefixes and lines draw on top of the slots, so the selected slot's frame never cuts them (the user wanted the hotbar's R4 to overlap the frame like AzuEPI's row); the backings of prefixes above the slots go in a second container behind the slots, because Valheim's labels overhang the slots' top edge (the arrow on the D-pad-up glyph). A 6-unit lift for the hotbar was tried and dropped: the backing looked too tall and the raised prefix showed above the inventory panel.
  - Prefix bounds come from each character's mesh corners (`vertex_BL`/`vertex_TR`): `bottomLeft`/`topRight` reach the font's ascent and descent lines for sprites.
  - **Messages moved (decided 2026-11-08):** Valheim's top-left messages sat right under AzuEPI's bar and touched its R5 prefix. Chosen over moving R5 to the row's right end: `MessageRoom` moves the message text's parent down by how far the prefixes reach below the bar, only while the quick-slot row has prefixes, and moves it back otherwise (glyphs off, no groups, plugin destroyed). AzuEPI already lowers the same object by 75 units in its `MessageHud.Awake` postfix, so we only add and undo our own shift. First time the mod moves Valheim UI.
  - The self-test's text-layout check threw (a bare `TextMeshProUGUI` has no font or canvas); it now checks TMP's two lookups for the tag instead.
  - Build notes: `UnityEngine.ImageConversionModule` targets netstandard 2.1, which the net48 plugin can't reference (CS1705), so `PngFiles` calls `LoadImage`/`EncodeToPNG` by reflection (contract-tested); `UnityEngine.TextCoreFontEngineModule` is referenced directly for glyph metrics.
- **Known limits (review, 2026-11-08):**
  - Hover-hint glyphs use `<size=75%>`, which TMP takes from the text's base size, not the size around it. Inside a hint a mod wraps in its own `<size=…>`, our glyph keeps the base-relative size (the `<space=0.12em>` padding does follow the current size). None of my hints do this. If one does: a second sprite asset (`brrcg_deck_hint`) built at 0.75 scale shrinks with its context.
  - Switching `ButtonGlyphs` live redraws bars, prefixes and hover hints at once, but `$KEY_` text Valheim's Localization cached updates after the next input-mode switch (said in the setting's description). Clearing that cache is planned with F9's mapping reload.
  - `MessageRoom` moves the messages back when AzuEPI's quick-slot row loses its prefixes through our labels (glyphs off, no groups) or a world load; if AzuEPI's bar is turned off mid-session they stay lowered until then. It also doesn't check where the user placed AzuEPI's bar.
- **Later (asked 2026-11-08): `ModGlyphsForAll` everywhere.** Today it only changes text the mod writes (slot labels, prefixes, mapped keys in hover hints), as designed above; Valheim's own prompts keep Valheim's art, so a hint can mix both styles ("Use Wood" with Valheim's X next to our trackpad glyph). The user expects the setting to switch every glyph. Needs: F1's `$KEY_` results mapped from Valheim's sprite names (`button_x`, `dpad_up`, ...) to ours; hint panels and `UIInputHint` text the same way; image badges (`UIGamePad`) can't take a sprite tag, so those need our sprites as `Image` sprites. Design it before building (look per place, what happens for buttons we have no glyph for, e.g. sticks).

### F6. Hint panels and menu-button badges
- Bottom-right `KeyHints` panels, build-menu hints (`m_buildMenuHintsGP` / `KB`), `UIInputHint` gamepad/keyboard variants, `UIGamePad` button badges.
- For anything still wrong after F1, find the component with UnityExplorer and add a narrow visibility postfix (e.g. on `UIInputHint.UpdateInputHints`).
- **In-game finding (2026-11-02):** F1 keeps world/hover prompts as controller glyphs even with trackpad (mouse) input. **The inventory screen does not:** keyboard-mapped buttons or trackpad cursor movement switch its hints to keyboard. Those hints are object visibility (gamepad vs keyboard variants), not `$KEY_` text, so F6 is needed for the inventory. Next step: identify the inventory hint objects/components with UnityExplorer.
- **Constraint:** the trackpad cursor must keep working in the inventory (don't force gamepad mode; only show the gamepad hint variants).
- **Bug found 2026-11-02:** after keyboard input, the combat hint bar (Attack/Block/Dodge) showed controller glyphs on keyboard key-cap boxes: `UIInputHint` picked its keyboard version and F1 put glyphs in it.
- **Implemented (option A, chosen over a transpiler or faking `IsGamepadActive`):** postfix on `UIInputHint.UpdateInputHints` re-picks the version with Core `HintVisibility.Decide` (gamepad version while glyphs are on; vanilla when off; vanilla when the hint has no gamepad version). Copies vanilla's visibility condition (FIXME). Covered by the `KeyPrompts` toggle. If the inventory also needs `UIGamePad` badges, reconsider a shared approach (the transpiler or scoped `IsGamepadActive` options) instead of copying more conditions.
- **Verified in game (2026-11-02):** combat bar and inventory bottom hint bar stay on controller glyphs after keyboard/trackpad input, no key-cap boxes; trackpad cursor still works. **Still switching:** the `UIGamePad` badges on inventory buttons (e.g. `Y`, `LSt`) disappear after trackpad input. Next F6 target.
- **Scope checked in game (2026-11-08), keyboard vs gamepad screenshots:** bottom hint bars (inventory, build mode), the build piece list's badges (LB/RB tabs, LS/Y by the filter) and hover hints already show controller glyphs in keyboard mode. Only `UIGamePad` badges were missing: the inventory's LS/Y on the panel tabs, and at a crafting station RT (Upgrade tab), RS (item tabs) and X (Craft button).
  - Dropped after that check: `KeyHints` build-menu GP/KB arrays and `RadialKeyHints` next/prev, which switch by input mode in the decompiled code but looked right in game. The pause menu's controller diagram (`Menu.m_gamepadRoot`) is out of scope: Xbox layout, not a prompt.
- **Badges (2026-11-08):** `UIGamePad.Update` shows `m_hint` only when `IsGamepadActive() && (!IsMouseActive() || m_availableForConsoleMouse)` (or its alternative group is active). Postfix at `Priority.Low` re-decides with Core `BadgeVisibility.Show`: with glyphs on, keyboard/mouse mode counts as plain gamepad mode; Valheim's gamepad cursor mode (gamepad and mouse both active) keeps vanilla's choice, since there unmarked buttons are blocked. Uses vanilla's own `IsInteractive()`; copies the rest of the condition (FIXME). Returns early in gamepad mode (vanilla's answer is ours there; runs per badge per frame) and only calls `SetActive` on a change. Covered by `KeyPrompts`; when glyphs turn off, vanilla's next `Update` restores the badge. **Verified in game (2026-11-08):** badges stay in keyboard mode in the inventory and at a crafting station; `brrcg_selftest` all PASS, including the new badge postfix check.

### F7. Jotunn KeyHintManager hints (deferred)
- Tool/building hints registered by mods through Jotunn. First check whether those mods declare gamepad buttons in their `ButtonConfig`; otherwise postfix Jotunn's hint update.
- **No target in my profile (checked 2026-11-08):** no plugin DLL in "QoL Valheim" except Jotunn references `KeyHintManager`, `KeyHintConfig` or `AddKeyHint`. AzuEPI and XPortal use `ButtonConfig` only to register inputs. Revisit if a mod I add registers key hints.

### F8. Config and mapping generation
- BepInEx config (renamed 2026-11-08, see "Settings names" under F9; old names in brackets):
  - `General`: `Enabled` (master switch); `Glyphs used by mod` [ButtonGlyphs] (`Valheim` / `SteamDeck`, default `Valheim`) (F5).
  - `Hint Glyphs`: `Force controller glyphs in Valheim hints` [KeyPrompts] (F1+F2, F6); `Force controller glyphs in mod hints` [HoverText] (F4).
  - `Hotbar Glyphs`: `Force hotbar controller glyphs` [HotbarLabels]; `Hotbar group colors` [PrefixColors] (`Neutral` / `Palette` / `Custom`); `Hotbar group background` [PrefixBackground] (color with alpha, default black at 70%) (F3).
  - `Key Glyph Mapping` (read-only, F9): `Mapping file`, `Mapping status`, `Keys missing from mapping`.
- Later: a script that generates the YAML mapping from my Steam layout VDF.
- **Status (2026-11-08):** every entry above is bound. The generator is the same item as F9 piece 6 and is tracked there.

### F9. Helping other users set up their mapping
Written 2026-11-07, before any of it is built. Today the mapping is a YAML file only: `BepInEx/config/com.angiebrr.controllerglyphfixer.mapping.yaml` (now `BrrControllerGlyphs.mapping.yaml`), written from `KeyMapTemplate` on first run with every example commented out, read once at startup. A new user has to find the file, learn the format, edit it in a text editor and restart the game.

**What can't move into the game.** The *button* half of each entry can't be detected: Steam Input turns the button press into a key before Valheim sees anything, and action-set holds, chords and mode shifts are invisible to the game. The game only ever sees the resulting key. So users must write the buttons themselves, reading them from their own Steam layout (Steam's controller configurator, outside the game; or Big Picture's overlay while in game). The spec and the mod's README must say this plainly. The *key* half can be confirmed in game (below).

**Settings UIs in my profile** (both read the BepInEx config):
- Jotunn's *Mod Settings* (main menu and pause menu): lists config entries. To check: which `ConfigurationManagerAttributes` it honours (probably `Order`, `ReadOnly`, `Browsable`; not `CustomDrawer`).
- shudnal's ConfigurationManager 1.1.27 (`F1`, or the pause menu's *Mod Config* button): honours `ConfigurationManagerAttributes` including `CustomDrawer`, `ReadOnly`, `HideDefaultButton` (seen in its DLL), and has an in-game **File Editor** for `.cfg`, `.yaml` and `.json` files (its changelog). So users with it can edit the mapping file without leaving the game.

**Proposed pieces** (smallest first; each optional):
1. **Mapping status in the config** (`[Mapping]` section, read-only): the file's path, and "Loaded 20 keys" or the parse error with its line number. Shows in both settings UIs, so a broken file isn't silent. (Read-only entries are still written to the `.cfg`; acceptable, or use a `CustomDrawer` label that isn't persisted, ConfigurationManager only.) Also list the **missing keys**: key names seen in hover hints this session that the mapping doesn't cover (F4), e.g. "E (AzuCraftyBoxes chest toggle)", so users know what to add.
2. **Reload without restarting** (the existing TODO in `BrrControllerGlyphs.LoadKeyMap`): watch the file (`FileSystemWatcher`), plus a *Reload mapping* button (`CustomDrawer`) and a `brrcg_reloadmapping` console command. On reload: swap the `KeyMap` in `GlyphService`, clear Localization's cache, call `HotbarLabelWriter.Invalidate()`. With ConfigurationManager's File Editor this gives a full in-game loop: edit, save, see the labels change.
3. ~~**Key finder** (`brrcg_keylog`)~~ **Dropped 2026-11-08:** a command that watches key presses reads as a keylogger, even one that only names keys while switched on. Replaced by a **shortcut audit**: other mods' keyboard shortcut settings, read once at startup, compared with the mapping. "Which key does this button send?" is answered by the Steam layout file instead (next phase, see piece 6).
4. **Starter mappings:** a few commented example blocks in the template (Steam Controller back buttons + action set, Steam Deck back grips, a DualSense touchpad layout) so users copy the closest one instead of starting from nothing.
5. **Open file / write template buttons** (`CustomDrawer`, ConfigurationManager only): *Open mapping file* (opens it in the system's default editor, outside the game) and *Reset to template* (with confirmation; backs up the old file first).
6. **Next phase (decided 2026-11-08):** audit the Steam layout VDF against the mapping (keys the layout sends that the mapping lacks, entries that disagree), then generate the mapping from it, so most users never type buttons at all. Both need the same parser. Found 2026-11-08: Steam keeps the layouts at `<Steam>\steamapps\common\Steam Controller Configs\<account id>\config\892970\*.vdf`; mine is `FXA9962300C51.vdf` ("Valheim - Gamepad - Base Layout 2 - Action Set Layers", action sets Default / Hotbar / Quick slots) with bindings such as `"key_press B, Torch on/off/timer, , "` and `"xinput_button X, Use, , "`, so the labels I gave each binding are in the file. To research: which file is the active one, and how groups, modes, action sets and layers nest. Read-only, and only Valheim's folder; the README says so.

**Built 2026-11-08 (pieces 1–3, branch `phase-4-mapping-setup`).** Seen working in game 2026-11-08: the settings sections and names in Mod Config, the missing-keys tree (wrapping is fine), toggling glyph settings mid-game (only My Little UI's own chest cache stays stale, see below), and the radial menu hint showing LB. Not yet checked: saving the mapping file to reload, an invalid file's status, the Reload and Copy buttons, and the new `brrcg_selftest` lines.
- **Settings names (reviewed with me one by one, 2026-11-08):** sections in Title Case, settings in sentence case, all in Core `SettingNames`; US spelling ("color") everywhere. Sections `General`, `Hint Glyphs`, `Hotbar Glyphs`, `Key Glyph Mapping` sort in that order alphabetically (both settings UIs sort by name; unit-tested); `ConfigurationManagerAttributes.Order` keeps each section's order. Settings and values as listed under F8. Notes from the review: the Valheim-hints switch was "Override keyboard prompts" for a while; in game it was unclear what it covered (the hotbar has its own switch), so it moved with the mod-hints switch into `Hint Glyphs` (chosen over "Crosshair Hint Glyphs", which sorts first and undersells hint bars and badges) as `Force controller glyphs in Valheim hints` / `… in mod hints`: "Force" because off leaves Valheim's own behavior rather than forcing keys, and mouse input doesn't switch them back. Descriptions say what off does and that input isn't changed; `Glyphs used by mod` says it only changes glyphs this mod draws (Valheim's own prompts keep Valheim's art, see the `ModGlyphsForAll everywhere` item under F5). **No migration of old names:** this is a local mod, so renamed settings reset to defaults once and the old entries stay in the `.cfg` until it's deleted (decided 2026-11-08; a carry-over through BepInEx's orphaned entries was built and removed). Short PascalCase `.cfg` keys with the long names as `DispName` were considered and dropped: most mods in my profile (Azumatt's, shudnal's) use readable keys with spaces, and one name everywhere is simpler. Tooltips shortened to one or two sentences, and read-only settings' tooltips start with "Read-only." (the .cfg and Jotunn's Mod Settings may not enforce it; asked 2026-11-08).
- **Status:** Core `MappingStatus` ("Loaded 20 keys", or the error plus "Still using the last good mapping"). A failed reload keeps the last good mapping; a failed first load maps nothing, as before. In shudnal's ConfigurationManager `Mapping status` has a *Reload mapping* button (`CustomDrawer`; the reload runs in the next `Update`, not mid-draw); Jotunn's Mod Settings shows the plain text. Read-only values are set without saving the `.cfg` (`SaveOnConfigSet` off around the write), so no disk write runs inside a frame; the `.cfg` catches up on the next save.
- **Reload:** `MappingFile` watches the file (`FileSystemWatcher` raises a flag; the plugin's `Update` reloads 0.5 s later on the main thread, since editors save in several writes), plus `brrcg_reloadmapping`. A reload swaps `GlyphService`'s map, redraws bars, prefixes and hover text, and empties `Localization.m_cache` (the `$KEY_` text cache). `Glyphs used by mod` changes empty it too, which should fix the known limit above (to check in game).
- **Keys missing from mapping** (one read-only setting, merged 2026-11-08 from "missing hint keys" and "unmapped shortcuts", which read alike): Core `MissingKeys.Find` merges two sources by key, skipping keys the mapping has, `None`, mouse and joystick buttons, and only listing the unmapped keys of a combo. Every spelling of a key is one entry (`KeyMap.KeyId`); `KeyMap` now also accepts Unity KeyCode names, which other mods' settings use, for keys the Input System names differently (`LeftControl`/`LeftCtrl`, `Return`/`Enter`, `KeypadEnter`/`NumpadEnter`, `LeftWindows`/`LeftMeta`, …), found in review 2026-11-08: a key mapped as `LeftCtrl` was listed as missing under `LeftControl`. Each key's uses (and the summary's mods) are sorted alphabetically (asked 2026-11-08):
  - **Mod keybindings**, read once when the main menu first opens (Jotunn's `GUIManager.OnCustomGUIAvailable`, then unsubscribed; every plugin has bound its config by then): `ModShortcuts` reads each loaded plugin's `KeyboardShortcut` and `KeyCode` settings through `Chainloader.PluginInfos`, read-only, and the log lists each missing key with every setting using it. Not seen: settings a mod keeps in its own file, and hard-coded keys.
  - **Hover hints** this session: `HoverTextWriter` records each unmapped key's first line, and tells listeners after the hover text is set, catching their errors (review 2026-11-08: a failed `.cfg` save mid-rewrite could leave the previous object's hint on screen). Hover text doesn't say which mod wrote a line, so `HoverTextMods` names the mods with Harmony patches on the hovered object's `GetHoverText` (Harmony IDs shown as plugin names; cached per type; only looked up on a key's first sighting): one mod, "A or B", or none ("UI Hint" alone) when none patch it. A best guess, said in the setting's description. Mods that add hint lines from other methods (e.g. MyLittleUI's smelter patches) aren't named.
  - **Shown as** a tree by key, chosen 2026-11-08 over a paste-ready YAML stub or a tree by mod after the one-line list was hard to read in game (~20 keys wrapping): the key, then its uses sorted: `├─ AzuCraftyBoxes > UI Hint > Fill all` for hints (`A or B > UI Hint > …` when several mods patch it, `UI Hint > …` when none do; asked 2026-11-08) and `├─ Mod > Setting` for keybindings (the whole shortcut only for combos). Long lines wrap; checked in game, fine. `MonoTextDrawer` draws it in ConfigurationManager in an OS monospace font (Consolas, else Courier New; destroyed with the plugin) with a *Copy* button; the `.cfg` and Jotunn's Mod Settings show the one-line summary ("E (AzuCraftyBoxes, Epic Loot), T (UI Hint)"). A reload re-checks both sources against the new mapping without reading the mods again. My profile has ~60 keybinding settings, most of them keyboard-only conveniences; the description says to add only keys the Steam layout sends.
- **Template (2026-11-08):** its button list predated F5 and called R4, L5 and the trackpads text badges. It now lists every button with art, grouped (face, bumpers, triggers, sticks, back buttons, menu, D-pad, trackpads) with their other spellings and which art each has; a test fails if a button in `ValheimButtons` or `ModGlyphs` is missing from it. Only new files get the template; an existing mapping file isn't rewritten.
- **Self-test:** mapping loaded, file watched, Localization cache reachable; INFO lines for the number of keybinding settings read, the mods patching the hovered object's hover text (look at a chest), and the missing keys.
- **In game 2026-11-08: turning the Valheim-hints switch off mid-game** left chest and fire hints on glyphs while others went to keys: Valheim's `Localize` cache kept text translated while it was on. Every setting that changes which glyphs show (Enabled, both Hint Glyphs switches, Glyphs used by mod, Force hotbar controller glyphs), and a gamepad connecting or disconnecting, now runs `RedrawGlyphs` (one `Config.SettingChanged` handler). Its description now says what it covers (crosshair hints, hint bars, menu badges) and that the hotbar has its own switch.
- **In game 2026-11-08: `[G] Open Radial Menu` on cooking stations.** My Little UI adds `[$KEY_OpenRadial] $settings_open_radial` to hover hints. Valheim finds a key's controller binding as "Joy" + name, but the radial menu's are `OpenRadial` (keyboard) and `JoyRadial` (controller), so `JoyOpenRadial` doesn't exist and even Valheim's own gamepad mode shows G. Valheim's naming quirk plus My Little UI not handling it; not a config problem. Fixed on our side: `BoundKeyResolver.ControllerNameExceptions` (`OpenRadial` → `JoyRadial`, contract-tested against ZInput's literals; the only keyboard/controller pair that's clearly the same action). Worth reporting to My Little UI's author (shudnal). **Verified in game 2026-11-08:** the hint shows LB.
- **In game 2026-11-08: after turning the Valheim-hints switch off, some chests kept the X glyph, others showed E.** My Little UI caches each chest's whole localized hover text per chest (`ChestHoverText.hoverTextCache`, keyed by the chest's ZDO), cleared only when that chest's contents change (`Container.OnContainerChanged` postfix) or its own settings change (`ResetHoverCache`). Chests hovered before the toggle keep the glyph they were cached with. The same happens in vanilla when switching between keyboard and controller (the cached `$KEY_Use` doesn't follow the input mode), so it's My Little UI's bug, exposed by our toggles and gamepad connects. Options: call its `ResetHoverCache` by reflection from `RedrawGlyphs`, put that in the F10 compat plugin, or report upstream (cache should be cleared on `ZInput.OnInputLayoutChanged`). **Decided 2026-11-08:** F10 compat plugin (fix 4), not this mod; also worth reporting upstream.
- **To check in game:** that the first main menu fires `OnCustomGUIAvailable` (the self-test's keybinding count says so), and whether Jotunn's Mod Settings honors `ReadOnly`.

**READMEs (asked 2026-11-08, written 2026-11-08).** Were the JotunnModStub template; now two documents for two readers:

- **Development README** (repo root `README.md`): for me or a contributor. What the mod is in two lines and a link to the mod-page README; repo layout (Core, plugin, the two test projects, `scripts/`); setup (`Environment.props` and its four paths, the Gale profile, ilspycmd); build/test/deploy commands and the locked-DLL gotcha; `brrcg_selftest` and contract tests after a game update; the design rules in short, pointing to `CLAUDE.md` and the spec for the rest; how the glyph PNGs are made (`scripts/make-glyphs.ps1`, Kenney credit). Drop the template's Unity sections unless the Unity project is used.
- **Mod-page README** (`BrrControllerGlyphs/README.md`, packaged for Thunderstore): for players.
  - **Style I like:** ImpactfulSkills (Thunderstore): short tagline and intro, then one section per feature with its settings named right there in the text, a screenshot per feature, console commands, suggested mods to pair with. RimWorld Workshop pages (Vanilla Temperature Expanded, Reel's Facial Animation Textures): friendly first-person prose intro that says the problem the mod solves, requirements at the top, a Q&A FAQ ("Can I add it to an existing save?"), an honest compatibility list and plain disclaimers about what it doesn't do, how to report issues, and credits.
  - **Contents:** tagline; the problem (Steam Input layouts that mix gamepad buttons and keyboard keys flip Valheim to keyboard prompts) and what the mod does about it, with before/after screenshots; requirements (BepInEx, Jotunn, a controller through Steam Input); features, each with a screenshot and its settings (prompts stay controller glyphs, hotbar and quick-slot labels with group prefixes, hover hints from other mods, glyph art, inventory badges); **setting up your mapping**: "the mapping lives outside your Steam layout and must match it", then the steps (open the file, read each key from Steam's controller configurator or let the layout audit list them, write the buttons, save and see it reload) and the `labels`/`colors`/`aliases` sections; console commands; FAQ (does it change my input mode? does the trackpad cursor still work? why does a hint still show a key? can I use it without a Steam Controller?); compatibility (AzuEPI, MyLittleUI, ConfigurationManager, mods whose hints it rewrites); limits (only keys Steam sends are seen; holds and chords aren't; Valheim-art vs mod-art mix); reporting issues; credits (Kenney's Input Prompts, CC0); changelog link.
  - Screenshots need to be taken in game; list the shots needed when writing it.
  - **Written 2026-11-08** to this plan. Screenshots go in tables like ImpactfulSkills' (one column per state: vanilla, then the mod or each setting value), linked from GitHub as `docs/images/<name>.png?raw=true`; `docs/images/README.md` lists the shots still to take.
  - **Hotbar grouping table (2026-11-08):** rows no groups / one group / multiple groups, columns HUD and inventory, each with and without AzuEPI quick slots (12 shots). The mappings for them are in `docs/example-mappings/` (one-group is the author's layout); `ExampleMappingsTests` checks each groups both bars as named.
  - **Mapping check section (2026-11-08):** the mod page explains the audit in its own section (sources: other mods' keybinding settings at the main menu and hover hints seen; what it leaves out; one-line and tree forms; status and reload), with two shots (`audit_missing_keys`, `audit_status_error`).
  - **Known issue (user, 2026-11-08):** switching the mod or its features off and on while the game runs can leave labels or hints in the wrong state; a restart fixes it. Listed in the mod page's Known issues until fixed. The layout audit (F9 piece 6) isn't in the setup steps until it exists.
  - **Known issues on the mod page (2026-11-08):** also lists AzuEPI's inventory layouts (only `Legacy` built and checked; see F3's layout bug) and the top-left messages moved for AzuEPI's bar (see `MessageRoom` in F3). Remove each when fixed.

### F10. Mod compat plugin (later, separate mod)
A small separate BepInEx plugin for conflicts between other mods in my profile. Not part of BrrControllerGlyphs, whose rules forbid changing other mods' patches or behaviour.

- **Structure:** one class per fix, each with:
  - a config switch (`[Fixes] AutomaticFuelSmelterHint`, `[Fixes] ChestRenameOnHold`, …), on by default, so any fix can be turned off on its own;
  - a list of required plugin IDs (and the member names it touches), checked through `Chainloader.PluginInfos` after all plugins have loaded. If one is missing, or a member was renamed, the fix logs why it's skipped and does nothing;
  - a startup log line per fix: applied, skipped (switched off), or skipped (requirement missing);
  - contract tests that read the target mods' DLLs in the Gale profile (as for AzuEPI), skipping when a mod isn't installed.
- **Fix 1, AutomaticFuel smelter hint** (needs `TastyChickenLegs.AutomaticFuel`, `shudnal.MyLittleUI`): MyLittleUI's postfixes on `Smelter.OnHoverAddFuel` / `OnHoverAddOre` replace the whole `__result`; when they run after AutomaticFuel's, its "[B] AutoFuel …" line is lost (fuelling still works). The fix finds AutomaticFuel's two postfixes (classes `Smelter_GetHoverText_Patch` / `Smelter_GetHoverTextFuel_Patch`) via `Harmony.GetPatchInfo`, unpatches them, and patches them again with `Priority.Last`, so they append to MyLittleUI's text. Alternatives: turn off MyLittleUI's `hoverSmelterShowFuelAndItem` and `hoverSmelterEstimatedTime`; or ask AutomaticFuel's author to add `[HarmonyAfter("shudnal.MyLittleUI")]` (no source repo or licence found).
- **Fix 2, chest rename on a held alternate Use** (needs `shudnal.MyLittleUI` with *Hover - Chests → Enable custom names* on, `Azumatt.AzuCraftyBoxes`):
  - **Conflict (found 2026-11-08):** both mods use a tap of Valheim's alternate Use on chests (LT + X; Shift + E on keyboard). AzuCraftyBoxes' `Container.Interact` prefix runs first, toggles the crafting chest and cancels; MyLittleUI's rename prefix (priority 0) then sees the interaction cancelled and does nothing. So Rename never opens on chests AzuCraftyBoxes manages, while its hint still shows. MyLittleUI's rename has only an on/off setting, and both read the same game input, so neither settings nor the Steam layout can separate them.
  - **Fix:** a tap keeps the crafting toggle; holding the alternate Use opens MyLittleUI's rename (vanilla `Container.Interact` ignores held presses and both mods skip them, so the input is free). Its rename hint reads "hold". First check that "Hold to stack" doesn't also fire while the alternate key is held.
  - **Current workaround (2026-11-08):** custom names turned off in MyLittleUI.
- **Fix 3, one gamepad selection square at start** (needs `Azumatt.AzuExtendedPlayerInventory`; found 2026-11-08):
  - **Bug:** after loading in, both Valheim's hotbar and AzuEPI's quick-slot bar show the gamepad selection square on slot 1. AzuEPI replaces `HotkeyBar.Update` (prefix returning false) with its own controller (`HotkeyBarController.Hud_Update_Patch`, a `Hud.Update` postfix), which keeps one selected bar and sets `m_selected = -1` on the others, but only in `SelectHotkeyBar`, on the first D-pad left/right press. Until then `SelectedHotkeyBarIndex` is -1 and every bar keeps Valheim's default `m_selected = 0`, and `HotkeyBar.UpdateIcons` draws a square at `m_selected` whenever the gamepad is active. With the group prefixes it also makes the R4/R5 groups look misaligned (the frame is larger than the slot).
  - **Fix:** once AzuEPI has found its bars (`ExtendedPlayerInventory.HotkeyBars` set) while `SelectedHotkeyBarIndex` is still -1, call its `SelectHotkeyBar(0, fromRight: false)`: the hotbar keeps slot 1, the other bars get -1, exactly as the first D-pad press would. Postfix on `Hud.Update` after AzuEPI's (`[HarmonyAfter]`).
  - **Upstream:** worth reporting to AzuEPI's author (same fix inside its controller, e.g. select bar 0 where it first fills `HotkeyBars`). Confirmed in game (2026-11-08): one D-pad left/right press clears the second square.
- **Fix 4, stale chest hints** (needs `shudnal.MyLittleUI`; found 2026-11-08, see F9's in-game findings): My Little UI caches each chest's localized hover text per chest (`ChestHoverText.hoverTextCache`) and only clears it when that chest's contents or its own settings change, so a chest keeps the key glyphs it was cached with after an input switch (in vanilla too) or after BrrControllerGlyphs's glyph settings or the gamepad change.
  - **Fix:** call its `ChestHoverText.ResetHoverCache()` on `ZInput.OnInputLayoutChanged` and when BrrControllerGlyphs's glyph settings change (watch its config entries through `Chainloader.PluginInfos`, no compile-time reference) and when a gamepad connects or disconnects.
  - **Upstream:** ask shudnal to clear the cache on `ZInput.OnInputLayoutChanged`; same report as the `$KEY_OpenRadial` / `JoyRadial` mismatch.
- **MyLittleUI audit (2026-11-08):** it fully replaces hover text for chests, smelters (add fuel, add ore, empty), cooking stations and pickables (name only), so other mods' lines there can be lost; it only appends for fermenters, plants, beehives, tameables, characters and eggs. Only its chest hints use a non-`yellow` color (`#ffff00ff`).

## Testing strategy

Develop test-first. Valheim code needs Unity's native engine, so it can't run in a test runner; keep logic in Core and patches thin. Four layers:

### 1. Unit tests (`BrrControllerGlyphs.Core.Tests`, xUnit, .NET 10) — TDD, seconds
Red → green → refactor for every Core function. Required cases:
- `KeyMap`: every row of the fixture mapping (tests supply the mapping; Core has none); aliases (`Alpha1`, `KeypadNumber1`, `Left Shift`, `$button_lshift`); case-insensitivity; unknown keys pass through; combos; invalid YAML gives a clear error; the template maps nothing, and uncommenting its examples yields a valid map; empty sections are allowed; keys written in the file in any spelling (`Numpad1`, `Alpha1`) match every spelling; two entries for the same key (`Q` and `q`, `"1"` and `Alpha1`) are an error naming both lines.
- `GlyphMode.Decide`: truth table of the four inputs.
- `BoundKeyResolver.Resolve`:
  - `("Use", "E", showGlyphs: true, joy → A sprite)` → A sprite.
  - `("Hotbar1", "1", showGlyphs: true, joy → "")` → mapped `R4 + D-pad Up`.
  - `showGlyphs: false` → original result unchanged.
  - `name` starting with `Joy` → never looks up `JoyJoy…` (recursion guard).
  - unmapped key with no Joy binding → original result.
- `SlotLabel.Buttons`: hotbar and quick-slot keys give the mapped buttons; modifiers first (unmapped ones keep their name); null modifiers mean none; unmapped main key or no key → null (leave the label).
- `BarLabels.Split`: one prefix button → one group; mixed → a group per run; a returning prefix button starts a new group; a lone slot still gets a group; unmapped slots split groups; one-button slots aren't grouped; multi-button prefixes; names match like the mapping file; no mapped slots.
- `PrefixColors.For`: neutral → none; palette in prefix-button name order, unknown buttons none, wraps around; custom from `colors:`, unlisted none; multi-button prefixes use the first.
- `KeyMap` colors section: values matched like button names; invalid colors name the line; `PrefixButtons` sorted and distinct.
- `KeyMap.Buttons` / `KeyMap.Format`: raw button names; formatting matches `Translate`.
- `SlotLabelRefresh.Decide`: first sight, unchanged (skip), forced, slots rebuilt with the same count (new first-slot ID), count changed, glyphs on/off (restore once, never again).
- `ValheimButtons`: every button with Valheim art; Steam-only buttons have none.
- `HintRewriter.Rewrite`: `[B]`, `[<color=yellow><b>B</b></color>]` and combos (`[LeftShift + sprite]`) translated, brackets dropped when fully translated; partly translated hints keep brackets and report unmapped keys; non-key brackets (`[Zebra]`, `[1/5]`) and free text untouched; nested badges count as glyphs; idempotent.
- `BadgeVisibility.Show` (F6): glyphs off matches vanilla `UIGamePad.Update`; glyphs on shows interactive badges after keyboard or trackpad input; inactive buttons stay hidden; Valheim's gamepad cursor mode keeps vanilla.
- `ModGlyphs` / `KeyMap.Format` (F5): every Deck button and alias; `ValheimFirst` and `ModGlyphsForAll` orders; badge fallback when neither has art; every sprite name has a PNG.

### 2. Contract tests (`BrrControllerGlyphs.Contract.Tests`) — catch game updates, seconds
Inspect the real game DLLs **read-only** with `System.Reflection.MetadataLoadContext` (no game code runs). Paths come from `Environment.props` (`VALHEIM_INSTALL`); tests skip when the game isn't installed. Assert each patch target and touched member exists with the expected signature:
- `ZInput.GetBoundKeyString(string, bool) : string`, `ZInput.instance`, `IsGamepadEnabled()`, `IsGamepadActive()`, `IsMouseActive()`, `CurrentGlyph`, `ConnectedGamepadType`, `s_gamepadSpriteMap`, glyph enum names (assembly_utils)
- `UIInputHint` members used by the F6 patch; `UIGamePad.Update`, `IsInteractive()`, `m_hint`, `m_availableForConsoleMouse`, `alternativeGroupHandler` (F6 badges)
- `HotkeyBar.UpdateIcons(Player)`, `ToggleBindingHint(bool)`, `OnEnable()`, `m_elements`, `ElementData.m_go` (F3)
- `Settings.instance`, `Localization.instance.Localize(string)`
- To do with F4: `Hud.UpdateCrosshair(Player, float)`, `Hud.m_hoverName : TMP_Text`
- F5 (`Unity.TextMeshPro`): `MaterialReferenceManager.AddSpriteAsset(int, TMP_SpriteAsset)` and the `TMP_SpriteAsset` / `TMP_SpriteGlyph` / `TMP_SpriteCharacter` members we set

Other mods' names we copy are checked against their DLLs in the Gale profile (`GALE_PROFILE`), by reading the string literals in their code; tests skip when the mod isn't installed. AzuExtendedPlayerInventory: plugin ID, `8 - Quick Slot Hotkeys`, `Hotkey N`, `3 - Quick Slots`, `Number of Quick Slots`. Run after every Valheim or mod update before launching the game.

### 3. In-game self-test (Jotunn console command `brrcg_selftest`) — real game
Registered by the plugin; logs `PASS`/`FAIL` per check with a `[brrcg_selftest]` prefix:
- each of our Harmony patches is applied (`Harmony.GetPatchInfo`);
- `ZInput.IsGamepadActive()` still reports the real input mode (behaviour untouched);
- a gamepad device is present (open question 1);
- `Localization.instance.Localize("$KEY_Use")` returns a controller sprite while in keyboard mode; `"$KEY_Hotbar1"` returns the mapped string;
- F3: the raw `Hotbar1` key can be read (reverse patch); AzuEPI's quick slot 1 shortcut can be read (if installed); slot 1 of the hotbar and the quick-slot bar shows its own button, and each bar shows one prefix per group of slots on screen (in a world).
- F4: the `Hud.UpdateCrosshair` postfix is applied; a sample hint loses its brackets around a glyph while `[Zebra]` stays; the keys hover hints used this session that the mapping lacks are listed (INFO). Semi-automated: run `brrcg_selftest` in the F5 console, then grep `LogOutput.log`. Later option: a launch flag (`-brrcg_selftest`) runs it at the main menu and quits, so a script can launch the game the way Gale does (doorstop args pointing at the profile) and read the results without manual steps (~1 min per run; use before commits, not per edit).

### 4. Manual visual checks
Glyph rendering, layout/overlap, and MyLittleUI / AzuExtendedPlayerInventory compatibility. Use the in-game test checklist below and UnityExplorer to inspect anything that looks wrong.

### Not doing
Applying real Harmony patches to Valheim methods inside the test runner: loading Unity types without the native engine is fragile and tests less than the in-game self-test.

### Solution layout
- `BrrControllerGlyphs.Core/` (netstandard2.0, YamlDotNet 16.3.0 NuGet)
- `BrrControllerGlyphs.Core.Tests/` (net10.0, xUnit, references Core)
- `BrrControllerGlyphs.Contract.Tests/` (net10.0, xUnit, MetadataLoadContext; references Core for the mod names it checks)
- `BrrControllerGlyphs/` (plugin; compiles Core sources in; YamlDotNet NuGet with `ExcludeAssets="runtime"` so YamlDotNet.dll is not deployed)
- `dotnet test BrrControllerGlyphs.sln` runs layers 1 and 2. Build through the solution: JotunnLib finds `Environment.props` via `$(SolutionDir)`, so building the plugin project alone loses the BepInEx paths.

## Phases

Each phase follows: failing unit tests → Core code → contract tests for new patch targets → thin patches → in-game self-test checks → manual checklist.

1. **v1:** bump JotunnLib NuGet and manifest dependencies to the installed versions; set up Core + both test projects; `KeyMap`, `GlyphMode`, `BoundKeyResolver` (TDD); F1 + F2 postfix; config; startup gamepad log; `brrcg_selftest`. Verify in game.
2. **v2:** F3 hotbar + AzuExtendedPlayerInventory extra slots, F4 hover rewriter (TDD for `HotbarLabels` and `HintRewriter`).
3. **v3:** F6 targeted visibility patches, starting with the inventory screen (in-game finding 2026-11-02). F5 glyph images (branch `phase-f5-glyphs`, started 2026-11-08).
4. **v4 (branch `phase-4-mapping-setup`, started 2026-11-08):** F9 pieces 1–3 (mapping status, reload, mod shortcut audit) and the settings reorganisation below. F7 has no target (see F7).
5. **v5 (next):** F9 piece 6, the Steam layout VDF audit, then the VDF → YAML generator. **Plugin ID renamed (done 2026-11-08, in v4):** ConfigurationManager's file view lists configs by file name, and ours (`com.angiebrr.controllerglyphfixer.cfg`, `….mapping.yaml`) read oddly next to the others' `Author.ModName` style (`Azumatt.AzuAutoStore.yml`, `aedenthorn.AmmoCount.cfg`). Now `angiebrr.ControllerGlyphFixer` (also the Harmony ID); see the mod rename below. No migration (local mod): the old files are left behind, so the mapping file had to be renamed by hand once. Still to check: why our file showed uncolored where most mods' files are highlighted (Gale shows a "?" on config files it can't tie to a mod). **Mod renamed to BrrControllerGlyphs (2026-11-08, branch `rename-brr-controller-glyphs`):** a "Brr" prefix like Azumatt's "Azu", so the mod and its files sort together in Gale. Plugin ID `angiebrr.BrrControllerGlyphs`; the mapping file is now named after the mod (`BrrControllerGlyphs.mapping.yaml`), not the ID, so it sorts next to the mod's entry; console commands and created objects use the `brrcg_` prefix (`NamePrefix`). Projects, namespaces and folders renamed too. No migration: the old .cfg and mapping file were copied to the new names once, by hand. **Repo cleanup and review (same branch):** removed the stub's unused Unity project, rename and Linux scripts; `OnDestroy` now restores slot labels and removes prefixes (Jotunn can't remove console commands); the `$KEY_` path no longer allocates a delegate and enum names per key. GitHub repo renamed to `valheim-mod-brr-controller-glyphs` (old URL redirects); LICENSE changed from the stub's MIT-0 (Valheim Modding Community) to MIT, Copyright 2026 angiebrr.
6. **Before sharing the mod:** F9 piece 4. Both READMEs written 2026-11-08 (update with each feature).
7. **Later:** F10 mod compat plugin (separate mod): AutomaticFuel smelter hint, chest rename on hold. Settings reorganisation (moved into v4 on 2026-11-08): move the settings into sections (e.g. `General` for the switches, `Hotbar labels` for `HotbarLabels`, `PrefixColors`, `PrefixBackground`) with `ConfigurationManagerAttributes.Order`, so Mod Config reads in a sensible order. Moving an entry to another section resets it to its default once, so do it before others install the mod.

## In-game test checklist (per phase)

- Startup log lists a gamepad device.
- Hover a chest/workbench, press a keyboard-emitted button (e.g. `Q`): prompt stays a controller glyph.
- Inventory: trackpad mouse cursor still visible and usable.
- Keyboard settings screen still shows keyboard keys.
- Set `Enabled = false`: vanilla behaviour returns.
- (v2) Hotbar shows `R4 + …` labels; AzuAutoStore / AzuCraftyBoxes / AutomaticFuel / ImpactfulSkills hover hints show my buttons.

## Open questions

1. ~~Does Steam Input expose a virtual gamepad to Unity's Input System with this layout?~~ **Answered 2026-11-02: yes.** It appears as `Xbox Controller (XInputControllerWindows)`, but connects after plugins load (around character selection). The glyph check reads `Gamepad.current` live, so glyphs turn on once it connects; the plugin logs connects and disconnects.
2. Exact key strings mods write in hover text (`Z` vs `KeyCode.Z` vs localized). Collect from game before finalizing aliases.
3. ~~Does `ZInput.CurrentGlyph` stay correct (Xbox vs PlayStation style) while the game is in keyboard mode?~~ **Answered 2026-11-02: yes** (`Auto` + `XInput` gives xbox glyphs while Valheim's input mode is keyboard).

## Reference: verified game internals (Valheim 1.0, decompiled 2026-11-01)

- `ZInput` (`assembly_utils`): `m_inputSource` flipped by private `OnInput(InputSource, bool)` on every key, mouse move, scroll. `IsGamepadActive()` reads it. Hidden switching modes (`BothGamepadHints` etc.) still switch on keyboard input; hint bits are unused.
- `Localization.Translate` (`assembly_guiutils`): `$KEY_X` → `GetBoundKeyString("JoyX")` if gamepad active and non-empty, else `GetBoundKeyString("X")`.
- `ZInput.GetBoundKeyString`: keyboard bindings return key names (or `s_keyLocalizationMap` tokens); gamepad bindings return sprite tags based on `CurrentGlyph`.
- `HotkeyBar.UpdateIcons`: `binding` label = `""` in gamepad mode, `index + 1` otherwise.
- `Hud.UpdateCrosshair`: `m_hoverName.text = hoverable.GetHoverText()`, with sprite-bracket cleanup only in gamepad mode.
- `ZCursor`: cursor visible only when `IsMouseActive()` and not gamepad-mouse.
