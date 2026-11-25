namespace LogVice
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Microsoft.Win32;

    public sealed class UpdateInfo
    {
        public string Latest;
        public string Url;
        public bool Newer;
        public string Error;
        public string MsiUrl;
        public string ExeUrl;
        public string SumsUrl;
        public string Notes;
        public DateTime CheckedUtc;

        public bool CanInstall
        {
            get { return Newer && SumsUrl != null && (Updates.IsInstalled ? MsiUrl != null : ExeUrl != null); }
        }
    }

    // Where the gate reads the device from: the registry in the app, a fake in the tests. A path starts with HKLM or HKCU.
    public interface IRegSource
    {
        string[] SubKeys(string path);
        // The value as text, or null when the key or value is missing.
        string Value(string path, string name);
    }

    public sealed class LiveRegistry : IRegSource
    {
        static RegistryKey Open(string path)
        {
            int i = path.IndexOf('\\');
            string hive = i < 0 ? path : path.Substring(0, i);
            string sub = i < 0 ? "" : path.Substring(i + 1);
            RegistryKey root = hive.ToUpperInvariant() == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
            if (sub.Length == 0) return root;
            return root.OpenSubKey(sub, false);
        }

        public string[] SubKeys(string path)
        {
            try
            {
                using (RegistryKey k = Open(path)) return k == null ? new string[0] : k.GetSubKeyNames();
            }
            catch (Exception) { return new string[0]; }
        }

        public string Value(string path, string name)
        {
            try
            {
                using (RegistryKey k = Open(path))
                {
                    object v = k == null ? null : k.GetValue(name);
                    return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception) { return null; }
        }
    }

    // What decides whether LogVice may ask GitHub for updates or install one. Each one, when true, stops both.
    public sealed class UpdateContext
    {
        // False until the window sets it: a check needs the LogVice window.
        public bool Window;
        // A screenshot run (--shot).
        public bool Screenshot;
        public bool WinPE;
        public bool System;
        // Windows setup or the Enrollment Status Page runs on this device, or the user is defaultuser0.
        public bool Oobe;
        public bool TaskSequence;
        public bool Policy;
    }

    public static class UpdateGate
    {
        public const string PolicyKey = @"HKLM\SOFTWARE\Policies\LogVice";
        public const string PolicyValue = "DisableUpdateCheck";
        public const string NoWindow = "LogVice checks for updates only when it runs with its window.";

        // Null when LogVice may check, otherwise why not.
        public static string Reason(UpdateContext c)
        {
            if (c == null || !c.Window) return NoWindow;
            if (c.Screenshot) return "No update check while LogVice takes a screenshot.";
            if (c.WinPE) return "No update check in Windows PE.";
            if (c.System) return "No update check while LogVice runs as SYSTEM.";
            if (c.TaskSequence) return "No update check while a task sequence runs on this device.";
            if (c.Oobe) return "No update check while Windows setup or the Enrollment Status Page runs on this device.";
            if (c.Policy) return "Your organization has turned this off with the policy value " + PolicyValue + ".";
            return null;
        }

        // Reads the parts of the context that come from the device itself.
        public static void ReadDevice(UpdateContext c, IRegSource reg, string user, bool system, bool tsRunning)
        {
            c.System = system;
            c.TaskSequence = tsRunning;
            c.WinPE = Has(reg.SubKeys(@"HKLM\SYSTEM\CurrentControlSet\Control"), "MiniNT");
            c.Oobe = On(reg, @"HKLM\SYSTEM\Setup", "OOBEInProgress") || On(reg, @"HKLM\SYSTEM\Setup", "SystemSetupInProgress")
                || (user ?? "").StartsWith("defaultuser", StringComparison.OrdinalIgnoreCase);
            c.Policy = On(reg, PolicyKey, PolicyValue);
        }

        public static bool BlockedByPolicy(IRegSource reg)
        {
            return On(reg, PolicyKey, PolicyValue);
        }

        static bool On(IRegSource reg, string path, string name)
        {
            long n;
            string v = reg.Value(path, name);
            return v != null && long.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n != 0;
        }

        static bool Has(string[] names, string name)
        {
            foreach (string n in names) if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public static class Updates
    {
        public const string Repo = "Baldissera88/LogVice";
        public const string ReleasesUrl = "https://github.com/" + Repo + "/releases";
        const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

        // Says why no check or download may happen now, or null. Unless the window sets it, nothing may happen.
        public static Func<string> Gate = delegate { return UpdateGate.NoWindow; };

        public static string Blocked
        {
            get
            {
                try
                {
                    Func<string> g = Gate;
                    return g == null ? UpdateGate.NoWindow : g();
                }
                catch (Exception) { return UpdateGate.NoWindow; }
            }
        }

        public static bool BlockedByPolicy
        {
            get { return UpdateGate.BlockedByPolicy(new LiveRegistry()); }
        }

        public static bool Enabled
        {
            get { return !BlockedByPolicy && Settings.GetBool("UpdateCheck", true); }
        }

        public static bool DueToday
        {
            get
            {
                long ticks;
                string s = Settings.Get("UpdateChecked");
                if (string.IsNullOrEmpty(s) || !long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return true;
                return DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) > TimeSpan.FromHours(20);
            }
        }

        public static DateTime? LastChecked
        {
            get
            {
                long ticks;
                string s = Settings.Get("UpdateChecked");
                if (string.IsNullOrEmpty(s) || !long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return null;
                try { return new DateTime(ticks, DateTimeKind.Utc); }
                catch (ArgumentException) { return null; }
            }
        }

        public static void CheckAsync(Action<UpdateInfo> done)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateInfo r = Check();
                if (done != null) done(r);
            });
        }

        public static UpdateInfo Check()
        {
            UpdateInfo r = new UpdateInfo();
            string blocked = Blocked;
            if (blocked != null) { r.Error = blocked; return r; }
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                req.UserAgent = "LogVice/" + Program.Version;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;
                IWebProxy proxy = WebRequest.GetSystemWebProxy();
                if (proxy != null) proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                req.Proxy = proxy;
                string json;
                using (WebResponse resp = req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    json = sr.ReadToEnd();
                Parse(json, r);
                r.CheckedUtc = DateTime.UtcNow;
                if (StillPublishing(r)) return r;
                Settings.Set("UpdateChecked", r.CheckedUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch (WebException ex)
            {
                HttpWebResponse hr = ex.Response as HttpWebResponse;
                r.Error = hr != null && hr.StatusCode == HttpStatusCode.NotFound ? "No release has been published yet." : "GitHub could not be reached.";
            }
            catch (Exception) { r.Error = "GitHub could not be reached."; }
            return r;
        }

        // A new release whose files are not attached yet: GitHub Actions adds them a few minutes after it is published.
        // It is not offered as a download then, and the check is not counted, so the next start asks again.
        public static bool StillPublishing(UpdateInfo r)
        {
            if (r == null || !r.Newer || r.SumsUrl != null) return false;
            r.Newer = false;
            r.Error = "LogVice " + r.Latest + " is being published. Its files are added a few minutes after the release; check again then.";
            return true;
        }

        public static void Parse(string json, UpdateInfo r)
        {
            Match tag = Regex.Match(json ?? "", "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            Match url = Regex.Match(json ?? "", "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");
            if (!tag.Success) { r.Error = "GitHub returned no version."; return; }
            r.Latest = tag.Groups[1].Value.TrimStart('v', 'V');
            r.Url = url.Success ? url.Groups[1].Value : ReleasesUrl;
            r.Newer = IsNewer(r.Latest, Program.Version);
            foreach (Match m in RxAsset.Matches(json ?? ""))
            {
                string name = m.Groups[2].Value;
                if (string.Equals(name, "LogVice.msi", StringComparison.OrdinalIgnoreCase)) r.MsiUrl = m.Groups[1].Value;
                else if (string.Equals(name, "LogVice.exe", StringComparison.OrdinalIgnoreCase)) r.ExeUrl = m.Groups[1].Value;
                else if (string.Equals(name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) r.SumsUrl = m.Groups[1].Value;
            }
            Match body = RxBody.Match(json ?? "");
            if (body.Success) r.Notes = ReleaseNotes.FromRelease(JsonString(body.Groups[1].Value));
        }

        static readonly Regex RxBody = new Regex("\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

        public static string JsonString(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                int code;
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 < s.Length && int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        else sb.Append(n);
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        static readonly Regex RxAsset = new Regex("\"browser_download_url\"\\s*:\\s*\"(https://github\\.com/" + Regex.Escape(Repo) + "/releases/download/[^\"/]+/(LogVice\\.msi|LogVice\\.exe|SHA256SUMS\\.txt))\"", RegexOptions.IgnoreCase);

        public static bool IsInstalled
        {
            get
            {
                try
                {
                    string dir;
                    using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\LogVice"))
                        dir = k != null ? k.GetValue("InstallDir") as string : null;
                    if (string.IsNullOrEmpty(dir)) return false;
                    string here = Path.GetDirectoryName(Program.Location) ?? "";
                    return string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), Path.GetFullPath(here).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception) { return false; }
            }
        }

        static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "LogVice/" + Program.Version;
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            req.AllowAutoRedirect = true;
            IWebProxy proxy = WebRequest.GetSystemWebProxy();
            if (proxy != null) proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
            req.Proxy = proxy;
            return req;
        }

        static void Download(string url, string path, Action<int> progress)
        {
            HttpWebRequest req = Request(url);
            using (WebResponse resp = req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long length = resp.ContentLength;
                byte[] buf = new byte[81920];
                int n, last = -1;
                long total = 0;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    total += n;
                    if (total > 256L * 1024 * 1024) throw new IOException("The download is larger than expected.");
                    if (progress != null && length > 0)
                    {
                        int pct = (int)Math.Min(100, total * 100 / length);
                        if (pct != last) { last = pct; progress(pct); }
                    }
                }
            }
        }

        public static string ExpectedHash(string sums, string file)
        {
            foreach (string raw in (sums ?? "").Split('\n'))
            {
                string line = raw.Trim();
                int sp = line.IndexOf(' ');
                if (sp != 64) continue;
                string name = line.Substring(sp).Trim().TrimStart('*');
                if (string.Equals(name, file, StringComparison.OrdinalIgnoreCase)) return line.Substring(0, 64).ToLowerInvariant();
            }
            return null;
        }

        public static string FileHash(string path)
        {
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(64);
                foreach (byte x in h) sb.Append(x.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static string WorkFolder(string version)
        {
            return Path.Combine(Path.GetTempPath(), "LogVice-update-" + version);
        }

        public static bool HandedOff;

        public static string Prepare(UpdateInfo r)
        {
            return Prepare(r, null);
        }

        public static string Prepare(UpdateInfo r, Action<int> progress)
        {
            bool msi = IsInstalled;
            string url = msi ? r.MsiUrl : r.ExeUrl;
            string name = msi ? "LogVice.msi" : "LogVice.exe";
            if (url == null || r.SumsUrl == null) throw new InvalidOperationException("This release has no " + (url == null ? name : "SHA256SUMS.txt") + " to install automatically. Download it from the release page.");
            string dir = WorkFolder(r.Latest);
            Directory.CreateDirectory(dir);
            string sumsPath = Path.Combine(dir, "SHA256SUMS.txt");
            string file = Path.Combine(dir, name);
            Download(r.SumsUrl, sumsPath, null);
            string want = ExpectedHash(File.ReadAllText(sumsPath), name);
            if (want == null) throw new InvalidOperationException("SHA256SUMS.txt in the release does not list " + name + ".");
            Download(url, file, progress);
            string got = FileHash(file);
            if (got != want)
            {
                try { File.Delete(file); } catch (Exception) { }
                throw new InvalidOperationException("The downloaded " + name + " does not match its checksum, so it was not installed. Try again later, or download it from the release page.");
            }
            return file;
        }

        public static void Launch(string file, string[] reopen)
        {
            bool msi = file.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
            string dir = Path.GetDirectoryName(file);
            string helper = Path.Combine(dir, "LogVice-updater.exe");
            File.Copy(Program.Location, helper, true);
            StringBuilder a = new StringBuilder();
            a.Append("--apply-update ").Append(msi ? "msi" : "exe");
            a.Append(" \"").Append(file).Append('"');
            a.Append(" \"").Append(Program.Location).Append('"');
            a.Append(' ').Append(System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
            if (reopen != null)
                foreach (string p in reopen)
                    if (!string.IsNullOrEmpty(p)) a.Append(" \"").Append(p.TrimEnd('\\')).Append('"');
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(helper, a.ToString());
            psi.UseShellExecute = false;
            psi.WorkingDirectory = dir;
            try
            {
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception)
            {
                if (!msi) throw;
                System.Diagnostics.ProcessStartInfo inst = new System.Diagnostics.ProcessStartInfo(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"),
                    "/i \"" + file + "\" /passive /norestart");
                inst.UseShellExecute = true;
                inst.Verb = "runas";
                System.Diagnostics.Process.Start(inst);
            }
        }

        public static int Apply(string[] args)
        {
            if (args.Length < 5) return 2;
            UpdateJob job = new UpdateJob(args);
            try { NativeMethods.SetProcessDPIAware(); } catch (Exception) { }
            try
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) Dpi.Scale = Math.Max(1f, g.DpiX / 96f);
            }
            catch (Exception) { }
            Fonts.Init();
            using (UpdaterForm f = new UpdaterForm(job.Version))
            {
                f.Shown += delegate { ThreadPool.QueueUserWorkItem(delegate { job.Run(f); }); };
                System.Windows.Forms.Application.Run(f);
            }
            return job.Result;
        }

        sealed class UpdateJob
        {
            readonly bool msi;
            readonly string file, target;
            readonly int pid;
            readonly string[] reopen;
            public readonly string Version;
            public int Result = 1;

            public UpdateJob(string[] args)
            {
                msi = args[1] == "msi";
                file = args[2];
                target = args[3];
                int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
                reopen = new string[Math.Max(0, args.Length - 5)];
                Array.Copy(args, 5, reopen, 0, reopen.Length);
                string folder = Path.GetFileName(Path.GetDirectoryName(file) ?? "") ?? "";
                Version = folder.StartsWith("LogVice-update-", StringComparison.OrdinalIgnoreCase) ? folder.Substring("LogVice-update-".Length) : null;
            }

            public void Run(UpdaterForm ui)
            {
                string problem = null;
                try
                {
                    ui.Status("Waiting for LogVice to close\u2026");
                    System.Diagnostics.Process parent = null;
                    try { parent = System.Diagnostics.Process.GetProcessById(pid); } catch (Exception) { }
                    if (parent != null)
                        using (parent)
                        {
                            bool gone = false;
                            try { gone = parent.WaitForExit(15000); } catch (Exception) { gone = true; }
                            if (!gone)
                            {
                                ui.Status("LogVice is still open. Closing it\u2026");
                                CloseOrKill(parent);
                            }
                        }
                    CloseOthers();
                    if (msi)
                    {
                        ui.Status("Installing LogVice" + (Version != null ? " " + Version : "") + ". Windows asks for administrator rights.");
                        problem = InstallMsi();
                    }
                    else
                    {
                        ui.Status("Replacing LogVice.exe\u2026");
                        problem = ReplaceExe();
                    }
                }
                catch (Exception ex) { problem = "The update stopped: " + ex.Message; }
                if (problem != null) ui.Problem(problem);
                ui.Status("Starting LogVice\u2026");
                try
                {
                    StringBuilder a = new StringBuilder();
                    foreach (string p in reopen) a.Append(" \"").Append(p).Append('"');
                    System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo(target, a.ToString().Trim());
                    start.UseShellExecute = false;
                    start.WorkingDirectory = Path.GetDirectoryName(target);
                    System.Diagnostics.Process.Start(start);
                }
                catch (Exception ex) { ui.Problem("LogVice could not start again: " + ex.Message); }
                Result = problem == null ? 0 : 1;
                ui.Finish();
            }

            void CloseOthers()
            {
                string name = Path.GetFileNameWithoutExtension(target);
                string full = Path.GetFullPath(target);
                System.Diagnostics.Process[] all;
                try { all = System.Diagnostics.Process.GetProcessesByName(name); }
                catch (Exception) { return; }
                foreach (System.Diagnostics.Process p in all)
                    using (p)
                    {
                        string path = null;
                        try { path = p.MainModule.FileName; } catch (Exception) { }
                        if (path == null || !string.Equals(Path.GetFullPath(path), full, StringComparison.OrdinalIgnoreCase)) continue;
                        CloseOrKill(p);
                    }
            }

            static void CloseOrKill(System.Diagnostics.Process p)
            {
                try { if (p.HasExited) return; } catch (Exception) { return; }
                try { p.CloseMainWindow(); } catch (Exception) { }
                try { if (p.WaitForExit(8000)) return; } catch (Exception) { return; }
                try
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }
                catch (Exception) { }
            }

            string InstallMsi()
            {
                string log = Path.Combine(Path.GetDirectoryName(file), "install.log");
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"),
                        "/i \"" + file + "\" /passive /norestart /l*v \"" + log + "\"");
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                    {
                        p.WaitForExit();
                        int code = p.ExitCode;
                        if (code == 1618) return "Another installation is running. Try again when it has finished.";
                        if (code != 0 && code != 3010 && code != 1641)
                            return "The installation ended with code " + code + ". The installer log is " + log + ".";
                    }
                    return null;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    return ex.NativeErrorCode == 1223 ? "The update was not installed, because administrator rights were not given." : "The installer could not start: " + ex.Message;
                }
            }

            string ReplaceExe()
            {
                Exception last = null;
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        File.Copy(file, target, true);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Thread.Sleep(500);
                    }
                }
                return "LogVice could not replace " + target + ": " + last.Message + "\r\n\r\nThe new version is in " + file + ". Copy it there yourself.";
            }
        }

        public static void CleanUp()
        {
            try
            {
                string mine = Path.GetDirectoryName(Program.Location) ?? "";
                foreach (string d in Directory.GetDirectories(Path.GetTempPath(), "LogVice-update-*"))
                {
                    if (mine.StartsWith(d, StringComparison.OrdinalIgnoreCase)) continue;
                    if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(d) < TimeSpan.FromMinutes(10)) continue;
                    try { Directory.Delete(d, true); } catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        public static bool IsNewer(string latest, string current)
        {
            Version a, b;
            if (!TryVersion(latest, out a) || !TryVersion(current, out b)) return false;
            return a > b;
        }

        static bool TryVersion(string s, out Version v)
        {
            v = null;
            if (string.IsNullOrEmpty(s)) return false;
            Match m = Regex.Match(s, @"^\s*v?(\d+)(\.\d+){1,3}");
            return m.Success && Version.TryParse(m.Value.Trim().TrimStart('v', 'V'), out v);
        }
    }
}
