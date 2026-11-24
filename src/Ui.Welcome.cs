namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    // A large choice on the start page: a glyph, a title and one or two plain sentences.
    public sealed class WelcomeCard : FlatControl
    {
        public Glyph Glyph;
        public string Title, Desc;

        public WelcomeCard(Glyph glyph, string title, string desc)
        {
            Glyph = glyph;
            Title = title;
            Desc = desc;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        int TextX
        {
            get { return Dpi.S(64); }
        }

        public int Measure(int width)
        {
            int tw = Math.Max(Dpi.S(80), width - TextX - Dpi.S(20));
            return Math.Max(Dpi.S(96), Dpi.S(20) + Fonts.UiSemibold.Height + Dpi.S(6) + Draw.WrapHeight(Desc, Fonts.Ui, tw) + Dpi.S(20));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            StartPage sp = Parent as StartPage;
            if (sp != null) sp.ScrollBy(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, r, Dpi.F(8), down ? Theme.Over(T.Card, T.Pressed) : hover ? Theme.Mix(T.Card, T.Accent, T.IsDark ? 0.08f : 0.05f) : T.Card);
            Draw.Stroke(g, r, Dpi.F(8), hover || (Focused && ShowFocusCues) ? T.Accent : T.Divider, 1f);
            int s = Dpi.S(26);
            Draw.Icon(g, Glyph, new RectangleF(Dpi.S(20), Dpi.S(20), s, s), T.Accent);
            int x = TextX, tw = Math.Max(0, Width - x - Dpi.S(20));
            int y = Dpi.S(20);
            Draw.Text(g, Title, Fonts.UiSemibold, new Rectangle(x, y, tw, Fonts.UiSemibold.Height + 2), T.Text, Draw.Line);
            y += Fonts.UiSemibold.Height + Dpi.S(6);
            Draw.Text(g, Desc, Fonts.Ui, new Rectangle(x, y, tw, Math.Max(0, Height - y)), T.Text2, Draw.Wrap);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    // The heading of the start page: the LogVice mark, a title and a line under it.
    public sealed class WelcomeHead : FlatControl
    {
        public string Title, Desc;

        public WelcomeHead(string title, string desc)
        {
            Title = title;
            Desc = desc;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
        }

        int TextX
        {
            get { return Dpi.S(76); }
        }

        public int Measure(int width)
        {
            return Math.Max(Dpi.S(64), Fonts.Title.Height + Dpi.S(6) + Draw.WrapHeight(Desc, Fonts.Ui, Math.Max(Dpi.S(80), width - TextX)) + Dpi.S(4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            Draw.AppMark(g, new RectangleF(0, 0, Dpi.S(60), Dpi.S(60)), T);
            int x = TextX, tw = Math.Max(0, Width - x);
            Draw.Text(g, Title, Fonts.Title, new Rectangle(x, 0, tw, Fonts.Title.Height + 2), T.Text, Draw.Line);
            int y = Fonts.Title.Height + Dpi.S(6);
            Draw.Text(g, Desc, Fonts.Ui, new Rectangle(x, y, tw, Math.Max(0, Height - y)), T.Text2, Draw.Wrap);
        }
    }

    // One recent entry on the start page: click to open, the pin at the right, a menu on right click.
    public sealed class RecentRow : FluentControl
    {
        public readonly RecentEntry Entry;
        readonly string name, detail;
        readonly bool exists;
        bool pinHot;
        public event EventHandler PinClicked, MenuRequested;

        public RecentRow(RecentEntry entry)
        {
            Entry = entry;
            name = entry.Label;
            detail = entry.Detail;
            exists = true;
            foreach (string p in entry.Paths)
                if (!EventLogs.Available(p)) exists = false;
            Height = Dpi.S(38);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        Rectangle PinRect()
        {
            int s = Dpi.S(28);
            return new Rectangle(Width - Dpi.S(8) - s, (Height - s) / 2, s, s);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            StartPage sp = Parent as StartPage;
            if (sp != null) sp.ScrollBy(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool h = PinRect().Contains(e.Location);
            if (h != pinHot) { pinHot = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            pinHot = false;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right && MenuRequested != null) MenuRequested(this, EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e)
        {
            Point p = PointToClient(MousePosition);
            if (MouseButtons == MouseButtons.None && PinRect().Contains(p) && hot)
            {
                if (PinClicked != null) PinClicked(this, EventArgs.Empty);
                return;
            }
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            if (hot || pressed) Draw.Fill(g, r, Dpi.F(5), Theme.Over(surface, pressed ? theme.Pressed : theme.Hover));
            int s = Dpi.S(16);
            Draw.Icon(g, EventLogs.IsChannel(Entry.Paths[0]) ? Glyph.Events : Glyph.Recent, new RectangleF(Dpi.S(12), (Height - s) / 2f, s, s), theme.Text3);
            int x = Dpi.S(12) + s + Dpi.S(12);
            int right = Width - Dpi.S(44);
            int nw = Math.Min(Draw.Measure(name, Fonts.Ui).Width + 2, Math.Max(0, (right - x) * 2 / 3));
            Draw.Text(g, name, Fonts.Ui, new Rectangle(x, 0, nw, Height), exists ? theme.Text : theme.Text3, Draw.Line);
            x += nw + Dpi.S(12);
            Draw.Text(g, exists ? detail : "Not found \u00b7 " + detail, Fonts.Small, new Rectangle(x, 0, Math.Max(0, right - x), Height), theme.Text3, Draw.Line | TextFormatFlags.PathEllipsis);
            if (Entry.Pinned || hot)
            {
                Rectangle pr = PinRect();
                if (pinHot) Draw.Fill(g, pr, Dpi.F(5), Theme.Over(surface, theme.Hover));
                int ps = Dpi.S(14);
                Draw.Icon(g, Glyph.Pin, new RectangleF(pr.X + (pr.Width - ps) / 2f, pr.Y + (pr.Height - ps) / 2f, ps, ps), Entry.Pinned ? theme.Accent : theme.Text3);
            }
            if (Focused && ShowFocusCues) Draw.Stroke(g, r, Dpi.F(5), theme.Accent, Dpi.F(1.5f));
        }
    }

    // The page shown when nothing is open and for Start: four ways to begin, the recent entries and the folders found here.
    public sealed class StartPage : FluentControl
    {
        public const string HeadTitle = "Welcome to LogVice";
        public const string HeadDesc = "Read ConfigMgr, Intune and Windows logs. They are opened read-only and stay on this computer. How do you want to start?";
        const string Foot = "Reads CMTrace and Intune IME, SMS trace, CBS, DISM and Panther, Windows Update ETL traces, Windows event logs and plain text logs. Drop files anywhere in the window to open them. Hold Ctrl while dropping to merge with what is already open.";
        const int MaxRecent = 8;

        readonly WelcomeHead head = new WelcomeHead(HeadTitle, HeadDesc);
        readonly WelcomeCard cOpen, cBrowse, cFollow, cLook;
        public readonly FlatButton ResumeButton;
        readonly FluentTextBox search;
        readonly List<LocationCard> cards = new List<LocationCard>();
        readonly List<RecentRow> rows = new List<RecentRow>();
        List<RecentEntry> allRecent = new List<RecentEntry>();
        public event Action<string[]> OpenRecent;
        public event Action<RecentEntry> PinToggled, RecentRemoved;
        public event Action<LogLocation> OpenLocation;
        public event EventHandler OpenRequested, BrowseRequested, FollowRequested, DemoRequested, ResumeRequested;
        int recentY, locY, footY, emptyY, contentH, scrollY;
        bool searching = true;
        bool hasResume;

        public StartPage()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            AllowDrop = true;
            cOpen = new WelcomeCard(Glyph.Open, "Open a log", "Choose log files, or drop them in this window. Several files are merged by time.");
            cBrowse = new WelcomeCard(Glyph.Browse, "Browse logs", "ConfigMgr, Intune and Windows logs found on this PC, or on another computer you connect to.");
            cFollow = new WelcomeCard(Glyph.Follow, "Follow an item", "Pick an app, update or package and see every client log line about it in one timeline.");
            cLook = new WelcomeCard(Glyph.Rows, "Look around", "Open made-up ConfigMgr and Intune logs from a made-up laptop. Nothing on this PC is read.");
            cOpen.Click += delegate { if (OpenRequested != null) OpenRequested(this, EventArgs.Empty); };
            cBrowse.Click += delegate { if (BrowseRequested != null) BrowseRequested(this, EventArgs.Empty); };
            cFollow.Click += delegate { if (FollowRequested != null) FollowRequested(this, EventArgs.Empty); };
            cLook.Click += delegate { if (DemoRequested != null) DemoRequested(this, EventArgs.Empty); };
            ResumeButton = new FlatButton("Back to the log", Glyph.Back);
            ResumeButton.Outline = true;
            ResumeButton.Visible = false;
            ResumeButton.Click += delegate { if (ResumeRequested != null) ResumeRequested(this, EventArgs.Empty); };
            search = new FluentTextBox(Glyph.Search, "Search recent files");
            search.Width = Dpi.S(260);
            search.TextChanged += delegate { Refill(); };
            Controls.Add(head);
            Controls.Add(cOpen);
            Controls.Add(cBrowse);
            Controls.Add(cFollow);
            Controls.Add(cLook);
            Controls.Add(ResumeButton);
            Controls.Add(search);
        }

        public IEnumerable<Control> Cards
        {
            get
            {
                yield return cOpen;
                yield return cBrowse;
                yield return cFollow;
                yield return cLook;
                yield return ResumeButton;
            }
        }

        public void SetResume(string label)
        {
            hasResume = !string.IsNullOrEmpty(label);
            if (!hasResume)
            {
                ResumeButton.Visible = false;
            }
            else
            {
                if (label.Length > 44) label = label.Substring(0, 43) + "\u2026";
                ResumeButton.Text = "Back to " + label;
                ResumeButton.Visible = true;
            }
            PerformLayout();
            Invalidate();
        }

        public void ScrollBy(int delta)
        {
            int max = Math.Max(0, contentH - Height);
            int n = Math.Max(0, Math.Min(max, scrollY - delta / 120 * Dpi.S(60)));
            if (n == scrollY) return;
            scrollY = n;
            PerformLayout();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(e.Delta);
            base.OnMouseWheel(e);
        }

        public void SetLocations(List<LogLocation> list)
        {
            SuspendLayout();
            foreach (LocationCard c in cards) { Controls.Remove(c); c.Dispose(); }
            cards.Clear();
            searching = list == null;
            if (list != null)
            {
                int n = 0;
                foreach (LogLocation l in list)
                {
                    if (n++ >= 10) break;
                    LocationCard card = new LocationCard(l);
                    card.ApplyTheme(theme);
                    LogLocation loc = l;
                    card.Click += delegate { if (OpenLocation != null) OpenLocation(loc); };
                    cards.Add(card);
                    Controls.Add(card);
                }
            }
            ResumeLayout(true);
            Invalidate();
        }

        public void SetRecent(List<RecentEntry> list)
        {
            allRecent = RecentList.Ordered(list ?? new List<RecentEntry>());
            Refill();
        }

        void Refill()
        {
            SuspendLayout();
            foreach (RecentRow r in rows) { Controls.Remove(r); r.Dispose(); }
            rows.Clear();
            List<RecentEntry> shown = RecentList.Search(allRecent, search.Text);
            int n = 0;
            foreach (RecentEntry entry in shown)
            {
                if (n++ >= MaxRecent) break;
                RecentRow row = new RecentRow(entry);
                row.ApplyTheme(theme);
                RecentEntry en = entry;
                row.Click += delegate { if (OpenRecent != null) OpenRecent(en.Paths); };
                row.PinClicked += delegate { if (PinToggled != null) PinToggled(en); };
                row.MenuRequested += delegate { ShowRowMenu(en); };
                rows.Add(row);
                Controls.Add(row);
            }
            ResumeLayout(true);
            PerformLayout();
            Invalidate();
        }

        void ShowRowMenu(RecentEntry en)
        {
            ContextMenuStrip m = Menus.New(theme);
            Menus.Add(m, "Open", delegate { if (OpenRecent != null) OpenRecent(en.Paths); });
            Menus.Add(m, en.Pinned ? "Unpin" : "Pin to the top", delegate { if (PinToggled != null) PinToggled(en); });
            Menus.Add(m, "Copy the paths", delegate { Menus.Copy(string.Join("\r\n", en.Paths)); });
            Menus.Separator(m);
            Menus.Add(m, "Remove from the list", delegate { if (RecentRemoved != null) RecentRemoved(en); });
            m.Show(Cursor.Position);
        }

        public override void ApplyTheme(Theme t)
        {
            base.ApplyTheme(t);
            BackColor = t.Bg;
            foreach (Control c in Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
            }
        }

        int ColumnWidth()
        {
            int cw = Math.Min(Dpi.S(900), Width - Dpi.S(64));
            if (cw < Dpi.S(200)) cw = Math.Max(0, Width - Dpi.S(32));
            return cw;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (cOpen == null || Width <= 0) return;
            int cw = ColumnWidth();
            int x0 = (Width - cw) / 2;
            int cols = cw >= Dpi.S(560) ? 2 : 1;
            int gap = Dpi.S(12);
            int colW = (cw - gap * (cols - 1)) / cols;
            int headH = head.Measure(cw);
            WelcomeCard[] wc = { cOpen, cBrowse, cFollow, cLook };
            int cardsH = 0;
            int[] heights = new int[wc.Length];
            for (int i = 0; i < wc.Length; i += cols)
            {
                int h = 0;
                for (int k = i; k < i + cols && k < wc.Length; k++) h = Math.Max(h, wc[k].Measure(colW));
                for (int k = i; k < i + cols && k < wc.Length; k++) heights[k] = h;
                cardsH += h + gap;
            }
            cardsH -= gap;
            int resumeH = hasResume ? ResumeButton.Height + Dpi.S(16) : 0;
            int itemH = Dpi.S(38);
            int labelH = Fonts.SmallSemibold.Height;
            int recentHead = Math.Max(search.Height, labelH) + Dpi.S(8);
            int recentBody = rows.Count > 0 ? rows.Count * itemH : Fonts.Ui.Height + Dpi.S(8);
            int cardRows = (cards.Count + cols - 1) / cols;
            int cardH = Dpi.S(62);
            int locBody = cards.Count > 0 ? cardRows * (cardH + gap) - gap : Fonts.Ui.Height + Dpi.S(8);
            int footH = Draw.WrapHeight(Foot, Fonts.Small, cw);
            int total = headH + Dpi.S(20) + resumeH + cardsH + Dpi.S(32) + recentHead + recentBody + Dpi.S(32) + labelH + Dpi.S(8) + locBody + Dpi.S(32) + footH;
            contentH = total + Dpi.S(56);
            int max = Math.Max(0, contentH - Height);
            if (scrollY > max) scrollY = max;
            int y = Math.Max(Dpi.S(28), (Height - total) / 3) - scrollY;
            head.SetBounds(x0, y, cw, headH);
            y += headH + Dpi.S(20);
            if (hasResume)
            {
                ResumeButton.SetBounds(x0, y, Math.Min(ResumeButton.Width, cw), ResumeButton.Height);
                y += resumeH;
            }
            int cy = y;
            for (int i = 0; i < wc.Length; i++)
            {
                int c = i % cols, r = i / cols;
                int ry = cy;
                for (int k = 0; k < r * cols; k += cols) ry += heights[k] + gap;
                wc[i].SetBounds(x0 + c * (colW + gap), ry, colW, heights[i]);
            }
            y += cardsH + Dpi.S(32);
            recentY = y;
            search.SetBounds(x0 + cw - Math.Min(Dpi.S(260), cw / 2), y - Dpi.S(2), Math.Min(Dpi.S(260), cw / 2), search.Height);
            y += recentHead;
            emptyY = y;
            foreach (RecentRow r in rows)
            {
                r.SetBounds(x0 - Dpi.S(10), y, cw + Dpi.S(20), itemH);
                y += itemH;
            }
            if (rows.Count == 0) y += recentBody;
            y += Dpi.S(32);
            locY = y;
            y += labelH + Dpi.S(8);
            for (int i = 0; i < cards.Count; i++)
            {
                int c = i % cols, r = i / cols;
                cards[i].SetBounds(x0 + c * (colW + gap), y + r * (cardH + gap), colW, cardH);
            }
            y += locBody + Dpi.S(32);
            footY = y;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.Bg);
            int cw = ColumnWidth();
            int x0 = (Width - cw) / 2;
            Draw.Text(g, "Recent", Fonts.SmallSemibold, new Rectangle(x0, recentY, cw / 2, Math.Max(search.Height, Fonts.SmallSemibold.Height) - Dpi.S(4)), theme.Text3, Draw.Line);
            if (rows.Count == 0)
            {
                string none = search.Text.Length > 0 ? "No recent entry matches." : "Nothing opened yet. The logs you open are listed here, and you can pin the ones you come back to.";
                Draw.Text(g, none, Fonts.Ui, new Rectangle(x0, emptyY, cw, Fonts.Ui.Height + 2), theme.Text2, Draw.Line);
            }
            Draw.Text(g, "Found on this computer", Fonts.SmallSemibold, new Rectangle(x0, locY, cw, Fonts.SmallSemibold.Height + 2), theme.Text3, Draw.Line);
            if (cards.Count == 0)
                Draw.Text(g, searching ? "Looking for ConfigMgr, Intune and Windows log folders\u2026" : "No known log folders here. Use Browse to look on another computer, or add your own folder in Settings.", Fonts.Ui,
                    new Rectangle(x0, locY + Fonts.SmallSemibold.Height + Dpi.S(8), cw, Fonts.Ui.Height + 2), theme.Text2, Draw.Line);
            int footH = Draw.WrapHeight(Foot, Fonts.Small, cw);
            Draw.Text(g, Foot, Fonts.Small, new Rectangle(x0, footY, cw, footH + 2), theme.Text3, Draw.Wrap);
            if (contentH > Height)
            {
                float track = Height - Dpi.S(16);
                float len = Math.Max(Dpi.S(28), track * Height / contentH);
                float top = Dpi.S(8) + (track - len) * scrollY / Math.Max(1, contentH - Height);
                Draw.Fill(g, new RectangleF(Width - Dpi.S(6), top, Dpi.S(3), len), Dpi.F(1.5f), theme.Thumb);
            }
        }
    }
}
