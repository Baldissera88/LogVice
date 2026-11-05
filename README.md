<p align="center">
  <img src="docs/images/logo.png" alt="LogVice logo" width="112">
</p>

<h1 align="center">LogVice</h1>

<p align="center">
  A fast, modern CMTrace-style log viewer for ConfigMgr, Intune, Windows logs and Windows event logs.
</p>

<p align="center">
  <img alt="Version 1.4.0" src="https://img.shields.io/badge/version-1.4.0-005fb8">
  <img alt="Windows 10, 11 and Server 2016 or later" src="https://img.shields.io/badge/Windows-10%20%7C%2011%20%7C%20Server%202016%2B-0078d4">
  <img alt=".NET Framework 4.5 or later" src="https://img.shields.io/badge/.NET%20Framework-4.5%2B-512bd4">
  <img alt="No dependencies" src="https://img.shields.io/badge/dependencies-none-0f7b0f">
  <a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/license-MIT-6b4fbb"></a>
</p>

![LogVice showing three AppEnforce.log parts merged by time, with highlight rules and a linked error code](docs/images/log-light.png)

LogVice opens the logs you read every day as an endpoint admin: ConfigMgr client and site server logs, Intune Management Extension logs, CBS, DISM and Panther logs, Windows Update ETL traces and Windows event logs. It reads large files in the background, merges several logs by time, follows them as they are written, and explains error codes, in a Windows 11 style window with light and dark themes.

It is one small exe with no installer requirement and nothing to configure. An MSI and a single PowerShell script are available too. Free and open source under the MIT license.

> LogVice was called Trace Lens up to version 1.3.2. The MSI upgrades an installed Trace Lens, and your settings come along.

## Features

- **Large files, fast.** Logs are read in the background and only the visible rows are drawn, so a 500 MB log opens without freezing the window.
- **All the usual formats.** CMTrace (ConfigMgr and Intune IME), SMS trace, CBS, DISM and Panther, Windows Update ETL traces, and any plain text log with timestamps. UTF-8, UTF-16 and ANSI are detected.
- **Windows event logs.** Open exported `.evtx` files, or read live channels such as Application, System, Security, Intune MDM (DeviceManagement-Enterprise-Diagnostics-Provider), Entra, Autopilot and Windows Update, on this computer or another one. Errors and warnings are marked like in CMTrace.
- **One timeline across sources.** Merge an Intune IME log with the MDM event log, or AppEnforce.log with the Application log, and read them by time.
- **Browse logs.** Finds ConfigMgr site server, management point, distribution point, console, client, client setup and task sequence logs, Intune Management Extension logs, Windows logs and event logs for you, on this computer or another one.
- **Connect as.** Read logs on a server with your admin account, without Run as or a second session.
- **Merge by time.** Open several logs and read them as one timeline. Auto-merge adds the rolled-over parts of a log (`.lo_`, dated copies, `CbsPersist_*`, `WindowsUpdate.*.etl`) by itself.
- **Live tail.** Follow logs and local event log channels while they are written, including rollover.
- **Filters and find.** Filter by text or regex, severity, component, thread and time. Find with F3, jump between errors with F8.
- **Highlights.** Your own rules with a label, a match and a colour, for example *Detection*, *Download* or *Exit code*. Filter by rule and jump between them with F7.
- **Error lookup.** HRESULT, Win32, MSI, Windows Update and ConfigMgr codes are explained in the detail pane. Codes in a message are links.
- **Line numbers.** See where each entry is in the file, or the record ID of an event, and press Ctrl+G to go to it.
- **Administrator rights only when needed.** LogVice runs as a normal user and offers to restart as administrator when a log needs it, such as the Security event log.
- **Read-only and private.** Files are opened read-only with full sharing, so clients keep writing and rolling them. Nothing leaves the computer.

<table>
  <tr>
    <td><img src="docs/images/log-dark.png" alt="LogVice in the dark theme"></td>
    <td><img src="docs/images/highlights.png" alt="Highlight rules in Settings"></td>
  </tr>
  <tr>
    <td align="center">Dark theme</td>
    <td align="center">Highlight rules</td>
  </tr>
