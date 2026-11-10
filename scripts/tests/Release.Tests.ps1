# ------------------------------------------------------------------------------------------------
# Release.Tests.ps1: Pester tests for scripts/Release.psm1 (release checks and packaging).
# ------------------------------------------------------------------------------------------------
#
# Run: pwsh -NoProfile -Command "Invoke-Pester scripts/tests -Output Detailed"

BeforeAll {
    Import-Module "$PSScriptRoot/../Release.psm1" -Force

    # Writes text as UTF-8, with or without a BOM, and with LF or CRLF line endings.
    function Write-Text([string] $Path, [string] $Text, [switch] $Bom, [switch] $Crlf) {
        New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
        if ($Crlf) { $Text = $Text -replace "`r?`n", "`r`n" }
        [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new([bool]$Bom))
    }

    # A PNG's first bytes: the signature and an IHDR chunk giving the width and height.
    function New-PngBytes([int] $Width, [int] $Height) {
        $bytes = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52) + (New-Object byte[] 17)
        [byte[]] $w = [BitConverter]::GetBytes([int]$Width); [array]::Reverse($w)
        [byte[]] $h = [BitConverter]::GetBytes([int]$Height); [array]::Reverse($h)
        [array]::Copy($w, 0, $bytes, 16, 4)
        [array]::Copy($h, 0, $bytes, 20, 4)
        return $bytes
    }

    # A repo-shaped folder with a package that passes every check; parameters break one thing each.
    function New-FixtureRoot {
        param(
            [string] $PluginVersion = '0.1.0',
            [string] $ManifestVersion = '0.1.0',
            [string] $Changelog = "# Changelog`n`n## 0.1.0`n`n### Added`n- Things`n",
            [string] $Name = 'BrrControllerGlyphs',
            [string] $Description = 'Keeps controller glyphs on.',
            [string[]] $Dependencies = @('denikson-BepInExPack_Valheim-5.4.2360'),
            [int] $IconSize = 256,
            [byte[]] $IconBytes,
            [switch] $ReadmeBom,
            [switch] $ChangelogBom
        )
        $root = Join-Path $TestDrive ([guid]::NewGuid())
        $mod = Join-Path $root 'BrrControllerGlyphs'
        Write-Text "$mod/BrrControllerGlyphs.cs" "        public const string PluginVersion = `"$PluginVersion`";`n"
        $manifest = [ordered]@{ name = $Name; description = $Description; version_number = $ManifestVersion; website_url = ''; dependencies = $Dependencies }
        Write-Text "$mod/Package/manifest.json" ($manifest | ConvertTo-Json)
        if (-not $IconBytes) { $IconBytes = New-PngBytes $IconSize $IconSize }
        [System.IO.File]::WriteAllBytes("$mod/Package/icon.png", $IconBytes)
        Write-Text "$mod/README.md" "# BrrControllerGlyphs`n" -Bom:$ReadmeBom
        Write-Text "$mod/CHANGELOG.md" $Changelog -Bom:$ChangelogBom
        return $root
    }
}

