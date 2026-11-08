# Spec: Controller glyphs for a keyboard-emitting Steam Input layout

Status: draft, approved direction, not implemented
Last updated: 2026-11-08
Mod: ControllerGlyphFixer (`com.angiebrr.controllerglyphfixer`), BepInEx + Jotunn, Valheim 1.0

## Problem

My Steam Controller layout sends **real gamepad buttons for core actions** (A/B/X/Y,
bumpers, triggers, sticks, D-pad) and **keyboard keys for extras** (hotbar, mod hotkeys).
Every keyboard key flips Valheim into keyboard/mouse mode, so:

1. Prompts switch from controller glyphs to keyboard keys after any extra-button press.
2. Hotbar/quick-slot labels and mod hover hints show keyboard keys (`1`, `Z`, `LeftShift`)
   instead of the Steam Controller button I actually press.

## Goal

While a gamepad is connected, every button prompt shows what I press on the controller:
Valheim's own glyph for real gamepad buttons, and my Steam binding (e.g. `R4 + D-pad Up`)
for keyboard-emitted keys. Game input behaviour (mouse cursor, run toggle, UI navigation)
stays vanilla.

## Non-goals

- Changing which device the game thinks is active (`ZInput.m_inputSource`). Rejected:
  gamepad mode also hides the mouse cursor (`ZCursor`), changes run toggle
  (`ShouldDefaultToggleRun`), drops some hover lines, and other mods read
  `IsGamepadActive()` for behaviour.
- Rebinding anything. Steam Input stays the source of truth for bindings.
- Supporting other people's layouts in v1 (mapping is personal; data-driven so it could be later).

## Design rules (breakage and compatibility)

1. **Postfixes only in v1.** No prefixes that skip the original (`return false`), no transpilers.
2. **Never mutate `ZInput` state.** `IsGamepadActive()` keeps telling the truth.
3. **Prefer one public choke point** over many private methods.
4. **Each feature has its own config toggle and guarded patch.** Missing target method →
   feature disables itself with a log warning. Postfix bodies are wrapped in try/catch and
   log an error once.
5. **Text rewrites run at low Harmony priority** (`Priority.Low`) so other mods' postfixes
   have already written their text.
6. **Translation logic is a pure function** (no Unity/Valheim types) with xUnit tests outside
   Unity. Patches only call it.

## Shared pieces

### Controller glyphs on (`GlyphService.ShowKeyPrompts`, decided by `GlyphMode.Decide`)
Read live on every prompt. True when all of:
- config `General.Enabled` and `General.KeyPrompts` are true,
- `ZInput.IsGamepadEnabled()` (gamepad not disabled in Valheim settings),
- a gamepad device is present (`UnityEngine.InputSystem.Gamepad.current != null`),
- the Settings screen is not open (`Settings.instance == null`), so the keyboard
  rebinding UI keeps showing real keyboard keys.

Log connected gamepads (name + layout) on startup and whenever one connects or disconnects.
Steam Input's virtual controller connects after plugins load, so the startup line alone
shows none (open question 1, answered).

### Mapping table (YAML)
- File: `<profile>/BepInEx/config/com.angiebrr.controllerglyphfixer.mapping.yaml`
  (next to the `.cfg`).
- **No mapping is built into the mod.** If the file is missing, the plugin writes
  `KeyMapTemplate` (format notes plus commented-out examples; maps nothing). Controller
  glyphs (F1) still work; keyboard-only keys stay as keys until the file is edited.
  Uncommenting an example (deleting its `#`) must be enough to enable it (tested).
- My layout lives in the test fixture `ControllerGlyphFixer.Core.Tests/Fixtures/steam-controller.yaml`
  and, as my real config, in the profile's mapping file. Later the VDF generator produces it.
- Maps a key name to display output: Valheim key names (`Q`, `LeftShift`, `Keypad1`,
  `1`–`8`), plus aliases for spellings mods use (`Alpha1`, `KeypadNumber1`, `Left Shift`).
- Output is a TMP string: a Valheim sprite tag for buttons Valheim has art for
  (A/B/X/Y, D-pad, bumpers), a text badge for buttons it doesn't (`[R4]`, `[L5]`, trackpad).
  Combos join parts: `[R4] + <sprite …dpad_up>`.
- Parsed with YamlDotNet from the shared `ValheimModding-YamlDotNet` package (see
  Dependencies). Parsing lives in the pure library.

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

### Pure library: `ControllerGlyphFixer.Core`
- Separate `netstandard2.0` class library with no Unity/BepInEx references. The plugin
  compiles Core's source files into `ControllerGlyphFixer.dll` (linked `Compile` items), so
  only one DLL is deployed and `publish.ps1` needs no changes. xUnit test project
  `ControllerGlyphFixer.Core.Tests` references the Core project and targets a modern .NET.
- Holds **all decision logic** ("functional core"); Harmony patches are a thin
  "imperative shell" that gathers inputs from the game, calls Core, and applies the result.
