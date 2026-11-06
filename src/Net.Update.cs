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

        public bool CanInstall
        {
            get { return Newer && SumsUrl != null && (Updates.IsInstalled ? MsiUrl != null : ExeUrl != null); }
        }
    }

    public static class Updates
    {
        public const string Repo = "Baldissera88/LogVice";
        public const string ReleasesUrl = "https://github.com/" + Repo + "/releases";
        const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";
        const string PolicyKey = @"SOFTWARE\Policies\LogVice";

        public static bool BlockedByPolicy
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(PolicyKey))
                    {
                        if (k == null) return false;
                        object v = k.GetValue("DisableUpdateCheck");
                        return v is int && (int)v != 0;
                    }
                }
                catch (Exception) { return false; }
            }
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
                Settings.Set("UpdateChecked", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch (WebException ex)
            {
                HttpWebResponse hr = ex.Response as HttpWebResponse;
                r.Error = hr != null && hr.StatusCode == HttpStatusCode.NotFound ? "No release has been published yet." : "GitHub could not be reached.";
            }
            catch (Exception) { r.Error = "GitHub could not be reached."; }
            return r;
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

        static void Download(string url, string path)
        {
            HttpWebRequest req = Request(url);
            using (WebResponse resp = req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buf = new byte[81920];
                int n;
                long total = 0;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    total += n;
                    if (total > 256L * 1024 * 1024) throw new IOException("The download is larger than expected.");
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

        public static string Prepare(UpdateInfo r)
        {
            bool msi = IsInstalled;
            string url = msi ? r.MsiUrl : r.ExeUrl;
            string name = msi ? "LogVice.msi" : "LogVice.exe";
            if (url == null || r.SumsUrl == null) throw new InvalidOperationException("This release has no " + (url == null ? name : "SHA256SUMS.txt") + " to install automatically. Download it from the release page.");
            string dir = WorkFolder(r.Latest);
            Directory.CreateDirectory(dir);
            string sumsPath = Path.Combine(dir, "SHA256SUMS.txt");
            string file = Path.Combine(dir, name);
            Download(r.SumsUrl, sumsPath);
            string want = ExpectedHash(File.ReadAllText(sumsPath), name);
            if (want == null) throw new InvalidOperationException("SHA256SUMS.txt in the release does not list " + name + ".");
            Download(url, file);
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
            bool msi = args[1] == "msi";
            string file = args[2], target = args[3];
            int pid;
            int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
            string[] reopen = new string[Math.Max(0, args.Length - 5)];
            Array.Copy(args, 5, reopen, 0, reopen.Length);
            try
            {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(pid)) p.WaitForExit(60000);
            }
            catch (Exception) { }
            string problem = null;
            if (msi)
            {
                try
                {
                    string log = Path.Combine(Path.GetDirectoryName(file), "install.log");
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"),
                        "/i \"" + file + "\" /passive /norestart /l*v \"" + log + "\"");
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                    {
                        p.WaitForExit();
                        int code = p.ExitCode;
                        if (code != 0 && code != 3010 && code != 1641)
                            problem = "The installation ended with code " + code + ". The installer log is " + log + ".";
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    problem = ex.NativeErrorCode == 1223 ? "The update was not installed, because administrator rights were not given." : "The installer could not start: " + ex.Message;
                }
                catch (Exception ex) { problem = "The installer could not start: " + ex.Message; }
            }
            else
            {
                Exception last = null;
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        File.Copy(file, target, true);
                        last = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Thread.Sleep(500);
                    }
                }
                if (last != null) problem = "LogVice could not replace " + target + ": " + last.Message + "\r\n\r\nThe new version is in " + file + ". Copy it there yourself.";
            }
            if (problem != null)
                System.Windows.Forms.MessageBox.Show(problem, "LogVice update", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            try
            {
                StringBuilder a = new StringBuilder();
                foreach (string p in reopen) a.Append(" \"").Append(p).Append('"');
                System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo(target, a.ToString().Trim());
                start.UseShellExecute = false;
                start.WorkingDirectory = Path.GetDirectoryName(target);
                System.Diagnostics.Process.Start(start);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("LogVice could not start again: " + ex.Message, "LogVice update", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            return problem == null ? 0 : 1;
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