</table>

## Download

Get the latest version from [Releases](../../releases). Pick the package that suits you:

| Package | Use it when |
|---|---|
| `LogVice.msi` | You deploy to many devices with Intune or ConfigMgr. Installs per machine, adds a Start menu shortcut and *Open with* for `.log`, `.lo_`, `.etl` and `.evtx` |
| `LogVice.exe` | You want a portable tool on a USB stick, a share or a jump server. No install needed |
| `LogVice.ps1` | Only scripts are allowed. Compiles the same viewer at start with Windows PowerShell 5.1 |

### Requirements

- Windows 10, Windows 11 or Windows Server 2016 or later
- .NET Framework 4.5 or later (included in all supported Windows versions)
- `LogVice.ps1` needs Windows PowerShell 5.1 in Full Language mode

## Deploy with Intune or ConfigMgr

The MSI installs per machine, needs no restart and upgrades any earlier version in place, Trace Lens included.

| Setting | Value |
|---|---|
| Install command | `msiexec /i LogVice.msi /qn` |
| Uninstall command | `msiexec /x {2BDC8F66-2461-46EE-AA50-3264BCC5F34A} /qn` |
| Detection | MSI product code `{2BDC8F66-2461-46EE-AA50-3264BCC5F34A}` (version 1.4.0) |
| Upgrade code | `{2A8DF866-E6D4-4A44-ABAC-C64CA0D367B3}` (the same for every version) |
| Install context | System |

Each new version has a new product code, listed in the [changelog](CHANGELOG.md). Add it as a new app that supersedes the earlier one.

## Usage

```powershell
# Open one log
LogVice.exe C:\Windows\CCM\Logs\AppEnforce.log

# Open several logs merged by time
LogVice.exe C:\Windows\CCM\Logs\AppDiscovery.log C:\Windows\CCM\Logs\AppEnforce.log

# Wildcards and remote computers work too
LogVice.exe \\PC0123\C$\Windows\CCM\Logs\CcmExec*.lo*

# Event logs: an exported file, a channel here, or a channel on another computer
LogVice.exe C:\Temp\System.evtx
LogVice.exe evt:Application "evt:Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin"
LogVice.exe evt:\\PC0123\System

# Merge a log file with an event log
LogVice.exe C:\ProgramData\Microsoft\IntuneManagementExtension\Logs\IntuneManagementExtension.log "evt:Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin"

# The script version takes the same arguments
.\LogVice.ps1 C:\ProgramData\Microsoft\IntuneManagementExtension\Logs\IntuneManagementExtension.log
```

You can also drag files onto the window. Hold Ctrl while dropping to merge them into what is open.

### Event logs

In Browse logs, the **Event logs** group has two entries:

- **Common event logs**: Application, System, Security, Setup, Intune MDM, Entra (AAD), device registration, Autopilot, provisioning, Windows Update, BITS, Group Policy, BitLocker, Defender, AppLocker, Code Integrity, AppX deployment, PowerShell, Task Scheduler, WMI and WinRM.
- **All event logs**: every channel that has events.

Select one or more channels and open or merge them. Each event becomes one row:

| Column | Shows |
|---|---|
| Message | `[Event ID]` and the event text |
| Severity | Critical and Error as errors, Warning and audit failures as warnings |
| Component | The event provider |
| Thread | The thread ID of the event |
| Line | The record ID. Ctrl+G goes to a record |
| Source (detail pane) | Event ID and task category |

Live view follows channels on this computer. Reading another computer's event logs uses its event log service, so the firewall rule group *Remote Event Log Management* must be enabled there. Connect as works for event logs too.

### Keyboard shortcuts

