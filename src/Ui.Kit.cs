namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    // Catches exceptions from event handlers and reports them in the status bar instead of failing.
    public static class Safe
    {
        public static Action<Exception> Reporter;
        public static Action<string> Status;

        public static void Report(Exception ex)
        {
            try { if (Reporter != null && ex != null) Reporter(ex); } catch (Exception) { }
        }

        public static void Say(string text)
        {
            try { if (Status != null) Status(text); } catch (Exception) { }
        }

        public static EventHandler Wrap(EventHandler h)
        {
            return delegate (object s, EventArgs e)
            {
                try { h(s, e); }
                catch (Exception ex) { Report(ex); }
            };
        }

        public static void Run(Action a)
        {
            try { a(); }
            catch (Exception ex) { Report(ex); }
        }
    }

    // Menus in the look of the theme, built when they are opened.
    public static class Menus
    {
        public static ContextMenuStrip New(Theme t)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            MenuRenderer r = new MenuRenderer();
            r.Theme = t;
            m.Renderer = r;
            m.ShowImageMargin = false;
            m.ShowCheckMargin = false;
            m.BackColor = t.Card;
            m.ForeColor = t.Text;
            m.Font = Fonts.Ui;
            m.Padding = new Padding(0, Dpi.S(4), 0, Dpi.S(4));
            m.Opened += delegate { try { NativeMethods.RoundPopup(m.Handle); } catch (Exception) { } };
            return m;
        }

        public static ToolStripMenuItem Add(ToolStrip m, string text, string shortcut, EventHandler click)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            if (click != null) i.Click += Safe.Wrap(click);
            if (!string.IsNullOrEmpty(shortcut)) i.ShortcutKeyDisplayString = shortcut;
            i.ForeColor = m.ForeColor;
            i.Padding = new Padding(Dpi.S(4), Dpi.S(5), Dpi.S(4), Dpi.S(5));
            m.Items.Add(i);
            return i;
        }

        public static ToolStripMenuItem Add(ToolStrip m, string text, EventHandler click)
        {
            return Add(m, text, null, click);
        }

        public static ToolStripMenuItem Sub(ToolStrip m, string text)
        {
            return Add(m, text, null, null);
        }

        public static void Separator(ToolStrip m)
        {
            if (m.Items.Count > 0 && !(m.Items[m.Items.Count - 1] is ToolStripSeparator)) m.Items.Add(new ToolStripSeparator());
        }

        public static bool Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                Clipboard.SetText(text);
                Safe.Say("Copied to the clipboard.");
                return true;
            }
            catch (Exception)
            {
                Safe.Say("The clipboard is busy. Try again.");
                return false;
            }
        }
    }

    // A slim bar that slides while LogVice reads files.
    public sealed class BusyBar : FluentControl
    {
        readonly Timer timer = new Timer();
        float pos;
        bool running;

        public BusyBar()
        {
            Height = Dpi.S(3);
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            timer.Interval = 30;
            timer.Tick += delegate
            {
                pos += 0.012f;
                if (pos > 1.2f) pos = -0.3f;
                Invalidate();
            };
        }

        public bool Running
        {
            get { return running; }
            set
            {
                if (running == value) return;
                running = value;
                pos = -0.3f;
                if (value) timer.Start(); else timer.Stop();
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.Bg);
            if (!running) return;
            int w = Math.Max(Dpi.S(60), Width / 4);
            int x = (int)(pos * Width);
            using (SolidBrush b = new SolidBrush(theme.Accent)) g.FillRectangle(b, x, 0, w, Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // The base of the flat controls of the header and the pages. Colours are read from the theme in use.
    public class FlatControl : FluentControl
    {
        protected Theme T
        {
            get { return theme; }
        }

        protected bool hover
        {
            get { return hot; }
        }

        protected bool down
        {
            get { return pressed; }
        }

        public override void ApplyTheme(Theme t)
        {
            theme = t;
            Theme.CornerScale = t.Corners;
            Invalidate();
        }
    }

    public sealed class FlatButton : FlatControl
    {
        public Glyph Glyph;
        // Drawn after the text, such as the chevron of a button that opens a menu.
        Glyph trailing;

        public Glyph Trailing
        {
            get { return trailing; }
            set { trailing = value; AutoWidth(); Invalidate(); }
        }
        public bool Primary;
        public bool Checked;
        public bool Compact;
        // A thin border around the button, for buttons that sit alone and must look like buttons.
        public bool Outline;
        // A green dot on the glyph while Checked, for Live.
        public bool LiveDot;
        // The header sets this to hide a button for good, apart from folding it.
        public bool Wanted = true;
        // The colour of the glyph while the button is plain, such as red for the error buttons.
        public Color GlyphColor = Color.Empty;
        float pulse;
        Timer pulseTimer;

        public FlatButton(string text, Glyph glyph)
        {
            Text = text;
            Glyph = glyph;
            Cursor = Cursors.Hand;
            Height = Dpi.S(32);
            TabStop = true;
            AutoWidth();
        }

        public void PerformClick()
        {
            if (Enabled) OnClick(EventArgs.Empty);
        }

        int Pad
        {
            get { return Dpi.S(Compact ? 10 : 14); }
        }

        public void AutoWidth()
        {
            bool hasText = !string.IsNullOrEmpty(Text);
            int w = Pad * 2;
            if (!hasText && Glyph != Glyph.None) w = Dpi.S(32);
            else
            {
                if (hasText) w += TextRenderer.MeasureText(Text, Fonts.Ui).Width;
                if (Glyph != Glyph.None) w += Dpi.S(24);
            }
            if (Trailing != Glyph.None) w += Dpi.S(20);
            Width = Math.Max(w, Dpi.S(32));
        }

        public void Pulse()
        {
            pulse = 1f;
            if (pulseTimer == null)
            {
                pulseTimer = new Timer();
                pulseTimer.Interval = 40;
                pulseTimer.Tick += delegate
                {
                    pulse -= 0.06f;
                    if (pulse <= 0) { pulse = 0; pulseTimer.Stop(); }
                    Invalidate();
                };
            }
            pulseTimer.Start();
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AutoWidth();
            if (Parent != null) Parent.PerformLayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            float rad = Dpi.F(5);
            Color fg = T.Text, glyphC = T.Text2;
            if (Primary)
            {
                Draw.Fill(g, r, rad, !Enabled ? T.Divider : down ? Theme.Mix(T.Accent, surface, 0.2f) : hover ? T.AccentHover : T.Accent);
                fg = glyphC = Enabled ? T.OnAccent : T.Text3;
            }
            else
            {
                Color bg = Checked ? T.AccentSoft : down ? Theme.Over(surface, T.Pressed) : hover && Enabled ? Theme.Over(surface, T.Hover) : Color.Empty;
                if (!bg.IsEmpty) Draw.Fill(g, r, rad, bg);
                if (Checked) Draw.Stroke(g, r, rad, T.Accent, 1f);
                else if (Outline) Draw.Stroke(g, r, rad, hover && Enabled ? T.Text3 : T.Divider, 1f);
                if (Checked) fg = glyphC = T.Accent;
                if (!Enabled) fg = glyphC = T.Text3;
            }
            if (!GlyphColor.IsEmpty && Enabled && !Checked && !Primary) glyphC = GlyphColor;
            if (pulse > 0) Draw.Fill(g, r, rad, Color.FromArgb((int)(pulse * 70), T.OkFg));
            bool hasText = !string.IsNullOrEmpty(Text);
            int x = Pad;
            int gs = Dpi.S(16);
            if (Glyph != Glyph.None)
            {
                float gx = hasText ? x : (Width - gs) / 2f - (Trailing != Glyph.None ? Dpi.S(8) : 0);
                Draw.Icon(g, Glyph, new RectangleF(gx, (Height - gs) / 2f, gs, gs), glyphC);
                if (LiveDot && Checked && Enabled)
                {
                    float d = Dpi.F(6);
                    using (SolidBrush b = new SolidBrush(T.OkFg))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.FillEllipse(b, gx + gs - d / 2f, (Height - gs) / 2f - d / 3f, d, d);
                    }
                }
                if (hasText) x += Dpi.S(24);
            }
            int right = Width - (Trailing != Glyph.None ? Dpi.S(28) : Pad);
            if (hasText)
            {
                Rectangle tr = new Rectangle(x, 0, Math.Max(0, right - x), Height);
                TextFormatFlags f = Draw.Line | (Glyph != Glyph.None ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
                Draw.Text(g, Text, Fonts.Ui, tr, fg, f);
            }
            if (Trailing != Glyph.None)
            {
                int ts = Dpi.S(12);
                Draw.Icon(g, Trailing, new RectangleF(Width - Dpi.S(10) - ts - Dpi.S(2), (Height - ts) / 2f, ts, ts), Primary ? fg : T.Text3);
            }
            if (Focused && ShowFocusCues) Draw.Stroke(g, r, rad, T.Accent, Dpi.F(1.5f));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { PerformClick(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && pulseTimer != null) pulseTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    // A clickable count that also works as a filter.
    public sealed class StatChip : FlatControl
    {
        public string Value = "0";
        public string Label;
        // 0 plain, 1 warning, 2 error: the colour of the number when there is something to see.
        public int Tone;
        // The count is above zero, so the tone shows.
        public bool Alert;
        // On: the group of rows is shown. Off: it is hidden, and the chip is drawn dim.
        public bool Selected = true;
        // Some other group is hidden, so the groups that are shown stand out.
        public bool Emphasis;
        // A low window draws the chip as one short line.
        public bool Slim;

        public StatChip(string label, int tone)
        {
            Label = label;
            Tone = tone;
            Cursor = Cursors.Hand;
            TabStop = true;
            Fit();
        }

        public void Fit()
        {
            int lw = Draw.Measure(Label, Fonts.Small).Width;
            int w = Math.Max(Dpi.S(118), lw + Dpi.S(30));
            if (Slim) w = Dpi.S(12) + Draw.Measure(Value, T.Num(Fonts.UiSemibold)).Width + Dpi.S(10) + lw + Dpi.S(14);
            Size = new Size(w, Dpi.S(Slim ? 32 : 54));
        }

        public void SetCount(long n, bool alert)
        {
            string v = n.ToString("N0");
            if (v == Value && alert == Alert) return;
            Value = v;
            Alert = alert;
            if (Slim) Fit();
            Invalidate();
        }

        Color Tint
        {
            get { return Tone == 2 ? T.ErrFg : Tone == 1 ? T.WarnFg : Tone == 3 ? T.Text3 : T.Accent; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Color fill = T.Card, stroke = T.Divider, num = T.Text3, lab = T.Text3;
            if (Selected)
            {
                num = T.Text;
                lab = T.Text2;
                if (Tone == 0) { fill = T.AccentSoft; stroke = T.Accent; }
                else if (!Emphasis)
                {
                    if (Alert) num = Tint;
                }
                else
                {
                    fill = Theme.Mix(T.Card, Tint, T.IsDark ? 0.14f : 0.1f);
                    stroke = Theme.Mix(T.Card, Tint, 0.55f);
                    if (Alert) num = Tint;
                }
            }
            if (hover) fill = Theme.Over(fill, T.Hover);
            Draw.Fill(g, r, Dpi.F(7), fill);
            Draw.Stroke(g, r, Dpi.F(7), stroke, 1f);
            int pad = Dpi.S(12);
            if (Slim)
            {
                int nw = Draw.Measure(Value, T.Num(Fonts.UiSemibold)).Width + 2;
                Draw.Text(g, Value, T.Num(Fonts.UiSemibold), new Rectangle(pad, 0, nw, Height), num, Draw.Line);
                Draw.Text(g, Label, Fonts.Small, new Rectangle(pad + nw + Dpi.S(8), 0, Math.Max(0, Width - pad * 2 - nw - Dpi.S(8)), Height), lab, Draw.Line);
            }
            else
            {
                Draw.Text(g, Value, T.Num(Fonts.Title), new Rectangle(pad, Dpi.S(4), Width - pad, Dpi.S(28)), num, Draw.Line);
                Draw.Text(g, Label, Fonts.Small, new Rectangle(pad, Dpi.S(31), Width - pad, Dpi.S(18)), lab, Draw.Line);
            }
            if (Focused && ShowFocusCues) Draw.Stroke(g, r, Dpi.F(7), T.Accent, Dpi.F(1.5f));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    public sealed class TabStrip : FlatControl
    {
        readonly List<string> tabs = new List<string>();
        readonly List<Rectangle> hits = new List<Rectangle>();
        int selected, hot2 = -1;
        public event EventHandler SelectedChanged;

        public TabStrip()
        {
            Height = Dpi.S(38);
            Cursor = Cursors.Hand;
        }

        public void SetTabs(params string[] names)
        {
            tabs.Clear();
            tabs.AddRange(names);
            Invalidate();
        }

        public int Selected
        {
            get { return selected; }
            set
            {
                if (value == selected) return;
                selected = value;
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(T.Bg);
            hits.Clear();
            int x = Dpi.S(16);
            int pad = Dpi.S(20);
            for (int i = 0; i < tabs.Count; i++)
            {
                Font f = i == selected ? Fonts.UiSemibold : Fonts.Ui;
                int w = TextRenderer.MeasureText(tabs[i], f).Width + pad;
                Rectangle r = new Rectangle(x, 0, w, Height);
                hits.Add(r);
                if (i == hot2 && i != selected) Draw.Fill(g, new RectangleF(r.X, Dpi.S(4), r.Width, Height - Dpi.S(10)), Dpi.F(5), Theme.Over(T.Bg, T.Hover));
                Draw.Text(g, tabs[i], f, new Rectangle(r.X, 0, r.Width, Height - Dpi.S(4)), i == selected ? T.Text : T.Text2, Draw.Line | TextFormatFlags.HorizontalCenter);
                if (i == selected) Draw.Fill(g, new RectangleF(r.X + Dpi.S(10), Height - Dpi.S(5), r.Width - Dpi.S(20), Dpi.S(3)), Dpi.F(1.5f), T.Accent);
                x += w + Dpi.S(4);
            }
            using (Pen p = new Pen(T.Divider)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < hits.Count; i++) if (hits[i].Contains(e.Location)) h = i;
            if (h != hot2) { hot2 = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot2 = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            for (int i = 0; i < hits.Count; i++) if (hits[i].Contains(e.Location)) Selected = i;
            base.OnMouseClick(e);
        }
    }
}
