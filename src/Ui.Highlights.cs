namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    public sealed class HighlightRule
    {
        public bool On = true;
        public string Label = "";
        public string Pattern = "";
        public bool IsRegex;
        public Color Color = Palette[0];
        public Regex Rx;

        public static readonly Color[] Palette =
        {
            Theme.Hex(0x0078D4), Theme.Hex(0x00B7C3), Theme.Hex(0x10893E), Theme.Hex(0x8CBD18), Theme.Hex(0xFFB900),
            Theme.Hex(0xF7630C), Theme.Hex(0xE81123), Theme.Hex(0xE3008C), Theme.Hex(0x8764B8), Theme.Hex(0x7A7574)
        };

        public static readonly string[] PaletteNames = { "Blue", "Teal", "Green", "Lime", "Yellow", "Orange", "Red", "Pink", "Purple", "Grey" };

        public HighlightRule Clone()
        {
            HighlightRule r = new HighlightRule();
            r.On = On;
            r.Label = Label;
            r.Pattern = Pattern;
            r.IsRegex = IsRegex;
            r.Color = Color;
            r.Compile();
            return r;
        }

        public string Name
        {
            get { return Label.Length > 0 ? Label : Pattern; }
        }

        public bool Compile()
        {
            Rx = null;
            if (string.IsNullOrEmpty(Pattern)) return false;
            try
            {
                Rx = new Regex(IsRegex ? Pattern : Regex.Escape(Pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public bool Matches(string msg, string comp)
        {
            if (Rx == null) return false;
            if (msg != null && Rx.IsMatch(msg)) return true;
            return comp != null && Rx.IsMatch(comp);
        }

        public Color Tint(Theme t, Color under)
        {
            return Theme.Mix(under, Color, t.IsDark ? 0.30f : 0.20f);
        }

        string Encode()
        {
            return (On ? "1" : "0") + "|" + (Color.ToArgb() & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture) + "|" + (IsRegex ? "1" : "0") + "|" +
                Uri.EscapeDataString(Label) + "|" + Uri.EscapeDataString(Pattern);
        }

        static HighlightRule Decode(string s)
        {
            string[] p = s.Split('|');
            if (p.Length < 5) return null;
            HighlightRule r = new HighlightRule();
            r.On = p[0] == "1";
            int rgb;
            if (int.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) r.Color = Theme.Hex(rgb);
            r.IsRegex = p[2] == "1";
            try
            {
                r.Label = Uri.UnescapeDataString(p[3]);
                r.Pattern = Uri.UnescapeDataString(p[4]);
            }
            catch (Exception) { return null; }
            r.Compile();
            return r.Pattern.Length > 0 ? r : null;
        }

        public static List<HighlightRule> Load()
        {
            List<HighlightRule> list = new List<HighlightRule>();
            foreach (string e in Settings.GetList("Highlights"))
            {
                HighlightRule r = Decode(e);
                if (r != null && list.Count < 200) list.Add(r);
            }
            return list;
        }

        public static void Save(List<HighlightRule> list)
        {
            List<string> v = new List<string>();
            foreach (HighlightRule r in list) v.Add(r.Encode());
            Settings.SetList("Highlights", v);
        }

        public static List<HighlightRule> Examples()
        {
            List<HighlightRule> list = new List<HighlightRule>();
            list.Add(Make("Exit code", @"exit ?code", true, 0));
            list.Add(Make("Detection", @"detect(ed|ion)", true, 8));
            list.Add(Make("Download", @"download", false, 1));
            list.Add(Make("Restart", @"reboot|restart", true, 5));
            list.Add(Make("Success", @"succe(ss|eded)", true, 2));
            return list;
        }

        static HighlightRule Make(string label, string pattern, bool rx, int color)
        {
            HighlightRule r = new HighlightRule();
            r.Label = label;
            r.Pattern = pattern;
            r.IsRegex = rx;
            r.Color = Palette[color];
            r.Compile();
            return r;
        }
    }

    public sealed class Swatches : FluentControl
    {
        Color value = HighlightRule.Palette[0];
        int hotIdx = -1;
        public event EventHandler Changed;

        public Swatches()
        {
            TabStop = true;
            Height = Dpi.S(34);
            Width = Count * Cell + Dpi.S(4);
        }

        static int Count
        {
            get { return HighlightRule.Palette.Length + 1; }
        }

        static int Cell
        {
            get { return Dpi.S(34); }
        }

        public Color Value
        {
            get { return value; }
            set { this.value = value; Invalidate(); }
        }

        int IndexAt(Point p)
        {
            int i = (p.X - Dpi.S(2)) / Cell;
            return i >= 0 && i < Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
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
            int i = IndexAt(e.Location);
            if (i < 0 || e.Button != MouseButtons.Left) return;
            Pick(i);
        }

        void Pick(int i)
        {
            if (i < HighlightRule.Palette.Length) value = HighlightRule.Palette[i];
            else
            {
                using (ColorDialog d = new ColorDialog())
                {
                    d.FullOpen = true;
                    d.Color = value;
                    if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                    value = Color.FromArgb(255, d.Color);
                }
            }
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k == Keys.Left || k == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int cur = Array.IndexOf(HighlightRule.Palette, value);
            if (e.KeyCode == Keys.Left && cur > 0) { Pick(cur - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Right && cur >= 0 && cur < HighlightRule.Palette.Length - 1) { Pick(cur + 1); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            bool known = false;
            for (int i = 0; i < Count; i++)
            {
                RectangleF r = new RectangleF(Dpi.S(2) + i * Cell + Dpi.S(4), Dpi.S(4), Cell - Dpi.S(8), Height - Dpi.S(8));
                bool custom = i == HighlightRule.Palette.Length;
                bool sel;
                if (!custom)
                {
                    sel = HighlightRule.Palette[i].ToArgb() == value.ToArgb();
                    if (sel) known = true;
                    Draw.Fill(g, r, r.Height / 2f, HighlightRule.Palette[i]);
                }
                else
                {
                    sel = !known;
                    Draw.Fill(g, r, r.Height / 2f, sel ? value : theme.CardAlt);
                    Draw.Stroke(g, r, r.Height / 2f, theme.FieldStroke, 1f);
                    int s = Dpi.S(12);
                    Draw.Icon(g, Glyph.Plus, new RectangleF(r.X + (r.Width - s) / 2f, r.Y + (r.Height - s) / 2f, s, s), sel ? Color.White : theme.Text2);
                }
                if (sel) Draw.Stroke(g, RectangleF.Inflate(r, Dpi.F(3), Dpi.F(3)), r.Height / 2f + Dpi.F(3), theme.Text, Dpi.F(2));
                else if (i == hotIdx) Draw.Stroke(g, RectangleF.Inflate(r, Dpi.F(2), Dpi.F(2)), r.Height / 2f + Dpi.F(2), theme.Text3, 1f);
            }
            if (Focused && ShowFocusCues) Draw.Stroke(g, new RectangleF(0, 0, Width - 1, Height - 1), Dpi.F(4), theme.Accent, 1f);
        }
    }

    public sealed class HighlightPreview : FluentControl
    {
        public HighlightRule Rule;

        public HighlightPreview()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Color bg = Rule != null ? Rule.Tint(theme, theme.Card) : theme.Card;
            Draw.Fill(g, r, Dpi.F(6), bg);
            Draw.Stroke(g, r, Dpi.F(6), theme.Divider, 1f);
            if (Rule != null) Draw.Fill(g, new RectangleF(Dpi.S(6), Dpi.S(9), Dpi.F(3), Height - Dpi.S(18)), Dpi.F(1.5f), Rule.Color);
            string label = Rule != null ? Rule.Name : "";
            int lw = 0;
            if (label.Length > 0)
            {
                lw = Math.Min(Draw.Measure(label, Fonts.SmallSemibold).Width + Dpi.S(16), Width / 3);
                RectangleF pill = new RectangleF(Width - lw - Dpi.S(10), (Height - Dpi.S(22)) / 2f, lw, Dpi.S(22));
                Draw.Fill(g, pill, pill.Height / 2f, Rule.Color);
                Color on = Rule.Color.GetBrightness() > 0.55f ? Color.Black : Color.White;
                Draw.Text(g, label, Fonts.SmallSemibold, Rectangle.Round(pill), on, Draw.Line | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                lw += Dpi.S(20);
            }
            Draw.Text(g, "Process 4242 terminated with exitcode: 0. Detection after install: detected", Fonts.Mono, new Rectangle(Dpi.S(18), 0, Math.Max(0, Width - Dpi.S(18) - lw), Height), theme.Text, Draw.Line | TextFormatFlags.EndEllipsis);
        }
    }

    public sealed class HighlightEditor : Form
    {
        readonly FluentTextBox tLabel, tPattern;
        readonly FluentButton bRegex, bSave, bCancel;
        readonly Swatches swatches;
        readonly HighlightPreview preview;
        readonly Label err;
        readonly Theme theme;
        public HighlightRule Result;

        public HighlightEditor(HighlightRule rule, Theme t, bool isNew)
        {
            theme = t;
            Text = isNew ? "Add highlight" : "Edit highlight";
            Font = Fonts.Ui;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            BackColor = t.Bg;
            ClientSize = new Size(Dpi.S(520), Dpi.S(420));
            int x = Dpi.S(24), w = ClientSize.Width - Dpi.S(48), y = Dpi.S(20);

            Caption("Label", "Shown in the detail pane and in the highlight filter.", x, ref y, w);
            tLabel = new FluentTextBox(Glyph.None, "For example Detection");
            tLabel.SetBounds(x, y, w, Dpi.S(32));
            tLabel.Text = rule.Label;
            Controls.Add(tLabel);
            y += Dpi.S(32) + Dpi.S(16);

            Caption("Match", "Rows whose message or component contains this text. Not case sensitive.", x, ref y, w);
            tPattern = new FluentTextBox(Glyph.Search, "Text to look for");
            tPattern.Box.Font = Fonts.Mono;
            bRegex = new FluentButton(ButtonKind.Standard, Glyph.None, ".*");
            bRegex.Font = Fonts.Mono;
            bRegex.Toggle = true;
            bRegex.Width = bRegex.GetPreferredSize(Size.Empty).Width;
            bRegex.Checked = rule.IsRegex;
            tPattern.SetBounds(x, y, w - bRegex.Width - Dpi.S(6), Dpi.S(32));
            bRegex.SetBounds(x + w - bRegex.Width, y, bRegex.Width, Dpi.S(32));
            tPattern.Text = rule.Pattern;
            Controls.Add(tPattern);
            Controls.Add(bRegex);
            y += Dpi.S(32) + Dpi.S(4);
            err = new Label();
            err.AutoSize = false;
            err.SetBounds(x, y, w, Fonts.Small.Height + Dpi.S(2));
            err.Font = Fonts.Small;
            err.ForeColor = t.ErrFg;
            err.BackColor = t.Bg;
            Controls.Add(err);
            y += Fonts.Small.Height + Dpi.S(10);

            Caption("Colour", "", x, ref y, w);
            swatches = new Swatches();
            swatches.Location = new Point(x - Dpi.S(2), y);
            swatches.Value = rule.Color;
            Controls.Add(swatches);
            y += swatches.Height + Dpi.S(16);

            Caption("Preview", "", x, ref y, w);
            preview = new HighlightPreview();
            preview.SetBounds(x, y, w, Dpi.S(40));
            Controls.Add(preview);
            y += Dpi.S(40) + Dpi.S(24);

            bCancel = new FluentButton(ButtonKind.Standard, Glyph.None, "Cancel");
            bCancel.Width = bCancel.GetPreferredSize(Size.Empty).Width;
            bSave = new FluentButton(ButtonKind.Accent, Glyph.None, isNew ? "Add" : "Save");
            bSave.Width = bSave.GetPreferredSize(Size.Empty).Width;
            bSave.SetBounds(x + w - bSave.Width - bCancel.Width - Dpi.S(8), y, bSave.Width, Dpi.S(32));
            bCancel.SetBounds(x + w - bCancel.Width, y, bCancel.Width, Dpi.S(32));
            Controls.Add(bSave);
            Controls.Add(bCancel);
            ClientSize = new Size(ClientSize.Width, y + Dpi.S(32) + Dpi.S(20));

            foreach (Control c in Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
            }
            tLabel.TextChanged += delegate { UpdatePreview(); };
            tPattern.TextChanged += delegate { UpdatePreview(); };
            bRegex.CheckedChanged += delegate { UpdatePreview(); };
            swatches.Changed += delegate { UpdatePreview(); };
            bSave.Click += delegate { Commit(); };
            bCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            UpdatePreview();
        }

        void Caption(string title, string sub, int x, ref int y, int w)
        {
            Label l = new Label();
            l.AutoSize = false;
            l.Text = title;
            l.Font = Fonts.UiSemibold;
            l.ForeColor = theme.Text;
            l.BackColor = theme.Bg;
            l.SetBounds(x, y, w, Fonts.UiSemibold.Height + Dpi.S(2));
            Controls.Add(l);
            y += Fonts.UiSemibold.Height + Dpi.S(2);
            if (sub.Length > 0)
            {
                Label d = new Label();
                d.AutoSize = false;
                d.Text = sub;
                d.Font = Fonts.Small;
                d.ForeColor = theme.Text2;
                d.BackColor = theme.Bg;
                d.SetBounds(x, y, w, Fonts.Small.Height + Dpi.S(2));
                Controls.Add(d);
                y += Fonts.Small.Height + Dpi.S(2);
            }
            y += Dpi.S(6);
        }

        HighlightRule Current()
        {
            HighlightRule r = new HighlightRule();
            r.Label = tLabel.Text.Trim();
            r.Pattern = tPattern.Text;
            r.IsRegex = bRegex.Checked;
            r.Color = swatches.Value;
            return r;
        }

        void UpdatePreview()
        {
            HighlightRule r = Current();
            bool ok = r.Compile();
            bool empty = r.Pattern.Length == 0;
            tPattern.Invalid = !ok && !empty;
            err.Text = !ok && !empty ? "This is not a valid regular expression." : "";
            bSave.Enabled = ok;
            preview.Rule = r;
            preview.Invalidate();
        }

        void Commit()
        {
            HighlightRule r = Current();
            if (!r.Compile()) return;
            Result = r;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (tPattern.Text.Length == 0) tPattern.Box.Focus(); else tLabel.Box.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            if (keyData == Keys.Enter && bSave.Enabled) { Commit(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    public sealed class ButtonStrip : Panel, IThemed
    {
        public ButtonStrip(params Control[] items)
        {
            int x = 0, h = 0;
            foreach (Control c in items)
            {
                FluentButton b = c as FluentButton;
                if (b != null) b.Width = b.GetPreferredSize(Size.Empty).Width;
                c.Location = new Point(x, 0);
                x += c.Width + Dpi.S(6);
                h = Math.Max(h, c.Height);
                Controls.Add(c);
            }
            foreach (Control c in items) c.Top = (h - c.Height) / 2;
            Size = new Size(Math.Max(0, x - Dpi.S(6)), h);
        }

        public void ApplyTheme(Theme t)
        {
            BackColor = t.Card;
            foreach (Control c in Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
            }
        }
    }
}
