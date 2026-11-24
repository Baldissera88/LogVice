namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    // The top of the window: the name, a line about what is open, and the buttons. Buttons that do not fit fold into More.
    public sealed class HeaderBar : FluentControl
    {
        public string TitleText = "LogVice";
        string subtitle = "";
        readonly List<FlatButton> buttons = new List<FlatButton>();
        public readonly FlatButton More;
        public readonly List<FlatButton> Folded = new List<FlatButton>();
        bool fitting;

        public HeaderBar()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(64);
            More = new FlatButton("More", Glyph.More);
            More.Name = "More";
            More.Visible = false;
            Controls.Add(More);
        }

        public string Subtitle
        {
            get { return subtitle; }
            set
            {
                value = value ?? "";
                if (subtitle == value) return;
                subtitle = value;
                Invalidate(new Rectangle(0, Dpi.S(34), Width, Height - Dpi.S(34)));
            }
        }

        public void AddButtons(params FlatButton[] list)
        {
            foreach (FlatButton b in list)
            {
                buttons.Add(b);
                Controls.Add(b);
            }
            PerformLayout();
        }

        public override void ApplyTheme(Theme t)
        {
            base.ApplyTheme(t);
            BackColor = t.Bg;
            foreach (FlatButton b in buttons) b.ApplyTheme(t);
            More.ApplyTheme(t);
        }

        int Taken(FlatButton b)
        {
            return b.Width + Dpi.S(2);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Fit();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Fit();
        }

        public void Fit()
        {
            if (fitting || More == null || Width <= 0) return;
            fitting = true;
            try
            {
                List<FlatButton> want = new List<FlatButton>();
                foreach (FlatButton b in buttons) if (b.Wanted) want.Add(b);
                string[] names = new string[want.Count];
                int[] widths = new int[want.Count];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = want[i].Name;
                    widths[i] = Taken(want[i]);
                }
                int titleW = TextRenderer.MeasureText(TitleText, Fonts.Title).Width;
                int room = Width - Dpi.S(20) - titleW - Dpi.S(28) - Dpi.S(12);
                List<string> fold = HeaderFold.Folded(names, widths, Taken(More), room);
                Folded.Clear();
                List<FlatButton> shown = new List<FlatButton>();
                foreach (FlatButton b in buttons)
                {
                    bool f = b.Wanted && fold.Contains(b.Name);
                    if (f) Folded.Add(b);
                    bool v = b.Wanted && !f;
                    if (v) shown.Add(b);
                    b.Visible = v;
                }
                bool more = Folded.Count > 0;
                More.Visible = more;
                // From the right: Settings and Help stay in the corner, More sits before them.
                int x = Width - Dpi.S(12);
                int y = Dpi.S(6);
                for (int i = buttons.Count - 1; i >= 0; i--)
                {
                    FlatButton b = buttons[i];
                    bool v = shown.Contains(b);
                    if (b.Name == HeaderFold.Settings && more)
                    {
                        if (v) { x -= Taken(b); b.Location = new Point(x + Dpi.S(2), y); }
                        x -= Taken(More);
                        More.Location = new Point(x + Dpi.S(2), y);
                        continue;
                    }
                    if (!v) continue;
                    x -= Taken(b);
                    b.Location = new Point(x + Dpi.S(2), y);
                }
                Invalidate();
            }
            finally { fitting = false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.Bg);
            Draw.Text(g, TitleText, Fonts.Title, new Rectangle(Dpi.S(20), Dpi.S(8), Width - Dpi.S(40), Fonts.Title.Height + 2), theme.Text, Draw.Line);
            Draw.Text(g, subtitle, Fonts.Small, new Rectangle(Dpi.S(22), Dpi.S(38), Math.Max(0, Width - Dpi.S(38)), Fonts.Small.Height + Dpi.S(4)), theme.Text2, Draw.Line);
        }
    }

    // The second row: how many entries, errors, warnings and info rows there are, and the find box.
    public sealed class ChipRow : FluentControl
    {
        public readonly StatChip Entries, Errors, Warnings, Info;
        readonly FluentTextBox find;
        readonly FlatButton findPrev, findNext, errPrev, errNext;

        public ChipRow(StatChip entries, StatChip errors, StatChip warnings, StatChip info, FluentTextBox find, FlatButton findPrev, FlatButton findNext, FlatButton errPrev, FlatButton errNext)
        {
            Entries = entries;
            Errors = errors;
            Warnings = warnings;
            Info = info;
            this.find = find;
            this.findPrev = findPrev;
            this.findNext = findNext;
            this.errPrev = errPrev;
            this.errNext = errNext;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(66);
            Controls.AddRange(new Control[] { entries, errors, warnings, info, find, findPrev, findNext, errPrev, errNext });
            foreach (StatChip c in new StatChip[] { entries, errors, warnings, info }) c.SizeChanged += delegate { PerformLayout(); };
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

        // A low window draws the chips as short lines in a shorter row.
        public static int RowHeight(int clientHeight)
        {
            return clientHeight < Dpi.S(740) ? Dpi.S(46) : Dpi.S(66);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (find == null || Width <= 0) return;
            bool slim = Height < Dpi.S(60);
            StatChip[] chips = { Entries, Errors, Warnings, Info };
            foreach (StatChip c in chips)
            {
                if (c.Slim != slim) { c.Slim = slim; }
                c.Fit();
            }
            int pad = Dpi.S(18), gap = Dpi.S(10);
            int btn = findPrev.Width;
            int rightFixed = btn * 4 + Dpi.S(2) * 3 + Dpi.S(10);
            int findMin = Dpi.S(150), findMax = Dpi.S(300);
            int[] widths = new int[chips.Length];
            for (int i = 0; i < chips.Length; i++) widths[i] = chips[i].Width + gap;
            int room = Width - pad * 2 - rightFixed - findMin - Dpi.S(14);
            // The count of all entries and then the info rows go first.
            bool[] hidden = NarrowFit.Hidden(widths, new int[] { 0, 3 }, 0, room);
            int x = pad;
            for (int i = 0; i < chips.Length; i++)
            {
                chips[i].Visible = !hidden[i];
                if (hidden[i]) continue;
                chips[i].Location = new Point(x, (Height - chips[i].Height) / 2);
                x += widths[i];
            }
            int rx = Width - pad;
            Control[] rightEnd = { errNext, errPrev };
            foreach (Control c in rightEnd) { rx -= c.Width; c.Location = new Point(rx, (Height - c.Height) / 2); rx -= Dpi.S(2); }
            rx -= Dpi.S(8);
            Control[] mid = { findNext, findPrev };
            foreach (Control c in mid) { rx -= c.Width; c.Location = new Point(rx, (Height - c.Height) / 2); rx -= Dpi.S(2); }
            int fw = Math.Max(Dpi.S(110), Math.Min(findMax, rx - Dpi.S(8) - x));
            find.SetBounds(rx - Dpi.S(4) - fw, (Height - find.Height) / 2, fw, find.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(theme.Bg);
        }
    }

    // The line under the title: what is open.
    public static class HeaderText
    {
        public static string Subtitle(bool demo, string demoComputer, string[] paths)
        {
            if (paths == null || paths.Length == 0) return "No logs open";
            if (demo) return "Demo logs \u00b7 " + demoComputer;
            string first = paths[0];
            int more = paths.Length - 1;
            string rest = more > 0 ? " + " + more + " more" : "";
            if (EventLogs.IsChannel(first))
            {
                string name = EventLogs.DisplayName(first);
                if (more == 0) return "Event log: " + name;
                return "Event logs: " + name + rest + " \u00b7 merged by time";
            }
            string dir = "";
            try { dir = Demo.Show(System.IO.Path.GetDirectoryName(first) ?? ""); } catch (Exception) { }
            string text = System.IO.Path.GetFileName(first) + rest;
            if (more > 0) text += " \u00b7 merged by time";
            if (dir.Length > 0) text += " \u00b7 " + dir;
            return text;
        }
    }
}
