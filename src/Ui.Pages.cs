namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    public interface IMeasured
    {
        int Measure(int width);
    }

    // Keeps a dialog inside the working area of the screen it opens on. At 125 % and 150 % scaling or on a small screen
    // a dialog made for a large one would run off the edge, with its buttons out of reach.
    public static class DialogFit
    {
        public static void Keep(Form f)
        {
            f.Load += delegate { Clamp(f); };
        }

        static void Clamp(Form f)
        {
            Rectangle wa;
            try { wa = Screen.FromControl(f).WorkingArea; }
            catch (Exception) { return; }
            f.MinimumSize = new Size(NarrowFit.Capped(f.MinimumSize.Width, wa.Width), NarrowFit.Capped(f.MinimumSize.Height, wa.Height));
            f.Size = new Size(NarrowFit.Capped(f.Width, wa.Width), NarrowFit.Capped(f.Height, wa.Height));
            f.Location = new Point(NarrowFit.Inside(f.Left, f.Width, wa.Left, wa.Width), NarrowFit.Inside(f.Top, f.Height, wa.Top, wa.Height));
        }
    }

    // A themed dialog with a scrolling column of cards and a footer.
    public class PageForm : Form
    {
        protected Theme theme;
        readonly Panel scroll = new Panel();
        readonly Panel col = new Panel();
        protected readonly Panel footer = new Panel();
        readonly List<Control> items = new List<Control>();
        protected readonly FlatButton bClose = new FlatButton("Close", Glyph.None);

        public PageForm(string title, Theme t)
        {
            theme = t;
            Text = title;
            Font = Fonts.Ui;
            Icon appIcon = Program.AppIcon();
            if (appIcon != null) Icon = appIcon;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            KeyPreview = true;
            Size = new Size(Dpi.S(780), Dpi.S(660));
            MinimumSize = new Size(Dpi.S(560), Dpi.S(420));
            DialogFit.Keep(this);
            BackColor = t.Bg;
            ForeColor = t.Text;
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.BackColor = t.Bg;
            col.BackColor = t.Bg;
            scroll.Controls.Add(col);
            scroll.Resize += delegate { Relayout(); };
            scroll.HandleCreated += delegate { NativeMethods.ScrollTheme(scroll, theme.IsDark); };
            footer.Dock = DockStyle.Bottom;
            footer.Height = Dpi.S(60);
            footer.Padding = new Padding(Dpi.S(24), Dpi.S(14), Dpi.S(24), Dpi.S(14));
            footer.BackColor = t.Bg;
            bClose.Primary = true;
            bClose.Width = Math.Max(bClose.Width, Dpi.S(96));
            bClose.Dock = DockStyle.Right;
            bClose.Click += delegate { Close(); };
            bClose.ApplyTheme(t);
            footer.Controls.Add(bClose);
            Controls.Add(scroll);
            Controls.Add(footer);
            HandleCreated += delegate { NativeMethods.StyleWindow(Handle, theme); };
            HookWheel(scroll);
        }

        public Theme CurrentTheme
        {
            get { return theme; }
        }

        public void Retheme(Theme t)
        {
            theme = t;
            BackColor = scroll.BackColor = col.BackColor = footer.BackColor = t.Bg;
            ForeColor = t.Text;
            foreach (Control c in items) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            foreach (Control c in footer.Controls) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            if (IsHandleCreated) NativeMethods.StyleWindow(Handle, t);
            Invalidate(true);
        }

        protected void Add(Control c)
        {
            items.Add(c);
            col.Controls.Add(c);
            IThemed th = c as IThemed;
            if (th != null) th.ApplyTheme(theme);
            if (hooked.Add(c)) HookWheel(c);
        }

        readonly HashSet<Control> hooked = new HashSet<Control>();

        // Takes every card out of the column, for a page that is built again.
        protected void ClearItems()
        {
            col.SuspendLayout();
            foreach (Control c in items) col.Controls.Remove(c);
            items.Clear();
            col.ResumeLayout(false);
            scroll.AutoScrollPosition = new Point(0, 0);
        }

        protected void ScrollTo(Control c)
        {
            if (c == null || !items.Contains(c)) return;
            scroll.AutoScrollPosition = new Point(0, Math.Max(0, c.Top - Dpi.S(8)));
        }

        // Docked from the right, after Close: the first one given ends up next to Close.
        protected FlatButton Footer(string text, bool primary, EventHandler click)
        {
            FlatButton b = new FlatButton(text, Glyph.None);
            b.Primary = primary;
            b.AutoWidth();
            b.Width = Math.Max(b.Width + Dpi.S(8), Dpi.S(96));
            b.Dock = DockStyle.Right;
            b.Click += Safe.Wrap(click);
            b.ApplyTheme(theme);
            Panel gap = new Panel();
            gap.Width = Dpi.S(8);
            gap.Dock = DockStyle.Right;
            gap.BackColor = theme.Bg;
            footer.Controls.Add(gap);
            footer.Controls.Add(b);
            gap.BringToFront();
            b.BringToFront();
            return b;
        }

        // The Close button gets another text and stops being the main button.
        protected void CloseAs(string text, bool primary)
        {
            bClose.Text = text;
            bClose.Primary = primary;
            bClose.AutoWidth();
            bClose.Width = Math.Max(bClose.Width + Dpi.S(8), Dpi.S(96));
            bClose.ApplyTheme(theme);
        }

        // Scrolls to the end of the column, for screenshots of the lower part of a page.
        public void ScrollToEnd()
        {
            Relayout();
            scroll.AutoScrollPosition = new Point(0, col.Height);
        }

        protected void Relayout()
        {
            int pad = Dpi.S(24);
            int w = Math.Max(Dpi.S(240), scroll.ClientSize.Width - pad * 2);
            int y = Dpi.S(8);
            col.SuspendLayout();
            foreach (Control c in items)
            {
                IMeasured m = c as IMeasured;
                int h = m != null ? m.Measure(w) : c.Height;
                if (c is SectionTitle && y > Dpi.S(8)) y += Dpi.S(10);
                c.SetBounds(0, y, w, h);
                y += h + (c is SectionTitle ? Dpi.S(2) : Dpi.S(6));
            }
            col.ResumeLayout(true);
            col.SetBounds(pad, scroll.AutoScrollPosition.Y, w, y + Dpi.S(16));
            foreach (Control c in items) c.Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Relayout();
        }

        void HookWheel(Control c)
        {
            c.MouseWheel += delegate (object s, MouseEventArgs e)
            {
                if (s == scroll) return;
                HandledMouseEventArgs h = e as HandledMouseEventArgs;
                if (h != null) h.Handled = true;
                int y = -scroll.AutoScrollPosition.Y - e.Delta / 120 * Dpi.S(80);
                scroll.AutoScrollPosition = new Point(0, Math.Max(0, y));
            };
            foreach (Control k in c.Controls) HookWheel(k);
        }

        protected FlatButton SmallButton(string text, EventHandler click)
        {
            FlatButton b = new FlatButton(text, Glyph.None);
            b.Compact = true;
            b.Outline = true;
            b.AutoWidth();
            b.Width += Dpi.S(8);
            b.Click += Safe.Wrap(click);
            b.ApplyTheme(theme);
            return b;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
