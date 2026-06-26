#Requires -Version 5.1
<#
.SYNOPSIS
    Mirrors src\Shared from KPPasskeyChecker into this repo.
.DESCRIPTION
    KPPasskeyChecker owns src\Shared canonically. Run this script after any
    change to KPPasskeyChecker\src\Shared, then build and verify KP2FAChecker.
#>

$ErrorActionPreference = 'Stop'

$src = "$PSScriptRoot\..\KPPasskeyChecker\src\Shared"
$dst = "$PSScriptRoot\src\Shared"

if (-not (Test-Path $src)) {
    Write-Error "KPPasskeyChecker\src\Shared not found at: $src`nEnsure both plugins are checked out side by side."
    exit 1
}

Copy-Item "$src\*" $dst -Recurse -Force
Write-Host "Shared synced from KPPasskeyChecker." -ForegroundColor Green
