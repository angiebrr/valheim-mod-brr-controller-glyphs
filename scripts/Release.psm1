# ------------------------------------------------------------------------------------------------
# Release.psm1: release checks and packaging shared by the CI/release workflows and publish.ps1.
# ------------------------------------------------------------------------------------------------
#
# Tests: scripts/tests/Release.Tests.ps1 (Invoke-Pester scripts/tests). The rules come from
# Thunderstore's upload docs; see .claude/specs/ci-and-releases.md.

Set-StrictMode -Version Latest

# SHA-256 of the JotunnModStub "MOD STUB" icon, which must not be published.
$PlaceholderIconHash = 'fc637d630b28edfe423c8c6131802a77439634ae95f9e13bdbff2fc58dac3369'

# Thunderstore's limit for the manifest description.
$MaxDescriptionLength = 250

# Reads a text file with Windows line endings turned into "\n".
function Read-Text([string] $Path) {
    return ([System.IO.File]::ReadAllText($Path)) -replace "`r`n", "`n"
}

# Whether a file starts with the UTF-8 byte order mark (Thunderstore wants UTF-8 without one).
function Test-Bom([string] $Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
}

# A PNG's width and height from its IHDR chunk (big-endian, at bytes 16-23), so no image library is needed.
function Get-PngSize([string] $Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47) {
        return $null
    }

    $read = { param($offset) ([int]$bytes[$offset] -shl 24) -bor ([int]$bytes[$offset + 1] -shl 16) -bor ([int]$bytes[$offset + 2] -shl 8) -bor [int]$bytes[$offset + 3] }
    return @{ Width = & $read 16; Height = & $read 20 }
}

function Get-PluginVersion {
    <#
    .SYNOPSIS
    Reads PluginVersion ("public const string PluginVersion = "x.y.z";") from the plugin's source.
    #>
    param([Parameter(Mandatory)] [string] $SourcePath)

    $match = [regex]::Match((Read-Text $SourcePath), 'PluginVersion\s*=\s*"([^"]+)"')
    if (-not $match.Success) {
        throw "No PluginVersion constant in $SourcePath"
    }

    return $match.Groups[1].Value
}

function Get-ChangelogSection {
    <#
    .SYNOPSIS
    Returns a version's section of CHANGELOG.md: the lines under its "## x.y.z" heading (a "v"
    prefix and text after the version are allowed) up to the next "## " heading, trimmed; $null if
    there's no such heading.
    #>
    param([Parameter(Mandatory)] [string] $ChangelogPath, [Parameter(Mandatory)] [string] $Version)

    $heading = '^##\s+v?' + [regex]::Escape($Version) + '(?![\d.])'
    $lines = (Read-Text $ChangelogPath) -split "`n"
    $start = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match $heading) { $start = $i + 1; break }
    }

    if ($start -lt 0) {
        return $null
    }

    $end = $lines.Length
    for ($i = $start; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^##\s') { $end = $i; break }
    }

    return ($lines[$start..($end - 1)] -join "`n").Trim()
}

function Test-ReleaseReady {
    <#
    .SYNOPSIS
    Checks the package files under a repo root against the release rules and Thunderstore's upload
    rules; returns one message per problem, none when the package is ready.
    #>
    param([Parameter(Mandatory)] [string] $Root)

    $mod = Join-Path $Root 'BrrControllerGlyphs'
    $problems = [System.Collections.Generic.List[string]]::new()

    $pluginVersion = Get-PluginVersion -SourcePath (Join-Path $mod 'BrrControllerGlyphs.cs')
    $manifest = Read-Text (Join-Path $mod 'Package/manifest.json') | ConvertFrom-Json
    $manifestVersion = [string]$manifest.version_number

    if ($pluginVersion -ne $manifestVersion) {
        $problems.Add("PluginVersion $pluginVersion doesn't match manifest version_number $manifestVersion")
    }

    if ($manifestVersion -notmatch '^\d+\.\d+\.\d+$') {
        $problems.Add("manifest version_number $manifestVersion isn't x.y.z (digits only)")
    }

    $changelog = Join-Path $mod 'CHANGELOG.md'
    if (-not (Test-Path $changelog)) {
        $problems.Add('CHANGELOG.md is missing')
    }
    elseif (-not (Get-ChangelogSection -ChangelogPath $changelog -Version $pluginVersion)) {
        $problems.Add("CHANGELOG.md has no section for $pluginVersion")
    }

    foreach ($file in 'README.md', 'CHANGELOG.md') {
        $path = Join-Path $mod $file
        if ((Test-Path $path) -and (Test-Bom $path)) {
            $problems.Add("$file starts with a BOM (save it as UTF-8 without BOM)")
        }
    }

    if ([string]$manifest.name -notmatch '^[A-Za-z0-9_]+$') {
        $problems.Add("manifest name '$($manifest.name)' must be letters, digits and underscores only")
    }

    $descriptionLength = ([string]$manifest.description).Length
    if ($descriptionLength -gt $MaxDescriptionLength) {
        $problems.Add("manifest description is $descriptionLength characters (max $MaxDescriptionLength)")
    }

    foreach ($dependency in @($manifest.dependencies)) {
        if ([string]$dependency -notmatch '^[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+$') {
            $problems.Add("manifest dependency '$dependency' isn't Team-Package-x.y.z")
        }
    }

    $icon = Join-Path $mod 'Package/icon.png'
    $size = Get-PngSize $icon
    if (-not $size) {
        $problems.Add('icon.png is not a PNG')
    }
    elseif ($size.Width -ne 256 -or $size.Height -ne 256) {
        $problems.Add("icon.png is $($size.Width)x$($size.Height), must be 256x256")
    }

    if ((Get-FileHash -Algorithm SHA256 $icon).Hash -eq $PlaceholderIconHash) {
        $problems.Add("icon.png is still the JotunnModStub placeholder ('MOD STUB'); add the mod's own icon")
    }

    return , $problems.ToArray()
}