- API sketch:
  - `KeyMap.Parse(string yaml) -> KeyMap` (throws `KeyMapException`)
  - `KeyMap.Translate(string keyName, Func<string, string> valheimGlyph) -> string` (null if
    unmapped; case-insensitive, aliases; `valheimGlyph` turns a Valheim control name such as
    `buttonSouth` into a sprite tag, else the button shows as a `[badge]`)
  - `ValheimButtons.TryGetControl` (A/B/X/Y/LB/... → Valheim control names)
  - `SpriteStyle.For(string glyphSetting, string gamepadType)`, `SpriteStyle.Tag(style, sprite)`
    (mirrors ZInput's xbox/ps5/switch2 choice)
  - `GlyphMode.Decide(bool enabled, bool gamepadEnabled, bool gamepadPresent, bool settingsOpen) -> bool`
  - `BoundKeyResolver.Resolve(string name, string originalResult, bool showGlyphs, Func<string, string> lookupJoy, Func<string, string> translateKey) -> string` (F1/F2)
  - `KeyMap.Buttons(string keyName) -> IReadOnlyList<string>`, `KeyMap.Format(buttons, valheimGlyph) -> string`
  - `SlotLabel.Buttons(string mainKey, IReadOnlyList<string> modifiers, Func<string, IReadOnlyList<string>> buttonsFor) -> IReadOnlyList<string>` (F3)
  - `BarLabels.Split(IReadOnlyList<IReadOnlyList<string>> slots) -> BarLabels { Groups, Slots }` (F3)
  - `PrefixColors.For(PrefixColorMode mode, IReadOnlyList<string> prefix, KeyMap map) -> string` (F3);
    `KeyMap.Color(button)`, `KeyMap.PrefixButtons`
  - `SlotLabelRefresh.Decide(SlotBarState? last, SlotBarState current, bool force) -> Skip | Write | Restore` (F3)
  - `AzuEpiQuickSlots` (AzuEPI's plugin ID and quick-slot config names; checked by contract tests)
  - `HintRewriter.Rewrite(string text, Func<string, string> translateKey, Func<string, bool> isKeyName) -> HintRewrite { Text, UnmappedKeys }` (F4; see rules below)
  - `ModGlyphs.TryGetSprite(string normalizedButton, out string sprite)` (F5)
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
AzuAutoStore (`Z` favorite), AzuCraftyBoxes (`LeftShift` fill all), AutomaticFuel (`B` torch
mode), ImpactfulSkills (`N` AOE toggle), Vapok AdventureBackpacks (`I` backpack), PlantEasily,
EpicLoot.

### Mods that may touch the same UI (compat checks)
- **MyLittleUI** (shudnal): changes hover text and item/hotbar UI. Our F3/F4 postfixes run at
  low priority so they see its final text; verify both still look right.
- **AzuExtendedPlayerInventory**: own quick-slot bar and labels (F3 note).
- **TradersExtended**, **EpicLoot**: own UI panels; only affected through `$KEY_` (F1/F2).

## Features

### F1. Controller glyphs in `$KEY_` prompts regardless of last input
- **Patch:** postfix on `ZInput.GetBoundKeyString(string name, bool emptyStringOnMissing)`
  (public instance method, `assembly_utils`). Every `$KEY_` token resolves through it via
  `Localization.Translate` → `Localization.GetBoundKeyString` → this method.
- **Behaviour:** if `ShowKeyPrompts` and `name` does not start with `Joy`, look up
  `"Joy" + name` (with `emptyStringOnMissing: true`, guarded against recursion). If
  non-empty, return it (Valheim's own glyph for the current `ZInput.CurrentGlyph` style).
  Otherwise fall through to F2.
- **Why this works:** vanilla `Translate` only tries `Joy…` when `IsGamepadActive()`; we make
  the keyboard path return the same answer, so the glyph no longer depends on last input.
- **Risk:** low (one public method, return value only).

### F2. Keyboard-only keys shown as my Steam buttons
- **Patch:** same postfix as F1. When there is no `Joy…` binding, translate the returned
  keyboard key string through `KeyMap.TryTranslateKey`. Unmapped keys stay as-is.
- **Note:** `GetBoundKeyString` returns localized display names for some keys
  (`s_keyLocalizationMap`, e.g. `$button_lshift`) before `Localization` resolves them.
  The mapping must match on those tokens too; verify the exact strings in game.
- **Risk:** low.

### F3. Hotbar slot labels
- **Covers:** Valheim's hotbar, AzuExtendedPlayerInventory's quick-slot bar, and in the
  inventory the top row and AzuEPI's quick-slot cells. Config `General.HotbarLabels`; prefix
  colours `General.PrefixColors`.
- **Patches:** postfixes on `HotkeyBar.UpdateIcons(Player)`, `ToggleBindingHint(bool)` and
  `OnEnable()` (all private, `assembly_valheim`). Rewrite each slot's `binding` text; the only
  objects added are the group prefixes and lines under one container per bar (`cgf_PrefixLabels`,
  a child of the bar), rebuilt on each write and removed when glyphs turn off.
- **When:** `SlotLabelRefresh.Decide` (Core). These methods run every frame, so labels are
  written only when the slots were rebuilt (count or first slot object changed), glyphs
  toggled, or Valheim just reset them; when glyphs turn off, Valheim's labels are restored
  once with `ToggleBindingHint` (checked in game 2026-11-07: Settings shows `1`, `2`, ...).
- **Risk:** medium (private but long-stable).
- **Compat:** AzuEPI's quick-slot bar is a second `HotkeyBar`; AzuEPI rebuilds its slots with
  the same count after Settings closes, which is why rebuilds are detected by the first slot
  object, not only the count. AzuEPI keeps shortcuts for 8 quick slots whatever its
  `[3 - Quick Slots] Number of Quick Slots` is; unused ones keep defaults (`Alt + 7`, ...), so
  only the first "Number of Quick Slots" count when finding the shared prefix.
- **Inventory (2026-11-07):** first tried full-size glyphs on the top row and dropped them;
  now smaller glyphs (`<size=70%>`) with the same prefix groups. `InventoryLabelsPatch` postfixes
  `InventoryGui.UpdateInventory(Player)` at low priority, after AzuEPI's postfix there.
  `InventoryLabelWriter` labels the player grid's first row (vanilla writes `1`–`8` once when
  the grid is built, both input modes) and AzuEPI's quick-slot cells, found by the name AzuEPI
  writes on them (`QuickSlotCells.Find`; `AzuEpiQuickSlots.CellName`: its "Hotkey N Display
  Text", or the shortcut when blank) and remembered until the grid is rebuilt (AzuEPI only
  rewrites a cell's text when its name or cell changes). Prefixes sit above each group: a line
  across a row, down a column's left side, or an outline when a group spans rows and columns
  (`GroupShapes`). Glyphs off: top row back to `1`–`8`, quick-slot cells back to AzuEPI's text.
- **In-game finding (2026-11-02):** after v1, hotbar and AzuExtendedPlayerInventory extra-slot
  labels still show keyboard keys (expected: not covered by F1). Wanted: both show the mapped
  buttons (e.g. `[R4] + D-pad up` on slot 1, `[R5] + ...` on extra slots).
- **Limit:** Valheim can't see Steam action-set modifiers (holding R4/R5 switches layers inside
  Steam; the game only sees the key sent by the second button). Labels can always show the
  combo, but can't appear only while R4/R5 is held unless the hold itself sends a key.
- **Found with a `cgf_dumphotbars` dev command (2026-11-02; removed once F3 settled):** labels are written in two places, and both
  apply to AzuEPI's quick-slot bar too (it's a second `HotkeyBar` named `AzuEPI_QuickAccessBar`):
  `UpdateIcons` when slots are created (AzuEPI sets its "Display Text" there), and
  `HotkeyBar.ToggleBindingHint`, which Valheim calls on every keyboard/gamepad switch and which
  overwrites every label with `1`.. or blank. That's why AzuEPI's names (`R5 Up`) disappear.
  Mods patching `HotkeyBar.UpdateIcons`: MyLittleUI (prefix 800, 2 postfixes), AzuEPI (prefix
  and postfix, priority 0), EpicLoot (transpiler); none of their postfixes write labels.
- **Implemented:** `SlotLabel.Buttons` (Core, tested) finds a slot's buttons from its keyboard
  shortcut (modifiers + main key) through the mapping; null leaves the slot alone (no key,
  unmapped). `BarLabels.Split` (Core, tested) groups runs of neighbouring slots that start with
  the same button (the R4/R5 hold) and moves their shared leading buttons into the group;
  unmapped slots split groups, one-button slots aren't grouped, every slot keeps a button.
  `HotbarLabelsPatch` postfixes `UpdateIcons`, `ToggleBindingHint` and
  `OnEnable` and calls `HotbarLabelWriter`, which only rewrites when slots were rebuilt, glyphs
  toggled, or Valheim just reset the labels, and restores Valheim's labels when glyphs turn off.
  Keys: Valheim's bar asks the original `GetBoundKeyString("HotbarN")` through a Harmony reverse
  patch (`OriginalBoundKeyString`, raw key, respects rebinding); AzuEPI's bar reads AzuEPI's
  `[8 - Quick Slot Hotkeys] Hotkey N` config at runtime (`QuickSlotKeys`, no compile reference).
  Config `General.HotbarLabels`. AzuEPI's own display names are ignored.
- **Size / presentation (in-game look 2026-11-07):** the full `[R4] + (glyph)` label wrapped a
  character per line in the hotbar's one-digit label box (`[` and `R` above the slot) and put
  the glyph over the item icon on AzuEPI's bar. Each slot now shows only its own button (one
  glyph, like vanilla's digit); a prefix right of the last slot looked good (2026-11-07).
- **Groups (2026-11-07):** each prefix group gets its prefix at its left end with a thin line
  to its last slot (`PrefixLabels`): above the hotbar (8 UI units of space; 2 looked cramped),
  below AzuEPI's bar (the hotbar is right above it). The bars start ~20 px from the screen
  edge, so nothing fits directly to their left. Mixed prefix buttons in one bar give one
  prefix per run.
- **Naming:** "prefix", not "hold": the shared leading buttons are an action-set hold in my
  layout, but Steam Input also has chords and mode shifts, so the mod doesn't assume how
  they're pressed. A *prefix group* is a run of slots; its *prefix* is the label beside it.
  ("Badge" still means the `[R4]` text shown for buttons Valheim has no art for.)
- **Colours:** `General.PrefixColors` = `Neutral` (default; slot-label colour), `Palette`
  (built-in colour per prefix button, numbered in prefix-button name order) or `Custom` (mapping
  file `colors:` section, `"#RRGGBB"`/`"#RRGGBBAA"`; unlisted buttons stay neutral). Keyed by
  the group's first prefix button, so R4 looks the same everywhere. Changing the setting
  rewrites the bars (`HotbarLabelWriter.Invalidate`).
- **Backings (2026-11-07):** the inventory panel's gamepad highlight ran through `[R4]`. Each
  prefix now sits on a rounded, translucent backing (`General.PrefixBackground`, default black
  at 70% (was 55%), alpha 0 hides it; changes redraw live). For a row it spans the prefix and its line;
  for a column or outlined group, the prefix only. Corners: a 7-unit radius from a small sprite
  built once at startup (`RoundedSprite`, pixels from `RoundedCorners.Coverage` in Core) and
  stretched with fixed corners; Unity has no rounded shape mods can use at runtime. The
  texture and sprite are marked `DontUnloadUnusedAsset`: in game the corners first showed
  square, most likely because Valheim's unused-asset sweep on world load freed the texture made
  at the main menu. The self-test checks the sprite is still loaded.
- **Attached look (2026-11-07):** with backings, the hotbar's prefix sits 2 units above the
  slots (like the quick-slot bar's below), so the backing meets the bar.
- **Alignment (2026-11-07):** backings started a few units right of the first slot: the slot's
  layout box differs from what's drawn. Bounds now come from each slot's visible background
  (the largest active image on the slot or its direct children, ignoring the selected,
  equipped and queued overlays), measured in world space.
- **Inventory spacing (2026-11-07):** tried 24 units above the slots to clear the panel border:
  far too high, and it showed the HUD hotbar's own prefix peeking out from under the inventory
  panel. Back to the attached 2 units, with the default backing darker (70%) to read over the
  highlight. Revisit once F5's glyph images replace the `[R4]` text.
- **Edges flush (2026-11-07):** the backing's left edge sits on the first slot's and, for rows,
  its right edge on the last slot's; padding goes inside, around the text and line. Outside
  padding showed on one side only wherever the measured and drawn edges differed slightly.
- **Inventory follow-ups:** check the 70% size, the prefix positions over the panel edge, and
  whether the grid clips anything drawn above its first row. **Checked with F5's glyphs
  (2026-11-08):** the inventory looked good; nothing clipped.
- **Bug (found 2026-11-08, for later): AzuEPI's other inventory layouts.** Everything above was
  built and checked in AzuEPI's *legacy* layout. In its "equipment slots and quick slots" layout,
  AzuEPI moves the quick-slot cells into their own panel under the equipment: those cells keep
  AzuEPI's own names (`R5 Up`, `R5 Right`, ...) instead of our glyphs, and our R5 outline is drawn
  around an empty column at the right of the equipment panel, where the cells were in legacy mode.
  To check: which layout setting AzuEPI uses and its values; how `QuickSlotCells.Find` and the
  group bounds pick the cells' parent and positions when the cells live in a separate panel; and
  each layout's look (outline vs a row prefix). Add the setting's name to `AzuEpiQuickSlots` with
  a contract test.
- **Later idea:** highlight the bar's slots while R4/R5 is held. Needs the Steam layout to also
  send a spare key while the action set is active (e.g. F13 on layer activation).

### F4. Hover text with key names written directly by mods
- **Where:** Valheim rebuilds the hover text every frame (`hoverable.GetHoverText()`, which
  mods extend with postfixes), then in gamepad mode only strips `[<color=yellow><b>` /
  `</b></color>]` around a single `<sprite=…>`, and assigns it to `Hud.m_hoverName.text` in
  `Hud.UpdateCrosshair(Player, float)` (private, `assembly_valheim`). One postfix there covers
  vanilla and every mod.
- **What mods write** (decompiled 2026-11-08):

  | Mod | Hint | Written as |
  |---|---|---|
  | AzuCraftyBoxes chest toggle | `[E + LeftShift]` | `KeyboardShortcut.ToString()` |
  | AzuCraftyBoxes fill all | `[LeftShift + $KEY_Use]` | shortcut text + `$KEY_Use` |
  | AutomaticFuel | `[B] AutoFuel: …` | shortcut text (`{togglekey}` placeholder) |
  | AzuAutoStore backpack | `Press [$KEY_Use] to open` | `$KEY_Use` (F1 already covers it) |

- **Keys, not actions (decided 2026-11-08):** the mapping file maps keyboard keys to the buttons
  Steam presses to send them; it knows nothing about actions. Valheim actions (`$KEY_Use`) are
  F1's job: they resolve through Valheim's bindings (Joy glyph, else the bound key through the
  mapping). Raw key names in mod text have lost their action, so they're translated only
  through the mapping. The spec's earlier idea (E -> Use -> X) is dropped: AzuCraftyBoxes reads
  its toggle with `Input.GetKey` (real keyboard keys only) and has no controller path, so
  showing X would be wrong. If a key really is pressed with a button for some mod, the user
  says so in the mapping (`E: X`); the mod never guesses.
- **`HintRewriter.Rewrite` (Core, TDD):**
  - Only looks inside `[ … ]` hints (outermost brackets; a nested `[R4]` badge counts as a
    glyph). Inside, TMP tags (`<color>`, `<b>`, `<sprite>`) are kept; the text between tags is
    split on `+`, and each trimmed piece is a token.
  - Token kinds: mapped key (replaced with its buttons), unmapped key name (left as is,
    reported), glyph (sprite tag or nested badge), other text.
  - Any "other" token means it isn't a key hint: the whole `[ … ]` is left untouched
    (`[Zebra]`, `[1/5]`, free text). Free text outside brackets is never touched.
  - When every token is a mapped key or a glyph, the outer `[ ]` are dropped, like vanilla in
    gamepad mode (also makes keyboard mode match gamepad mode for vanilla hints and avoids
    `[[L5]]`). Partly translated hints keep their brackets.
  - Idempotent: rewriting its own output changes nothing.
  - **Leftover repair (2026-11-08):** an unclosed `[` whose opening `<color=…>`/`<b>` tags run
    straight into a `<sprite=` is removed with those tags. That's what Valheim's gamepad cleanup
    leaves of a hint coloured anything but `yellow` (it strips `"></b></color>]` but only matches
    `[<color=yellow><b><sprite=`). Other unclosed brackets stay. TODO in code: report it to
    MyLittleUI's author; once fixed upstream the repair finds nothing to do.
  - Whether a piece is a key name is a predicate from the plugin (Unity `KeyCode` names, any
    case), so Core stays free of Unity.
- **Patch:** postfix on `Hud.UpdateCrosshair`, low priority; runs only when `ShowKeyPrompts` and
  config `General.HoverText` are on. Caches the last input and output, so an unchanged hover
  costs one string compare per frame. Known cost: while a hint is rewritten, Valheim sets the
  original text every frame and we set ours back, so TMP re-lays out that one label each frame.
  Avoiding it would need a prefix or transpiler, which the design rules rule out; it's small.
- **Missing keys (2026-11-08):** each unmapped key name the rewriter meets is logged once per
  session ("Hover hint uses key E, which isn't in your mapping file: no controller button
  shows for it"), and `cgf_selftest` lists the ones seen so far. Later, F9's mapping status in
  Mod Config can show them too, so users find gaps in their mapping without reading code.
- **Expected results with my mapping:** `[B] AutoFuel: …` -> `[LPad Up] AutoFuel: …`;
  `[LeftShift + (Use)]` -> `[LPad Down] + (Use glyph)`; `[E + LeftShift]` -> `[E + [LPad Down]]`
  with `E` reported (no button in my layout sends E).
- **In-game check (2026-11-08):** AutomaticFuel's `[LPad Up] AutoFuel` and AzuCraftyBoxes'
  `[E + [LPad Down]]` show as designed; vanilla chest hints match in both modes in keyboard
  mode. Two problems come from other mods, not ours:
  - **Smelter:** no AutomaticFuel line. MyLittleUI's postfix on `Smelter.OnHoverAddFuel` /
    `OnHoverAddOre` replaces the whole `__result`, dropping AutomaticFuel's appended line.
  - **Chest in gamepad mode:** a stray `[` and yellow text to the end of the line. MyLittleUI
    writes chest hints as `[<color=#ffff00ff><b>…`; vanilla's gamepad cleanup only matches
    `<color=yellow>`, so it removes the closing `</b></color>]` but not the opening. The hint
    has no closing bracket left, so the bracket rewrite can't see it; the leftover repair above
    fixes it (added after this check).
  - Missing-key reports first named the wrong line (substring match: "Chest ( EMPTY )" contains
    `E`); `UnmappedKey.Line` now carries the hint's own line, tags removed.
- **Out of scope:** ImpactfulSkills' crafting-panel hint (`m_recipeDecription`, not hover text);
  `<b>KEY</b>` hints without brackets (none of my mods use them).
- **Risk:** medium (one private method; mod-agnostic). Contract test for the method and
  `Hud.m_hoverName : TMP_Text`.

### F5. Glyph appearance
Designed 2026-11-08. Buttons Valheim has no art for (R4/R5, L4/L5, trackpads) show as glyph
images instead of `[R4]`-style text badges. No patch: it changes what the mapping produces.

- **Art (decided 2026-11-08):** Kenney's *Input Prompts* 1.5A, Steam Deck set (CC0;
  https://kenney.nl/assets/input-prompts). It has every Deck button, both trackpads in every
  direction, and a plain and an `_outline` variant per glyph. Rejected:
  - Steam's own glyphs (`<Steam>\controller_base\images\api\{knockout,dark,light}`, `sd_*` /
    `sc_*`): no open licence found, so they could only be read from the user's Steam install at
    runtime, never shipped.
  - The CC0 OpenGameArt Steam Controller pack (2016): original Steam Controller only, no back
    buttons.
- **Set: Steam Deck** (chosen over Steam's `sc_` set: square back buttons, separate left/right
  trackpad arrows).
- **Look: dark fill, light rim, light letters, soft shadow** (chosen from mockups 2026-11-08).
  Valheim's glyphs are dark; dark alone vanished on the HUD, and white looked foreign. Made from
  Kenney's art: the plain glyph (letters cut out) plus its `_outline` variant (rim + letters) is
  the solid shape; fill that dark, draw the outline variant over it in light, add a shadow.
- **Build-time script:** `scripts/make-glyphs.ps1` reads the unpacked Kenney zip (path as a
  parameter; the zip isn't committed, the script says where to download it), recolours the
  128 px `Double` PNGs, pads them for the shadow and writes `ControllerGlyphFixer/Glyphs/*.png`.
  The outputs are committed and embedded in the DLL as resources, with Kenney's `License.txt`
  beside them and a credit in the README. The whole Deck set is made (A/B/X/Y, D-pad, bumpers,
  triggers, sticks, R4/R5/L4/L5, both trackpads and their directions; ~40 images), so
  `ModGlyphsForAll` needs no new art.
- **Which art wins (decided 2026-11-08):** config `General.ButtonGlyphs`:
  - `ValheimFirst` (default): Valheim's sprite where it has one (A/B/X/Y, D-pad, bumpers;
    follows Valheim's Xbox/PlayStation glyph setting), else ours, else the `[badge]`.
  - `ModGlyphsForAll`: ours for every button in mapped output (hotbar labels, prefixes, hover
    hints), Valheim's for none, so labels look uniform. Valheim's own `$KEY_` prompts (F1) keep
    Valheim's art either way. Built in this phase: it's only the lookup order in `KeyMap.Format`.
  - The `[badge]` (with the `labels:` section's text) stays the last fallback in both.
- **Core (TDD):** `ModGlyphs.TryGetSprite(normalizedButton, out sprite)`, a table like
  `ValheimButtons`: `R4` → `r4`, `LPadUp` → `lpad_up`, `LPad` (click) → `lpad`, plus aliases
  (`L1`/`LB`, `L2`/`LT`, `View`/`Back`, `Options`/`Start`, ...). `KeyMap.Format` takes a second
  lookup for our sprites and the `ButtonGlyphs` order; tests cover both orders and the fallback.
  A test checks every sprite name in the table has an embedded PNG (reads the plugin's
  `Glyphs/` folder).
- **Sprite set (plugin, `ModSpriteAsset`):** at startup, load the embedded PNGs, pack them into
  one texture (`Texture2D.PackTextures`) and build a `TMP_SpriteAsset` named `cgf_deck` with
  TMP's sprite shader (a glyph and a character per image, lookup tables updated). Register it with
  `MaterialReferenceManager.AddSpriteAsset(hash, asset)` so `<sprite="cgf_deck" name="r4">`
  finds it: TMP checks that table before `Resources.Load` (decompiled `TMP_Text` 2026-11-08).
  - Size and baseline copied from Valheim's `xbox` sprite asset (its `button_a` glyph metrics,
    scaled to our image size), so our glyphs line up with Valheim's in the same label.
  - Texture, material and asset marked `DontUnloadUnusedAsset` (Valheim's unused-asset sweep;
    see F3's backing sprite) and destroyed in `OnDestroy`.
  - TMP has no way to unregister, so after `OnDestroy` the lookup keeps a dead entry. Plugins are
    destroyed only when the game quits, so this is accepted, not worked around.
  - If the set can't be built, the plugin logs once and labels fall back to `[badge]` text.
- **Effects on F3/F4:**
  - Prefixes show a glyph instead of `[R4]`. `PrefixColors` keeps colouring the prefix's line and
    text; the glyph itself is not tinted (TMP sprites ignore the text colour unless `tint=1`).
  - Recheck in game what F3 postponed: inventory prefix spacing over the panel edge, the 70% label
    size, and the backing width now that the prefix is one glyph.
  - `HintRewriter` already treats `<sprite…>` as a glyph: `[LPad Up] AutoFuel` becomes the glyph
    without brackets. Add a test with our sprite tag.
- **Self-test:** the sprite set is registered and its texture still loaded; formatting `R4` gives
  our sprite tag, and a TMP text with it has a sprite character in its text info (not the letters
  `[R4]`); dump Valheim's `xbox` sprite sheet to a PNG next to `LogOutput.log` (INFO) to compare
  the looks.
- **Contract tests:** `MaterialReferenceManager.AddSpriteAsset(int, TMP_SpriteAsset)`,
  `TMP_SpriteAsset.spriteGlyphTable` / `spriteCharacterTable` / `UpdateLookupTables()`, and
  `TMP_SpriteGlyph` / `TMP_SpriteCharacter` constructors we use (`Unity.TextMeshPro`).
- **Out of scope:** colouring glyphs per prefix; other controllers' sets (Xbox/PlayStation art
  for `ModGlyphsForAll` users on other pads); a setting for the rim and shadow colours.
- **In game (2026-11-08), tuned over five rounds of screenshots:**
  - Valheim's `xbox` sprite asset has no point size (`button_a`: height 32, bearing 20, scale 2.5,
    point size 0). TMP then fits every sprite's own height to the font ascent, so scaling by
    Valheim's pixel height made ours tiny. `SpriteFit` (Core, tested) handles both TMP paths.
  - Valheim's sprites carry transparent padding, so our 128 px glyph at its full size looked about
    1.4 times Valheim's round glyphs. Glyphs are drawn at 0.7 of Valheim's sprite, centred on it.
  - Hover-hint lines are packed tightly (Valheim's own glyphs nearly touch), and the trackpad-up
    arrow was squeezed under the glyph on the line above: hover hints use their own translator
    with the glyph in `<size=75%>` (85% was still crowded). `$KEY_` prompts keep the full size.
  - Prefix glyphs hang below the text line and ran into the slots: prefixes are now placed by
    TMP's drawn bounds (`PrefixLabels.PlaceByDrawnBounds`), not the line box.
  - Kenney's 6 px rim looked thick next to Valheim's thin one: the script keeps the outer 3 px.
    The thin rim made the trackpad arrows dark and invisible at hint size: small separate shapes
    are drawn fully light.
  - Valheim's own glyphs are dark with a light rim and a soft shadow (its sheet, dumped by the
    self-test), so the chosen look matches.
  - Group alignment looked slightly off from a distance in full screen; up close it's the selected
    slot's white frame, which is larger than the slot. Left as is.
  - The self-test's text-layout check threw (a bare `TextMeshProUGUI` has no font or canvas); it now
    checks TMP's two lookups for the tag instead.
  - Build notes: `UnityEngine.ImageConversionModule` targets netstandard 2.1, which the net48
    plugin can't reference (CS1705), so `PngFiles` calls `LoadImage`/`EncodeToPNG` by reflection
    (contract-tested); `UnityEngine.TextCoreFontEngineModule` is referenced directly for glyph metrics.
- **Later (asked 2026-11-08): `ModGlyphsForAll` everywhere.** Today it only changes text the mod
  writes (slot labels, prefixes, mapped keys in hover hints), as designed above; Valheim's own
  prompts keep Valheim's art, so a hint can mix both styles ("Use Wood" with Valheim's X next to
  our trackpad glyph). The user expects the setting to switch every glyph. Needs: F1's `$KEY_`
  results mapped from Valheim's sprite names (`button_x`, `dpad_up`, ...) to ours; hint panels
  and `UIInputHint` text the same way; image badges (`UIGamePad`) can't take a sprite tag, so
  those need our sprites as `Image` sprites. Design it before building (look per place, what
  happens for buttons we have no glyph for, e.g. sticks).

### F6. Hint panels and menu-button badges
- Bottom-right `KeyHints` panels, build-menu hints (`m_buildMenuHintsGP` / `KB`),
  `UIInputHint` gamepad/keyboard variants, `UIGamePad` button badges.
- For anything still wrong after F1, find the component with UnityExplorer and add a narrow
  visibility postfix (e.g. on `UIInputHint.UpdateInputHints`).
- **In-game finding (2026-11-02):** F1 keeps world/hover prompts as controller glyphs even
  with trackpad (mouse) input. **The inventory screen does not:** keyboard-mapped buttons or
  trackpad cursor movement switch its hints to keyboard. Those hints are object visibility
  (gamepad vs keyboard variants), not `$KEY_` text, so F6 is needed for the inventory. Next
  step: identify the inventory hint objects/components with UnityExplorer.
- **Constraint:** the trackpad cursor must keep working in the inventory (don't force
  gamepad mode; only show the gamepad hint variants).
- **Bug found 2026-11-02:** after keyboard input, the combat hint bar (Attack/Block/Dodge)
  showed controller glyphs on keyboard key-cap boxes: `UIInputHint` picked its keyboard version
  and F1 put glyphs in it.
- **Implemented (option A, chosen over a transpiler or faking `IsGamepadActive`):** postfix
  on `UIInputHint.UpdateInputHints` re-picks the version with Core `HintVisibility.Decide`
  (gamepad version while glyphs are on; vanilla when off; vanilla when the hint has no gamepad
  version). Copies vanilla's visibility condition (FIXME). Covered by the `KeyPrompts` toggle.
  If the inventory also needs `UIGamePad` badges, reconsider a shared approach (the
  transpiler or scoped `IsGamepadActive` options) instead of copying more conditions.
- **Verified in game (2026-11-02):** combat bar and inventory bottom hint bar stay on controller
  glyphs after keyboard/trackpad input, no key-cap boxes; trackpad cursor still works.
  **Still switching:** the `UIGamePad` badges on inventory buttons (e.g. `Y`, `LSt`) disappear
  after trackpad input. Next F6 target.

### F7. Jotunn KeyHintManager hints (deferred)
- Tool/building hints registered by mods through Jotunn. First check whether those mods
  declare gamepad buttons in their `ButtonConfig`; otherwise postfix Jotunn's hint update.

### F8. Config and mapping generation
- BepInEx config, `[General]` section:
  - `Enabled` (bool, default true): master switch.
  - `KeyPrompts` (F1+F2), `HotbarLabels` (F3), `HoverText` (F4) (bool, default true each).
  - `PrefixColors` (`Neutral` / `Palette` / `Custom`, default `Neutral`) (F3).
  - `PrefixBackground` (colour with alpha, default black at 70%) (F3).
  - `ButtonGlyphs` (`ValheimFirst` / `ModGlyphsForAll`, default `ValheimFirst`) (F5).
- Later: a script that generates the YAML mapping from my Steam layout VDF.

### F9. Helping other users set up their mapping
Written 2026-11-07, before any of it is built. Today the mapping is a YAML file only:
`BepInEx/config/com.angiebrr.controllerglyphfixer.mapping.yaml`, written from
`KeyMapTemplate` on first run with every example commented out, read once at startup. A new
user has to find the file, learn the format, edit it in a text editor and restart the game.

**What can't move into the game.** The *button* half of each entry can't be detected: Steam
Input turns the button press into a key before Valheim sees anything, and action-set holds,
chords and mode shifts are invisible to the game. The game only ever sees the resulting key.
So users must write the buttons themselves, reading them from their own Steam layout (Steam's
controller configurator, outside the game; or Big Picture's overlay while in game). The spec
and the mod's README must say this plainly. The *key* half can be confirmed in game (below).

**Settings UIs in my profile** (both read the BepInEx config):
- Jotunn's *Mod Settings* (main menu and pause menu): lists config entries. To check: which
  `ConfigurationManagerAttributes` it honours (probably `Order`, `ReadOnly`, `Browsable`; not
  `CustomDrawer`).
- shudnal's ConfigurationManager 1.1.27 (`F1`, or the pause menu's *Mod Config* button): honours
  `ConfigurationManagerAttributes` including `CustomDrawer`, `ReadOnly`, `HideDefaultButton`
  (seen in its DLL), and has an in-game **File Editor** for `.cfg`, `.yaml` and `.json` files
  (its changelog). So users with it can edit the mapping file without leaving the game.

**Proposed pieces** (smallest first; each optional):
1. **Mapping status in the config** (`[Mapping]` section, read-only): the file's path, and
   "Loaded 20 keys" or the parse error with its line number. Shows in both settings UIs, so a
   broken file isn't silent. (Read-only entries are still written to the `.cfg`; acceptable, or
   use a `CustomDrawer` label that isn't persisted, ConfigurationManager only.) Also list the
   **missing keys**: key names seen in hover hints this session that the mapping doesn't
   cover (F4), e.g. "E (AzuCraftyBoxes chest toggle)", so users know what to add.
2. **Reload without restarting** (the existing TODO in `ControllerGlyphFixer.LoadKeyMap`): watch
   the file (`FileSystemWatcher`), plus a *Reload mapping* button (`CustomDrawer`) and a
   `cgf_reloadmapping` console command. On reload: swap the `KeyMap` in `GlyphService`, clear
   Localization's cache, call `HotbarLabelWriter.Invalidate()`. With ConfigurationManager's File
   Editor this gives a full in-game loop: edit, save, see the labels change.
3. **Key finder** (`cgf_keylog` console command, toggles on/off): logs each keyboard key the game
   receives, by the name the mapping file uses (e.g. `Keypad1`, `LeftShift`), and whether the
   mapping has it. Press a button combo on the controller, read which key Steam sent, write
   `Keypad1: [R5, DPadUp]`. Answers "what key does this button send?" in game.
4. **Starter mappings:** a few commented example blocks in the template (Steam Controller back
   buttons + action set, Steam Deck back grips, a DualSense touchpad layout) so users copy the
   closest one instead of starting from nothing.
5. **Open file / write template buttons** (`CustomDrawer`, ConfigurationManager only): *Open
   mapping file* (opens it in the system's default editor, outside the game) and *Reset to
   template* (with confirmation; backs up the old file first).
6. **Later:** generate the mapping from the Steam layout VDF (see above), so most users never
   type buttons at all. Needs research: where Steam stores the active layout for Valheim
   (app 892970) and how action sets, chords and mode shifts appear in it.

**User-facing doc** (README / Thunderstore page): what the mod does; "the mapping lives
outside your Steam layout and must match it"; the four setup steps (open the file, find each
key with `cgf_keylog`, write the buttons, save and reload); the `labels`/`colors`/`aliases`
sections; and the limits (only keys Steam sends are seen; holds and chords aren't).

### F10. Mod compat plugin (later, separate mod)
A small separate BepInEx plugin for conflicts between other mods in my profile. Not part of
ControllerGlyphFixer, whose rules forbid changing other mods' patches or behaviour.

- **Structure:** one class per fix, each with:
  - a config switch (`[Fixes] AutomaticFuelSmelterHint`, `[Fixes] ChestRenameOnHold`, …), on by
    default, so any fix can be turned off on its own;
  - a list of required plugin IDs (and the member names it touches), checked through
    `Chainloader.PluginInfos` after all plugins have loaded. If one is missing, or a member was
    renamed, the fix logs why it's skipped and does nothing;
  - a startup log line per fix: applied, skipped (switched off), or skipped (requirement missing);
  - contract tests that read the target mods' DLLs in the Gale profile (as for AzuEPI), skipping
    when a mod isn't installed.
- **Fix 1, AutomaticFuel smelter hint** (needs `TastyChickenLegs.AutomaticFuel`,
  `shudnal.MyLittleUI`): MyLittleUI's postfixes on `Smelter.OnHoverAddFuel` / `OnHoverAddOre`
  replace the whole `__result`; when they run after AutomaticFuel's, its "[B] AutoFuel …" line is
  lost (fuelling still works). The fix finds AutomaticFuel's two postfixes (classes
  `Smelter_GetHoverText_Patch` / `Smelter_GetHoverTextFuel_Patch`) via `Harmony.GetPatchInfo`,
  unpatches them, and patches them again with `Priority.Last`, so they append to MyLittleUI's
  text. Alternatives: turn off MyLittleUI's `hoverSmelterShowFuelAndItem` and
  `hoverSmelterEstimatedTime`; or ask AutomaticFuel's author to add
  `[HarmonyAfter("shudnal.MyLittleUI")]` (no source repo or licence found).
- **Fix 2, chest rename on a held alternate Use** (needs `shudnal.MyLittleUI` with
  *Hover - Chests → Enable custom names* on, `Azumatt.AzuCraftyBoxes`):
  - **Conflict (found 2026-11-08):** both mods use a tap of Valheim's alternate Use on chests
    (LT + X; Shift + E on keyboard). AzuCraftyBoxes' `Container.Interact` prefix runs first,
    toggles the crafting chest and cancels; MyLittleUI's rename prefix (priority 0) then sees the
    interaction cancelled and does nothing. So Rename never opens on chests AzuCraftyBoxes
    manages, while its hint still shows. MyLittleUI's rename has only an on/off setting, and both
    read the same game input, so neither settings nor the Steam layout can separate them.
  - **Fix:** a tap keeps the crafting toggle; holding the alternate Use opens MyLittleUI's rename
    (vanilla `Container.Interact` ignores held presses and both mods skip them, so the input is
    free). Its rename hint reads "hold". First check that "Hold to stack" doesn't also fire
    while the alternate key is held.
  - **Current workaround (2026-11-08):** custom names turned off in MyLittleUI.
- **MyLittleUI audit (2026-11-08):** it fully replaces hover text for chests, smelters (add fuel,
  add ore, empty), cooking stations and pickables (name only), so other mods' lines there can be
  lost; it only appends for fermenters, plants, beehives, tameables, characters and eggs. Only its
  chest hints use a non-`yellow` colour (`#ffff00ff`).

## Testing strategy

Develop test-first. Valheim code needs Unity's native engine, so it can't run in a test
runner; keep logic in Core and patches thin. Four layers:

### 1. Unit tests (`ControllerGlyphFixer.Core.Tests`, xUnit, .NET 10) — TDD, seconds
Red → green → refactor for every Core function. Required cases:
- `KeyMap`: every row of the fixture mapping (tests supply the mapping; Core has none);
  aliases (`Alpha1`, `KeypadNumber1`, `Left Shift`, `$button_lshift`); case-insensitivity;
  unknown keys pass through; combos; invalid YAML gives a clear error; the template maps
  nothing, and uncommenting its examples yields a valid map; empty sections are allowed;
  keys written in the file in any spelling (`Numpad1`, `Alpha1`) match every spelling; two
  entries for the same key (`Q` and `q`, `"1"` and `Alpha1`) are an error naming both lines.
- `GlyphMode.Decide`: truth table of the four inputs.
- `BoundKeyResolver.Resolve`:
  - `("Use", "E", showGlyphs: true, joy → A sprite)` → A sprite.
  - `("Hotbar1", "1", showGlyphs: true, joy → "")` → mapped `R4 + D-pad Up`.
  - `showGlyphs: false` → original result unchanged.
  - `name` starting with `Joy` → never looks up `JoyJoy…` (recursion guard).
  - unmapped key with no Joy binding → original result.
- `SlotLabel.Buttons`: hotbar and quick-slot keys give the mapped buttons; modifiers first
  (unmapped ones keep their name); null modifiers mean none; unmapped main key or no key →
  null (leave the label).
- `BarLabels.Split`: one prefix button → one group; mixed → a group per run; a returning prefix
  button starts a new group; a lone slot still gets a group; unmapped slots split groups;
  one-button slots aren't grouped; multi-button prefixes; names match like the mapping file;
  no mapped slots.
- `PrefixColors.For`: neutral → none; palette in prefix-button name order, unknown buttons none,
  wraps around; custom from `colors:`, unlisted none; multi-button prefixes use the first.
- `KeyMap` colors section: values matched like button names; invalid colours name the line;
  `PrefixButtons` sorted and distinct.
- `KeyMap.Buttons` / `KeyMap.Format`: raw button names; formatting matches `Translate`.
- `SlotLabelRefresh.Decide`: first sight, unchanged (skip), forced, slots rebuilt with the
  same count (new first-slot ID), count changed, glyphs on/off (restore once, never again).
- `ValheimButtons`: every button with Valheim art; Steam-only buttons have none.
- `HintRewriter.Rewrite`: `[B]`, `[<color=yellow><b>B</b></color>]` and combos (`[LeftShift + sprite]`)
  translated, brackets dropped when fully translated; partly translated hints keep brackets
  and report unmapped keys; non-key brackets (`[Zebra]`, `[1/5]`) and free text untouched;
  nested badges count as glyphs; idempotent.
- `ModGlyphs` / `KeyMap.Format` (F5): every Deck button and alias; `ValheimFirst` and
  `ModGlyphsForAll` orders; badge fallback when neither has art; every sprite name has a PNG.

### 2. Contract tests (`ControllerGlyphFixer.Contract.Tests`) — catch game updates, seconds
Inspect the real game DLLs **read-only** with `System.Reflection.MetadataLoadContext`
(no game code runs). Paths come from `Environment.props` (`VALHEIM_INSTALL`); tests skip
when the game isn't installed. Assert each patch target and touched member exists with the
expected signature:
- `ZInput.GetBoundKeyString(string, bool) : string`, `ZInput.instance`, `IsGamepadEnabled()`,
  `IsGamepadActive()`, `IsMouseActive()`, `CurrentGlyph`, `ConnectedGamepadType`,
  `s_gamepadSpriteMap`, glyph enum names (assembly_utils)
- `UIInputHint` members used by the F6 patch
- `HotkeyBar.UpdateIcons(Player)`, `ToggleBindingHint(bool)`, `OnEnable()`, `m_elements`,
  `ElementData.m_go` (F3)
- `Settings.instance`, `Localization.instance.Localize(string)`
- To do with F4: `Hud.UpdateCrosshair(Player, float)`, `Hud.m_hoverName : TMP_Text`
- F5 (`Unity.TextMeshPro`): `MaterialReferenceManager.AddSpriteAsset(int, TMP_SpriteAsset)` and
  the `TMP_SpriteAsset` / `TMP_SpriteGlyph` / `TMP_SpriteCharacter` members we set

Other mods' names we copy are checked against their DLLs in the Gale profile
(`GALE_PROFILE`), by reading the string literals in their code; tests skip when the mod isn't
installed. AzuExtendedPlayerInventory: plugin ID, `8 - Quick Slot Hotkeys`, `Hotkey N`,
`3 - Quick Slots`, `Number of Quick Slots`.
Run after every Valheim or mod update before launching the game.

### 3. In-game self-test (Jotunn console command `cgf_selftest`) — real game
Registered by the plugin; logs `PASS`/`FAIL` per check with a `[cgf-selftest]` prefix:
- each of our Harmony patches is applied (`Harmony.GetPatchInfo`);
- `ZInput.IsGamepadActive()` still reports the real input mode (behaviour untouched);
- a gamepad device is present (open question 1);
- `Localization.instance.Localize("$KEY_Use")` returns a controller sprite while in
  keyboard mode; `"$KEY_Hotbar1"` returns the mapped string;
- F3: the raw `Hotbar1` key can be read (reverse patch); AzuEPI's quick slot 1 shortcut can
  be read (if installed); slot 1 of the hotbar and the quick-slot bar shows its own button,
  and each bar shows one prefix per group of slots on screen
  (in a world).
- F4: the `Hud.UpdateCrosshair` postfix is applied; a sample hint loses its brackets around a
  glyph while `[Zebra]` stays; the keys hover hints used this session that the mapping lacks
  are listed (INFO).
Semi-automated: run `cgf_selftest` in the F5 console, then grep `LogOutput.log`.
Later option: a launch flag (`-cgf-selftest`) runs it at the main menu and quits, so a
script can launch the game the way Gale does (doorstop args pointing at the profile) and
read the results without manual steps (~1 min per run; use before commits, not per edit).

### 4. Manual visual checks
Glyph rendering, layout/overlap, and MyLittleUI / AzuExtendedPlayerInventory compatibility.
Use the in-game test checklist below and UnityExplorer to inspect anything that looks wrong.

### Not doing
Applying real Harmony patches to Valheim methods inside the test runner: loading Unity
types without the native engine is fragile and tests less than the in-game self-test.

### Solution layout
- `ControllerGlyphFixer.Core/` (netstandard2.0, YamlDotNet 16.3.0 NuGet)
- `ControllerGlyphFixer.Core.Tests/` (net10.0, xUnit, references Core)
- `ControllerGlyphFixer.Contract.Tests/` (net10.0, xUnit, MetadataLoadContext; references
  Core for the mod names it checks)
- `ControllerGlyphFixer/` (plugin; compiles Core sources in; YamlDotNet NuGet with
  `ExcludeAssets="runtime"` so YamlDotNet.dll is not deployed)
- `dotnet test ControllerGlyphFixer.sln` runs layers 1 and 2. Build through the solution:
  JotunnLib finds `Environment.props` via `$(SolutionDir)`, so building the plugin project
  alone loses the BepInEx paths.

## Phases

Each phase follows: failing unit tests → Core code → contract tests for new patch targets →
thin patches → in-game self-test checks → manual checklist.

1. **v1:** bump JotunnLib NuGet and manifest dependencies to the installed versions; set up
   Core + both test projects; `KeyMap`, `GlyphMode`, `BoundKeyResolver` (TDD); F1 + F2
   postfix; config; startup gamepad log; `cgf_selftest`. Verify in game.
2. **v2:** F3 hotbar + AzuExtendedPlayerInventory extra slots, F4 hover rewriter (TDD for
   `HotbarLabels` and `HintRewriter`).
3. **v3:** F6 targeted visibility patches, starting with the inventory screen (in-game finding
   2026-11-02). F5 glyph images (branch `phase-f5-glyphs`, started 2026-11-08).
4. **v4:** F7, VDF → YAML generator.
5. **Before sharing the mod:** F9 pieces 1–4 and the user-facing README.
6. **Later:** F10 mod compat plugin (separate mod): AutomaticFuel smelter hint, chest rename on hold.
   Also: move the settings into sections (e.g. `General` for the switches, `Hotbar labels` for
   `HotbarLabels`, `PrefixColors`, `PrefixBackground`) with `ConfigurationManagerAttributes.Order`,
   so Mod Config reads in a sensible order. Moving an entry to another section resets it to its
   default once, so do it before others install the mod.

## In-game test checklist (per phase)

- Startup log lists a gamepad device.
- Hover a chest/workbench, press a keyboard-emitted button (e.g. `Q`): prompt stays a
  controller glyph.
- Inventory: trackpad mouse cursor still visible and usable.
- Keyboard settings screen still shows keyboard keys.
- Set `Enabled = false`: vanilla behaviour returns.
- (v2) Hotbar shows `R4 + …` labels; AzuAutoStore / AzuCraftyBoxes / AutomaticFuel /
  ImpactfulSkills hover hints show my buttons.

## Open questions

1. ~~Does Steam Input expose a virtual gamepad to Unity's Input System with this layout?~~
   **Answered 2026-11-02: yes.** It appears as `Xbox Controller (XInputControllerWindows)`, but
   connects after plugins load (around character selection). The glyph check reads
   `Gamepad.current` live, so glyphs turn on once it connects; the plugin logs connects and
   disconnects.
2. Exact key strings mods write in hover text (`Z` vs `KeyCode.Z` vs localized). Collect
   from game before finalizing aliases.
3. ~~Does `ZInput.CurrentGlyph` stay correct (Xbox vs PlayStation style) while the game is in
   keyboard mode?~~ **Answered 2026-11-02: yes** (`Auto` + `XInput` gives xbox glyphs while
   Valheim's input mode is keyboard).

## Reference: verified game internals (Valheim 1.0, decompiled 2026-11-01)

- `ZInput` (`assembly_utils`): `m_inputSource` flipped by private `OnInput(InputSource, bool)`
  on every key, mouse move, scroll. `IsGamepadActive()` reads it. Hidden switching modes
  (`BothGamepadHints` etc.) still switch on keyboard input; hint bits are unused.
- `Localization.Translate` (`assembly_guiutils`): `$KEY_X` → `GetBoundKeyString("JoyX")` if
  gamepad active and non-empty, else `GetBoundKeyString("X")`.
- `ZInput.GetBoundKeyString`: keyboard bindings return key names (or `s_keyLocalizationMap`
  tokens); gamepad bindings return sprite tags based on `CurrentGlyph`.
- `HotkeyBar.UpdateIcons`: `binding` label = `""` in gamepad mode, `index + 1` otherwise.
- `Hud.UpdateCrosshair`: `m_hoverName.text = hoverable.GetHoverText()`, with sprite-bracket
  cleanup only in gamepad mode.
- `ZCursor`: cursor visible only when `IsMouseActive()` and not gamepad-mouse.
