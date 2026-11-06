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
            FollowIntuneMore();
            FollowSeveral();
            InstalledApps();
            EventsWithoutEventLog();
            UpdateRelease();
            Notes();

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

        static void Notes()
        {
            string log = ReleaseNotes.Changelog;
            string ver = LogVice.Program.Version;
            Check(log.Contains("## LogVice " + ver + "\n") || log.Contains("## LogVice " + ver + "\r\n"), "the changelog is built into LogVice and has a section for " + ver);
            string mine = ReleaseNotes.Section(log, ver);
            Check(mine.StartsWith("## LogVice " + ver, StringComparison.Ordinal) && mine.IndexOf("\n## ", StringComparison.Ordinal) < 0, "What's new shows only this version's section");
            string since = ReleaseNotes.Sections(log, "1.5.0", "1.5.2");
            Check(since.Contains("## LogVice 1.5.2") && since.Contains("## LogVice 1.5.1") && !since.Contains("## LogVice 1.5.0") && !since.Contains("## LogVice 1.5.3") && !since.Contains("## Install and upgrade"),
                "notes since a version cover the versions after it and nothing else");
            string all = ReleaseNotes.Sections(log, null, ver);
            Check(all.Contains("## Trace Lens 1.1.0") && !all.Contains("### At a glance") && !all.Contains("## Version history"), "all notes list every version and leave the other parts out");

            string body = "Fixed a thing.\r\n\r\n- one\r\n\r\n### Deployment\n\n| | |\n### SHA256\n```\nabc\n```";
            Check(ReleaseNotes.FromRelease(body) == "Fixed a thing.\n\n- one" && ReleaseNotes.FromRelease("### Deployment\nx") == null && ReleaseNotes.FromRelease(null) == null,
                "release notes from GitHub leave out the deployment table and checksums");

            UpdateInfo r = new UpdateInfo();
            Updates.Parse("{\"tag_name\":\"v99.0.0\",\"body\":\"Line \\\"one\\\"\\r\\n- **b\\u00e6r** C:\\\\Logs\\n\\n### Deployment\\nx\"}", r);
            Check(r.Notes == "Line \"one\"\n- **b\u00e6r** C:\\Logs", "release notes are read from the GitHub answer");

            List<NoteBlock> p = ReleaseNotes.Parse("Use **bold** and `code` and [the **site**](https://x.y) here.");
            bool inline = p.Count == 1 && p[0].Kind == NoteKind.Paragraph && p[0].Runs.Count == 8
                && p[0].Runs[1].Text == "bold" && p[0].Runs[1].Bold && p[0].Runs[3].Text == "code" && p[0].Runs[3].Code
                && p[0].Runs[5].Text == "the " && p[0].Runs[5].Url == "https://x.y" && !p[0].Runs[5].Bold
                && p[0].Runs[6].Text == "site" && p[0].Runs[6].Bold && p[0].Runs[6].Url == "https://x.y" && p[0].Runs[7].Text == " here." && p[0].Runs[7].Url == null;
            Check(inline, "release notes read bold, code and links");
            List<NoteBlock> q = ReleaseNotes.Parse("## Title\n\n| | |\n|---|---|\n| Product code | `{X}` |\n\n- one\n  - two\n\n```\nraw **text**\n```\nline one\nline two");
            bool shape = q.Count == 6 && q[0].Kind == NoteKind.Heading && q[1].Kind == NoteKind.Row && q[1].Cells.Count == 2 && q[1].Cells[1][0].Code
                && q[2].Kind == NoteKind.Bullet && q[2].Depth == 0 && q[3].Depth == 1 && q[4].Kind == NoteKind.Code && q[4].Lines[0] == "raw **text**"
                && q[5].Kind == NoteKind.Paragraph && q[5].Runs[0].Text == "line one line two";
            Check(shape, "release notes read headings, tables, nested bullets, code blocks and paragraphs");
            int heads = 0, versions = Regex.Matches(all, "^## (LogVice|Trace Lens) ", RegexOptions.Multiline).Count;
            foreach (NoteBlock nb in ReleaseNotes.Parse(all)) if (nb.Kind == NoteKind.Heading) heads++;
            Check(heads == versions && versions > 10, "the whole changelog reads as " + versions + " version sections");
        }

        static List<string> FollowFiles()
        {
            return Follow.LogFiles(new string[] { Path.Combine(data, "follow") });
        }

        static void FollowIntuneMore()
        {
            List<string> files = FollowFiles();
            List<FollowItem> items = Follow.DiscoverFiles(files);
            FollowItem msi = items.Find(delegate (FollowItem x) { return x.Name == "Fabrikam Viewer"; });
            Check(msi != null && msi.Kind == Follow.IntuneApp && msi.Terms.Contains("B7C8D9E0-1F2A-4B3C-9D4E-5F6A7B8C9D0E"), "Intune app picks up the MSI product code from its detection rule");
            FollowItem store = items.Find(delegate (FollowItem x) { return x.Name == "Contoso Notes"; });
            Check(store != null && store.Kind == Follow.StoreApp && store.Terms.Contains("9NCONTOSO123"), "Store app is recognised by its package identifier");
            FollowItem rem = items.Find(delegate (FollowItem x) { return x.Kind == Follow.Remediation; });
            Check(rem != null && rem.Id == "5b6c7d8e-9f0a-4b1c-8d2e-3f4a5b6c7d8e" && rem.State == "Not compliant" && rem.Note == "Output: Disk cleanup is not needed",
                "remediation found in HealthScripts.log with its result and output");
            FollowItem ps = items.Find(delegate (FollowItem x) { return x.Kind == Follow.Script; });
            bool onlyOne = items.FindAll(delegate (FollowItem x) { return x.Kind == Follow.Script; }).Count == 1;
            Check(ps != null && onlyOne && ps.Id == "8f9e0d1c-2b3a-4c5d-9e8f-7a6b5c4d3e2f" && ps.State == "Success", "platform script found, and a remediation is not listed as a script");
            if (store != null)
            {
                FollowResult r = Follow.Start(store, false);
                Follow.ScanFiles(files, r, null, null);
                Check(r.Total == 3 && r.Files.Count == 2, "following a Store app finds its WinGet lines in AgentExecutor.log (found " + r.Total + " in " + r.Files.Count + ")");
            }
            if (msi != null)
            {
                FollowResult r = Follow.Start(msi, false);
                Follow.ScanFiles(files, r, null, null);
                Check(r.Total == 2 && r.Files.Count == 1, "following an MSI app finds its product code detection (found " + r.Total + " in " + r.Files.Count + ")");
            }
            if (rem != null)
            {
                FollowResult r = Follow.Start(rem, false);
                Follow.ScanFiles(files, r, null, null);
                Check(r.Total == 4 && r.Files.Count == 3, "following a remediation finds it in HealthScripts, AgentExecutor and the IME log (found " + r.Total + " in " + r.Files.Count + ")");
            }
        }

        static void FollowSeveral()
        {
            List<string> files = FollowFiles();
            List<FollowItem> items = Follow.DiscoverFiles(files);
            List<FollowItem> pick = items.FindAll(delegate (FollowItem x) { return x.Name == "Fabrikam Viewer" || x.Name == "Contoso Notes"; });
            FollowResult r = Follow.Start(pick, false);
            Follow.ScanFiles(files, r, null, null);
            Check(pick.Count == 2 && r.Terms.Count == 4 && r.Total == 4 && r.Files.Count == 2, "following two items gives one timeline, each line once (found " + r.Total + " in " + r.Files.Count + ")");
        }

        static void InstalledApps()
        {
            Check(Follow.InstalledItem("{11111111-2222-3333-4444-555555555555}", "Tool", "1.0", "Me", 1, null, null) == null
                && Follow.InstalledItem("KB123", "Update for Tool", "1", null, 0, "Tool", null) == null
                && Follow.InstalledItem("X", "Security Update", "1", null, 0, null, "Security Update") == null
                && Follow.InstalledItem("X", "  ", "1", null, 0, null, null) == null,
                "installed apps leave out system components, updates and entries without a name");
            FollowItem a = Follow.InstalledItem("{b7c8d9e0-1f2a-4b3c-9d4e-5f6a7b8c9d0e}", "Fabrikam Viewer 2.1", "2.1.0", "Fabrikam", 0, null, null);
            FollowItem b = Follow.InstalledItem("FabrikamTool", "Fabrikam Tool", "5", null, 0, null, null);
            Check(a != null && a.Id == "{B7C8D9E0-1F2A-4B3C-9D4E-5F6A7B8C9D0E}" && a.Terms.Count == 1 && a.NameIsTerm && b != null && b.Id == null && b.Terms.Count == 0,
                "installed app takes its MSI product code from the key name");
            FollowResult r = Follow.Start(b, false);
            Check(r.Terms.Count == 1 && r.Terms[0] == "Fabrikam Tool", "an installed app without a product code is followed by its name");

            const string root = @"Software\LogViceTests";
            Microsoft.Win32.RegistryKey baseKey = null;
            try
            {
                baseKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(root);
                using (Microsoft.Win32.RegistryKey k = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AAAABBBB-CCCC-4DDD-8EEE-FFFF00001111}"))
                {
                    k.SetValue("DisplayName", "Northwind Reports");
                    k.SetValue("DisplayVersion", "4.2");
                    k.SetValue("Publisher", "Northwind");
                }
                using (Microsoft.Win32.RegistryKey k = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hidden"))
                {
                    k.SetValue("DisplayName", "Hidden part");
                    k.SetValue("SystemComponent", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
                List<FollowItem> list = new List<FollowItem>();
                Follow.ReadUninstall(baseKey, list, new HashSet<string>());
                Check(list.Count == 1 && list[0].Name == "Northwind Reports" && list[0].State == "4.2" && list[0].Note == "Northwind", "installed apps are read from the Uninstall key");
            }
            catch (Exception ex) { Fail("installed apps are read from the Uninstall key: " + ex.Message); }
            finally
            {
                if (baseKey != null) baseKey.Close();
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(root); } catch (Exception) { }
            }

            List<FollowItem> intune = Follow.DiscoverFiles(FollowFiles());
            List<FollowItem> merged = Follow.Merge(intune, new List<FollowItem> { a });
            FollowItem viewer = merged.Find(delegate (FollowItem x) { return x.Name == "Fabrikam Viewer"; });
            bool gone = !merged.Exists(delegate (FollowItem x) { return x.Kind == Follow.Installed && x.Name == "Fabrikam Viewer 2.1"; });
            Check(viewer != null && gone && viewer.State == "Installed 2.1.0", "an installed app that Intune deploys is shown once, on the Intune app");
        }

        static void EventsWithoutEventLog()
        {
            FollowResult r = Follow.Start(new FollowItem[0], false);
            r.Terms.Add("Northwind Reports");
            bool ok = true;
            try { Follow.ScanEvents(null, r, null, null); }
            catch (Exception) { ok = false; }
            Check(ok, "searching Windows events never throws, also where there are no event logs");
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
