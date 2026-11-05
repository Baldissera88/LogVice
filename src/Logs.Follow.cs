namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Management;
    using System.Security;
    using System.Text;
    using System.Text.RegularExpressions;

    public sealed class FollowItem
    {
        public string Kind;
        public string Name;
        public string Id;
        public string State;
        public string Source;
        public readonly List<string> Terms = new List<string>();
        internal string WmiAppId;
        internal string WmiAppRevision;
        internal bool WmiAppMachine;

        public string Line2
        {
            get
            {
                StringBuilder sb = new StringBuilder(Kind);
                if (!string.IsNullOrEmpty(State)) sb.Append("  \u00B7  ").Append(State);
                if (!string.IsNullOrEmpty(Id)) sb.Append("  \u00B7  ").Append(Id);
                return sb.ToString();
            }
        }
    }

    public sealed class FollowResult
    {
        public readonly List<string> Files = new List<string>();
        public readonly List<string> Terms = new List<string>();
        public readonly Dictionary<string, int> Hits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int Total;
        public string Pattern;
    }

    public static class Follow
    {
        public const string Application = "Application";
        public const string DeploymentType = "Deployment type";
        public const string Update = "Software update";
        public const string Package = "Package";
        public const string Baseline = "Configuration baseline";
        public const string IntuneApp = "Intune app";

        static readonly Regex RxGuid = new Regex(@"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", RegexOptions.Compiled);
        static readonly Regex RxDt = new Regex("App DT \"([^\"]+)\" ApplicationDeliveryType - (ScopeId_[^/\\s]+/DeploymentType_[0-9A-Fa-f-]{36}), Revision - (\\d+)", RegexOptions.Compiled);
        static readonly Regex RxUpdate = new Regex(@"Update \((Site_[^/\s]+/SUM_([0-9A-Fa-f-]{36}))\) Name \((.*?)\) ArticleID \((\d+)\)", RegexOptions.Compiled);
        static readonly Regex RxProgram = new Regex(@"package ([A-Z0-9]{3}[0-9A-F]{5}) program (.+?) state change", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex RxIme = new Regex("\\\\?\"Id\\\\?\"\\s*:\\s*\\\\?\"([0-9a-fA-F-]{36})\\\\?\"\\s*,\\s*\\\\?\"Name\\\\?\"\\s*:\\s*\\\\?\"((?:[^\"\\\\]|\\\\[^\"])+)\\\\?\"", RegexOptions.Compiled);
        static readonly Regex RxContent = new Regex(@"Content_([0-9A-Fa-f]{8}-[0-9A-Fa-f-]{27})", RegexOptions.Compiled);
        static readonly Regex RxJob = new Regex(@"(?:CTM job|DTS job|job ID|JobID|job)\s*[:=]?\s*\{?([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\}?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<string> Folders(string computer)
        {
            List<string> list = new List<string>();
            if (string.IsNullOrEmpty(computer) || LogLocations.IsLocal(computer))
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string data = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string ccm = null;
                try
                {
                    using (Microsoft.Win32.RegistryKey b = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                    using (Microsoft.Win32.RegistryKey k = b.OpenSubKey(@"SOFTWARE\Microsoft\CCM\Logging\@Global"))
                        if (k != null) ccm = k.GetValue("LogDirectory") as string;
                }
                catch (Exception) { }
                if (string.IsNullOrEmpty(ccm)) ccm = Path.Combine(win, @"CCM\Logs");
                list.Add(ccm);
                list.Add(Path.Combine(ccm, "SMSTSLog"));
                list.Add(Path.Combine(win, @"ccmsetup\Logs"));
                list.Add(Path.Combine(data, @"Microsoft\IntuneManagementExtension\Logs"));
            }
            else
            {
                string root = @"\\" + RemoteLogin.Host(computer) + @"\";
                list.Add(root + @"admin$\CCM\Logs");
                list.Add(root + @"admin$\CCM\Logs\SMSTSLog");
                list.Add(root + @"admin$\ccmsetup\Logs");
                list.Add(root + @"C$\ProgramData\Microsoft\IntuneManagementExtension\Logs");
            }
            return list;
        }

        public static List<string> LogFiles(IEnumerable<string> folders)
        {
            List<string> files = new List<string>();
            foreach (string d in folders)
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    foreach (string f in Directory.GetFiles(d))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".log" || ext == ".lo_") files.Add(f);
                    }
                }
                catch (Exception) { }
            }
            return files;
        }

        public static List<FollowItem> DiscoverWmi(string computer, out string note)
        {
            List<FollowItem> items = new List<FollowItem>();
            Dictionary<string, FollowItem> byId = new Dictionary<string, FollowItem>(StringComparer.OrdinalIgnoreCase);
            note = null;
            try { FromWmi(computer, items, byId); }
            catch (UnauthorizedAccessException) { note = "The ConfigMgr client's WMI needs administrator rights, so only items found in the logs are listed."; }
            catch (Exception ex) { note = "The ConfigMgr client's WMI could not be read (" + Short(ex) + "), so only items found in the logs are listed."; }
            Sort(items);
            return items;
        }

        public static List<FollowItem> DiscoverLogs(string computer)
        {
            return DiscoverFiles(LogFiles(Folders(computer)));
        }

        public static List<FollowItem> Merge(List<FollowItem> a, List<FollowItem> b)
        {
            List<FollowItem> all = new List<FollowItem>(a);
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (FollowItem it in a)
            {
                seen[it.Id ?? ""] = true;
                foreach (string t in it.Terms) seen[t] = true;
            }
            foreach (FollowItem it in b)
            {
                bool dup = seen.ContainsKey(it.Id ?? "");
                foreach (string t in it.Terms) if (seen.ContainsKey(t)) dup = true;
                if (!dup) all.Add(it);
            }
            Sort(all);
            return all;
        }

        public static List<FollowItem> DiscoverFiles(List<string> files)
        {
            List<FollowItem> items = new List<FollowItem>();
            FromLogs(files, items, new Dictionary<string, FollowItem>(StringComparer.OrdinalIgnoreCase));
            Sort(items);
            return items;
        }

        static void Sort(List<FollowItem> items)
        {
            items.Sort(delegate (FollowItem a, FollowItem b)
            {
                int c = Order(a.Kind).CompareTo(Order(b.Kind));
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        static int Order(string kind)
        {
            switch (kind)
            {
                case Application: return 0;
                case DeploymentType: return 1;
                case IntuneApp: return 2;
                case Update: return 3;
                case Package: return 4;
                default: return 5;
            }
        }

        static string Short(Exception ex)
        {
            string m = (ex.Message ?? ex.GetType().Name).Trim();
            int i = m.IndexOfAny(new char[] { '\r', '\n' });
            if (i > 0) m = m.Substring(0, i);
            return m.TrimEnd('.', ' ');
        }

        static ManagementScope Scope(string computer, string ns)
        {
            ConnectionOptions o = new ConnectionOptions();
            o.Impersonation = ImpersonationLevel.Impersonate;
            o.Authentication = AuthenticationLevel.PacketPrivacy;
            o.Timeout = TimeSpan.FromSeconds(30);
            string path;
            if (string.IsNullOrEmpty(computer) || LogLocations.IsLocal(computer)) path = @"\\.\" + ns;
            else
            {
                string host = RemoteLogin.Host(computer);
                path = @"\\" + host + @"\" + ns;
                string domain, user;
                SecureString pwd;
                if (RemoteLogin.SessionCredential(host, out domain, out user, out pwd))
                {
                    o.Username = domain != null ? domain + "\\" + user : user;
                    o.SecurePassword = pwd;
                }
            }
            ManagementScope s = new ManagementScope(path, o);
            s.Connect();
            return s;
        }

        static ManagementObjectCollection Query(ManagementScope scope, string wql)
        {
            EnumerationOptions e = new EnumerationOptions();
            e.ReturnImmediately = true;
            e.Rewindable = false;
            e.Timeout = TimeSpan.FromSeconds(90);
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery(wql), e))
                return s.Get();
        }

        static string Str(ManagementBaseObject o, string name)
        {
            try
            {
                object v = o[name];
                return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            catch (ManagementException) { return null; }
        }

        static void FromWmi(string computer, List<FollowItem> items, Dictionary<string, FollowItem> byId)
        {
            ManagementScope sdk = Scope(computer, @"root\ccm\ClientSDK");
            try
            {
                foreach (ManagementBaseObject o in Query(sdk, "SELECT Id, Name, Revision, IsMachineTarget, InstallState, ApplicabilityState FROM CCM_Application"))
                    using (o)
                    {
                        string id = Str(o, "Id");
                        if (string.IsNullOrEmpty(id) || byId.ContainsKey(id)) continue;
                        FollowItem it = new FollowItem();
                        it.Kind = Application;
                        it.Name = Str(o, "Name") ?? id;
                        it.Id = id;
                        it.State = AppState(Str(o, "InstallState"), Str(o, "ApplicabilityState"));
                        it.Source = "WMI";
                        it.WmiAppId = id;
                        it.WmiAppRevision = Str(o, "Revision");
                        it.WmiAppMachine = string.Equals(Str(o, "IsMachineTarget"), "True", StringComparison.OrdinalIgnoreCase);
                        AddGuidTerms(it, id);
                        Add(items, byId, it);
                    }
            }
            catch (ManagementException) { }
            try
            {
                foreach (ManagementBaseObject o in Query(sdk, "SELECT ArticleID, UpdateID, Name, EvaluationState FROM CCM_SoftwareUpdate"))
                    using (o)
                    {
                        string uid = Str(o, "UpdateID");
                        if (string.IsNullOrEmpty(uid) || byId.ContainsKey(uid)) continue;
                        FollowItem it = UpdateItem(uid, Str(o, "Name"), Str(o, "ArticleID"));
                        it.State = UpdateState(Str(o, "EvaluationState"));
                        it.Source = "WMI";
                        Add(items, byId, it);
                    }
            }
            catch (ManagementException) { }
            try
            {
                foreach (ManagementBaseObject o in Query(sdk, "SELECT PackageID, ProgramID, Name FROM CCM_Program"))
                    using (o)
                    {
                        string pkg = Str(o, "PackageID");
                        string prg = Str(o, "ProgramID");
                        if (string.IsNullOrEmpty(pkg)) continue;
                        string key = pkg + ":" + prg;
                        if (byId.ContainsKey(key)) continue;
                        FollowItem it = new FollowItem();
                        it.Kind = Package;
                        it.Name = (Str(o, "Name") ?? pkg) + (string.IsNullOrEmpty(prg) ? "" : " (" + prg + ")");
                        it.Id = key;
                        it.Source = "WMI";
                        it.Terms.Add(pkg);
                        byId[key] = it;
                        items.Add(it);
                    }
            }
            catch (ManagementException) { }
            try
            {
                ManagementScope dcm = Scope(computer, @"root\ccm\dcm");
                foreach (ManagementBaseObject o in Query(dcm, "SELECT DisplayName, Name, Version, LastComplianceStatus FROM SMS_DesiredConfiguration"))
                    using (o)
                    {
                        string id = Str(o, "Name");
                        if (string.IsNullOrEmpty(id) || byId.ContainsKey(id)) continue;
                        FollowItem it = new FollowItem();
                        it.Kind = Baseline;
                        it.Name = Str(o, "DisplayName") ?? id;
                        it.Id = id;
                        string c = Str(o, "LastComplianceStatus");
                        it.State = c == "1" ? "Compliant" : c == "0" ? "Non-compliant" : c == "4" ? "Error" : null;
                        it.Source = "WMI";
                        AddGuidTerms(it, id);
                        if (it.Terms.Count == 0) it.Terms.Add(id);
                        Add(items, byId, it);
                    }
            }
            catch (ManagementException) { }
        }

        static string AppState(string install, string applicability)
        {
            if (string.Equals(install, "Installed", StringComparison.OrdinalIgnoreCase)) return "Installed";
            if (string.Equals(applicability, "NotApplicable", StringComparison.OrdinalIgnoreCase)) return "Not applicable";
            if (string.Equals(install, "NotInstalled", StringComparison.OrdinalIgnoreCase)) return "Not installed";
            return install;
        }

        static string UpdateState(string s)
        {
            switch (s)
            {
                case "0": return "Not started";
                case "1": return "Available";
                case "2": return "Submitted";
                case "3": return "Detecting";
                case "5": return "Downloading";
                case "6": return "Waiting to install";
                case "7": return "Installing";
                case "8":
                case "9":
                case "10": return "Restart pending";
                case "11": return "Verifying";
                case "12": return "Installed";
                case "13": return "Failed";
                case "14": return "Waiting for maintenance window";
                default: return null;
            }
        }

        static FollowItem UpdateItem(string updateId, string name, string article)
        {
            FollowItem it = new FollowItem();
            it.Kind = Update;
            it.Name = string.IsNullOrEmpty(name) ? "Update " + updateId : name;
            it.Id = string.IsNullOrEmpty(article) ? updateId : "KB" + article;
            it.Terms.Add(updateId);
            if (!string.IsNullOrEmpty(article)) it.Terms.Add("KB" + article);
            return it;
        }

        static void AddGuidTerms(FollowItem it, string id)
        {
            string g = LastGuid(id);
            if (g != null && !Contains(it.Terms, g)) it.Terms.Add(g);
        }

        static string LastGuid(string id)
        {
            MatchCollection mc = RxGuid.Matches(id ?? "");
            return mc.Count > 0 ? mc[mc.Count - 1].Value : null;
        }

        static bool Contains(List<string> list, string s)
        {
            foreach (string x in list) if (string.Equals(x, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void Add(List<FollowItem> items, Dictionary<string, FollowItem> byId, FollowItem it)
        {
            byId[it.Id] = it;
            foreach (string t in it.Terms) if (!byId.ContainsKey(t)) byId[t] = it;
            items.Add(it);
        }

        static void FromLogs(List<string> files, List<FollowItem> items, Dictionary<string, FollowItem> byId)
        {
            foreach (string f in files)
            {
                string name = Path.GetFileName(f).ToLowerInvariant();
                int kind = name.StartsWith("appenforce") ? 1 : name.StartsWith("updatesdeployment") ? 2 : name.StartsWith("execmgr") ? 3
                    : name.StartsWith("intunemanagementextension") || name.StartsWith("appworkload") ? 4 : 0;
                if (kind == 0) continue;
                string text;
                try { text = ReadText(f); } catch (Exception) { continue; }
                if (kind == 1)
                {
                    foreach (Match m in RxDt.Matches(text))
                    {
                        string id = m.Groups[2].Value;
                        FollowItem known;
                        if (byId.TryGetValue(id, out known)) continue;
                        string g = LastGuid(id);
                        if (g != null && byId.TryGetValue(g, out known)) continue;
                        FollowItem it = new FollowItem();
                        it.Kind = DeploymentType;
                        it.Name = m.Groups[1].Value;
                        it.Id = id;
                        it.State = "Revision " + m.Groups[3].Value;
                        it.Source = "logs";
                        AddGuidTerms(it, id);
                        Add(items, byId, it);
                    }
                }
                else if (kind == 2)
                {
                    foreach (Match m in RxUpdate.Matches(text))
                    {
                        string uid = m.Groups[2].Value;
                        if (byId.ContainsKey(uid)) continue;
                        FollowItem it = UpdateItem(uid, m.Groups[3].Value, m.Groups[4].Value);
                        it.Source = "logs";
                        Add(items, byId, it);
                    }
                }
                else if (kind == 3)
                {
                    foreach (Match m in RxProgram.Matches(text))
                    {
                        string pkg = m.Groups[1].Value.ToUpperInvariant();
                        string prg = m.Groups[2].Value.Trim();
                        string key = pkg + ":" + prg;
                        if (byId.ContainsKey(key)) continue;
                        FollowItem it = new FollowItem();
                        it.Kind = Package;
                        it.Name = pkg + " (" + prg + ")";
                        it.Id = key;
                        it.Source = "logs";
                        it.Terms.Add(pkg);
                        byId[key] = it;
                        items.Add(it);
                    }
                }
                else
                {
                    foreach (Match m in RxIme.Matches(text))
                    {
                        string id = m.Groups[1].Value;
                        if (byId.ContainsKey(id)) continue;
                        string n = m.Groups[2].Value.Replace("\\\\", "\\").Replace("\\/", "/");
                        if (n.Length == 0) continue;
                        FollowItem it = new FollowItem();
                        it.Kind = IntuneApp;
                        it.Name = n;
                        it.Id = id;
                        it.Source = "logs";
                        it.Terms.Add(id);
                        Add(items, byId, it);
                    }
                }
            }
        }

        public static string ReadText(string path)
        {
            byte[] b;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length;
                if (len > int.MaxValue / 2) len = int.MaxValue / 2;
                b = new byte[len];
                int n = 0;
                while (n < b.Length)
                {
                    int r = fs.Read(b, n, b.Length - n);
                    if (r <= 0) break;
                    n += r;
                }
                if (n < b.Length) Array.Resize(ref b, n);
            }
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            int zeros = 0, probe = Math.Min(b.Length, 4096);
            for (int i = 1; i < probe; i += 2) if (b[i] == 0) zeros++;
            if (probe > 16 && zeros > probe / 4) return Encoding.Unicode.GetString(b);
            return Encoding.UTF8.GetString(b);
        }

        public static void Expand(string computer, FollowItem item)
        {
            if (item.WmiAppId == null) return;
            ManagementScope sdk = Scope(computer, @"root\ccm\ClientSDK");
            string path = "CCM_Application.Id=\"" + item.WmiAppId.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",IsMachineTarget=" + (item.WmiAppMachine ? "TRUE" : "FALSE") + ",Revision=\"" + (item.WmiAppRevision ?? "") + "\"";
            using (ManagementObject app = new ManagementObject(sdk, new ManagementPath(path), null))
            {
                app.Get();
                ManagementBaseObject[] dts = app["AppDTs"] as ManagementBaseObject[];
                if (dts == null) return;
                foreach (ManagementBaseObject dt in dts)
                {
                    AddGuidTerms(item, Str(dt, "Id"));
                    dt.Dispose();
                }
            }
        }

        public static FollowResult Scan(string computer, FollowItem item, bool names, Func<bool> cancelled, Action<string, int, int> progress)
        {
            try { Expand(computer, item); } catch (Exception) { }
            FollowResult r = Start(item, names);
            ScanFiles(LogFiles(Folders(computer)), r, cancelled, progress);
            return r;
        }

        public static void ScanFiles(List<string> files, FollowResult r, Func<bool> cancelled, Action<string, int, int> progress)
        {
            HashSet<int>[] seen = new HashSet<int>[files.Count];
            for (int k = 0; k < seen.Length; k++) seen[k] = new HashSet<int>();
            List<string> derived = new List<string>();
            const int Levels = 3;
            int total = files.Count * (Levels + 1);
            Pass(files, r.Terms, seen, derived, r.Terms, cancelled, progress, 0, total);
            for (int level = 1; level <= Levels && derived.Count > 0; level++)
            {
                if (cancelled != null && cancelled()) break;
                List<string> next = new List<string>();
                List<string> known = new List<string>(r.Terms);
                known.AddRange(derived);
                Pass(files, derived, seen, level < Levels ? next : null, known, cancelled, progress, files.Count * level, total);
                r.Terms.AddRange(derived);
                derived = next;
            }
            for (int k = 0; k < files.Count; k++)
            {
                int n = seen[k].Count;
                if (n == 0) continue;
                r.Files.Add(files[k]);
                r.Hits[files[k]] = n;
                r.Total += n;
            }
            StringBuilder sb = new StringBuilder();
            foreach (string t in r.Terms)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(Regex.Escape(t));
            }
            r.Pattern = sb.ToString();
        }

        public static FollowResult Start(FollowItem item, bool names)
        {
            FollowResult r = new FollowResult();
            foreach (string t in item.Terms) if (t.Length >= 4 && !Contains(r.Terms, t)) r.Terms.Add(t);
            if (names && !string.IsNullOrEmpty(item.Name) && item.Name.Trim().Length >= 5 && !Contains(r.Terms, item.Name.Trim())) r.Terms.Add(item.Name.Trim());
            return r;
        }

        static void Pass(List<string> files, List<string> terms, HashSet<int>[] seen, List<string> derived, List<string> known,
            Func<bool> cancelled, Action<string, int, int> progress, int offset, int total)
        {
            string[] low = new string[terms.Count];
            for (int t = 0; t < low.Length; t++) low[t] = terms[t].ToLowerInvariant();
            object gate = new object();
            int done = 0;
            System.Threading.Tasks.ParallelOptions po = new System.Threading.Tasks.ParallelOptions();
            po.MaxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
            System.Threading.Tasks.Parallel.For(0, files.Count, po, delegate (int i, System.Threading.Tasks.ParallelLoopState state)
            {
                if (cancelled != null && cancelled()) { state.Stop(); return; }
                string f = files[i];
                string text;
                try { text = ReadText(f); } catch (Exception) { text = null; }
                List<int> starts = new List<int>();
                List<string> found = derived != null ? new List<string>() : null;
                if (text != null)
                {
                    HashSet<int> mine = seen[i];
                    HashSet<int> local = new HashSet<int>();
                    foreach (string t in low)
                    {
                        if (t.Length == 0) continue;
                        int p = 0;
                        while (p < text.Length)
                        {
                            int h = Find(text, t, p);
                            if (h < 0) break;
                            int ls = h == 0 ? 0 : text.LastIndexOf('\n', h - 1) + 1;
                            int le = text.IndexOf('\n', h);
                            if (le < 0) le = text.Length;
                            bool fresh;
                            lock (mine) fresh = !mine.Contains(ls);
                            if (fresh && local.Add(ls))
                            {
                                starts.Add(ls);
                                if (found != null && found.Count < 40) Derive(text.Substring(ls, le - ls), found, known);
                            }
                            p = le + 1;
                        }
                    }
                    lock (mine) foreach (int ls in starts) mine.Add(ls);
                }
                lock (gate)
                {
                    if (found != null)
                        foreach (string g in found)
                            if (derived.Count < 40 && !Contains(derived, g) && !Contains(known, g)) derived.Add(g);
                    done++;
                    if (progress != null) progress(Path.GetFileName(f), offset + done, total);
                }
            });
        }

        static int Find(string text, string lower, int from)
        {
            char a = lower[0], b = char.ToUpperInvariant(a);
            int n = lower.Length, last = text.Length - n;
            char[] any = a == b ? null : new char[] { a, b };
            int p = from;
            while (p <= last)
            {
                int h = any == null ? text.IndexOf(a, p, last - p + 1) : text.IndexOfAny(any, p, last - p + 1);
                if (h < 0) return -1;
                if (n == 1 || string.Compare(text, h + 1, lower, 1, n - 1, StringComparison.OrdinalIgnoreCase) == 0) return h;
                p = h + 1;
            }
            return -1;
        }

        static void Derive(string line, List<string> derived, List<string> known)
        {
            foreach (Match m in RxContent.Matches(line))
            {
                string g = m.Groups[1].Value;
                if (!Contains(known, g) && !Contains(derived, g)) derived.Add(g);
            }
            if (line.IndexOf("job", StringComparison.OrdinalIgnoreCase) < 0) return;
            foreach (Match m in RxJob.Matches(line))
            {
                string g = m.Groups[1].Value;
                if (!Contains(known, g) && !Contains(derived, g)) derived.Add(g);
            }
        }
    }
}
