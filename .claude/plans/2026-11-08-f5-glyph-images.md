# F5 Glyph Images Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show buttons Valheim has no art for (R4/R5, L4/L5, trackpads) as dark Steam Deck glyph
images instead of `[R4]` text badges, with a setting to use our glyphs for every mapped button.

**Architecture:** A build-time script recolours Kenney's CC0 Steam Deck PNGs into
`ControllerGlyphFixer/Glyphs/`, embedded in the DLL. Core gets a button → sprite table and the
lookup order in `KeyMap.Format`. The plugin packs the PNGs into one texture at startup, builds a
`TMP_SpriteAsset` named `cgf_deck` and registers it with `MaterialReferenceManager`, so
`<sprite="cgf_deck" name="r4">` renders in any TMP text.

**Tech Stack:** C# (netstandard2.0 Core, net48 plugin), BepInEx/Jotunn/Harmony, TextMeshPro
(Valheim's `Unity.TextMeshPro`), xUnit (.NET 10), PowerShell 7 + System.Drawing.Common (script).

**Spec:** `.claude/specs/controller-glyphs.md`, section F5 (also F3/F4 effects, F8 config,
Testing strategy). Read F5 before starting any task.

## Global Constraints

- Build the solution, never one project: `dotnet build ControllerGlyphFixer.sln -c Debug`;
  tests: `dotnet test ControllerGlyphFixer.sln --no-build`. Commit only after "Build succeeded"
  and passing tests.
- Code style (CLAUDE.md): file header block + usings + `////` divider; `///` docs with
  `<summary>`/`<param>`/`<returns>` on every member including private and tests; target-typed
  `new ()`; Unity objects checked with `if (obj)`, never `??`/`?.`; comments on bool `InlineData`.
- Logic in Core with tests written first; the plugin stays thin. No singletons or mutable
  statics beyond Harmony seams.
- Everything created is destroyed in `OnDestroy`; runtime textures/materials/assets get
  `HideFlags.DontUnloadUnusedAsset`.
- Kenney art is CC0 (`Input Prompts` 1.5A, https://kenney.nl/assets/input-prompts). Only the
  generated PNGs and Kenney's `License.txt` are committed, never the zip or its unpacked files.
- Sprite asset name: `cgf_deck`. Config: `[General] ButtonGlyphs`, values `ValheimFirst`
  (default) and `ModGlyphsForAll`.
- Look (spec): dark fill `#14181C`, light rim and letters `#EBE6DC`, soft black shadow.
- Branch `phase-f5-glyphs`. Commit messages end with
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Valheim's `xbox` sprite asset isn't loadable when the plugin's `Awake` runs.** Expect our
   glyphs still build with fallback metrics, and the self-test says which metrics were used
   (Task 4 logs it, Task 5 reports it).
2. **TMP's sprite-asset upgrade wipes our tables.** `UpdateLookupTables()` calls
   `UpgradeSpriteAsset()` when `material != null` and the internal `m_Version` is empty. Expect
   `m_Version` set to `"1.1.0"` before `UpdateLookupTables()`; the contract test pins the field
   (Task 3) and the self-test renders a real sprite (Task 5).
3. **Valheim's unused-asset sweep on world load frees the atlas.** Expect glyphs still showing
   after loading a world; self-test checks the atlas and asset are alive (Task 5).
4. **A mapping button with no sprite** (`LG`, `Steam`, `LS` under `ModGlyphsForAll`) or **a failed
   build**. Expect the next source (Valheim art, then the `[badge]`), never a blank (Task 2 tests).
5. **Changing `ButtonGlyphs` live in Mod Config.** Expect hotbar, inventory prefixes and hover
   text to redraw at once (Task 4 wires `SettingChanged`). Known limit: `$KEY_` text cached by
   Valheim's Localization updates on the next input-mode switch (existing TODO in
   `BoundKeyStringPatch`).

---

### Task 1: Glyph art script and generated PNGs

**Files:**
- Create: `scripts/make-glyphs.ps1`
- Create: `ControllerGlyphFixer/Glyphs/*.png` (28 files, generated), `ControllerGlyphFixer/Glyphs/License.txt` (Kenney's, copied)
- Modify: `ControllerGlyphFixer/ControllerGlyphFixer.csproj` (embed PNGs)
- Modify: `ControllerGlyphFixer/README.md` (credit)

**Interfaces:**
- Produces: PNGs named by sprite name (below), each 144 × 144: Kenney's 128 px glyph centred
  with 8 px padding for the shadow. Embedded with logical name
  `ControllerGlyphFixer.Glyphs.<sprite>.png`.

Sprite name ← Kenney base name (all from `Steam Deck/Double/`, each needs its `_outline` twin):

| Sprites | Kenney |
|---|---|
| `a` `b` `x` `y` | `steamdeck_button_{a,b,x,y}` |
| `l1` `r1` `l2` `r2` `l4` `l5` `r4` `r5` | `steamdeck_button_{l1,r1,l2,r2,l4,l5,r4,r5}` |
| `view` `options` | `steamdeck_button_view`, `steamdeck_button_options` |
| `dpad_up` `dpad_down` `dpad_left` `dpad_right` | `steamdeck_dpad_{up,down,left,right}` |
| `lpad` `lpad_up` `lpad_down` `lpad_left` `lpad_right` | `steamdeck_trackpad_l`, `steamdeck_trackpad_l_{up,down,left,right}` |
| `rpad` `rpad_up` `rpad_down` `rpad_left` `rpad_right` | `steamdeck_trackpad_r`, `steamdeck_trackpad_r_{up,down,left,right}` |

Sticks are left out: Kenney has no `_outline` variant for them, and Valheim has stick art.

- [ ] **Step 1: Write `scripts/make-glyphs.ps1`**

Parameter `-KenneyDir` (the unpacked zip's root; the header comment says to download *Input
Prompts* from kenney.nl and unzip it anywhere outside the repo). Output to
`ControllerGlyphFixer/Glyphs/` relative to the script; copies `License.txt` there. Per sprite,
compose with System.Drawing (`Add-Type` C# helper, references `System.Drawing.Common`,
`System.Drawing.Primitives`):

```
solidA  = max(plain.A, outline.A)          # plain has letters cut out; outline is rim + letters
colour  = lerp(#14181C, #EBE6DC, outline.A/255), alpha solidA
shadow  = solidA shifted (+3, +4) px, box blur radius 4, black at 80% opacity, drawn under
canvas  = 144 x 144, glyph at (8, 8)
```

A working prototype of both helpers is in the session scratchpad mocks (`outline-mock.ps1`
`Fx.Make` for shadow, `kenney-dark.ps1` `Dark.Make` for the recolour); reuse the approach, not
the files. The script fails loudly when a Kenney file is missing.

- [ ] **Step 2: Run it**

Run: `pwsh scripts/make-glyphs.ps1 -KenneyDir <unpacked zip>` (the zip unpacked earlier is at
`%TEMP%\claude\...\scratchpad\kenney\x`; ask the user for the path if it's gone).
Expected: 28 PNGs and `License.txt` in `ControllerGlyphFixer/Glyphs/`; each PNG 144 × 144.

- [ ] **Step 3: Show the user a contact sheet** of the 28 outputs on a dark and a light
  background (scratchpad, `SendUserFile`) and wait for an OK on the look before going on.

- [ ] **Step 4: Embed and credit**

csproj: `<EmbeddedResource Include="Glyphs\*.png" LogicalName="ControllerGlyphFixer.Glyphs.%(Filename)%(Extension)" />`
and `<None Include="Glyphs\License.txt" />`. README: a "Credits" section naming Kenney's
*Input Prompts* (CC0) for the controller glyphs.

- [ ] **Step 5: Build**

Run: `dotnet build ControllerGlyphFixer.sln -c Debug`
Expected: Build succeeded. Check the resources are in the DLL:
`[Reflection.Assembly]::LoadFile("<bin>\ControllerGlyphFixer.dll").GetManifestResourceNames()`
lists 28 `ControllerGlyphFixer.Glyphs.*.png`.

- [ ] **Step 6: Commit** (`scripts/make-glyphs.ps1`, `ControllerGlyphFixer/Glyphs/`, csproj, README):
  "F5: Generate dark Steam Deck glyphs from Kenney's CC0 prompts"

---

### Task 2: Core button → sprite table and lookup order (TDD)

**Files:**
- Create: `ControllerGlyphFixer.Core/ModGlyphs.cs`, `ControllerGlyphFixer.Core/ButtonGlyphOrder.cs`
- Modify: `ControllerGlyphFixer.Core/KeyMap.cs` (`Translate`, `Format`, `FormatButton`; remove the `TODO(F5)`)
- Test: `ControllerGlyphFixer.Core.Tests/ModGlyphsTests.cs`, `KeyMapTests.cs`, `HintRewriterTests.cs`

**Interfaces:**
- Produces:
  - `public enum ButtonGlyphOrder { ValheimFirst, ModGlyphsForAll }` (config enum).
  - `public static class ModGlyphs`:
    - `public const string AssetName = "cgf_deck";`
    - `public static bool TryGetSprite(string normalizedButton, out string sprite)`
    - `public static string Tag(string normalizedButton)`: `SpriteStyle.Tag(AssetName, sprite)` or null.
    - `public static IReadOnlyList<string> SpriteNames` (distinct, sorted; the 28 names of Task 1).
  - `KeyMap.Translate(string keyName, Func<string, string> valheimGlyph, Func<string, string> modGlyph = null, ButtonGlyphOrder order = ButtonGlyphOrder.ValheimFirst)`
  - `KeyMap.Format(IReadOnlyList<string> buttons, Func<string, string> valheimGlyph, Func<string, string> modGlyph = null, ButtonGlyphOrder order = ButtonGlyphOrder.ValheimFirst)`
  - `modGlyph` takes a normalized button name (the plugin passes `ModGlyphs.Tag` when the sprite
    asset was built, null otherwise). Null means no mod glyphs: existing behaviour and tests.

Normalized button name → sprite (normalization as `KeyMap.Normalize`: lowercase, no spaces,
`_`, `-`):

| Buttons | Sprite |
|---|---|
| `a` `b` `x` `y` | same |
| `l1` `lb` / `r1` `rb` / `l2` `lt` / `r2` `rt` | `l1` / `r1` / `l2` / `r2` |
| `l4` `l5` `r4` `r5` | same |
| `view` `back` `select` / `options` `start` `menu` | `view` / `options` |
| `dpadup` `dpaddown` `dpadleft` `dpadright` | `dpad_up` ... |
| `lpad` `lpadclick` `ltrackpad` / `lpadup` `lpaddown` `lpadleft` `lpadright` | `lpad` / `lpad_up` ... |
| same for `rpad` / `rtrackpad` | `rpad` / `rpad_up` ... |

- [ ] **Step 1: Write the failing tests** (`ModGlyphsTests`)
  - `Deck_buttons_map_to_sprites` (Theory over every row above, including aliases, e.g.
    `("r4","r4")`, `("lpadup","lpad_up")`, `("lb","l1")`, `("start","options")`).
  - `Buttons_without_art_have_no_sprite` (`"lg"`, `"steam"`, `"ls"`, `""` → false; `Tag` → null).
  - `Tag_uses_the_cgf_deck_asset`: `ModGlyphs.Tag("r4") == "<sprite=\"cgf_deck\" name=\"r4\">"`.
  - `Every_sprite_has_a_png_and_every_png_a_sprite`: find the repo root by walking up from
    `AppContext.BaseDirectory` to `ControllerGlyphFixer.sln`; the `*.png` base names in
    `ControllerGlyphFixer/Glyphs/` equal `ModGlyphs.SpriteNames` (as sets).

- [ ] **Step 2: Write the failing `KeyMapTests`** (fixture `SteamController`, `FakeGlyph`
  returns `<control>`, mod glyph = `ModGlyphs.Tag`):
  - `ValheimFirst_uses_valheim_art_then_ours`: `Translate("1", FakeGlyph, ModGlyphs.Tag)` ==
    `"<sprite=\"cgf_deck\" name=\"r4\"> + <dpad_up>"`; `Translate("B", ...)` == the `lpad_up` tag
    (the `labels:` entry no longer shows).
  - `ModGlyphsForAll_uses_ours_then_valheim`: `Translate("1", FakeGlyph, ModGlyphs.Tag, ModGlyphsForAll)`
    == `r4` tag + " + " + `dpad_up` tag; a button only Valheim has art for (`LS`, via a small
    inline map `keys: { Q: LS }`) gives `<leftStickPress>`.
  - `No_mod_glyphs_keeps_badges`: `modGlyph: null` with either order gives `"[R4] + <dpad_up>"`.
  - `Buttons_with_no_art_keep_their_badge`: inline map `keys: { Q: LG }` → `"[LG]"` in both orders.
  - Existing tests stay as they are (they pass no `modGlyph`).

- [ ] **Step 3: Write the failing `HintRewriterTests` case**:
  `Our_sprite_tags_count_as_glyphs`: `"[<sprite=\"cgf_deck\" name=\"lpad_up\">] AutoFuel"`
  → `"<sprite=\"cgf_deck\" name=\"lpad_up\"> AutoFuel"`, no unmapped keys. (Expected to pass
  already; it documents F4 with our tags.)

- [ ] **Step 4: Run to see them fail**
  Run: `dotnet build ControllerGlyphFixer.sln -c Debug` then
  `dotnet test ControllerGlyphFixer.sln --no-build --filter "FullyQualifiedName~ModGlyphs|FullyQualifiedName~KeyMap"`
  Expected: build errors for `ModGlyphs` / `ButtonGlyphOrder` (missing types).

- [ ] **Step 5: Implement** `ModGlyphs`, `ButtonGlyphOrder`, and the order in
  `KeyMap.FormatButton`: `ValheimFirst` = Valheim art, else `modGlyph`, else badge;
  `ModGlyphsForAll` = `modGlyph`, else Valheim art, else badge. A null or empty result from
  either lookup falls through. `ModGlyphs` is a static table like `ValheimButtons`.

- [ ] **Step 6: Run all tests**
  Run: `dotnet build ControllerGlyphFixer.sln -c Debug` then `dotnet test ControllerGlyphFixer.sln --no-build`
  Expected: all pass (contract tests pass or skip).

- [ ] **Step 7: Commit**: "F5: Core sprite table and Valheim-first / mod-first glyph order"

---

### Task 3: Contract tests for the TMP members we use

**Files:**
- Create: `ControllerGlyphFixer.Contract.Tests/TextMeshProContractTests.cs`

**Interfaces:**
- Consumes: `GameAssemblies.Type("Unity.TextMeshPro", "TMPro.<Type>")`,
  `GameAssemblies.Type("UnityEngine.TextCoreFontEngineModule", "UnityEngine.TextCore.<Type>")`,
  `[GameFact]`, `.InGame()` (existing helpers in `GameAssemblies.cs` / `PatchTargetTests.cs`).

- [ ] **Step 1: Write the tests** (one `[GameFact]` each):
  - `MaterialReferenceManager_registers_sprite_assets`: static `AddSpriteAsset(TMP_SpriteAsset)`.
  - `TMP_Asset_version_field_exists`: instance non-public field `m_Version` of type `String`
    (we set it so `UpdateLookupTables` doesn't run `UpgradeSpriteAsset`).
  - `TMP_SpriteAsset_members_exist`: public field `spriteSheet : Texture`; properties
    `spriteGlyphTable : List<TMP_SpriteGlyph>`, `spriteCharacterTable : List<TMP_SpriteCharacter>`;
    method `UpdateLookupTables()`; method `GetSpriteIndexFromName(String) : Int32`.
  - `Sprite_glyph_and_character_constructors_exist`:
    `TMP_SpriteGlyph(UInt32, GlyphMetrics, GlyphRect, Single, Int32)`,
    `TMP_SpriteCharacter(UInt32, TMP_SpriteAsset, TMP_SpriteGlyph)`; `TMP_SpriteCharacter.name`
    settable; `TMP_Settings.defaultSpriteAssetPath` static property.

- [ ] **Step 2: Run them**
  Run: `dotnet build ControllerGlyphFixer.sln -c Debug` then
  `dotnet test ControllerGlyphFixer.sln --no-build --filter FullyQualifiedName~TextMeshPro`
  Expected: PASS (all four exist in the decompiled Valheim 1.0 TMP, checked 2026-11-08).

- [ ] **Step 3: Update the spec's contract-test list** if names differ, then commit:
  "F5: Contract tests for the TextMeshPro sprite-asset members"

---

### Task 4: Runtime sprite asset and config

**Files:**
- Create: `ControllerGlyphFixer/ModSpriteAsset.cs`
- Modify: `ControllerGlyphFixer/ControllerGlyphFixer.cs` (config, build, wiring, `OnDestroy`)
- Modify: `ControllerGlyphFixer/GlyphService.cs` (pass `modGlyph` and order)
- Modify: `ControllerGlyphFixer/HoverTextWriter.cs` (`Invalidate`)

**Interfaces:**
- Consumes: `ModGlyphs.AssetName`, `ModGlyphs.SpriteNames`, `ModGlyphs.Tag`,
  `ButtonGlyphOrder`, `KeyMap.Translate/Format(..., modGlyph, order)` (Task 2); the embedded
  PNGs (Task 1).
- Produces:
  - `internal sealed class ModSpriteAsset`:
    - `internal static ModSpriteAsset TryCreate()`: builds and registers; null (logged once) on
      any failure, after destroying whatever it created.
    - `internal bool IsLoaded` (atlas, material and asset all alive).
    - `internal string MetricsSource` ("Valheim xbox button_a" or "fallback"), for the self-test.
    - `internal Texture2D Atlas`, `internal TMP_SpriteAsset Asset` (self-test).
    - `internal void Destroy()`.
  - `GlyphService` ctor gains `ConfigEntry<ButtonGlyphOrder> buttonGlyphs` and
    `Func<string, string> modGlyph` (null when no asset); `TranslateKey` / `FormatButtons` pass both.
  - `HoverTextWriter.Invalidate()`: clears `m_lastInput` / `m_lastOutput`.

- [ ] **Step 1: Write `ModSpriteAsset.TryCreate`**
  1. Load each `ModGlyphs.SpriteNames` resource (`ControllerGlyphFixer.Glyphs.<name>.png`) with
     `ImageConversion.LoadImage` into a new `Texture2D(2, 2)`.
  2. Pack with `atlas.PackTextures(textures, 2, 2048)`; destroy the source textures; convert the
     returned UV rects to pixel `GlyphRect`s.
  3. `ScriptableObject.CreateInstance<TMP_SpriteAsset>()`, `name = ModGlyphs.AssetName`,
     `spriteSheet = atlas`, `material = new Material(Shader.Find("TextMeshPro/Sprite"))` with
     `_MainTex` = atlas; set `m_Version = "1.1.0"` via `AccessTools.Field(typeof(TMP_Asset), "m_Version")`.
  4. Per sprite i: `new TMP_SpriteGlyph((uint)i, metrics, rect, scale, 0)` into
     `spriteGlyphTable`; `new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = sprite }` into
     `spriteCharacterTable`; then `asset.UpdateLookupTables()`. Unicode `0xFFFE` is TMP's "no
     code point" value (skipped by its unicode lookup; tags find sprites by name).
  5. Metrics: look up Valheim's asset with
     `Resources.Load<TMP_SpriteAsset>(TMP_Settings.defaultSpriteAssetPath + SpriteStyle.Xbox)` and
     its `button_a` glyph (`GetSpriteIndexFromName`). With its height `H`, bearing-Y `BY` and
     scale `S`, ours are: width = height = 144, bearing-X 0, bearing-Y `BY * 128 / H + 8`,
     advance 136, scale `S * H / 128` (the 128 px key matches Valheim's glyph; the 8 px shadow
     pad overhangs). Not found: bearing-Y 120, advance 136, scale 1, `MetricsSource = "fallback"`.
  6. `DontUnloadUnusedAsset` on atlas, material, asset; `MaterialReferenceManager.AddSpriteAsset(asset)`.

- [ ] **Step 2: Wire it in `Awake`**
  - Bind before `GlyphService`: `Config.Bind("General", "ButtonGlyphs", ButtonGlyphOrder.ValheimFirst, ...)`,
    description: "Which art shows for controller buttons in hotbar labels, prefixes and hover
    hints. ValheimFirst: Valheim's glyphs where it has them, this mod's Steam Deck glyphs for the
    rest (R4/R5, L4/L5, trackpads). ModGlyphsForAll: this mod's glyphs for every button. Buttons
    with no art show as [text]."
  - `m_modSprites = ModSpriteAsset.TryCreate();` pass `m_modSprites != null ? ModGlyphs.Tag : null`
    (a cached `Func<string, string>` field, not a new method group per call).
  - `buttonGlyphs.SettingChanged += RedrawGlyphs` (redraws prefixes like `RedrawPrefixes` and
    calls `hoverTextWriter.Invalidate()`); unsubscribe in `OnDestroy`.
  - `OnDestroy`: `m_modSprites?.Destroy()` (a plain class, so `?.` is fine); `Destroy()`
    destroys the asset, material and atlas. TMP can't unregister the asset (spec F5: accepted).

- [ ] **Step 3: Build and run tests**
  Run: `dotnet build ControllerGlyphFixer.sln -c Debug` then `dotnet test ControllerGlyphFixer.sln --no-build`
  Expected: Build succeeded, all tests pass. Check the deployed DLL in the Gale profile's
  `BepInEx\plugins` has the new timestamp (Valheim running locks it).

- [ ] **Step 4: Commit**: "F5: Build the cgf_deck sprite asset at startup; ButtonGlyphs setting"

---

### Task 5: Self-test checks and sprite-sheet dumps

**Files:**
- Modify: `ControllerGlyphFixer/SelfTestCommand.cs` (ctor takes `ModSpriteAsset`, may be null)
- Modify: `ControllerGlyphFixer/ControllerGlyphFixer.cs` (pass it)

**Interfaces:**
- Consumes: `ModSpriteAsset.IsLoaded`, `.MetricsSource`, `.Atlas`, `.Asset` (Task 4).

- [ ] **Step 1: Add `CheckModGlyphs()`** called from `Run`:
  - `Check("Mod glyph sprite asset built", modSprites != null)`; if null, stop here.
  - `Check("Mod glyph atlas and asset still loaded", modSprites.IsLoaded)`.
  - `Info("Mod glyph metrics from " + modSprites.MetricsSource)`.
  - `Check("Mod glyph renders as a sprite", ...)`: a temporary `GameObject` with
    `TextMeshProUGUI`, text `ModGlyphs.Tag("r4")`, `ForceMeshUpdate()`; pass when
    `textInfo.characterCount == 1`, its `elementType == TMP_TextElementType.Sprite` and its
    `spriteAsset == modSprites.Asset`. Destroy the object.
  - `Info` with `m_glyphs.FormatButtons(new[] { "R4", "DPadUp" })` and the current `ButtonGlyphs`.
  - Dump PNGs next to `LogOutput.log` (`Paths.BepInExRootPath`): `cgf-glyphs-ours.png` (our atlas)
    and `cgf-glyphs-xbox.png` (Valheim's xbox `spriteSheet`, if found). Copy through a temporary
    `RenderTexture` + `ReadPixels` (sheets aren't readable), `EncodeToPNG`, destroy the temporaries.
    `Info` the paths.

- [ ] **Step 2: Build and test**
  Run: `dotnet build ControllerGlyphFixer.sln -c Debug` then `dotnet test ControllerGlyphFixer.sln --no-build`
  Expected: Build succeeded, all pass.

- [ ] **Step 3: Commit**: "F5: Self-test checks for the mod glyph sprite asset"

---

### Task 6: In-game check and spec findings

- [ ] **Step 1: Ask the user to restart Valheim** and, in a world, run `cgf_selftest` (F5
  console), then send screenshots in controller mode and keyboard/trackpad mode of: hotbar,
  quick-slot bar, inventory, a hover hint with a mapped key (AutomaticFuel `[B]`).
- [ ] **Step 2: Read `LogOutput.log`** for `[cgf-selftest]` lines (all F5 checks PASS) and
  compare `cgf-glyphs-ours.png` with `cgf-glyphs-xbox.png`.
- [ ] **Step 3: Tune** size/baseline constants from the screenshots (Task 4 Step 1.5) if glyphs sit
  wrong next to Valheim's; then have the user toggle `ButtonGlyphs` to `ModGlyphsForAll` in Mod
  Config and back, and load a second world (asset sweep).
- [ ] **Step 4: Record dated findings in the spec** (F5; F3's postponed inventory spacing, 70%
  size, backing width), then commit: "Spec: F5 in-game findings".

---

### Task 7: Final review

- [ ] **Step 1:** Review the whole branch against CLAUDE.md's "Reviewing code" list (leaks and
  lifetimes, per-frame cost, docs, tests) and report findings ranked before fixing.
- [ ] **Step 2:** Ask the user whether to push and open a PR (they merge).
