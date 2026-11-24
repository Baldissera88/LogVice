<#
.SYNOPSIS
    Sets a new LogVice version.

.DESCRIPTION
    Writes the version to LogVice.wxs with a new product code (the upgrade code stays the same) and to
    src/AssemblyInfo.cs, updates the version badge, the uninstall command and the detection product code in README.md,
    and in CHANGELOG.md adds an At a glance row, an empty dated section and the product code in the install table.
    It refuses a version that is not higher than the one in LogVice.wxs.

.PARAMETER Version
    The new version, as major.minor.patch.

.EXAMPLE
    .\bump.ps1 -Version 2.1.0
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false

function Update-TextFile {
    param(
        [string]$Path,
        [string[]]$Pattern,
        [string[]]$Replacement
    )
    $full = Join-Path -Path $PSScriptRoot -ChildPath $Path
    $text = [System.IO.File]::ReadAllText($full)
    for ($i = 0; $i -lt $Pattern.Count; $i++) {
        $regex = New-Object System.Text.RegularExpressions.Regex -ArgumentList $Pattern[$i], 'Multiline'
        if (-not $regex.IsMatch($text)) {
            throw "$Path has no match for $($Pattern[$i]). Check it by hand."
        }
        $text = $regex.Replace($text, $Replacement[$i], 1)
    }
    [System.IO.File]::WriteAllText($full, $text, $script:utf8)
}

$wxsText = [System.IO.File]::ReadAllText((Join-Path -Path $PSScriptRoot -ChildPath 'LogVice.wxs'))
if ($wxsText -notmatch '(?m)^[ \t]*Version="([^"]+)"') {
    throw 'No Version was found in LogVice.wxs.'
}
$current = $Matches[1]
if ($wxsText -notmatch '<Product Id="([^"]+)"') {
    throw 'No product code was found in LogVice.wxs.'
}
$oldGuid = $Matches[1]
if ([version]$Version -le [version]$current) {
    throw "$Version is not higher than the current version $current."
}
$changelog = [System.IO.File]::ReadAllText((Join-Path -Path $PSScriptRoot -ChildPath 'CHANGELOG.md'))
if ($changelog -match ('(?m)^## LogVice ' + [regex]::Escape($Version) + '\s*$')) {
    throw "CHANGELOG.md already has a section for $Version."
}

$guid = [guid]::NewGuid().ToString().ToUpperInvariant()
$date = (Get-Date).ToString('d MMMM yyyy', [System.Globalization.CultureInfo]::InvariantCulture)
$oldEsc = [regex]::Escape($oldGuid)
$curEsc = [regex]::Escape($current)

if ($PSCmdlet.ShouldProcess('LogVice.wxs, src/AssemblyInfo.cs, src/Program.cs, README.md, CHANGELOG.md', "Set version $Version")) {
    Update-TextFile -Path 'LogVice.wxs' `
        -Pattern '<Product Id="[^"]*"', '^([ \t]*)Version="[^"]*"', 'DefaultVersion="[^"]*"' `
        -Replacement "<Product Id=`"$guid`"", "`${1}Version=`"$Version`"", "DefaultVersion=`"$Version.0`""
    Update-TextFile -Path 'src/AssemblyInfo.cs' `
        -Pattern 'AssemblyVersion\("[^"]*"\)', 'AssemblyFileVersion\("[^"]*"\)', 'AssemblyInformationalVersion\("[^"]*"\)' `
        -Replacement "AssemblyVersion(`"$Version.0`")", "AssemblyFileVersion(`"$Version.0`")", "AssemblyInformationalVersion(`"$Version`")"
    Update-TextFile -Path 'src/Program.cs' `
        -Pattern 'BuildDate = "[^"]*"' `
        -Replacement "BuildDate = `"$date`""
    Update-TextFile -Path 'README.md' `
        -Pattern 'alt="Version [^"]*"', 'badge/version-[0-9.]*-', "msiexec /x \{$oldEsc\} /qn", "MSI product code ``\{$oldEsc\}`` \(version [0-9.]*\)" `
        -Replacement "alt=`"Version $Version`"", "badge/version-$Version-", "msiexec /x {$guid} /qn", "MSI product code ``{$guid}`` (version $Version)"

    $newline = "`n"
    if ($changelog.Contains("`r`n")) {
        $newline = "`r`n"
    }
    # The install table at the end of the changelog.
    Update-TextFile -Path 'CHANGELOG.md' `
        -Pattern "Run the $curEsc MSI", "msiexec /x \{$oldEsc\} /qn", "add $curEsc as a new app", "^\| Product code \($curEsc\) \|" `
        -Replacement "Run the $Version MSI", "msiexec /x {$guid} /qn", "add $Version as a new app", ("| Product code ($Version) | ``{$guid}`` |" + $newline + '$&')
    # The At a glance row.
    $rowPattern = '^\| \*\*([0-9.]+)\*\* \(latest\) \| (.*?) \| (.*?) \|[ \t]*$'
    $rowText = [System.IO.File]::ReadAllText((Join-Path -Path $PSScriptRoot -ChildPath 'CHANGELOG.md'))
    $row = [regex]::Match($rowText, $rowPattern, 'Multiline')
    if (-not $row.Success) {
        throw 'CHANGELOG.md has no At a glance row marked (latest). Check it by hand.'
    }
    $oldVersion = $row.Groups[1].Value
    $highlights = $row.Groups[2].Value
    $upgrade = $row.Groups[3].Value
    $newRows = "| **$Version** (latest) | | $upgrade |" + $newline + "| $oldVersion | $highlights | |"
    Update-TextFile -Path 'CHANGELOG.md' -Pattern $rowPattern -Replacement $newRows.Replace('$', '$$')
    # The new section.
    $section = "## LogVice $Version$newline$newline$date.$newline$newline" +
        "| | |$newline|---|---|$newline" +
        "| Product code | ``{$guid}`` |$newline" +
        "| Upgrade code | ``{2A8DF866-E6D4-4A44-ABAC-C64CA0D367B3}`` (unchanged) |$newline" +
        "| Files | ``LogVice.msi``, ``LogVice.exe``, ``SHA256SUMS.txt`` |$newline$newline"
    Update-TextFile -Path 'CHANGELOG.md' -Pattern '^## ' -Replacement ($section.Replace('$', '$$') + '## ')

    Write-Output "LogVice $current -> $Version, product code {$guid}"
    Write-Output 'Changed LogVice.wxs, src/AssemblyInfo.cs, src/Program.cs, README.md and CHANGELOG.md.'
    Write-Output 'Write the changes in CHANGELOG.md, then build, test and commit.'
}
