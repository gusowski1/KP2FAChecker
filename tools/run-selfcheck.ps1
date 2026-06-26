#Requires -Version 5
<#
.SYNOPSIS
    Compiles and runs the KP2FAChecker pure-logic self-test harness.

.DESCRIPTION
    Compiles the real plugin sources (src\Shared + src\KP2FAChecker) together with
    tools\SelfCheck\SelfCheck.cs into a console .exe using the in-box csc.exe (C# 5,
    /langversion:5) against the same framework references as build.ps1 plus KeePass.exe,
    then runs it. The harness exercises only pure logic (parsing, scope mapping, cache
    keys, eTLD+1) — no KeePass process, no network, no file-system writes.

    Exit code is forwarded from the harness: 0 = all checks passed, 1 = a check failed.

.PARAMETER KeePassExe
    Path to KeePass.exe used as a compile reference. Defaults to Libs\KeePass.exe.
#>
[CmdletBinding()]
param(
    [string]$KeePassExe = ''
)

$ErrorActionPreference = 'Stop'

$ToolsDir    = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $ToolsDir
$SrcPlugin   = Join-Path $ProjectRoot 'src\KP2FAChecker'
$SrcShared   = Join-Path $ProjectRoot 'src\Shared'
$HarnessCs       = Join-Path $ToolsDir 'SelfCheck\SelfCheck.cs'
$HarnessSharedCs = Join-Path $ToolsDir 'SelfCheck\SharedChecks.cs'
if ([string]::IsNullOrEmpty($KeePassExe)) { $KeePassExe = Join-Path $ProjectRoot 'Libs\KeePass.exe' }

if (-not (Test-Path $SrcPlugin))  { throw "Plugin sources not found: $SrcPlugin" }
if (-not (Test-Path $SrcShared))  { throw "Shared sources not found: $SrcShared" }
if (-not (Test-Path $HarnessCs))       { throw "Harness not found: $HarnessCs" }
if (-not (Test-Path $HarnessSharedCs)) { throw "Shared harness not found: $HarnessSharedCs" }
if (-not (Test-Path $KeePassExe)) { throw "KeePass.exe not found: $KeePassExe (supply it locally, see Libs\README.md)" }
$KeePassExe = (Resolve-Path $KeePassExe).Path

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) not found." }

# Collect every .cs from the plugin + shared sources (excluding obj\ / bin\), plus the harness.
function Get-Sources($dir) {
    Get-ChildItem $dir -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        ForEach-Object { $_.FullName }
}
$sources = @()
$sources += Get-Sources $SrcShared
$sources += Get-Sources $SrcPlugin
$sources += $HarnessCs
$sources += $HarnessSharedCs

$OutExe = Join-Path $ToolsDir 'SelfCheck.exe'
if (Test-Path $OutExe) { Remove-Item $OutExe -Force }

# The PGP self-test reads its ".sig" fixture from a "fixtures" folder next to the harness .exe
# (AppDomain.BaseDirectory). The committed fixtures live under SelfCheck\fixtures; stage a copy.
$FixturesSrc = Join-Path $ToolsDir 'SelfCheck\fixtures'
$FixturesDst = Join-Path $ToolsDir 'fixtures'

# Compile + run inside try; clean up build/run artifacts in finally so a csc failure (or any
# mid-run error) never leaves the staged .exe / fixtures copy behind.
$code = 1
try {
    Write-Host "==> Compiling self-check harness (csc /langversion:5)" -ForegroundColor Cyan
    $cscArgs = @('/nologo','/target:exe','/langversion:5',"/out:$OutExe")
    foreach ($r in @('System.dll','System.Core.dll','System.Drawing.dll','System.IO.Compression.dll',
                     'System.Net.Http.dll','System.Web.Extensions.dll','System.Windows.Forms.dll')) {
        $cscArgs += "/reference:$r"
    }
    $cscArgs += "/reference:$KeePassExe"
    $cscArgs += $sources
    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed compiling the harness (exit $LASTEXITCODE)." }

    if (-not (Test-Path $FixturesSrc)) { throw "Fixtures not found: $FixturesSrc" }
    if (Test-Path $FixturesDst) { Remove-Item $FixturesDst -Recurse -Force }
    Copy-Item $FixturesSrc $FixturesDst -Recurse -Force

    Write-Host "==> Running self-check" -ForegroundColor Cyan
    & $OutExe
    $code = $LASTEXITCODE
}
finally {
    Remove-Item $OutExe -Force -ErrorAction SilentlyContinue
    Remove-Item $FixturesDst -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
if ($code -eq 0) { Write-Host "SELF-CHECK OK" -ForegroundColor Green }
else             { Write-Host "SELF-CHECK FAILED (exit $code)" -ForegroundColor Red }
exit $code
