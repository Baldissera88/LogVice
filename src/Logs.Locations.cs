namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Win32;

    public sealed class LogLocation
    {
        public string Name;
        public string Group;
        public string Path;
        public string Pattern = "*.log;*.lo_";
        public int Depth;
        public bool Custom;
        public bool Denied;
        public string Kind = "";
        public string Computer;
        public bool All;
        public string Problem;

        public string Where
        {
            get
            {
                if (Kind != "evt") return Path;
                string w = All ? "Every event log channel with events" : "Application, System, Security, Intune MDM, Entra, Autopilot, Windows Update and more";
                return Computer != null ? w + " on " + Computer : w;
            }
        }
        public Glyph Glyph = Glyph.Folder;
        public int FileCount;
        public DateTime Newest = DateTime.MinValue;

        public string Summary
        {
            get
            {
                if (Denied) return "Needs administrator rights";
                if (Kind == "evt" && Problem != null) return "Not reachable";
                if (Kind == "evt" && All) return "Channels that have events";
                if (Kind == "evt") return FileCount.ToString("N0") + (FileCount == 1 ? " channel" : " channels");
                string n = FileCount == 1 ? "1 log" : FileCount.ToString("N0") + " logs";
                if (Newest == DateTime.MinValue) return n;
                return n + " \u00B7 updated " + LogFile.When(Newest);
            }
        }
    }

    public sealed class LogFile
    {
        public string Path;
        public string Rel;
        public long Size;
        public DateTime Modified;
        public bool Rolled;

        public static string When(DateTime t)
        {
            DateTime now = DateTime.Now;
            if (t.Date == now.Date) return "today " + t.ToString("HH:mm");
            if (t.Date == now.Date.AddDays(-1)) return "yesterday " + t.ToString("HH:mm");
            return t.ToString("g");
        }
    }

    public static class LogLocations
    {
        public const string ConfigMgr = "Configuration Manager";
        public const string Intune = "Intune";
        public const string Windows = "Windows";
        public const string Yours = "Your folders";

        static readonly Regex SiteShare = new Regex("^SMS_[A-Z0-9]{3}$", RegexOptions.IgnoreCase);

        public static List<LogLocation> Discover(string computer, List<string> custom)
        {
            bool remote = !string.IsNullOrEmpty(computer) && !IsLocal(computer);
            List<LogLocation> list;
            try { list = remote ? Remote(computer.Trim().TrimStart('\\')) : Local(); }
            catch (Exception) { list = new List<LogLocation>(); }
            if (custom != null)
            {
                foreach (string c in custom)
                {
                    string name = System.IO.Path.GetFileName(c.TrimEnd('\\'));
                    if (string.IsNullOrEmpty(name)) name = c;
                    Add(list, Yours, name, c, Glyph.Folder, 0, null).Custom = true;
                }
            }
            List<LogLocation> found = new List<LogLocation>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (LogLocation l in list)
            {
                string key = l.Path.TrimEnd('\\') + "|" + l.Pattern;
                if (seen.ContainsKey(key)) continue;
                seen[key] = true;
                if (l.Kind == "evt")
                {
                    l.Problem = null;
                    try { l.FileCount = EventLogs.CountChannels(l.Computer, l.All); }
                    catch (UnauthorizedAccessException) { l.FileCount = 0; l.Denied = true; }
                    catch (Exception ex) { l.FileCount = 0; l.Problem = EventLogs.Explain(ex, l.Computer); }
                    if (l.FileCount > 0 || l.Denied || (l.Problem != null && l.Computer != null && !l.All)) found.Add(l);
                    continue;
                }
                if (!Exists(l.Path)) { if (l.Custom) found.Add(l); continue; }
                try { Count(l); } catch (Exception) { }
                if (l.FileCount > 0 || l.Custom || l.Denied) found.Add(l);
            }
            return found;
        }

        public static bool IsLocal(string computer)
        {
            string c = (computer ?? "").Trim().TrimStart('\\');
            if (c.Length == 0 || c == "." || string.Equals(c, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            string host = c.Split('.')[0];
            return string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        }

        static LogLocation Add(List<LogLocation> list, string group, string name, string path, Glyph glyph, int depth, string pattern)
        {
            LogLocation l = new LogLocation();
            l.Group = group;
            l.Name = name;
            l.Path = path;
            l.Glyph = glyph;
            l.Depth = depth;
            if (pattern != null) l.Pattern = pattern;
            list.Add(l);
            return l;
        }

        static string Reg(RegistryView view, string key, string value)
        {
            try
            {
                using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey k = hk.OpenSubKey(key))
                {
                    object v = k == null ? null : k.GetValue(value);
                    string s = v as string;
                    return string.IsNullOrEmpty(s) ? null : Environment.ExpandEnvironmentVariables(s);
                }
            }
            catch (Exception) { return null; }
        }

        static string P(params string[] parts)
        {
            try { return System.IO.Path.Combine(parts); } catch (Exception) { return ""; }
        }

        static List<LogLocation> Local()
        {
            List<LogLocation> list = new List<LogLocation>();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pf = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? pf;
            string data = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string sys = null;
            try { if (!string.IsNullOrEmpty(win)) sys = System.IO.Path.GetPathRoot(win); } catch (Exception) { }
            if (string.IsNullOrEmpty(sys))
            {
                string drive = Environment.GetEnvironmentVariable("SystemDrive");
                sys = string.IsNullOrEmpty(drive) ? @"C:\" : drive.TrimEnd('\\') + @"\";
            }
            if (string.IsNullOrEmpty(win)) win = P(sys, "Windows");
            if (string.IsNullOrEmpty(data)) data = P(sys, "ProgramData");

            string site = Reg(RegistryView.Registry64, @"SOFTWARE\Microsoft\SMS\Identification", "Installation Directory");
            if (site != null)
            {
                Add(list, ConfigMgr, "Site server", P(site, "Logs"), Glyph.Server, 0, null);
                Add(list, ConfigMgr, "Management point", P(site, "SMS_CCM", "Logs"), Glyph.Server, 0, null);
            }
            Add(list, ConfigMgr, "Site server", P(pf, "Microsoft Configuration Manager", "Logs"), Glyph.Server, 0, null);
            Add(list, ConfigMgr, "Management point", P(pf, "SMS_CCM", "Logs"), Glyph.Server, 0, null);
            Add(list, ConfigMgr, "Management point", P(sys, "SMS_CCM", "Logs"), Glyph.Server, 0, null);
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed) continue;
                    Add(list, ConfigMgr, "Distribution point", P(d.RootDirectory.FullName, "SMS_DP$", "sms", "logs"), Glyph.Server, 0, null);
                }
            }
            catch (Exception) { }
            Add(list, ConfigMgr, "Site setup", sys, Glyph.Server, 0, "ConfigMgr*.log;CMUpdate*.log");
            string ui = Reg(RegistryView.Registry32, @"SOFTWARE\Microsoft\ConfigMgr10\Setup", "UI Installation Directory");
            if (ui != null) Add(list, ConfigMgr, "Console", P(ui, "AdminUILog"), Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Console", P(pf86, "Microsoft Endpoint Manager", "AdminConsole", "AdminUILog"), Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Console", P(pf86, "Microsoft Configuration Manager", "AdminConsole", "AdminUILog"), Glyph.Computer, 0, null);
            string client = Reg(RegistryView.Registry64, @"SOFTWARE\Microsoft\CCM\Logging\@Global", "LogDirectory");
            if (client != null) Add(list, ConfigMgr, "Client", client, Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Client", P(win, "CCM", "Logs"), Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Client setup", P(win, "ccmsetup", "Logs"), Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Task sequence", P(win, "CCM", "Logs", "SMSTSLog"), Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Task sequence", P(sys, "_SMSTaskSequence", "Logs"), Glyph.Computer, 1, null);
            Add(list, ConfigMgr, "Software update point (WSUS)", P(pf, "Update Services", "LogFiles"), Glyph.Server, 0, "*.log;*.lo_;*.txt");
            Add(list, Intune, "Intune Management Extension", P(data, "Microsoft", "IntuneManagementExtension", "Logs"), Glyph.Computer, 0, null);
            Add(list, Windows, "Windows Update", P(win, "Logs", "WindowsUpdate"), Glyph.Folder, 0, "*.etl");
            Add(list, Windows, "Windows Update reporting", P(win, "SoftwareDistribution"), Glyph.Folder, 0, "ReportingEvents.log");
            Add(list, Windows, "Servicing (CBS, DISM)", P(win, "Logs"), Glyph.Folder, 2, null);
            Add(list, Windows, "Setup (Panther)", P(win, "Panther"), Glyph.Folder, 1, null);
            Add(list, Windows, "Upgrade (Panther)", P(sys, "$WINDOWS.~BT", "Sources", "Panther"), Glyph.Folder, 0, null);
            Add(list, Windows, "Debug (NetSetup, PASSWD)", P(win, "debug"), Glyph.Folder, 0, null);
            Add(list, Windows, "Temp (installer logs)", P(win, "Temp"), Glyph.Folder, 0, "*.log");
            AddEvents(list, null);
            return list;
        }

        static void AddEvents(List<LogLocation> list, string pc)
        {
            string root = pc == null ? EventLogs.Prefix : EventLogs.Prefix + @"\\" + pc + @"\";
            LogLocation common = Add(list, EventLogs.Group, "Common event logs", root + "common", Glyph.Events, 0, "");
            common.Kind = "evt";
            common.Computer = pc;
            LogLocation all = Add(list, EventLogs.Group, "All event logs", root + "all", Glyph.Events, 0, "");
            all.Kind = "evt";
            all.Computer = pc;
            all.All = true;
        }

        static List<LogLocation> Remote(string pc)
        {
            List<LogLocation> list = new List<LogLocation>();
            string root = @"\\" + pc + @"\";
            string admin = root + @"admin$\";
            string c = root + @"C$\";
            foreach (string share in NativeMethods.Shares(pc))
            {
                if (!SiteShare.IsMatch(share)) continue;
                Add(list, ConfigMgr, "Site server (" + share.Substring(4).ToUpperInvariant() + ")", root + share + @"\Logs", Glyph.Server, 0, null);
                Add(list, ConfigMgr, "Management point", root + share + @"\SMS_CCM\Logs", Glyph.Server, 0, null);
            }
            Add(list, ConfigMgr, "Site server", c + @"Program Files\Microsoft Configuration Manager\Logs", Glyph.Server, 0, null);
            Add(list, ConfigMgr, "Management point", c + @"Program Files\SMS_CCM\Logs", Glyph.Server, 0, null);
            Add(list, ConfigMgr, "Distribution point", root + @"SMS_DP$\sms\logs", Glyph.Server, 0, null);
            Add(list, ConfigMgr, "Site setup", c, Glyph.Server, 0, "ConfigMgr*.log;CMUpdate*.log");
            Add(list, ConfigMgr, "Console", c + @"Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\AdminUILog", Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Client", admin + @"CCM\Logs", Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Client setup", admin + @"ccmsetup\Logs", Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Task sequence", admin + @"CCM\Logs\SMSTSLog", Glyph.Computer, 0, null);
            Add(list, ConfigMgr, "Task sequence", c + @"_SMSTaskSequence\Logs", Glyph.Computer, 1, null);
            Add(list, ConfigMgr, "Software update point (WSUS)", c + @"Program Files\Update Services\LogFiles", Glyph.Server, 0, "*.log;*.lo_;*.txt");
            Add(list, Intune, "Intune Management Extension", c + @"ProgramData\Microsoft\IntuneManagementExtension\Logs", Glyph.Computer, 0, null);
            Add(list, Windows, "Windows Update", admin + @"Logs\WindowsUpdate", Glyph.Folder, 0, "*.etl");
            Add(list, Windows, "Windows Update reporting", admin + "SoftwareDistribution", Glyph.Folder, 0, "ReportingEvents.log");
            Add(list, Windows, "Servicing (CBS, DISM)", admin + "Logs", Glyph.Folder, 2, null);
            Add(list, Windows, "Setup (Panther)", admin + "Panther", Glyph.Folder, 1, null);
            Add(list, Windows, "Debug (NetSetup, PASSWD)", admin + "debug", Glyph.Folder, 0, null);
            Add(list, Windows, "Temp (installer logs)", admin + "Temp", Glyph.Folder, 0, "*.log");
            AddEvents(list, pc);
            return list;
        }

        public static bool Reachable(string computer, int timeoutMs, out string error)
        {
            error = null;
            string pc = computer.Trim().TrimStart('\\');
            string path = @"\\" + pc + @"\admin$";
            Task<bool> t = Task.Factory.StartNew(delegate { return Exists(path); });
            if (!t.Wait(timeoutMs))
            {
                error = "No answer from " + pc + " within " + (timeoutMs / 1000) + " seconds. Check that the name is right and that the computer is on the network.";
                return false;
            }
            if (!t.Result)
            {
                error = "Could not open \\\\" + pc + "\\admin$. Check the name, and that your account is an administrator on that computer.";
                return false;
            }
            return true;
        }

        static bool Exists(string path)
        {
            try { return !string.IsNullOrEmpty(path) && Directory.Exists(path); } catch (Exception) { return false; }
        }

        static void Count(LogLocation l)
        {
            List<LogFile> files = List(l);
            l.FileCount = files.Count;
            foreach (LogFile f in files) if (f.Modified > l.Newest) l.Newest = f.Modified;
        }

        static Regex PatternRx(string pattern)
        {
            List<string> parts = new List<string>();
            foreach (string p in pattern.Split(';'))
            {
                string t = p.Trim();
                if (t.Length == 0) continue;
                parts.Add(Regex.Escape(t).Replace("\\*", ".*").Replace("\\?", "."));
            }
            return new Regex("^(" + string.Join("|", parts.ToArray()) + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static List<LogFile> List(LogLocation l)
        {
            List<LogFile> list = new List<LogFile>();
            l.Denied = false;
            if (l.Kind == "evt")
            {
                l.Problem = null;
                try { return EventLogs.Channels(l.Computer, l.All); }
                catch (UnauthorizedAccessException) { l.Denied = true; return list; }
                catch (Exception ex) { l.Problem = EventLogs.Explain(ex, l.Computer); return list; }
            }
            try
            {
                using (IEnumerator<FileInfo> probe = new DirectoryInfo(l.Path).EnumerateFiles().GetEnumerator()) probe.MoveNext();
            }
            catch (UnauthorizedAccessException)
            {
                l.Denied = true;
                return list;
            }
            catch (Exception) { }
            try
            {
                Regex rx = PatternRx(l.Pattern);
                Walk(new DirectoryInfo(l.Path), l.Path, rx, l.Depth, list);
            }
            catch (Exception) { }
            return list;
        }

        static void Walk(DirectoryInfo dir, string root, Regex rx, int depth, List<LogFile> list)
        {
            try
            {
                foreach (FileInfo f in dir.EnumerateFiles())
                {
                    if (!rx.IsMatch(f.Name)) continue;
                    LogFile lf = new LogFile();
                    lf.Path = f.FullName;
                    string rel = f.FullName.Length > root.Length ? f.FullName.Substring(root.Length).TrimStart('\\', '/') : f.Name;
                    lf.Rel = rel;
                    lf.Size = f.Length;
                    lf.Modified = f.LastWriteTime;
                    lf.Rolled = f.Extension.Equals(".lo_", StringComparison.OrdinalIgnoreCase);
                    list.Add(lf);
                    if (list.Count >= 20000) return;
                }
                if (depth <= 0) return;
                foreach (DirectoryInfo d in dir.EnumerateDirectories())
                {
                    if ((d.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    Walk(d, root, rx, depth - 1, list);
                }
            }
            catch (Exception) { }
        }

        static readonly Regex RxStamp = new Regex(@"(?:[-_.](?:\d{8}|\d{4}-\d{2}-\d{2})(?:[-_.T]?(?:\d{6}|\d{2}-\d{2}-\d{2}))?|[-_]\d{14})(?:[-_.]\d{1,4})*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Family(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (ext != ".log" && ext != ".lo_" && ext != ".etl") return null;
            string stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string key = RxStamp.Replace(stem, "");
            if (key.Length == 0) return null;
            if (string.Equals(key, "CbsPersist", StringComparison.OrdinalIgnoreCase)) key = "CBS";
            return key;
        }

        static string Kind(string fileName)
        {
            return fileName.EndsWith(".etl", StringComparison.OrdinalIgnoreCase) ? "etl" : "log";
        }

        public const int MaxParts = 20;

        public static List<string> Spanned(IEnumerable<string> paths, out int added)
        {
            added = 0;
            List<string> outList = new List<string>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> done = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            List<string> input = new List<string>(paths);
            foreach (string p in input) if (!seen.ContainsKey(p)) { seen[p] = true; outList.Add(p); }
            foreach (string p in input)
            {
                if (EventLogs.IsChannel(p)) continue;
                string name = System.IO.Path.GetFileName(p);
                string key = Family(name);
                if (key == null) continue;
                string dir = System.IO.Path.GetDirectoryName(p);
                string kind = Kind(name);
                string famId = dir + "|" + key + "|" + kind;
                if (string.IsNullOrEmpty(dir) || done.ContainsKey(famId)) continue;
                done[famId] = true;
                List<FileInfo> parts = new List<FileInfo>();
                try
                {
                    foreach (FileInfo f in new DirectoryInfo(dir).EnumerateFiles())
                    {
                        if (seen.ContainsKey(f.FullName) || Kind(f.Name) != kind) continue;
                        string k = Family(f.Name);
                        if (k != null && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) parts.Add(f);
                    }
                }
                catch (Exception) { continue; }
                parts.Sort(delegate (FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                for (int i = 0; i < parts.Count && i < MaxParts; i++)
                {
                    seen[parts[i].FullName] = true;
                    outList.Add(parts[i].FullName);
                    added++;
                }
            }
            return outList;
        }

        public static List<string> WithHistory(IEnumerable<string> paths)
        {
            List<string> outList = new List<string>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in paths)
            {
                if (!seen.ContainsKey(p)) { seen[p] = true; outList.Add(p); }
                if (!p.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) continue;
                string lo = p.Substring(0, p.Length - 4) + ".lo_";
                try
                {
                    if (!seen.ContainsKey(lo) && File.Exists(lo)) { seen[lo] = true; outList.Add(lo); }
                }
                catch (Exception) { }
            }
            return outList;
        }
    }
}
