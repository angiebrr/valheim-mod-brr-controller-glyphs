# Spec: Controller glyphs for a keyboard-emitting Steam Input layout

Status: draft, approved direction, not implemented
Last updated: 2026-11-01
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
  - `HotbarLabels.For(int slotIndex, string currentText, bool showGlyphs, KeyMap map) -> string` (F3)
  - `HintRewriter.Rewrite(string text, KeyMap map) -> string` (F4; see rules below)
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
- **Patch:** postfix on `HotkeyBar.UpdateIcons(Player)` (private, `assembly_valheim`).
- **Behaviour:** for each element's `binding` `TMP_Text`, if `ShowKeyPrompts` and the
  text is a mapped key (`1`–`8`), replace it. Only rewrite text; never add or remove objects.
- **Note:** vanilla sets the label once when elements are created: empty in gamepad mode,
  `1`–`8` otherwise. In gamepad mode we need to write the label ourselves (slot index + 1
  → mapping), since the text is empty.
- **Risk:** medium (private but long-stable).
- **Compat:** AzuExtendedPlayerInventory quick slots draw their own labels. Check whether F4
  or a small dedicated postfix is needed.
- **In-game finding (2026-11-02):** after v1, hotbar and AzuExtendedPlayerInventory extra-slot
  labels still show keyboard keys (expected: not covered by F1). Wanted: both show the mapped
  buttons (e.g. `[R4] + D-pad up` on slot 1, `[R5] + ...` on extra slots).
- **Limit:** Valheim can't see Steam action-set modifiers (holding R4/R5 switches layers inside
  Steam; the game only sees the key sent by the second button). Labels can always show the
  combo, but can't appear only while R4/R5 is held unless the hold itself sends a key.
- **Found with `cgf_dumphotbars` (2026-11-02):** labels are written in two places, and both
  apply to AzuEPI's quick-slot bar too (it's a second `HotkeyBar` named `AzuEPI_QuickAccessBar`):
  `UpdateIcons` when slots are created (AzuEPI sets its "Display Text" there), and
  `HotkeyBar.ToggleBindingHint`, which Valheim calls on every keyboard/gamepad switch and which
  overwrites every label with `1`.. or blank. That's why AzuEPI's names (`R5 Up`) disappear.
  Mods patching `HotkeyBar.UpdateIcons`: MyLittleUI (prefix 800, 2 postfixes), AzuEPI (prefix
  and postfix, priority 0), EpicLoot (transpiler); none of their postfixes write labels.
- **Implemented:** `SlotLabel.For` (Core, tested) builds a slot's label from its keyboard
  shortcut (modifiers + main key) through the mapping; null leaves the slot alone (glyphs off,
  no key, unmapped). `HotbarLabelsPatch` postfixes `UpdateIcons`, `ToggleBindingHint` and
  `OnEnable` and calls `HotbarLabelWriter`, which only rewrites when slots were rebuilt, glyphs
  toggled, or Valheim just reset the labels, and restores Valheim's labels when glyphs turn off.
  Keys: Valheim's bar asks the original `GetBoundKeyString("HotbarN")` through a Harmony reverse
  patch (`OriginalBoundKeyString`, raw key, respects rebinding); AzuEPI's bar reads AzuEPI's
  `[8 - Quick Slot Hotkeys] Hotkey N` config at runtime (`QuickSlotKeys`, no compile reference).
  Config `General.HotbarLabels`. AzuEPI's own display names are ignored.
- **Size / presentation (pending a screenshot):** `[R4] + (glyph)` may be too wide for a slot.
  Alternatives to consider: a compact form (`R4+` glyph, or short labels via the mapping file's
  `labels:` section); an outline around each bar with the hold button (R4/R5) drawn once outside
  it and only the second button per slot; or highlighting the bar's slots while the action-set
  button is held. Highlighting needs the Steam layout to also send a spare key while R4/R5 is
  held (e.g. F13 on layer activation), which the mod would watch.

### F4. Hover text with key names written directly by mods
- **Patch:** postfix on `Hud.UpdateCrosshair(Player, float)` (private, `assembly_valheim`),
  the single place all hover text (vanilla and mods) is assigned to `m_hoverName.text`.
- **Behaviour:** if `ShowKeyPrompts`, run `HintRewriter.Rewrite` on `m_hoverName.text`:
  - Replace a key name only inside a key-hint wrapper: `[...]`,
    `[<color=yellow><b>...</b></color>]`, or `<b>...</b>`, and only if the whole inner
    text is a mapped key name or alias. Never touch free text.
  - Apply the same cleanup vanilla does in gamepad mode: strip `[<color=yellow><b>` /
    `</b></color>]` around `<sprite=…>` glyphs (vanilla does this only when
    `IsGamepadActive()`; with F1 sprites now appear in keyboard mode too).
