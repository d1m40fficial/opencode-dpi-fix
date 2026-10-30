# Builds OpencodeDpiFix.exe with the C# compiler that ships with Windows
# (.NET Framework 4.x). The result is a single self-contained exe that runs on
# any Windows 10/11 without installing a runtime.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
param(
    [string]$OutDir = 'dist'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$src = Join-Path $root 'src'

$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw 'csc.exe (.NET Framework 4.x) not found. Install .NET Framework 4.8.'
}

$out = Join-Path $root $OutDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

$icon = Join-Path $src 'app.ico'
if (-not (Test-Path -LiteralPath $icon)) {
    Write-Host 'app.ico not found, building it...'
    & (Join-Path $root 'tools\make-icon.ps1')
}

$sources = Get-ChildItem -LiteralPath $src -Filter '*.cs' | ForEach-Object { $_.FullName }
$exe = Join-Path $out 'OpencodeDpiFix.exe'

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    '/debug-'
    "/win32manifest:$(Join-Path $src 'app.manifest')"
    "/out:$exe"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
) + $sources

if (Test-Path -LiteralPath $icon) {
    $cscArgs = @("/win32icon:$icon") + $cscArgs
}

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }

$size = (Get-Item -LiteralPath $exe).Length
Write-Host ''
Write-Host ("Built {0} ({1:N0} bytes)" -f $exe, $size)
