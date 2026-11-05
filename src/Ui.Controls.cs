namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;
    using System.Windows.Forms;

    public class FluentControl : Control, IThemed
    {
        protected Theme theme = Theme.Light();
        protected bool hot, pressed;

        public FluentControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public Theme Theme
        {
            get { return theme; }
        }

        public virtual void ApplyTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected Color Surface
        {
            get { return Parent != null ? Parent.BackColor : theme.Bg; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hot = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }
    }

    public enum ButtonKind
    {
        Standard, Accent, Subtle, Chip
    }

    public class FluentButton : FluentControl
    {
        public ButtonKind Kind;
        public Glyph Glyph;
        public Glyph Trailing;
        public bool Toggle;
        public bool CanCompact;
        public int Severity;
        public string Count = "";
        public string Tip = "";
        public Color GlyphColor = Color.Empty;
        bool isChecked, compact;
        float pulse;
        Timer pulseTimer;

        public event EventHandler CheckedChanged;

        public FluentButton(ButtonKind kind, Glyph glyph, string text)
        {
            Kind = kind;
            Glyph = glyph;
            Font = Fonts.Ui;
            Text = text;
            TabStop = true;
            Height = Dpi.S(kind == ButtonKind.Chip ? 30 : 32);
            Width = GetPreferredSize(Size.Empty).Width;
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public bool Compact
        {
            get { return compact; }
            set
            {
                if (compact == value) return;
                compact = value;
                Invalidate();
            }
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

        bool IconOnly
        {
            get { return Glyph != Glyph.None && (compact || string.IsNullOrEmpty(Text)) && Kind != ButtonKind.Chip; }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int h = Height > 0 ? Height : Dpi.S(32);
            if (IconOnly) return new Size(h + (Trailing != Glyph.None ? Dpi.S(14) : 0), h);
            int w = Dpi.S(Kind == ButtonKind.Chip ? 12 : 12);
            if (Glyph != Glyph.None) w += Dpi.S(16) + Dpi.S(8);
            if (Kind == ButtonKind.Chip) w += Dpi.S(8) + Dpi.S(7);
            bool showText = !(Kind == ButtonKind.Chip && compact);
            if (showText) w += Draw.Measure(Text, Font).Width;
            if (Count.Length > 0) w += (showText ? Dpi.S(6) : 0) + Draw.Measure(Count, Fonts.UiSemibold).Width;
            if (Trailing != Glyph.None) w += Dpi.S(6) + Dpi.S(14);
            w += Dpi.S(Kind == ButtonKind.Chip ? 12 : 12);
            return new Size(w, h);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (Parent != null) Parent.PerformLayout();
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            if (Toggle) Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color surface = Surface;
            g.Clear(surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            float rad = Kind == ButtonKind.Chip ? Height / 2f : Dpi.F(4);
            bool en = Enabled;
            Color fill = Color.Empty, stroke = Color.Empty, fg = theme.Text, glyphC = theme.Text2;
            switch (Kind)
            {
                case ButtonKind.Accent:
                    fill = !en ? Theme.Mix(surface, theme.Text3, 0.35f) : pressed ? Theme.Mix(theme.Accent, surface, 0.2f) : hot ? theme.AccentHover : theme.Accent;
                    fg = glyphC = en ? theme.OnAccent : theme.Card;
                    break;
                case ButtonKind.Standard:
                    {
                        Color b = theme.IsDark ? Theme.Hex(0x2C3037) : Theme.Hex(0xFBFCFD);
                        if (surface == theme.Card) b = theme.IsDark ? Theme.Hex(0x31353D) : Theme.Hex(0xFFFFFF);
                        fill = pressed ? Theme.Over(b, theme.Pressed) : hot ? Theme.Over(b, theme.Hover) : b;
                        stroke = theme.FieldStroke;
                        if (isChecked)
                        {
                            fill = Theme.Mix(surface, theme.Accent, theme.IsDark ? 0.2f : 0.13f);
                            stroke = Theme.Mix(fill, theme.Accent, 0.45f);
                            fg = glyphC = theme.Accent;
                        }
                        break;
                    }
                case ButtonKind.Subtle:
                    fill = pressed ? Theme.Over(surface, theme.Pressed) : hot ? Theme.Over(surface, theme.Hover) : Color.Empty;
                    if (isChecked)
                    {
                        fill = Theme.Mix(surface, theme.Accent, (theme.IsDark ? 0.2f : 0.13f) + (hot ? 0.04f : 0f));
                        fg = glyphC = theme.Accent;
                    }
                    break;
                case ButtonKind.Chip:
                    {
                        Color sevFg = Severity == 3 ? theme.ErrFg : Severity == 2 ? theme.WarnFg : theme.Text3;
                        Color sevBg = Severity == 3 ? theme.ErrBg : Severity == 2 ? theme.WarnBg : theme.CardAlt;
                        if (isChecked)
                        {
                            fill = hot ? Theme.Mix(sevBg, sevFg, 0.12f) : sevBg;
                            stroke = Theme.Mix(sevBg, sevFg, theme.IsDark ? 0.35f : 0.3f);
                        }
                        else
                        {
                            fill = hot ? Theme.Over(surface, theme.Hover) : Color.Empty;
                            stroke = theme.Divider;
                            fg = theme.Text3;
                        }
                        glyphC = sevFg;
                        break;
                    }
            }
            if (!en)
            {
                fg = theme.Text3;
                if (Kind != ButtonKind.Accent) glyphC = theme.Text3;
            }
            if (GlyphColor != Color.Empty && en && !isChecked) glyphC = GlyphColor;
            if (pulse > 0) fill = Theme.Mix(fill.IsEmpty ? surface : fill, theme.OkFg, pulse * 0.25f);
            if (!fill.IsEmpty) Draw.Fill(g, r, rad, fill);
            if (!stroke.IsEmpty) Draw.Stroke(g, r, rad, stroke, 1f);

            int x;
            int gs = Dpi.S(16);
            if (IconOnly)
            {
                int iconArea = Height;
                Draw.Icon(g, Glyph, new RectangleF((iconArea - gs) / 2f, (Height - gs) / 2f, gs, gs), glyphC);
                if (Trailing != Glyph.None) Draw.Icon(g, Trailing, new RectangleF(iconArea - Dpi.S(4), (Height - Dpi.S(12)) / 2f, Dpi.S(12), Dpi.S(12)), theme.Text2);
            }
            else
            {
                x = Dpi.S(12);
                if (Glyph != Glyph.None)
                {
                    Draw.Icon(g, Glyph, new RectangleF(x, (Height - gs) / 2f, gs, gs), glyphC);
                    x += gs + Dpi.S(8);
                }
                if (Kind == ButtonKind.Chip)
                {
                    float d = Dpi.F(8);
                    RectangleF dot = new RectangleF(x, (Height - d) / 2f, d, d);
                    SmoothingMode old = g.SmoothingMode;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    if (isChecked)
                    {
                        using (SolidBrush b = new SolidBrush(glyphC)) g.FillEllipse(b, dot);
                    }
                    else
                    {
                        using (Pen p = new Pen(glyphC, Dpi.F(1.5f))) g.DrawEllipse(p, dot.X + 0.75f, dot.Y + 0.75f, dot.Width - 1.5f, dot.Height - 1.5f);
                    }
                    g.SmoothingMode = old;
                    x += (int)d + Dpi.S(7);
                }
                int right = Width - Dpi.S(12) - (Trailing != Glyph.None ? Dpi.S(20) : 0);
                bool showText = !(Kind == ButtonKind.Chip && compact);
                if (showText && !string.IsNullOrEmpty(Text))
                {
                    int tw = Draw.Measure(Text, Font).Width;
                    Draw.Text(g, Text, Font, new Rectangle(x, 0, Math.Max(0, Math.Min(tw + 2, right - x)), Height), fg, Draw.Line);
                    x += tw + Dpi.S(6);
                }
                if (Count.Length > 0)
                {
                    Draw.Text(g, Count, Fonts.UiSemibold, new Rectangle(x, 0, Math.Max(0, right - x + Dpi.S(4)), Height), fg, Draw.Line);
                }
                if (Trailing != Glyph.None)
                {
                    int ts = Dpi.S(14);
                    Draw.Icon(g, Trailing, new RectangleF(Width - Dpi.S(10) - ts, (Height - ts) / 2f, ts, ts), Kind == ButtonKind.Accent ? fg : theme.Text2);
                }
            }
            if (Toggle && isChecked && Glyph == Glyph.Live)
            {
                float d = Dpi.F(6);
                float cx = IconOnly ? (Height + gs) / 2f - d / 2f : Dpi.S(12) + gs - d / 2f;
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(theme.OkFg)) g.FillEllipse(b, cx, (Height - gs) / 2f - d / 3f, d, d);
                g.SmoothingMode = old;
            }
            if (Focused && ShowFocusCues)
                Draw.Stroke(g, r, rad, theme.Text, Dpi.F(2));
        }
    }

    public sealed class BarSep : FluentControl
    {
        public BarSep()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Size = new Size(Dpi.S(13), Dpi.S(20));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Surface);
            using (Pen p = new Pen(theme.Divider)) e.Graphics.DrawLine(p, Width / 2, 0, Width / 2, Height);
        }
    }

    public sealed class HLine : FluentControl
    {
        public HLine()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(17);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Surface);
            using (Pen p = new Pen(theme.Divider)) e.Graphics.DrawLine(p, 0, Height / 2, Width, Height / 2);
        }
    }

    public sealed class FluentTextBox : FluentControl
    {
        public readonly TextBox Box;
        public Glyph Glyph;
        string badge = "";
        bool invalid, clearHot;
        readonly string cue;

        public FluentTextBox(Glyph glyph, string cue)
        {
            Glyph = glyph;
            this.cue = cue;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.IBeam;
            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Fonts.Ui;
            Controls.Add(Box);
            Height = Dpi.S(32);
            Box.GotFocus += delegate { UpdateColors(); Invalidate(); };
            Box.LostFocus += delegate { UpdateColors(); Invalidate(); };
            Box.MouseEnter += delegate { hot = true; UpdateColors(); Invalidate(); };
            Box.MouseLeave += delegate { hot = ClientRectangle.Contains(PointToClient(MousePosition)); UpdateColors(); Invalidate(); };
            Box.TextChanged += delegate (object s, EventArgs e) { LayoutBox(); Invalidate(); OnTextChanged(e); };
            Box.HandleCreated += delegate { NativeMethods.Cue(Box, this.cue); };
            UpdateColors();
        }

        public override string Text
        {
            get { return Box == null ? "" : Box.Text; }
            set { if (Box != null) Box.Text = value; }
        }

        public string Badge
        {
            get { return badge; }
            set
            {
                value = value ?? "";
                if (badge == value) return;
                badge = value;
                LayoutBox();
                Invalidate();
            }
        }

        public bool Invalid
        {
            get { return invalid; }
            set
            {
                if (invalid == value) return;
                invalid = value;
                Invalidate();
            }
        }

        public override void ApplyTheme(Theme t)
        {
            theme = t;
            UpdateColors();
            Invalidate();
        }

        Color FillColor()
        {
            if (Box != null && Box.Focused) return theme.FieldFocus;
            return hot ? Theme.Over(theme.Field, theme.Hover) : theme.Field;
        }

        void UpdateColors()
        {
            Color f = FillColor();
            if (Box.BackColor != f) Box.BackColor = f;
            if (Box.ForeColor != theme.Text) Box.ForeColor = theme.Text;
        }

        Rectangle ClearRect()
        {
            int s = Dpi.S(24);
            return new Rectangle(Width - Dpi.S(4) - s, (Height - s) / 2, s, s);
        }

        void LayoutBox()
        {
            if (Box == null) return;
            int left = Glyph != Glyph.None ? Dpi.S(36) : Dpi.S(11);
            int right = Dpi.S(8);
            if (Box.Text.Length > 0) right = Dpi.S(30);
            if (badge.Length > 0) right += Draw.Measure(badge, Fonts.Small).Width + Dpi.S(8);
            int h = Box.PreferredHeight;
            Box.SetBounds(left, (Height - h) / 2 + Dpi.S(1), Math.Max(Dpi.S(20), Width - left - right), h);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutBox();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBox();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            UpdateColors();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            clearHot = false;
            UpdateColors();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool h = Box.Text.Length > 0 && ClearRect().Contains(e.Location);
            if (h != clearHot) { clearHot = h; Invalidate(); }
            Cursor = h ? Cursors.Default : Cursors.IBeam;
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Box.Text.Length > 0 && ClearRect().Contains(e.Location)) Box.Text = "";
            Box.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            float rad = Dpi.F(4);
            bool focus = Box.Focused;
            Draw.Fill(g, r, rad, FillColor());
            Draw.Stroke(g, r, rad, invalid ? theme.ErrFg : theme.FieldStroke, 1f);
            using (GraphicsPath clip = Draw.Round(r, rad))
            {
                Region old = g.Clip;
                g.SetClip(clip);
                int th = focus ? Dpi.S(2) : 1;
                using (SolidBrush b = new SolidBrush(invalid ? theme.ErrFg : focus ? theme.Accent : theme.FieldBottom))
                    g.FillRectangle(b, 0, Height - th, Width, th);
                g.Clip = old;
            }
            if (Glyph != Glyph.None)
            {
                int s = Dpi.S(16);
                Draw.Icon(g, Glyph, new RectangleF(Dpi.S(12), (Height - s) / 2f, s, s), focus ? theme.Text2 : theme.Text3);
            }
            int rx = Width - Dpi.S(8);
            if (Box.Text.Length > 0)
            {
                Rectangle cr = ClearRect();
                if (clearHot) Draw.Fill(g, cr, Dpi.F(4), Theme.Over(FillColor(), theme.Hover));
                int s = Dpi.S(12);
                Draw.Icon(g, Glyph.Close, new RectangleF(cr.X + (cr.Width - s) / 2f, cr.Y + (cr.Height - s) / 2f, s, s), theme.Text2);
                rx = cr.X - Dpi.S(2);
            }
            if (badge.Length > 0)
            {
                int bw = Draw.Measure(badge, Fonts.Small).Width;
                Draw.Text(g, badge, Fonts.Small, new Rectangle(rx - bw, 0, bw + 1, Height), invalid ? theme.ErrFg : theme.Text3, Draw.Line);
            }
        }
    }

    public sealed class BarPanel : Panel, IThemed
    {
        public readonly List<Control> LeftItems = new List<Control>();
        public readonly List<Control> RightItems = new List<Control>();
        public Control Flex;
        public int FlexMin, FlexMax;
        public bool OnCard;
        readonly int gap = Dpi.S(4);

        public BarPanel()
        {
            DoubleBuffered = true;
        }

        public void ApplyTheme(Theme t)
        {
            BackColor = OnCard ? t.Card : t.Bg;
        }

        public void AddLeft(params Control[] cs)
        {
            foreach (Control c in cs) { Controls.Add(c); LeftItems.Add(c); }
        }

        public void AddRight(params Control[] cs)
        {
            foreach (Control c in cs) { Controls.Add(c); RightItems.Add(c); }
        }

        int W(Control c)
        {
            if (c == Flex) return FlexMin;
            FluentButton b = c as FluentButton;
            return b != null ? b.GetPreferredSize(Size.Empty).Width : c.Width;
        }

        int Sum(List<Control> list)
        {
            int w = 0;
            foreach (Control c in list) if (c.Visible) w += W(c) + gap;
            return w;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int avail = ClientSize.Width - Padding.Horizontal;
            List<FluentButton> compactable = new List<FluentButton>();
            foreach (Control c in Controls)
            {
                FluentButton b = c as FluentButton;
                if (b != null && b.CanCompact) { b.Compact = false; compactable.Add(b); }
            }
            if (Sum(LeftItems) + Sum(RightItems) + Dpi.S(16) > avail)
                foreach (FluentButton b in compactable) b.Compact = true;
            Place();
        }

        void Place()
        {
            int top = Padding.Top, h = ClientSize.Height - Padding.Vertical;
            int rw = Sum(RightItems) - gap;
            int rx = ClientSize.Width - Padding.Right - Math.Max(0, rw);
            int leftFixed = 0;
            foreach (Control c in LeftItems) if (c.Visible && c != Flex) leftFixed += W(c) + gap;
            int x = Padding.Left;
            int flexW = 0;
            if (Flex != null && Flex.Visible) flexW = Math.Max(FlexMin, Math.Min(FlexMax, rx - Dpi.S(16) - x - leftFixed - gap));
            foreach (Control c in LeftItems)
            {
                if (!c.Visible) continue;
                int w = c == Flex ? flexW : W(c);
                c.SetBounds(x, top + (h - c.Height) / 2, w, c.Height);
                x += w + gap;
            }
            foreach (Control c in RightItems)
            {
                if (!c.Visible) continue;
                int w = W(c);
                c.SetBounds(rx, top + (h - c.Height) / 2, w, c.Height);
                rx += w + gap;
            }
        }
    }

    public sealed class CardPanel : Panel, IThemed
    {
        Theme theme = Theme.Light();

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Card;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : theme.Bg);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, r, Dpi.F(8), theme.Card);
            Draw.Elevation(g, r, Dpi.F(8), theme);
            Draw.Stroke(g, r, Dpi.F(8), theme.Stroke, 1f);
        }
    }

    public sealed class SeverityBadge : FluentControl
    {
        int sev = 1;

        public SeverityBadge()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Font = Fonts.SmallSemibold;
            Set(1);
        }

        public void Set(int s)
        {
            sev = s;
            Text = s == 3 ? "Error" : s == 2 ? "Warning" : "Info";
            Size = new Size(Dpi.S(10) + Dpi.S(14) + Dpi.S(6) + Draw.Measure(Text, Font).Width + Dpi.S(12), Dpi.S(26));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            Color bg = sev == 3 ? theme.ErrBg : sev == 2 ? theme.WarnBg : theme.CardAlt;
            Color edge = sev == 3 ? theme.ErrFg : sev == 2 ? theme.WarnFg : theme.Divider;
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, r, Height / 2f, bg);
            Draw.Stroke(g, r, Height / 2f, Theme.Mix(bg, edge, sev > 1 ? 0.35f : 1f), 1f);
            int s = Dpi.S(14);
            RectangleF ir = new RectangleF(Dpi.S(10), (Height - s) / 2f, s, s);
            if (sev >= 2) Draw.Severity(g, sev, ir, theme);
            else
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(theme.Text3)) g.FillEllipse(b, ir);
                using (Pen p = new Pen(theme.OnSeverity, Dpi.F(1.5f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    g.DrawLine(p, ir.X + s / 2f, ir.Y + s * 0.45f, ir.X + s / 2f, ir.Y + s * 0.74f);
                }
                using (SolidBrush b = new SolidBrush(theme.OnSeverity)) g.FillEllipse(b, ir.X + s / 2f - Dpi.F(1), ir.Y + s * 0.2f, Dpi.F(2), Dpi.F(2));
                g.SmoothingMode = old;
            }
            Draw.Text(g, Text, Font, new Rectangle(Dpi.S(10) + s + Dpi.S(6), 0, Width, Height), theme.Text, Draw.Line);
        }
    }

    public sealed class MetaItem : FluentControl
    {
        string label, value = "";
        readonly bool mono;

        Font valueFont
        {
            get { return mono ? Fonts.Mono : Fonts.Ui; }
        }

        public MetaItem(string label, bool mono)
        {
            this.label = label;
            this.mono = mono;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Margin = new Padding(0, 0, Dpi.S(22), Dpi.S(6));
        }

        public void Set(string newLabel, string newValue)
        {
            label = newLabel;
            value = newValue ?? "";
            Visible = value.Length > 0;
            int w = Math.Max(Draw.Measure(label, Fonts.Small).Width, Draw.Measure(value, valueFont).Width) + Dpi.S(2);
            Size = new Size(w, Fonts.Small.Height + Dpi.S(1) + valueFont.Height);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            int lh = Fonts.Small.Height;
            Draw.Text(g, label, Fonts.Small, new Rectangle(0, 0, Width, lh), theme.Text3, Draw.Line);
            Draw.Text(g, value, valueFont, new Rectangle(0, lh + Dpi.S(1), Width, valueFont.Height), theme.Text, Draw.Line);
        }
    }

    public sealed class CodePanel : FluentControl
    {
        List<uint> codes = new List<uint>();
        public string Heading = "Codes in this entry";
        public string Hint = "";
        public bool ShowDivider = true;
        public event Action<uint> CodeClicked;
        readonly List<KeyValuePair<Rectangle, uint>> cardRects = new List<KeyValuePair<Rectangle, uint>>();
        int scroll, contentH, hotCard = -1;

        public CodePanel()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
        }

        public int CodeCount
        {
            get { return codes.Count; }
        }

        public void SetCodes(List<uint> list)
        {
            codes = list ?? new List<uint>();
            scroll = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, contentH - Height);
            scroll = Math.Max(0, Math.Min(max, scroll - e.Delta / 120 * Dpi.S(40)));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (contentH > Height) Focus();
        }

        int CardAt(Point p)
        {
            for (int i = 0; i < cardRects.Count; i++) if (cardRects[i].Key.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = CodeClicked != null ? CardAt(e.Location) : -1;
            if (i != hotCard)
            {
                hotCard = i;
                Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotCard = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = CardAt(e.Location);
            if (i >= 0 && e.Button == MouseButtons.Left && CodeClicked != null) CodeClicked(cardRects[i].Value);
        }

        Color Stripe(uint u)
        {
            if (u == 0 || u == 3010 || u == 1641) return theme.OkFg;
            if (u >= 0x80000000u || (u > 0 && u < 0x10000)) return theme.ErrFg;
            return theme.Text3;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            int x = 0;
            if (ShowDivider)
            {
                using (Pen p = new Pen(theme.Divider)) g.DrawLine(p, 0, Dpi.S(2), 0, Height - Dpi.S(2));
                x = Dpi.S(16);
            }
            int width = Width - x - Dpi.S(2);
            int y = -scroll;
            cardRects.Clear();
            if (codes.Count == 0)
            {
                int hh = Draw.WrapHeight(Hint, Fonts.Ui, width);
                Draw.Text(g, Hint, Fonts.Ui, new Rectangle(x, y + Dpi.S(4), width, hh + 2), theme.Text2, Draw.Wrap);
                contentH = hh + Dpi.S(8);
                return;
            }
            if (Heading.Length > 0)
            {
                Draw.Text(g, Heading, Fonts.SmallSemibold, new Rectangle(x, y, width, Fonts.Small.Height + 2), theme.Text3, Draw.Line);
                y += Fonts.Small.Height + Dpi.S(8);
            }
            int pad = Dpi.S(12);
            foreach (uint u in codes)
            {
                Codes.Info d = Codes.Describe(u);
                string desc = d.Text ?? "Not in the built-in table or the Windows message tables";
                int inner = width - pad * 2 - Dpi.S(4);
                int titleH = Fonts.Mono.Height;
                int descH = Draw.WrapHeight(desc, Fonts.Ui, inner);
                int footH = Draw.WrapHeight(d.Foot, Fonts.Small, inner);
                int h = pad + titleH + Dpi.S(4) + descH + Dpi.S(4) + footH + pad;
                Rectangle card = new Rectangle(x, y, width, h);
                cardRects.Add(new KeyValuePair<Rectangle, uint>(card, u));
                bool hc = hotCard == cardRects.Count - 1;
                Draw.Fill(g, card, Dpi.F(6), hc ? Theme.Over(theme.CardAlt, theme.Hover) : theme.CardAlt);
                Draw.Stroke(g, card, Dpi.F(6), theme.Divider, 1f);
                Draw.Fill(g, new RectangleF(x + Dpi.S(5), y + pad, Dpi.S(3), h - pad * 2), Dpi.F(1.5f), Stripe(u));
                int tx = x + pad + Dpi.S(4);
                int ty = y + pad;
                int tw = Draw.Measure(d.Title, Fonts.Mono).Width;
                Draw.Text(g, d.Title, Fonts.Mono, new Rectangle(tx, ty, inner, titleH), theme.Text, Draw.Line);
                Draw.Text(g, d.Sub, Fonts.Small, new Rectangle(tx + tw + Dpi.S(10), ty, Math.Max(0, inner - tw - Dpi.S(10)), titleH), theme.Text3, Draw.Line);
                ty += titleH + Dpi.S(4);
                Draw.Text(g, desc, Fonts.Ui, new Rectangle(tx, ty, inner, descH + 2), d.Text != null ? theme.Text : theme.Text3, Draw.Wrap);
                ty += descH + Dpi.S(4);
                Draw.Text(g, d.Foot, Fonts.Small, new Rectangle(tx, ty, inner, footH + 2), theme.Text3, Draw.Wrap);
                y += h + Dpi.S(8);
            }
            contentH = y + scroll;
            if (contentH > Height)
            {
                float track = Height - Dpi.S(8);
                float len = Math.Max(Dpi.S(24), track * Height / contentH);
                float top = Dpi.S(4) + (track - len) * scroll / Math.Max(1, contentH - Height);
                Draw.Fill(g, new RectangleF(Width - Dpi.S(3), top, Dpi.S(3), len), Dpi.F(1.5f), theme.Thumb);
            }
        }
    }

    public sealed class StatusBar : FluentControl
    {
        public string LeftText = "", MidText = "", RightText = "";
        public float Progress = -1;

        public StatusBar()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(30);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.Bg);
            if (Progress >= 0)
            {
                int th = Dpi.S(2);
                int x0 = Dpi.S(12), w = Width - Dpi.S(24);
                Draw.Fill(g, new RectangleF(x0, 0, w, th), th / 2f, theme.Divider);
                Draw.Fill(g, new RectangleF(x0, 0, Math.Max(th, w * Math.Min(1f, Progress)), th), th / 2f, theme.Accent);
            }
            Font f = Fonts.Small;
            int pad = Dpi.S(16);
            int lw = Draw.Measure(LeftText, f).Width;
            int rw = Math.Min(Draw.Measure(RightText, f).Width + 2, Width / 2);
            Draw.Text(g, LeftText, f, new Rectangle(pad, 0, lw + 2, Height), theme.Text2, Draw.Line);
            int mx = pad + lw + (lw > 0 ? Dpi.S(20) : 0);
            Draw.Text(g, MidText, f, new Rectangle(mx, 0, Math.Max(0, Width - pad - rw - Dpi.S(20) - mx), Height), theme.Text3, Draw.Line);
            Draw.Text(g, RightText, f, new Rectangle(Width - pad - rw, 0, rw, Height), theme.Text3, Draw.Line | TextFormatFlags.Right);
        }
    }

    public sealed class DropOverlay : FluentControl
    {
        public bool TwoZones;
        int hotZone = -1;

        public DropOverlay()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            AllowDrop = true;
        }

        public int HotZone
        {
            get { return hotZone; }
            set
            {
                if (hotZone == value) return;
                hotZone = value;
                Invalidate();
            }
        }

        Rectangle[] Zones()
        {
            int pad = Dpi.S(2), gap = Dpi.S(12);
            Rectangle all = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);
            if (!TwoZones) return new Rectangle[] { all };
            int w = (all.Width - gap) / 2;
            return new Rectangle[] { new Rectangle(all.X, all.Y, w, all.Height), new Rectangle(all.X + w + gap, all.Y, all.Width - w - gap, all.Height) };
        }

        public int ZoneAt(Point p)
        {
            Rectangle[] z = Zones();
            for (int i = 0; i < z.Length; i++) if (z[i].Contains(p)) return i;
            return TwoZones ? -1 : 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.Bg);
            Rectangle[] z = Zones();
            for (int i = 0; i < z.Length; i++)
            {
                bool h = i == hotZone || z.Length == 1;
                Rectangle r = z[i];
                Draw.Fill(g, r, Dpi.F(8), h ? theme.AccentSoft : theme.Card);
                Draw.Stroke(g, r, Dpi.F(8), h ? theme.Accent : theme.Divider, h ? Dpi.F(2) : 1f);
                string title = i == 0 ? "Open" : "Merge in";
                string sub = i == 0 ? (TwoZones ? "Replace what's open. Several files are merged by time." : "Several files are merged by time.") : "Add to the open logs and interleave them by time.";
                int s = Dpi.S(40);
                int cy = r.Y + r.Height / 2;
                Draw.Icon(g, i == 0 ? Glyph.Open : Glyph.Merge, new RectangleF(r.X + (r.Width - s) / 2f, cy - s - Dpi.S(14), s, s), h ? theme.Accent : theme.Text2);
                Draw.Text(g, title, Fonts.Title, new Rectangle(r.X, cy - Dpi.S(6), r.Width, Fonts.Title.Height), theme.Text, Draw.Line | TextFormatFlags.HorizontalCenter);
                Draw.Text(g, sub, Fonts.Ui, new Rectangle(r.X + Dpi.S(16), cy - Dpi.S(6) + Fonts.Title.Height + Dpi.S(4), r.Width - Dpi.S(32), Fonts.Ui.Height * 2 + 4), theme.Text2, Draw.Wrap | TextFormatFlags.HorizontalCenter);
            }
        }
    }

    public sealed class RecentItem : FluentControl
    {
        public readonly string[] Paths;
        readonly string name, detail;
        readonly bool exists;

        public RecentItem(string[] paths)
        {
            Paths = paths;
            name = Settings.RecentLabel(paths, out detail);
            exists = true;
            foreach (string p in paths)
            {
                if (!EventLogs.Available(p)) exists = false;
            }
            Height = Dpi.S(38);
            TabStop = true;
            Cursor = Cursors.Hand;
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
            if (hot || pressed) Draw.Fill(g, r, Dpi.F(4), Theme.Over(surface, pressed ? theme.Pressed : theme.Hover));
            int s = Dpi.S(16);
            Draw.Icon(g, Glyph.Recent, new RectangleF(Dpi.S(10), (Height - s) / 2f, s, s), theme.Text3);
            int x = Dpi.S(10) + s + Dpi.S(12);
            int nw = Math.Min(Draw.Measure(name, Fonts.Ui).Width + 2, (Width - x) * 2 / 3);
            Draw.Text(g, name, Fonts.Ui, new Rectangle(x, 0, nw, Height), exists ? theme.Text : theme.Text3, Draw.Line);
            x += nw + Dpi.S(12);
            Draw.Text(g, exists ? detail : "Not found · " + detail, Fonts.Small, new Rectangle(x, 0, Math.Max(0, Width - x - Dpi.S(10)), Height), theme.Text3, Draw.Line | TextFormatFlags.PathEllipsis);
            if (Focused && ShowFocusCues) Draw.Stroke(g, r, Dpi.F(4), theme.Text, Dpi.F(2));
        }
    }

    public sealed class StartPage : FluentControl
    {
        public readonly FluentButton OpenButton, BrowseButton, ResumeButton;
        public string Eyebrow;
        readonly List<LocationCard> cards = new List<LocationCard>();
        readonly List<RecentItem> recent = new List<RecentItem>();
        public event Action<string[]> OpenRecent;
        public event Action<LogLocation> OpenLocation;
        int markY, titleY, descY, descH, locY, recentY, footY, contentH, scrollY;
        bool searching = true;
        const string Desc = "Drop log files anywhere in this window, or press Ctrl+O. Hold Ctrl while dropping to merge with what is already open.";
        const string Foot = "Reads CMTrace and Intune IME, SMS trace, CBS, DISM and Panther, Windows Update ETL traces, Windows event logs and plain text logs. Files are opened read-only and never leave this computer.";

        public StartPage()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            AllowDrop = true;
            OpenButton = new FluentButton(ButtonKind.Accent, Glyph.Open, "Open logs");
            OpenButton.Width = OpenButton.GetPreferredSize(Size.Empty).Width;
            BrowseButton = new FluentButton(ButtonKind.Standard, Glyph.Browse, "Browse logs");
            BrowseButton.Tip = "Find ConfigMgr, Intune and Windows logs on this or another computer (Ctrl+B)";
            BrowseButton.Width = BrowseButton.GetPreferredSize(Size.Empty).Width;
            ResumeButton = new FluentButton(ButtonKind.Standard, Glyph.Back, "Back to the log");
            ResumeButton.Tip = "Return to the logs that are open (Esc)";
            ResumeButton.Visible = false;
            Controls.Add(OpenButton);
            Controls.Add(BrowseButton);
            Controls.Add(ResumeButton);
        }

        public void SetResume(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                ResumeButton.Visible = false;
            }
            else
            {
                if (label.Length > 44) label = label.Substring(0, 43) + "\u2026";
                ResumeButton.Text = "Back to " + label;
                ResumeButton.Width = ResumeButton.GetPreferredSize(Size.Empty).Width;
                ResumeButton.Visible = true;
            }
            PerformLayout();
            Invalidate();
        }

        public IEnumerable<FluentButton> Buttons
        {
            get
            {
                yield return OpenButton;
                yield return BrowseButton;
                yield return ResumeButton;
            }
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

        public void SetRecent(List<string[]> list)
        {
            SuspendLayout();
            foreach (RecentItem r in recent) { Controls.Remove(r); r.Dispose(); }
            recent.Clear();
            int n = 0;
            foreach (string[] paths in list)
            {
                if (n++ >= 5) break;
                RecentItem item = new RecentItem(paths);
                item.ApplyTheme(theme);
                string[] p = paths;
                item.Click += delegate { if (OpenRecent != null) OpenRecent(p); };
                recent.Add(item);
                Controls.Add(item);
            }
            ResumeLayout(true);
            Invalidate();
        }

        public override void ApplyTheme(Theme t)
        {
            base.ApplyTheme(t);
            BackColor = t.Card;
            foreach (Control c in Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
            }
        }

        int ColumnWidth()
        {
            int cw = Math.Min(Dpi.S(760), Width - Dpi.S(64));
            if (cw < Dpi.S(200)) cw = Math.Max(0, Width - Dpi.S(32));
            return cw;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int cw = ColumnWidth();
            int x0 = (Width - cw) / 2;
            descH = Draw.WrapHeight(Desc, Fonts.Ui, cw);
            int cols = cw >= Dpi.S(560) ? 2 : 1;
            int gap = Dpi.S(8);
            int cardW = (cw - gap * (cols - 1)) / cols, cardH = Dpi.S(62);
            int cardRows = (cards.Count + cols - 1) / cols;
            int locBlock = Dpi.S(28) + Fonts.SmallSemibold.Height + Dpi.S(8) + (cards.Count > 0 ? cardRows * (cardH + gap) - gap : Fonts.Ui.Height);
            int itemH = Dpi.S(38);
            int recentBlock = recent.Count > 0 ? Dpi.S(28) + Fonts.SmallSemibold.Height + Dpi.S(8) + recent.Count * itemH : 0;
            int footH = Draw.WrapHeight(Foot, Fonts.Small, cw);
            int total = Dpi.S(64) + Dpi.S(18) + Fonts.SmallSemibold.Height + Dpi.S(4) + Fonts.Hero.Height + Dpi.S(8) + descH + Dpi.S(20) + OpenButton.Height + locBlock + recentBlock + Dpi.S(28) + footH + Dpi.S(28);
            if (ResumeButton.Visible && x0 + OpenButton.Width + BrowseButton.Width + Dpi.S(16) + ResumeButton.Width > x0 + cw) total += OpenButton.Height + Dpi.S(8);
            contentH = total + Dpi.S(28);
            int max = Math.Max(0, contentH - Height);
            if (scrollY > max) scrollY = max;
            int y = Math.Max(Dpi.S(28), (Height - total) / 2) - scrollY;
            markY = y;
            y += Dpi.S(64) + Dpi.S(18) + Fonts.SmallSemibold.Height + Dpi.S(4);
            titleY = y;
            y += Fonts.Hero.Height + Dpi.S(8);
            descY = y;
            y += descH + Dpi.S(20);
            OpenButton.SetBounds(x0, y, OpenButton.Width, OpenButton.Height);
            BrowseButton.SetBounds(x0 + OpenButton.Width + Dpi.S(8), y, BrowseButton.Width, BrowseButton.Height);
            if (ResumeButton.Visible)
            {
                int rx = x0 + OpenButton.Width + BrowseButton.Width + Dpi.S(16);
                if (rx + ResumeButton.Width > x0 + cw) { y += OpenButton.Height + Dpi.S(8); rx = x0; }
                ResumeButton.SetBounds(rx, y, Math.Min(ResumeButton.Width, cw), ResumeButton.Height);
            }
            y += OpenButton.Height + Dpi.S(28);
            locY = y;
            y += Fonts.SmallSemibold.Height + Dpi.S(8);
            for (int i = 0; i < cards.Count; i++)
            {
                int c = i % cols, r = i / cols;
                cards[i].SetBounds(x0 + c * (cardW + gap), y + r * (cardH + gap), cardW, cardH);
            }
            y += cards.Count > 0 ? cardRows * (cardH + gap) - gap : Fonts.Ui.Height;
            if (recent.Count > 0)
            {
                y += Dpi.S(28);
                recentY = y;
                y += Fonts.SmallSemibold.Height + Dpi.S(8);
                foreach (RecentItem r in recent)
                {
                    r.SetBounds(x0 - Dpi.S(10), y, cw + Dpi.S(20), itemH);
                    y += itemH;
                }
            }
            footY = y + Dpi.S(28);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF card = new RectangleF(0, 0, Width, Height);
            Draw.Fill(g, card, Dpi.F(8), theme.Card);
            using (System.Drawing.Drawing2D.GraphicsPath clip = Draw.Round(card, Dpi.F(8)))
            {
                Region old = g.Clip;
                g.SetClip(clip);
                float gh = Math.Max(Dpi.S(80), titleY + Fonts.Hero.Height + Dpi.S(40));
                RectangleF band = new RectangleF(0, 0, Width, gh);
                using (System.Drawing.Drawing2D.LinearGradientBrush lb = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(0, -1, Width, gh + 2), theme.Glow, theme.Card, 90f))
                    g.FillRectangle(lb, band);
                g.Clip = old;
            }
            int cw = ColumnWidth();
            int x0 = (Width - cw) / 2;
            Draw.AppMark(g, new RectangleF(x0, markY, Dpi.S(64), Dpi.S(64)), theme);
            Draw.Text(g, Eyebrow ?? ("LOGVICE  \u00B7  " + Program.Version), Fonts.SmallSemibold, new Rectangle(x0, titleY - Fonts.SmallSemibold.Height - Dpi.S(4), cw, Fonts.SmallSemibold.Height + 2), theme.Accent, Draw.Line);
            Draw.Text(g, ResumeButton.Visible ? "Open another log" : "Open a log to get started", Fonts.Hero, new Rectangle(x0, titleY, cw, Fonts.Hero.Height), theme.Text, Draw.Line);
            Draw.Text(g, Desc, Fonts.Ui, new Rectangle(x0, descY, cw, descH + 2), theme.Text2, Draw.Wrap);
            Draw.Text(g, "Log folders on this computer", Fonts.SmallSemibold, new Rectangle(x0, locY, cw, Fonts.SmallSemibold.Height + 2), theme.Text3, Draw.Line);
            if (cards.Count == 0)
                Draw.Text(g, searching ? "Looking for ConfigMgr, Intune and Windows log folders\u2026" : "No known log folders here. Use Browse logs to look on another computer, or add your own folder in Settings.", Fonts.Ui,
                    new Rectangle(x0, locY + Fonts.SmallSemibold.Height + Dpi.S(8), cw, Fonts.Ui.Height + 2), theme.Text2, Draw.Line);
            if (recent.Count > 0)
                Draw.Text(g, "Recent", Fonts.SmallSemibold, new Rectangle(x0, recentY, cw, Fonts.SmallSemibold.Height + 2), theme.Text3, Draw.Line);
            int footH = Draw.WrapHeight(Foot, Fonts.Small, cw);
            Draw.Text(g, Foot, Fonts.Small, new Rectangle(x0, footY, cw, footH + 2), theme.Text3, Draw.Wrap);
            if (contentH > Height)
            {
                float track = Height - Dpi.S(16);
                float len = Math.Max(Dpi.S(28), track * Height / contentH);
                float top = Dpi.S(8) + (track - len) * scrollY / Math.Max(1, contentH - Height);
                Draw.Fill(g, new RectangleF(Width - Dpi.S(6), top, Dpi.S(3), len), Dpi.F(1.5f), theme.Thumb);
            }
            Draw.Elevation(g, card, Dpi.F(8), theme);
            Draw.Stroke(g, card, Dpi.F(8), theme.Stroke, 1f);
        }
    }

    public sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public Theme Theme = Theme.Light();

        public MenuRenderer() : base(new ProfessionalColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Card)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(Theme.IsDark ? Theme.Hex(0x454545) : Theme.Hex(0xD6D6D6)))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle r = new Rectangle(Dpi.S(4), 1, e.Item.Width - Dpi.S(8), e.Item.Height - 2);
            Draw.Fill(e.Graphics, r, Dpi.F(4), Theme.Over(Theme.Card, Theme.Hover));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
            bool shortcut = mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;
            e.TextColor = !e.Item.Enabled ? Theme.Text3 : shortcut ? Theme.Text3 : Theme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Theme.Divider)) e.Graphics.DrawLine(p, Dpi.S(8), y, e.Item.Width - Dpi.S(8), y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            int s = Dpi.S(14);
            Draw.Icon(e.Graphics, Glyph.Check, new RectangleF(r.X + (r.Width - s) / 2f, r.Y + (r.Height - s) / 2f, s, s), Theme.Accent);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Text2;
            base.OnRenderArrow(e);
        }
    }
}
