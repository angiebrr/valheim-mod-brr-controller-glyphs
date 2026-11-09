# BrrControllerGlyphs

A personal BepInEx/Jotunn mod for Valheim 1.0 (Windows). The player uses a Steam Controller
whose Steam Input layout sends real gamepad buttons for core actions and keyboard keys for
extras (action-set holds on R4/R5, back grips, trackpad clicks). That flips Valheim to keyboard
glyphs and leaves keyboard keys on hotbar labels and mod hints. The mod keeps controller glyphs
on while a gamepad is connected, and shows keyboard-only keys as the Steam buttons that send them.

The spec is the source of truth for features, decisions and findings:
`.claude/specs/controller-glyphs.md` (features F1–F9, testing strategy, phases, verified game
internals). Read the relevant section before changing a feature, and record new in-game findings
and decisions there (dated).

## Layout

- `BrrControllerGlyphs.Core/`: netstandard2.0, no Unity or Valheim types. All decision logic
  (KeyMap, BoundKeyResolver, SlotLabel, BarLabels, SlotLabelRefresh, PrefixColors, HintRewriter,
  QuickSlotCells, GroupShapes, RoundedCorners…). Compiled into the plugin DLL via linked sources,
  so only one DLL ships.
- `BrrControllerGlyphs/`: the plugin, a thin shell. Reads game state, calls Core, sets text.
  Harmony patches live in `Patches/`.
- `BrrControllerGlyphs.Core.Tests/`: xUnit, net10.0. Fixture mapping in
  `Fixtures/steam-controller.yaml` (the mod ships no mapping; tests supply one).
- `BrrControllerGlyphs.Contract.Tests/`: reads the installed game DLLs (and other mods' DLLs in
  the Gale profile) with MetadataLoadContext to check every member we patch or read still exists.
  Tests skip when the game or mod isn't installed.
- `scripts/`: `publish.ps1` (from the JotunnModStub template) deploys Debug builds and packages
  Release builds; `make-glyphs.ps1` makes the glyph PNGs. `libraries/` holds `pdb2mdb.exe` for it.
- READMEs: the root one is for developers; `BrrControllerGlyphs/README.md` is the Thunderstore
  page (its plan is in the spec, F9). Update the mod page when a feature or setting changes.
- Names: the mod is BrrControllerGlyphs (plugin ID `angiebrr.BrrControllerGlyphs`); everything
  it adds to the game (commands, assets, objects) starts with `NamePrefix.Value` (`brrcg_`).

## Build, test, deploy

- Always build the solution, not a single project: JotunnLib's props need `$(SolutionDir)`.
  `dotnet build BrrControllerGlyphs.sln -c Debug`
- Tests: `dotnet test BrrControllerGlyphs.sln --no-build`
- `Environment.props` (git-ignored, machine-local) sets `VALHEIM_INSTALL`, `GALE_PROFILE`,
  `BEPINEX_PATH` and `MOD_DEPLOYPATH`. Mods run from the Gale profile "QoL Valheim", not the game
  folder; Debug builds copy into its `BepInEx\plugins`.
- If Valheim is running it locks the DLL and the copy fails ("user-mapped section open") while
  the build still succeeds. Check the deployed DLL matches the build before saying it's deployed.
- The game loads the DLL only at startup: the user must restart Valheim to test a new build.
- Only commit after a build that printed "Build succeeded" and passing tests.

## In-game testing

- The user tests in game and sends screenshots (controller mode and keyboard/trackpad mode).
- `brrcg_selftest` (F5 console) logs PASS/FAIL/INFO lines with a `[brrcg_selftest]` prefix to
  `LogOutput.log`. Extend it whenever a feature adds a patch or something only checkable in game.