Describe 'Get-PluginVersion' {
    It 'reads the version with <Name> line endings' -ForEach @(@{ Name = 'LF'; Crlf = $false }, @{ Name = 'CRLF'; Crlf = $true }) {
        $path = Join-Path $TestDrive "Plugin$Name.cs"
        Write-Text $path "namespace X`n{`n    public const string PluginVersion = `"0.1.0`";`n}`n" -Crlf:$Crlf

        Get-PluginVersion -SourcePath $path | Should -Be '0.1.0'
    }
}

Describe 'Get-ChangelogSection' {
    BeforeAll {
        $path = Join-Path $TestDrive 'CHANGELOG.md'
        Write-Text $path "# Changelog`n`n## v0.2.0 - 2026-11-09`n`n- Dated`n`n## 0.1.0`n`n### Added`n- A`n`n## 1.0.10`n`n- Ten`n" -Crlf
    }

    It "returns the section's lines" {
        Get-ChangelogSection -ChangelogPath $path -Version '0.1.0' | Should -Be "### Added`n- A"
    }

    It 'matches v-prefixed and dated headings' {
        Get-ChangelogSection -ChangelogPath $path -Version '0.2.0' | Should -Be '- Dated'
    }

    It "doesn't match a longer version" {
        Get-ChangelogSection -ChangelogPath $path -Version '1.0.1' | Should -BeNullOrEmpty
    }

    It 'returns nothing for a heading with no lines before the next one' {
        $empty = Join-Path $TestDrive 'CHANGELOG-empty.md'
        Write-Text $empty "# Changelog`n`n## 1.0.2`n## 1.0.1`n`n- Old`n"

        Get-ChangelogSection -ChangelogPath $empty -Version '1.0.2' | Should -BeNullOrEmpty
    }

    It 'returns nothing for a heading on the last line' {
        $last = Join-Path $TestDrive 'CHANGELOG-last.md'
        Write-Text $last "# Changelog`n`n## 1.0.2"

        Get-ChangelogSection -ChangelogPath $last -Version '1.0.2' | Should -BeNullOrEmpty
    }
}

Describe 'Test-ReleaseReady' {
    It 'passes a valid package' {
        Test-ReleaseReady -Root (New-FixtureRoot) | Should -BeNullOrEmpty
    }

    # release.yml collects the problems with @(...) and fails when there are any
    It 'returns one item per problem' {
        @(Test-ReleaseReady -Root (New-FixtureRoot)).Count | Should -Be 0
        @(Test-ReleaseReady -Root (New-FixtureRoot -ReadmeBom -ChangelogBom)).Count | Should -Be 2
    }

    It 'reports a BOM in the manifest' {
        $root = New-FixtureRoot
        $manifest = "$root/BrrControllerGlyphs/Package/manifest.json"
        Write-Text $manifest ([System.IO.File]::ReadAllText($manifest)) -Bom

        ((Test-ReleaseReady -Root $root) -join "`n") | Should -BeLike '*manifest.json starts with a BOM*'
    }

    It 'reports <Case>' -ForEach @(
        @{ Case = 'a version mismatch'; Fixture = @{ PluginVersion = '0.1.0'; ManifestVersion = '0.2.0'; Changelog = "## 0.1.0`n- A`n## 0.2.0`n- B`n" }; Text = "PluginVersion 0.1.0 doesn't match manifest version_number 0.2.0" }
        @{ Case = 'a version not in x.y.z form'; Fixture = @{ PluginVersion = '0.1.0-beta'; ManifestVersion = '0.1.0-beta' }; Text = "isn't x.y.z" }
        @{ Case = 'a missing changelog section'; Fixture = @{ Changelog = "# Changelog`n`n## 0.0.1`n- Old`n" }; Text = 'CHANGELOG.md has no section for 0.1.0' }
        @{ Case = 'a BOM in the README'; Fixture = @{ ReadmeBom = $true }; Text = 'README.md starts with a BOM' }
        @{ Case = 'a BOM in the changelog'; Fixture = @{ ChangelogBom = $true }; Text = 'CHANGELOG.md starts with a BOM' }
        @{ Case = 'a long description'; Fixture = @{ Description = 'x' * 251 }; Text = 'description is 251 characters (max 250)' }
        @{ Case = 'a name with a space'; Fixture = @{ Name = 'Brr Controller' }; Text = 'name' }
        @{ Case = 'a malformed dependency'; Fixture = @{ Dependencies = @('BepInExPack-5.4') }; Text = 'dependency' }
        @{ Case = 'a small icon'; Fixture = @{ IconSize = 128 }; Text = 'icon.png is 128x128, must be 256x256' }
    ) {
        $problems = Test-ReleaseReady -Root (New-FixtureRoot @Fixture)

        ($problems -join "`n") | Should -BeLike "*$Text*"
    }

    It 'reports the placeholder icon' {
        # The JotunnModStub icon, kept as a fixture so the real icon can replace it in the package
        $stub = [System.IO.File]::ReadAllBytes("$PSScriptRoot/fixtures/stub-icon.png")

        $problems = Test-ReleaseReady -Root (New-FixtureRoot -IconBytes $stub)

        ($problems -join "`n") | Should -BeLike '*placeholder*'
    }
}

