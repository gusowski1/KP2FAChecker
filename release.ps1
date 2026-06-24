#Requires -Version 5
<#
.SYNOPSIS
    Release helper for a single KeePass-plugin repo (KPPasskeyChecker / KP2FAChecker).

.DESCRIPTION
    A three-stage, branch + PR release flow. CHANGELOG.md is the single source of the
    GitHub release notes, and the GitHub release TYPE is explicit: draft | prerelease | release.

    The script is generic: it detects the plugin name and current version from VersionInfo.txt,
    so the same file works in either repo (and is what the umbrella release-all.ps1 calls).

    We do NOT ship a SHA256SUMS file: GitHub computes and shows a SHA-256 digest per release
    asset itself (REST 'digest' field + the release UI), which covers download integrity.

    Stages:
      Preview  (default)         List exactly what WOULD happen — version bump (old -> new),
                                 the files that change, the pending working-tree changes that
                                 would be committed, the CHANGELOG notes, the release type, and
                                 the branch/PR plan. Changes NOTHING. This is the approval gate.
      Prepare  (-Stage Prepare)  Bump the version in all relevant files, build, create branch
                                 release/vX.Y.Z, commit, push, and open a PR. Does NOT create
                                 the GitHub release (the tag must point at main, which only
                                 happens once the PR is merged).
      Publish  (-Stage Publish)  Run AFTER the PR is merged to main: verify main carries the
                                 target version, rebuild from main, and create the GitHub
                                 release vX.Y.Z (tag on main) with the .plgx/.dll assets and the
                                 CHANGELOG section as notes, using the chosen
                                 --draft / --prerelease / (none) type.

.PARAMETER Version
    Target semantic version, e.g. 0.3.0 (no leading 'v'). Required for Prepare and Publish.
    For Preview, defaults to the current version in VersionInfo.txt.

.PARAMETER Type
    GitHub release type: draft | prerelease | release. Default: prerelease.

.PARAMETER Stage
    Preview (default) | Prepare | Publish.

.PARAMETER Force
    Skip the interactive confirmation in Prepare/Publish.

.EXAMPLE
    .\release.ps1 -Version 0.3.0 -Type prerelease
    .\release.ps1 -Version 0.3.0 -Type prerelease -Stage Prepare
    # ...merge the PR on GitHub...
    .\release.ps1 -Version 0.3.0 -Type prerelease -Stage Publish
#>
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateSet('draft', 'prerelease', 'release')] [string]$Type = 'prerelease',
    [ValidateSet('Preview', 'Prepare', 'Publish')]   [string]$Stage = 'Preview',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

# ---- helpers ----------------------------------------------------------------

function Resolve-Gh {
    $c = Get-Command gh -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $fallback = Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'
    if (Test-Path $fallback) { return $fallback }
    throw "GitHub CLI (gh) not found. Install it (winget install GitHub.cli) or add it to PATH."
}

function Get-PluginInfo {
    $vi = Join-Path $RepoRoot 'VersionInfo.txt'
    if (-not (Test-Path $vi)) { throw "VersionInfo.txt not found in $RepoRoot." }
    foreach ($line in (Get-Content $vi)) {
        if ($line -match '^(?<name>[^:\s][^:]*):(?<ver>\d+\.\d+\.\d+)\s*$') {
            return [pscustomobject]@{ Name = $Matches.name; Version = $Matches.ver }
        }
    }
    throw "Could not parse a 'Name:Version' line from VersionInfo.txt."
}

function Get-ChangelogSection([string]$ver) {
    $path = Join-Path $RepoRoot 'CHANGELOG.md'
    if (-not (Test-Path $path)) { throw "CHANGELOG.md not found - it is the source of the release notes." }
    $lines = Get-Content $path
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match ('^##\s*\[' + [regex]::Escape($ver) + '\]')) { $start = $i; break }
    }
    if ($start -lt 0) { throw "CHANGELOG.md has no '## [$ver]' section. Add it before releasing." }
    $body = New-Object System.Collections.Generic.List[string]
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^##\s*\[') { break }
        $body.Add($lines[$i])
    }
    $text = ($body -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { throw "The '## [$ver]' section in CHANGELOG.md is empty." }
    return $text
}

