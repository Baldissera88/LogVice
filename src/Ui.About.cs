namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Text;
    using System.Windows.Forms;
    using Microsoft.Win32;

    // Facts about this installation, for About and for Copy details.
    public static class SysInfo
    {
        public static List<string[]> Details()
        {
            List<string[]> d = new List<string[]>();
            d.Add(new string[] { "Version", Program.Version });
            d.Add(new string[] { "Developer", Program.Developer });
            d.Add(new string[] { ".NET Framework", NetVersion() });
            d.Add(new string[] { "Windows", WindowsVersion() });
            d.Add(new string[] { "Process", (Environment.Is64BitProcess ? "64-bit" : "32-bit") + ", " + Environment.ProcessorCount + " logical processors" });
            d.Add(new string[] { "Computer", Environment.MachineName });
            d.Add(new string[] { "Program", Program.Location });
            d.Add(new string[] { "Settings", Settings.Location });
            d.Add(new string[] { "Fonts", Fonts.UiName + ", " + Fonts.MonoName });
            return d;
        }

        public static string NetVersion()
        {
            try
            {
                using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = hk.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    object v = k == null ? null : k.GetValue("Release");
                    if (v is int)
                    {
                        int r = (int)v;
                        string name = r >= 533320 ? "4.8.1" : r >= 528040 ? "4.8" : r >= 461808 ? "4.7.2" : r >= 461308 ? "4.7.1" : r >= 460798 ? "4.7" :
                            r >= 394802 ? "4.6.2" : r >= 394254 ? "4.6.1" : r >= 393295 ? "4.6" : r >= 379893 ? "4.5.2" : r >= 378675 ? "4.5.1" : "4.5";
                        return name + " (release " + r + ")";
                    }
                }
            }
            catch (Exception) { }
            return "CLR " + Environment.Version;
        }

        public static string WindowsVersion()
        {
            try
            {
                using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = hk.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        string name = k.GetValue("ProductName") as string ?? "Windows";
                        string disp = k.GetValue("DisplayVersion") as string ?? k.GetValue("ReleaseId") as string ?? "";
                        string build = k.GetValue("CurrentBuild") as string ?? "";
                        object ubr = k.GetValue("UBR");
                        int b;
                        if (int.TryParse(build, out b) && b >= 22000 && name.StartsWith("Windows 10", StringComparison.Ordinal)) name = "Windows 11" + name.Substring(10);
                        string s = name;
                        if (disp.Length > 0) s += " " + disp;
                        if (build.Length > 0) s += " (build " + build + (ubr is int ? "." + ubr : "") + ")";
                        return s;
                    }
                }
            }
            catch (Exception) { }
            return Environment.OSVersion.VersionString;
        }
    }

    // The texts of the Help pages: what LogVice reads, its keyboard shortcuts and its command line.
    public static class HelpTexts
    {
        public const string Blurb = "A log viewer for ConfigMgr, Intune and Windows logs. It reads large files in the background, merges several logs by time, follows logs as they are written, follows one app or update through every client log, and explains error codes.";

        public const string Privacy = "Files are opened read-only and shared, so clients keep writing them. LogVice sends nothing about you or your logs anywhere. The only internet calls are for updates: once a day LogVice asks GitHub for the latest version, and when you select Install it downloads that version from GitHub. Both can be turned off under Settings > Updates. It only reaches another computer when you connect to it in Browse logs, through that computer's admin shares and its event log service. With Connect as, the account is used only for that connection, and its password is kept only if you tick Remember my credentials, in Windows Credential Manager.";

        // The formats LogVice reads: the name, what it covers, and the glyph of its row.
        public static readonly string[][] Formats =
        {
            new string[] { "CMTrace", "ConfigMgr client and site server logs, Intune Management Extension logs (IntuneManagementExtension, AgentExecutor)." },
            new string[] { "SMS trace", "Older ConfigMgr site server logs with $$<component><time><thread> at the end of each line." },
            new string[] { "CBS, DISM and Panther", "CBS.log, DISM.log, setupact.log and setuperr.log." },
            new string[] { "ETW traces (.etl)", "Windows Update logs in C:\\Windows\\Logs\\WindowsUpdate and other ETL files, decoded with the event information Windows has on this computer. Open several WindowsUpdate*.etl files together to get the whole history in one view." },
            new string[] { "Windows event logs", "Live channels such as Application, System, Security and the Intune MDM, Entra, Autopilot and Windows Update logs, on this computer or on another one you connect to, and exported .evtx files. Errors and warnings are marked like in CMTrace, and live view picks up new events." },
            new string[] { "JSON Lines", "Logs with one JSON object per line (also called NDJSON), and files that are one array of objects: .json, .jsonl and .ndjson. Time, level, message, source and thread are found by their usual names, such as timestamp, level, message and logger. Other fields show under the message in the detail pane." },
            new string[] { "Plain text", "Any other text log. Timestamps at the start of a line are used for sorting and merging. UTF-8, UTF-16 and ANSI are detected." }
        };

        public static readonly string[][] Shortcuts =
        {
            new string[] { "Alt+Home", "Start page" },
            new string[] { "Ctrl+O", "Open log files" },
            new string[] { "Ctrl+Shift+O", "Merge in more log files, interleaved by time" },
            new string[] { "Alt+Left / Alt+Right", "Back and Forward through the logs you had open. The mouse back and forward buttons do the same" },
            new string[] { "Ctrl+S", "Save the open logs and filters as a session (.logvice file)" },
            new string[] { "Ctrl+Shift+C", "Copy the selected rows for a ticket: plain text with the rows around a single row and the error codes in them" },
            new string[] { "Ctrl+B", "Browse logs on this computer or another one. Press it again to go back" },
            new string[] { "Ctrl+I", "Follow an item through every client log" },
            new string[] { "F5", "Read the open files again" },
            new string[] { "Ctrl+L", "Error lookup" },
            new string[] { "Ctrl+F", "Find" },
            new string[] { "Enter / Shift+Enter", "In the find box: next / previous match" },
            new string[] { "F3 / Shift+F3", "Next / previous match" },
            new string[] { "F8 / Shift+F8", "Next / previous error" },
            new string[] { "F7 / Shift+F7", "Next / previous highlighted row" },
            new string[] { "Ctrl+G", "Go to a line of the log file" },
            new string[] { "Up, Down, PgUp, PgDn, Home, End", "Move through the rows. Hold Shift to select several" },
            new string[] { "Ctrl+C", "Copy the selected rows" },
            new string[] { "Ctrl+A", "Select all rows" },
            new string[] { "Ctrl+,", "Settings" },
            new string[] { "F1", "The Help menu" },
            new string[] { "Esc", "Clear the find box, leave the start page, or go back from Settings or Browse logs" }
        };

        // Every way to start LogVice. Options for screenshots are at the end.
        public static readonly string[][] CommandLine =
        {
            new string[] { "LogVice.exe", "Start with the start page" },
            new string[] { "LogVice.exe <file> [<file> ...]", "Open log files. Several files are merged by time" },
            new string[] { "LogVice.exe \"C:\\Windows\\CCM\\Logs\\*.log\"", "A wildcard opens every file that matches, merged by time" },
            new string[] { "LogVice.exe evt:<channel>", "Open a Windows event log, such as evt:Application. Use evt:\\\\<computer>\\<channel> for another computer" },
            new string[] { "LogVice.exe <file>.logvice", "Open a saved session: its logs, filters and search. Logs that are missing are named in the status bar" },
            new string[] { "--line <n>", "Select line n of the first file. If the line has no entry of its own, the next entry is selected" },
            new string[] { "--time \"yyyy-MM-dd HH:mm:ss[.fff]\"", "Select the first row at or after this time" },
            new string[] { "--find <text>", "Search for the text, starting from the selected row. Use --line or --time to choose the row first" },
            new string[] { "--filter <text>", "Only show rows that contain the text" },
            new string[] { "--errors", "Only show errors and warnings" },
            new string[] { "--demo", "Open made-up ConfigMgr, Intune and agent logs of CONTOSO-LT0417, merged by time. Nothing on this PC is read" },
            new string[] { "--theme <name>", "Use this theme for this run only: Light, Dark, Vice, Terminal, Amber or Synthwave. System follows Windows" },
            new string[] { "--timing", "Write how long each step of the start took to %TEMP%\\LogVice-start.txt, to find out why LogVice starts slowly" },
            new string[] { "Example", "LogVice.exe \"C:\\ProgramData\\Microsoft\\IntuneManagementExtension\\Logs\\AppWorkload.log\" --time \"2026-11-24 21:21:57\" --find \"Contoso Timesheet\" --theme vice" },
            new string[] { "--shot <file.png> [--size WxH]", "Save a picture of the window and exit. Nothing is saved in this run" },
            new string[] { "--page <name> [--select <text>]", "With --shot: show a page: welcome, about, shortcuts, commandline, ticket or history. --select picks a row by its text" }
        };
    }

    // About LogVice, Keyboard shortcuts and Command line: pages with cards, opened from the Help menu.
    public sealed class AboutForm : PageForm
    {
        public const string AboutPart = "about", ShortcutsPart = "shortcuts", CommandLinePart = "commandline";
        public event Action<UpdateInfo> InstallRequested;
        public event EventHandler WhatsNewRequested, EasterEgg;
        readonly KeyTable cli;
        SettingRow rowUpdate;
        FlatButton bUpdate;
        UpdateInfo update;
        int taps;
        DateTime lastTap = DateTime.MinValue;

        public static string TitleFor(string part)
        {
            return part == CommandLinePart ? "Command line" : part == ShortcutsPart ? "Keyboard shortcuts" : "About LogVice";
        }

        public AboutForm(Theme t, string part, UpdateInfo known) : base(TitleFor(part), t)
        {
            update = known;
            if (part == CommandLinePart)
            {
                Add(new SectionTitle("Command line"));
                cli = new KeyTable(true, HelpTexts.CommandLine);
                Add(cli);
                Add(new SettingRow(Glyph.Info, "Several files and wildcards", "Quote a path that has spaces. LogVice expands a wildcard itself, so it also works from a shortcut.", null));
                Add(new SettingRow(Glyph.Info, "Landing on a row", "--line, --time, --find, --filter and --errors work with files, event logs and sessions, alone or together. LogVice ignores an option it does not know and says so in the status bar.", null));
            }
            else if (part == ShortcutsPart)
            {
                Add(new SectionTitle("Keyboard shortcuts"));
                Add(new KeyTable(false, HelpTexts.Shortcuts));
            }
            else BuildAbout();
            if (part == ShortcutsPart) return;

            FlatButton bCopy = new FlatButton("Copy details", Glyph.Copy);
            bCopy.Outline = true;
            bCopy.Dock = DockStyle.Left;
            bCopy.ApplyTheme(t);
            bCopy.Click += Safe.Wrap(delegate
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("LogVice ").Append(Program.Version).Append(" (built ").Append(Program.BuildDate).Append(") by ").Append(Program.Developer).Append("\r\n");
                foreach (string[] kv in SysInfo.Details()) sb.Append(kv[0]).Append(": ").Append(kv[1]).Append("\r\n");
                if (cli != null) sb.Append("\r\n").Append(cli.AsText());
                bCopy.Text = Menus.Copy(sb.ToString()) ? "Copied" : "Clipboard busy";
            });
            footer.Controls.Add(bCopy);
        }

        void BuildAbout()
        {
            AboutHero hero = new AboutHero();
            hero.Version = "Version " + Program.Version;
            hero.Detail = "Built " + Program.BuildDate;
            hero.Developer = Program.Developer;
            hero.Blurb = HelpTexts.Blurb;
            hero.License = "Released under the MIT license  \u00b7  A companion to TrackVice and PolicyVice";
            hero.MarkTapped += delegate
            {
                DateTime now = DateTime.Now;
                taps = (now - lastTap).TotalSeconds < 2 ? taps + 1 : 1;
                lastTap = now;
                if (taps >= 5)
                {
                    taps = 0;
                    hero.Hint("");
                    if (EasterEgg != null) EasterEgg(this, EventArgs.Empty);
                }
                else if (taps >= 3) hero.Hint((5 - taps) == 1 ? "One more\u2026" : "Two more\u2026");
            };
            Add(hero);

            Add(new SectionTitle("Privacy"));
            Add(new SettingRow(Glyph.Shield, "Your logs stay where they are", HelpTexts.Privacy, null));

            Add(new SectionTitle("Supported formats"));
            foreach (string[] f in HelpTexts.Formats) Add(new SettingRow(f[0] == "Windows event logs" ? Glyph.Events : Glyph.File, f[0], f[1], null));

            Add(new SectionTitle("Updates"));
            bUpdate = SmallButton("Check now", delegate { UpdateClicked(); });
            rowUpdate = new SettingRow(Glyph.Live, "Updates", "Releases are published on GitHub: " + Updates.ReleasesUrl, bUpdate);
            Add(rowUpdate);
            FlatButton bNotes = SmallButton("Show", delegate { if (WhatsNewRequested != null) WhatsNewRequested(this, EventArgs.Empty); });
            Add(new SettingRow(Glyph.Text, "What's new", "Read what changed in this version and the ones before it.", bNotes));
            FlatButton bPage = SmallButton("Open", delegate { try { Process.Start(Updates.ReleasesUrl); } catch (Exception) { Safe.Say("Could not open " + Updates.ReleasesUrl); } });
            Add(new SettingRow(Glyph.Github, "LogVice on GitHub", "The releases, the source and the issues.", bPage));
            if (update != null) ShowUpdate(update);
        }

        void UpdateClicked()
        {
            if (update != null && update.Newer)
            {
                if (update.CanInstall && InstallRequested != null) InstallRequested(update);
                else try { Process.Start(update.Url); } catch (Exception) { }
                return;
            }
            bUpdate.Enabled = false;
            SetUpdateText("Checking GitHub\u2026", "Checking\u2026");
            Updates.CheckAsync(delegate (UpdateInfo r)
            {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)delegate { bUpdate.Enabled = true; ShowUpdate(r); }); } catch (InvalidOperationException) { }
            });
        }

        public void ShowUpdate(UpdateInfo r)
        {
            update = r;
            if (r == null || rowUpdate == null) return;
            if (r.Error != null) SetUpdateText(r.Error + " Releases are published on " + Updates.ReleasesUrl, "Check now");
            else if (r.Newer) SetUpdateText("LogVice " + r.Latest + " is available. You have " + Program.Version + ". " + (r.CanInstall ? "Install downloads it from GitHub, checks it against the release's checksum and installs it. LogVice then opens again with the same logs." : "Download opens the release page."), (r.CanInstall ? "Install " : "Download ") + r.Latest);
            else SetUpdateText("You have the latest version, " + Program.Version + ".", "Check now");
        }

        void SetUpdateText(string desc, string button)
        {
            rowUpdate.Desc = desc;
            bUpdate.Text = button;
            bUpdate.Compact = true;
            bUpdate.AutoWidth();
            bUpdate.Width += Dpi.S(8);
            rowUpdate.PerformLayout();
            Relayout();
            rowUpdate.Invalidate();
        }
    }
}
