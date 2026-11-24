namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    // One field that goes along with a report or a wish. Shown on the page before anything leaves, then put in the link.
    public sealed class FeedbackField
    {
        // The id of the field in the GitHub issue form, which is also its name in the link.
        public string Id;
        public string Label;
        public string Value;
        // Typed by the user, not filled in by LogVice.
        public bool Typed;

        public FeedbackField(string id, string label, string value, bool typed)
        {
            Id = id;
            Label = label;
            Value = value ?? "";
            Typed = typed;
        }
    }

    // Links that open a pre-filled GitHub issue or e-mail. LogVice sends nothing itself: the browser or mail program
    // opens with the text, and the user sends it.
    public static class Feedback
    {
        public const string Email = "dbal.itx@gmail.com";
        public const string RepoUrl = "https://github.com/" + Updates.Repo;
        public const string IssuesNew = RepoUrl + "/issues/new";
        public const string BugTemplate = "bug_report.yml";
        public const string FeatureTemplate = "feature_request.yml";
        // GitHub documents no length, only "414 URI Too Long" above its server limit, which is about 8 KB. This stays well below.
        public const int MaxIssueUrl = 6000;
        // Mail programs on Windows cut mailto: links of about 2,000 characters.
        public const int MaxMailUrl = 2000;
        public const string CutNote = "[The rest did not fit in the link.]";

        // Percent-encodes as RFC 3986 says, for a query value or a mailto: header. Spaces become %20, not +.
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder b = new StringBuilder(s.Length * 2);
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            foreach (byte c in bytes)
            {
                bool plain = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '_' || c == '~';
                if (plain) b.Append((char)c);
                else b.Append('%').Append(c.ToString("X2", CultureInfo.InvariantCulture));
            }
            return b.ToString();
        }

        // https://github.com/<repo>/issues/new?template=<form>&title=...&<field id>=... GitHub fills the issue form's fields by
        // their id. Values that make the link longer than max are cut, the longest first.
        public static string IssueUrl(string template, string title, List<FeedbackField> fields, int max)
        {
            List<string[]> q = new List<string[]>();
            q.Add(new string[] { "template", template });
            q.Add(new string[] { "title", Short(title, 100) });
            foreach (FeedbackField f in fields) q.Add(new string[] { f.Id, Lines(f.Value) });
            return Fit(IssuesNew + "?", q, 2, "&", max);
        }

        // mailto:dbal.itx@gmail.com?subject=...&body=... with the body cut to fit max.
        public static string MailUrl(string subject, string body, int max)
        {
            List<string[]> q = new List<string[]>();
            q.Add(new string[] { "subject", Short(subject, 120) });
            q.Add(new string[] { "body", (body ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n") });
            return Fit("mailto:" + Email + "?", q, 1, "&", max);
        }

        // The fields as plain text, for an e-mail or the clipboard.
        public static string Body(List<FeedbackField> fields)
        {
            StringBuilder b = new StringBuilder();
            foreach (FeedbackField f in fields)
            {
                b.Append(f.Label).Append(":\n");
                string v = Lines(f.Value).Trim();
                b.Append(v.Length > 0 ? v : "-").Append("\n\n");
            }
            return b.ToString().TrimEnd() + "\n";
        }

        static string Lines(string s)
        {
            return (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        }

        public static string Short(string s, int max)
        {
            s = (s ?? "").Trim();
            int nl = s.IndexOf('\n');
            if (nl >= 0) s = s.Substring(0, nl).Trim();
            return s.Length <= max ? s : s.Substring(0, max - 3).TrimEnd() + "...";
        }

        static string Join(string head, List<string[]> q, string sep)
        {
            StringBuilder b = new StringBuilder(head);
            bool first = true;
            foreach (string[] p in q)
            {
                if (p[1].Length == 0 && p[0] != "body") continue;
                if (!first) b.Append(sep);
                b.Append(p[0]).Append('=').Append(Escape(p[1]));
                first = false;
            }
            return b.ToString();
        }

        // Cuts the longest values from index from on, one at a time, until the link fits. Each cut value ends with CutNote.
        static string Fit(string head, List<string[]> q, int from, string sep, int max)
        {
            string url = Join(head, q, sep);
            int guard = 0;
            while (url.Length > max && guard++ < 64)
            {
                int longest = -1, len = 0;
                for (int i = from; i < q.Count; i++)
                {
                    int l = Escape(q[i][1]).Length;
                    if (l > len && q[i][1] != CutNote) { len = l; longest = i; }
                }
                if (longest < 0) break;
                string v = q[longest][1];
                int room = len - (url.Length - max);
                q[longest][1] = Prefix(v, room);
                url = Join(head, q, sep);
            }
            return url;
        }

        // The longest start of v that, with CutNote after it, takes at most room characters once encoded.
        static string Prefix(string v, int room)
        {
            if (Escape(CutNote).Length > room) return CutNote;
            int lo = 0, hi = v.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Escape(Cut(v, mid)).Length <= room) lo = mid;
                else hi = mid - 1;
            }
            return Cut(v, lo);
        }

        static string Cut(string v, int n)
        {
            if (n > 0 && n < v.Length && char.IsHighSurrogate(v[n - 1])) n--;
            string s = v.Substring(0, n).TrimEnd();
            return s.Length == 0 ? CutNote : s + "\n" + CutNote;
        }

        // How many logs of each format are open, as "CMTrace 3, Plain text 1". Never a path or a file name.
        public static string FormatsText(IEnumerable<string> names)
        {
            List<string> order = new List<string>();
            Dictionary<string, int> count = new Dictionary<string, int>();
            if (names != null)
            {
                foreach (string n in names)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    if (!count.ContainsKey(n)) { count[n] = 0; order.Add(n); }
                    count[n]++;
                }
            }
            if (order.Count == 0) return "No log is open.";
            order.Sort(delegate (string x, string y)
            {
                int c = count[y].CompareTo(count[x]);
                return c != 0 ? c : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            });
            List<string> parts = new List<string>();
            foreach (string n in order) parts.Add(n + " " + count[n]);
            return string.Join(", ", parts.ToArray());
        }
    }

    // Help > Report a problem: what the user types, and what LogVice fills in and shows before anything leaves.
    public sealed class ProblemReport
    {
        public string What = "", Expected = "", Steps = "";
        public string Version = "", Windows = "", DotNet = "", Theme = "", Formats = "";

        public static ProblemReport Make(string version, string windows, string dotnet, string theme, string formats)
        {
            ProblemReport r = new ProblemReport();
            r.Version = version ?? "";
            r.Windows = windows ?? "";
            r.DotNet = dotnet ?? "";
            r.Theme = theme ?? "";
            r.Formats = formats ?? "";
            return r;
        }

        // What LogVice adds, in the order the page shows it. The ids are the ids of the fields in bug_report.yml.
        public List<FeedbackField> Filled()
        {
            List<FeedbackField> l = new List<FeedbackField>();
            l.Add(new FeedbackField("version", "LogVice version", Version, false));
            l.Add(new FeedbackField("windows", "Windows version", Windows, false));
            l.Add(new FeedbackField("dotnet", ".NET Framework version", DotNet, false));
            l.Add(new FeedbackField("theme", "Theme", Theme, false));
            l.Add(new FeedbackField("formats", "Formats of the open logs", Formats, false));
            return l;
        }

        public List<FeedbackField> Fields()
        {
            List<FeedbackField> l = new List<FeedbackField>();
            l.Add(new FeedbackField("what-happened", "What happened", What, true));
            l.Add(new FeedbackField("expected", "What you expected", Expected, true));
            l.Add(new FeedbackField("steps", "Steps to repeat it", Steps, true));
            l.AddRange(Filled());
            return l;
        }

        public string Title
        {
            get { string s = Feedback.Short(What, 80); return "[Bug]: " + (s.Length > 0 ? s : ""); }
        }

        public string IssueUrl()
        {
            return Feedback.IssueUrl(Feedback.BugTemplate, Title, Fields(), Feedback.MaxIssueUrl);
        }

        public string MailUrl()
        {
            return Feedback.MailUrl("LogVice problem: " + Feedback.Short(What, 80), Feedback.Body(Fields()), Feedback.MaxMailUrl);
        }
    }

    // Help > Suggest a feature.
    public sealed class FeatureWish
    {
        public string What = "", Why = "", How = "";

        public List<FeedbackField> Fields()
        {
            List<FeedbackField> l = new List<FeedbackField>();
            l.Add(new FeedbackField("what", "What you would like", What, true));
            l.Add(new FeedbackField("why", "Why", Why, true));
            l.Add(new FeedbackField("how", "How you would use it", How, true));
            return l;
        }

        public string Title
        {
            get { return "[Feature]: " + Feedback.Short(What, 80); }
        }

        public string IssueUrl()
        {
            return Feedback.IssueUrl(Feedback.FeatureTemplate, Title, Fields(), Feedback.MaxIssueUrl);
        }

        public string MailUrl()
        {
            return Feedback.MailUrl("LogVice wish: " + Feedback.Short(What, 80), Feedback.Body(Fields()), Feedback.MaxMailUrl);
        }
    }

    // When the Help pages that open a browser or mail program are not offered. The same device checks as the update
    // check, except the policy, which only turns off updates.
    public static class FeedbackGate
    {
        public static string Reason(UpdateContext c)
        {
            if (c == null || !c.Window) return "Only in the LogVice window.";
            if (c.WinPE) return "Not in Windows PE.";
            if (c.System) return "Not while LogVice runs as SYSTEM.";
            if (c.TaskSequence) return "Not while a task sequence runs on this device.";
            if (c.Oobe) return "Not during Windows setup or the Enrollment Status Page.";
            return null;
        }
    }
}