function Get-VersionFiles {
    param([string]$pluginName)
    @(
        (Join-Path $RepoRoot 'VersionInfo.txt'),
        (Join-Path $RepoRoot ("src\{0}\Properties\AssemblyInfo.cs" -f $pluginName)),
        (Join-Path $RepoRoot ("src\{0}\PluginVersion.cs" -f $pluginName))
    )
}

function Update-VersionFiles {
    param([string]$pluginName, [string]$oldVer, [string]$newVer)
    if ($oldVer -eq $newVer) { Write-Host "  Version already $newVer - no file changes needed." -ForegroundColor DarkGray; return }
    foreach ($f in (Get-VersionFiles $pluginName)) {
        if (-not (Test-Path $f)) { throw "Expected version file not found: $f" }
        $raw = Get-Content $f -Raw
        # The 3-part version is a prefix of the 4-part AssemblyVersion (X.Y.Z.0), so a literal
        # replace of the 3-part string updates VersionInfo, both Assembly*Version attributes and
        # the doc comment in one pass.
        $new = $raw.Replace($oldVer, $newVer)
        if ($new -ne $raw) {
            # Preserve UTF-8 without BOM (KeePass requires VersionInfo.txt to have no BOM).
            [System.IO.File]::WriteAllText($f, $new, (New-Object System.Text.UTF8Encoding($false)))
            Write-Host ("  bumped {0}" -f (Resolve-Path $f -Relative))
        }
    }
}

function Invoke-Build {
    $build = Join-Path $RepoRoot 'build.ps1'
    if (-not (Test-Path $build)) { throw "build.ps1 not found in $RepoRoot." }
    Write-Host "==> Building" -ForegroundColor Cyan
    & $build
    if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed (exit $LASTEXITCODE)." }
}

function Get-Assets {
    # The release assets are the two shipping artifacts. GitHub shows each asset's SHA-256 itself,
    # so no separate SHA256SUMS file is produced.
    param([string]$pluginName)
    $buildDir = Join-Path $RepoRoot 'build'
    $plgx = Join-Path $buildDir ("{0}.plgx" -f $pluginName)
    $dll = Join-Path $buildDir ("{0}.dll" -f $pluginName)
    foreach ($a in @($plgx, $dll)) { if (-not (Test-Path $a)) { throw "Build artifact missing: $a" } }
    return @($plgx, $dll)
}

function Confirm-Or-Exit([string]$prompt) {
    if ($Force) { return }
    $ans = Read-Host "$prompt (y/N)"
    if ($ans -notmatch '^(y|yes)$') { Write-Host "Aborted." -ForegroundColor Yellow; exit 2 }
}

function Get-TypeArgs([string]$t) {
    switch ($t) {
        'draft'      { return @('--draft') }
        'prerelease' { return @('--prerelease') }
        'release'    { return @() }       # no flag => published as "Latest"
    }
}

# ---- main -------------------------------------------------------------------

$info = Get-PluginInfo
$plugin = $info.Name
$current = $info.Version
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $current }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "-Version must be x.y.z (got '$Version')." }

$tag = "v$Version"
$branch = "release/$tag"
$notes = Get-ChangelogSection $Version   # validates the section exists for every stage

Write-Host ""
Write-Host ("=== {0} release | plugin={1} | {2} -> {3} | type={4} | stage={5} ===" -f $tag, $plugin, $current, $Version, $Type, $Stage) -ForegroundColor Cyan
Write-Host ""

