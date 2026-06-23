#Requires -Version 5
<#
.SYNOPSIS
    Builds the KP2FAChecker KeePass plugin into build\, in both shipping formats
    (.plgx and .dll), from the canonical sources under src\.

.DESCRIPTION
    The canonical source lives in src\ as proper projects:
        src\Shared\        - infrastructure shared by all plugins (reused verbatim from
                             KPPasskeyChecker; keeps the KPPasskeyChecker.Shared.* namespaces)
        src\KP2FAChecker\  - this plugin (compiles Shared in -> single self-contained DLL)

    KeePass's "--plgx-create" needs ONE flat folder containing a .csproj and all .cs files,
    compiled on the user's machine at load time (C# 5). Rather than keep a hand-maintained copy
    of the sources, this script GENERATES that flat folder in TEMP on demand, builds from it, and
    deletes it again. So there is a single source of truth (src\); the flat layout is transient.

    Produces both artifacts in build\:
      * KP2FAChecker.plgx - packaged via KeePass.exe --plgx-create (compiled on load).
      * KP2FAChecker.dll  - compiled here with the in-box csc (C# 5, /optimize+) into a single
        self-contained assembly (no third-party deps). Its ProductName is asserted to be
        "KeePass Plugin", otherwise KeePass silently ignores the DLL.

    All paths are relative to this script's folder ($PSScriptRoot). KeePass.exe is supplied
    locally (not in the repo) at Libs\KeePass.exe; override with -KeePassExe.

.PARAMETER KeePassExe
    Path to the KeePass.exe used for packaging and as a compile reference.

.PARAMETER PlgxOnly
    Produce only the .plgx (skip the csc .dll step and the build\ wipe). Used by the Visual
    Studio Release post-build, which already produces its own .dll.

.EXAMPLE
    .\build.ps1
#>
[CmdletBinding()]
param(
    # Default resolved in the body: $PSScriptRoot is empty inside param defaults when the script
    # is launched via "powershell -File" (e.g. the VS post-build), so it can't be used here.
    [string]$KeePassExe = '',
    [switch]$PlgxOnly
)

$ErrorActionPreference = 'Stop'

# --- Paths -----------------------------------------------------------------
$ProjectRoot = $PSScriptRoot
if ([string]::IsNullOrEmpty($ProjectRoot)) { $ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrEmpty($KeePassExe)) { $KeePassExe = Join-Path $ProjectRoot 'Libs\KeePass.exe' }
$PluginName  = 'KP2FAChecker'
$SrcPlugin   = Join-Path $ProjectRoot ('src\' + $PluginName)
$SrcShared   = Join-Path $ProjectRoot 'src\Shared'
$BuildDir    = Join-Path $ProjectRoot 'build'
$PlgxOutput  = Join-Path $BuildDir ($PluginName + '.plgx')
$DllOutput   = Join-Path $BuildDir ($PluginName + '.dll')

# Transient flat staging folder in TEMP (named after the plugin so --plgx-create emits <name>.plgx).
$StageRoot   = Join-Path ([System.IO.Path]::GetTempPath()) 'KP2FAChecker-pack'
$StageDir    = Join-Path $StageRoot $PluginName
$StagePlgx   = Join-Path $StageRoot ($PluginName + '.plgx')

function Write-Step($msg) { Write-Host ("==> " + $msg) -ForegroundColor Cyan }
function Write-Artifact($path) {
    $item = Get-Item $path
    $hash = (Get-FileHash $path -Algorithm SHA256).Hash
    Write-Host ("  {0}" -f (Split-Path $path -Leaf)) -ForegroundColor Green
    Write-Host ("    Size   : {0:N0} bytes" -f $item.Length)
    Write-Host ("    SHA256 : {0}" -f $hash)
}
function Remove-Tree($path) {
    if (Test-Path $path) { try { Remove-Item $path -Recurse -Force } catch { } }
}
# Copy every .cs (excluding obj\ / bin\) from $src into $dest, preserving relative structure.
function Copy-CsTree($src, $dest) {
    $prefix = $src.TrimEnd('\') + '\'
    Get-ChildItem $src -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        ForEach-Object {
            $rel    = $_.FullName.Substring($prefix.Length)
            $target = Join-Path $dest $rel
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            Copy-Item $_.FullName $target -Force
        }
}

# --- 1. Validate -----------------------------------------------------------
if (-not (Test-Path $SrcPlugin)) { throw "Plugin sources not found: $SrcPlugin" }
if (-not (Test-Path $SrcShared)) { throw "Shared sources not found: $SrcShared" }
if (-not (Test-Path $KeePassExe)) { throw "KeePass.exe not found: $KeePassExe (supply it locally, see Libs\README.md)" }
$KeePassExe = (Resolve-Path $KeePassExe).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) not found." }

# --- 2. Clean --------------------------------------------------------------
if (-not (Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir | Out-Null }
if (-not $PlgxOnly) {
    Write-Step "Cleaning build\"
    Remove-Item (Join-Path $BuildDir '*') -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Tree $StageRoot

# --- 3. Stage the flat folder from src\ ------------------------------------
Write-Step "Staging flat source folder from src\"
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null
Copy-CsTree $SrcPlugin $StageDir
Copy-CsTree $SrcShared (Join-Path $StageDir 'Shared')

# Generate the legacy .csproj KeePass reads for references at .plgx compile time.
$sources = Get-ChildItem $StageDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$prefix  = $StageDir.TrimEnd('\') + '\'
$compileItems = ($sources | ForEach-Object { '    <Compile Include="' + $_.Substring($prefix.Length) + '" />' }) -join "`r`n"
$frameworkRefs = @('mscorlib','System','System.Core','System.Drawing','System.IO.Compression',
                   'System.Net.Http','System.Web.Extensions','System.Windows.Forms','KeePass')
$refItems = ($frameworkRefs | ForEach-Object { '    <Reference Include="' + $_ + '" />' }) -join "`r`n"
$csproj = @"
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Configuration Condition=" '`$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '`$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>$PluginName</RootNamespace>
    <AssemblyName>$PluginName</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <LangVersion>5</LangVersion>
  </PropertyGroup>
  <ItemGroup>
$refItems
  </ItemGroup>
  <ItemGroup>
$compileItems
  </ItemGroup>
  <Import Project="`$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
"@
[System.IO.File]::WriteAllText((Join-Path $StageDir ($PluginName + '.csproj')), $csproj, (New-Object System.Text.UTF8Encoding($false)))

# --- 4. Package .plgx ------------------------------------------------------
Write-Step "Packaging .plgx (KeePass --plgx-create)"
$proc = Start-Process -FilePath $KeePassExe -ArgumentList ('--plgx-create "{0}"' -f $StageDir) -PassThru -Wait
if ($proc.ExitCode -ne 0) { throw "KeePass --plgx-create exited with code $($proc.ExitCode)." }
Start-Sleep -Milliseconds 500
if (-not (Test-Path $StagePlgx)) { throw "Build produced no .plgx ($StagePlgx missing)." }
Move-Item $StagePlgx $PlgxOutput -Force

# --- 5. Compile standalone .dll (skipped in -PlgxOnly) ---------------------
if (-not $PlgxOnly) {
    Write-Step "Compiling standalone .dll (csc)"
    $cscArgs = @('/nologo','/target:library','/optimize+','/langversion:5',"/out:$DllOutput")
    foreach ($r in @('System.dll','System.Core.dll','System.Drawing.dll','System.IO.Compression.dll',
                     'System.Net.Http.dll','System.Web.Extensions.dll','System.Windows.Forms.dll')) {
        $cscArgs += "/reference:$r"
    }
    $cscArgs += "/reference:$KeePassExe"
    $cscArgs += $sources
    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed compiling the .dll (exit $LASTEXITCODE)." }
    $product = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($DllOutput).ProductName
    if ($product -ne 'KeePass Plugin') {
        throw "Built .dll has ProductName '$product' (expected 'KeePass Plugin'); KeePass would ignore it."
    }
}

# --- 6. Clean up staging ---------------------------------------------------
Remove-Tree $StageRoot

# --- 7. Summary ------------------------------------------------------------
Write-Host ""
Write-Host "BUILD OK" -ForegroundColor Green
Write-Artifact $PlgxOutput
if (-not $PlgxOnly) { Write-Artifact $DllOutput }
Write-Host ""
if (-not $PlgxOnly) {
    Write-Host "To install (pick ONE), close KeePass, copy build\$PluginName.plgx OR build\$PluginName.dll" -ForegroundColor Yellow
    Write-Host "into the KeePass Plugins folder, then restart KeePass. Do not install both at once." -ForegroundColor Yellow
}
