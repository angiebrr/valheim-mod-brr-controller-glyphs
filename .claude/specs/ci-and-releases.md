# Spec: CI, releases and Thunderstore publishing

Status: implemented 2026-11-10 (PR #8); plan in `.claude/plans/2026-11-09-ci-and-releases.md`. Publishing waits on a real icon and the Thunderstore team.
Last updated: 2026-11-10
Related: `.claude/specs/controller-glyphs.md` (the mod itself; F9 has the mod page plan)

## Goal

- Every pull request and push to `main` builds the whole solution and runs every test (Core and contract) on GitHub, with no game files in the repo.
- A release is one manual workflow run: it checks the package against Thunderstore's rules, builds it, tags it, creates a GitHub release with the zip, and publishes the same zip to Thunderstore.
- The package passes Thunderstore's upload rules (checked 2026-11-09 against the wiki: Your First Upload, Packaging Your Mods, Updating a Package, Markdown Guide, Global Rules; and the Thunderstore CLI wiki).

## Decisions (2026-11-09)

- **Full build in CI** (not Core only): the plugin and contract tests need the game's assemblies, so CI downloads them.
- **GitHub-hosted Ubuntu runners**, game files downloaded per run and cached. Rejected: a self-hosted runner on the user's PC (must be on; a public repo's PRs could run code on it) and building releases locally.
- **Version bumped in a PR, read by the workflow.** No bot commits to `main`; the changelog is written and reviewed like code.
- **Thunderstore team not decided yet.** It's a repository variable; until it's set, the workflow does everything except the upload. The team name can't be changed later.
- **Dedicated `CHANGELOG.md`** packaged with the mod (Thunderstore shows it in its own tab); the mod page's Changelog section goes.
- **AI disclosure:** the mod page says AI helped build it, and the package uses Valheim's `AI Generated` category, as the Global Rules ask for packages with AI-generated code.

## Part 1: game files and CI

### Setup action (`.github/actions/setup-valheim/action.yml`, composite)

1. **Game assemblies** from the Valheim Dedicated Server (Steam app 896660, anonymous SteamCMD login):
   - Read the current build ID (`app_info_print`). The cache key is `valheim-server-<buildid>`; on a hit, skip the download.
   - On a miss, download the server and keep only `valheim_server_Data/Managed` (cached).
   - Place it as `<install>/valheim_Data/Managed`, the path Jötunn's `Paths.props`/`JotunnBuildTask` and the contract tests (`GameAssemblies.ManagedDir`) expect.
2. **BepInEx and AzuEPI** downloaded into a stand-in profile: `<profile>/BepInEx/core` (BepInExPack_Valheim) and `<profile>/BepInEx/plugins/Azumatt-AzuExtendedPlayerInventory`. `.github/ci-packages.json` holds their direct download URLs and SHA-256 hashes (checked after download, so a replaced file fails loudly): BepInExPack from Thunderstore (matching the `manifest.json` dependency), AzuEPI from Hexium (see Findings).
3. **`Environment.props`** written to the repo root on the runner: `VALHEIM_INSTALL`, `GALE_PROFILE` (the stand-in profile), `BEPINEX_PATH`, `MOD_DEPLOYPATH` (a temp folder). The post-build deploy (`publish.ps1`) only runs on Windows, so nothing is deployed.
4. **Checked on the first CI run (2026-11-10):** check the server's `Managed` folder has every assembly the plugin references (`Unity.TextMeshPro`, `Unity.InputSystem`, `UnityEngine.UI`, `UnityEngine.TextCoreFontEngineModule`, the publicized `assembly_*`). If any is missing, stop and ask the user before going further.

### CI workflow (`.github/workflows/ci.yml`)

- **Triggers:** `pull_request`, `push` to `main`. Concurrency per branch, cancelling older runs.
- **Steps:** checkout, .NET 10 SDK, setup action, `dotnet build BrrControllerGlyphs.sln -c Release`, `dotnet test BrrControllerGlyphs.sln -c Release --no-build`.
- **No silent skips:** the job fails if any test was skipped (the contract tests skip without the game or a mod).
- **Artifact:** uploads `BrrControllerGlyphs.dll`, so a PR's build can be tried in game.
- **Windows PowerShell 5.1 job** (added 2026-11-10 from the final review): runs the release script tests where `publish.ps1` runs them. Jobs time out after 30 minutes (Windows: 15), so a hung SteamCMD can't hold a runner for hours.

## Part 2: release workflow (`.github/workflows/release.yml`)

Started by hand (`workflow_dispatch`) on `main`. Input **mode**: `dry-run` (default), `release`, `release + publish`.

### Checks (all modes; the run fails with a clear message on the first one that fails)

