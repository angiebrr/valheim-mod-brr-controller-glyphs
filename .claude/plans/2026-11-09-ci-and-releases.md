# CI, Releases and Thunderstore Publishing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GitHub Actions that build and test every PR and push to `main` with the game's assemblies, plus a manual release workflow that checks the package against Thunderstore's rules, tags, creates a GitHub release and publishes to Thunderstore.

**Architecture:** Release logic (version and file checks, changelog extraction, packaging, skipped-test detection, Steam build ID parsing) lives in one PowerShell module, `scripts/Release.psm1`, tested with Pester. A composite action `.github/actions/setup-valheim` fetches the Valheim Dedicated Server's `Managed` folder (cached by Steam build ID) and BepInEx/AzuEPI from Thunderstore, and writes `Environment.props`. `ci.yml` and `release.yml` call the action and the module; `publish.ps1` packages with the same module.

**Tech Stack:** GitHub Actions (ubuntu-latest), PowerShell 7 + Pester 5, SteamCMD, .NET 10 SDK, Thunderstore CLI (`tcli`), `gh`.

**Spec:** `.claude/specs/ci-and-releases.md`

## Global Constraints

- No game, BepInEx or other mods' files are committed; CI downloads them.
- Dedicated server: Steam app `896660`, anonymous login; cache key `valheim-server-<buildid>`; only `valheim_server_Data/Managed` is kept, placed at `<install>/valheim_Data/Managed`.
- Pinned packages: `denikson-BepInExPack_Valheim-5.4.2360` (matches `manifest.json`), `Azumatt-AzuExtendedPlayerInventory-2.6.6`; download URL `https://thunderstore.io/package/download/<team>/<name>/<version>/`.
- Version format `x.y.z` (digits only); `PluginVersion` must equal `manifest.json` `version_number`; tag `v<x.y.z>`.
- Thunderstore: zip files at the root (`manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `plugins/BrrControllerGlyphs.dll`); icon exactly 256 × 256 PNG; README/CHANGELOG UTF-8 without BOM; description ≤ 250 characters; `name` letters/digits/underscores; dependencies `Team-Package-x.y.z`.
- Placeholder icon SHA-256: `fc637d630b28edfe423c8c6131802a77439634ae95f9e13bdbff2fc58dac3369` (refused).
- Thunderstore publish: community `valheim`, categories `mods`, `client-side`, `tweaks`, `ai-generated`; team from repository variable `THUNDERSTORE_TEAM`; token from secret `TCLI_AUTH_TOKEN`; skipped with a notice if either is missing; not NSFW.
- Release modes: `dry-run` (default), `release`, `release + publish`.
- Repo conventions: script files start with a header comment like `scripts/make-glyphs.ps1`; markdown one line per paragraph; commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; build the solution, never a single project.

## Review Focus

- **Changelog headings that almost match:** version `1.0.1` must not match `## 1.0.10`; `## 1.0.1 - 2026-11-09` and `## v1.0.1` must match. Tests in Task 1.
- **Windows line endings:** a CRLF checkout of `BrrControllerGlyphs.cs`, `manifest.json` or `CHANGELOG.md` must parse the same as LF. Tests in Task 1.
- **Zip entry separators:** entries must use `/` (`plugins/BrrControllerGlyphs.dll`), also when Windows PowerShell 5.1 runs `publish.ps1`; backslash entries break mod managers. Test in Task 2.
- **Re-running a release after a failed publish:** an existing tag on the same commit continues; on another commit it fails. Tests in Task 1.
- **SteamCMD output changes or a failed query:** an unparsable `app_info_print` must fail the setup with a clear message, never fall back to a stale cache key. Test in Task 1 (`Get-SteamBuildId` throws).

---

### Task 1: Release checks module

**Files:**
- Create: `scripts/Release.psm1`
- Test: `scripts/tests/Release.Tests.ps1`

**Interfaces:**
- Produces (all exported from `scripts/Release.psm1`):
  - `Get-PluginVersion -SourcePath <string> -> string` (reads `public const string PluginVersion = "x.y.z";`)
  - `Get-ChangelogSection -ChangelogPath <string> -Version <string> -> string` (body under the matching `##` heading up to the next `##`, trimmed; `$null` if absent)
  - `Test-ReleaseReady -Root <string> -> string[]` (problems; empty when ready). Root is the repo root; reads `BrrControllerGlyphs/BrrControllerGlyphs.cs`, `BrrControllerGlyphs/Package/manifest.json`, `BrrControllerGlyphs/Package/icon.png`, `BrrControllerGlyphs/README.md`, `BrrControllerGlyphs/CHANGELOG.md`
  - `Test-TagUsable -Tag <string> -Commit <string> -> bool` (true if the tag doesn't exist or points at `Commit`; uses `git rev-parse`)
  - `Get-SkippedTestCount -TrxPath <string[]> -> int` (sums `notExecuted` from TRX `<Counters>`)
  - `Get-SteamBuildId -AppInfoText <string> -> string` (the `buildid` under `"branches"` → `"public"`; throws `"Couldn't read the public build ID from SteamCMD's app info"` if absent)

- [ ] **Step 1: Install Pester 5 locally if needed**

Run: `pwsh -NoProfile -Command "if (-not (Get-Module -ListAvailable Pester | Where-Object Version -ge 5.0.0)) { Install-Module Pester -MinimumVersion 5.5.0 -Scope CurrentUser -Force -SkipPublisherCheck }; (Get-Module -ListAvailable Pester)[0].Version"`
Expected: a 5.x version.

- [ ] **Step 2: Write the failing tests** in `scripts/tests/Release.Tests.ps1` (Pester 5, `BeforeAll { Import-Module "$PSScriptRoot/../Release.psm1" -Force }`, fixtures built in `$TestDrive`). Each `It` named as below:
  - `Get-PluginVersion reads the version` → `"0.1.0"` from a file containing `public const string PluginVersion = "0.1.0";`, with LF and with CRLF.
  - `Get-ChangelogSection returns the section's lines` → for `"## 0.1.0\n\n### Added\n- A\n\n## 0.0.1\n- B"` returns `"### Added`n- A"`.
  - `Get-ChangelogSection matches v-prefixed and dated headings` → `## v0.1.0` and `## 0.1.0 - 2026-11-09` both found for `0.1.0`.
  - `Get-ChangelogSection doesn't match a longer version` → `## 1.0.10` gives `$null` for `1.0.1`.
  - `Test-ReleaseReady passes a valid package` → `@()` for a fixture root with matching versions, a changelog section, a 256 × 256 PNG, README/CHANGELOG without BOM, description ≤ 250, valid name and dependencies. Fixture PNGs are crafted bytes (the 8-byte PNG signature, then an IHDR chunk with the width and height) so the tests run on Linux too.
  - One `It` per problem, each asserting the returned array contains a message with the given text: version mismatch (`"PluginVersion 0.1.0 doesn't match manifest version_number 0.2.0"`), bad version format (`"isn't x.y.z"`), missing changelog section (`"CHANGELOG.md has no section for 0.1.0"`), BOM in README (`"README.md starts with a BOM"`), BOM in CHANGELOG, description of 251 characters (`"description is 251 characters (max 250)"`), name with a space (`"name"`), dependency `BepInExPack-5.4` (`"dependency"`), 128 × 128 icon (`"icon.png is 128x128, must be 256x256"`), placeholder icon (copy the repo's current `icon.png`; `"placeholder"`).
  - `Test-TagUsable` (in a `git init` repo in `$TestDrive` with two commits): true for a missing tag; true for a tag on the given commit; false for a tag on the other commit.
  - `Get-SkippedTestCount sums notExecuted` → 3 for two TRX files with `notExecuted="1"` and `notExecuted="2"`.
  - `Get-SteamBuildId reads the public branch` → `"20123456"` from app info text containing `"branches" { "public" { "buildid" "20123456" ... } "beta" { "buildid" "1" } }`; throws the message above for text without it.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests -Output Detailed"`
Expected: FAIL (module functions not found).

- [ ] **Step 4: Implement the functions** in `scripts/Release.psm1` with the signatures above; `Export-ModuleMember` the six functions. Read files as bytes for the BOM check (`EF BB BF`), normalise `\r\n` to `\n` before parsing, and read the icon size from the PNG's IHDR bytes (offset 16, big-endian width then height) rather than System.Drawing, so it works on Linux.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests -Output Detailed"`
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/Release.psm1 scripts/tests/Release.Tests.ps1
git commit -m "Release checks module with Pester tests"
```

### Task 2: Package zip from one place

**Files:**
- Modify: `scripts/Release.psm1` (add one function)
- Modify: `scripts/publish.ps1` (Release branch uses it)
- Modify: `.gitignore` (`*/Package/CHANGELOG.md`)
- Create: `BrrControllerGlyphs/CHANGELOG.md` (the package needs it)
- Test: `scripts/tests/Release.Tests.ps1`

**Interfaces:**
- Consumes: Task 1's module.
- Produces: `New-ReleasePackage -Root <string> -DllPath <string> -OutFile <string> -> void`; writes a zip with exactly the entries `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `plugins/BrrControllerGlyphs.dll` (README and CHANGELOG from `BrrControllerGlyphs/`, manifest and icon from `BrrControllerGlyphs/Package/`). Uses `System.IO.Compression.ZipArchive` with explicit `/` entry names, overwriting `OutFile`.

- [ ] **Step 1: Write the failing test** `New-ReleasePackage writes the Thunderstore layout`: build a fixture root and a dummy DLL, call it, open the zip, assert the sorted entry names equal `@('CHANGELOG.md','README.md','icon.png','manifest.json','plugins/BrrControllerGlyphs.dll')` and that no entry name contains `\`.

- [ ] **Step 2: Run to verify it fails**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests -Output Detailed"`
Expected: FAIL on the new test.

- [ ] **Step 3: Implement `New-ReleasePackage`**; then in `publish.ps1`'s Release branch replace the copy/`Compress-Archive` lines with `Import-Module "$PSScriptRoot\Release.psm1"` and `New-ReleasePackage -Root "$ProjectPath\.." -DllPath "$TargetPath\$TargetAssembly" -OutFile "$TargetPath\$name.zip"`. Add `*/Package/CHANGELOG.md` to `.gitignore` next to the README line.

- [ ] **Step 4: Write `BrrControllerGlyphs/CHANGELOG.md`** (UTF-8 without BOM): title `# Changelog`, then `## 0.0.1` with `### Added` listing the features as on the mod page: controller glyphs kept on after keyboard or mouse input, mapped buttons for keyboard keys, hotbar and quick-slot labels with groups, other mods' hover hints, Steam Deck glyph art, the missing-keys check, live mapping reload.

- [ ] **Step 5: Run tests and a Release build**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests -Output Detailed"` then `dotnet build BrrControllerGlyphs.sln -c Release`
Expected: all PASS; "Build succeeded"; `BrrControllerGlyphs/bin/Release/net48/BrrControllerGlyphs.zip` has the five entries.

- [ ] **Step 6: Commit**

```bash
git add scripts/Release.psm1 scripts/tests/Release.Tests.ps1 scripts/publish.ps1 .gitignore BrrControllerGlyphs/CHANGELOG.md
git commit -m "Package the Thunderstore zip in one place, with / entry names"
```

### Task 3: Game files setup action and CI workflow

**Files:**
- Create: `.github/ci-packages.json`
- Create: `.github/actions/setup-valheim/action.yml`
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `Get-SteamBuildId`, `Get-SkippedTestCount` (Task 1).
- Produces: composite action `./.github/actions/setup-valheim` with no inputs; after it runs, `Environment.props` exists at the repo root and `dotnet build BrrControllerGlyphs.sln` works. Output `server-build-id`.

- [ ] **Step 1: Write `.github/ci-packages.json`**: `{ "BepInExPack": "denikson-BepInExPack_Valheim-5.4.2360", "AzuEPI": "Azumatt-AzuExtendedPlayerInventory-2.6.6" }`.

- [ ] **Step 2: Write the action** (shell `pwsh` unless noted):
  1. Install SteamCMD (`steamcmd/steamcmd` isn't needed: download `https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz` into `$RUNNER_TEMP/steamcmd`).
  2. Run `steamcmd.sh +login anonymous +app_info_update 1 +app_info_print 896660 +quit`, pass the output to `Get-SteamBuildId`, set output `server-build-id`.
  3. `actions/cache@v4` with path `${{ runner.temp }}/valheim/valheim_Data/Managed`, key `valheim-server-<build id>`.
  4. On a cache miss: `steamcmd.sh +force_install_dir $RUNNER_TEMP/server +login anonymous +app_update 896660 validate +quit`, then move `server/valheim_server_Data/Managed` to `valheim/valheim_Data/Managed` and delete the rest of the server.
  5. Download both packages from `.github/ci-packages.json`; unzip BepInExPack's `BepInExPack_Valheim/BepInEx` into `$RUNNER_TEMP/profile/BepInEx`, AzuEPI into `$RUNNER_TEMP/profile/BepInEx/plugins/Azumatt-AzuExtendedPlayerInventory`.
  6. Write `Environment.props` at `$GITHUB_WORKSPACE` with `VALHEIM_INSTALL=$RUNNER_TEMP/valheim`, `GALE_PROFILE=$RUNNER_TEMP/profile`, `BEPINEX_PATH=$(GALE_PROFILE)/BepInEx`, `MOD_DEPLOYPATH=$RUNNER_TEMP/deploy` (same shape as the README's example).

- [ ] **Step 3: Write `ci.yml`**: triggers `pull_request`, `push: branches: [main]`, `workflow_dispatch`; `concurrency: { group: ci-${{ github.ref }}, cancel-in-progress: true }`; job on `ubuntu-latest`: checkout, `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x`, the setup action, `dotnet build BrrControllerGlyphs.sln -c Release`, `dotnet test BrrControllerGlyphs.sln -c Release --no-build --logger trx --results-directory TestResults`, a pwsh step failing when `Get-SkippedTestCount (Get-ChildItem TestResults/*.trx)` is not 0 (message: `"<n> tests were skipped; CI must run them all"`), a pwsh step running `Invoke-Pester scripts/tests -CI`, and `actions/upload-artifact@v4` of `BrrControllerGlyphs/bin/Release/net48/BrrControllerGlyphs.dll`.

- [ ] **Step 4: Push the branch, open a draft PR (the user asked for these workflows; the PR is where `pull_request` runs) and check the run**

Run: `git push -u origin ci-and-releases` then `gh pr create --draft --fill`, then read the run with `gh run watch`.
Expected first-run check (the spec's first task): the build finds every assembly in the server's `Managed` folder. **If a reference is missing (e.g. `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`, `UnityEngine.TextCoreFontEngineModule`), stop and report to the user before any further task.** Then: build succeeds, 0 skipped, all tests and Pester pass, the DLL artifact is uploaded. A second run (re-run) restores `valheim-server-<id>` from the cache without downloading the server. Once, by hand: rename the game folder in a throwaway commit (or set a bogus `VALHEIM_INSTALL`) on the branch to see the no-skip step fail the job, then drop that commit.

- [ ] **Step 5: Commit** (before Step 4's push)

```bash
git add .github/ci-packages.json .github/actions/setup-valheim/action.yml .github/workflows/ci.yml
git commit -m "CI: build and test with the dedicated server's assemblies"
```

### Task 4: Release workflow

**Files:**
- Create: `.github/workflows/release.yml`
- Create: `thunderstore.toml`

**Interfaces:**
- Consumes: setup action (Task 3); `Test-ReleaseReady`, `Get-PluginVersion`, `Get-ChangelogSection`, `Test-TagUsable`, `Get-SkippedTestCount`, `New-ReleasePackage` (Tasks 1–2).

- [ ] **Step 1: Write `thunderstore.toml`**: `[general] repository = "https://thunderstore.io"`, `[publish] communities = ["valheim"]`, `[publish.categories] valheim = ["mods", "client-side", "tweaks", "ai-generated"]`. Check against `tcli publish --help` whether `[package]` keys are required with `--file`; if so add `namespace`/`name`/`versionNumber` placeholders overridden by the CLI flags below.

- [ ] **Step 2: Write `release.yml`**: `workflow_dispatch` with input `mode` (choice: `dry-run`, `release`, `release + publish`; default `dry-run`); `permissions: contents: write`; job on `ubuntu-latest`, fails unless `github.ref == 'refs/heads/main'`:
  1. checkout with `fetch-depth: 0` (tags), setup-dotnet, setup action.
  2. pwsh: `Test-ReleaseReady -Root .`; any problem → `::error::` per problem and exit 1. Read the version (`Get-PluginVersion`), set `version`/`tag` outputs; unless `dry-run`, fail if `Test-TagUsable "v$version" $env:GITHUB_SHA` is false.
  3. Build Release, test with TRX, no-skip check (same as CI).
  4. `New-ReleasePackage` → `BrrControllerGlyphs-<version>.zip`; upload as artifact.
  5. Unless `dry-run`: `Get-ChangelogSection` → notes file; create the tag if missing and `git push origin v<version>`; `gh release view` → if missing `gh release create v<version> <zip> --title "BrrControllerGlyphs <version>" --notes-file notes.md`, else `gh release upload v<version> <zip> --clobber`.
  6. If `release + publish`: if `vars.THUNDERSTORE_TEAM` or `secrets.TCLI_AUTH_TOKEN` is empty, write a `::notice::` and skip; else `dotnet tool install -g tcli` and `tcli publish --file <zip> --package-namespace "$THUNDERSTORE_TEAM" --package-name BrrControllerGlyphs --package-version <version>` with `TCLI_AUTH_TOKEN` in the environment.

- [ ] **Step 3: Validate the workflow files**

Run: the `actionlint` release binary (downloaded into the scratchpad) on `.github/workflows/`.
Expected: no errors. (The dispatch itself can only run once the file is on `main`; the dry-run is in Task 6.)

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/release.yml thunderstore.toml
git commit -m "Release workflow: checks, zip, tag, GitHub release, Thunderstore publish"
```

### Task 5: Mod page and changelog for Thunderstore

**Files:**
- Modify: `BrrControllerGlyphs/README.md`

**Interfaces:**
- Consumes: `Test-ReleaseReady` (Task 1) as the check.

- [ ] **Step 1: Run the check on the repo to see the current problems**

Run: `pwsh -NoProfile -Command "Import-Module ./scripts/Release.psm1; Test-ReleaseReady -Root ."`
Expected: exactly two problems, the README BOM and the placeholder icon.

- [ ] **Step 2: Edit the mod page**:
  - `> [!IMPORTANT]` + next line → `> **Important:** <the user's text>` on one line.
  - Delete the table of contents block.
  - Replace in-page links `[text](#anchor)` with plain text: `see the *Checking your mapping against your mods* section for more`.
  - `<summary>` lines: drop `<code>`/`</code>`, keep the file name as plain text.
  - Delete the `## Changelog` section (Thunderstore shows CHANGELOG.md in its own tab).
  - Add before `## Credits` the user-approved section, verbatim:

```markdown
## Made with AI help

I built this mod with help from Claude Code, Anthropic's AI coding assistant. I designed it, decided how everything should look and behave, and tested each feature in game with my own Steam Controller setup; the AI helped write the code and these docs.

It isn't a one-shot generated mod. It was built from a written design, feature by feature, with:

- about 360 automated tests for its logic, plus checks that every game method it relies on still exists after a Valheim update;
- only additive game patches (postfixes), so it doesn't replace Valheim's code, and each feature turns itself off safely if a game update breaks it;
- the full source on GitHub, if you'd like to look.
```

  - Save without BOM (UTF-8, `[System.IO.File]::WriteAllText(path, text, [System.Text.UTF8Encoding]::new($false))`).

- [ ] **Step 3: Check and test**

Run: `grep -c "](#" BrrControllerGlyphs/README.md` → `0`; the `Test-ReleaseReady` command from Step 1 → only the placeholder-icon problem left; `dotnet build BrrControllerGlyphs.sln -c Debug` then `dotnet test BrrControllerGlyphs.sln --no-build` → all pass (`ExampleMappingsTests` still finds each example on the page).

- [ ] **Step 4: Commit**

```bash
git add BrrControllerGlyphs/CHANGELOG.md BrrControllerGlyphs/README.md
git commit -m "Mod page and CHANGELOG.md for Thunderstore's Markdown and rules"
```

### Task 6: Docs, PR and first dry run

**Files:**
- Modify: `README.md` (developer README: a "CI and releases" section)
- Modify: `CLAUDE.md` (Build, test, deploy: CI, Pester, release routine pointer)
- Modify: `.claude/specs/ci-and-releases.md` (Status, findings with dates)

- [ ] **Step 1: Developer README**: CI runs on PRs and `main` (what it downloads, the no-skip rule, the DLL artifact); `Invoke-Pester scripts/tests` for the release scripts; the release routine from the spec (4 steps); the `THUNDERSTORE_TEAM` variable and `TCLI_AUTH_TOKEN` secret and where to get the token; updating `.github/ci-packages.json`.

- [ ] **Step 2: CLAUDE.md**: under Build, test, deploy add `Invoke-Pester scripts/tests` and "CI must pass before merging"; one line pointing to the developer README's release routine.

- [ ] **Step 3: Spec**: Status → implemented, with any findings from Task 3's first run (dated), e.g. which assemblies the server has.

- [ ] **Step 4: Commit, push, mark the PR ready**

```bash
git add README.md CLAUDE.md .claude/specs/ci-and-releases.md
git commit -m "Docs: CI and the release routine"
git push
```
Then update the PR description with the repo's template and mark it ready (`gh pr ready`). CI must be green.

- [ ] **Step 5: After the user merges:** run Release in `dry-run` on `main` (`gh workflow run release.yml -f mode=dry-run`), check the checks fail only on the placeholder icon (expected until the user adds a real icon), and tell the user. With a real icon, the dry run's zip goes to the user to import into Gale.
