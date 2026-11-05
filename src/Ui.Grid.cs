namespace LogVice
{
    using System;
    using System.Drawing;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    public static class Fmt
    {
        public static string Time(long t)
        {
            return t == Store.NoTime ? "" : new DateTime(t).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        public static string Size(long b)
        {
            if (b < 1048576) return (b / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            if (b < 1073741824) return (b / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            return (b / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        public static string Delta(long ticks)
        {
            string sg = ticks < 0 ? "-" : "+";
            double ms = Math.Abs(ticks) / 10000.0;
            if (ms < 1000) return sg + ms.ToString("0", CultureInfo.InvariantCulture) + " ms";
            if (ms < 60000) return sg + (ms / 1000).ToString("0.000", CultureInfo.InvariantCulture) + " s";
            long s = (long)(ms / 1000);
            long h = s / 3600, m = s % 3600 / 60;
            return h > 0 ? sg + h + " h " + m + " min" : sg + m + " min " + (s % 60) + " s";
        }

        public static string Utc(short bias)
        {
            int off = -bias, a = Math.Abs(off);
            return "UTC" + (off >= 0 ? "+" : "-") + (a / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (a % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static string Display(string m)
        {
            int nl = m.IndexOf('\n');
            string s = nl >= 0 ? m.Substring(0, nl).TrimEnd('\r') : m;
            if (s.IndexOf('\t') >= 0) s = s.Replace("\t", "    ");
            if (s.Length > 400) s = s.Substring(0, 400);
            if (nl >= 0) s += "  ↵";
            return s;
        }
    }

    public sealed class LogGrid : FluentControl
    {
        public Store Data = new Store();
        public byte[] Hits;
        public Regex Highlight;
        public bool MultiFile;
        public bool ShowLines = true;
        public byte[] Marks;
        public HighlightRule[] MarkRules = new HighlightRule[0];
        public bool Busy;
        public string BusyTitle = "", BusyText = "";
        public float BusyProgress;
        public string EmptyTitle = "", EmptyText = "";

        int[] view = new int[0];
        int count;
        int rowH, lineW, dateW = -1;
        readonly int headH;
        public bool Tint = true;
        double off;
        int cur = -1, anchor = -1, hoverRow = -1;
        readonly int[] widths;
        bool sbHot, sbDrag, dragSel;
        double sbGrab;
        int resizing = -1, resizeX0, resizeW0, lastY;
        readonly Timer scrollTimer = new Timer();

        public event EventHandler SelectionChanged;
        public event MouseEventHandler RowMenu;

        public LogGrid()
        {
            TabStop = true;
            AllowDrop = true;
            widths = new int[] { Dpi.S(170), Dpi.S(200), Dpi.S(70), Dpi.S(170) };
            SetDensity(1);
            headH = Dpi.S(36);
            scrollTimer.Interval = 40;
            scrollTimer.Tick += delegate { AutoScrollTick(); };
        }

        public void SetDensity(int level)
        {
            int pad = level == 0 ? 4 : level == 2 ? 12 : 8;
            rowH = Math.Max(Dpi.S(18), Fonts.Mono.Height + Dpi.S(pad));
            widths[1] = Draw.Measure("2026-11-03 00:00:00.000", Fonts.Mono).Width + Dpi.S(22);
            widths[2] = Math.Max(Dpi.S(70), Draw.Measure("000000", Fonts.Mono).Width + Dpi.S(22));
            lineW = Draw.Measure("0000000", Fonts.Mono).Width + Dpi.S(12);
            dateW = -1;
            Clamp();
            Invalidate();
        }

        public int Count { get { return count; } }
        public int Cur { get { return cur; } }
        public int SelAnchor { get { return anchor; } }
        public int SelLo { get { return Math.Min(cur, anchor); } }
        public int SelHi { get { return Math.Max(cur, anchor); } }

        public void SetView(int[] v, int n)
        {
            view = v ?? new int[0];
            count = n;
            if (cur >= count) cur = count - 1;
            if (anchor >= count) anchor = count - 1;
            if (cur < 0) anchor = -1;
            Clamp();
            Invalidate();
        }

        public void ResetSelection()
        {
            cur = anchor = -1;
            off = 0;
            Invalidate();
        }

        Rectangle RowsRect
        {
            get { return new Rectangle(1, headH, Math.Max(0, Width - 2), Math.Max(0, Height - headH - Dpi.S(6))); }
        }

        int Gutter
        {
            get { return Dpi.S(14); }
        }

        double MaxOff
        {
            get { return Math.Max(0, (double)count * rowH - RowsRect.Height); }
        }

        void Clamp()
        {
            if (off > MaxOff) off = MaxOff;
            if (off < 0) off = 0;
        }

        public int PageRows
        {
            get { return Math.Max(1, RowsRect.Height / rowH - 1); }
        }

        public bool LastRowVisible()
        {
            return count == 0 || off >= MaxOff - rowH;
        }

        public void ScrollToEnd()
        {
            off = MaxOff;
            Invalidate();
        }

        public void Select(int p, bool extend, int align)
        {
            if (count == 0) return;
            if (p < 0) p = 0;
            if (p >= count) p = count - 1;
            cur = p;
            if (!extend || anchor < 0) anchor = p;
            EnsureVisible(p, align);
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public void SelectAll()
        {
            if (count == 0) return;
            anchor = 0;
            cur = count - 1;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public void EnsureVisible(int p, int align)
        {
            Rectangle rr = RowsRect;
            double top = (double)p * rowH;
            if (align == 2) off = MaxOff;
            else if (align == 1) off = top - (rr.Height - rowH) / 2.0;
            else if (top < off) off = top;
            else if (top + rowH > off + rr.Height) off = top + rowH - rr.Height;
            Clamp();
        }

        struct Cols
        {
            public int Sev, Line, LineW, Msg, MsgW, Comp, Time, Thr, File, End;
        }

        Cols ColumnLayout()
        {
            Cols c = new Cols();
            int left = Dpi.S(8);
            int sevW = Dpi.S(28);
            c.End = Width - 1 - Gutter;
            int fixedW = widths[0] + widths[1] + widths[2] + (MultiFile ? widths[3] : 0);
            c.Sev = left;
            c.Line = left + sevW;
            c.LineW = ShowLines ? lineW : 0;
            c.Msg = c.Line + c.LineW;
            c.MsgW = Math.Max(Dpi.S(120), c.End - c.Msg - fixedW);
            c.Comp = c.Msg + c.MsgW;
            c.Time = c.Comp + widths[0];
            c.Thr = c.Time + widths[1];
            c.File = c.Thr + widths[2];
            return c;
        }

        int[] Boundaries(Cols c)
        {
            return MultiFile ? new int[] { c.Comp, c.Time, c.Thr, c.File } : new int[] { c.Comp, c.Time, c.Thr };
        }

        int BoundaryAt(int x, int y)
        {
            if (y < 0 || y >= headH) return -1;
            int[] b = Boundaries(ColumnLayout());
            for (int i = 0; i < b.Length; i++) if (Math.Abs(x - b[i]) <= Dpi.S(4)) return i;
            return -1;
        }

        Rectangle Track()
        {
            Rectangle rr = RowsRect;
            return new Rectangle(Width - 1 - Gutter, rr.Top + Dpi.S(2), Gutter, Math.Max(0, rr.Height - Dpi.S(4)));
        }

        bool HasBar
        {
            get { return (double)count * rowH > RowsRect.Height; }
        }

        void Thumb(out int top, out int len)
        {
            Rectangle t = Track();
            double total = (double)count * rowH;
            len = (int)Math.Max(Dpi.S(36), t.Height * (RowsRect.Height / Math.Max(1.0, total)));
            if (len > t.Height) len = t.Height;
            double max = MaxOff;
            top = t.Top + (int)(max > 0 ? (t.Height - len) * (off / max) : 0);
        }

        int RowAt(int y)
        {
            Rectangle rr = RowsRect;
            return (int)Math.Floor((y - rr.Top + off) / rowH);
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
                using (SolidBrush hb = new SolidBrush(theme.Header)) g.FillRectangle(hb, 0, 0, Width, headH);
                g.Clip = old;
            }
            Cols c = ColumnLayout();
            PaintHeader(g, c);
            Rectangle rr = RowsRect;
            if (count > 0 && Data != null)
            {
                g.SetClip(rr);
                int first = (int)(off / rowH);
                int y = rr.Top - (int)Math.Round(off - (double)first * rowH);
                int lo = SelLo, hi = SelHi;
                for (int p = first; p < count && y < rr.Bottom; p++, y += rowH) PaintRow(g, c, p, y, lo, hi);
                g.ResetClip();
            }
            if (HasBar)
            {
                Rectangle t = Track();
                int top, len;
                Thumb(out top, out len);
                bool wide = sbHot || sbDrag;
                float tw = wide ? Dpi.F(6) : Dpi.F(3);
                if (wide) Draw.Fill(g, new RectangleF(t.X + Dpi.S(3), t.Y, t.Width - Dpi.S(6), t.Height), Dpi.F(4), Theme.Over(theme.Card, theme.Hover));
                Draw.Fill(g, new RectangleF(t.Right - Dpi.S(4) - tw, top, tw, len), tw / 2f, theme.Thumb);
            }
            if (Busy) PaintBusy(g, rr);
            else if (count == 0 && EmptyTitle.Length > 0) PaintEmpty(g, rr);
            Draw.Elevation(g, card, Dpi.F(8), theme);
            Draw.Stroke(g, card, Dpi.F(8), theme.Stroke, 1f);
        }

        void PaintHeader(Graphics g, Cols c)
        {
            Font f = Fonts.SmallSemibold;
            Color fg = theme.Text2;
            int pad = Dpi.S(8);
            if (c.LineW > 0) Draw.Text(g, "Line", f, new Rectangle(c.Line, 0, c.LineW - Dpi.S(4), headH), fg, Draw.Line | TextFormatFlags.Right);
            Draw.Text(g, "Message", f, new Rectangle(c.Msg + pad, 0, c.MsgW - pad, headH), fg, Draw.Line);
            Draw.Text(g, "Component", f, new Rectangle(c.Comp + pad, 0, widths[0] - pad * 2, headH), fg, Draw.Line);
            Draw.Text(g, "Date / time", f, new Rectangle(c.Time + pad, 0, widths[1] - pad * 2, headH), fg, Draw.Line);
            Draw.Text(g, "Thread", f, new Rectangle(c.Thr + pad, 0, widths[2] - pad * 2, headH), fg, Draw.Line | TextFormatFlags.Right);
            if (MultiFile) Draw.Text(g, "Log file", f, new Rectangle(c.File + pad, 0, widths[3] - pad * 2, headH), fg, Draw.Line);
            using (Pen p = new Pen(theme.Divider))
            {
                g.DrawLine(p, Dpi.S(8), headH - 1, Width - Dpi.S(8), headH - 1);
                foreach (int x in Boundaries(c)) g.DrawLine(p, x, Dpi.S(11), x, headH - Dpi.S(11));
            }
        }

        void PaintRow(Graphics g, Cols c, int p, int y, int lo, int hi)
        {
            int i = view[p];
            byte sv = Data.Sev[i];
            Color bg = !Tint ? theme.Card : sv == 3 ? theme.ErrBg : sv == 2 ? theme.WarnBg : theme.Card;
            HighlightRule hr = MarkAt(i);
            if (hr != null) bg = hr.Tint(theme, theme.Card);
            else if (bg == theme.Card && (p & 1) == 1) bg = theme.Zebra;
            if (bg != theme.Card)
                using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, 1, y, Width - 2, rowH);
            Color bar = hr != null ? hr.Color : sv == 3 ? theme.ErrFg : sv == 2 ? theme.WarnFg : Color.Empty;
            if (bar != Color.Empty)
                using (SolidBrush b = new SolidBrush(bar)) g.FillRectangle(b, 1, y, Dpi.S(3), rowH);
            bool inSel = cur >= 0 && p >= lo && p <= hi;
            Color rowBg = bg;
            RectangleF pill = new RectangleF(Dpi.S(4), y + 1, Width - Dpi.S(8), rowH - 2);
            if (inSel)
            {
                float t = p == cur ? (theme.IsDark ? 0.26f : 0.17f) : (theme.IsDark ? 0.15f : 0.09f);
                rowBg = Theme.Mix(bg, theme.Accent, t);
                Draw.Fill(g, pill, Dpi.F(4), rowBg);
                if (p == cur)
                {
                    float ph = Math.Min(Dpi.F(14), rowH - Dpi.F(8));
                    Draw.Fill(g, new RectangleF(Dpi.S(4), y + (rowH - ph) / 2f, Dpi.F(3), ph), Dpi.F(1.5f), theme.Accent);
                }
            }
            else if (p == hoverRow)
            {
                rowBg = Theme.Over(bg, theme.Hover);
                Draw.Fill(g, pill, Dpi.F(4), rowBg);
            }
            int s = Dpi.S(13);
            Draw.Severity(g, sv, new RectangleF(c.Sev + (Dpi.S(28) - s) / 2f + Dpi.S(2), y + (rowH - s) / 2f, s, s), theme);
            int pad = Dpi.S(8);
            if (c.LineW > 0)
            {
                int ln = i < Data.Line.Length ? Data.Line[i] : 0;
                if (ln > 0) Draw.Text(g, ln.ToString(CultureInfo.InvariantCulture), Fonts.Mono, new Rectangle(c.Line, y, c.LineW - Dpi.S(4), rowH), theme.Text3, Draw.Line | TextFormatFlags.Right);
            }
            string msg = Fmt.Display(Data.Msg[i]);
            Rectangle mr = new Rectangle(c.Msg + pad, y, c.MsgW - pad * 2, rowH);
            bool hit = Hits != null && Highlight != null && i < Hits.Length && Hits[i] != 0;
            if (hit) PaintHighlighted(g, msg, mr);
            else Draw.Text(g, msg, Fonts.Mono, mr, theme.Text, Draw.Line);
            int ci = Data.Comp[i];
            if (ci >= 0) Draw.Text(g, Data.CompNames[ci], Fonts.Ui, new Rectangle(c.Comp + pad, y, widths[0] - pad * 2, rowH), theme.Text2, Draw.Line);
            long tm = Data.Time[i];
            if (tm != Store.NoTime)
            {
                string ts = Fmt.Time(tm);
                if (dateW < 0) dateW = TextW(g, "2026-11-04 ", Fonts.Mono);
                int tx = c.Time + pad, tw = widths[1] - pad * 2;
                Draw.Text(g, ts.Substring(0, 11), Fonts.Mono, new Rectangle(tx, y, Math.Min(dateW + 1, tw), rowH), theme.Text3, Draw.Line);
                if (tw > dateW) Draw.Text(g, ts.Substring(11), Fonts.Mono, new Rectangle(tx + dateW, y, tw - dateW, rowH), theme.Text, Draw.Line);
            }
            int th = Data.Thread[i];
            if (th >= 0) Draw.Text(g, th.ToString(CultureInfo.InvariantCulture), Fonts.Mono, new Rectangle(c.Thr + pad, y, widths[2] - pad * 2, rowH), theme.Text2, Draw.Line | TextFormatFlags.Right);
            if (MultiFile) Draw.Text(g, Data.Sources[Data.Src[i]].Name, Fonts.Ui, new Rectangle(c.File + pad, y, widths[3] - pad * 2, rowH), theme.Text2, Draw.Line);
        }

        public HighlightRule MarkAt(int i)
        {
            byte[] m = Marks;
            if (m == null || i < 0 || i >= m.Length) return null;
            int k = m[i];
            HighlightRule[] rs = MarkRules;
            return k > 0 && k <= rs.Length ? rs[k - 1] : null;
        }

        void PaintHighlighted(Graphics g, string text, Rectangle r)
        {
            Font f = Fonts.Mono;
            int x = r.X, last = 0, n = 0;
            foreach (Match m in Highlight.Matches(text))
            {
                if (m.Length == 0) continue;
                if (++n > 40 || x >= r.Right) break;
                if (m.Index > last)
                {
                    string seg = text.Substring(last, m.Index - last);
                    int w = TextW(g, seg, f);
                    Draw.Text(g, seg, f, new Rectangle(x, r.Y, Math.Max(0, Math.Min(w + 1, r.Right - x)), r.Height), theme.Text, Draw.Line);
                    x += w;
                }
                if (x >= r.Right) break;
                int mw = TextW(g, m.Value, f);
                int vis = Math.Min(mw, r.Right - x);
                Draw.Fill(g, new RectangleF(x - 1, r.Y + Dpi.S(3), vis + 2, r.Height - Dpi.S(6)), Dpi.F(3), theme.Mark);
                Draw.Text(g, m.Value, f, new Rectangle(x, r.Y, Math.Max(0, vis + 1), r.Height), theme.OnMark, Draw.Line);
                x += mw;
                last = m.Index + m.Length;
            }
            if (last < text.Length && x < r.Right)
                Draw.Text(g, text.Substring(last), f, new Rectangle(x, r.Y, r.Right - x, r.Height), theme.Text, Draw.Line);
        }

        static int TextW(Graphics g, string s, Font f)
        {
            const TextFormatFlags mf = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            Size big = new Size(int.MaxValue, int.MaxValue);
            return TextRenderer.MeasureText(g, s + "x", f, big, mf).Width - TextRenderer.MeasureText(g, "x", f, big, mf).Width;
        }

        void PaintBusy(Graphics g, Rectangle rr)
        {
            int w = Math.Min(Dpi.S(400), Width - Dpi.S(48)), h = Dpi.S(118);
            Rectangle box = new Rectangle((Width - w) / 2, rr.Top + Math.Max(Dpi.S(24), (rr.Height - h) / 2), w, h);
            Draw.Fill(g, box, Dpi.F(8), theme.CardAlt);
            Draw.Stroke(g, box, Dpi.F(8), theme.Divider, 1f);
            int pad = Dpi.S(20);
            Draw.Text(g, BusyTitle, Fonts.UiSemibold, new Rectangle(box.X + pad, box.Y + pad, w - pad * 2, Fonts.UiSemibold.Height), theme.Text, Draw.Line);
            float ty = box.Y + pad + Fonts.UiSemibold.Height + Dpi.S(14);
            Draw.Fill(g, new RectangleF(box.X + pad, ty, w - pad * 2, Dpi.S(4)), Dpi.F(2), theme.Divider);
            Draw.Fill(g, new RectangleF(box.X + pad, ty, Math.Max(Dpi.S(4), (w - pad * 2) * Math.Min(1f, BusyProgress)), Dpi.S(4)), Dpi.F(2), theme.Accent);
            Draw.Text(g, BusyText, Fonts.Small, new Rectangle(box.X + pad, (int)ty + Dpi.S(14), w - pad * 2, Fonts.Small.Height + 2), theme.Text2, Draw.Line);
        }

        void PaintEmpty(Graphics g, Rectangle rr)
        {
            int w = Math.Min(Dpi.S(460), Width - Dpi.S(48));
            int y = rr.Top + Math.Max(Dpi.S(24), rr.Height / 2 - Dpi.S(40));
            Draw.Text(g, EmptyTitle, Fonts.UiSemibold, new Rectangle((Width - w) / 2, y, w, Fonts.UiSemibold.Height + 2), theme.Text, Draw.Line | TextFormatFlags.HorizontalCenter);
            int h = Draw.WrapHeight(EmptyText, Fonts.Ui, w);
            Draw.Text(g, EmptyText, Fonts.Ui, new Rectangle((Width - w) / 2, y + Fonts.UiSemibold.Height + Dpi.S(6), w, h + 2), theme.Text2, Draw.Wrap | TextFormatFlags.HorizontalCenter);
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
            if (count == 0) return;
            int p = cur < 0 ? 0 : cur;
            bool handled = true;
            switch (e.KeyCode)
            {
                case Keys.Up: p = cur < 0 ? 0 : cur - 1; break;
                case Keys.Down: p = cur < 0 ? 0 : cur + 1; break;
                case Keys.PageUp: p = cur - PageRows; break;
                case Keys.PageDown: p = cur + PageRows; break;
                case Keys.Home: p = 0; break;
                case Keys.End: p = count - 1; break;
                default: handled = false; break;
            }
            if (handled)
            {
                e.Handled = true;
                Select(p, e.Shift, 0);
            }
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            off -= e.Delta / 120.0 * 3 * rowH;
            Clamp();
            UpdateHover(PointToClient(MousePosition));
            Invalidate();
            base.OnMouseWheel(e);
        }

        void UpdateHover(Point pt)
        {
            Rectangle rr = RowsRect;
            int h = -1;
            bool inBar = HasBar && Track().Contains(pt);
            if (rr.Contains(pt) && !inBar)
            {
                int p = RowAt(pt.Y);
                if (p >= 0 && p < count) h = p;
            }
            if (h != hoverRow || inBar != sbHot)
            {
                hoverRow = h;
                sbHot = inBar;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            lastY = e.Y;
            if (resizing >= 0)
            {
                int dx = e.X - resizeX0;
                if (resizing == 0) widths[0] = Math.Max(Dpi.S(50), resizeW0 - dx);
                else widths[resizing - 1] = Math.Max(Dpi.S(40), resizeW0 + dx);
                Invalidate();
                return;
            }
            if (sbDrag)
            {
                Rectangle t = Track();
                int top, len;
                Thumb(out top, out len);
                double range = Math.Max(1, t.Height - len);
                off = (e.Y - t.Top - sbGrab) / range * MaxOff;
                Clamp();
                Invalidate();
                return;
            }
            if (dragSel)
            {
                Rectangle rr = RowsRect;
                if (e.Y < rr.Top || e.Y >= rr.Bottom) { if (!scrollTimer.Enabled) scrollTimer.Start(); }
                else
                {
                    scrollTimer.Stop();
                    int p = RowAt(e.Y);
                    if (p >= 0 && p < count && p != cur) Select(p, true, 0);
                }
                return;
            }
            Cursor = BoundaryAt(e.X, e.Y) >= 0 ? Cursors.VSplit : Cursors.Default;
            UpdateHover(e.Location);
        }

        void AutoScrollTick()
        {
            if (!dragSel) { scrollTimer.Stop(); return; }
            Rectangle rr = RowsRect;
            if (lastY < rr.Top) Select(Math.Max(0, cur - 1), true, 0);
            else if (lastY >= rr.Bottom) Select(Math.Min(count - 1, cur + 1), true, 0);
            else scrollTimer.Stop();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int b = BoundaryAt(e.X, e.Y);
            if (b >= 0 && e.Button == MouseButtons.Left)
            {
                resizing = b;
                resizeX0 = e.X;
                resizeW0 = b == 0 ? widths[0] : widths[b - 1];
                return;
            }
            if (HasBar && Track().Contains(e.Location) && e.Button == MouseButtons.Left)
            {
                int top, len;
                Thumb(out top, out len);
                if (e.Y >= top && e.Y < top + len)
                {
                    sbDrag = true;
                    sbGrab = e.Y - top;
                }
                else
                {
                    off += (e.Y < top ? -1 : 1) * RowsRect.Height;
                    Clamp();
                }
                Invalidate();
                return;
            }
            Rectangle rr = RowsRect;
            if (!rr.Contains(e.Location)) return;
            int p = RowAt(e.Y);
            if (p < 0 || p >= count) return;
            if (e.Button == MouseButtons.Right)
            {
                if (cur < 0 || p < SelLo || p > SelHi) Select(p, false, 0);
                if (RowMenu != null) RowMenu(this, e);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                Select(p, (ModifierKeys & Keys.Shift) != 0, 0);
                dragSel = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            resizing = -1;
            sbDrag = false;
            dragSel = false;
            scrollTimer.Stop();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverRow != -1 || sbHot)
            {
                hoverRow = -1;
                sbHot = false;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Clamp();
        }
    }
}
