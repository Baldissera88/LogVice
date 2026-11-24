namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;

    // The mouse back and forward buttons go Back and Forward in the main window, wherever the pointer is in it.
    sealed class XButtonFilter : IMessageFilter
    {
        const int WmXButtonUp = 0x020C;
        readonly Form form;
        readonly Action<bool> go;

        public XButtonFilter(Form form, Action<bool> go)
        {
            this.form = form;
            this.go = go;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmXButtonUp) return false;
            Control c = Control.FromHandle(m.HWnd);
            Form owner = c == null ? null : c as Form ?? c.FindForm();
            if (owner != form) return false;
            int button = (int)(((long)m.WParam >> 16) & 0xFFFF);
            if (button != 1 && button != 2) return false;
            go(button == 2);
            return true;
        }
    }

    // Where to land and how to show the logs that are about to open: from a saved session or from the command line.
    public sealed class Landing
    {
        public Session State;
        public List<string> Missing = new List<string>();
        public int Line;
        public long Time = Store.NoTime;
        public string Find;
        public string Filter;
        public bool Errors;
        // Set when the options were already used up, so the note is only said once.
        public string Note;

        public static Landing From(StartOptions o)
        {
            if (o == null) return null;
            if (o.Line == 0 && o.Time == Store.NoTime && o.Find == null && o.Filter == null && !o.Errors && o.Ignored.Count == 0) return null;
            Landing l = new Landing();
            l.Line = o.Line;
            l.Time = o.Time;
            l.Find = o.Find;
            l.Filter = o.Filter;
            l.Errors = o.Errors;
            l.Note = o.IgnoredNote();
            return l;
        }
    }

    // Saved sessions, Back and Forward, Copy for ticket and landing on the right row.
    public sealed partial class MainForm
    {
        readonly SessionHistory history = new SessionHistory();
        // navigating: the logs are being shown again from the history, so the change is not added to it.
        bool navigating, skipHistory;
        Action afterView;
        Landing landing;
        string sessionRecent, sessionLoad;
        FlatButton bSave;
        FluentButton aTicket;
        XButtonFilter xbuttons;

        void InitSessions()
        {
            xbuttons = new XButtonFilter(this, delegate (bool forward) { Safe.Run(delegate { NavGo(forward); }); });
            Application.AddMessageFilter(xbuttons);
            FormClosed += delegate { Application.RemoveMessageFilter(xbuttons); };
        }

        // ---------- the state of the window as a session ----------

        Session Snapshot()
        {
            Session s = Session.ForPaths(OpenPaths());
            SessionFilter f = s.Filter;
            f.Text = tFilter.Text;
            f.Regex = bRegex.Checked;
            f.Exclude = bExcl.Checked;
            f.Severities = types;
            f.Component = compName;
            f.Thread = threadF >= 0 ? tThread.Text.Trim() : null;
            f.From = fromK;
            f.To = toK;
            f.Highlight = hlFilter < 0 ? null : hlFilter == 0 ? "*" : hlFilter - 1 < markRules.Length ? markRules[hlFilter - 1].Name : null;
            s.Find = tFind.Text;
            int p = CurPos();
            if (p >= 0 && !loading)
            {
                int i = view[p];
                if (st.Line[i] > 0 && st.Src[i] < st.Sources.Count)
                {
                    s.SelectedSource = st.Src[i];
                    s.SelectedLine = st.Line[i];
                }
            }
            return s;
        }

        // Puts a saved filter into the filter row. Called while the window is reset for new logs.
        void ApplyFilterState(SessionFilter f, bool errorsOnly, string filterText)
        {
            suppress = true;
            try
            {
                bRegex.Checked = f.Regex;
                bExcl.Checked = f.Exclude;
                tFilter.Text = filterText ?? f.Text ?? "";
                if (filterText != null) { bRegex.Checked = false; bExcl.Checked = false; }
                bool bad;
                filterRx = MakeRx(tFilter.Text, out bad);
                tFilter.Invalid = bad;
                tFilter.Badge = "";
                if (bad) filterRx = null;
                types = errorsOnly ? 6 : (f.Severities & 7) == 0 ? 7 : f.Severities & 7;
                SyncChips();
                compName = f.Component;
                tThread.Text = f.Thread ?? "";
                tThread.Invalid = false;
                threadF = -1;
                if (!string.IsNullOrEmpty(f.Thread))
                {
                    string t = f.Thread.Trim();
                    int v;
                    if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.TryParse(t.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) : int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out v)) threadF = v;
                }
                SetTimeLimit(true, f.From);
                SetTimeLimit(false, f.To);
                hlFilter = -1;
                if (!string.IsNullOrEmpty(f.Highlight))
                {
                    if (f.Highlight == "*") hlFilter = 0;
                    else
                    {
                        int k = 0;
                        foreach (HighlightRule r in rules)
                        {
                            if (!r.On || r.Rx == null) continue;
                            k++;
                            if (string.Equals(r.Name, f.Highlight, StringComparison.OrdinalIgnoreCase)) { hlFilter = k; break; }
                        }
                    }
                }
            }
            finally { suppress = false; }
            UpdateCompButton();
            UpdateHlButton();
            filterBar.PerformLayout();
        }

        // A time limit of the filter: the From and Until buttons of the filter row.
        void SetTimeLimit(bool from, long ticks)
        {
            bool none = ticks == (from ? long.MinValue : long.MaxValue);
            if (from) fromK = ticks; else toK = ticks;
            FluentButton b = from ? bFrom : bTo;
            b.Visible = !none;
            if (none) return;
            string label = Fmt.Time(ticks);
            label = label.Length >= 12 ? label.Substring(11) : label;
            b.Text = (from ? "From " : "Until ") + label;
            b.Checked = false;
        }

        // ---------- open, save ----------

        // Files, event logs or one saved session.
        void OpenAny(string[] paths, bool merge)
        {
            if (paths != null)
                foreach (string p in paths)
                    if (Session.IsSessionFile(p)) { OpenSession(p, null); return; }
            OpenFiles(paths, merge, false);
        }

        void PickSession()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Open a session";
                d.Filter = "LogVice sessions (*.logvice)|*.logvice|All files (*.*)|*.*";
                string dir = StartDir();
                if (dir != null) d.InitialDirectory = dir;
                if (d.ShowDialog(this) == DialogResult.OK) OpenSession(d.FileName, null);
            }
        }

        void OpenSession(string path, Landing extra)
        {
            Session s;
            try { s = Session.Load(path); }
            catch (Exception ex)
            {
                Problem("The session " + Path.GetFileName(path) + " could not be opened. " + ex.Message);
                return;
            }
            ApplySession(s, path, extra);
        }

        void Problem(string text)
        {
            if (Program.Options.Shot != null) Note(text);
            else MessageBox.Show(this, text, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Opens the logs of a session that are there, and says which are missing.
        void ApplySession(Session s, string recentPath, Landing extra)
        {
            List<string> found, missing;
            s.Split(out found, out missing);
            if (found.Count == 0)
            {
                Problem(s.Sources.Count == 0 ? "This session has no logs." : "None of the logs of this session could be found.");
                return;
            }
            Landing l = extra ?? new Landing();
            l.State = s;
            l.Missing = missing;
            sessionRecent = recentPath;
            landing = l;
            OpenFiles(found.ToArray(), false, false);
        }

        void SaveSession()
        {
            if (st.Sources.Count == 0 || loading) { Note("Open some logs first, then save them as a session."); return; }
            string first = st.Sources[0].Name;
            if (st.Sources.Count > 1) first = Path.GetFileNameWithoutExtension(first) + " and " + (st.Sources.Count - 1) + " more";
            else first = Path.GetFileNameWithoutExtension(first);
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "Save a session";
                d.Filter = "LogVice sessions (*.logvice)|*.logvice";
                d.DefaultExt = "logvice";
                d.AddExtension = true;
                d.FileName = first + Session.Extension;
                try
                {
                    string dir = lastDir != null && Directory.Exists(lastDir) ? lastDir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (!string.IsNullOrEmpty(dir)) d.InitialDirectory = dir;
                }
                catch (Exception) { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                SaveSessionTo(d.FileName);
            }
        }

        void SaveSessionTo(string path)
        {
            try
            {
                Snapshot().Save(path);
                Settings.AddRecent(new string[] { path });
                RefreshRecent();
                Note("Saved the session to " + path);
            }
            catch (Exception ex)
            {
                Problem("The session could not be saved. " + ex.Message);
            }
        }

        // ---------- Back and Forward ----------

        // The open logs are about to change: keep how they are now.
        void RecordHistory()
        {
            if (navigating || st.Sources.Count == 0) return;
            history.Push(Snapshot());
            UpdateNav();
        }

        void UpdateNav()
        {
            if (bBack == null) return;
            bBack.Enabled = history.CanBack;
            bForward.Enabled = history.CanForward;
            bBack.Invalidate();
            bForward.Invalidate();
        }

        void NavGo(bool forward)
        {
            if (viewKind != ViewKind.Logs) ShowView(ViewKind.Logs);
            Session now = st.Sources.Count > 0 ? Snapshot() : null;
            Session target = forward ? history.Forward(now) : history.Back(now);
            UpdateNav();
            if (target == null) return;
            navigating = true;
            try { ApplySession(target, null, null); }
            finally { navigating = false; }
        }

        // ---------- landing on the right row ----------

        // Called once the list of a new set of logs is built.
        void Landed(Landing l)
        {
            List<string> notes = new List<string>();
            if (l.State != null && l.State.SelectedSource >= 0 && l.Line == 0 && l.Time == Store.NoTime)
            {
                int src = SourceIndex(l.State.Sources[l.State.SelectedSource].ToPath());
                if (src >= 0) SelectLine(src, l.State.SelectedLine);
            }
            if (l.Line > 0)
            {
                int r = SelectLine(0, l.Line);
                if (r == 0) notes.Add("Line " + l.Line.ToString("N0", CultureInfo.CurrentCulture) + " is past the end of " + st.Sources[0].Name + " or hidden by filters.");
                else if (r == 2) notes.Add("Line " + l.Line.ToString("N0", CultureInfo.CurrentCulture) + " has no entry of its own. Showing the next one.");
            }
            else if (l.Time != Store.NoTime)
            {
                if (!SelectTime(l.Time)) notes.Add("No row at or after " + Fmt.Time(l.Time) + ". Showing the last one.");
            }
            string find = l.Find ?? (l.State != null ? l.State.Find : null);
            if (!string.IsNullOrEmpty(find))
            {
                suppress = true;
                tFind.Text = find;
                suppress = false;
                ApplyFindText();
                // A search that comes from a saved session stays where the session was.
                if (l.Find == null) jumpOnFind = false;
            }
            if (l.Missing != null && l.Missing.Count > 0)
            {
                List<string> names = new List<string>();
                for (int i = 0; i < l.Missing.Count && i < 3; i++) names.Add(EventLogs.DisplayName(l.Missing[i]));
                string more = l.Missing.Count > 3 ? " and " + (l.Missing.Count - 3) + " more" : "";
                notes.Insert(0, (l.Missing.Count == 1 ? "1 log of the session was not found: " : l.Missing.Count + " logs of the session were not found: ") + string.Join(", ", names.ToArray()) + more + ".");
            }
            if (l.Note != null) notes.Add(l.Note);
            if (notes.Count > 0) Note(string.Join(" ", notes.ToArray()));
        }

        int SourceIndex(string path)
        {
            for (int k = 0; k < st.Sources.Count; k++)
            {
                if (string.Equals(st.Sources[k].Path, path, StringComparison.OrdinalIgnoreCase)) return k;
                try { if (!EventLogs.IsChannel(path) && string.Equals(st.Sources[k].Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) return k; } catch (Exception) { }
            }
            return -1;
        }

        // Selects the row of line want of a file, or the next row after it. 0: not found, 1: that row, 2: the next row.
        int SelectLine(int src, int want)
        {
            int best = -1, bestLine = int.MaxValue;
            for (int p = 0; p < viewCount; p++)
            {
                int i = view[p];
                if (st.Src[i] != src) continue;
                int ln = st.Line[i];
                if (ln == want) { best = p; break; }
                if (ln > want && ln < bestLine) { best = p; bestLine = ln; }
            }
            if (best < 0) return 0;
            SelectPos(best, 1);
            return st.Line[view[best]] == want ? 1 : 2;
        }

        // Selects the first row at or after the time. When there is none, the last row.
        bool SelectTime(long t)
        {
            for (int p = 0; p < viewCount; p++)
            {
                long rt = st.Time[view[p]];
                if (rt != Store.NoTime && rt >= t) { SelectPos(p, 1); return true; }
            }
            if (viewCount > 0) SelectPos(viewCount - 1, 2);
            return false;
        }

        // ---------- Copy for ticket ----------

        // The text for a ticket from the selected rows, or null when no row is selected.
        string TicketText(out int rows, out int codes)
        {
            rows = codes = 0;
            if (viewCount == 0 || grid.Cur < 0 || st.Sources.Count == 0) return null;
            int first, last, marked, omitted;
            Ticket.Range(grid.SelLo, grid.SelHi, viewCount, out first, out last, out marked, out omitted);
            if (last < first) return null;
            int[] r = new int[last - first + 1];
            for (int k = 0; k < r.Length; k++) r[k] = view[first + k];
            TicketInput input = new TicketInput();
            input.Store = st;
            input.Rows = r;
            input.Marked = marked >= 0 ? marked - first : -1;
            input.Omitted = omitted;
            input.Computer = demoMode ? Demo.Computer : Ticket.ComputerFor(OpenPaths(), Environment.MachineName);
            input.Now = DateTime.Now;
            input.Version = Program.Version;
            input.State = Snapshot();
            string text = Ticket.Build(input);
            rows = r.Length;
            List<uint> all = new List<uint>();
            foreach (int i in r) foreach (uint u in Codes.Find(st.Msg[i])) if (!all.Contains(u)) all.Add(u);
            codes = all.Count;
            return text;
        }

        void CopyTicket()
        {
            int rows, codes;
            string text = TicketText(out rows, out codes);
            if (text == null) { Note("Select a row first, then copy it for a ticket."); return; }
            try
            {
                Clipboard.SetText(text);
                Note(rows == 1 ? "Copied 1 row for a ticket." : "Copied " + rows.ToString("N0") + " rows for a ticket.");
            }
            catch (Exception) { Note("The clipboard is busy. Try again."); }
        }

        void PreviewTicket()
        {
            int rows, codes;
            string text = TicketText(out rows, out codes);
            if (text == null) { Note("Select a row first, then preview the text for a ticket."); return; }
            using (TicketForm f = new TicketForm(theme, text, rows, codes)) f.ShowDialog(this);
        }

        Form TicketPage()
        {
            int rows, codes;
            string text = TicketText(out rows, out codes);
            return text == null ? null : new TicketForm(theme, text, rows, codes);
        }

        // ---------- menus ----------

        void FillSave(ToolStrip m)
        {
            bool has = st.Sources.Count > 0 && !loading;
            Menus.Add(m, "Save session\u2026", "Ctrl+S", delegate { SaveSession(); }).Enabled = has;
            Menus.Separator(m);
            Menus.Add(m, "Copy for ticket", "Ctrl+Shift+C", delegate { CopyTicket(); }).Enabled = has && grid.Cur >= 0;
            Menus.Add(m, "Preview the text for a ticket\u2026", delegate { PreviewTicket(); }).Enabled = has && grid.Cur >= 0;
        }

        ContextMenuStrip SaveMenu()
        {
            ContextMenuStrip m = Menus.New(theme);
            FillSave(m);
            return m;
        }
    }
}
