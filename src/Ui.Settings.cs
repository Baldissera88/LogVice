namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    public sealed class PrefsSettingStore : ISettingStore
    {
        public string Get(string name) { return Settings.Get(name); }

        public void Set(string name, string value)
        {
            if (value == null) Settings.Remove(name);
            else Settings.Set(name, value);
        }
    }

    // The subjects on the left of Settings. When the window is narrow, only their icons or first letters show.
    public sealed class SubjectList : FlatControl
    {
        readonly SettingPage[] pages;
        readonly Glyph[] glyphs;
        readonly HashSet<string> changed = new HashSet<string>();
        readonly ToolTip tip = new ToolTip();
        string selected;
        int hotIdx = -1;
        bool compact;
        public event EventHandler Picked;

        public SubjectList(SettingPage[] pages, Glyph[] glyphs)
        {
            this.pages = pages;
            this.glyphs = glyphs;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public string Selected
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        public bool Compact
        {
            get { return compact; }
            set { if (compact == value) return; compact = value; Invalidate(); }
        }

        public void SetChanged(ICollection<string> ids)
        {
            changed.Clear();
            foreach (string id in ids) changed.Add(id);
            Invalidate();
        }

        int RowH
        {
            get { return Dpi.S(38); }
        }

        Rectangle RowRect(int i)
        {
            return new Rectangle(Dpi.S(8), Dpi.S(6) + i * RowH, Math.Max(0, Width - Dpi.S(17)), RowH - Dpi.S(4));
        }

        int IndexAt(Point p)
        {
            for (int i = 0; i < pages.Length; i++) if (RowRect(i).Contains(p)) return i;
            return -1;
        }

        int SelectedIndex
        {
            get
            {
                for (int i = 0; i < pages.Length; i++) if (pages[i].Id == selected) return i;
                return -1;
            }
        }

        void Pick(int i)
        {
            if (i < 0 || i >= pages.Length) return;
            selected = pages[i].Id;
            Invalidate();
            if (Picked != null) Picked(this, EventArgs.Empty);
        }

        static string Initials(string title)
        {
            string t = title.Replace("/", " ").Trim();
            string[] w = t.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length >= 2 && char.IsUpper(w[1][0])) return w[0].Substring(0, 1) + w[1].Substring(0, 1);
            return t.Length >= 2 ? t.Substring(0, 2) : t;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            using (Pen p = new Pen(T.Divider)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
            bool icons = true;
            for (int i = 0; i < pages.Length; i++)
            {
                Rectangle r = RowRect(i);
                bool sel = pages[i].Id == selected;
                if (sel)
                {
                    Draw.Fill(g, r, Dpi.F(5), Theme.Mix(T.Bg, T.Accent, T.IsDark ? 0.22f : 0.13f));
                    Draw.Fill(g, new RectangleF(r.X, r.Y + Dpi.S(9), Dpi.S(3), r.Height - Dpi.S(18)), 1.5f, T.Accent);
                }
                else if (i == hotIdx) Draw.Fill(g, r, Dpi.F(5), Theme.Over(T.Bg, T.Hover));
                Color fg = sel ? T.Text : T.Text2;
                Font f = sel ? Fonts.UiSemibold : Fonts.Ui;
                if (compact)
                {
                    if (icons) Draw.Icon(g, glyphs[i], new RectangleF(r.X + (r.Width - Dpi.S(18)) / 2f, r.Y + (r.Height - Dpi.S(18)) / 2f, Dpi.S(18), Dpi.S(18)), sel ? T.Accent : fg);
                    else Draw.Text(g, Initials(pages[i].Title), f, r, sel ? T.Accent : fg, Draw.Line | TextFormatFlags.HorizontalCenter);
                }
                else
                {
                    int x = r.X + Dpi.S(14);
                    if (icons)
                    {
                        Draw.Icon(g, glyphs[i], new RectangleF(x - Dpi.S(2), r.Y + (r.Height - Dpi.S(18)) / 2f, Dpi.S(18), Dpi.S(18)), sel ? T.Accent : fg);
                        x += Dpi.S(28);
                    }
                    Draw.Text(g, pages[i].Title, f, new Rectangle(x, r.Y, r.Right - x - Dpi.S(20), r.Height), fg, Draw.Line);
                }
                if (changed.Contains(pages[i].Id))
                {
                    int d = Dpi.S(6);
                    float cx = compact ? r.Right - Dpi.S(8) - d : r.Right - Dpi.S(10) - d;
                    float cy = compact ? r.Y + Dpi.S(6) : r.Y + (r.Height - d) / 2f;
                    Draw.Fill(g, new RectangleF(cx, cy, d, d), d / 2f, T.Accent);
                }
            }
            int si = SelectedIndex;
            if (Focused && ShowFocusCues && si >= 0) Draw.Stroke(g, RowRect(si), Dpi.F(5), T.Text, 2f);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i != hotIdx)
            {
                hotIdx = i;
                Invalidate();
                tip.SetToolTip(this, compact && i >= 0 ? pages[i].Title : null);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotIdx = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Pick(IndexAt(e.Location));
            base.OnMouseClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k == Keys.Up || k == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int si = SelectedIndex;
            if (e.KeyCode == Keys.Up) { Pick(si <= 0 ? 0 : si - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Pick(Math.Min(pages.Length - 1, si + 1)); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }

    // The top of a Settings page: its subject, what it holds, and Reset.
    sealed class SettingsHeader : Panel, IThemed, IMeasured
    {
        Theme theme = Theme.Light();
        public string Title, Summary, Note;
        // Draws the changed mark before the note, so the note explains it.
        public bool NoteMark;
        public readonly FlatButton Reset = new FlatButton("Reset", Glyph.None);

        public SettingsHeader(string title, string summary)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Title = title;
            Summary = summary;
            Reset.Compact = true;
            Reset.Outline = true;
            Reset.AutoWidth();
            Reset.Width += Dpi.S(8);
            Controls.Add(Reset);
        }

        int TextW(int width)
        {
            return Math.Max(Dpi.S(80), width - (Reset.Visible ? Reset.Width + Dpi.S(16) : 0));
        }

        int MarkW
        {
            get { return NoteMark ? Dpi.S(14) : 0; }
        }

        public int Measure(int width)
        {
            int tw = TextW(width);
            int h = Dpi.S(8) + Fonts.Title.Height + Dpi.S(4) + Draw.WrapHeight(Summary, Fonts.Ui, tw);
            if (!string.IsNullOrEmpty(Note)) h += Dpi.S(6) + Draw.WrapHeight(Note, Fonts.Small, tw - MarkW);
            return h + Dpi.S(8);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Bg;
            Reset.ApplyTheme(t);
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Reset.Location = new Point(Width - Reset.Width, Dpi.S(8));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.Bg);
            int tw = TextW(Width);
            int y = Dpi.S(8);
            Draw.Text(g, Title, Fonts.Title, new Rectangle(Dpi.S(2), y, tw, Fonts.Title.Height + 2), theme.Text, Draw.Line);
            y += Fonts.Title.Height + Dpi.S(4);
            int sh = Draw.WrapHeight(Summary, Fonts.Ui, tw);
            Draw.Text(g, Summary, Fonts.Ui, new Rectangle(Dpi.S(2), y, tw, sh + 2), theme.Text2, Draw.Wrap);
            y += sh;
            if (string.IsNullOrEmpty(Note)) return;
            y += Dpi.S(6);
            if (NoteMark)
            {
                int d = Dpi.S(7);
                Draw.Fill(g, new RectangleF(Dpi.S(3), y + (Fonts.Small.Height - d) / 2f, d, d), d / 2f, theme.Accent);
            }
            int nh = Draw.WrapHeight(Note, Fonts.Small, tw - MarkW);
            Draw.Text(g, Note, Fonts.Small, new Rectangle(Dpi.S(2) + MarkW, y, tw - MarkW, nh + 2), theme.Text3, Draw.Wrap);
        }
    }

    // Settings: a list of subjects on the left, one page on the right, and a search box over all of them.
    public sealed class SettingsDialog : PageForm
    {
        public event EventHandler SettingsChanged, FoldersChanged, RecentCleared, HighlightsChanged, ElevateRequested, BrowseRequested, WhatsNewRequested;
        // Off for screenshots, so the subject shown is not saved.
        public static bool RememberPage = true;

        static readonly Glyph[] PageGlyphs = { Glyph.Gear, Glyph.Rows, Glyph.File, Glyph.Highlight, Glyph.Folder, Glyph.Reload, Glyph.Shield };

        readonly ISettingStore store = new PrefsSettingStore();
        readonly SubjectList subjects;
        readonly Panel head = new Panel();
        readonly TextBox search = new TextBox();
        readonly Dictionary<string, List<Control>> built = new Dictionary<string, List<Control>>();
        readonly Dictionary<string, SettingsHeader> headers = new Dictionary<string, SettingsHeader>();
        // Each setting's row, and the card in the column that holds it.
        readonly Dictionary<string, SettingRow> rows = new Dictionary<string, SettingRow>();
        readonly Dictionary<string, Control> cards = new Dictionary<string, Control>();
        readonly List<Control> results = new List<Control>();
        List<HighlightRule> rules;
        List<LogLocation> locations;
        string current;
        SettingRow lit;
        Control pendingScroll;
        bool quiet;
        readonly ToolTip tips = new ToolTip();

        public SettingsDialog(Theme t, string page, List<LogLocation> found) : base("LogVice settings", t)
        {
            rules = HighlightRule.Load();
            locations = found ?? new List<LogLocation>();
            Size = new Size(Dpi.S(1000), Dpi.S(720));
            MinimumSize = new Size(Dpi.S(560), Dpi.S(420));
            subjects = new SubjectList(SettingsCatalog.Pages, PageGlyphs);
            subjects.Dock = DockStyle.Left;
            subjects.Width = Dpi.S(208);
            subjects.ApplyTheme(t);
            subjects.Picked += Safe.Wrap(delegate
            {
                quiet = true;
                search.Text = "";
                quiet = false;
                ShowPage(subjects.Selected, null);
            });
            head.Dock = DockStyle.Top;
            head.Height = Dpi.S(52);
            search.BorderStyle = BorderStyle.FixedSingle;
            search.Font = Fonts.Ui;
            head.Paint += delegate (object s, PaintEventArgs e)
            {
                Draw.Text(e.Graphics, SearchLabel, Fonts.Ui, new Rectangle(Dpi.S(16), 0, search.Left - Dpi.S(16), head.Height), theme.Text2, Draw.Line);
            };
            search.Location = new Point(Dpi.S(16) + Draw.Measure(SearchLabel, Fonts.Ui).Width + Dpi.S(12), Dpi.S(14));
            search.TextChanged += Safe.Wrap(delegate { if (!quiet) Searched(); });
            search.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && search.Text.Trim().Length > 0)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    List<SettingDef> hits = SettingsCatalog.Search(search.Text);
                    if (hits.Count > 0) Open(hits[0]);
                }
                else if (e.KeyCode == Keys.Down) { e.Handled = true; subjects.Focus(); }
            };
            head.Controls.Add(search);
            HandleCreated += delegate { NativeMethods.Cue(search, "Name or description (Ctrl+F)"); };
            Controls.Add(subjects);
            Controls.Add(head);
            Resize += delegate { Fit(); };
            PaintChrome(t);
            Fit();
            ActiveControl = search;
            string first = SettingsCatalog.Page(page) != null ? SettingsCatalog.Page(page).Id : SettingsCatalog.Remembered(Settings.Get(SettingsCatalog.LastPagePref));
            ShowPage(first, null);
        }

        const string SearchLabel = "Search";

        public string CurrentPage
        {
            get { return current; }
        }

        public List<HighlightRule> Rules
        {
            get { return rules; }
        }

        void PaintChrome(Theme t)
        {
            head.BackColor = t.Bg;
            head.Invalidate();
            search.BackColor = t.Field;
            search.ForeColor = t.Text;
            subjects.ApplyTheme(t);
        }

        void ApplyNewTheme(Theme t)
        {
            Retheme(t);
            PaintChrome(t);
            foreach (KeyValuePair<string, List<Control>> p in built)
                foreach (Control c in p.Value) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            foreach (Control c in results) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            Relayout();
        }

        // Narrow windows keep only the icons or first letters of the subjects.
        void Fit()
        {
            bool narrow = ClientSize.Width < Dpi.S(700);
            subjects.Compact = narrow;
            subjects.Width = narrow ? Dpi.S(60) : Dpi.S(208);
            search.Width = Math.Max(Dpi.S(120), Math.Min(Dpi.S(380), ClientSize.Width - search.Left - Dpi.S(16)));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (pendingScroll != null) { ScrollTo(pendingScroll); pendingScroll = null; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && search.Text.Length > 0)
            {
                search.Text = "";
                return true;
            }
            if (keyData == (Keys.Control | Keys.F))
            {
                search.Focus();
                search.SelectAll();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Oemcomma))
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Changed()
        {
            RefreshMarks();
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        // ---------- pages ----------

        public void ShowPage(string id, string highlight)
        {
            if (SettingsCatalog.Page(id) == null) id = SettingsCatalog.General;
            if (lit != null) { lit.Highlight = false; lit.Invalidate(); lit = null; }
            current = id;
            subjects.Selected = id;
            ClearResults();
            ClearItems();
            foreach (Control c in Built(id)) Add(c);
            RefreshMarks();
            Relayout();
            if (RememberPage) Settings.Set(SettingsCatalog.LastPagePref, id);
            SettingRow row;
            Control card;
            if (highlight != null && rows.TryGetValue(highlight, out row) && cards.TryGetValue(highlight, out card))
            {
                row.Highlight = true;
                row.Invalidate();
                lit = row;
                if (Visible) ScrollTo(card);
                else pendingScroll = card;
            }
        }

        List<Control> Built(string id)
        {
            List<Control> l;
            if (built.TryGetValue(id, out l)) return l;
            SettingPage p = SettingsCatalog.Page(id);
            SettingsHeader h = new SettingsHeader(p.Title, p.Summary);
            string page = id;
            h.Reset.Click += Safe.Wrap(delegate { ResetPage(page); });
            headers[id] = h;
            l = new List<Control>();
            l.Add(h);
            switch (id)
            {
                case SettingsCatalog.General: BuildGeneral(l); break;
                case SettingsCatalog.List: BuildList(l); break;
                case SettingsCatalog.Reading: BuildReading(l); break;
                case SettingsCatalog.Highlights: BuildHighlights(l); break;
                case SettingsCatalog.Folders: BuildFolders(l); break;
                case SettingsCatalog.Updates: BuildUpdates(l); break;
                case SettingsCatalog.Data: BuildData(l); break;
            }
            // Rows that other parts of LogVice make are matched by their title.
            foreach (Control c in l)
            {
                SettingRow r = c as SettingRow;
                if (r == null || rows.ContainsValue(r)) continue;
                foreach (SettingDef d in SettingsCatalog.On(id))
                {
                    if ((d.Row ?? d.Title) != r.Title || rows.ContainsKey(d.Id)) continue;
                    rows[d.Id] = r;
                    cards[d.Id] = c;
                    r.WrapAccessory = true;
                    r.MoreToggled += delegate { Relayout(); };
                    break;
                }
            }
            built[id] = l;
            return l;
        }

        // Registers the row of a setting, with its description from the catalog when the row has none.
        SettingRow Bind(string id, SettingRow row)
        {
            SettingDef d = SettingsCatalog.Find(id);
            if (row.Desc == null) row.Desc = d.Summary;
            if (row.More == null) row.More = d.More;
            row.WrapAccessory = true;
            rows[id] = row;
            cards[id] = row;
            row.MoreToggled += delegate { Relayout(); };
            Watch(row.Accessory);
            return row;
        }

        // The changed marks follow every change of a toggle or a choice.
        void Watch(Control c)
        {
            FluentToggle tg = c as FluentToggle;
            if (tg != null) { tg.Changed += delegate { RefreshMarks(); }; return; }
            Segmented sg = c as Segmented;
            if (sg != null) { sg.Changed += delegate { RefreshMarks(); }; return; }
            ThemePicker tp = c as ThemePicker;
            if (tp != null) { tp.Changed += delegate { RefreshMarks(); }; return; }
        }

        void RefreshMarks()
        {
            foreach (SettingDef d in SettingsCatalog.All)
            {
                SettingRow r;
                if (!rows.TryGetValue(d.Id, out r)) continue;
                bool m = d.Changed(store);
                if (m != r.Marked) { r.Marked = m; r.Invalidate(); }
            }
            List<string> pages = new List<string>();
            foreach (SettingPage p in SettingsCatalog.Pages) if (SettingsCatalog.ChangedOn(p.Id, store).Count > 0) pages.Add(p.Id);
            subjects.SetChanged(pages);
            foreach (KeyValuePair<string, SettingsHeader> kv in headers) UpdateHeader(kv.Key, kv.Value);
        }

        void UpdateHeader(string page, SettingsHeader h)
        {
            bool can = SettingsCatalog.Resettable(page);
            int n = SettingsCatalog.ChangedOn(page, store).Count;
            h.Reset.Visible = can;
            h.Reset.Enabled = n > 0;
            h.NoteMark = n > 0;
            if (!can)
            {
                if (page == SettingsCatalog.Highlights) h.Note = "Rules are not reset here. Remove them one by one.";
                else if (page == SettingsCatalog.Folders) h.Note = "Your folders are not reset here. Remove them one by one.";
                else h.Note = "Nothing here has a default to go back to. Reset all settings is on this page.";
            }
            else if (n > 0) h.Note = "Changed from the default. Reset puts back the defaults of this page.";
            else h.Note = "Every setting here has its default.";
            h.Invalidate();
        }

        void ResetPage(string page)
        {
            List<SettingDef> changed = SettingsCatalog.ChangedOn(page, store);
            if (changed.Count == 0) return;
            List<string> names = new List<string>();
            foreach (SettingDef d in changed) names.Add(d.Title);
            string what = string.Join(", ", names.ToArray());
            if (MessageBox.Show(this, "Put back the default of " + what + "?", "Reset " + SettingsCatalog.Page(page).Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            bool themeChanged = changed.Contains(SettingsCatalog.Find("theme"));
            SettingsCatalog.Reset(page, store);
            Remake(page);
            if (themeChanged) ApplyNewTheme(Theme.Named(Theme.SavedName()));
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        // Makes a page again, so its controls show what is saved now.
        void Remake(string page)
        {
            List<Control> old;
            if (built.TryGetValue(page, out old))
            {
                built.Remove(page);
                headers.Remove(page);
                foreach (SettingDef d in SettingsCatalog.On(page)) { rows.Remove(d.Id); cards.Remove(d.Id); }
            }
            if (current == page) ShowPage(page, null);
            if (old != null) foreach (Control c in old) c.Dispose();
        }

        // ---------- search ----------

        void Searched()
        {
            string text = search.Text.Trim();
            if (text.Length == 0) { ShowPage(current, null); return; }
            if (lit != null) { lit.Highlight = false; lit.Invalidate(); lit = null; }
            subjects.Selected = null;
            ClearResults();
            ClearItems();
            List<SettingDef> hits = SettingsCatalog.Search(text);
            SettingsHeader h = new SettingsHeader("Search", hits.Count == 0
                ? "No setting matches “" + text + "”."
                : hits.Count + (hits.Count == 1 ? " setting matches “" : " settings match “") + text + "”. Enter opens the first.");
            h.Reset.Visible = false;
            results.Add(h);
            foreach (SettingDef d in hits)
            {
                SettingDef one = d;
                SettingPage p = SettingsCatalog.Page(d.Page);
                FlatButton show = SmallButton("Show", delegate { Open(one); });
                SettingRow r = new SettingRow(PageGlyphs[Array.IndexOf(SettingsCatalog.Pages, p)], d.Title, p.Title + ": " + d.Summary, show);
                r.Marked = d.Changed(store);
                results.Add(r);
            }
            foreach (Control c in results) Add(c);
            Relayout();
        }

        void ClearResults()
        {
            if (results.Count == 0) return;
            ClearItems();
            foreach (Control c in results) c.Dispose();
            results.Clear();
        }

        void Open(SettingDef d)
        {
            quiet = true;
            search.Text = "";
            quiet = false;
            ShowPage(d.Page, d.Id);
        }

        // ---------- the pages ----------

        FluentToggle Toggle(string key, bool def, Action<bool> after)
        {
            FluentToggle t = new FluentToggle();
            t.SetSilently(Settings.GetBool(key, def));
            t.Changed += Safe.Wrap(delegate
            {
                Settings.SetBool(key, t.On);
                if (after != null) after(t.On);
                Changed();
            });
            return t;
        }

        void BuildGeneral(List<Control> l)
        {
            ThemePicker pick = new ThemePicker(Palettes.Choices);
            pick.SelectedName = Theme.SavedName();
            pick.Changed += Safe.Wrap(delegate
            {
                Theme.Override = null;
                Settings.Set("Theme", pick.SelectedName);
                ApplyNewTheme(Theme.Named(pick.SelectedName));
                Changed();
            });
            l.Add(Bind("theme", new SettingRow(Glyph.Palette, "Theme", null, pick)));
            l.Add(Bind("reopen", new SettingRow(Glyph.Reload, "Reopen the last logs at start", null, Toggle("ReopenLast", false, null))));
            l.Add(Bind("live", new SettingRow(Glyph.Live, "Start with Live on", null, Toggle("Live", true, null))));
        }

        void BuildList(List<Control> l)
        {
            Segmented segSize = new Segmented("Small", "Medium", "Large");
            segSize.SetSilently(Settings.GetInt("TextSize", 1, 0, 2));
            segSize.Changed += Safe.Wrap(delegate { Settings.Set("TextSize", segSize.Selected.ToString()); Changed(); });
            Segmented segDensity = new Segmented("Compact", "Standard", "Relaxed");
            segDensity.SetSilently(Settings.GetInt("Density", 1, 0, 2));
            segDensity.Changed += Safe.Wrap(delegate { Settings.Set("Density", segDensity.Selected.ToString()); Changed(); });
            l.Add(Bind("textsize", new SettingRow(Glyph.Text, "Log text size", null, segSize)));
            l.Add(Bind("density", new SettingRow(Glyph.Rows, "Row spacing", null, segDensity)));
            l.Add(Bind("tint", new SettingRow(Glyph.Warning, "Tint error and warning rows", null, Toggle("Tint", true, null))));
            l.Add(Bind("linenumbers", new SettingRow(Glyph.Rows, "Show line numbers", null, Toggle("LineNumbers", true, null))));
        }

        void BuildReading(List<Control> l)
        {
            bool merge = Settings.GetBool("AutoMerge", Settings.GetBool("History", true));
            FluentToggle tog = new FluentToggle();
            tog.SetSilently(merge);
            tog.Changed += Safe.Wrap(delegate { Settings.SetBool("AutoMerge", tog.On); Changed(); });
            l.Add(Bind("automerge", new SettingRow(Glyph.Merge, "Auto-merge spanned logs", null, tog)));
            if (Program.IsAdmin)
            {
                SettingRow admin = new SettingRow(Glyph.Shield, "Administrator rights", "LogVice is running as administrator, so it can read protected logs on this computer.", null);
                admin.Value = "On";
                l.Add(Bind("admin", admin));
            }
            else
            {
                FlatButton bAdmin = SmallButton("Restart as administrator", delegate
                {
                    Close();
                    if (ElevateRequested != null) ElevateRequested(this, EventArgs.Empty);
                });
                tips.SetToolTip(bAdmin, "Restart LogVice with a UAC prompt and open the same logs again");
                l.Add(Bind("admin", new SettingRow(Glyph.Shield, "Administrator rights", null, bAdmin)));
            }
            FlatButton bApps = SmallButton("Open Default apps", delegate
            {
                try { Process.Start("ms-settings:defaultapps"); } catch (Exception) { Safe.Say("Could not open Windows settings."); }
            });
            l.Add(Bind("defaultapp", new SettingRow(Glyph.Open, "Default app for .log files", null, bApps)));
        }

        // ---------- Highlights ----------

        void BuildHighlights(List<Control> l)
        {
            FlatButton bAdd = SmallButton("Add rule", delegate { EditRule(-1); });
            bAdd.Primary = true;
            bAdd.Outline = false;
            bAdd.ApplyTheme(theme);
            l.Add(new SectionTitle("Rules"));
            l.Add(Bind("rules", new SettingRow(Glyph.Highlight, "Colour rows with your own rules", null, bAdd)));
            if (rules.Count == 0)
            {
                FlatButton bEx = SmallButton("Add examples", delegate
                {
                    rules.AddRange(HighlightRule.Examples());
                    RulesChanged();
                });
                l.Add(new SettingRow(Glyph.Info, "No rules yet", "Add your own, or start with examples for exit codes, detection, downloads, restarts and success.", bEx));
            }
            for (int i = 0; i < rules.Count; i++)
            {
                HighlightRule r = rules[i];
                int idx = i;
                FluentToggle on = new FluentToggle();
                on.SetSilently(r.On);
                on.Changed += Safe.Wrap(delegate { rules[idx].On = on.On; RulesChanged(); });
                FlatButton up = new FlatButton("", Glyph.Up);
                up.Outline = true;
                up.Enabled = i > 0;
                tips.SetToolTip(up, "Move up. Rules higher in the list win.");
                up.Click += Safe.Wrap(delegate
                {
                    if (idx <= 0) return;
                    HighlightRule t = rules[idx - 1];
                    rules[idx - 1] = rules[idx];
                    rules[idx] = t;
                    RulesChanged();
                });
                FlatButton edit = SmallButton("Edit", delegate { EditRule(idx); });
                FlatButton del = SmallButton("Remove", delegate
                {
                    rules.RemoveAt(idx);
                    RulesChanged();
                });
                SettingRow row = new SettingRow(Glyph.Highlight, r.Name, (r.IsRegex ? "regex  " : "contains  ") + r.Pattern, new ButtonStrip(on, up, edit, del));
                row.Swatch = r.Color;
                row.MonoDesc = true;
                row.Dim = !r.On;
                l.Add(row);
            }
        }

        void EditRule(int idx)
        {
            HighlightRule r = idx >= 0 ? rules[idx].Clone() : new HighlightRule();
            if (idx < 0) r.Color = HighlightRule.Palette[rules.Count % HighlightRule.Palette.Length];
            using (HighlightEditor d = new HighlightEditor(r, theme, idx < 0))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Result == null) return;
                if (idx >= 0) { d.Result.On = rules[idx].On; rules[idx] = d.Result; }
                else rules.Add(d.Result);
            }
            RulesChanged();
        }

        // Opens the editor with a rule made from the selected row, as Add highlight from a row does.
        public void AddRule(HighlightRule seed)
        {
            ShowPage(SettingsCatalog.Highlights, null);
            HighlightRule r = seed;
            r.Color = HighlightRule.Palette[rules.Count % HighlightRule.Palette.Length];
            using (HighlightEditor d = new HighlightEditor(r, theme, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Result == null) return;
                rules.Add(d.Result);
            }
            RulesChanged();
        }

        void RulesChanged()
        {
            HighlightRule.Save(rules);
            if (HighlightsChanged != null) HighlightsChanged(this, EventArgs.Empty);
            MethodInvoker rebuild = delegate { Remake(SettingsCatalog.Highlights); };
            if (IsHandleCreated) BeginInvoke(rebuild); else rebuild();
        }

        // ---------- Folders ----------


        void BuildFolders(List<Control> l)
        {
            FlatButton bBrowse = SmallButton("Browse logs", delegate
            {
                Close();
                if (BrowseRequested != null) BrowseRequested(this, EventArgs.Empty);
            });
            SettingRow found = Bind("found", new SettingRow(Glyph.Browse, "Found on this computer", null, bBrowse));
            l.Add(new SectionTitle("On this computer"));
            l.Add(found);
            AddLocationRows(l, found);
            FlatButton bAdd = SmallButton("Add folder", delegate { AddFolder(); });
            l.Add(new SectionTitle("Your folders"));
            l.Add(Bind("addfolder", new SettingRow(Glyph.Plus, "Add a folder", null, bAdd)));
            foreach (LogLocation loc in locations)
            {
                if (!loc.Custom) continue;
                string path = loc.Path;
                FlatButton bRemove = SmallButton("Remove", delegate { RemoveFolder(path); });
                tips.SetToolTip(bRemove, "Remove this folder from LogVice. The folder itself is not changed.");
                SettingRow row = new SettingRow(Glyph.Folder, loc.Name, loc.Path + (loc.FileCount > 0 ? "  ·  " + loc.Summary : "  ·  no logs found"), bRemove);
                row.MonoDesc = false;
                l.Add(row);
            }
        }

        void AddLocationRows(List<Control> l, SettingRow found)
        {
            int n = 0;
            foreach (LogLocation loc in locations)
            {
                if (loc.Custom) continue;
                n++;
                SettingRow row = new SettingRow(loc.Glyph, loc.Group + "  ·  " + loc.Name, loc.Where, null);
                row.Value = loc.Summary;
                l.Add(row);
            }
            found.Desc = n == 0
                ? "No ConfigMgr, Intune or Windows log folders were found on this computer."
                : n + (n == 1 ? " folder" : " folders") + " with logs.";
        }

        // The folders found on this PC changed, after a folder was added or removed.
        public void SetLocations(List<LogLocation> list)
        {
            locations = list ?? new List<LogLocation>();
            if (current == SettingsCatalog.Folders && search.Text.Trim().Length == 0) Remake(SettingsCatalog.Folders);
            else built.Remove(SettingsCatalog.Folders);
        }

        void AddFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Choose a folder with log files";
                d.ShowNewFolderButton = false;
                if (d.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(d.SelectedPath)) return;
                List<string> list = Settings.GetList("Folders");
                foreach (string s in list) if (string.Equals(s, d.SelectedPath, StringComparison.OrdinalIgnoreCase)) return;
                list.Add(d.SelectedPath);
                Settings.SetList("Folders", list);
                if (FoldersChanged != null) FoldersChanged(this, EventArgs.Empty);
            }
        }

        void RemoveFolder(string path)
        {
            List<string> list = Settings.GetList("Folders");
            list.RemoveAll(delegate (string s) { return string.Equals(s, path, StringComparison.OrdinalIgnoreCase); });
            Settings.SetList("Folders", list);
            if (FoldersChanged != null) FoldersChanged(this, EventArgs.Empty);
        }

        // ---------- Updates ----------

        void BuildUpdates(List<Control> l)
        {
            SettingDef check = SettingsCatalog.Find("updatecheck");
            foreach (Control c in UpdatesUi.SettingRows(theme, delegate { Relayout(); }))
            {
                SettingRow r = c as SettingRow;
                // The row of the policy says why it is off; the toggle gets the short text and More.
                if (r != null && r.Title == check.Title && r.Accessory is FluentToggle)
                {
                    r.Desc = check.Summary;
                    r.More = check.More;
                    rows["updatecheck"] = r;
                    cards["updatecheck"] = r;
                    r.MoreToggled += delegate { Relayout(); };
                    Watch(r.Accessory);
                }
                l.Add(c);
            }
            FlatButton bPage = SmallButton("Open", delegate { UpdatesUi.OpenUrl(Updates.ReleasesUrl); });
            l.Add(Bind("releasepage", new SettingRow(Glyph.Github, "Release page", Updates.ReleasesUrl, bPage)));
            FlatButton bNotes = SmallButton("Show", delegate { if (WhatsNewRequested != null) WhatsNewRequested(this, EventArgs.Empty); });
            l.Add(Bind("whatsnew", new SettingRow(Glyph.Text, "What's new", null, bNotes)));
        }

        // ---------- Data and privacy ----------

        SettingRow recentRow;

        void UpdateRecentRow()
        {
            if (recentRow == null) return;
            int n = Settings.Recent().Count;
            recentRow.Desc = n == 0 ? "No recent files are stored." : n == 1 ? "1 file or merged set is remembered." : n + " files or merged sets are remembered.";
            recentRow.Invalidate();
        }

        void BuildData(List<Control> l)
        {
            FlatButton bClear = SmallButton("Clear", delegate
            {
                Settings.ClearRecent();
                UpdateRecentRow();
                if (RecentCleared != null) RecentCleared(this, EventArgs.Empty);
            });
            recentRow = Bind("recent", new SettingRow(Glyph.Recent, "Recent files", "", bClear));
            UpdateRecentRow();
            SettingRow store = Bind("settingsstore", new SettingRow(Glyph.Gear, "Where settings are stored", null, null));
            store.Desc += " " + Settings.Location + ".";
            FlatButton bReset = SmallButton("Reset", delegate { ResetEverything(); });
            l.Add(new SectionTitle("On this PC"));
            l.Add(recentRow);
            l.Add(store);
            l.Add(Bind("writes", new SettingRow(Glyph.File, "What LogVice writes", null, null)));
            l.Add(new SectionTitle("What leaves this PC"));
            l.Add(Bind("sends", new SettingRow(Glyph.Info, "What LogVice sends", null, null)));
            l.Add(new SectionTitle("Reset"));
            l.Add(Bind("resetall", new SettingRow(Glyph.Reload, "Reset all settings", null, bReset)));
        }

        void ResetEverything()
        {
            if (MessageBox.Show(this, "Reset all settings? Recent files, your own folders and your highlight rules are removed too.", "LogVice", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Settings.ResetAll();
            rules = new List<HighlightRule>();
            Theme.Override = null;
            Theme t = Theme.Chosen();
            built.Clear();
            headers.Clear();
            rows.Clear();
            cards.Clear();
            ApplyNewTheme(t);
            ShowPage(current, null);
            if (HighlightsChanged != null) HighlightsChanged(this, EventArgs.Empty);
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
            if (FoldersChanged != null) FoldersChanged(this, EventArgs.Empty);
            if (RecentCleared != null) RecentCleared(this, EventArgs.Empty);
        }

        // ---------- screenshots ----------

        // --page settings and settings-<subject>. --select is a search, the ID of a setting to open highlighted, or narrow
        // for the smallest window.
        public static bool IsShotPage(string page)
        {
            return page != null && (page == "settings" || page.StartsWith("settings-", StringComparison.Ordinal));
        }

        public static SettingsDialog ForShot(Theme t, string page, string select, List<LogLocation> found)
        {
            RememberPage = false;
            string sub = page.StartsWith("settings-", StringComparison.Ordinal) ? page.Substring(9) : null;
            SettingsDialog d = new SettingsDialog(t, sub ?? SettingsCatalog.General, found);
            SettingDef def = select != null ? SettingsCatalog.Find(select) : null;
            if (select == "narrow") d.Size = d.MinimumSize;
            else if (def != null) d.ShowPage(def.Page, def.Id);
            else if (!string.IsNullOrEmpty(select)) d.Shown += delegate { d.search.Text = select; };
            return d;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }

    public sealed partial class MainForm
    {
        SettingsDialog settingsOpen;

        // Settings is a dialog of its own. The window follows what changes in it while it is open.
        void OpenSettings(string page)
        {
            if (settingsOpen != null && !settingsOpen.IsDisposed) { settingsOpen.Activate(); return; }
            SettingsDialog d = new SettingsDialog(theme, page, localLocations);
            settingsOpen = d;
            d.SettingsChanged += delegate { ApplySettings(); };
            d.FoldersChanged += delegate { Discover(); };
            d.BrowseRequested += delegate { BeginInvoke((MethodInvoker)delegate { OpenBrowse(null); }); };
            d.RecentCleared += delegate { RefreshRecent(); };
            d.ElevateRequested += delegate { BeginInvoke((MethodInvoker)delegate { Elevate(OpenPaths()); }); };
            d.WhatsNewRequested += delegate { ShowAllNotes(); };
            d.HighlightsChanged += delegate
            {
                rules = new List<HighlightRule>(d.Rules);
                if (hlFilter > 0) hlFilter = -1;
                RebuildMarks();
            };
            try { d.ShowDialog(this); }
            finally
            {
                settingsOpen = null;
                d.Dispose();
            }
            ApplySettings();
        }

        // Add highlight from the selected row: Settings opens on the rule editor.
        void OpenSettingsWithRule(HighlightRule seed)
        {
            if (settingsOpen != null && !settingsOpen.IsDisposed) return;
            SettingsDialog d = new SettingsDialog(theme, SettingsCatalog.Highlights, localLocations);
            settingsOpen = d;
            d.SettingsChanged += delegate { ApplySettings(); };
            d.HighlightsChanged += delegate
            {
                rules = new List<HighlightRule>(d.Rules);
                if (hlFilter > 0) hlFilter = -1;
                RebuildMarks();
            };
            d.Shown += delegate { d.BeginInvoke((MethodInvoker)delegate { d.AddRule(seed); }); };
            try { d.ShowDialog(this); }
            finally
            {
                settingsOpen = null;
                d.Dispose();
            }
        }
    }
}
