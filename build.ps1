<#
.SYNOPSIS
    Builds LogVice on Windows: LogVice.exe and, with -Msi, LogVice.msi.

.DESCRIPTION
    Uses the C# compiler that ships with .NET Framework 4.x, so no SDK or Visual Studio is needed.
    The MSI needs WiX Toolset 3 (candle.exe and light.exe) on the PATH or in the default install folder.

.PARAMETER Msi
    Also build LogVice.msi with WiX Toolset 3.

.EXAMPLE
    .\build.ps1

.EXAMPLE
    .\build.ps1 -Msi
#>
[CmdletBinding()]
param(
    [switch]$Msi
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$sources = @(
    'src\AssemblyInfo.cs', 'src\Core.cs', 'src\Logs.Locations.cs', 'src\Logs.Etl.cs', 'src\Logs.EventLog.cs',
    'src\Net.Remote.cs', 'src\Net.Update.cs', 'src\Ui.Base.cs', 'src\Ui.Controls.cs', 'src\Ui.Controls2.cs',
    'src\Ui.Grid.cs', 'src\Ui.Browse.cs', 'src\Ui.Highlights.cs', 'src\Ui.Settings.cs', 'src\Ui.MainForm.cs',
    'src\Ui.Lookup.cs', 'src\Program.cs'
)

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -Path $csc)) {
    throw 'csc.exe from .NET Framework 4.x was not found.'
}

$cscArgs = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/langversion:5',
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    '/win32icon:LogVice.ico', '/resource:LogVice.ico,LogVice.ico', '/out:LogVice.exe'
) + $sources

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE."
}
Write-Output 'Built LogVice.exe'

if ($Msi) {
    $wixBin = $null
    $candle = Get-Command -Name candle.exe -ErrorAction SilentlyContinue
    if ($candle) {
        $wixBin = Split-Path -Path $candle.Source
    }
    else {
        $found = Get-ChildItem -Path "${env:ProgramFiles(x86)}\WiX Toolset v3*\bin\candle.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $wixBin = $found.DirectoryName
        }
    }
    if (-not $wixBin) {
        throw 'WiX Toolset 3 was not found. Install it, or build without -Msi.'
    }
    & (Join-Path $wixBin 'candle.exe') -nologo -arch x64 -out LogVice.wixobj LogVice.wxs
    if ($LASTEXITCODE -ne 0) {
        throw "candle.exe failed with exit code $LASTEXITCODE."
    }
    & (Join-Path $wixBin 'light.exe') -nologo -out LogVice.msi LogVice.wixobj
    if ($LASTEXITCODE -ne 0) {
        throw "light.exe failed with exit code $LASTEXITCODE."
    }
    Remove-Item -Path LogVice.wixobj, LogVice.wixpdb -ErrorAction SilentlyContinue
    Write-Output 'Built LogVice.msi'
}