- Mod Config (shudnal's ConfigurationManager, F1 or the pause menu button) applies setting
  changes live; the Gale config editor needs a restart.

## Design rules

- Postfixes only, at `Priority.Low`; no transpilers, no prefixes that skip originals. Never change
  Valheim's input mode or ZInput state.
- Each patch class catches its own errors and logs the first one (`PatchErrorLog`), leaving the
  game's behaviour in place.
- A patch that can't apply (missing target after a game update) disables only its feature.
- Logic goes in Core with tests; the plugin stays thin.
- Other mods are read at runtime (BepInEx `Chainloader`, config entries, text they write), never
  referenced at compile time. Names copied from them live in Core and are checked by contract
  tests.
- Everything the mod adds to the UI is cleaned up when glyphs turn off, and on plugin destroy
  (unpatch, clear the static seams, unsubscribe, destroy created assets).

## Code style

- Every C# file starts with the header block, then usings, then the divider:
  ```
  // ------------------------------------------------------------------------------------------------
  // FileName.cs: one-line description of the file.
  // ------------------------------------------------------------------------------------------------

  using ...;

  ///////////////////////////////////////////////////////////////////////////////////////////////////
  ```
- `///` XML docs on every type and member, private ones and tests included; every method has a
  `<summary>` plus `<param>`/`<returns>` as relevant.
- `// TODO:` / `// FIXME:` for known follow-ups, tagged where useful (`TODO(F3)`,
  `TODO(before sharing)`). FIXME marks code copied from the game or another mod.
- Target-typed `new ()` (with a space).
- Dependencies through explicit constructors (readonly fields, null checks), built and wired in
  the plugin's `Awake`. No singletons or mutable statics; the only static state is the seam a
  Harmony patch class needs (`HotbarLabelsPatch.Writer`), documented and null-safe.
- Prefer instance members. Static only where needed: Harmony patch methods, Core's pure-function
  classes, constants, and small stateless helper classes (`BindingLabels`, `RoundedSprite`).
- Named locals for non-obvious conditions; comments on bool `InlineData` rows in tests.
- Unity objects: check with `if (obj)` / `obj ? a : b`, never `??` or `?.`, which skip Unity's
  destroyed-object check.
- Comments explain why, briefly; put them at the member they apply to, not in long class-level
  remarks.

## How we work

- Run the design by the user before larger changes, and say when an approach changes what was
  agreed. Ask when a choice is really theirs (look and feel, scope); decide routine details.
- TDD: write the failing test first, then the code.
- Diagrams help the user decide on layouts: draw options before building UI.
- Keep the spec current: decisions, in-game findings (dated), and what's out of scope.
- Decompile game or mod DLLs into the session scratchpad, never the repo (ilspycmd via
  `%USERPROFILE%\.dotnet\tools\ilspycmd.cmd` from PowerShell; game DLLs in
  `<VALHEIM_INSTALL>\valheim_Data\Managed`).
- Git: `main` is the default branch. Work on a branch per phase or feature (e.g.
  `phase-2-hover-text`), commit in focused steps with clear messages, push when asked or as
  established in the session. `gh` is installed and logged in as angiebrr (if Git Bash can't
  find it, use `"/c/Program Files/GitHub CLI/gh.exe"`); create PRs only when the user asks, and
  they merge them. Write PR descriptions with this repo's `.github/pull_request_template.md`
  (adapted from the user's Athena template: no checkbox list, since this repo has no
  `specifications/` folder; testing is described in prose instead). Offer diagrams as PNGs to
  drag in.

## Reviewing code

When asked to review (or before calling a feature done), go through the whole change, not just
the last edit, and report findings ranked by impact before fixing anything the user hasn't
approved:

- **Correctness:** check assumptions against the decompiled game and mod code; look for missed
  cases (rebuilt UI, other mods writing the same text, re-entrancy from our own calls).
- **Leaks and lifetimes:** caches and dictionaries are bounded or keyed so they can't grow per
  world load; created GameObjects are destroyed when no longer used; event handlers are
  unsubscribed; runtime-created textures/sprites are protected from Valheim's unused-asset sweep
  (`HideFlags.DontUnloadUnusedAsset`) and destroyed on unload.
- **Per-frame cost:** code in `Update`/`UpdateIcons`/`UpdateCrosshair` paths does nothing when
  nothing changed; no per-call allocations (closures, method-group delegates, LINQ, `name`
  reads) on hot paths.
- **DRY:** one rule in one place (shared helpers, constants, a single source for names copied
  from other mods).
- **Docs:** each summary says briefly what the member is and how it does it, without restating
  the name, the parameters, or other docs, and without being verbose.
- **TDD and tests:** new logic came with tests written first; tests check behaviour (inputs and
  outcomes a user would notice) not implementation details; bool rows are commented; tests use
  realistic data (the fixture mapping, strings mods really write).
- **Jotunn / BepInEx / Harmony:** postfixes at low priority; patch targets covered by contract
  tests; config entries bound once with clear descriptions; changes to settings applied live where
  possible; client-side `NetworkCompatibility`; clean `OnDestroy`.
- **Unity:** Unity null checks (no `??`/`?.` on Unity objects); UI positioned in the right space
  (visible bounds, local vs world); no reliance on screen pixels, so it works at any resolution.
- **C#:** the style rules above; instance members preferred; explicit dependencies.
