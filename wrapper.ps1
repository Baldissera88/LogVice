<#
.SYNOPSIS
    LogVice - a CMTrace-style log viewer for ConfigMgr, Intune and Windows logs.

.DESCRIPTION
    Opens CMTrace / Intune IME, SMS trace, CBS / DISM / Panther, Windows Update ETL, Windows event logs
    (.evtx files and channels such as evt:System) and plain text logs.
    Large files are read in the background and only the visible rows are drawn.
    Filter by text, severity, component, thread and time, find with F3, merge several
    logs by timestamp, follow files as they are written, and decode HRESULT, Win32,
    MSI and ConfigMgr error codes.

    Files are opened read-only with full sharing, so clients keep writing and rolling
    their logs while they are open here.

.PARAMETER Path
    Logs to open at start. Wildcards are allowed for files. Event log channels are written
    evt:<channel> or evt:\\<computer>\<channel>. Several logs are merged by time.

.EXAMPLE
    .\LogVice.ps1

.EXAMPLE
    .\LogVice.ps1 C:\Windows\CCM\Logs\AppEnforce.log, C:\Windows\CCM\Logs\AppDiscovery.log

.EXAMPLE
    .\LogVice.ps1 \\PC0123\C$\Windows\CCM\Logs\CcmExec*.lo*

.EXAMPLE
    .\LogVice.ps1 evt:Application, C:\Windows\CCM\Logs\AppEnforce.log

.NOTES
    Runs in Windows PowerShell 5.1. When started from PowerShell 7 it relaunches itself there.
    Needs FullLanguage mode, because the viewer is compiled with Add-Type at start.

    Version 1.4.0, developed by Daniel L. Baldissera

    Alt+Home start page    Ctrl+O open            Ctrl+B browse logs
    Ctrl+Shift+O merge in  Ctrl+F find            Ctrl+, settings and about
    F3 / Shift+F3 next / previous match           F8 / Shift+F8 next / previous error
    F7 / Shift+F7 next / previous highlighted row Ctrl+G go to line
    Ctrl+L error lookup    F5 reload              Ctrl+C copy selected rows
    Hold Ctrl while dropping files to merge them into the current view.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Path
)

if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    throw 'LogVice needs Windows.'
}

if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
    throw "LogVice compiles its viewer with Add-Type, which $($ExecutionContext.SessionState.LanguageMode) mode blocks."
}

$files = foreach ($item in $Path) {
    if ($item -like 'evt:*') {
        $item
        continue
    }
    $resolved = Resolve-Path -Path $item -ErrorAction SilentlyContinue
    if (-not $resolved) {
        Write-Warning "Not found: $item"
        continue
    }
    $resolved | Where-Object { $_.Provider.Name -eq 'FileSystem' } | ForEach-Object { $_.ProviderPath }
}

$isCore = $PSVersionTable.PSEdition -eq 'Core'
$isSta = [System.Threading.Thread]::CurrentThread.GetApartmentState() -eq 'STA'
if ($isCore -or -not $isSta) {
    $exe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $argList = @('-NoProfile', '-STA', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', "`"$PSCommandPath`"")
    if ($files) {
        $argList += foreach ($file in $files) { "`"$file`"" }
    }
    Start-Process -FilePath $exe -ArgumentList $argList
    return
}

$source = @'
__CSHARP__
'@

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

if (-not ('LogVice.Program' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Core -IgnoreWarnings
}

[LogVice.Program]::ScriptPath = $PSCommandPath
[LogVice.Program]::Run([string[]]$files)
