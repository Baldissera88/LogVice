namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    // One log of a session: a file, a Windows event log, or a log on another computer.
    public sealed class SessionSource
    {
        public string Path;
        public string Channel;
        public string Computer;

        // The source for a path as LogVice opens it: a file, evt:<channel> or evt:\\<computer>\<channel>.
        public static SessionSource From(string path)
        {
            SessionSource s = new SessionSource();
            if (EventLogs.IsChannel(path))
            {
                EventLogs.Split(path, out s.Computer, out s.Channel);
                return s;
            }
            // \\PC\C$\Windows\CCM\Logs\x.log is the file C:\Windows\CCM\Logs\x.log on PC.
            Match m = Regex.Match(path ?? "", @"^\\\\([^\\]+)\\([A-Za-z])\$(\\.*)?$");
            if (m.Success)
            {
                s.Computer = m.Groups[1].Value;
                s.Path = m.Groups[2].Value.ToUpperInvariant() + ":" + (m.Groups[3].Success ? m.Groups[3].Value : "\\");
                return s;
            }
            s.Path = path;
            return s;
        }

        // The path to open, the inverse of From.
        public string ToPath()
        {
            if (Channel != null) return EventLogs.Make(Computer, Channel);
            if (string.IsNullOrEmpty(Computer)) return Path;
            if (Path != null && Regex.IsMatch(Path, @"^[A-Za-z]:(\\|$)"))
                return @"\\" + Computer + @"\" + Path.Substring(0, 1) + "$" + Path.Substring(2);
            return Path;
        }
    }

    // The filter of a session, the same things the filter row and the count chips hold.
    public sealed class SessionFilter
    {
        public string Text = "";
        public bool Regex, Exclude;
        // Bit 1 information, 2 warnings, 4 errors. 7 is all.
        public int Severities = 7;
        public string Component, Thread, Highlight;
        public long From = long.MinValue, To = long.MaxValue;

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(Text) && Severities == 7 && Component == null && string.IsNullOrEmpty(Thread) && Highlight == null
                    && From == long.MinValue && To == long.MaxValue;
            }
        }
    }

    // What is open and how it is shown. It is saved as a .logvice file and it is what Back and Forward keep.
    public sealed class Session
    {
        public const string Extension = ".logvice";
        public const int FormatVersion = 1;

        public List<SessionSource> Sources = new List<SessionSource>();
        public bool Merged;
        public SessionFilter Filter = new SessionFilter();
        public string Find = "";
        // The selected row: the index into Sources and the line of that file. -1 when nothing is selected.
        public int SelectedSource = -1, SelectedLine;
        public string Saved;

        public static bool IsSessionFile(string path)
        {
            return path != null && path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        }

        public string[] Paths()
        {
            string[] p = new string[Sources.Count];
            for (int i = 0; i < p.Length; i++) p[i] = Sources[i].ToPath();
            return p;
        }

        public static Session ForPaths(string[] paths)
        {
            Session s = new Session();
            foreach (string p in paths) s.Sources.Add(SessionSource.From(p));
            s.Merged = paths.Length > 1;
            return s;
        }

        static string Iso(long ticks)
        {
            return new DateTime(ticks).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        }

        static bool ReadIso(string s, out long ticks)
        {
            ticks = 0;
            DateTime d;
            if (string.IsNullOrEmpty(s) || !DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out d)) return false;
            ticks = d.Ticks;
            return true;
        }

        public string ToJson()
        {
            JsonWriter w = new JsonWriter();
            w.Begin(null);
            w.Num("logvice", FormatVersion);
            w.BeginArray("sources");
            foreach (SessionSource s in Sources)
            {
                w.Begin(null);
                if (s.Computer != null) w.Str("computer", s.Computer);
                if (s.Channel != null) w.Str("channel", s.Channel);
                else w.Str("path", s.Path);
                w.End();
            }
            w.End();
            w.Bool("merged", Merged);
            w.Begin("filter");
            w.Str("text", Filter.Text ?? "");
            w.Bool("regex", Filter.Regex);
            w.Bool("exclude", Filter.Exclude);
            w.BeginArray("severities");
            if ((Filter.Severities & 4) != 0) w.Str(null, "error");
            if ((Filter.Severities & 2) != 0) w.Str(null, "warning");
            if ((Filter.Severities & 1) != 0) w.Str(null, "info");
            w.End();
            w.Str("component", Filter.Component);
            w.Str("thread", string.IsNullOrEmpty(Filter.Thread) ? null : Filter.Thread);
            w.Str("from", Filter.From == long.MinValue ? null : Iso(Filter.From));
            w.Str("to", Filter.To == long.MaxValue ? null : Iso(Filter.To));
            w.Str("highlight", Filter.Highlight);
            w.End();
            w.Str("find", Find ?? "");
            w.Begin("selected");
            w.Num("source", SelectedSource);
            w.Num("line", SelectedLine);
            w.End();
            w.Str("saved", Saved ?? DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
            w.End();
            return w.ToString() + "\n";
        }

        public void Save(string path)
        {
            File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
        }

        public static Session Load(string path)
        {
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        // Reads a session. Throws FormatException with a sentence for the user when the text is not a session.
        public static Session Parse(string text)
        {
            bool complete;
            Dictionary<string, object> root = Json.Parse(text ?? "", out complete) as Dictionary<string, object>;
            if (root == null || !complete) throw new FormatException("This is not a LogVice session file.");
            long? ver = Json.Long(Json.Get(root, "logvice"));
            if (!ver.HasValue) throw new FormatException("This is not a LogVice session file.");
            Session s = new Session();
            List<object> src = Json.Get(root, "sources") as List<object>;
            if (src != null)
            {
                foreach (object o in src)
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    SessionSource ss = new SessionSource();
                    ss.Computer = NullIfEmpty(Json.Text(Json.Get(d, "computer")));
                    ss.Channel = NullIfEmpty(Json.Text(Json.Get(d, "channel")));
                    ss.Path = NullIfEmpty(Json.Text(Json.Get(d, "path")));
                    if (ss.Channel == null && ss.Path == null) continue;
                    s.Sources.Add(ss);
                }
            }
            object merged = Json.Get(root, "merged");
            s.Merged = merged is bool ? (bool)merged : s.Sources.Count > 1;
            Dictionary<string, object> f = Json.Get(root, "filter") as Dictionary<string, object>;
            if (f != null)
            {
                s.Filter.Text = Json.Text(Json.Get(f, "text")) ?? "";
                s.Filter.Regex = Json.Get(f, "regex") is bool && (bool)Json.Get(f, "regex");
                s.Filter.Exclude = Json.Get(f, "exclude") is bool && (bool)Json.Get(f, "exclude");
                List<object> sev = Json.Get(f, "severities") as List<object>;
                if (sev != null && sev.Count > 0)
                {
                    int bits = 0;
                    foreach (object o in sev)
                    {
                        string n = (Json.Text(o) ?? "").ToLowerInvariant();
                        if (n == "error" || n == "errors") bits |= 4;
                        else if (n == "warning" || n == "warnings") bits |= 2;
                        else if (n == "info" || n == "information") bits |= 1;
                    }
                    if (bits != 0) s.Filter.Severities = bits;
                }
                s.Filter.Component = NullIfEmpty(Json.Text(Json.Get(f, "component")));
                s.Filter.Thread = NullIfEmpty(Json.Text(Json.Get(f, "thread")));
                s.Filter.Highlight = NullIfEmpty(Json.Text(Json.Get(f, "highlight")));
                long t;
                if (ReadIso(Json.Text(Json.Get(f, "from")), out t)) s.Filter.From = t;
                if (ReadIso(Json.Text(Json.Get(f, "to")), out t)) s.Filter.To = t;
            }
            s.Find = Json.Text(Json.Get(root, "find")) ?? "";
            Dictionary<string, object> sel = Json.Get(root, "selected") as Dictionary<string, object>;
            if (sel != null)
            {
                long? a = Json.Long(Json.Get(sel, "source"));
                long? b = Json.Long(Json.Get(sel, "line"));
                if (a.HasValue && a.Value >= 0 && a.Value < s.Sources.Count && b.HasValue && b.Value > 0)
                {
                    s.SelectedSource = (int)a.Value;
                    s.SelectedLine = (int)Math.Min(b.Value, int.MaxValue);
                }
            }
            s.Saved = NullIfEmpty(Json.Text(Json.Get(root, "saved")));
            return s;
        }

        static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        // The sources that are not there and the paths of the others, in order. Event logs are always counted as there.
        public void Split(out List<string> found, out List<string> missing)
        {
            found = new List<string>();
            missing = new List<string>();
            foreach (SessionSource s in Sources)
            {
                string p = s.ToPath();
                if (EventLogs.Available(p)) found.Add(p);
                else missing.Add(p);
            }
        }
    }

    // Back and Forward: the states the open logs had before each change. Newest last. At most Max are kept.
    public sealed class SessionHistory
    {
        public const int Max = 30;
        readonly List<Session> back = new List<Session>();
        readonly List<Session> forward = new List<Session>();

        public bool CanBack
        {
            get { return back.Count > 0; }
        }

        public bool CanForward
        {
            get { return forward.Count > 0; }
        }

        public int BackCount
        {
            get { return back.Count; }
        }

        public int ForwardCount
        {
            get { return forward.Count; }
        }

        // The logs are about to change: keep how they are now. What was ahead is dropped.
        public void Push(Session before)
        {
            if (before == null || before.Sources.Count == 0) return;
            back.Add(before);
            while (back.Count > Max) back.RemoveAt(0);
            forward.Clear();
        }

        // The state to go back to, or null. current is how things are now, so Forward can return to it.
        public Session Back(Session current)
        {
            if (back.Count == 0) return null;
            Session s = back[back.Count - 1];
            back.RemoveAt(back.Count - 1);
            if (current != null && current.Sources.Count > 0)
            {
                forward.Add(current);
                while (forward.Count > Max) forward.RemoveAt(0);
            }
            return s;
        }

        public Session Forward(Session current)
        {
            if (forward.Count == 0) return null;
            Session s = forward[forward.Count - 1];
            forward.RemoveAt(forward.Count - 1);
            if (current != null && current.Sources.Count > 0)
            {
                back.Add(current);
                while (back.Count > Max) back.RemoveAt(0);
            }
            return s;
        }

        public void Clear()
        {
            back.Clear();
            forward.Clear();
        }
    }
}
