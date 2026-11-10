# BrrControllerGlyphs

A BepInEx/Jötunn mod for Valheim that keeps controller glyphs on for Steam Input layouts that also send keyboard keys, and shows those keys as the controller buttons that send them.

The player-facing description (features, setup, settings) is the mod's Thunderstore page:

- [BrrControllerGlyphs/README.md](BrrControllerGlyphs/README.md). 

The design, decisions and in-game findings are in the spec: 

- [.claude/specs/controller-glyphs.md](.claude/specs/controller-glyphs.md).

## Layout

| Folder | What's in it |
|---|---|
| `BrrControllerGlyphs.Core/` | All decision logic, `netstandard2.0`, no Unity or Valheim types: the key mapping, prompt and hint rewriting, hotbar grouping, colors, visibility rules. |
| `BrrControllerGlyphs/` | The plugin (`net48`): a thin shell that reads game state, calls Core and sets text. Harmony postfixes in `Patches/`, glyph PNGs in `Glyphs/`, Thunderstore files in `Package/`. Core's sources are compiled in, so only `BrrControllerGlyphs.dll` ships. |
| `BrrControllerGlyphs.Core.Tests/` | xUnit tests for Core (`net10.0`), using the mapping in `Fixtures/steam-controller.yaml`. |
| `BrrControllerGlyphs.Contract.Tests/` | Reads the installed game DLLs and other mods' DLLs to check every member the plugin patches or reads still exists. Skips when the game or mod isn't installed. |
| `scripts/` | `publish.ps1` (post-build deploy and packaging), `Release.psm1` (release checks and the Thunderstore zip; Pester tests in `scripts/tests/`) and `make-glyphs.ps1` (makes `Glyphs/` from Kenney's Input Prompts). |
| `.github/` | The CI and Release workflows, the `setup-valheim` action that fetches the game's assemblies, and `ci-packages.json` (BepInEx and AzuEPI download links for CI). |
| `thunderstore.toml` | Thunderstore CLI settings for publishing (community, categories). |
| `libraries/` | `pdb2mdb.exe`, used by `publish.ps1` for debug symbols. |
| `docs/images/` | Screenshots for the mod page, with the list of shots in its README. |
| `docs/example-mappings/` | Example mapping files (no groups, one group, multiple groups) for the hotbar screenshots; `ExampleMappingsTests` checks they group as named and that the mod page shows each one word for word (update both together). |

## Setup

1. Install the .NET SDK (10 or later) and Valheim with a BepInEx profile (the author uses Gale).
2. Create `Environment.props` in the repo root (it's git-ignored):

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
     <PropertyGroup>
       <VALHEIM_INSTALL>C:\Program Files (x86)\Steam\steamapps\common\Valheim</VALHEIM_INSTALL>
       <!-- Gale profile: BepInEx lives here, not in the game folder -->
       <GALE_PROFILE>%APPDATA%\com.kesomannen.gale\valheim\profiles\Your Profile</GALE_PROFILE>
       <BEPINEX_PATH>$(GALE_PROFILE)\BepInEx</BEPINEX_PATH>
       <!-- Debug builds are copied here -->
       <MOD_DEPLOYPATH>$(BEPINEX_PATH)\plugins</MOD_DEPLOYPATH>
     </PropertyGroup>
   </Project>
   ```

   Use the full path in place of `%APPDATA%`. `DoPrebuild.props` makes Jötunn generate publicized game assemblies on the first build.
3. To read Valheim's or another mod's code, install ilspycmd (`dotnet tool install -g ilspycmd`) and decompile from `<VALHEIM_INSTALL>\valheim_Data\Managed` into a folder outside the repo.

## Design rules in short

- Postfixes only, at low priority; never change Valheim's input mode.
- Each patch catches its own errors; a patch that can't apply turns off only its feature.
- Logic lives in Core with tests; the plugin stays thin.
- Other mods are read at runtime, never referenced at compile time.
- Everything the mod adds to the UI is removed when glyphs turn off.

[CLAUDE.md](CLAUDE.md) has the full rules and code style; the spec has the reasoning.

## Build, test, deploy

Always build the solution: Jötunn's build props need `$(SolutionDir)`.

```bash
dotnet build BrrControllerGlyphs.sln -c Debug
```

```bash
dotnet test BrrControllerGlyphs.sln --no-build
```

- **Debug** builds copy the DLL and symbols to `<MOD_DEPLOYPATH>\BrrControllerGlyphs`. If Valheim is running it locks the DLL: the build still succeeds but the copy fails, so check the output. Restart the game to load a new build.
- **Release** builds also write the Thunderstore zip (`BrrControllerGlyphs/bin/Release/net48/BrrControllerGlyphs.zip`), the same one the Release workflow publishes: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md` and `plugins/BrrControllerGlyphs.dll`.
- **Release scripts** have their own tests: `Invoke-Pester scripts/tests` (Pester 5 or later).

## Testing

- **Unit tests** cover all of Core, written test-first.
- **Contract tests** catch game and mod updates: run them after Valheim or a supported mod updates, before starting the game.
- **In game:** `brrcg_selftest` in the F5 console logs PASS/FAIL/INFO lines starting with `[brrcg_selftest]` to `LogOutput.log`. Then check prompts, the hotbar and hover hints in both controller and keyboard/trackpad input.

## CI and releases

**CI** (`.github/workflows/ci.yml`) runs on every pull request and push to `main`. It downloads the free Valheim Dedicated Server with SteamCMD and keeps its `Managed` folder, cached by the server's Steam build ID so it's fetched again only after a Valheim update (or after a week without runs). BepInExPack (Thunderstore) and AzuEPI (Hexium, where Azumatt publishes it now) come from the links in `.github/ci-packages.json`; update those when a newer version should be tested. CI builds the solution, runs every test, fails if any test was skipped (the contract tests skip without the game), runs the release script tests, and uploads the built DLL so a PR can be tried in game.

**Releases** use the Release workflow (Actions tab > Release > Run workflow, on `main`):

1. In a PR, bump `PluginVersion` (`BrrControllerGlyphs/BrrControllerGlyphs.cs`) and `version_number` (`BrrControllerGlyphs/Package/manifest.json`), and add a `## x.y.z` section to `BrrControllerGlyphs/CHANGELOG.md`. Merge it.
2. Run Release in **dry-run**: it checks the package against Thunderstore's rules, builds, tests and uploads the zip as an artifact. Import the zip in Gale (Import local mod) and check it in game.
3. Run Release in **release + publish** (or **release** for a GitHub release only): it tags `vx.y.z`, creates the GitHub release with the changelog section and the zip, and uploads the same zip to Thunderstore. A failed run can be re-run; it reuses the tag and release.

Publishing needs two repository settings (Settings > Secrets and variables > Actions): the variable `THUNDERSTORE_TEAM` (the Thunderstore team; it can't be renamed later) and the secret `TCLI_AUTH_TOKEN` (thunderstore.io: Settings > Teams > your team > Service Accounts > Add service account). Without them the publish step is skipped with a notice.
## Glyph art

`Glyphs/*.png` are made by `scripts/make-glyphs.ps1` from Kenney's [Input Prompts](https://kenney.nl/assets/input-prompts) (CC0) and committed. To remake them, unzip the pack outside the repo and run:

```bash
pwsh scripts/make-glyphs.ps1 -KenneyDir <unzipped folder>
```

## License

See [LICENSE](LICENSE). Glyph art: CC0 ([BrrControllerGlyphs/Glyphs/License.txt](BrrControllerGlyphs/Glyphs/License.txt)).
