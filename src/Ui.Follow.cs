namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public sealed class FollowForm : Form, IThemed
    {
        readonly string computer;
        readonly FluentTextBox search;
        readonly ListBox list;
        readonly Label status;
        readonly CheckBox names;
        readonly FluentButton bFollow, bCancel;
        readonly Panel bottom;
        List<FollowItem> all = new List<FollowItem>();
        Theme theme = Theme.Light();
        volatile bool stop;
        bool busy;
        bool loading;

        public FollowResult Result;
        public FollowItem Chosen;

        public FollowForm(string computer)
        {
            this.computer = string.IsNullOrEmpty(computer) ? null : computer;
            Text = "Follow an item" + (this.computer != null ? " on " + this.computer : "");
            Font = Fonts.Ui;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(Dpi.S(720), Dpi.S(560));
            MinimumSize = new Size(Dpi.S(480), Dpi.S(360));
            Padding = new Padding(Dpi.S(20), Dpi.S(16), Dpi.S(20), Dpi.S(14));

            Label intro = new Label();
            intro.Dock = DockStyle.Top;
            intro.Height = Dpi.S(44);
            intro.Text = "Pick an application, deployment type, update, package, baseline or Intune app. LogVice finds every line about it in the client logs and shows them as one timeline.";

            search = new FluentTextBox(Glyph.Search, "Filter by name or ID");
            search.Dock = DockStyle.Top;
            search.TextChanged += delegate { Fill(); };
            search.Box.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down && list.Items.Count > 0) { e.SuppressKeyPress = true; list.Focus(); if (list.SelectedIndex < 0) list.SelectedIndex = 0; }
                else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Start(); }
            };

            Panel gap = new Panel();
            gap.Dock = DockStyle.Top;
            gap.Height = Dpi.S(10);

            list = new ListBox();
            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = Dpi.S(46);
            list.IntegralHeight = false;
            list.DrawItem += DrawRow;
            list.DoubleClick += delegate { Start(); };
            list.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Start(); } };
            list.SelectedIndexChanged += delegate { bFollow.Enabled = (!busy || loading) && list.SelectedItem != null; };

            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Dpi.S(84);

            names = new CheckBox();
            names.Text = "Also find lines that only mention the name";
            names.Checked = true;
            names.AutoSize = true;
            names.Location = new Point(0, Dpi.S(10));

            status = new Label();
            status.Location = new Point(0, Dpi.S(38));
            status.Height = Dpi.S(40);

            bFollow = new FluentButton(ButtonKind.Accent, Glyph.Search, "Follow");
            bFollow.Enabled = false;
            bFollow.Click += delegate { Start(); };
            bCancel = new FluentButton(ButtonKind.Standard, Glyph.None, "Cancel");
            bCancel.Click += delegate { if (busy && !loading) stop = true; else Close(); };
            foreach (FluentButton b in new FluentButton[] { bFollow, bCancel })
            {
                b.Size = new Size(b.GetPreferredSize(Size.Empty).Width, Dpi.S(34));
                bottom.Controls.Add(b);
            }
            bottom.Controls.Add(names);
            bottom.Controls.Add(status);
            bottom.Resize += delegate { LayoutBottom(); };

            Controls.Add(list);
            Controls.Add(gap);
            Controls.Add(search);
            Controls.Add(intro);
            Controls.Add(bottom);
            AcceptButton = null;
            CancelButton = null;
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { e.Handled = true; if (busy && !loading) stop = true; else Close(); } };
            Shown += delegate { search.Box.Focus(); Load(); };
            FormClosing += delegate { stop = true; };
        }

        void LayoutBottom()
        {
            int w = bottom.ClientSize.Width;
            int y = Dpi.S(36);
            bCancel.Location = new Point(w - bCancel.Width, y);
            bFollow.Location = new Point(bCancel.Left - Dpi.S(8) - bFollow.Width, y);
            status.Width = Math.Max(Dpi.S(80), bFollow.Left - Dpi.S(16));
        }

        void SetBusy(bool b, string text)
        {
            busy = b;
            status.Text = text ?? "";
            bFollow.Enabled = !b && list.SelectedItem != null;
            search.Enabled = !b;
            names.Enabled = !b;
            bCancel.Text = b ? "Stop" : "Cancel";
            bCancel.Width = bCancel.GetPreferredSize(Size.Empty).Width;
            LayoutBottom();
        }

        new void Load()
        {
            string where = computer != null ? " on " + computer : "";
            SetBusy(true, "Reading the client logs" + where + "\u2026");
            string pc = computer;
            Task.Factory.StartNew(delegate
            {
                List<FollowItem> fromLogs;
                try { fromLogs = Follow.DiscoverLogs(pc); }
                catch (Exception) { fromLogs = new List<FollowItem>(); }
                Ui(delegate
                {
                    all = fromLogs;
                    Fill();
                    SetBusy(false, Count(all.Count) + " found in the logs. Reading the ConfigMgr client's WMI" + where + "\u2026");
                    busy = true;
                    loading = true;
                    bFollow.Enabled = list.SelectedItem != null;
                    search.Enabled = names.Enabled = true;
                });
                string note;
                List<FollowItem> fromWmi;
                try { fromWmi = Follow.DiscoverWmi(pc, out note); }
                catch (Exception ex) { fromWmi = new List<FollowItem>(); note = ex.Message; }
                Ui(delegate
                {
                    if (!loading) return;
                    loading = false;
                    all = Follow.Merge(fromWmi, all);
                    Fill();
                    string s = all.Count == 0 ? "Nothing to follow was found in the client's WMI or logs." : Count(all.Count) + ".";
                    SetBusy(false, note != null ? s + " " + note : s);
                });
            });
        }

        static string Count(int n)
        {
            return n == 1 ? "1 item" : n.ToString("N0") + " items";
        }

        void Fill()
        {
            string q = search.Text.Trim();
            FollowItem sel = list.SelectedItem as FollowItem;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (FollowItem it in all)
            {
                if (q.Length > 0 && (it.Name ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0
                    && (it.Id ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && it.Kind.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                list.Items.Add(it);
            }
            list.EndUpdate();
            if (sel != null && list.Items.Contains(sel)) list.SelectedItem = sel;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            bFollow.Enabled = (!busy || loading) && list.SelectedItem != null;
        }

        void Start()
        {
            if (busy && !loading) return;
            loading = false;
            FollowItem it = list.SelectedItem as FollowItem;
            if (it == null) return;
            stop = false;
            SetBusy(true, "Searching the logs for " + it.Name + "…");
            bool useNames = names.Checked;
            string pc = computer;
            Task.Factory.StartNew(delegate
            {
                FollowResult r = null;
                string err = null;
                try
                {
                    r = Follow.Scan(pc, it, useNames, delegate { return stop; }, delegate (string file, int i, int n)
                    {
                        Ui(delegate { status.Text = "Searching " + file + "  (" + Math.Min(100, (i * 100) / Math.Max(1, n)) + " %)"; });
                    });
                }
                catch (Exception ex) { err = ex.Message; }
                Ui(delegate
                {
                    if (stop) { SetBusy(false, "Stopped."); return; }
                    if (err != null) { SetBusy(false, err); return; }
                    if (r.Files.Count == 0) { SetBusy(false, "No lines about " + it.Name + " were found in the client logs."); return; }
                    Result = r;
                    Chosen = it;
                    DialogResult = DialogResult.OK;
                    Close();
                });
            });
        }

        void Ui(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            FollowItem it = (FollowItem)list.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Rectangle r = e.Bounds;
            using (SolidBrush bg = new SolidBrush(sel ? theme.AccentSoft : theme.Card)) e.Graphics.FillRectangle(bg, r);
            if (sel) using (SolidBrush bar = new SolidBrush(theme.Accent)) e.Graphics.FillRectangle(bar, r.Left, r.Top + Dpi.S(8), Dpi.S(3), r.Height - Dpi.S(16));
            int x = r.Left + Dpi.S(12), w = r.Width - Dpi.S(20);
            Draw.Text(e.Graphics, it.Name ?? "", Fonts.UiSemibold, new Rectangle(x, r.Top + Dpi.S(5), w, Dpi.S(20)), theme.Text, Draw.Line | TextFormatFlags.EndEllipsis);
            Draw.Text(e.Graphics, it.Line2, Fonts.Small, new Rectangle(x, r.Top + Dpi.S(25), w, Dpi.S(17)), theme.Text3, Draw.Line | TextFormatFlags.EndEllipsis);
            using (Pen p = new Pen(theme.Divider)) e.Graphics.DrawLine(p, r.Left + Dpi.S(8), r.Bottom - 1, r.Right - Dpi.S(8), r.Bottom - 1);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Bg;
            ForeColor = t.Text;
            list.BackColor = t.Card;
            list.ForeColor = t.Text;
            bottom.BackColor = t.Bg;
            names.ForeColor = t.Text;
            status.ForeColor = t.Text2;
            foreach (Control c in Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
                else if (c != list) { c.BackColor = t.Bg; c.ForeColor = t.Text; }
            }
            foreach (Control c in bottom.Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
            }
            if (IsHandleCreated) NativeMethods.StyleWindow(Handle, t);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
            LayoutBottom();
        }
    }
}