Describe 'New-ReleasePackage' {
    It 'writes the Thunderstore layout' {
        $root = New-FixtureRoot
        $dll = Join-Path $TestDrive 'BrrControllerGlyphs.dll'
        [System.IO.File]::WriteAllBytes($dll, [byte[]](1, 2, 3))
        $zip = Join-Path $TestDrive 'package.zip'

        New-ReleasePackage -Root $root -DllPath $dll -OutFile $zip

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
        try { $names = @($archive.Entries | ForEach-Object FullName | Sort-Object) } finally { $archive.Dispose() }
        $names | Should -Be @('CHANGELOG.md', 'icon.png', 'manifest.json', 'plugins/BrrControllerGlyphs.dll', 'README.md')
        ($names | Where-Object { $_ -like '*\*' }) | Should -BeNullOrEmpty
    }
}

Describe 'Test-TagUsable' {
    BeforeAll {
        $repo = Join-Path $TestDrive 'repo'
        New-Item -ItemType Directory $repo | Out-Null
        Push-Location $repo
        git init -q
        git -c user.name=t -c user.email=t@t commit -q --allow-empty -m one
        $first = (git rev-parse HEAD).Trim()
        git -c user.name=t -c user.email=t@t commit -q --allow-empty -m two
        $second = (git rev-parse HEAD).Trim()
        git tag v0.1.0 $first
    }

    AfterAll { Pop-Location }

    It 'allows a missing tag' {
        Test-TagUsable -Tag 'v0.2.0' -Commit $second | Should -BeTrue
    }

    It 'allows a tag on the same commit' {
        Test-TagUsable -Tag 'v0.1.0' -Commit $first | Should -BeTrue
    }

    It 'refuses a tag on another commit' {
        Test-TagUsable -Tag 'v0.1.0' -Commit $second | Should -BeFalse
    }
}

Describe 'Get-SkippedTestCount' {
    It 'sums notExecuted' {
        $a = Join-Path $TestDrive 'a.trx'; $b = Join-Path $TestDrive 'b.trx'
        Write-Text $a '<TestRun><ResultSummary><Counters total="5" executed="4" passed="4" notExecuted="1" /></ResultSummary></TestRun>'
        Write-Text $b '<TestRun><ResultSummary><Counters total="9" executed="7" passed="7" notExecuted="2" /></ResultSummary></TestRun>'

        Get-SkippedTestCount -TrxPath $a, $b | Should -Be 3
    }

    # dotnet test names TRX files like "runner_2026-11-10_net10.0[1].trx"; brackets are wildcards to Get-Content
    It 'reads TRX files with brackets in their names' {
        $path = Join-Path $TestDrive 'runner_net10.0[1].trx'
        [System.IO.File]::WriteAllText($path, '<TestRun><ResultSummary><Counters total="2" executed="2" passed="2" notExecuted="0" /></ResultSummary></TestRun>')

        Get-SkippedTestCount -TrxPath $path | Should -Be 0
    }
}

Describe 'Get-SteamBuildId' {
    It 'reads the public branch' {
        $text = "`"896660`"`n{`n`t`"depots`"`n`t{`n`t`t`"branches`"`n`t`t{`n`t`t`t`"beta`"`n`t`t`t{`n`t`t`t`t`"buildid`"`t`t`"1`"`n`t`t`t}`n`t`t`t`"public`"`n`t`t`t{`n`t`t`t`t`"buildid`"`t`t`"20123456`"`n`t`t`t`t`"timeupdated`"`t`t`"1730000000`"`n`t`t`t}`n`t`t}`n`t}`n}"

        Get-SteamBuildId -AppInfoText $text | Should -Be '20123456'
    }

    It "throws when there's no public build ID" {
        { Get-SteamBuildId -AppInfoText 'Connecting anonymously... FAILED' } |
            Should -Throw "Couldn't read the public build ID from SteamCMD's app info"
    }
}