- `PluginVersion` (`BrrControllerGlyphs.cs`) equals `version_number` (`manifest.json`), in `x.y.z` form (no suffixes; Thunderstore compares numerically).
- `BrrControllerGlyphs/CHANGELOG.md` has a `## x.y.z` section for that version.
- `manifest.json`: `name` is letters, digits and underscores; `description` is 250 characters or less; `dependencies` are `Team-Package-x.y.z`.
- `icon.png` is exactly 256 × 256 PNG and not the JotunnModStub placeholder (compared by hash).
- `README.md`, `CHANGELOG.md` and `manifest.json` are UTF-8 without BOM.
- Tag `vx.y.z` doesn't exist, or already points at this commit (so a failed run can be re-run).

### Build and package

- Setup action, Release build, all tests (same no-skip rule as CI).
- Zip with the files at its root: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `plugins/BrrControllerGlyphs.dll` (the `plugins` folder installs to `BepInEx/plugins`). No game or BepInEx files.
- **dry-run** stops here and uploads the zip as an artifact, for the user to try with Gale's "Import local mod" before a real release.

### Release (`release` and `release + publish`)

- Tag `vx.y.z` and create the GitHub release; notes are that version's `CHANGELOG.md` section; the zip is attached (re-runs replace the asset).

### Publish (`release + publish` only)

- Thunderstore CLI (`dotnet tool install -g tcli`): `tcli publish --file <zip>`, with `thunderstore.toml` giving the repository, the `valheim` community and categories `mods`, `client-side`, `tweaks`, `ai-generated`. Team from `THUNDERSTORE_TEAM`, written into `namespace` in a copy of `thunderstore.toml` (tcli won't take `--package-namespace` with `--file`); version and dependencies come from the zip's `manifest.json`.
- Token: repository secret `TCLI_AUTH_TOKEN`, from a team service account (thunderstore.io: Settings > Teams > team > Service Accounts > Add service account).
- If `THUNDERSTORE_TEAM` or the secret isn't set, the step is skipped with a notice (the GitHub release still happens).
- The upload is not marked NSFW.

### Release routine (goes in the developer README)

1. A PR bumps `PluginVersion` and `manifest.json` and adds the `CHANGELOG.md` section.
2. Merge it; CI runs on `main`.
3. Run Release in `dry-run`, import the zip in Gale, check it in game.
4. Run Release in `release + publish`.

## Part 3: making the package fit Thunderstore

- **`BrrControllerGlyphs/CHANGELOG.md`:** newest first, `## x.y.z` per version with Added / Changed / Fixed lists; starts with `0.0.1` (or the first published version). The mod page's Changelog section is removed. `publish.ps1`'s Release build and the Release workflow both write the zip with `New-ReleasePackage`, straight from the sources (nothing is copied into `Package/`).
- **Mod page fixes** (Thunderstore's Markdown, per its guide):
  - `> [!IMPORTANT]` isn't supported: becomes `> **Important:** …`.
  - In-page heading links don't work: remove the table of contents; links like "see Checking your mapping" become plain text ("see the *Checking your mapping against your mods* section for more").
  - `<summary>` text without `<code>` tags (not on the supported list).
  - Save without BOM.
- **AI disclosure:** a short "Made with AI help" section before Credits (wording approved by the user), saying the user designed, directed and tested the mod in game and that AI helped write the code and docs, with the concrete quality evidence (written design, tests, contract tests, postfix-only patches, open source).
- **Icon:** the current `icon.png` is the stub's "MOD STUB" placeholder. The user makes or chooses a real 256 × 256 PNG before the first publish; the release check refuses the placeholder.

## Testing the workflows

- CI: its own PR shows it building and running all tests on GitHub (and that the skip check fails when a test is forced to skip, checked once by hand).
- Release: a `dry-run` on `main` after merging; the zip is imported into Gale and checked in game. A real release only when the user starts one.

## Findings (2026-11-10)

- **The dedicated server has everything the plugin builds against.** CI built the plugin on Ubuntu against the server's `Managed` folder (server build 26266951, 135 files), and all 29 contract tests passed against it.
- **AzuEPI moved to Hexium.** Azumatt now publishes AzuExtendedPlayerInventory on Hexium (valheim.hexium.gg, a Valheim mod site Gale supports); the Thunderstore listing is deprecated at 2.4.14 and 2.6.6 isn't there. CI downloads 2.6.6 from Hexium's CDN (`https://cdn.hexium.gg/upload/11/2.6.6.zip`, the same DLL as the Gale profile). Hexium serves a Thunderstore-style package list at `https://valheim.hexium.gg/api/v1/package/`. Whether to also publish BrrControllerGlyphs on Hexium is an open question for the user.
- **tcli 0.2.4** won't combine `--file` with `--package-namespace`, so the team goes into a copy of `thunderstore.toml`.
- **TRX file names** from `dotnet test` contain brackets (`..._net10.0[1].trx`); PowerShell must read them with `-LiteralPath`.

## Out of scope

- Automatic version bumps or changelog generation.
- Publishing anywhere but Thunderstore and GitHub releases.
- Signing, or checking the published package on Thunderstore after upload.