switch ($Stage) {

    'Preview' {
        Write-Host "Version bump:" -ForegroundColor White
        if ($current -eq $Version) { Write-Host "  (already at $Version - no version-file changes)" -ForegroundColor DarkGray }
        else { Get-VersionFiles $plugin | ForEach-Object { Write-Host ("  {0}: {1} -> {2}" -f (Resolve-Path $_ -Relative), $current, $Version) } }

        Write-Host ""
        Write-Host "Working-tree changes that WOULD be committed (git status):" -ForegroundColor White
        $st = git status --short
        if ([string]::IsNullOrWhiteSpace(($st -join ''))) { Write-Host "  (clean - nothing pending besides the version bump)" -ForegroundColor DarkGray }
        else { $st | ForEach-Object { Write-Host "  $_" } }

        Write-Host ""
        Write-Host ("Release notes (from CHANGELOG.md [{0}]):" -f $Version) -ForegroundColor White
        $notes -split "`n" | ForEach-Object { Write-Host "  | $_" }

        Write-Host ""
        Write-Host "Plan:" -ForegroundColor White
        Write-Host "  Prepare -> branch '$branch', commit + push, open PR to main (no GitHub release yet)."
        Write-Host ("  Publish -> after merge: build from main, create GitHub release '{0}' ({1}) with assets" -f $tag, $Type)
        Write-Host ("            build\{0}.plgx and build\{0}.dll (GitHub shows each asset's SHA-256 itself)." -f $plugin)
        Write-Host ""
        Write-Host "Nothing changed. Re-run with -Stage Prepare to proceed." -ForegroundColor Green
    }

    'Prepare' {
        $gh = Resolve-Gh
        if (git rev-parse --abbrev-ref HEAD) { } else { throw "Not a git repository." }
        Confirm-Or-Exit "Bump to $Version, build, create branch '$branch', commit, push and open a PR?"

        Write-Host "==> Bumping version files" -ForegroundColor Cyan
        Update-VersionFiles $plugin $current $Version
        Invoke-Build
        Get-Assets $plugin | Out-Null   # verify the artifacts exist after the build

        Write-Host "==> Creating branch, commit, push" -ForegroundColor Cyan
        git checkout -b $branch
        git add -A
        git commit -m ("Release {0}" -f $Version)
        git push -u origin $branch

        Write-Host "==> Opening PR" -ForegroundColor Cyan
        $notesFile = [System.IO.Path]::GetTempFileName()
        Set-Content $notesFile $notes -Encoding utf8
        & $gh pr create --base main --head $branch --title ("Release {0}" -f $Version) --body-file $notesFile
        Remove-Item $notesFile -ErrorAction SilentlyContinue

        Write-Host ""
        Write-Host ("PR opened for {0}. Merge it to main, then run:" -f $tag) -ForegroundColor Green
        Write-Host ("  .\release.ps1 -Version {0} -Type {1} -Stage Publish" -f $Version, $Type) -ForegroundColor Green
        Write-Host ("Artifacts built in build\ ({0}.plgx/.dll) for verification; rebuilt from main at Publish." -f $plugin) -ForegroundColor DarkGray
    }

    'Publish' {
        $gh = Resolve-Gh
        Write-Host "==> Switching to main and pulling" -ForegroundColor Cyan
        git checkout main
        git pull --ff-only
        $onMain = (Get-PluginInfo).Version
        if ($onMain -ne $Version) {
            throw "main is at version $onMain, expected $Version. Is the release PR merged? (Run -Stage Prepare first, then merge.)"
        }
        Confirm-Or-Exit ("Create GitHub release {0} ({1}) from main with built assets?" -f $tag, $Type)

        Invoke-Build
        $assets = Get-Assets $plugin

        $notesFile = [System.IO.Path]::GetTempFileName()
        Set-Content $notesFile $notes -Encoding utf8
        $typeArgs = Get-TypeArgs $Type

        Write-Host ("==> Creating GitHub release {0} ({1})" -f $tag, $Type) -ForegroundColor Cyan
        & $gh release create $tag @assets `
            --target main --title ("{0} {1}" -f $plugin, $Version) --notes-file $notesFile @typeArgs
        Remove-Item $notesFile -ErrorAction SilentlyContinue

        Write-Host ""
        Write-Host ("Done. Release {0} created as '{1}'." -f $tag, $Type) -ForegroundColor Green
    }
}
