<#
.SYNOPSIS
  Build AlienSkies.scr.

.DESCRIPTION
  Default: framework-dependent single file (small; needs the .NET 10 Desktop Runtime).
  -SelfContained: bundles the runtime (large, but runs on any Windows x64 machine).

  Install: right-click artifacts\AlienSkies.scr -> Install, or copy it to C:\Windows\System32 (admin)
  so it appears in Screen Saver Settings.
#>
param(
    [switch]$SelfContained,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$out = Join-Path $PSScriptRoot "artifacts\publish"
$sc = if ($SelfContained) { "true" } else { "false" }

dotnet publish src\AlienSkies.Screensaver\AlienSkies.Screensaver.csproj `
    -c $Configuration -r $Runtime --self-contained $sc `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=$sc `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$scr = Join-Path $PSScriptRoot "artifacts\AlienSkies.scr"
Copy-Item (Join-Path $out "AlienSkies.exe") $scr -Force
Write-Host "Built $scr"
