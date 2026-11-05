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
