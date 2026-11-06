namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    public enum NoteKind
    {
        Heading, Subheading, Paragraph, Bullet, Row, Code
    }

    public sealed class NoteRun
    {
        public string Text;
        public bool Bold;
        public bool Code;
        public string Url;
    }

    public sealed class NoteBlock
    {
        public NoteKind Kind;
        public int Depth;
        public List<NoteRun> Runs = new List<NoteRun>();
        public List<List<NoteRun>> Cells;
        public List<string> Lines;
    }

    public static class ReleaseNotes
    {
        public const string ChangelogUrl = "https://github.com/" + Updates.Repo + "/blob/main/CHANGELOG.md";
        static readonly Regex RxVersion = new Regex(@"^##\s+(?:LogVice|Trace Lens)\s+(\d+(?:\.\d+){1,3})\s*$");
        static string changelog;

        public static string Changelog
        {
            get
            {
                if (changelog != null) return changelog;
                try
                {
                    using (Stream s = typeof(ReleaseNotes).Assembly.GetManifestResourceStream("CHANGELOG.md"))
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                        changelog = r.ReadToEnd();
                }
                catch (Exception) { changelog = ""; }
                return changelog;
            }
        }

        public static string Sections(string markdown, string after, string upTo)
        {
            return Pick(markdown, delegate (string v)
            {
                return (after == null || Updates.IsNewer(v, after)) && (upTo == null || !Updates.IsNewer(v, upTo));
            });
        }

        public static string Section(string markdown, string version)
        {
            return Pick(markdown, delegate (string v) { return !Updates.IsNewer(v, version) && !Updates.IsNewer(version, v); });
        }

        static string Pick(string markdown, Predicate<string> wanted)
        {
            StringBuilder sb = new StringBuilder();
            bool take = false;
            foreach (string raw in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.StartsWith("## ", StringComparison.Ordinal))
                {
                    Match m = RxVersion.Match(raw);
                    take = m.Success && wanted(m.Groups[1].Value);
                }
                if (take) sb.Append(raw).Append('\n');
            }
            return sb.ToString().Trim();
        }

        public static string FromRelease(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            string s = "\n" + body.Replace("\r\n", "\n");
            foreach (string cut in new string[] { "\n### Deployment", "\n### SHA256" })
            {
                int i = s.IndexOf(cut, StringComparison.Ordinal);
                if (i >= 0) s = s.Substring(0, i);
            }
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }

        public static List<NoteBlock> Parse(string markdown)
        {
            List<NoteBlock> blocks = new List<NoteBlock>();
            StringBuilder para = new StringBuilder();
            NoteBlock code = null;
            foreach (string raw in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushPara(blocks, para);
                    if (code == null)
                    {
                        code = new NoteBlock();
                        code.Kind = NoteKind.Code;
                        code.Lines = new List<string>();
                        blocks.Add(code);
                    }
                    else code = null;
                    continue;
                }
                if (code != null) { code.Lines.Add(line); continue; }
                if (trimmed.Length == 0) { FlushPara(blocks, para); continue; }
                if (trimmed.StartsWith("![", StringComparison.Ordinal)) continue;

                int hashes = 0;
                while (hashes < trimmed.Length && trimmed[hashes] == '#') hashes++;
                if (hashes > 0 && hashes <= 6 && hashes < trimmed.Length && trimmed[hashes] == ' ')
                {
                    FlushPara(blocks, para);
                    NoteBlock h = new NoteBlock();
                    h.Kind = hashes <= 2 ? NoteKind.Heading : NoteKind.Subheading;
                    Inline(trimmed.Substring(hashes + 1).Trim(), false, null, h.Runs);
                    blocks.Add(h);
                    continue;
                }
                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
                {
                    FlushPara(blocks, para);
                    NoteBlock b = new NoteBlock();
                    b.Kind = NoteKind.Bullet;
                    b.Depth = Math.Min(3, (line.Length - trimmed.Length + 3) / 4);
                    Inline(trimmed.Substring(2).Trim(), false, null, b.Runs);
                    blocks.Add(b);
                    continue;
                }
                if (trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    FlushPara(blocks, para);
                    string[] cells = trimmed.Trim('|').Split('|');
                    bool empty = true, rule = true;
                    foreach (string c in cells)
                    {
                        string v = c.Trim();
                        if (v.Length > 0) empty = false;
                        if (v.Length == 0 || v.Trim(':', '-').Length > 0) rule = false;
                    }
                    if (empty || rule) continue;
                    NoteBlock row = new NoteBlock();
                    row.Kind = NoteKind.Row;
                    row.Cells = new List<List<NoteRun>>();
                    foreach (string c in cells)
                    {
                        List<NoteRun> runs = new List<NoteRun>();
                        Inline(c.Trim(), false, null, runs);
                        row.Cells.Add(runs);
                    }
                    blocks.Add(row);
                    continue;
                }
                if (para.Length > 0) para.Append(' ');
                para.Append(trimmed);
            }
            FlushPara(blocks, para);
            return blocks;
        }

        static void FlushPara(List<NoteBlock> blocks, StringBuilder para)
        {
            if (para.Length == 0) return;
            NoteBlock p = new NoteBlock();
            p.Kind = NoteKind.Paragraph;
            Inline(para.ToString(), false, null, p.Runs);
            blocks.Add(p);
            para.Length = 0;
        }

        static void Inline(string s, bool bold, string url, List<NoteRun> runs)
        {
            StringBuilder plain = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '`')
                {
                    int j = s.IndexOf('`', i + 1);
                    if (j > i)
                    {
                        Add(runs, plain, bold, url);
                        NoteRun r = new NoteRun();
                        r.Text = s.Substring(i + 1, j - i - 1);
                        r.Code = true;
                        r.Url = url;
                        runs.Add(r);
                        i = j + 1;
                        continue;
                    }
                }
                else if (c == '*' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int j = s.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (j > i)
                    {
                        Add(runs, plain, bold, url);
                        Inline(s.Substring(i + 2, j - i - 2), true, url, runs);
                        i = j + 2;
                        continue;
                    }
                }
                else if (c == '[' && url == null)
                {
                    int mid = s.IndexOf("](", i + 1, StringComparison.Ordinal);
                    int end = mid > i ? s.IndexOf(')', mid + 2) : -1;
                    if (mid > i && end > mid)
                    {
                        Add(runs, plain, bold, url);
                        string target = s.Substring(mid + 2, end - mid - 2).Trim();
                        Inline(s.Substring(i + 1, mid - i - 1), bold, target.Length > 0 ? target : null, runs);
                        i = end + 1;
                        continue;
                    }
                }
                plain.Append(c);
                i++;
            }
            Add(runs, plain, bold, url);
        }

        static void Add(List<NoteRun> runs, StringBuilder plain, bool bold, string url)
        {
            if (plain.Length == 0) return;
            NoteRun r = new NoteRun();
            r.Text = plain.ToString();
            r.Bold = bold;
            r.Url = url;
            runs.Add(r);
            plain.Length = 0;
        }
    }

    public sealed class NotesView : FluentControl
    {
        sealed class Piece
        {
            public string Text;
            public Font Font;
            public int Tone;
            public Rectangle Bounds;
            public string Url;
        }

        static readonly Regex RxToken = new Regex(@"\S+|\s+");
        const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping;
        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Font, Dictionary<string, int>> widths = new Dictionary<Font, Dictionary<string, int>>();
        List<NoteBlock> blocks = new List<NoteBlock>();
        string source = "";
        int laidOut = -1;
        string hotUrl;
        Font h1, h2;

        public NotesView()
        {
            TabStop = true;
        }

        public string Markdown
        {
            get { return source; }
            set
            {
                source = value ?? "";
                blocks = ReleaseNotes.Parse(source);
                laidOut = -1;
                LayoutNotes();
            }
        }

        Font H1
        {
            get { return h1 ?? (h1 = new Font(Fonts.UiSemibold.FontFamily, 13f, Fonts.UiSemibold.Style)); }
        }

        Font H2
        {
            get { return h2 ?? (h2 = new Font(Fonts.UiSemibold.FontFamily, 10.5f, Fonts.UiSemibold.Style)); }
        }

        int TextWidth(string s, Font f)
        {
            Dictionary<string, int> d;
            if (!widths.TryGetValue(f, out d)) { d = new Dictionary<string, int>(StringComparer.Ordinal); widths[f] = d; }
            int w;
            if (!d.TryGetValue(s, out w))
            {
                w = s == " " ? Draw.Measure("a a", f).Width - Draw.Measure("aa", f).Width : Draw.Measure(s, f).Width;
                d[s] = w;
            }
            return w;
        }

        static int LineHeight(Font f)
        {
            return f.Height + Dpi.S(4);
        }

        public void LayoutNotes()
        {
            int w = ClientSize.Width;
            if (w <= 0) return;
            pieces.Clear();
            int y = Dpi.S(2);
            bool first = true;
            NoteKind prev = NoteKind.Paragraph;
            foreach (NoteBlock b in blocks)
            {
                if (!first && prev == NoteKind.Row && b.Kind != NoteKind.Row) y += Dpi.S(8);
                switch (b.Kind)
                {
                    case NoteKind.Heading:
                        if (!first) y += Dpi.S(20);
                        y = Flow(b.Runs, 0, w, y, H1, 0, true) + Dpi.S(6);
                        break;
                    case NoteKind.Subheading:
                        if (!first) y += Dpi.S(10);
                        y = Flow(b.Runs, 0, w, y, H2, 0, true) + Dpi.S(4);
                        break;
                    case NoteKind.Paragraph:
                        y = Flow(b.Runs, 0, w, y, Fonts.Ui, 0, false) + Dpi.S(8);
                        break;
                    case NoteKind.Bullet:
                        {
                            int indent = Dpi.S(20) * (b.Depth + 1);
                            Piece dot = new Piece();
                            dot.Text = b.Depth == 0 ? ((char)0x2022).ToString() : ((char)0x2013).ToString();
                            dot.Font = Fonts.Ui;
                            dot.Tone = 1;
                            dot.Bounds = new Rectangle(indent - Dpi.S(14), y, Dpi.S(12), LineHeight(Fonts.Ui));
                            pieces.Add(dot);
                            y = Flow(b.Runs, indent, w - indent, y, Fonts.Ui, 0, false) + Dpi.S(4);
                            break;
                        }
                    case NoteKind.Row:
                        {
                            int col = Math.Min(Dpi.S(150), w / 3);
                            int y1 = Flow(b.Cells[0], 0, col - Dpi.S(8), y, Fonts.Ui, 1, false);
                            List<NoteRun> rest = new List<NoteRun>();
                            for (int k = 1; k < b.Cells.Count; k++)
                            {
                                if (k > 1) { NoteRun gap = new NoteRun(); gap.Text = "   "; rest.Add(gap); }
                                rest.AddRange(b.Cells[k]);
                            }
                            int y2 = rest.Count > 0 ? Flow(rest, col, w - col, y, Fonts.Ui, 0, false) : y1;
                            y = Math.Max(y1, y2) + Dpi.S(2);
                            break;
                        }
                    case NoteKind.Code:
                        foreach (string line in b.Lines)
                        {
                            Piece p = new Piece();
                            p.Text = line;
                            p.Font = Fonts.MonoSmall;
                            p.Tone = 1;
                            p.Bounds = new Rectangle(Dpi.S(12), y, Math.Max(1, TextWidth(line.Length == 0 ? " " : line, Fonts.MonoSmall) + 2), LineHeight(Fonts.MonoSmall));
                            pieces.Add(p);
                            y += LineHeight(Fonts.MonoSmall);
                        }
                        y += Dpi.S(8);
                        break;
                }
                first = false;
                prev = b.Kind;
            }
            laidOut = w;
            int h = y + Dpi.S(6);
            if (Height != h) Height = h;
            Invalidate();
        }

        int Flow(List<NoteRun> runs, int x0, int width, int y, Font baseFont, int tone, bool heading)
        {
            int line = LineHeight(baseFont);
            int x = x0;
            foreach (NoteRun r in runs)
            {
                Font f = r.Code ? Fonts.MonoSmall : r.Bold && !heading ? Fonts.UiSemibold : baseFont;
                int t = r.Url != null ? 2 : tone;
                foreach (Match m in RxToken.Matches(r.Text ?? ""))
                {
                    string tok = m.Value;
                    if (char.IsWhiteSpace(tok[0]))
                    {
                        if (x > x0) x += TextWidth(" ", f);
                        continue;
                    }
                    int tw = TextWidth(tok, f);
                    if (x + tw > x0 + width && x > x0)
                    {
                        y += line;
                        x = x0;
                    }
                    Piece p = new Piece();
                    p.Text = tok;
                    p.Font = f;
                    p.Tone = t;
                    p.Bounds = new Rectangle(x, y, tw + 1, line);
                    p.Url = r.Url;
                    pieces.Add(p);
                    x += tw;
                }
            }
            return y + line;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ClientSize.Width != laidOut) LayoutNotes();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            Rectangle clip = e.ClipRectangle;
            foreach (Piece p in pieces)
            {
                if (p.Bounds.Bottom < clip.Top || p.Bounds.Top > clip.Bottom) continue;
                Color c = p.Tone == 1 ? theme.Text2 : p.Tone == 2 ? theme.Accent : theme.Text;
                TextRenderer.DrawText(g, p.Text, p.Font, p.Bounds, c, Flags);
                if (p.Url != null && p.Url == hotUrl)
                    using (Pen pen = new Pen(c)) g.DrawLine(pen, p.Bounds.Left, p.Bounds.Bottom - Dpi.S(4), p.Bounds.Right - 1, p.Bounds.Bottom - Dpi.S(4));
            }
        }

        string UrlAt(Point pt)
        {
            foreach (Piece p in pieces)
                if (p.Url != null && p.Bounds.Contains(pt)) return p.Url;
            return null;
        }

        static bool IsWebLink(string url)
        {
            return url != null && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string u = UrlAt(e.Location);
            if (!IsWebLink(u)) u = null;
            if (u == hotUrl) return;
            hotUrl = u;
            Cursor = u != null ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hotUrl == null) return;
            hotUrl = null;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            string u = UrlAt(e.Location);
            if (e.Button == MouseButtons.Left && IsWebLink(u)) try { Process.Start(u); } catch (Exception) { }
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
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            ScrollableControl host = Parent as ScrollableControl;
            if (e.Control && e.KeyCode == Keys.C)
            {
                try { Clipboard.SetText(source); } catch (Exception) { }
                e.Handled = true;
                return;
            }
            if (host == null) return;
            int page = Math.Max(Dpi.S(40), host.ClientSize.Height - Dpi.S(40));
            int step = Dpi.S(40);
            int top = -host.AutoScrollPosition.Y;
            switch (e.KeyCode)
            {
                case Keys.Up: top -= step; break;
                case Keys.Down: top += step; break;
                case Keys.PageUp: top -= page; break;
                case Keys.PageDown: top += page; break;
                case Keys.Home: top = 0; break;
                case Keys.End: top = Height; break;
                default: return;
            }
            host.AutoScrollPosition = new Point(0, Math.Max(0, top));
            e.Handled = true;
        }
    }

    public sealed class InfoBar : FluentControl
    {
        public readonly FluentButton Action;
        readonly FluentButton dismiss;
        string message = "";

        public InfoBar()
        {
            Action = new FluentButton(ButtonKind.Standard, Glyph.None, "What's new");
            dismiss = new FluentButton(ButtonKind.Subtle, Glyph.Close, "");
            dismiss.Width = Dpi.S(32);
            dismiss.Click += delegate { Visible = false; };
            Controls.Add(Action);
            Controls.Add(dismiss);
            Height = Dpi.S(48);
        }

        public string Message
        {
            get { return message; }
            set { message = value ?? ""; Invalidate(); }
        }

        public void SetTips(ToolTip tips)
        {
            tips.SetToolTip(dismiss, "Close");
        }

        public override void ApplyTheme(Theme t)
        {
            base.ApplyTheme(t);
            BackColor = t.AccentSoft;
            Action.ApplyTheme(t);
            dismiss.ApplyTheme(t);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Action == null || dismiss == null) return;
            int h = Height - 1;
            dismiss.Location = new Point(Width - Dpi.S(12) - dismiss.Width, (h - dismiss.Height) / 2);
            Action.Width = Action.GetPreferredSize(Size.Empty).Width;
            Action.Location = new Point(dismiss.Left - Dpi.S(8) - Action.Width, (h - Action.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.AccentSoft);
            int s = Dpi.S(18);
            int h = Height - 1;
            Draw.Icon(g, Glyph.Info, new RectangleF(Dpi.S(20), (h - s) / 2f, s, s), theme.Accent);
            int x = Dpi.S(50);
            int right = Action != null ? Action.Left : Width;
            Draw.Text(g, message, Fonts.Ui, new Rectangle(x, 0, Math.Max(0, right - Dpi.S(12) - x), h), theme.Text, Draw.Line);
            using (Pen p = new Pen(theme.Divider)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    public sealed class NotesForm : Form, IThemed
    {
        readonly Label heading, intro;
        readonly Panel card, scroller, bottom;
        readonly NotesView view;
        readonly List<FluentButton> buttons = new List<FluentButton>();
        Theme theme = Theme.Light();

        public NotesForm(string caption, string title, string text, string notes)
        {
            Text = caption;
            Font = Fonts.Ui;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(Dpi.S(680), Dpi.S(620));
            MinimumSize = new Size(Dpi.S(440), Dpi.S(360));
            Padding = new Padding(Dpi.S(24), Dpi.S(18), Dpi.S(24), 0);

            heading = new Label();
            heading.Dock = DockStyle.Top;
            heading.Font = Fonts.Title;
            heading.Text = title;
            heading.Height = Fonts.Title.Height + Dpi.S(8);
            heading.UseMnemonic = false;

            intro = new Label();
            intro.Dock = DockStyle.Top;
            intro.Text = text ?? "";
            intro.UseMnemonic = false;
            intro.Visible = intro.Text.Length > 0;

            Panel gap = new Panel();
            gap.Dock = DockStyle.Top;
            gap.Height = Dpi.S(12);

            card = new Panel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(Dpi.S(20), Dpi.S(16), Dpi.S(4), Dpi.S(8));

            scroller = new Panel();
            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.Resize += delegate { FitView(); };

            view = new NotesView();
            view.Location = new Point(0, 0);
            scroller.Controls.Add(view);
            card.Controls.Add(scroller);

            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Dpi.S(64);
            bottom.Resize += delegate { LayoutButtons(); };

            Controls.Add(card);
            Controls.Add(gap);
            Controls.Add(intro);
            Controls.Add(heading);
            Controls.Add(bottom);
            Resize += delegate { SizeIntro(); };
            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Escape) return;
                e.Handled = true;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            view.Markdown = string.IsNullOrEmpty(notes) ? "No release notes were published with this version." : notes;
        }

        public FluentButton AddButton(ButtonKind kind, string text, DialogResult result)
        {
            FluentButton b = new FluentButton(kind, Glyph.None, text);
            b.Size = new Size(Math.Max(Dpi.S(96), b.GetPreferredSize(Size.Empty).Width), Dpi.S(34));
            b.Click += delegate { DialogResult = result; Close(); };
            buttons.Add(b);
            bottom.Controls.Add(b);
            LayoutButtons();
            return b;
        }

        void FitView()
        {
            int w = Math.Max(Dpi.S(100), scroller.ClientSize.Width - Dpi.S(16));
            if (view.Width != w) view.Width = w;
        }

        void LayoutButtons()
        {
            int x = bottom.ClientSize.Width;
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                FluentButton b = buttons[i];
                x -= b.Width;
                b.Location = new Point(x, (bottom.ClientSize.Height - b.Height) / 2);
                x -= Dpi.S(8);
            }
        }

        void SizeIntro()
        {
            if (!intro.Visible) return;
            int w = Math.Max(Dpi.S(100), ClientSize.Width - Padding.Horizontal);
            intro.Height = Draw.WrapHeight(intro.Text, intro.Font, w) + Dpi.S(4);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Bg;
            ForeColor = t.Text;
            foreach (Control c in Controls) c.BackColor = t.Bg;
            heading.ForeColor = t.Text;
            intro.ForeColor = t.Text2;
            card.BackColor = t.Card;
            scroller.BackColor = t.Card;
            view.ApplyTheme(t);
            foreach (FluentButton b in buttons) b.ApplyTheme(t);
            NativeMethods.ScrollTheme(scroller, t.IsDark);
            if (IsHandleCreated) NativeMethods.StyleWindow(Handle, t);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
            NativeMethods.ScrollTheme(scroller, theme.IsDark);
            SizeIntro();
            FitView();
            LayoutButtons();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitView();
            if (buttons.Count > 0) buttons[0].Focus();
        }
    }
}
