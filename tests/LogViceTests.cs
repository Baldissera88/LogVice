namespace LogVice.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    static class Program
    {
        static int failed;
        static string data;

        static int Main(string[] args)
        {
            data = args.Length > 0 ? args[0] : Path.Combine("tests", "data");
            bool update = args.Length > 1 && args[1] == "--update";
            string expectedPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(data)), "expected.txt");

            Dictionary<string, string> expected = new Dictionary<string, string>();
            if (File.Exists(expectedPath))
                foreach (string line in File.ReadAllLines(expectedPath))
                {
                    int sp = line.IndexOf(' ');
                    if (sp > 0) expected[line.Substring(0, sp)] = line.Substring(sp + 1);
                }

            List<string> files = new List<string>();
            foreach (string f in Directory.GetFiles(data, "*", SearchOption.AllDirectories)) files.Add(f);
            files.Sort(StringComparer.Ordinal);
            StringBuilder outLines = new StringBuilder();
            foreach (string f in files)
            {
                string rel = f.Substring(data.Length).TrimStart('/', '\\').Replace('\\', '/');
                string sig = Signature(f);
                outLines.Append(rel).Append(' ').Append(sig).Append('\n');
                string want;
                if (update) continue;
                if (!expected.TryGetValue(rel, out want)) Fail(rel + ": no expected signature (run with --update)");
                else if (want != sig) Fail(rel + ": parsed output changed\n    expected " + want + "\n    actual   " + sig);
                else Pass(rel);
            }
            if (update)
            {
                File.WriteAllText(expectedPath, outLines.ToString());
                Console.WriteLine("Wrote " + expectedPath);
                return 0;
            }

            LineNumbers();
            Severity();
            MergeOrder();
            ParallelFilter();
            AbsorbMatchesSequential();
            FollowChain();
            FollowIntune();
            UpdateRelease();

            Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : failed + " TEST(S) FAILED");
            return failed == 0 ? 0 : 1;
        }

        static void Pass(string name) { Console.WriteLine("  ok   " + name); }

        static void Fail(string msg)
        {
            failed++;
            Console.WriteLine("  FAIL " + msg);
        }

        static void Check(bool ok, string name)
        {
            if (ok) Pass(name);
            else Fail(name);
        }

        static Store Load(params string[] paths)
        {
            Store st = new Store();
            foreach (string p in paths)
            {
                LogSource s = new LogSource(p, st.Sources.Count);
                st.Sources.Add(s);
                Loader.ReadAll(st, s, new LoadToken(), delegate (long pos) { });
                s.Dispose();
            }
            st.BuildOrder();
            return st;
        }

        static string Signature(string path)
        {
            Store st = Load(path);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < st.Count; i++)
            {
                sb.Append(st.Msg[i]).Append('\u0001').Append(st.Time[i]).Append('\u0001').Append(st.Bias[i]).Append('\u0001')
                  .Append(st.Sev[i]).Append('\u0001').Append(st.Comp[i] >= 0 ? st.CompNames[st.Comp[i]] : "-").Append('\u0001')
                  .Append(st.Thread[i]).Append('\u0001').Append(st.CFile[i] >= 0 ? st.CFNames[st.CFile[i]] : "-").Append('\u0001')
                  .Append(st.Line[i]).Append('\n');
            }
            using (SHA256 h = SHA256.Create())
            {
                byte[] hash = h.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return st.Count + " " + st.Sources[0].Format + " " + BitConverter.ToString(hash, 0, 8).Replace("-", "");
            }
        }

        static void LineNumbers()
        {
            Store st = Load(Path.Combine(data, "plain.txt"));
            Check(st.Count == 4 && st.Line[0] == 1 && st.Line[1] == 3 && st.Line[2] == 4 && st.Line[3] == 7, "plain text keeps file line numbers (1, 3, 4, 7)");
            Store cm = Load(Path.Combine(data, "cm-edge.log"));
            int multi = -1;
            for (int i = 0; i < cm.Count; i++) if (cm.Msg[i].StartsWith("multi", StringComparison.Ordinal)) multi = i;
            Check(multi >= 0 && cm.Msg[multi].Contains("\n") && cm.Line[multi] == 7, "multi-line CMTrace entry starts on line 7");
        }

        static void Severity()
        {
            Store cbs = Load(Path.Combine(data, "cbs.log"));
            Check(cbs.Count == 2 && cbs.Sev[0] == 1 && cbs.Sev[1] == 3, "CBS Info and Error levels");
            Store sms = Load(Path.Combine(data, "sms.log"));
            Check(sms.Sources[0].Format == "sms" && sms.Count == 5 && sms.Sev[1] == 3 && sms.Sev[2] == 2 && sms.Thread[1] == 5678, "SMS trace severity, thread and format");
        }

        static void MergeOrder()
        {
            string d = Path.Combine(data, "spans");
            Store st = Load(Path.Combine(d, "AppEnforce-20261104-080000.log"), Path.Combine(d, "AppEnforce.lo_"), Path.Combine(d, "AppEnforce.log"));
            bool sorted = true;
            for (int o = 1; o < st.Count; o++) if (st.Key[st.Order[o]] < st.Key[st.Order[o - 1]]) sorted = false;
            Check(st.Count > 0 && sorted, "merged view is sorted by time across " + st.Sources.Count + " parts");
        }

        static void ParallelFilter()
        {
            Store st = Load(Path.Combine(data, "follow", "AppEnforce.log"), Path.Combine(data, "follow", "CAS.log"));
            string[] msg = st.Msg;
            int[] order = st.Order;
            Regex rx = new Regex("content", RegexOptions.IgnoreCase);
            int serial = 0;
            for (int o = 0; o < st.Count; o++) if (rx.IsMatch(msg[order[o]])) serial++;
            int count;
            int[] v = ViewFilter.Run(st.Count, delegate (int a, int b, int[] outp)
            {
                int m = 0;
                for (int k = a; k < b; k++) if (rx.IsMatch(msg[order[k]])) outp[m++] = order[k];
                return m;
            }, null, out count);
            Check(v != null && count == serial && serial > 0, "parallel filter returns the same rows as a serial scan (" + serial + ")");
        }

        static void AbsorbMatchesSequential()
        {
            string d = Path.Combine(data, "follow");
            string[] paths = Directory.GetFiles(d, "*.log");
            Array.Sort(paths, StringComparer.Ordinal);
            Store seq = Load(paths);
            Store merged = new Store();
            LogSource[] srcs = new LogSource[paths.Length];
            for (int k = 0; k < paths.Length; k++) { srcs[k] = new LogSource(paths[k], k); merged.Sources.Add(srcs[k]); }
            for (int k = paths.Length - 1; k >= 0; k--)
            {
                Store part = new Store();
                Loader.ReadAll(part, srcs[k], new LoadToken(), delegate (long p) { });
                srcs[k].Dispose();
                merged.Absorb(part);
            }
            merged.BuildOrder();
            bool same = seq.Count == merged.Count;
            for (int o = 0; same && o < seq.Count; o++)
            {
                int i = seq.Order[o], j = merged.Order[o];
                if (seq.Msg[i] != merged.Msg[j] || seq.Src[i] != merged.Src[j] || seq.Line[i] != merged.Line[j] || seq.Sev[i] != merged.Sev[j]
                    || (seq.Comp[i] >= 0 ? seq.CompNames[seq.Comp[i]] : null) != (merged.Comp[j] >= 0 ? merged.CompNames[merged.Comp[j]] : null)) same = false;
            }
            for (int s = 0; same && s < 4; s++) if (seq.SevCount[s] != merged.SevCount[s]) same = false;
            Check(same, "parallel load (absorbed out of order) gives the same merged view as a sequential load");
        }

        static void FollowChain()
        {
            List<string> files = Follow.LogFiles(new string[] { Path.Combine(data, "follow") });
            List<FollowItem> items = Follow.DiscoverFiles(files);
            FollowItem dt = items.Find(delegate (FollowItem x) { return x.Kind == Follow.DeploymentType && x.Name.StartsWith("7-Zip", StringComparison.Ordinal); });
            Check(dt != null && dt.Terms.Count == 1, "deployment type found in AppEnforce.log, without the scope ID as a term");
            Check(items.Exists(delegate (FollowItem x) { return x.Kind == Follow.DeploymentType && x.Name.StartsWith("Notepad++", StringComparison.Ordinal); }), "second deployment type on the same scope is listed");
            Check(items.Exists(delegate (FollowItem x) { return x.Kind == Follow.Update && x.Id == "KB5066835"; }), "software update found in UpdatesDeployment.log");
            Check(items.Exists(delegate (FollowItem x) { return x.Kind == Follow.Package && x.Id == "PS100045:Install"; }), "program found in execmgr.log");
            if (dt == null) return;
            FollowResult r = Follow.Start(dt, true);
            Follow.ScanFiles(files, r, null, null);
            Check(r.Total == 13 && r.Files.Count == 6, "follow finds 13 lines in 6 logs (found " + r.Total + " in " + r.Files.Count + ")");
            Check(r.Terms.Exists(delegate (string t) { return t.Equals("11112222-3333-4444-5555-666677778888", StringComparison.OrdinalIgnoreCase); }), "follow chains content ID to CTM job to DTS job");
            bool noOther = true;
            foreach (string f in r.Files) if (Path.GetFileName(f) == "UpdatesDeployment.log" || Path.GetFileName(f) == "execmgr.log") noOther = false;
            Check(noOther, "follow leaves unrelated logs out");
        }

        static void FollowIntune()
        {
            List<string> files = Follow.LogFiles(new string[] { Path.Combine(data, "follow") });
            List<FollowItem> items = Follow.DiscoverFiles(files);
            FollowItem app = items.Find(delegate (FollowItem x) { return x.Kind == Follow.IntuneApp && x.Name == "Company Portal Helper"; });
            Check(app != null, "Intune Win32 app found in the IME log");
            if (app == null) return;
            FollowResult r = Follow.Start(app, true);
            Follow.ScanFiles(files, r, null, null);
            Check(r.Total == 2 && r.Files.Count == 1, "follow finds the Intune app's 2 lines");
        }

        static void UpdateRelease()
        {
            const string dl = "https://github.com/Baldissera88/LogVice/releases/download/v99.0.0/";
            string json = "{\"tag_name\":\"v99.0.0\",\"html_url\":\"https://github.com/Baldissera88/LogVice/releases/tag/v99.0.0\",\"assets\":["
                + "{\"name\":\"LogVice.msi\",\"browser_download_url\":\"" + dl + "LogVice.msi\"},"
                + "{\"name\":\"LogVice.exe\",\"browser_download_url\":\"https://example.com/Baldissera88/LogVice/releases/download/v99.0.0/LogVice.exe\"},"
                + "{\"name\":\"SHA256SUMS.txt\",\"browser_download_url\":\"" + dl + "SHA256SUMS.txt\"}]}";
            UpdateInfo r = new UpdateInfo();
            Updates.Parse(json, r);
            Check(r.Newer && r.Latest == "99.0.0" && r.Url.EndsWith("/tag/v99.0.0", StringComparison.Ordinal), "update check reads the latest version and its release page");
            Check(r.MsiUrl == dl + "LogVice.msi" && r.SumsUrl == dl + "SHA256SUMS.txt", "update check takes the MSI and checksums from the repository's own releases");
            Check(r.ExeUrl == null, "update check ignores downloads from any other address");

            UpdateInfo old = new UpdateInfo();
            Updates.Parse("{\"tag_name\":\"v99.0.0\",\"assets\":[{\"browser_download_url\":\"" + dl + "LogVice.msi\"},{\"browser_download_url\":\"" + dl + "LogVice.exe\"}]}", old);
            Check(old.Newer && !old.CanInstall, "a release without SHA256SUMS.txt is offered as a download, not installed");

            UpdateInfo same = new UpdateInfo();
            Updates.Parse("{\"tag_name\":\"v" + LogVice.Program.Version + "\"}", same);
            Check(!same.Newer && !same.CanInstall, "the running version is not offered as an update");

            string a = new string('a', 64), b = new string('B', 64);
            string sums = a + "  LogVice.exe\n" + b + " *LogVice.msi\r\nnot a checksum line\n";
            Check(Updates.ExpectedHash(sums, "LogVice.exe") == a && Updates.ExpectedHash(sums, "LogVice.msi") == b.ToLowerInvariant() && Updates.ExpectedHash(sums, "other.txt") == null,
                "SHA256SUMS.txt lines are read in sha256sum format");

            string tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "abc");
                Check(Updates.FileHash(tmp) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "file checksum is SHA-256 in lower case hex");
            }
            finally { File.Delete(tmp); }
        }
    }
}