function Test-TagUsable {
    <#
    .SYNOPSIS
    Whether a release can use a tag: it doesn't exist yet, or it already points at the commit being
    released (a re-run after a failed publish).
    #>
    param([Parameter(Mandatory)] [string] $Tag, [Parameter(Mandatory)] [string] $Commit)

    $tagged = git rev-parse -q --verify "refs/tags/$Tag^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $tagged) {
        return $true
    }

    return $tagged.Trim() -eq (git rev-parse $Commit).Trim()
}

function Get-SkippedTestCount {
    <#
    .SYNOPSIS
    Sums the tests that didn't run (skipped) across dotnet test TRX result files.
    #>
    param([Parameter(Mandatory)] [string[]] $TrxPath)

    $total = 0
    foreach ($path in $TrxPath) {
        # -LiteralPath: dotnet test names TRX files like "..._net10.0[1].trx", and brackets are wildcards
        $counters = ([xml](Get-Content -Raw -LiteralPath $path)).GetElementsByTagName('Counters') | Select-Object -First 1
        if ($counters) {
            $total += [int]$counters.GetAttribute('notExecuted')
        }
    }

    return $total
}

function Get-SteamBuildId {
    <#
    .SYNOPSIS
    Reads the public branch's build ID from SteamCMD's app_info_print output; throws if it's not
    there, so a failed query never falls back to a stale cache key.
    #>
    param([Parameter(Mandatory)] [AllowEmptyString()] [string] $AppInfoText)

    $match = [regex]::Match($AppInfoText, '"public"\s*\{\s*"buildid"\s*"(\d+)"')
    if (-not $match.Success) {
        throw "Couldn't read the public build ID from SteamCMD's app info"
    }

    return $match.Groups[1].Value
}

function New-ReleasePackage {
    <#
    .SYNOPSIS
    Writes the Thunderstore zip: manifest.json, icon.png, README.md and CHANGELOG.md at its root and
    the DLL under plugins/ (installed to BepInEx/plugins). Entry names are written with "/" by hand:
    Windows PowerShell 5.1's Compress-Archive can write "\", which mod managers don't unpack.
    #>
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $DllPath,
        [Parameter(Mandatory)] [string] $OutFile
    )

    $mod = Join-Path $Root 'BrrControllerGlyphs'
    $entries = [ordered]@{
        'manifest.json' = Join-Path $mod 'Package/manifest.json'
        'icon.png'      = Join-Path $mod 'Package/icon.png'
        'README.md'     = Join-Path $mod 'README.md'
        'CHANGELOG.md'  = Join-Path $mod 'CHANGELOG.md'
        "plugins/$(Split-Path -Leaf $DllPath)" = $DllPath
    }

    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    if (Test-Path $OutFile) {
        Remove-Item $OutFile
    }

    $archive = [System.IO.Compression.ZipFile]::Open($OutFile, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $entries.GetEnumerator()) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Value, $entry.Key) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

Export-ModuleMember -Function Get-PluginVersion, Get-ChangelogSection, Test-ReleaseReady, Test-TagUsable, Get-SkippedTestCount, Get-SteamBuildId, New-ReleasePackage
