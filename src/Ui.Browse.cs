namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public sealed class TitleText : FluentControl
    {
        public string Sub = "";
        public Font TitleFont = Fonts.Title;

        public TitleText(string title)
        {
            Text = title;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Height = Dpi.S(48);
            Fit();
        }

        public void Fit()
        {
            Width = Math.Max(Draw.Measure(Text, TitleFont).Width, Draw.Measure(Sub, Fonts.Small).Width) + Dpi.S(8);
            if (Parent != null) Parent.PerformLayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Surface);
            int th = TitleFont.Height, sh = Sub.Length > 0 ? Fonts.Small.Height : 0;
            int y = (Height - th - sh) / 2;
            Draw.Text(g, Text, TitleFont, new Rectangle(0, y, Width, th), theme.Text, Draw.Line);
            if (sh > 0) Draw.Text(g, Sub, Fonts.Small, new Rectangle(Dpi.S(1), y + th, Width, sh + 2), theme.Text2, Draw.Line);
        }
    }

    public sealed class VLine : FluentControl
    {
        public VLine()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            Width = 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(theme.Divider);
        }
    }

    public sealed class BrowseView : Panel, IThemed
    {
        readonly BarPanel header, tools, footer;
        readonly FluentButton bBack, bFollow, bConnect, bConnectAs, bLocal, bHistory, bRefresh, bExplorer, bOpen, bMerge, bAll, bElevate;
        readonly FluentTextBox tComputer, tFilter;
        readonly TitleText title, locTitle, info;
        readonly CardPanel card;
        readonly LocationList nav;
        readonly FileList files;
        readonly Panel right;
        string computer = "";
        List<LogLocation> localCache;
        int gen;
        public Func<List<string>> CustomFolders;
        public string Computer { get { return computer; } }
        public event Action<string[], bool> OpenRequested;
        public event EventHandler BackRequested;
        public event EventHandler HistoryChanged;
        public event EventHandler ElevateRequested;
        public event Action<string> Notice;
        public event Action<string> FollowRequested;

        readonly ToolTip tips;

        public BrowseView(ToolTip tips)
        {
            this.tips = tips;
            header = new BarPanel();
            header.Dock = DockStyle.Top;
            header.Height = Dpi.S(64);
            header.Padding = new Padding(0, Dpi.S(4), 0, Dpi.S(4));
            bBack = Btn(ButtonKind.Subtle, Glyph.Back, "", "Back to the log view (Esc)", tips);
            bBack.Click += delegate { if (BackRequested != null) BackRequested(this, EventArgs.Empty); };
            title = new TitleText("Browse logs");
            title.Sub = "On this computer";
            title.Fit();
            tComputer = new FluentTextBox(Glyph.Computer, "Computer name");
            tComputer.Width = Dpi.S(240);
            tComputer.Box.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; ConnectAs(); }
                else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Connect(); }
            };
            bConnect = Btn(ButtonKind.Standard, Glyph.None, "Connect", "Find logs on another computer through its admin shares, with your own account", tips);
            bConnect.Click += delegate { Connect(); };
            bConnectAs = Btn(ButtonKind.Standard, Glyph.User, "Connect as\u2026", "Connect with another account, for example a server admin account. Only LogVice uses it, so you need no extra session or Run as.", tips);
            bConnectAs.CanCompact = false;
            bConnectAs.Click += delegate { ConnectAs(); };
            bLocal = Btn(ButtonKind.Subtle, Glyph.Computer, "This computer", "Show logs on this computer", tips);
            bLocal.Click += delegate { tComputer.Text = ""; ShowFor("", localCache, null); };
            bLocal.Visible = false;
            bFollow = Btn(ButtonKind.Standard, Glyph.Search, "Follow an item", "Pick an app, deployment type, update, package or baseline from this computer's ConfigMgr client, and see every log line about it in one timeline (Ctrl+I)", tips);
            bFollow.CanCompact = true;
            bFollow.Click += delegate { if (FollowRequested != null) FollowRequested(computer); };
            header.AddLeft(bBack, title);
            header.AddRight(bFollow, new BarSep(), bLocal, tComputer, bConnect, bConnectAs);

            card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(1, Dpi.S(6), 1, Dpi.S(4));

            nav = new LocationList();
            nav.Dock = DockStyle.Left;
            nav.Width = Dpi.S(330);
            nav.SelectionChanged += delegate { LoadFiles(nav.Selected); };
            VLine vl = new VLine();
            vl.Dock = DockStyle.Left;

            right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Tag = "card";
            right.Padding = new Padding(Dpi.S(12), 0, Dpi.S(8), 0);

            locTitle = new TitleText("");
            locTitle.TitleFont = Fonts.UiSemibold;
            locTitle.Dock = DockStyle.Top;
            locTitle.Height = Dpi.S(52);

            tools = new BarPanel();
            tools.OnCard = true;
            tools.Dock = DockStyle.Top;
            tools.Height = Dpi.S(46);
            tFilter = new FluentTextBox(Glyph.Filter, "Filter file names");
            tFilter.TextChanged += delegate { files.Filter = tFilter.Text; };
            tools.Flex = tFilter;
            tools.FlexMin = Dpi.S(160);
            tools.FlexMax = Dpi.S(360);
            bHistory = Btn(ButtonKind.Standard, Glyph.Merge, "Auto-merge parts", "When you open a log, also merge the other parts it was split into: X.lo_, dated copies like X-20261104-101500.log, and WindowsUpdate.*.etl", tips);
            bHistory.Toggle = true;
            bHistory.CanCompact = true;
            bHistory.CheckedChanged += delegate { if (HistoryChanged != null) HistoryChanged(this, EventArgs.Empty); };
            bRefresh = Btn(ButtonKind.Subtle, Glyph.Reload, "", "Look for log folders again", tips);
            bRefresh.Click += delegate { ShowFor(computer, null, nav.Selected); };
            bExplorer = Btn(ButtonKind.Subtle, Glyph.Open, "", "Open this folder in File Explorer", tips);
            bExplorer.Click += delegate
            {
                LogLocation l = nav.Selected;
                if (l == null) return;
                if (l.Kind == "evt") { EventLogs.Open(l.Computer); return; }
                try { Process.Start("explorer.exe", "\"" + l.Path + "\""); } catch (Exception) { }
            };
            tools.AddLeft(tFilter, bHistory);
            bElevate = Btn(ButtonKind.Accent, Glyph.Shield, "Restart as administrator", "Windows needs administrator rights to read this folder. LogVice restarts with a UAC prompt.", tips);
            bElevate.Visible = false;
            bElevate.Click += delegate { if (ElevateRequested != null) ElevateRequested(this, EventArgs.Empty); };
            tools.AddRight(bElevate, bRefresh, bExplorer);

            files = new FileList();
            files.Dock = DockStyle.Fill;
            files.SelectionChanged += delegate { UpdateFooter(); };
            files.Activated += delegate { Open(false); };

            HLine fl = new HLine();
            fl.Dock = DockStyle.Bottom;
            footer = new BarPanel();
            footer.OnCard = true;
            footer.Dock = DockStyle.Bottom;
            footer.Height = Dpi.S(52);
            info = new TitleText("");
            info.TitleFont = Fonts.Ui;
            info.Height = Dpi.S(36);
            bAll = Btn(ButtonKind.Subtle, Glyph.Check, "Select all", "Select every file in the list (Ctrl+A)", tips);
            bAll.Click += delegate { files.SelectAll(); files.Focus(); };
            bMerge = Btn(ButtonKind.Standard, Glyph.Merge, "Merge in", "Add the selected files to the logs that are open", tips);
            bMerge.Click += delegate { Open(true); };
            bOpen = Btn(ButtonKind.Accent, Glyph.Open, "Open", "Open the selected files, merged by time (Enter)", tips);
            bOpen.Click += delegate { Open(false); };
            footer.AddLeft(info);
            footer.AddRight(bAll, bMerge, bOpen);

            right.Controls.Add(files);
            right.Controls.Add(tools);
            right.Controls.Add(locTitle);
            right.Controls.Add(fl);
            right.Controls.Add(footer);

            card.Controls.Add(right);
            card.Controls.Add(vl);
            card.Controls.Add(nav);
            Controls.Add(card);
            Controls.Add(header);
            UpdateFooter();
        }

        static FluentButton Btn(ButtonKind kind, Glyph glyph, string text, string tip, ToolTip tips)
        {
            FluentButton b = new FluentButton(kind, glyph, text);
            b.Tip = tip;
            if (tips != null && !string.IsNullOrEmpty(tip)) tips.SetToolTip(b, tip);
            return b;
        }

        public bool CanMerge
        {
            set
            {
                bMerge.Visible = value;
                footer.PerformLayout();
            }
        }

        public bool IncludeHistory
        {
            get { return bHistory.Checked; }
            set { bHistory.Checked = value; }
        }

        public void ApplyTheme(Theme t)
        {
            BackColor = t.Bg;
            ThemeAll(this, t);
            Invalidate(true);
        }

        static void ThemeAll(Control parent, Theme t)
        {
            foreach (Control c in parent.Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
                else if (c is Panel) c.BackColor = (c.Tag as string) == "card" ? t.Card : t.Bg;
                ThemeAll(c, t);
            }
        }

        public void FocusList()
        {
            if (files.Count > 0) files.Focus(); else nav.Focus();
        }

        void SafeInvoke(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        void Connect()
        {
            string pc = tComputer.Text.Trim();
            if (pc.Length == 0 || LogLocations.IsLocal(pc)) { tComputer.Text = ""; ShowFor("", localCache, null); return; }
            ShowFor(pc, null, null);
        }

        void ConnectAs()
        {
            string pc = RemoteLogin.Host(tComputer.Text);
            if (pc.Length == 0 || LogLocations.IsLocal(pc))
            {
                if (Program.IsAdmin)
                {
                    MessageBox.Show(FindForm(), "Connect as signs in to another computer with another account.\r\n\r\nType the name of a server or client in the box first, for example SRV-CM01, and select Connect as again.\r\n\r\nOn this computer LogVice already runs as administrator.", "Connect as", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (MessageBox.Show(FindForm(), "Connect as signs in to another computer with another account. Type the name of a server or client in the box first, for example SRV-CM01, and select Connect as again.\r\n\r\nTo read logs on this computer that need administrator rights, LogVice can restart as administrator instead.\r\n\r\nRestart as administrator now?", "Connect as", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (ElevateRequested != null) ElevateRequested(this, EventArgs.Empty);
                    return;
                }
                tComputer.Box.Focus();
                return;
            }
            string user = RemoteLogin.AccountFor(pc) ?? RemoteLogin.RememberedUser(pc);
            TryLogin(pc, user, 0);
        }

        void TryLogin(string pc, string user, int lastError)
        {
            string password;
            bool save;
            try
            {
                if (!RemoteLogin.Prompt(FindForm(), pc, lastError, ref user, out password, out save)) return;
            }
            catch (Exception ex)
            {
                nav.SetItems(null);
                nav.EmptyText = "Windows could not show the sign-in dialog: " + ex.Message;
                nav.Invalidate();
                return;
            }
            int g = ++gen;
            nav.SetItems(null);
            files.SetFiles(null);
            nav.EmptyText = "Signing in to " + pc + " as " + user + "\u2026";
            nav.Invalidate();
            bConnect.Enabled = bConnectAs.Enabled = false;
            string u = user, pw = password;
            Task.Factory.StartNew(delegate
            {
                RemoteLogin.Disconnect(pc);
                string note;
                int r = RemoteLogin.Connect(pc, u, pw, out note);
                if (r == 0 && save && !RemoteLogin.Save(pc, u, pw)) note = (note != null ? note + " " : "") + "The password could not be saved in Credential Manager.";
                SafeInvoke(delegate
                {
                    bConnect.Enabled = bConnectAs.Enabled = true;
                    if (g != gen) return;
                    if (r == RemoteLogin.ErrBadPassword || r == 1909 || r == 1330)
                    {
                        nav.EmptyText = RemoteLogin.Explain(r, pc);
                        nav.Invalidate();
                        TryLogin(pc, u, r);
                        return;
                    }
                    if (r != 0)
                    {
                        string why = RemoteLogin.Explain(r, pc);
                        nav.EmptyText = why;
                        nav.Invalidate();
                        MessageBox.Show(FindForm(), why, "Connect as " + u, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    ShowFor(pc, null, null);
                    if (note != null && Notice != null) Notice(note);
                });
            });
        }

        public void SetLocalCache(List<LogLocation> list)
        {
            localCache = list;
        }

        public void ShowFor(string pc, List<LogLocation> cached, LogLocation select)
        {
            computer = pc ?? "";
            bool remote = computer.Length > 0;
            bLocal.Visible = remote;
            string account = remote ? RemoteLogin.AccountFor(computer) : null;
            title.Sub = remote ? "On " + computer + (account != null ? "  \u00B7  as " + account : "") : "On this computer";
            title.Fit();
            header.PerformLayout();
            int g = ++gen;
            files.SetFiles(null);
            files.EmptyText = "";
            locTitle.Text = "";
            locTitle.Sub = "";
            locTitle.Invalidate();
            string selPath = select != null ? select.Path : null;
            if (cached != null && !remote)
            {
                Apply(g, cached, null, selPath);
                return;
            }
            nav.SetItems(null);
            nav.EmptyText = remote ? "Connecting to " + computer + "…" : "Looking for log folders…";
            nav.Invalidate();
            List<string> custom = CustomFolders != null ? CustomFolders() : null;
            Task.Factory.StartNew(delegate
            {
                string err = null;
                List<LogLocation> list = null;
                try
                {
                    string target = remote ? RemoteLogin.PathHost(computer) : null;
                    if (remote && !LogLocations.Reachable(target, 8000, out err))
                    {
                        list = new List<LogLocation>();
                        if (RemoteLogin.AccountFor(computer) != null) err = RemoteLogin.Explain(5, computer);
                        else err += " To use another account, select Connect as.";
                    }
                    else list = LogLocations.Discover(target, remote ? null : custom);
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                    list = new List<LogLocation>();
                }
                if (!remote) localCache = list;
                SafeInvoke(delegate { Apply(g, list, err, selPath); });
            });
        }

        void Apply(int g, List<LogLocation> list, string err, string selPath)
        {
            if (g != gen) return;
            nav.SetItems(list);
            if (list.Count == 0)
            {
                nav.EmptyText = err ?? (computer.Length > 0
                    ? "No log folders were found on " + computer + "."
                    : "No log folders were found on this computer. Add your own folders in Settings.");
                nav.Invalidate();
                files.EmptyText = "";
                UpdateFooter();
                return;
            }
            LogLocation pick = list[0];
            if (selPath != null)
                foreach (LogLocation l in list)
                    if (string.Equals(l.Path, selPath, StringComparison.OrdinalIgnoreCase)) { pick = l; break; }
            nav.Select(pick);
        }

        void LoadFiles(LogLocation l)
        {
            if (l == null) return;
            int g = ++gen;
            locTitle.Text = l.Group + "  ·  " + l.Name;
            locTitle.Sub = l.Kind == "evt" ? l.Where : l.Path + (l.Pattern != "*.log;*.lo_" ? "   (" + l.Pattern.Replace(";", ", ") + ")" : "");
            locTitle.Invalidate();
            files.SetFiles(null);
            files.EmptyText = l.Kind == "evt" ? "Reading the event log list…" : "Reading the folder…";
            tips.SetToolTip(bExplorer, l.Kind == "evt" ? "Open Event Viewer" + (l.Computer != null ? " for " + l.Computer : "") : "Open this folder in File Explorer");
            files.Invalidate();
            if (bElevate.Visible) { bElevate.Visible = false; tools.PerformLayout(); }
            Task.Factory.StartNew(delegate
            {
                List<LogFile> list;
                try { list = LogLocations.List(l); } catch (Exception) { list = new List<LogFile>(); }
                SafeInvoke(delegate
                {
                    if (g != gen) return;
                    files.EmptyText = list.Count == 0 ? (l.Kind == "evt" ? "No event logs with events were found." : "No log files in this folder right now.") : "No file names match the filter.";
                    if (l.Problem != null) files.EmptyText = l.Problem;
                    if (l.Denied)
                    {
                        bool remoteNow = computer.Length > 0;
                        files.EmptyText = remoteNow
                            ? "Your account cannot read this folder on " + computer + ". Select Connect as and use an account that is an administrator there."
                            : Program.IsAdmin ? "Windows does not allow this account to read this folder." : "Windows needs administrator rights to show this folder. Select Restart as administrator.";
                        bElevate.Visible = !remoteNow && !Program.IsAdmin;
                        tools.PerformLayout();
                    }
                    files.SetFiles(list);
                    files.Filter = tFilter.Text;
                    UpdateFooter();
                });
            });
        }

        void UpdateFooter()
        {
            List<LogFile> sel = files.Selection();
            long size = 0;
            foreach (LogFile f in sel) size += f.Size;
            if (sel.Count > 0)
                info.Text = string.Format("{0:N0} of {1:N0} files selected  ·  {2}", sel.Count, files.Count, Fmt.Size(size));
            else
                info.Text = files.Count > 0 ? string.Format("{0:N0} files  ·  Ctrl+click or Shift+click to pick several, double-click to open", files.Count) : "";
            info.Fit();
            bOpen.Enabled = bMerge.Enabled = sel.Count > 0;
            bOpen.Text = sel.Count > 1 ? string.Format("Open {0:N0} files", sel.Count) : "Open";
            bAll.Enabled = files.Count > 0;
            footer.PerformLayout();
        }

        void Open(bool merge)
        {
            List<LogFile> sel = files.Selection();
            if (sel.Count == 0) return;
            List<string> paths = new List<string>();
            foreach (LogFile f in sel) paths.Add(f.Path);
            if (OpenRequested != null) OpenRequested(paths.ToArray(), merge);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (BackRequested != null) BackRequested(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