- **Risk:** medium (one private method; mod-agnostic).
- **Cost:** runs every frame while hovering. Skip the rewrite if the text is unchanged since
  last frame.
- **In-game finding (2026-11-02):** AzuCraftyBoxes' chest hint shows `LT + X` in gamepad mode
  but `[E + LeftShift] Crafting Chest` after keyboard/trackpad input. `E` isn't in my mapping:
  it's Valheim's Use key, which I press as X. So F4 must also translate keys Valheim has bound
  to an action (key -> Valheim binding -> its Joy glyph, e.g. E -> Use -> X), not only keys in
  the mapping file. Vanilla chest hover text also differs by mode (yellow, no brackets around
  glyphs in gamepad mode; white with `[ ]` otherwise); the bracket cleanup above covers part of it.

### F5. Glyph appearance
- No patch. Handled by mapping output (Valheim sprite tags where art exists, text badges
  otherwise). Later option: Steam's controller glyph PNGs as a custom TMP sprite asset.

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
- Later: a script that generates the YAML mapping from my Steam layout VDF.

## Testing strategy

Develop test-first. Valheim code needs Unity's native engine, so it can't run in a test
runner; keep logic in Core and patches thin. Four layers:

### 1. Unit tests (`ControllerGlyphFixer.Core.Tests`, xUnit, .NET 10) — TDD, seconds
Red → green → refactor for every Core function. Required cases:
- `KeyMap`: every row of the fixture mapping (tests supply the mapping; Core has none);
  aliases (`Alpha1`, `KeypadNumber1`, `Left Shift`, `$button_lshift`); case-insensitivity;
  unknown keys pass through; combos; invalid YAML gives a clear error; the template maps
  nothing, and uncommenting its examples yields a valid map; empty sections are allowed.
- `GlyphMode.Decide`: truth table of the four inputs.
- `BoundKeyResolver.Resolve`:
  - `("Use", "E", showGlyphs: true, joy → A sprite)` → A sprite.
  - `("Hotbar1", "1", showGlyphs: true, joy → "")` → mapped `R4 + D-pad Up`.
  - `showGlyphs: false` → original result unchanged.
  - `name` starting with `Joy` → never looks up `JoyJoy…` (recursion guard).
  - unmapped key with no Joy binding → original result.
- `HotbarLabels.For`: gamepad mode (empty current text) writes the label from slot index;
  keyboard mode rewrites `1`–`8`; unmapped/other text untouched.
- `HintRewriter.Rewrite`: rewrites `[Z]`, `[<color=yellow><b>Z</b></color>]`, `<b>Z</b>`;
  leaves free text and non-key brackets alone (`Zone [Zebra]`); strips sprite brackets
  like vanilla gamepad mode; idempotent (rewriting twice = once).

### 2. Contract tests (`ControllerGlyphFixer.Contract.Tests`) — catch game updates, seconds
Inspect the real game DLLs **read-only** with `System.Reflection.MetadataLoadContext`
(no game code runs). Paths come from `Environment.props` (`VALHEIM_INSTALL`); tests skip
when the game isn't installed. Assert each patch target and touched member exists with the
expected signature:
- `ZInput.GetBoundKeyString(string, bool) : string` (assembly_utils)
- `HotkeyBar.UpdateIcons(Player)`, `Hud.UpdateCrosshair(Player, float)`,
  `Hud.m_hoverName : TMP_Text` (assembly_valheim)
- `Settings.instance`, `ZInput.IsGamepadEnabled()`
Run after every Valheim update before launching the game.

### 3. In-game self-test (Jotunn console command `cgf_selftest`) — real game
Registered by the plugin; logs `PASS`/`FAIL` per check with a `[cgf-selftest]` prefix:
- each of our Harmony patches is applied (`Harmony.GetPatchInfo`);
- `ZInput.IsGamepadActive()` still reports the real input mode (behaviour untouched);
- a gamepad device is present (open question 1);
- `Localization.instance.Localize("$KEY_Use")` returns a controller sprite while in
  keyboard mode; `"$KEY_Hotbar1"` returns the mapped string.
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
- `ControllerGlyphFixer.Contract.Tests/` (net10.0, xUnit, MetadataLoadContext)
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
   2026-11-02).
4. **v4:** F7, VDF → YAML generator.

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
