namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text;
    using System.Windows.Forms;

    public sealed class FluentToggle : FluentControl
    {
        bool on;
        public event EventHandler Changed;

        public FluentToggle()
        {
            TabStop = true;
            Size = new Size(Dpi.S(96), Dpi.S(32));
        }

        public bool On
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        public void SetSilently(bool v)
        {
            on = v;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            On = !On;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { e.Handled = true; On = !On; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            int w = Dpi.S(40), h = Dpi.S(20);
            RectangleF track = new RectangleF(Width - w - Dpi.S(2), (Height - h) / 2f, w, h);
            string text = on ? "On" : "Off";
            int tw = Draw.Measure(text, Fonts.Ui).Width + 2;
            Draw.Text(g, text, Fonts.Ui, new Rectangle((int)track.X - Dpi.S(12) - tw, 0, tw, Height), Enabled ? theme.Text : theme.Text3, Draw.Line);
            float d = hot ? Dpi.F(14) : Dpi.F(12);
            if (on)
            {
                Draw.Fill(g, track, h / 2f, Enabled ? (hot ? theme.AccentHover : theme.Accent) : theme.Text3);
                Draw.Fill(g, new RectangleF(track.Right - h / 2f - d / 2f, track.Y + (h - d) / 2f, d, d), d / 2f, theme.OnAccent);
            }
            else
            {
                if (hot) Draw.Fill(g, track, h / 2f, Theme.Over(Surface, theme.Hover));
                Draw.Stroke(g, track, h / 2f, theme.Text2, 1f);
                Draw.Fill(g, new RectangleF(track.X + h / 2f - d / 2f, track.Y + (h - d) / 2f, d, d), d / 2f, theme.Text2);
            }
            if (Focused && ShowFocusCues)
                Draw.Stroke(g, RectangleF.Inflate(track, Dpi.F(3), Dpi.F(3)), h / 2f + Dpi.F(3), theme.Text, Dpi.F(2));
        }
    }

    public sealed class Segmented : FluentControl
    {
        readonly string[] items;
        int sel, hotIdx = -1;
        public event EventHandler Changed;

        public Segmented(params string[] items)
        {
            this.items = items;
            TabStop = true;
            Height = Dpi.S(32);
            int w = Dpi.S(6);
            for (int i = 0; i < items.Length; i++) w += ItemW(i);
            Width = w;
        }

        int ItemW(int i)
        {
            return Draw.Measure(items[i], Fonts.UiSemibold).Width + Dpi.S(22);
        }

        public int Selected
        {
            get { return sel; }
            set
            {
                if (value < 0 || value >= items.Length || value == sel) return;
                sel = value;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        public void SetSilently(int v)
        {
            if (v >= 0 && v < items.Length) sel = v;
            Invalidate();
        }

        int IndexAt(int x)
        {
            int px = Dpi.S(3);
            for (int i = 0; i < items.Length; i++)
            {
                int w = ItemW(i);
                if (x >= px && x < px + w) return i;
                px += w;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.X);
            if (i != hotIdx) { hotIdx = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotIdx = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int i = IndexAt(e.X);
            if (i >= 0 && e.Button == MouseButtons.Left) Selected = i;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k == Keys.Left || k == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Selected = Math.Max(0, sel - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Selected = Math.Min(items.Length - 1, sel + 1); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            RectangleF outer = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, outer, Dpi.F(5), theme.Field);
            Draw.Stroke(g, outer, Dpi.F(5), theme.FieldStroke, 1f);
            int x = Dpi.S(3);
            for (int i = 0; i < items.Length; i++)
            {
                int w = ItemW(i);
                RectangleF r = new RectangleF(x, Dpi.S(3), w, Height - Dpi.S(6));
                if (i == sel)
                {
                    Draw.Fill(g, r, Dpi.F(4), Theme.Mix(theme.Field, theme.Accent, theme.IsDark ? 0.22f : 0.13f));
                    float pw = Dpi.F(16);
                    Draw.Fill(g, new RectangleF(r.X + (w - pw) / 2f, r.Bottom - Dpi.F(3), pw, Dpi.F(3)), Dpi.F(1.5f), theme.Accent);
                }
                else if (i == hotIdx)
                {
                    Draw.Fill(g, r, Dpi.F(4), Theme.Over(theme.Field, theme.Hover));
                }
                Draw.Text(g, items[i], i == sel ? Fonts.UiSemibold : Fonts.Ui, Rectangle.Round(r), i == sel ? theme.Accent : theme.Text, Draw.Line | TextFormatFlags.HorizontalCenter);
                x += w;
            }
            if (Focused && ShowFocusCues) Draw.Stroke(g, outer, Dpi.F(5), theme.Text, Dpi.F(2));
        }
    }

    public interface IFitWidth { void FitWidth(int avail); }

    public sealed class SettingRow : Panel, IThemed, IMeasured
    {
        Theme theme = Theme.Light();
        public Glyph Glyph;
        public string Title, Desc, Value;
        public bool MonoValue, MonoDesc, Dim;
        public Color Swatch = Color.Empty;
        public readonly Control Accessory;
        // A longer explanation behind a small More link under the description.
        public string More;
        // Marked: changed from its default. Highlight: the setting a search opened.
        public bool Marked, Highlight;
        // Puts a wide control under the text instead of beside it when the row is too narrow for both.
        public bool WrapAccessory;
        public event EventHandler MoreToggled;
        bool expanded, linkHot;
        Rectangle link;

        public SettingRow(Glyph glyph, string title, string desc, Control right)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Glyph = glyph;
            Title = title;
            Desc = desc;
            Accessory = right;
            if (right != null) Controls.Add(right);
        }

        public bool Expanded
        {
            get { return expanded; }
            set { if (expanded == value) return; expanded = value; Invalidate(); if (MoreToggled != null) MoreToggled(this, EventArgs.Empty); }
        }

        int TextX()
        {
            return Glyph != Glyph.None || Swatch != Color.Empty ? Dpi.S(56) : Dpi.S(20);
        }

        bool Under(int width)
        {
            if (!WrapAccessory || Accessory == null) return false;
            int aw = Accessory is IFitWidth ? 4 * Dpi.S(108) + Dpi.S(6) : Accessory.Width;
            return aw + Dpi.S(32) > (width - TextX()) / 2;
        }

        int RightW(int width)
        {
            if (Under(width)) return Dpi.S(16);
            if (Accessory != null) return Accessory.Width + Dpi.S(32);
            if (!string.IsNullOrEmpty(Value)) return Math.Min(Draw.Measure(Value, MonoValue ? Fonts.Mono : Fonts.Ui).Width + 2, width / 2) + Dpi.S(20);
            return Dpi.S(16);
        }

        bool HasMore
        {
            get { return !string.IsNullOrEmpty(More); }
        }

        int MoreH(int tw)
        {
            if (!HasMore) return 0;
            int h = Dpi.S(2) + Fonts.Small.Height;
            if (expanded) h += Dpi.S(4) + Draw.WrapHeight(More, Fonts.Small, tw);
            return h;
        }

        void FitAccessory(int width)
        {
            IFitWidth f = Accessory as IFitWidth;
            if (f == null) return;
            f.FitWidth(Under(width) ? width - TextX() - Dpi.S(16) : 4 * Dpi.S(108) + Dpi.S(6));
        }

        public int Measure(int width)
        {
            FitAccessory(width);
            int tw = Math.Max(Dpi.S(80), width - TextX() - RightW(width));
            int h = Dpi.S(14) + Fonts.Ui.Height;
            if (!string.IsNullOrEmpty(Desc)) h += Dpi.S(2) + Draw.WrapHeight(Desc, DescFont, tw);
            h += MoreH(tw);
            if (Under(width)) h += Dpi.S(10) + Accessory.Height;
            h += Dpi.S(14);
            int min = Accessory != null && !Under(width) ? Accessory.Height + Dpi.S(24) : Dpi.S(56);
            return Math.Max(min, h);
        }

        Font DescFont
        {
            get { return MonoDesc ? Fonts.MonoSmall : Fonts.Small; }
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Card;
            IThemed th = Accessory as IThemed;
            if (th != null) th.ApplyTheme(t);
            else if (Accessory != null) { Accessory.BackColor = t.Field; Accessory.ForeColor = t.Text; }
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Accessory == null) return;
            FitAccessory(Width);
            if (Under(Width)) Accessory.Location = new Point(TextX(), Height - Dpi.S(14) - Accessory.Height);
            else Accessory.Location = new Point(Width - Dpi.S(16) - Accessory.Width, (Height - Accessory.Height) / 2);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = HasMore && link.Contains(e.Location);
            if (hot != linkHot) { linkHot = hot; Cursor = hot ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (linkHot) { linkHot = false; Cursor = Cursors.Default; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (HasMore && link.Contains(e.Location)) Expanded = !expanded;
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : theme.Bg);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, r, Dpi.F(6), theme.Card);
            Draw.Stroke(g, r, Dpi.F(6), theme.Divider, 1f);
            if (Highlight) Draw.Stroke(g, new RectangleF(1f, 1f, Width - 2.5f, Height - 2.5f), Dpi.F(6), theme.Accent, 2f);
            if (Swatch != Color.Empty)
            {
                int s = Dpi.S(18);
                RectangleF sw = new RectangleF(Dpi.S(20), (Height - s) / 2f, s, s);
                Draw.Fill(g, sw, s / 2f, Dim ? Theme.Mix(theme.Card, Swatch, 0.35f) : Swatch);
            }
            else if (Glyph != Glyph.None)
            {
                int s = Dpi.S(18);
                Draw.Icon(g, Glyph, new RectangleF(Dpi.S(20), (Height - s) / 2f, s, s), theme.Text2);
            }
            int tx = TextX();
            int tw = Math.Max(Dpi.S(80), Width - tx - RightW(Width));
            int th = Fonts.Ui.Height;
            int dh = string.IsNullOrEmpty(Desc) ? 0 : Draw.WrapHeight(Desc, DescFont, tw);
            int mh = MoreH(tw);
            int block = th + (dh > 0 ? Dpi.S(2) + dh : 0) + mh;
            int y = Under(Width) ? Dpi.S(14) : (Height - block) / 2;
            Draw.Text(g, Title, Fonts.Ui, new Rectangle(tx, y, tw, th), Dim ? theme.Text3 : theme.Text, Draw.Line);
            if (Marked)
            {
                int d = Dpi.S(7);
                int x = tx + Math.Min(tw - d, Draw.Measure(Title, Fonts.Ui).Width + Dpi.S(8));
                Draw.Fill(g, new RectangleF(x, y + (th - d) / 2f, d, d), d / 2f, theme.Accent);
            }
            int ly = y + th;
            if (dh > 0)
            {
                Draw.Text(g, Desc, DescFont, new Rectangle(tx, ly + Dpi.S(2), tw, dh + 2), theme.Text2, Draw.Wrap);
                ly += Dpi.S(2) + dh;
            }
            if (HasMore)
            {
                ly += Dpi.S(2);
                if (expanded)
                {
                    int eh = Draw.WrapHeight(More, Fonts.Small, tw);
                    Draw.Text(g, More, Fonts.Small, new Rectangle(tx, ly, tw, eh + 2), theme.Text2, Draw.Wrap);
                    ly += eh + Dpi.S(4);
                }
                string label = expanded ? "Less" : "More";
                Font f = linkHot ? new Font(Fonts.Small, FontStyle.Underline) : Fonts.Small;
                try
                {
                    link = new Rectangle(tx, ly, Draw.Measure(label, f).Width + Dpi.S(4), Fonts.Small.Height);
                    Draw.Text(g, label, f, link, theme.Accent, Draw.Line);
                }
                finally { if (linkHot) f.Dispose(); }
            }
            if (Accessory == null && !string.IsNullOrEmpty(Value))
            {
                Font f = MonoValue ? Fonts.Mono : Fonts.Ui;
                int vw = RightW(Width) - Dpi.S(20);
                Draw.Text(g, Value, f, new Rectangle(Width - Dpi.S(20) - vw, 0, vw, Height), theme.Text2, Draw.Line | TextFormatFlags.Right | TextFormatFlags.PathEllipsis);
            }
        }
    }

    public sealed class SectionTitle : FluentControl, IMeasured
    {
        public SectionTitle(string text)
        {
            Text = text;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(34);
        }

        public int Measure(int width)
        {
            return Height;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Surface);
            Draw.Text(e.Graphics, Text, Fonts.UiSemibold, new Rectangle(Dpi.S(2), Height - Fonts.UiSemibold.Height - Dpi.S(8), Width, Fonts.UiSemibold.Height + 2), theme.Text, Draw.Line);
        }
    }

    public sealed class AboutHero : FluentControl, IMeasured
    {
        public string Version = "", Detail = "", Developer = "", Blurb = "", License = "";
        public event EventHandler MarkTapped;
        string hint = "";
        float bounce;
        readonly Timer anim = new Timer();
        readonly Timer hintTimer = new Timer();

        public AboutHero()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            anim.Interval = 30;
            anim.Tick += delegate
            {
                bounce -= 0.08f;
                if (bounce <= 0) { bounce = 0; anim.Stop(); }
                Invalidate(MarkRect(4));
            };
            hintTimer.Interval = 1800;
            hintTimer.Tick += delegate { hintTimer.Stop(); hint = ""; Invalidate(); };
        }

        Rectangle MarkRect(int grow)
        {
            int g = Dpi.S(grow);
            return new Rectangle(Dpi.S(20) - g, Dpi.S(22) - g, Dpi.S(56) + g * 2, Dpi.S(56) + g * 2);
        }

        public void Hint(string text)
        {
            hint = text ?? "";
            hintTimer.Stop();
            if (hint.Length > 0) hintTimer.Start();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !MarkRect(0).Contains(e.Location)) return;
            bounce = 1f;
            anim.Start();
            if (MarkTapped != null) MarkTapped(this, EventArgs.Empty);
        }

        public int Measure(int width)
        {
            int tx = Dpi.S(96);
            int bh = Draw.WrapHeight(Blurb, Fonts.Ui, Math.Max(Dpi.S(100), width - tx - Dpi.S(20)));
            int dev = Developer.Length > 0 ? Fonts.Ui.Height + Dpi.S(2) : 0;
            int lic = License.Length > 0 ? Dpi.S(8) + Fonts.Small.Height : 0;
            return Math.Max(Dpi.S(112), Dpi.S(20) + Fonts.Title.Height + Dpi.S(4) + Fonts.Ui.Height + dev + Dpi.S(10) + bh + lic + Dpi.S(20));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, r, Dpi.F(6), theme.Card);
            Draw.Stroke(g, r, Dpi.F(6), theme.Divider, 1f);
            float k = 1f + 0.12f * (float)Math.Sin(bounce * Math.PI);
            float m = Dpi.F(56) * k;
            Draw.AppMark(g, new RectangleF(Dpi.S(48) - m / 2f, Dpi.S(50) - m / 2f, m, m), theme);
            int tx = Dpi.S(96), tw = Width - tx - Dpi.S(20);
            int y = Dpi.S(20);
            Draw.Text(g, "LogVice", Fonts.Title, new Rectangle(tx, y, tw, Fonts.Title.Height), theme.Text, Draw.Line);
            if (hint.Length > 0)
            {
                int hw = Draw.Measure(hint, Fonts.Small).Width + Dpi.S(20);
                Rectangle hr = new Rectangle(Width - Dpi.S(20) - hw, y + (Fonts.Title.Height - Dpi.S(24)) / 2, hw, Dpi.S(24));
                Draw.Fill(g, hr, Dpi.F(12), theme.AccentSoft);
                Draw.Text(g, hint, Fonts.Small, hr, theme.Accent, Draw.Line | TextFormatFlags.HorizontalCenter);
            }
            y += Fonts.Title.Height + Dpi.S(4);
            Draw.Text(g, Version + "   \u00B7   " + Detail, Fonts.Ui, new Rectangle(tx, y, tw, Fonts.Ui.Height), theme.Text2, Draw.Line);
            y += Fonts.Ui.Height;
            if (Developer.Length > 0)
            {
                y += Dpi.S(2);
                string lead = "Developed by";
                int lw = Draw.Measure(lead, Fonts.Ui).Width + Dpi.S(5);
                Draw.Text(g, lead, Fonts.Ui, new Rectangle(tx, y, lw + 2, Fonts.Ui.Height), theme.Text2, Draw.Line);
                Draw.Text(g, Developer, Fonts.UiSemibold, new Rectangle(tx + lw, y, Math.Max(0, tw - lw), Fonts.Ui.Height), theme.Text, Draw.Line);
                y += Fonts.Ui.Height;
            }
            y += Dpi.S(10);
            int bh = Draw.WrapHeight(Blurb, Fonts.Ui, tw);
            Draw.Text(g, Blurb, Fonts.Ui, new Rectangle(tx, y, tw, bh + 2), theme.Text, Draw.Wrap);
            y += bh;
            if (License.Length > 0) Draw.Text(g, License, Fonts.Small, new Rectangle(tx, y + Dpi.S(8), tw, Fonts.Small.Height + 2), theme.Text3, Draw.Line);
        }
    }

    // Two columns: a key (a shortcut or a command) and what it does.
    public sealed class KeyTable : FluentControl, IMeasured
    {
        readonly string[][] rows;
        readonly bool mono;

        public KeyTable(bool monoKeys, params string[][] rows)
        {
            this.rows = rows;
            mono = monoKeys;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
        }

        Font KeyFont
        {
            get { return mono ? Fonts.Mono : Fonts.SmallSemibold; }
        }

        int KeyW(int width)
        {
            int w = 0;
            foreach (string[] r in rows) w = Math.Max(w, Draw.Measure(r[0], KeyFont).Width + Dpi.S(16));
            return Math.Min(w, width * 3 / 5);
        }

        int RowH(string[] r, int width)
        {
            int dw = Math.Max(Dpi.S(60), width - KeyW(width) - Dpi.S(64));
            return Math.Max(Dpi.S(34), Draw.WrapHeight(r[1], Fonts.Ui, dw) + Dpi.S(14));
        }

        public string AsText()
        {
            StringBuilder b = new StringBuilder();
            foreach (string[] r in rows) b.Append(r[0].PadRight(42)).Append(r[1]).Append("\r\n");
            return b.ToString();
        }

        public int Measure(int width)
        {
            int h = Dpi.S(12);
            foreach (string[] r in rows) h += RowH(r, width);
            return h;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF card = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, card, Dpi.F(6), theme.Card);
            Draw.Stroke(g, card, Dpi.F(6), theme.Divider, 1f);
            int kw = KeyW(Width);
            int y = Dpi.S(6);
            int dx = Dpi.S(20) + kw + Dpi.S(20);
            foreach (string[] r in rows)
            {
                int h = RowH(r, Width);
                int kh = KeyFont.Height + Dpi.S(6);
                Rectangle key = new Rectangle(Dpi.S(20), y + (h - kh) / 2, Math.Min(kw, Draw.Measure(r[0], KeyFont).Width + Dpi.S(16)), kh);
                Draw.Fill(g, key, Dpi.F(4), theme.CardAlt);
                Draw.Stroke(g, key, Dpi.F(4), theme.Divider, 1f);
                Draw.Text(g, r[0], KeyFont, key, theme.Text, Draw.Line | TextFormatFlags.HorizontalCenter);
                int dw = Math.Max(0, Width - dx - Dpi.S(20));
                int dh = Draw.WrapHeight(r[1], Fonts.Ui, dw);
                Draw.Text(g, r[1], Fonts.Ui, new Rectangle(dx, y + (h - dh) / 2, dw, dh + 2), theme.Text2, Draw.Wrap);
                y += h;
            }
        }
    }

    public sealed class LocationCard : FluentControl
    {
        public readonly LogLocation Loc;

        public static Color GroupColor(string group, Theme t)
        {
            switch (group)
            {
                case LogLocations.ConfigMgr: return t.Accent;
                case LogLocations.Intune: return t.IsDark ? Theme.Hex(0x4CD9E6) : Theme.Hex(0x00838F);
                case LogLocations.Windows: return t.IsDark ? Theme.Hex(0xB4A0FF) : Theme.Hex(0x6B4FBB);
                case EventLogs.Group: return t.IsDark ? Theme.Hex(0x6BE38F) : Theme.Hex(0x2E7D32);
                default: return t.IsDark ? Theme.Hex(0xFFB36B) : Theme.Hex(0xC25700);
            }
        }

        public LocationCard(LogLocation l)
        {
            Loc = l;
            TabStop = true;
            Cursor = Cursors.Hand;
            Height = Dpi.S(62);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Color fill = pressed ? Theme.Over(theme.Card, theme.Pressed) : hot ? Theme.Over(theme.Card, theme.Hover) : theme.Card;
            Draw.Fill(g, r, Dpi.F(6), fill);
            Draw.Stroke(g, r, Dpi.F(6), hot ? theme.Accent : theme.Divider, 1f);
            Color tone = GroupColor(Loc.Group, theme);
            int tile = Dpi.S(36);
            RectangleF tr = new RectangleF(Dpi.S(12), (Height - tile) / 2f, tile, tile);
            Draw.Fill(g, tr, Dpi.F(6), Theme.Mix(theme.Card, tone, theme.IsDark ? 0.22f : 0.13f));
            int s = Dpi.S(18);
            Draw.Icon(g, Loc.Glyph, new RectangleF(tr.X + (tile - s) / 2f, tr.Y + (tile - s) / 2f, s, s), tone);
            int tx = Dpi.S(12) + tile + Dpi.S(12);
            int tw = Width - tx - Dpi.S(12);
            int lh = Fonts.UiSemibold.Height, sh = Fonts.Small.Height;
            int y = (Height - lh - sh - Dpi.S(3)) / 2;
            string sum = Loc.Summary;
            int sw = Draw.Measure(sum, Fonts.Small).Width + 2;
            Draw.Text(g, Loc.Name, Fonts.UiSemibold, new Rectangle(tx, y, Math.Max(0, tw - sw - Dpi.S(8)), lh), theme.Text, Draw.Line);
            Draw.Text(g, sum, Fonts.Small, new Rectangle(tx + tw - sw, y + 1, sw, lh), theme.Text3, Draw.Line);
            Draw.Text(g, Loc.Where, Fonts.Small, new Rectangle(tx, y + lh + Dpi.S(3), tw, sh + 2), theme.Text2, Draw.Line | TextFormatFlags.PathEllipsis);
            if (Focused && ShowFocusCues) Draw.Stroke(g, r, Dpi.F(6), theme.Text, Dpi.F(2));
        }
    }

    public sealed class LocationList : FluentControl
    {
        List<LogLocation> items = new List<LogLocation>();
        readonly List<int> rowsY = new List<int>();
        int sel = -1, hotIdx = -1, scroll, contentH;
        public string EmptyText = "";
        public event EventHandler SelectionChanged;

        public LocationList()
        {
            TabStop = true;
        }

        public LogLocation Selected
        {
            get { return sel >= 0 && sel < items.Count ? items[sel] : null; }
        }

        public void SetItems(List<LogLocation> list)
        {
            items = list ?? new List<LogLocation>();
            sel = -1;
            scroll = 0;
            Invalidate();
        }

        public void Select(LogLocation l)
        {
            int i = items.IndexOf(l);
            if (i < 0) return;
            sel = i;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        int ItemH { get { return Dpi.S(54); } }
        int HeadH { get { return Dpi.S(34); } }

        void LayoutRows()
        {
            rowsY.Clear();
            int y = Dpi.S(6);
            string group = null;
            foreach (LogLocation l in items)
            {
                if (l.Group != group) { group = l.Group; y += HeadH; }
                rowsY.Add(y);
                y += ItemH;
            }
            contentH = y + Dpi.S(8);
        }

        int IndexAt(int y)
        {
            for (int i = 0; i < rowsY.Count; i++)
            {
                int top = rowsY[i] - scroll;
                if (y >= top && y < top + ItemH) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Y);
            if (i != hotIdx) { hotIdx = i; Invalidate(); }
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotIdx = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int i = IndexAt(e.Y);
            if (i >= 0 && i != sel)
            {
                sel = i;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll = Math.Max(0, Math.Min(Math.Max(0, contentH - Height), scroll - e.Delta / 120 * Dpi.S(54)));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k == Keys.Up || k == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int n = sel;
            if (e.KeyCode == Keys.Up) n = Math.Max(0, sel - 1);
            else if (e.KeyCode == Keys.Down) n = Math.Min(items.Count - 1, sel + 1);
            if (n != sel && n >= 0)
            {
                sel = n;
                e.Handled = true;
                if (rowsY.Count > n)
                {
                    int top = rowsY[n];
                    if (top - scroll < 0) scroll = Math.Max(0, top - HeadH);
                    else if (top + ItemH - scroll > Height) scroll = top + ItemH - Height;
                }
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            LayoutRows();
            if (items.Count == 0)
            {
                int h = Draw.WrapHeight(EmptyText, Fonts.Ui, Width - Dpi.S(32));
                Draw.Text(g, EmptyText, Fonts.Ui, new Rectangle(Dpi.S(16), Dpi.S(16), Width - Dpi.S(32), h + 2), theme.Text2, Draw.Wrap);
                return;
            }
            string group = null;
            for (int i = 0; i < items.Count; i++)
            {
                LogLocation l = items[i];
                int y = rowsY[i] - scroll;
                if (l.Group != group)
                {
                    group = l.Group;
                    Draw.Text(g, group, Fonts.SmallSemibold, new Rectangle(Dpi.S(16), y - HeadH, Width - Dpi.S(32), HeadH - Dpi.S(4)), theme.Text3, Draw.Line | TextFormatFlags.Bottom);
                }
                if (y + ItemH < 0 || y > Height) continue;
                RectangleF r = new RectangleF(Dpi.S(6), y + Dpi.S(2), Width - Dpi.S(12), ItemH - Dpi.S(4));
                if (i == sel)
                {
                    Draw.Fill(g, r, Dpi.F(5), Theme.Mix(surface, theme.Accent, theme.IsDark ? 0.18f : 0.1f));
                    Draw.Fill(g, new RectangleF(r.X, r.Y + (r.Height - Dpi.F(16)) / 2f, Dpi.F(3), Dpi.F(16)), Dpi.F(1.5f), theme.Accent);
                }
                else if (i == hotIdx) Draw.Fill(g, r, Dpi.F(5), Theme.Over(surface, theme.Hover));
                int s = Dpi.S(18);
                Draw.Icon(g, l.Glyph, new RectangleF(r.X + Dpi.S(12), r.Y + (r.Height - s) / 2f, s, s), i == sel ? theme.Accent : theme.Text2);
                int tx = (int)r.X + Dpi.S(12) + s + Dpi.S(12);
                int tw = (int)r.Right - tx - Dpi.S(10);
                string cnt = l.FileCount.ToString("N0");
                int cw = Draw.Measure(cnt, Fonts.Small).Width + 2;
                int lh = Fonts.Ui.Height, sh = Fonts.Small.Height;
                int ty = (int)r.Y + ((int)r.Height - lh - sh - Dpi.S(2)) / 2;
                Draw.Text(g, l.Name, Fonts.Ui, new Rectangle(tx, ty, Math.Max(0, tw - cw - Dpi.S(6)), lh), theme.Text, Draw.Line);
                Draw.Text(g, cnt, Fonts.Small, new Rectangle(tx + tw - cw, ty + 1, cw, lh), theme.Text3, Draw.Line);
                Draw.Text(g, l.Where, Fonts.Small, new Rectangle(tx, ty + lh + Dpi.S(2), tw, sh + 2), theme.Text3, Draw.Line | (l.Kind == "evt" ? TextFormatFlags.EndEllipsis : TextFormatFlags.PathEllipsis));
            }
            if (contentH > Height)
            {
                float track = Height - Dpi.S(8);
                float len = Math.Max(Dpi.S(24), track * Height / contentH);
                float top = Dpi.S(4) + (track - len) * scroll / Math.Max(1, contentH - Height);
                Draw.Fill(g, new RectangleF(Width - Dpi.S(4), top, Dpi.S(3), len), Dpi.F(1.5f), theme.Thumb);
            }
        }
    }

    public sealed class FileList : FluentControl
    {
        List<LogFile> all = new List<LogFile>();
        List<LogFile> view = new List<LogFile>();
        bool[] selected = new bool[0];
        int cur = -1, anchor = -1, hotRow = -1, sortCol = 1;
        bool sortDesc = true;
        double off;
        string filter = "";
        public string EmptyText = "";
        public event EventHandler SelectionChanged;
        public event EventHandler Activated;

        public FileList()
        {
            TabStop = true;
        }

        int RowH { get { return Dpi.S(32); } }
        int HeadH { get { return Dpi.S(34); } }

        public void SetFiles(List<LogFile> files)
        {
            all = files ?? new List<LogFile>();
            off = 0;
            Rebuild();
        }

        public string Filter
        {
            set
            {
                filter = value ?? "";
                Rebuild();
            }
        }

        public int Count { get { return view.Count; } }

        public List<LogFile> Selection()
        {
            List<LogFile> list = new List<LogFile>();
            for (int i = 0; i < view.Count; i++) if (selected[i]) list.Add(view[i]);
            return list;
        }

        void Rebuild()
        {
            List<LogFile> sel = new List<LogFile>();
            for (int i = 0; i < view.Count && i < selected.Length; i++) if (selected[i]) sel.Add(view[i]);
            view = new List<LogFile>();
            foreach (LogFile f in all)
                if (filter.Length == 0 || f.Rel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) view.Add(f);
            Comparison<LogFile> cmp;
            if (sortCol == 0) cmp = delegate (LogFile a, LogFile b) { return string.Compare(a.Rel, b.Rel, StringComparison.OrdinalIgnoreCase); };
            else if (sortCol == 2) cmp = delegate (LogFile a, LogFile b) { return a.Size.CompareTo(b.Size); };
            else cmp = delegate (LogFile a, LogFile b) { return a.Modified.CompareTo(b.Modified); };
            bool desc = sortDesc;
            view.Sort(delegate (LogFile a, LogFile b) { int c = cmp(a, b); return desc ? -c : c; });
            selected = new bool[view.Count];
            cur = anchor = -1;
            for (int i = 0; i < view.Count; i++)
            {
                if (sel.Contains(view[i])) { selected[i] = true; if (cur < 0) cur = anchor = i; }
            }
            Clamp();
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        Rectangle Rows
        {
            get { return new Rectangle(0, HeadH, Width, Math.Max(0, Height - HeadH)); }
        }

        void Clamp()
        {
            double max = Math.Max(0, (double)view.Count * RowH - Rows.Height);
            if (off > max) off = max;
            if (off < 0) off = 0;
        }

        void Ensure(int p)
        {
            double top = (double)p * RowH;
            if (top < off) off = top;
            else if (top + RowH > off + Rows.Height) off = top + RowH - Rows.Height;
            Clamp();
        }

        void Pick(int p, bool shift, bool ctrl)
        {
            if (p < 0 || p >= view.Count) return;
            if (shift && anchor >= 0)
            {
                if (!ctrl) for (int i = 0; i < selected.Length; i++) selected[i] = false;
                int lo = Math.Min(anchor, p), hi = Math.Max(anchor, p);
                for (int i = lo; i <= hi; i++) selected[i] = true;
            }
            else if (ctrl)
            {
                selected[p] = !selected[p];
                anchor = p;
            }
            else
            {
                for (int i = 0; i < selected.Length; i++) selected[i] = false;
                selected[p] = true;
                anchor = p;
            }
            cur = p;
            Ensure(p);
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public void SelectAll()
        {
            for (int i = 0; i < selected.Length; i++) selected[i] = true;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        struct Cols
        {
            public int Icon, Name, NameW, Mod, Size, End;
        }

        Cols Layout2()
        {
            Cols c = new Cols();
            c.End = Width - Dpi.S(12);
            c.Icon = Dpi.S(12);
            c.Name = c.Icon + Dpi.S(28);
            c.Size = c.End - Dpi.S(90);
            c.Mod = c.Size - Dpi.S(190);
            c.NameW = Math.Max(Dpi.S(80), c.Mod - c.Name);
            return c;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            Cols c = Layout2();
            string[] heads = { "Name", "Modified", "Size" };
            int[] xs = { c.Name, c.Mod, c.Size };
            for (int k = 0; k < 3; k++)
            {
                string h = heads[k] + (sortCol == k ? (sortDesc ? "  ↓" : "  ↑") : "");
                TextFormatFlags f = Draw.Line | (k == 2 ? TextFormatFlags.Right : TextFormatFlags.Left);
                int w = k == 0 ? c.NameW : k == 1 ? Dpi.S(190) : Dpi.S(90);
                Draw.Text(g, h, Fonts.SmallSemibold, new Rectangle(xs[k] + Dpi.S(8), 0, w - Dpi.S(16), HeadH), sortCol == k ? theme.Text : theme.Text2, f);
            }
            using (Pen p = new Pen(theme.Divider)) g.DrawLine(p, Dpi.S(8), HeadH - 1, Width - Dpi.S(8), HeadH - 1);
            Rectangle rr = Rows;
            if (view.Count == 0)
            {
                int h = Draw.WrapHeight(EmptyText, Fonts.Ui, Width - Dpi.S(48));
                Draw.Text(g, EmptyText, Fonts.Ui, new Rectangle(Dpi.S(24), rr.Top + Dpi.S(24), Width - Dpi.S(48), h + 2), theme.Text2, Draw.Wrap | TextFormatFlags.HorizontalCenter);
                return;
            }
            g.SetClip(rr);
            int first = (int)(off / RowH);
            int y = rr.Top - (int)Math.Round(off - (double)first * RowH);
            DateTime now = DateTime.Now;
            for (int i = first; i < view.Count && y < rr.Bottom; i++, y += RowH)
            {
                LogFile f = view[i];
                RectangleF r = new RectangleF(Dpi.S(4), y + 1, Width - Dpi.S(8), RowH - 2);
                if (selected[i])
                {
                    Draw.Fill(g, r, Dpi.F(4), Theme.Mix(surface, theme.Accent, i == cur ? (theme.IsDark ? 0.26f : 0.17f) : (theme.IsDark ? 0.18f : 0.11f)));
                    if (i == cur) Draw.Fill(g, new RectangleF(r.X, r.Y + (r.Height - Dpi.F(14)) / 2f, Dpi.F(3), Dpi.F(14)), Dpi.F(1.5f), theme.Accent);
                }
                else if (i == hotRow) Draw.Fill(g, r, Dpi.F(4), Theme.Over(surface, theme.Hover));
                int s = Dpi.S(16);
                Draw.Icon(g, f.Rolled ? Glyph.Recent : Glyph.File, new RectangleF(c.Icon + Dpi.S(4), y + (RowH - s) / 2f, s, s), f.Rolled ? theme.Text3 : theme.Text2);
                Draw.Text(g, f.Rel, Fonts.Ui, new Rectangle(c.Name + Dpi.S(8), y, c.NameW - Dpi.S(16), RowH), f.Rolled ? theme.Text2 : theme.Text, Draw.Line | TextFormatFlags.PathEllipsis);
                bool active = (now - f.Modified).TotalMinutes < 10 && f.Modified <= now.AddMinutes(1);
                int mx = c.Mod + Dpi.S(8);
                if (active)
                {
                    float d = Dpi.F(7);
                    Draw.Fill(g, new RectangleF(mx, y + (RowH - d) / 2f, d, d), d / 2f, theme.OkFg);
                    mx += Dpi.S(14);
                }
                Draw.Text(g, LogFile.When(f.Modified), Fonts.Ui, new Rectangle(mx, y, c.Mod + Dpi.S(190) - mx - Dpi.S(8), RowH), theme.Text2, Draw.Line);
                Draw.Text(g, Fmt.Size(f.Size), Fonts.Ui, new Rectangle(c.Size + Dpi.S(8), y, Dpi.S(90) - Dpi.S(16), RowH), theme.Text2, Draw.Line | TextFormatFlags.Right);
            }
            g.ResetClip();
            double total = (double)view.Count * RowH;
            if (total > rr.Height)
            {
                float track = rr.Height - Dpi.S(8);
                float len = (float)Math.Max(Dpi.S(28), track * rr.Height / total);
                float top = rr.Top + Dpi.S(4) + (float)((track - len) * off / Math.Max(1, total - rr.Height));
                Draw.Fill(g, new RectangleF(Width - Dpi.S(5), top, Dpi.S(3), len), Dpi.F(1.5f), theme.Thumb);
            }
        }

        int RowAt(int y)
        {
            if (y < HeadH) return -1;
            int p = (int)Math.Floor((y - HeadH + off) / RowH);
            return p >= 0 && p < view.Count ? p : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int r = RowAt(e.Y);
            if (r != hotRow) { hotRow = r; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotRow = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Y < HeadH)
            {
                Cols c = Layout2();
                int col = e.X >= c.Size ? 2 : e.X >= c.Mod ? 1 : 0;
                if (col == sortCol) sortDesc = !sortDesc;
                else { sortCol = col; sortDesc = col != 0; }
                Rebuild();
                return;
            }
            int p = RowAt(e.Y);
            if (p < 0) return;
            bool shift = (ModifierKeys & Keys.Shift) != 0, ctrl = (ModifierKeys & Keys.Control) != 0;
            if (e.Button == MouseButtons.Right && selected[p]) return;
            Pick(p, shift, ctrl);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (cur >= 0 && Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            off -= e.Delta / 120.0 * 3 * RowH;
            Clamp();
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                case Keys.Enter:
                case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (view.Count == 0) return;
            int page = Math.Max(1, Rows.Height / RowH - 1);
            int p = -2;
            switch (e.KeyCode)
            {
                case Keys.Up: p = Math.Max(0, cur - 1); break;
                case Keys.Down: p = Math.Min(view.Count - 1, cur + 1); break;
                case Keys.PageUp: p = Math.Max(0, cur - page); break;
                case Keys.PageDown: p = Math.Min(view.Count - 1, cur + page); break;
                case Keys.Home: p = 0; break;
                case Keys.End: p = view.Count - 1; break;
                case Keys.Space:
                    if (cur >= 0) { Pick(cur, false, true); e.Handled = true; }
                    return;
                case Keys.Enter:
                    if (Activated != null) Activated(this, EventArgs.Empty);
                    e.Handled = true;
                    return;
                case Keys.A:
                    if (e.Control) { SelectAll(); e.Handled = true; }
                    return;
            }
            if (p >= 0)
            {
                e.Handled = true;
                Pick(p, e.Shift, false);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Clamp();
        }
    }
}
