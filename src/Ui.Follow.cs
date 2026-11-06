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
        readonly CheckBox names, events;
        readonly FluentButton bFollow, bCancel;
        readonly Panel bottom;
        List<FollowItem> all = new List<FollowItem>();
        Theme theme = Theme.Light();
        volatile bool stop;
        bool busy;
        bool loading;

        public FollowResult Result;
        public FollowItem Chosen;
        public readonly List<FollowItem> ChosenItems = new List<FollowItem>();

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
            Size = new Size(Dpi.S(760), Dpi.S(620));
            MinimumSize = new Size(Dpi.S(480), Dpi.S(360));
            Padding = new Padding(Dpi.S(20), Dpi.S(16), Dpi.S(20), Dpi.S(14));

            Label intro = new Label();
            intro.Dock = DockStyle.Top;
            intro.Height = Dpi.S(44);
            intro.Text = "Pick an application, update, package, baseline, Intune app, script, remediation or installed app. LogVice finds every line about it in the client logs and Windows events and shows them as one timeline. Hold Ctrl or Shift to pick several.";

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
            list.SelectionMode = SelectionMode.MultiExtended;
            list.DrawItem += DrawRow;
            list.DoubleClick += delegate { Start(); };
            list.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Start(); } };
            list.SelectedIndexChanged += delegate { UpdateFollowButton(); };

            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Dpi.S(108);

            names = new CheckBox();
            names.Text = "Also find lines that only mention the name";
            names.Checked = true;
            names.AutoSize = true;
            names.Location = new Point(0, Dpi.S(8));

            events = new CheckBox();
            events.Text = "Also search Windows events (Application, System, MDM, AppX, Windows Update)";
            events.Checked = true;
            events.AutoSize = true;
            events.Location = new Point(0, Dpi.S(32));

            status = new Label();
            status.Location = new Point(0, Dpi.S(62));
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
            bottom.Controls.Add(events);
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
            EventHandler sizeIntro = delegate { intro.Height = Draw.WrapHeight(intro.Text, intro.Font, Math.Max(Dpi.S(100), ClientSize.Width - Padding.Horizontal)) + Dpi.S(8); };
            Resize += sizeIntro;
            Shown += sizeIntro;
            Shown += delegate { search.Box.Focus(); Load(); };
            FormClosing += delegate { stop = true; };
        }

        void LayoutBottom()
        {
            int w = bottom.ClientSize.Width;
            int y = Dpi.S(62);
            bCancel.Location = new Point(w - bCancel.Width, y);
            bFollow.Location = new Point(bCancel.Left - Dpi.S(8) - bFollow.Width, y);
            status.Width = Math.Max(Dpi.S(80), bFollow.Left - Dpi.S(16));
        }

        void SetBusy(bool b, string text)
        {
            busy = b;
            status.Text = text ?? "";
            search.Enabled = !b;
            names.Enabled = events.Enabled = !b;
            UpdateFollowButton();
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
                    search.Enabled = names.Enabled = events.Enabled = true;
                    UpdateFollowButton();
                });
                string wmiNote, appNote;
                List<FollowItem> fromWmi;
                try { fromWmi = Follow.DiscoverWmi(pc, out wmiNote); }
                catch (Exception ex) { fromWmi = new List<FollowItem>(); wmiNote = ex.Message; }
                Ui(delegate
                {
                    if (!loading) return;
                    all = Follow.Merge(fromWmi, all);
                    Fill();
                    status.Text = Count(all.Count) + ". Reading the installed apps" + where + "\u2026";
                });
                List<FollowItem> installed;
                try { installed = Follow.DiscoverInstalled(pc, out appNote); }
                catch (Exception ex) { installed = new List<FollowItem>(); appNote = ex.Message; }
                Ui(delegate
                {
                    if (!loading) return;
                    loading = false;
                    all = Follow.Merge(all, installed);
                    Fill();
                    string s = all.Count == 0 ? "Nothing to follow was found." : Count(all.Count) + ".";
                    if (wmiNote != null) s += " " + wmiNote;
                    if (appNote != null) s += " " + appNote;
                    SetBusy(false, s);
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
            List<FollowItem> sel = Selected();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (FollowItem it in all)
            {
                if (q.Length > 0 && (it.Name ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0
                    && (it.Id ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && it.Kind.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0
                    && (it.Note ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                list.Items.Add(it);
            }
            bool any = false;
            foreach (FollowItem it in sel)
            {
                int i = list.Items.IndexOf(it);
                if (i >= 0) { list.SetSelected(i, true); any = true; }
            }
            if (!any && list.Items.Count > 0) list.SetSelected(0, true);
            list.EndUpdate();
            UpdateFollowButton();
        }

        List<FollowItem> Selected()
        {
            List<FollowItem> sel = new List<FollowItem>();
            foreach (object o in list.SelectedItems)
            {
                FollowItem it = o as FollowItem;
                if (it != null) sel.Add(it);
            }
            return sel;
        }

        void UpdateFollowButton()
        {
            int n = list.SelectedItems.Count;
            bFollow.Text = n > 1 ? "Follow " + n.ToString(System.Globalization.CultureInfo.CurrentCulture) : "Follow";
            bFollow.Width = bFollow.GetPreferredSize(Size.Empty).Width;
            bFollow.Enabled = (!busy || loading) && n > 0;
            LayoutBottom();
        }

        void Start()
        {
            if (busy && !loading) return;
            List<FollowItem> chosen = Selected();
            if (chosen.Count == 0) return;
            loading = false;
            stop = false;
            string what = chosen.Count == 1 ? chosen[0].Name : chosen.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) + " items";
            SetBusy(true, "Searching the logs for " + what + "\u2026");
            bool useNames = names.Checked, useEvents = events.Checked;
            string pc = computer;
            Task.Factory.StartNew(delegate
            {
                FollowResult r = null;
                string err = null;
                try
                {
                    r = Follow.Scan(pc, chosen, useNames, useEvents, delegate { return stop; }, delegate (string file, int i, int n)
                    {
                        Ui(delegate { status.Text = "Searching " + file + "  (" + Math.Min(100, (i * 100) / Math.Max(1, n)) + " %)"; });
                    });
                }
                catch (Exception ex) { err = ex.Message; }
                Ui(delegate
                {
                    if (stop) { SetBusy(false, "Stopped."); return; }
                    if (err != null) { SetBusy(false, err); return; }
                    if (r.Terms.Count == 0) { SetBusy(false, "There is nothing to search for: the name is too short and there is no ID."); return; }
                    if (r.Files.Count == 0) { SetBusy(false, "No lines about " + what + " were found in the client logs" + (useEvents ? " or Windows events." : ".") + (r.Note != null ? " " + r.Note : "")); return; }
                    Result = r;
                    Chosen = chosen[0];
                    ChosenItems.AddRange(chosen);
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
            names.ForeColor = events.ForeColor = t.Text;
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