| Keys | Action |
|---|---|
| Ctrl+O | Open logs |
| Ctrl+B | Browse logs |
| Ctrl+Shift+O | Merge in more logs |
| Alt+Home | Start page |
| Ctrl+F | Find |
| F3 / Shift+F3 | Next / previous match |
| F8 / Shift+F8 | Next / previous error |
| F7 / Shift+F7 | Next / previous highlighted row |
| Ctrl+G | Go to line |
| Ctrl+L | Error lookup |
| F5 | Reload |
| Ctrl+C | Copy selected rows |
| Ctrl+, | Settings, Highlights and About |

### Where logs are found

Browse logs looks in these places, on this computer and, through the admin shares and the event log service, on other computers:

| Group | Folders |
|---|---|
| ConfigMgr | Site server and management point logs (from the registry or the default folder), `SMS_<site code>` shares, distribution point `SMS_DP$\sms\logs`, site setup, console `AdminUILog`, client `CCM\Logs`, `ccmsetup\Logs`, task sequence `SMSTSLog`, WSUS `LogFiles` |
| Intune | `C:\ProgramData\Microsoft\IntuneManagementExtension\Logs` |
| Windows | `C:\Windows\Logs\WindowsUpdate` (ETL), `SoftwareDistribution\ReportingEvents.log`, `C:\Windows\Logs` (CBS, DISM), Panther, `$WINDOWS.~BT`, `debug`, `Temp` |
| Event logs | Common event log channels, and every channel that has events |

Add your own folders, for example a share with collected logs, under Settings.

## Settings

Settings are stored per user in `HKEY_CURRENT_USER\Software\LogVice`. Delete that key to reset everything. On first start, LogVice copies the settings from `HKEY_CURRENT_USER\Software\Trace Lens` if they are there. LogVice writes nothing else, except a temporary copy of an ETL file while it is read.

## Build from source

LogVice is plain C# 5 and Windows Forms, with no NuGet packages.

**On Windows**, with the compiler that ships with .NET Framework:

```powershell
.\build.ps1          # LogVice.exe and LogVice.ps1
.\build.ps1 -Msi     # also LogVice.msi, needs WiX Toolset 3
```

**On Linux or macOS**, with Mono and msitools:

```sh
./build.sh           # LogVice.exe
./package.sh         # LogVice.exe, LogVice.ps1 and LogVice.msi
```

| Path | Contents |
|---|---|
| `src/Core.cs` | Log store, parsers for each format, loader and error code tables |
| `src/Logs.Etl.cs` | ETW trace reader for ETL files |
| `src/Logs.EventLog.cs` | Windows event log reader for channels and `.evtx` files |
| `src/Logs.Locations.cs` | Log discovery and auto-merge of spanned logs |
| `src/Net.Remote.cs` | Connect as: Windows sign-in dialog and SMB session |
| `src/Ui.*.cs` | The window, log grid, browser, settings and dialogs |
| `wrapper.ps1` | Template for `LogVice.ps1`. The build places the C# source inside it |
| `LogVice.wxs` | WiX source for the MSI |

## Known limitations

- The exe, MSI and script are unsigned. SmartScreen warns on downloaded copies, and AppLocker or WDAC need them signed with your own certificate.
- Windows Update traces from Windows Server 2016 and Windows 10 1607 and earlier need Microsoft symbol files. Run `Get-WindowsUpdateLog` for those.
- Browsing another computer needs an administrator account on it and SMB access. Local accounts other than the built-in Administrator are blocked over the network by Remote UAC.
- Event log channels on another computer are not followed live. Press F5 to read new events.
- An `.evtx` file from another computer is shown with the event texts installed on this computer. Events from a provider that is not installed here show their values instead of the full text.
- Merging sorts by the local time in each log, so logs from different time zones can interleave out of order.
- Logs are held in memory, about two to three times their size on disk.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for every version.

## Contributing

Bug reports, log samples that do not parse well, and ideas are welcome in [Issues](../../issues). Remove computer names, user names and other private data from logs before you attach them. Pull requests are welcome too. Please keep to C# 5 and .NET Framework 4.5 so LogVice keeps running on every supported Windows version without extra installs.

## License

LogVice is released under the [MIT license](LICENSE). You may use, change and share it freely, also at work and for customers.

## Author

Developed by **Daniel L. Baldissera**.
