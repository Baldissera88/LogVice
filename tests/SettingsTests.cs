namespace LogVice.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    // The Settings catalog, the update gate, and the feedback links.
    static partial class Program
    {
        sealed class FakeReg : IRegSource
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string[]> Keys = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            public string[] SubKeys(string path)
            {
                string[] k;
                return Keys.TryGetValue(path, out k) ? k : new string[0];
            }

            public string Value(string path, string name)
            {
                string v;
                return Values.TryGetValue(path + "|" + name, out v) ? v : null;
            }
        }

        static void SettingsCatalogTests()
        {
            HashSet<string> ids = new HashSet<string>();
            bool unique = true, pageKnown = true;
            foreach (SettingDef d in SettingsCatalog.All)
            {
                if (!ids.Add(d.Id)) unique = false;
                if (SettingsCatalog.Page(d.Page) == null) pageKnown = false;
                if (string.IsNullOrEmpty(d.Title) || string.IsNullOrEmpty(d.Summary)) pageKnown = false;
            }
            Check(unique && pageKnown, "every setting has a unique id, a title, a description and a page that exists");
            bool each = true;
            foreach (SettingPage p in SettingsCatalog.Pages) if (SettingsCatalog.On(p.Id).Count == 0) each = false;
            Check(each && SettingsCatalog.Pages.Length == 7, "the seven subjects each hold at least one setting");
            Check(SettingsCatalog.Pages[0].Id == "general" && SettingsCatalog.Pages[6].Id == "data", "General comes first and Data and privacy last");

            // Every value LogVice has kept under HKCU\Software\LogVice that a setting controls is in the catalog.
            string[] keys = { "Theme", "TextSize", "Density", "Tint", "LineNumbers", "Live", "AutoMerge", "History", "ReopenLast", "UpdateCheck", "Folders" };
            bool all = true;
            string missing = "";
            foreach (string k in keys)
            {
                bool found = false;
                foreach (SettingDef d in SettingsCatalog.All) if (Array.IndexOf(d.Keys, k) >= 0) found = true;
                if (!found) { all = false; missing += k + " "; }
            }
            Check(all, "every earlier LogVice setting has a place in the catalog " + missing);
            string[] titles = { "Theme", "Reopen the last logs at start", "Start with Live on", "Log text size", "Row spacing", "Tint error and warning rows", "Show line numbers",
                "Auto-merge spanned logs", "Administrator rights", "Default app for .log files", "Check for updates", "Recent files", "Reset all settings", "Add a folder", "Found on this computer" };
            bool named = true;
            foreach (string t in titles)
            {
                bool f = false;
                foreach (SettingDef d in SettingsCatalog.All) if (d.Title == t) f = true;
                if (!f) { named = false; missing = t; }
            }
            Check(named, "the settings of the old Settings view keep their names (" + missing + ")");
            Check(SettingsCatalog.Find("theme").Page == SettingsCatalog.General && SettingsCatalog.Find("textsize").Page == SettingsCatalog.List
                && SettingsCatalog.Find("automerge").Page == SettingsCatalog.Reading && SettingsCatalog.Find("addfolder").Page == SettingsCatalog.Folders
                && SettingsCatalog.Find("updatecheck").Page == SettingsCatalog.Updates && SettingsCatalog.Find("recent").Page == SettingsCatalog.Data
                && SettingsCatalog.Find("rules").Page == SettingsCatalog.Highlights, "settings sit on the pages the plan names");
            Check(SettingsCatalog.Remembered("folders") == "folders" && SettingsCatalog.Remembered("nonsense") == "general" && SettingsCatalog.Remembered(null) == "general", "the remembered subject falls back to General");
        }

        static void SettingsSearch()
        {
            List<SettingDef> a = SettingsCatalog.Search("theme");
            Check(a.Count > 0 && a[0].Id == "theme", "search finds a setting by its title, and puts the title match first");
            List<SettingDef> b = SettingsCatalog.Search("tight rows");
            Check(b.Count == 1 && b[0].Id == "density", "search finds a setting by words of its description");
            List<SettingDef> c = SettingsCatalog.Search("COLOUR");
            bool tint = false, rules = false;
            foreach (SettingDef d in c) { if (d.Id == "tint") tint = true; if (d.Id == "rules") rules = true; }
            Check(tint && rules, "search ignores case and finds keywords");
            Check(SettingsCatalog.Search("xyzzy").Count == 0 && SettingsCatalog.Search("  ").Count == 0 && SettingsCatalog.Search(null).Count == 0, "search finds nothing for unknown or empty text");
            List<SettingDef> d2 = SettingsCatalog.Search("registry hkcu");
            Check(d2.Count >= 1 && d2[0].Page == SettingsCatalog.Data, "search finds a setting by its keywords");
            List<SettingDef> e = SettingsCatalog.Search("Administrator");
            bool admin = false;
            foreach (SettingDef d in e) if (d.Id == "admin") admin = true;
            Check(admin, "search finds Administrator rights");
        }

        static void SettingsReset()
        {
            MemorySettingStore s = new MemorySettingStore();
            Check(SettingsCatalog.ChangedOn(SettingsCatalog.List, s).Count == 0, "nothing saved means nothing changed");
            s.Set("TextSize", "1");
            s.Set("Tint", "true");
            s.Set("Live", "1");
            Check(SettingsCatalog.ChangedOn(SettingsCatalog.List, s).Count == 0, "a saved value equal to the default is not a change, in 1, 0, true or false");
            s.Set("TextSize", "2");
            s.Set("Tint", "0");
            List<SettingDef> ch = SettingsCatalog.ChangedOn(SettingsCatalog.List, s);
            Check(ch.Count == 2 && SettingsCatalog.Find("textsize").Changed(s) && SettingsCatalog.Find("tint").Changed(s) && !SettingsCatalog.Find("density").Changed(s), "a saved value that is not the default is marked as changed");
            s.Set("Theme", "Terminal");
            s.Set("ReopenLast", "1");
            Check(SettingsCatalog.Find("theme").Changed(s) && SettingsCatalog.ChangedOn(SettingsCatalog.General, s).Count == 2, "the theme and a toggle on General are changed");
            s.Set("Theme", "vice");
            Check(!SettingsCatalog.Find("theme").Changed(s), "the theme name is compared without regard to case");
            int removed = SettingsCatalog.Reset(SettingsCatalog.List, s);
            Check(removed == 2 && s.Get("TextSize") == null && s.Get("Tint") == null && s.Get("ReopenLast") == "1", "reset removes the saved values of one subject and leaves the others");
            Check(SettingsCatalog.ChangedOn(SettingsCatalog.List, s).Count == 0, "after reset nothing on the page is changed");
            s.Set("AutoMerge", "0");
            s.Set("History", "0");
            Check(SettingsCatalog.Find("automerge").Changed(s) && SettingsCatalog.Reset(SettingsCatalog.Reading, s) == 2 && s.Get("History") == null, "reset also removes the older History value");
            s.Set("Folders", "x");
            s.Set("Highlights", "x");
            Check(!SettingsCatalog.Resettable(SettingsCatalog.Folders) && !SettingsCatalog.Resettable(SettingsCatalog.Highlights) && SettingsCatalog.Reset(SettingsCatalog.Folders, s) == 0 && s.Get("Folders") == "x",
                "folders and highlight rules are not removed by a reset");
            Check(SettingsCatalog.Resettable(SettingsCatalog.General) && SettingsCatalog.Resettable(SettingsCatalog.Updates) && !SettingsCatalog.Resettable(SettingsCatalog.Data), "the subjects with defaults can be reset");
        }

        static void UpdateGateTests()
        {
            UpdateContext c = new UpdateContext();
            Check(UpdateGate.Reason(c) == UpdateGate.NoWindow && UpdateGate.Reason(null) == UpdateGate.NoWindow, "nothing may be checked until the window says so");
            c.Window = true;
            Check(UpdateGate.Reason(c) == null, "a normal window may check");
            c.Screenshot = true;
            Check(UpdateGate.Reason(c) != null, "no check on the command line with --shot");
            c = new UpdateContext(); c.Window = true; c.WinPE = true;
            Check(UpdateGate.Reason(c) != null && UpdateGate.Reason(c).Contains("Windows PE"), "no check in Windows PE");
            c = new UpdateContext(); c.Window = true; c.System = true;
            Check(UpdateGate.Reason(c) != null && UpdateGate.Reason(c).Contains("SYSTEM"), "no check as SYSTEM");
            c = new UpdateContext(); c.Window = true; c.TaskSequence = true;
            Check(UpdateGate.Reason(c) != null && UpdateGate.Reason(c).Contains("task sequence"), "no check while a task sequence runs");
            c = new UpdateContext(); c.Window = true; c.Oobe = true;
            Check(UpdateGate.Reason(c) != null && UpdateGate.Reason(c).Contains("Enrollment Status Page"), "no check during Windows setup or the ESP");
            c = new UpdateContext(); c.Window = true; c.Policy = true;
            Check(UpdateGate.Reason(c) != null && UpdateGate.Reason(c).Contains("DisableUpdateCheck"), "no check when the policy turns it off");

            FakeReg reg = new FakeReg();
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "Alex", false, false);
            Check(UpdateGate.Reason(c) == null, "an ordinary device may check");
            reg.Keys[@"HKLM\SYSTEM\CurrentControlSet\Control"] = new string[] { "Lsa", "MiniNT" };
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "Alex", false, false);
            Check(c.WinPE && UpdateGate.Reason(c) != null, "the MiniNT key means Windows PE");
            reg = new FakeReg();
            reg.Values[@"HKLM\SYSTEM\Setup|OOBEInProgress"] = "1";
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "Alex", false, false);
            Check(c.Oobe, "OOBEInProgress means Windows setup");
            reg = new FakeReg();
            reg.Values[@"HKLM\SYSTEM\Setup|SystemSetupInProgress"] = "1";
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "Alex", false, false);
            Check(c.Oobe, "SystemSetupInProgress means Windows setup");
            reg = new FakeReg();
            reg.Values[@"HKLM\SYSTEM\Setup|OOBEInProgress"] = "0";
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "defaultuser0", false, false);
            Check(c.Oobe, "the user defaultuser0 means the ESP");
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "Alex", false, false);
            Check(!c.Oobe, "OOBEInProgress = 0 is not setup");
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, new FakeReg(), "SYSTEM", true, true);
            Check(c.System && c.TaskSequence && UpdateGate.Reason(c).Contains("SYSTEM"), "SYSTEM and a task sequence come from the caller");
            reg = new FakeReg();
            reg.Values[UpdateGate.PolicyKey + "|" + UpdateGate.PolicyValue] = "1";
            c = new UpdateContext(); c.Window = true;
            UpdateGate.ReadDevice(c, reg, "Alex", false, false);
            Check(c.Policy && UpdateGate.BlockedByPolicy(reg) && UpdateGate.Reason(c).Contains("policy"), "DisableUpdateCheck = 1 is the policy");
            reg.Values[UpdateGate.PolicyKey + "|" + UpdateGate.PolicyValue] = "0";
            Check(!UpdateGate.BlockedByPolicy(reg), "DisableUpdateCheck = 0 is not a policy");
            Check(UpdateGate.PolicyKey == @"HKLM\SOFTWARE\Policies\LogVice" && UpdateGate.PolicyValue == "DisableUpdateCheck", "the policy key and value keep their names");

            // The gate also decides before anything is asked of GitHub.
            Func<string> keep = Updates.Gate;
            try
            {
                Updates.Gate = delegate { return "blocked for the test"; };
                UpdateInfo r = Updates.Check();
                Check(r.Error == "blocked for the test" && r.Latest == null, "a check that the gate stops asks nobody and says why");
                Updates.Gate = delegate { throw new InvalidOperationException(); };
                Check(Updates.Blocked == UpdateGate.NoWindow, "a gate that fails stops the check");
            }
            finally { Updates.Gate = keep; }
            Check(FeedbackGate.Reason(new UpdateContext()) != null, "Report a problem is only offered in the window");
            c = new UpdateContext(); c.Window = true; c.Policy = true;
            Check(FeedbackGate.Reason(c) == null, "the update policy does not turn off Report a problem");
            c.System = true;
            Check(FeedbackGate.Reason(c) != null, "Report a problem is not offered as SYSTEM");
        }

        static Dictionary<string, string> Query(string url)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            int q = url.IndexOf('?');
            foreach (string p in url.Substring(q + 1).Split('&'))
            {
                int eq = p.IndexOf('=');
                d[p.Substring(0, eq)] = Uri.UnescapeDataString(p.Substring(eq + 1));
            }
            return d;
        }

        static string RepoFile(string name)
        {
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(data)));
            return Path.Combine(root, name);
        }

        static void FeedbackLinks()
        {
            Check(Feedback.Escape("a b\nc&d=e+f") == "a%20b%0Ac%26d%3De%2Bf", "spaces, new lines, & = and + are encoded");
            Check(Feedback.Escape("S\u00f8lr\u00f8d") == "S%C3%B8lr%C3%B8d" && Feedback.Escape("A-z_0.9~") == "A-z_0.9~", "non-ASCII is UTF-8, unreserved characters stay");
            ProblemReport r = ProblemReport.Make("2.0.0", "Windows 11 Enterprise 24H2 (26100.2605)", ".NET Framework 4.8.1", "Vice", Feedback.FormatsText(new string[] { "CMTrace", "CMTrace", "Plain text" }));
            r.What = "The list is empty & slow";
            r.Expected = "Rows";
            r.Steps = "1. Open\n2. Filter";
            string url = r.IssueUrl();
            Check(url.StartsWith(Feedback.IssuesNew + "?template=bug_report.yml&title=%5BBug%5D%3A%20The%20list%20is%20empty%20%26%20slow"), "the bug link uses the bug form and a short title: " + url);
            Check(Feedback.IssuesNew == "https://github.com/Baldissera88/LogVice/issues/new", "the link goes to the issues of LogVice");
            Dictionary<string, string> q = Query(url);
            Check(q["what-happened"] == "The list is empty & slow" && q["steps"] == "1. Open\n2. Filter" && q["version"] == "2.0.0" && q["formats"] == "CMTrace 2, Plain text 1" && q["theme"] == "Vice" && q["dotnet"] == ".NET Framework 4.8.1", "the link fills the fields of the form by their ids");
            Check(url.Length <= Feedback.MaxIssueUrl, "a short report fits");

            // The ids in the link are the ids of the fields in the issue forms.
            string bug = File.ReadAllText(RepoFile(Path.Combine(".github", Path.Combine("ISSUE_TEMPLATE", "bug_report.yml"))));
            string feat = File.ReadAllText(RepoFile(Path.Combine(".github", Path.Combine("ISSUE_TEMPLATE", "feature_request.yml"))));
            bool bugIds = true, featIds = true;
            string lost = "";
            foreach (FeedbackField f in r.Fields()) if (!Regex.IsMatch(bug, @"^\s+id: " + Regex.Escape(f.Id) + @"\s*$", RegexOptions.Multiline)) { bugIds = false; lost += f.Id + " "; }
            FeatureWish w = new FeatureWish();
            foreach (FeedbackField f in w.Fields()) if (!Regex.IsMatch(feat, @"^\s+id: " + Regex.Escape(f.Id) + @"\s*$", RegexOptions.Multiline)) { featIds = false; lost += f.Id + " "; }
            Check(bugIds && featIds, "every field of the report and the wish is a field of its issue form (" + lost + ")");
            Check(File.Exists(RepoFile(Path.Combine(".github", Path.Combine("ISSUE_TEMPLATE", "config.yml")))) && !File.ReadAllText(RepoFile(Path.Combine(".github", Path.Combine("ISSUE_TEMPLATE", "config.yml")))).Contains("discussions"), "the issue chooser has no Discussions link");

            ProblemReport longer = ProblemReport.Make("2.0.0", "Windows 11", ".NET", "Vice", "CMTrace 1");
            longer.What = "It fails";
            longer.Steps = new string('x', 12000);
            string lu = longer.IssueUrl();
            Check(lu.Length <= Feedback.MaxIssueUrl && lu.Length > Feedback.MaxIssueUrl - 200, "a long report is cut to " + Feedback.MaxIssueUrl + " characters, got " + lu.Length);
            Dictionary<string, string> lq = Query(lu);
            Check(lq["steps"].EndsWith(Feedback.CutNote) && lq["what-happened"] == "It fails", "only the longest value is cut, and it ends with a note");
            longer.What = "\U0001F4E6" + new string('\u00e6', 4000);
            Dictionary<string, string> eq = Query(longer.IssueUrl());
            bool whole = true;
            foreach (string v in eq.Values) for (int i = 0; i < v.Length; i++) if (char.IsHighSurrogate(v[i]) && (i + 1 >= v.Length || !char.IsLowSurrogate(v[i + 1]))) whole = false;
            Check(whole, "a cut never splits a surrogate pair");

            FeatureWish wish = new FeatureWish();
            wish.What = "Filter by thread";
            wish.Why = "Many threads";
            string fu = wish.IssueUrl();
            Check(fu.StartsWith(Feedback.IssuesNew + "?template=feature_request.yml&title=%5BFeature%5D%3A%20Filter%20by%20thread"), "the feature link uses the feature form: " + fu);
            Check(Query(fu)["why"] == "Many threads" && !Query(fu).ContainsKey("how"), "an empty field is left out of the link");

            string mail = r.MailUrl();
            Check(mail.StartsWith("mailto:" + Feedback.Email + "?subject=LogVice%20problem%3A%20The%20list%20is%20empty%20%26%20slow&body="), "the e-mail link names the address and the subject");
            Check(Query(mail)["body"].Contains("What happened:\r\nThe list is empty & slow"), "the e-mail body lists the fields with their labels");
            longer.What = "Long";
            string lm = longer.MailUrl();
            Check(lm.Length <= Feedback.MaxMailUrl && Query(lm)["body"].Contains(Feedback.CutNote), "a long e-mail link is cut to " + Feedback.MaxMailUrl + " and says so");

            List<string> shown = new List<string>();
            foreach (FeedbackField f in r.Fields()) shown.Add(f.Id);
            List<string> filled = new List<string>();
            foreach (FeedbackField f in r.Filled()) { filled.Add(f.Id); Check(!f.Typed, f.Id + " is filled in by LogVice"); }
            Check(shown.Contains("what-happened") && filled.Count == 5 && !filled.Contains("what-happened"), "the page shows the same fields that go in the link, and only five are filled in by LogVice");
            string body = Feedback.Body(r.Fields());
            bool all = true;
            foreach (FeedbackField f in r.Fields()) if (!body.Contains(f.Label + ":")) all = false;
            Check(all, "the text lists every field by its label");

            Check(Feedback.FormatsText(null) == "No log is open." && Feedback.FormatsText(new string[0]) == "No log is open.", "no open log is said in words");
            Check(Feedback.FormatsText(new string[] { "Plain text", "CMTrace", "CMTrace", "CMTrace" }) == "CMTrace 3, Plain text 1", "formats are counted, most first, and never carry a name");
            string rep = string.Join(" ", new List<FeedbackField>(r.Filled()).ConvertAll(delegate (FeedbackField f) { return f.Value; }).ToArray());
            Check(!rep.Contains("\\") && !rep.Contains(".log"), "what LogVice fills in holds no path or file name");
        }

        static void GridWidths()
        {
            int[] want = { 200, 190, 80, 180 };
            int[] min = { 90, 120, 52, 90 };
            bool[] none = { false, false, false, false };
            bool[] all = { true, true, true, true };
            int[] w = LogGrid.FitWidths(900 - 100, 360, want, min, none, all);
            int tot = w[0] + w[1] + w[2] + w[3];
            Check(800 - tot >= 360, "FitWidths: message keeps its share");
            Check(w[3] == 90 && w[0] >= 90 && w[1] >= 120 && w[2] >= 52, "FitWidths: columns stay above their minimum");
            int[] roomy = LogGrid.FitWidths(2000, 360, want, min, none, all);
            Check(roomy[0] == 200 && roomy[3] == 180, "FitWidths: wide window changes nothing");
            bool[] keep = { true, false, false, false };
            int[] k = LogGrid.FitWidths(700, 300, want, min, keep, all);
            Check(k[0] == 200, "FitWidths: dragged width is kept");
            bool[] one = { true, true, true, false };
            int[] o = LogGrid.FitWidths(600, 240, want, min, none, one);
            Check(o[3] == 180, "FitWidths: hidden column is left alone");
        }

        static void FeedbackMail()
        {
            FakeReg reg = new FakeReg();
            Check(!MailHandler.HasProgram(reg), "no mail program is found when nothing is registered");
            reg.Values[@"HKLM\SOFTWARE\Classes\mailto\shell\open\command|"] = "\"C:\\Mail\\mail.exe\" \"%1\"";
            Check(MailHandler.HasProgram(reg), "a mailto open command is a mail program");
            reg = new FakeReg();
            reg.Values[@"HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\mailto\UserChoice|ProgId"] = "Gone";
            reg.Values[@"HKLM\SOFTWARE\Classes\mailto\shell\open\command|"] = "x";
            Check(!MailHandler.HasProgram(reg), "a chosen program that is gone is not a mail program");
            string text = null;
            string launched = null;
            MailResult none = MailHandler.Open(new FakeReg(), "mailto:x@example.com?subject=A%20note&body=B", "Whole text", delegate (string u) { launched = u; }, delegate (string t) { text = t; return true; });
            Check(none.Outcome == MailOutcome.NoProgram && none.Copied && launched == null && text.StartsWith("Subject: A note\r\n\r\n") && text.EndsWith("Whole text"), "without a mail program the subject and text are copied");
            reg = new FakeReg();
            reg.Values[@"HKLM\SOFTWARE\Classes\mailto\shell\open\command|"] = "x";
            MailResult ok = MailHandler.Open(reg, "mailto:x@example.com", "T", delegate (string u) { launched = u; }, delegate (string t) { return true; });
            Check(ok.Outcome == MailOutcome.Opened && launched == "mailto:x@example.com", "with a mail program the link is opened");
            MailResult bad = MailHandler.Open(reg, "mailto:x@example.com", "T", delegate (string u) { throw new InvalidOperationException("no"); }, delegate (string t) { text = t; return true; });
            Check(bad.Outcome == MailOutcome.NoProgram && bad.Copied, "when opening fails the text is copied");
        }
    }
}
