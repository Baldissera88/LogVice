namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public sealed class MainForm : Form
    {
        const string HelpText = "Select a row to see the full entry. Error codes in the message are links to Error lookup.\r\n\r\n" +
            "Ctrl+O  open logs          Ctrl+Shift+O  merge in\r\n" +
            "Ctrl+B  browse logs        Ctrl+,         settings and about\r\n" +
            "Ctrl+F  find               F3 / Shift+F3  next / previous match\r\n" +
            "F8      next error         Shift+F8       previous error\r\n" +
            "F7      next highlight     Shift+F7       previous highlight\r\n" +
            "Ctrl+G  go to line         Alt+Home       start page\r\n" +
            "Ctrl+L  error lookup       F5             reload\r\n" +
            "Ctrl+C  copy rows          Ctrl+A         select all rows";

        Theme theme;
        Store st = new Store();
        int[] view = new int[16];
        int viewCount;
        volatile int viewGen, findGen;
        byte[] hitMask;
        int[] hitPos = new int[16];
        int hitCount;
        bool jumpOnFind;
        int selEntry = -1;
        bool loading, filtering, suppress, reloading, homeMode, vice;
        string typed = "";
        List<HighlightRule> rules = new List<HighlightRule>();
        HighlightRule[] markRules = new HighlightRule[0];
        byte[] marks;
        int[] markCounts = new int[0];
        int markAny;
        volatile int markGen;
        int hlFilter = -1;
        int partsAdded;
        string[] chosenPaths;
        List<Codes.Span> links = new List<Codes.Span>();
        Font linkFont;
        int tipLink = -2;
        LoadToken loadTok;
        string lastDir;
        string loadSummary = "";
        int types = 7;
        string compName;
        int threadF = -1;
        long fromK = long.MinValue, toK = long.MaxValue;
        Regex filterRx, findRx;
        readonly string[] startPaths;
        readonly bool eggMode;
        string eggPath;
        DateTime eggStart;
        int eggLine, konamiPos;
        Timer eggTimer;
        static readonly Keys[] Konami = { Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right, Keys.B, Keys.A };
        static readonly string[][] EggText =
        {
            new string[] { "1", "LogVice", "Starting the LogVice credits. Thank you for reading logs with us." },
            new string[] { "1", "Credits", "Design and development by Daniel L. Baldissera." },
            new string[] { "1", "Credits", "Built for everyone who has scrolled CcmExec.log at two in the morning." },
            new string[] { "1", "PolicyAgent", "Evaluating policy \"Read logs faster\". Result: compliant." },
            new string[] { "1", "AppEnforce", "Executing command line: msiexec /i \"Patience.msi\" /qn /norestart" },
            new string[] { "1", "AppEnforce", "Process 1337 terminated with exitcode: 3010. Patience is installed and needs a restart." },
            new string[] { "2", "CoffeeMaker", "Coffee level at 12 percent. Refill recommended before the next maintenance window." },
            new string[] { "3", "CoffeeMaker", "Brewing failed with error 0x00C0FFEE. Developer productivity degraded." },
            new string[] { "1", "CoffeeMaker", "Retry 1 of 1: fresh pot brewed. Productivity restored." },
            new string[] { "1", "LocationServices", "Current location: right where the logs are." },
            new string[] { "3", "MemoryCheck", "Freed memory still reads 0xDEADBEEF. Nobody knows who wrote it." },
            new string[] { "2", "WUAHandler", "Scan found 1 applicable update: more coffee." },
            new string[] { "1", "Credits", "Thanks to every admin who reads the logs so users never have to." },
            new string[] { "1", "Credits", "Tip: press F8 to jump between errors. This log has exactly two." },
            new string[] { "1", "LogVice", "Credits complete. You found the Easter egg. Now go find that bug." }
        };

        BarPanel cmdBar, filterBar;
        FluentButton bUpdate, bTheme, bHome, bOpen, bRecent, bBrowse, bMerge, bReload, bStop, bLive, bLookup, bFindPrev, bFindNext, bErrPrev, bErrNext, bSettings;
        FluentButton bExcl, bRegex, chipErr, chipWarn, chipInfo, bComp, bHl, bFrom, bTo, bClear;
        FluentTextBox tFind, tFilter, tThread;
        Panel content;
        SplitContainer split;
        LogGrid grid;
        StartPage start;
        DropOverlay drop;
        CardPanel detailCard;
        FlowLayoutPanel metaFlow, actFlow;
        SeverityBadge badge;
        MetaItem mTime, mDelta, mLine, mComp, mThread, mSource, mFile, mHl;
        FluentButton aThread, aComp, aFrom, aTo, aCopy;
        Panel body;
        RichTextBox tMsg;
        CodePanel codes;
        StatusBar status;
        ToolTip tips;
        MenuRenderer renderer;
        ContextMenuStrip compMenu, recentMenu, rowMenu, hlMenu;
        LookupForm lookup;
        BrowseView browse;
        SettingsView settingsView;
        enum ViewKind { Logs, Browse, Settings }
        ViewKind viewKind = ViewKind.Logs;
        List<LogLocation> localLocations;
        volatile int discoverGen;
        readonly Timer tailTimer = new Timer();
        readonly Timer filterTimer = new Timer();
        readonly Timer findTimer = new Timer();
        readonly Timer threadTimer = new Timer();
        readonly Timer noteTimer = new Timer();

        public MainForm(string[] paths) : this(paths, false)
        {
        }

        public MainForm(string[] paths, bool egg)
        {
            startPaths = paths ?? new string[0];
            eggMode = egg;
            theme = WantedTheme();
            Fonts.SetLogSize(Settings.GetInt("TextSize", 1, 0, 2));

            Text = TitlePrefix + "LogVice";
            Font = Fonts.Ui;
            KeyPreview = true;
            AllowDrop = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Dpi.S(1360), Dpi.S(880));
            MinimumSize = new Size(Dpi.S(900), Dpi.S(560));
            Icon appIcon = Program.AppIcon();
            if (appIcon != null) Icon = appIcon;

            renderer = new MenuRenderer();
            tips = new ToolTip();
            tips.OwnerDraw = true;
            tips.InitialDelay = 500;
            tips.Popup += delegate (object s, PopupEventArgs e)
            {
                Size sz = Draw.Measure(tips.GetToolTip(e.AssociatedControl), Fonts.Small);
                e.ToolTipSize = new Size(sz.Width + Dpi.S(18), sz.Height + Dpi.S(12));
            };
            tips.Draw += delegate (object s, DrawToolTipEventArgs e)
            {
                e.Graphics.Clear(theme.Card);
                using (Pen p = new Pen(theme.IsDark ? Theme.Hex(0x454545) : Theme.Hex(0xD6D6D6)))
                    e.Graphics.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, Fonts.Small, e.Bounds, theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };

            BuildCommandBar();
            BuildFilterBar();
            BuildContent();
            status = new StatusBar();
            status.Dock = DockStyle.Bottom;

            Controls.Add(content);
            Controls.Add(filterBar);
            Controls.Add(cmdBar);
            Controls.Add(status);

            compMenu = NewMenu();
            recentMenu = NewMenu();
            rowMenu = NewMenu();
            hlMenu = NewMenu();
            rules = HighlightRule.Load();

            foreach (Control c in new Control[] { this, cmdBar, filterBar, grid, start, detailCard, tMsg, drop, status }) HookDrop(c);
            grid.Tint = Settings.GetBool("Tint", true);
            grid.ShowLines = Settings.GetBool("LineNumbers", true);
            grid.SetDensity(Settings.GetInt("Density", 1, 0, 2));
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreference;

            tailTimer.Interval = 1000;
            tailTimer.Tick += delegate { TailTick(); };
            filterTimer.Interval = 300;
            filterTimer.Tick += delegate { filterTimer.Stop(); ApplyFilterText(); };
            findTimer.Interval = 250;
            findTimer.Tick += delegate { findTimer.Stop(); ApplyFindText(); };
            threadTimer.Interval = 350;
            threadTimer.Tick += delegate { threadTimer.Stop(); ApplyThreadText(); };
            noteTimer.Interval = 4000;
            noteTimer.Tick += delegate { noteTimer.Stop(); UpdateStatus(); };

            FormClosing += delegate { if (!eggMode) SaveWindow(); };
            FormClosed += delegate
            {
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreference;
                tailTimer.Stop();
                if (eggTimer != null) eggTimer.Stop();
                if (loadTok != null) loadTok.State = 2;
                CloseSources(st);
                try { RemoteLogin.DisconnectAll(); } catch (Exception) { }
                if (eggPath != null)
                {
                    try { File.Delete(eggPath); Directory.Delete(Path.GetDirectoryName(eggPath)); } catch (Exception) { }
                }
            };
            ApplyTheme(theme);
        }

        // ---------- building ----------

        FluentButton Btn(ButtonKind kind, Glyph glyph, string text, string tip, EventHandler click)
        {
            FluentButton b = new FluentButton(kind, glyph, text);
            b.Tip = tip;
            if (!string.IsNullOrEmpty(tip)) tips.SetToolTip(b, tip);
            if (click != null) b.Click += click;
            return b;
        }

        FluentButton Chip(int sev, string label)
        {
            FluentButton b = Btn(ButtonKind.Chip, Glyph.None, label, "Show or hide " + label.ToLowerInvariant(), null);
            b.Severity = sev;
            b.Toggle = true;
            b.Checked = true;
            b.CanCompact = true;
            b.Count = "0";
            b.CheckedChanged += delegate
            {
                int bit = 1 << (sev - 1);
                types = b.Checked ? types | bit : types & ~bit;
                if (!suppress) RebuildView(false);
            };
            return b;
        }

        void BuildCommandBar()
        {
            cmdBar = new BarPanel();
            cmdBar.Dock = DockStyle.Top;
            cmdBar.Height = Dpi.S(54);
            cmdBar.Padding = new Padding(Dpi.S(12), Dpi.S(6), Dpi.S(12), 0);
            bHome = Btn(ButtonKind.Subtle, Glyph.Home, "", "Start page (Alt+Home)", delegate { ToggleHome(); });
            bOpen = Btn(ButtonKind.Accent, Glyph.Open, "Open", "Open log files (Ctrl+O)", delegate { PickFiles(false, null); });
            bRecent = Btn(ButtonKind.Subtle, Glyph.Recent, "", "Recent files", delegate { ShowRecentMenu(); });
            bRecent.Trailing = Glyph.Chevron;
            bBrowse = Btn(ButtonKind.Subtle, Glyph.Browse, "Browse logs", "Find ConfigMgr, Intune and Windows logs on this or another computer (Ctrl+B)", delegate { if (viewKind == ViewKind.Browse) ShowView(ViewKind.Logs); else OpenBrowse(null); });
            bBrowse.CanCompact = true;
            bMerge = Btn(ButtonKind.Subtle, Glyph.Merge, "Merge in", "Add log files and interleave them by time (Ctrl+Shift+O)", delegate { PickFiles(true, null); });
            bMerge.CanCompact = true;
            bReload = Btn(ButtonKind.Subtle, Glyph.Reload, "Reload", "Read the open files again (F5)", delegate { Reload(); });
            bReload.CanCompact = true;
            bStop = Btn(ButtonKind.Standard, Glyph.Stop, "Stop", "Stop reading and show what has been read so far", delegate { if (loadTok != null) loadTok.State = 1; });
            bStop.Visible = false;
            bLive = Btn(ButtonKind.Subtle, Glyph.Live, "Live", "Follow the files as new lines are written", null);
            bLive.Toggle = true;
            bLive.Checked = Settings.GetBool("Live", true);
            bLive.CheckedChanged += delegate
            {
                if (bLive.Checked && !loading && st.Sources.Count > 0) tailTimer.Start();
                else tailTimer.Stop();
            };
            bLookup = Btn(ButtonKind.Subtle, Glyph.Lookup, "Error lookup", "Explain an error code (Ctrl+L)", delegate { ShowLookup(null); });
            bLookup.CanCompact = true;

            tFind = new FluentTextBox(Glyph.Search, "Find (Ctrl+F)");
            tFind.Width = Dpi.S(280);
            tFind.TextChanged += delegate { if (!suppress) { findTimer.Stop(); findTimer.Start(); } };
            tFind.Box.KeyDown += OnFindKey;
            bFindPrev = Btn(ButtonKind.Subtle, Glyph.Up, "", "Previous match (Shift+F3)", delegate { FindStep(-1); });
            bFindNext = Btn(ButtonKind.Subtle, Glyph.Down, "", "Next match (F3)", delegate { FindStep(1); });
            bErrPrev = Btn(ButtonKind.Subtle, Glyph.ErrUp, "", "Previous error (Shift+F8)", delegate { ErrStep(-1); });
            bErrNext = Btn(ButtonKind.Subtle, Glyph.ErrDown, "", "Next error (F8)", delegate { ErrStep(1); });
            bTheme = Btn(ButtonKind.Subtle, Glyph.Moon, "", "Switch to the dark theme", delegate { ToggleTheme(); });
            bSettings = Btn(ButtonKind.Subtle, Glyph.Gear, "", "Settings and about (Ctrl+,)", delegate { if (viewKind == ViewKind.Settings) ShowView(ViewKind.Logs); else OpenSettings(0); });

            cmdBar.AddLeft(bHome, bOpen, bRecent, bBrowse, bMerge, bReload, bStop, new BarSep(), bLive, bLookup);
            bUpdate = Btn(ButtonKind.Accent, Glyph.Live, "Update", "A newer version of LogVice is available", delegate { OpenSettings(2); });
            bUpdate.Visible = false;
            cmdBar.AddRight(tFind, bFindPrev, bFindNext, new BarSep(), bErrPrev, bErrNext, new BarSep(), bUpdate, bTheme, bSettings);
        }

        void BuildFilterBar()
        {
            filterBar = new BarPanel();
            filterBar.Dock = DockStyle.Top;
            filterBar.Height = Dpi.S(48);
            filterBar.Padding = new Padding(Dpi.S(12), 0, Dpi.S(12), Dpi.S(4));
            tFilter = new FluentTextBox(Glyph.Filter, "Filter rows");
            tFilter.TextChanged += delegate { if (!suppress) { filterTimer.Stop(); filterTimer.Start(); } };
            filterBar.Flex = tFilter;
            filterBar.FlexMin = Dpi.S(170);
            filterBar.FlexMax = Dpi.S(420);
            bExcl = Btn(ButtonKind.Standard, Glyph.None, "Exclude", "Hide matching rows instead of showing only them", null);
            bExcl.Toggle = true;
            bExcl.CheckedChanged += delegate { if (!suppress && filterRx != null) RebuildView(false); };
            bRegex = Btn(ButtonKind.Standard, Glyph.None, ".*", "Use regular expressions in filter and find", null);
            bRegex.Font = Fonts.Mono;
            bRegex.Width = bRegex.GetPreferredSize(Size.Empty).Width;
            bRegex.Toggle = true;
            bRegex.CheckedChanged += delegate { if (!suppress) { ApplyFilterText(); ApplyFindText(); } };
            chipErr = Chip(3, "Errors");
            chipWarn = Chip(2, "Warnings");
            chipInfo = Chip(1, "Info");
            bComp = Btn(ButtonKind.Standard, Glyph.None, "All components", "Show one component", delegate { ShowCompMenu(); });
            bComp.Trailing = Glyph.Chevron;
            bHl = Btn(ButtonKind.Standard, Glyph.Highlight, "Highlights", "Show only the rows of one highlight rule", delegate { ShowHlMenu(); });
            bHl.Trailing = Glyph.Chevron;
            bHl.Visible = false;
            tThread = new FluentTextBox(Glyph.None, "Thread");
            tThread.Width = Dpi.S(96);
            tThread.TextChanged += delegate { if (!suppress) { threadTimer.Stop(); threadTimer.Start(); } };
            bFrom = Btn(ButtonKind.Subtle, Glyph.None, "", "Remove this time limit", delegate { fromK = long.MinValue; bFrom.Visible = false; filterBar.PerformLayout(); RebuildView(false); });
            bTo = Btn(ButtonKind.Subtle, Glyph.None, "", "Remove this time limit", delegate { toK = long.MaxValue; bTo.Visible = false; filterBar.PerformLayout(); RebuildView(false); });
            foreach (FluentButton b in new FluentButton[] { bFrom, bTo })
            {
                b.Toggle = false;
                b.Trailing = Glyph.Close;
                b.Visible = false;
            }
            bClear = Btn(ButtonKind.Subtle, Glyph.Close, "Clear filters", "Show every entry again", delegate { ClearFilters(); });
            filterBar.AddLeft(tFilter, bExcl, bRegex, new BarSep(), chipErr, chipWarn, chipInfo, new BarSep(), bComp, bHl, tThread, bFrom, bTo);
            filterBar.AddRight(bClear);
        }

        void BuildContent()
        {
            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(Dpi.S(12), Dpi.S(2), Dpi.S(12), Dpi.S(4));

            grid = new LogGrid();
            grid.Dock = DockStyle.Fill;
            grid.SelectionChanged += delegate { if (!suppress) OnSelChanged(); };
            grid.RowMenu += delegate (object s, MouseEventArgs e) { ShowRowMenu(e.Location); };

            start = new StartPage();
            start.Dock = DockStyle.Fill;
            start.OpenButton.Click += delegate { PickFiles(false, null); };
            start.BrowseButton.Click += delegate { OpenBrowse(null); };
            start.OpenLocation += delegate (LogLocation l) { OpenBrowse(l); };
            start.OpenRecent += delegate (string[] p) { OpenFiles(p, false, false); };
            start.ResumeButton.Click += delegate { LeaveHome(); };
            foreach (FluentButton b in start.Buttons) if (!string.IsNullOrEmpty(b.Tip)) tips.SetToolTip(b, b.Tip);

            split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.FixedPanel = FixedPanel.Panel2;
            split.SplitterWidth = Dpi.S(8);
            split.TabStop = false;
            split.Panel1.Controls.Add(grid);
            split.Panel1.Controls.Add(start);
            split.Paint += delegate (object s, PaintEventArgs e)
            {
                Rectangle r = split.SplitterRectangle;
                int w = Dpi.S(36), h = Dpi.S(3);
                Draw.Fill(e.Graphics, new RectangleF(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f, w, h), h / 2f, theme.Divider);
            };

            detailCard = new CardPanel();
            detailCard.Dock = DockStyle.Fill;
            detailCard.Padding = new Padding(Dpi.S(16), Dpi.S(12), Dpi.S(14), Dpi.S(10));

            metaFlow = new FlowLayoutPanel();
            metaFlow.Dock = DockStyle.Top;
            metaFlow.AutoSize = true;
            metaFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            metaFlow.WrapContents = true;
            metaFlow.Margin = Padding.Empty;
            metaFlow.Tag = "card";
            badge = new SeverityBadge();
            badge.Margin = new Padding(0, Dpi.S(5), Dpi.S(22), Dpi.S(6));
            mTime = new MetaItem("Time", true);
            mDelta = new MetaItem("Since previous row", true);
            mLine = new MetaItem("Line", true);
            mHl = new MetaItem("Highlight", false);
            mComp = new MetaItem("Component", false);
            mThread = new MetaItem("Thread", true);
            mSource = new MetaItem("Source", true);
            mFile = new MetaItem("Log file", false);
            metaFlow.Controls.AddRange(new Control[] { badge, mTime, mDelta, mLine, mComp, mThread, mSource, mFile, mHl });

            actFlow = new FlowLayoutPanel();
            actFlow.Dock = DockStyle.Top;
            actFlow.AutoSize = true;
            actFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actFlow.WrapContents = true;
            actFlow.Tag = "card";
            aThread = Btn(ButtonKind.Standard, Glyph.Filter, "Only this thread", "Show only rows from this thread", delegate { OnlyThread(); });
            aComp = Btn(ButtonKind.Standard, Glyph.Filter, "Only this component", "Show only rows from this component", delegate { OnlyComp(); });
            aFrom = Btn(ButtonKind.Standard, Glyph.Up, "Hide earlier", "Hide rows before this one", delegate { HideBefore(true); });
            aTo = Btn(ButtonKind.Standard, Glyph.Down, "Hide later", "Hide rows after this one", delegate { HideBefore(false); });
            aCopy = Btn(ButtonKind.Standard, Glyph.Copy, "Copy", "Copy the selected rows (Ctrl+C)", delegate { CopySelection(false); });
            foreach (FluentButton b in new FluentButton[] { aThread, aComp, aFrom, aTo, aCopy })
            {
                b.Height = Dpi.S(30);
                b.Width = b.GetPreferredSize(Size.Empty).Width;
                b.Margin = new Padding(0, 0, Dpi.S(6), Dpi.S(4));
                actFlow.Controls.Add(b);
            }

            HLine line = new HLine();
            line.Dock = DockStyle.Top;

            body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Tag = "card";
            tMsg = new RichTextBox();
            tMsg.ReadOnly = true;
            tMsg.BorderStyle = BorderStyle.None;
            tMsg.ScrollBars = RichTextBoxScrollBars.Vertical;
            tMsg.WordWrap = true;
            tMsg.DetectUrls = false;
            tMsg.Dock = DockStyle.Fill;
            tMsg.Font = Fonts.Detail;
            tMsg.MaxLength = int.MaxValue;
            tMsg.HideSelection = false;
            tMsg.ShortcutsEnabled = true;
            tMsg.HandleCreated += delegate { NativeMethods.ScrollTheme(tMsg, theme.IsDark); };
            tMsg.MouseMove += delegate (object s, MouseEventArgs e)
            {
                int k = LinkAt(e.Location);
                tMsg.Cursor = k >= 0 ? Cursors.Hand : Cursors.IBeam;
                if (k != tipLink)
                {
                    tipLink = k;
                    tips.SetToolTip(tMsg, k >= 0 ? "Look up " + Codes.Describe(links[k].Code).Title : "");
                }
            };
            tMsg.MouseUp += delegate (object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || tMsg.SelectionLength > 0) return;
                int k = LinkAt(e.Location);
                if (k >= 0) ShowLookup(Codes.Describe(links[k].Code).Title);
            };
            codes = new CodePanel();
            codes.CodeClicked += delegate (uint u) { ShowLookup(Codes.Describe(u).Title); };
            codes.Dock = DockStyle.Right;
            codes.Width = Dpi.S(340);
            codes.Visible = false;
            body.Controls.Add(tMsg);
            body.Controls.Add(codes);

            detailCard.Controls.Add(body);
            detailCard.Controls.Add(line);
            detailCard.Controls.Add(actFlow);
            detailCard.Controls.Add(metaFlow);
            split.Panel2.Controls.Add(detailCard);

            drop = new DropOverlay();
            drop.Dock = DockStyle.Fill;
            drop.Visible = false;

            browse = new BrowseView(tips);
            browse.Dock = DockStyle.Fill;
            browse.Visible = false;
            browse.CustomFolders = delegate { return Settings.GetList("Folders"); };
            browse.BackRequested += delegate { ShowView(ViewKind.Logs); };
            browse.OpenRequested += delegate (string[] paths, bool merge)
            {
                ShowView(ViewKind.Logs);
                OpenFiles(paths, merge && st.Sources.Count > 0, false);
            };
            browse.ElevateRequested += delegate { Elevate(OpenPaths()); };
            browse.Notice += delegate (string text) { Note(text); };
            browse.HistoryChanged += delegate
            {
                Settings.SetBool("AutoMerge", browse.IncludeHistory);
                settingsView.SetAutoMerge(browse.IncludeHistory);
            };

            settingsView = new SettingsView(tips);
            settingsView.Dock = DockStyle.Fill;
            settingsView.Visible = false;
            settingsView.BackRequested += delegate { ShowView(ViewKind.Logs); };
            settingsView.SettingsChanged += delegate { ApplySettings(); };
            settingsView.FoldersChanged += delegate { Discover(); };
            settingsView.BrowseRequested += delegate { OpenBrowse(null); };
            settingsView.RecentCleared += delegate { start.SetRecent(Settings.Recent()); };
            settingsView.EasterEgg += delegate { ShowEgg(); };
            settingsView.ElevateRequested += delegate { Elevate(OpenPaths()); };
            settingsView.HighlightsChanged += delegate
            {
                rules = new List<HighlightRule>(settingsView.Rules);
                if (hlFilter > 0) hlFilter = -1;
                RebuildMarks();
            };

            content.Controls.Add(split);
            content.Controls.Add(browse);
            content.Controls.Add(settingsView);
            content.Controls.Add(drop);
        }

        // ---------- views and settings ----------

        void ShowView(ViewKind v)
        {
            viewKind = v;
            SuspendLayout();
            browse.Visible = v == ViewKind.Browse;
            settingsView.Visible = v == ViewKind.Settings;
            split.Visible = v == ViewKind.Logs;
            filterBar.Visible = v == ViewKind.Logs;
            if (v == ViewKind.Browse) browse.BringToFront();
            if (v == ViewKind.Settings) settingsView.BringToFront();
            bBrowse.Checked = v == ViewKind.Browse;
            bSettings.Checked = v == ViewKind.Settings;
            bHome.Checked = v == ViewKind.Logs && start.Visible;
            tFind.Enabled = v == ViewKind.Logs;
            ResumeLayout(true);
            if (v == ViewKind.Logs) ShowStart();
            if (v == ViewKind.Logs) { if (grid.Visible && st.Sources.Count > 0 && !homeMode) grid.Focus(); }
            else if (v == ViewKind.Browse) browse.FocusList();
        }

        void OpenBrowse(LogLocation select)
        {
            browse.CanMerge = st.Sources.Count > 0 && !loading;
            browse.IncludeHistory = AutoMerge;
            browse.SetLocalCache(localLocations);
            ShowView(ViewKind.Browse);
            browse.ShowFor("", localLocations, select);
        }

        void OpenSettings(int tab)
        {
            settingsView.LoadValues();
            ShowView(ViewKind.Settings);
            settingsView.ShowTab(tab);
        }

        void Discover()
        {
            int g = ++discoverGen;
            List<string> custom = Settings.GetList("Folders");
            Task.Factory.StartNew(delegate
            {
                List<LogLocation> list;
                try { list = LogLocations.Discover(null, custom); }
                catch (Exception) { list = new List<LogLocation>(); }
                SafeInvoke(delegate
                {
                    if (g != discoverGen) return;
                    localLocations = list;
                    start.SetLocations(list);
                    settingsView.SetLocations(list);
                    browse.SetLocalCache(list);
                });
            });
        }

        Theme WantedTheme()
        {
            if (vice) return Theme.Vice();
            string pref = Settings.Get("Theme");
            bool dark = pref == "Dark" || (pref != "Light" && Theme.SystemPrefersDark());
            return dark ? Theme.Dark() : Theme.Light();
        }

        void ToggleTheme()
        {
            bool toDark = !theme.IsDark || vice;
            if (vice) { vice = false; start.Eyebrow = null; toDark = false; }
            Settings.Set("Theme", toDark ? "Dark" : "Light");
            settingsView.LoadValues();
            ApplySettings();
            Note(toDark ? "Dark theme. Settings > Appearance has System too." : "Light theme. Settings > Appearance has System too.");
        }

        void ToggleVice()
        {
            vice = !vice;
            start.Eyebrow = vice ? "LOGVICE  \u00B7  VICE MODE" : null;
            ApplyTheme(WantedTheme());
            start.Invalidate();
            Note(vice ? "Vice mode. Neon on, pastel suits optional, errors off the streets. Type VICE again to go back." : "Back from Miami. Normal colours restored.");
        }

        static Control FocusedLeaf(Control c)
        {
            ContainerControl cc = c as ContainerControl;
            while (cc != null && cc.ActiveControl != null)
            {
                c = cc.ActiveControl;
                cc = c as ContainerControl;
            }
            return c;
        }

        void ApplySettings()
        {
            Theme t = WantedTheme();
            if (t.Name != theme.Name) ApplyTheme(t);
            int size = Settings.GetInt("TextSize", 1, 0, 2);
            if (size != Fonts.LogSize)
            {
                Fonts.SetLogSize(size);
                tMsg.Font = Fonts.Detail;
            }
            grid.SetDensity(Settings.GetInt("Density", 1, 0, 2));
            grid.Tint = Settings.GetBool("Tint", true);
            grid.ShowLines = Settings.GetBool("LineNumbers", true);
            grid.Invalidate();
            ShowDetails();
            metaFlow.PerformLayout();
        }

        void OnUserPreference(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            SafeInvoke(delegate
            {
                string pref = Settings.Get("Theme");
                if (pref == "Light" || pref == "Dark" || vice) return;
                Theme t = WantedTheme();
                if (t.IsDark != theme.IsDark) ApplyTheme(t);
            });
        }

        ContextMenuStrip NewMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = renderer;
            m.ShowImageMargin = false;
            m.ShowCheckMargin = false;
            m.Font = Fonts.Ui;
            m.Padding = new Padding(0, Dpi.S(4), 0, Dpi.S(4));
            m.Opened += delegate { NativeMethods.RoundPopup(m.Handle); };
            return m;
        }

        ToolStripMenuItem Item(string text, string shortcut, bool enabled, EventHandler click)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(text);
            mi.Padding = new Padding(Dpi.S(4), Dpi.S(5), Dpi.S(4), Dpi.S(5));
            if (!string.IsNullOrEmpty(shortcut)) mi.ShortcutKeyDisplayString = shortcut;
            mi.Enabled = enabled;
            if (click != null) mi.Click += click;
            return mi;
        }

        // ---------- theme and window ----------

        void ApplyTheme(Theme t)
        {
            theme = t;
            renderer.Theme = t;
            SuspendLayout();
            BackColor = t.Bg;
            ThemeTree(this, t);
            tMsg.BackColor = t.Card;
            tMsg.ForeColor = t.Text;
            foreach (ContextMenuStrip m in new ContextMenuStrip[] { compMenu, recentMenu, rowMenu })
            {
                if (m == null) continue;
                m.BackColor = t.Card;
                m.ForeColor = t.Text;
            }
            bErrPrev.GlyphColor = bErrNext.GlyphColor = t.ErrFg;
            bTheme.Glyph = t.IsDark ? Glyph.Sun : Glyph.Moon;
            bTheme.Tip = t.Name == "Vice" ? "Leave Vice mode and use the light theme" : t.IsDark ? "Switch to the light theme" : "Switch to the dark theme";
            tips.SetToolTip(bTheme, bTheme.Tip);
            ResumeLayout(true);
            if (IsHandleCreated) NativeMethods.StyleWindow(Handle, t);
            NativeMethods.ScrollTheme(tMsg, t.IsDark);
            if (lookup != null && !lookup.IsDisposed) lookup.ApplyTheme(t);
            Invalidate(true);
            if (mHl != null) ShowDetails();
        }

        static void ThemeTree(Control parent, Theme t)
        {
            foreach (Control c in parent.Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
                else if (c is Panel || c is SplitterPanel || c is SplitContainer || c is FlowLayoutPanel)
                    c.BackColor = (c.Tag as string) == "card" ? t.Card : t.Bg;
                if (!(c is StartPage) && !(c is BrowseView) && !(c is SettingsView)) ThemeTree(c, t);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!eggMode) RestoreWindow();
            try
            {
                split.Panel1MinSize = Dpi.S(160);
                split.Panel2MinSize = Dpi.S(150);
                split.SplitterDistance = Math.Max(Dpi.S(160), (int)(split.Height * 0.66));
            }
            catch (Exception) { }
            lastDir = Settings.Get("LastFolder");
            start.SetRecent(Settings.Recent());
            if (!eggMode) Discover();
            if (!eggMode && Updates.Enabled && Updates.DueToday) Updates.CheckAsync(delegate (UpdateInfo r) { SafeInvoke(delegate { UpdateFound(r); }); });
            RefreshCounts();
            ShowStart();
            ShowDetails();
            UpdateStatus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (eggMode) StartEgg();
            else if (startPaths.Length > 0) OpenFiles(startPaths, false, false);
            else if (Settings.GetBool("ReopenLast", false))
            {
                List<string[]> r = Settings.Recent();
                if (r.Count > 0)
                {
                    bool all = true;
                    foreach (string p in r[0]) { if (!EventLogs.Available(p)) all = false; }
                    if (all) OpenFiles(r[0], false, false);
                }
            }
        }

        void RestoreWindow()
        {
            string s = Settings.Get("Window");
            if (string.IsNullOrEmpty(s)) return;
            string[] p = s.Split(',');
            int x, y, w, h;
            if (p.Length < 5 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return;
            Rectangle r = new Rectangle(x, y, Math.Max(w, MinimumSize.Width), Math.Max(h, MinimumSize.Height));
            foreach (Screen sc in Screen.AllScreens)
            {
                if (sc.WorkingArea.IntersectsWith(new Rectangle(r.X + Dpi.S(40), r.Y, Math.Max(1, r.Width - Dpi.S(80)), Dpi.S(40))))
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = r;
                    if (p[4] == "1") WindowState = FormWindowState.Maximized;
                    return;
                }
            }
        }

        void SaveWindow()
        {
            Rectangle r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Settings.Set("Window", string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}", r.X, r.Y, r.Width, r.Height, WindowState == FormWindowState.Maximized ? 1 : 0));
            if (lastDir != null) Settings.Set("LastFolder", lastDir);
        }

        void ShowStart()
        {
            bool has = st.Sources.Count > 0 && !loading;
            if (!has && !loading) homeMode = false;
            bool showStart = (st.Sources.Count == 0 && !loading) || (homeMode && has);
            SuspendLayout();
            start.SetResume(homeMode && has ? FilesLabel() : null);
            start.Visible = showStart;
            if (showStart) start.BringToFront(); else grid.BringToFront();
            split.Panel2Collapsed = showStart;
            filterBar.Visible = viewKind == ViewKind.Logs && !(homeMode && has);
            bMerge.Enabled = bReload.Enabled = has;
            bHome.Checked = viewKind == ViewKind.Logs && showStart;
            ResumeLayout(true);
            content.PerformLayout();
            content.Invalidate(true);
        }

        string FilesLabel()
        {
            int n = st.Sources.Count;
            if (n == 0) return null;
            return n == 1 ? st.Sources[0].Name : n.ToString(CultureInfo.InvariantCulture) + " merged files";
        }

        static string TitlePrefix
        {
            get { return Program.IsAdmin ? "Administrator: " : ""; }
        }

        string[] OpenPaths()
        {
            List<string> p = new List<string>();
            foreach (LogSource src in st.Sources) p.Add(src.Path);
            return p.ToArray();
        }

        void Elevate(string[] paths)
        {
            if (Program.IsAdmin) { Note("LogVice already runs as administrator."); return; }
            if (Program.RestartElevated(this, paths)) Close();
        }

        void ToggleHome()
        {
            if (viewKind != ViewKind.Logs)
            {
                ShowView(ViewKind.Logs);
                if (st.Sources.Count > 0 && !loading) { homeMode = true; ShowStart(); }
                return;
            }
            if (homeMode) { LeaveHome(); return; }
            if (st.Sources.Count == 0 || loading) return;
            homeMode = true;
            ShowStart();
        }

        void LeaveHome()
        {
            if (!homeMode) return;
            homeMode = false;
            ShowStart();
            if (grid.Visible) grid.Focus();
        }

        bool AutoMerge
        {
            get { return Settings.GetBool("AutoMerge", Settings.GetBool("History", true)); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Konami[konamiPos])
            {
                konamiPos++;
                if (konamiPos == Konami.Length)
                {
                    konamiPos = 0;
                    ShowEgg();
                    return true;
                }
            }
            else konamiPos = keyData == Konami[0] ? 1 : 0;
            if (keyData >= Keys.A && keyData <= Keys.Z && !(FocusedLeaf(this) is TextBoxBase))
            {
                typed = (typed + (char)('a' + (keyData - Keys.A)));
                if (typed.Length > 8) typed = typed.Substring(typed.Length - 8);
                if (typed.EndsWith("vice", StringComparison.Ordinal)) { typed = ""; ToggleVice(); return true; }
            }
            switch (keyData)
            {
                case Keys.Alt | Keys.Home: ToggleHome(); return true;
                case Keys.Escape:
                    if (viewKind == ViewKind.Logs && homeMode) { LeaveHome(); return true; }
                    break;
                case Keys.Control | Keys.G: GoToLine(); return true;
                case Keys.F7: HlStep(1); return true;
                case Keys.Shift | Keys.F7: HlStep(-1); return true;
                case Keys.Control | Keys.O: PickFiles(false, null); return true;
                case Keys.Control | Keys.B: if (viewKind == ViewKind.Browse) ShowView(ViewKind.Logs); else OpenBrowse(null); return true;
                case Keys.Control | Keys.Oemcomma: if (viewKind == ViewKind.Settings) ShowView(ViewKind.Logs); else OpenSettings(0); return true;
                case Keys.Control | Keys.Shift | Keys.O: if (st.Sources.Count > 0) PickFiles(true, null); return true;
                case Keys.Control | Keys.F: if (viewKind != ViewKind.Logs) ShowView(ViewKind.Logs); tFind.Box.Focus(); tFind.Box.SelectAll(); return true;
                case Keys.F3: FindStep(1); return true;
                case Keys.Shift | Keys.F3: FindStep(-1); return true;
                case Keys.F8: ErrStep(1); return true;
                case Keys.Shift | Keys.F8: ErrStep(-1); return true;
                case Keys.Control | Keys.L: ShowLookup(null); return true;
                case Keys.F5: Reload(); return true;
                case Keys.Control | Keys.C:
                    if (grid.Focused) { CopySelection(false); return true; }
                    break;
                case Keys.Control | Keys.A:
                    if (grid.Focused) { grid.SelectAll(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Note(string text)
        {
            status.MidText = text;
            status.Invalidate();
            noteTimer.Stop();
            noteTimer.Start();
        }

        void UpdateFound(UpdateInfo r)
        {
            settingsView.ShowUpdate(r);
            if (r == null || !r.Newer) return;
            bUpdate.Text = "Update " + r.Latest;
            tips.SetToolTip(bUpdate, "LogVice " + r.Latest + " is available. Open About to download it.");
            bUpdate.Visible = true;
            cmdBar.PerformLayout();
            Note("LogVice " + r.Latest + " is available.");
        }

        void SafeInvoke(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        // ---------- easter egg ----------

        void ShowEgg()
        {
            try
            {
                MainForm f = new MainForm(new string[0], true);
                f.StartPosition = FormStartPosition.Manual;
                f.Size = new Size(Math.Max(MinimumSize.Width, Width - Dpi.S(160)), Math.Max(MinimumSize.Height, Height - Dpi.S(120)));
                f.Location = new Point(Left + Dpi.S(80), Top + Dpi.S(60));
                f.Show();
            }
            catch (Exception) { }
        }

        void StartEgg()
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "LogVice-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(dir);
                eggPath = Path.Combine(dir, "LogVice-credits.log");
                eggStart = DateTime.Now;
                File.WriteAllText(eggPath, EggEntry(0), new UTF8Encoding(false));
                eggLine = 1;
            }
            catch (Exception)
            {
                eggPath = null;
                return;
            }
            bLive.Checked = true;
            tailTimer.Interval = 250;
            OpenFiles(new string[] { eggPath }, false, false);
            eggTimer = new Timer();
            eggTimer.Interval = 700;
            eggTimer.Tick += delegate
            {
                if (loading) return;
                if (eggLine >= EggText.Length)
                {
                    eggTimer.Stop();
                    tailTimer.Interval = 1000;
                    Note("You found the Easter egg. Thanks for using LogVice.");
                    return;
                }
                try { File.AppendAllText(eggPath, EggEntry(eggLine), new UTF8Encoding(false)); } catch (Exception) { }
                eggLine++;
            };
            eggTimer.Start();
        }

        string EggEntry(int i)
        {
            DateTime t = eggStart.AddMilliseconds(i * 700);
            int bias = -(int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(t).TotalMinutes);
            string time = t.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + (bias >= 0 ? "+" : "") + bias.ToString(CultureInfo.InvariantCulture);
            string date = t.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
            string[] e = EggText[i];
            return "<![LOG[" + e[2] + "]LOG]!><time=\"" + time + "\" date=\"" + date + "\" component=\"" + e[1] + "\" context=\"\" type=\"" + e[0] +
                "\" thread=\"4242\" file=\"credits.cpp:" + (100 + i * 7).ToString(CultureInfo.InvariantCulture) + "\">\r\n";
        }

        // ---------- drag and drop ----------

        void HookDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += OnDragEnterAny;
            c.DragOver += OnDragOverAny;
            c.DragLeave += OnDragLeaveAny;
            c.DragDrop += OnDragDropAny;
        }

        static bool HasFiles(DragEventArgs e)
        {
            return e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        void OnDragEnterAny(object sender, DragEventArgs e)
        {
            if (!HasFiles(e)) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Copy;
            if (!drop.Visible)
            {
                drop.TwoZones = st.Sources.Count > 0 && !loading;
                drop.HotZone = -1;
                drop.Visible = true;
                drop.BringToFront();
            }
        }

        void OnDragOverAny(object sender, DragEventArgs e)
        {
            if (!HasFiles(e)) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Copy;
            if (drop.Visible)
            {
                Point p = drop.PointToClient(new Point(e.X, e.Y));
                drop.HotZone = drop.ClientRectangle.Contains(p) ? drop.ZoneAt(p) : -1;
            }
        }

        void OnDragLeaveAny(object sender, EventArgs e)
        {
            if (!drop.Visible) return;
            Point p = drop.PointToClient(MousePosition);
            if (sender != drop && drop.ClientRectangle.Contains(p)) return;
            Point f = PointToClient(MousePosition);
            if (sender == drop && ClientRectangle.Contains(f) && !drop.ClientRectangle.Contains(p)) return;
            drop.Visible = false;
        }

        void OnDragDropAny(object sender, DragEventArgs e)
        {
            int zone = drop.Visible ? drop.HotZone : -1;
            drop.Visible = false;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            bool merge = st.Sources.Count > 0 && !loading && ((e.KeyState & 8) != 0 || (drop.TwoZones && zone == 1));
            OpenFiles(files, merge, false);
        }

        // ---------- loading ----------

        void PickFiles(bool merge, string folder)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Multiselect = true;
                d.Title = merge ? "Merge in log files" : "Open log files";
                d.Filter = "Log files (*.log;*.lo_;*.etl;*.evtx)|*.log;*.lo_;*.etl;*.evtx|Text logs (*.log;*.lo_;*.txt)|*.log;*.lo_;*.txt|ETW traces (*.etl)|*.etl|Event logs (*.evtx)|*.evtx|All files (*.*)|*.*";
                string dir = folder ?? StartDir();
                if (dir != null) d.InitialDirectory = dir;
                if (d.ShowDialog(this) == DialogResult.OK) OpenFiles(d.FileNames, merge && st.Sources.Count > 0, false);
            }
        }

        string StartDir()
        {
            try { if (lastDir != null && Directory.Exists(lastDir)) return lastDir; } catch (Exception) { }
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string data = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] candidates = { Path.Combine(win, @"CCM\Logs"), Path.Combine(data, @"Microsoft\IntuneManagementExtension\Logs"), Path.Combine(win, "Logs") };
            foreach (string c in candidates)
            {
                try { if (Directory.Exists(c)) return c; } catch (Exception) { }
            }
            return null;
        }

        void Reload()
        {
            if (st.Sources.Count == 0 || loading) return;
            string[] paths = new string[st.Sources.Count];
            for (int k = 0; k < paths.Length; k++) paths[k] = st.Sources[k].Path;
            OpenFiles(paths, false, true);
        }

        static void CloseSources(Store s)
        {
            if (s == null) return;
            foreach (LogSource src in s.Sources.ToArray()) src.Dispose();
        }

        void OpenFiles(string[] paths, bool merge, bool keep)
        {
            List<string> found = new List<string>();
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (EventLogs.IsChannel(p)) { found.Add(p); continue; }
                try { if (File.Exists(p)) found.Add(Path.GetFullPath(p)); } catch (Exception) { }
            }
            if (found.Count == 0)
            {
                MessageBox.Show(this, "None of those logs could be found.", "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (viewKind != ViewKind.Logs) ShowView(ViewKind.Logs);
            homeMode = false;
            if (loading)
            {
                if (merge) return;
                if (loadTok != null) loadTok.State = 2;
            }
            tailTimer.Stop();
            Store target;
            if (merge && st.Sources.Count > 0) target = st;
            else
            {
                CloseSources(st);
                target = new Store();
                if (!keep)
                {
                    compName = null;
                    threadF = -1;
                    fromK = long.MinValue;
                    toK = long.MaxValue;
                    suppress = true;
                    tThread.Text = "";
                    tThread.Invalid = false;
                    suppress = false;
                    bFrom.Visible = bTo.Visible = false;
                }
                selEntry = -1;
            }
            reloading = keep;
            st = target;
            grid.Data = target;
            foreach (string p in found)
                if (!EventLogs.IsChannel(p)) { try { lastDir = Path.GetDirectoryName(p); } catch (Exception) { } break; }
            viewGen++;
            findGen++;
            filtering = false;
            view = new int[16];
            viewCount = 0;
            hitMask = null;
            hitCount = 0;
            grid.Hits = null;
            if (!merge) { marks = null; grid.Marks = null; markCounts = new int[markRules.Length]; markAny = 0; }
            grid.ResetSelection();
            grid.SetView(view, 0);
            LoadToken tok = new LoadToken();
            loadTok = tok;
            loading = true;
            long total = 0;
            foreach (string p in found)
            {
                if (EventLogs.IsChannel(p)) continue;
                try { total += new FileInfo(p).Length; } catch (Exception) { }
            }
            grid.Busy = true;
            grid.BusyTitle = "Reading " + EventLogs.DisplayName(found[0]);
            grid.BusyText = "0 of " + Fmt.Size(total);
            grid.BusyProgress = 0;
            SetLoadingUi(true);
            string[] ps = found.ToArray();
            chosenPaths = ps;
            partsAdded = 0;
            bool expand = AutoMerge;
            List<string> already = new List<string>();
            if (merge) foreach (LogSource src0 in target.Sources) already.Add(src0.Path);
            Stopwatch sw = Stopwatch.StartNew();
            ShowDetails();
            Task.Factory.StartNew(delegate
            {
                string[] list = ps;
                long size = total;
                foreach (string p in ps) if (EventLogs.IsChannel(p)) size += EventLogs.Size(p);
                if (expand)
                {
                    int added;
                    List<string> all = LogLocations.Spanned(ps, out added);
                    all.RemoveAll(delegate (string x) { return already.Exists(delegate (string y) { return string.Equals(x, y, StringComparison.OrdinalIgnoreCase); }); });
                    if (all.Count > ps.Length)
                    {
                        list = all.ToArray();
                        size = 0;
                        foreach (string p in list) { if (EventLogs.IsChannel(p)) { size += EventLogs.Size(p); continue; } try { size += new FileInfo(p).Length; } catch (Exception) { } }
                        int extra = list.Length - ps.Length;
                        SafeInvoke(delegate { if (tok == loadTok) partsAdded = extra; });
                    }
                }
                LoadAll(target, list, tok, size);
            }, TaskCreationOptions.LongRunning)
                .ContinueWith(delegate (Task t)
                {
                    string err = t.Exception != null ? t.Exception.GetBaseException().Message : null;
                    TimeSpan el = sw.Elapsed;
                    SafeInvoke(delegate { LoadDone(target, tok, el, err); });
                });
        }

        void LoadAll(Store s, string[] paths, LoadToken tok, long total)
        {
            long done = 0, lastTick = -1000;
            Stopwatch clock = Stopwatch.StartNew();
            foreach (string p in paths)
            {
                if (tok.State != 0) break;
                LogSource src = new LogSource(p, s.Sources.Count);
                s.Sources.Add(src);
                long baseDone = done;
                string name = src.Name;
                try
                {
                    Loader.ReadAll(s, src, tok, delegate (long pos)
                    {
                        long now = clock.ElapsedMilliseconds;
                        if (now - lastTick < 100) return;
                        lastTick = now;
                        long b = baseDone + pos;
                        int cnt = s.Count;
                        SafeInvoke(delegate { Progress(tok, name, b, total, cnt); });
                    });
                }
                catch (UnauthorizedAccessException ex)
                {
                    src.Error = ex.Message;
                    src.Denied = true;
                    src.Dispose();
                }
                catch (OutOfMemoryException)
                {
                    src.Partial = true;
                    tok.Oom = true;
                }
                catch (Exception ex)
                {
                    src.Error = ex.Message;
                    src.Dispose();
                }
                done += Math.Max(src.Size, src.Pos);
                if (tok.Oom) break;
            }
        }

        void Progress(LoadToken tok, string name, long b, long total, int cnt)
        {
            if (tok != loadTok) return;
            float f = total > 0 ? (float)Math.Min(1.0, (double)b / total) : 0f;
            grid.BusyTitle = "Reading " + name;
            grid.BusyText = string.Format("{0} of {1}  ·  {2:N0} entries", Fmt.Size(b), Fmt.Size(total), cnt);
            grid.BusyProgress = f;
            grid.Invalidate();
            status.Progress = f;
            status.MidText = grid.BusyText;
            status.Invalidate();
        }

        void LoadDone(Store s, LoadToken tok, TimeSpan el, string err)
        {
            if (tok.State == 2 || s != st)
            {
                if (s != st) CloseSources(s);
                return;
            }
            loading = false;
            loadTok = null;
            s.BuildOrder();
            StringBuilder errs = new StringBuilder();
            long bytes = 0;
            List<string> ok = new List<string>();
            foreach (LogSource src in s.Sources)
            {
                if (src.Error != null) errs.Append(src.Name).Append(": ").Append(src.Error).Append("\r\n");
                else ok.Add(src.Path);
                bytes += src.Pos;
            }
            loadSummary = Fmt.Size(bytes) + " read in " + el.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" +
                (tok.State == 1 ? ", stopped early" : "") + (tok.Oom ? ", out of memory" : "");
            if (!reloading && !eggMode && ok.Count > 0)
            {
                bool single = s.Sources.Count == ok.Count && partsAdded > 0 && chosenPaths != null && chosenPaths.Length + partsAdded == ok.Count;
                Settings.AddRecent(single ? chosenPaths : ok.ToArray());
                start.SetRecent(Settings.Recent());
            }
            StringBuilder notes = new StringBuilder();
            foreach (LogSource src in s.Sources)
            {
                if (src.Note == null) continue;
                if (src.Entries == 0 && src.Error == null) errs.Append(src.Note).Append("\r\n");
                else notes.Append(src.Note).Append(". ");
            }
            reloading = false;
            grid.Busy = false;
            grid.MultiFile = s.Sources.Count > 1;
            SetLoadingUi(false);
            RefreshFilesUi();
            RefreshCounts();
            if (compName != null && s.CompIndex(compName) < 0) compName = null;
            UpdateCompButton();
            RebuildView(true);
            RebuildMarks();
            if (bLive.Checked) tailTimer.Start();
            if (notes.Length > 0) Note(notes.ToString().Trim());
            else if (partsAdded > 0 && !reloading) Note(string.Format("Auto-merge added {0} more part{1} of the same log.", partsAdded, partsAdded == 1 ? "" : "s"));
            partsAdded = 0;
            if (tok.Oom)
                MessageBox.Show(this, "Windows ran out of memory for this process while reading. Showing what was read.\r\n\r\nClose other large apps, or open fewer files at once.", "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            List<string> denied = new List<string>();
            foreach (LogSource src in s.Sources) if (src.Denied) denied.Add(src.Path);
            if (denied.Count > 0 && !Program.IsAdmin && !eggMode)
            {
                StringBuilder names = new StringBuilder();
                for (int k = 0; k < denied.Count && k < 8; k++) names.Append("  ").Append(EventLogs.DisplayName(denied[k])).Append("\r\n");
                if (denied.Count > 8) names.Append("  and ").Append(denied.Count - 8).Append(" more\r\n");
                if (MessageBox.Show(this, "Windows needs administrator rights to read:\r\n\r\n" + names + "\r\nRestart LogVice as administrator and open the logs again?", "Administrator rights needed", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    List<string> all = new List<string>();
                    foreach (LogSource src in s.Sources) all.Add(src.Path);
                    Elevate(all.ToArray());
                }
                return;
            }
            if (errs.Length > 0)
                MessageBox.Show(this, "Some logs could not be read:\r\n\r\n" + errs, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (err != null)
                MessageBox.Show(this, "Reading stopped: " + err, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void SetLoadingUi(bool on)
        {
            bStop.Visible = on;
            status.Progress = on ? 0f : -1f;
            cmdBar.PerformLayout();
            ShowStart();
            UpdateStatus();
        }

        // ---------- live tail ----------

        void TailTick()
        {
            if (loading || st.Sources.Count == 0) return;
            int before = st.Count;
            bool rolled = false;
            foreach (LogSource src in st.Sources)
            {
                if (src.EventChannel)
                {
                    try { EventLogs.ReadMore(st, src); } catch (Exception) { }
                    continue;
                }
                if (src.Fs == null || src.Enc == null) continue;
                try
                {
                    long len = src.Fs.Length;
                    if (len < src.Pos) { rolled = true; break; }
                    FileInfo fi = new FileInfo(src.Path);
                    if (fi.Exists && fi.Length < src.Pos) { rolled = true; break; }
                    if (len > src.Pos) Loader.ReadMore(st, src);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (rolled)
            {
                Note("A log rolled over. Reloading.");
                Reload();
                return;
            }
            if (st.Count > before) OnAppended(before);
        }

        void OnAppended(int from)
        {
            st.AppendOrder(from);
            MarkRange(from, st.Count);
            bLive.Pulse();
            RefreshCounts();
            RefreshFilesUi();
            if (filtering) { UpdateStatus(); return; }
            int old = viewCount;
            int cur = CurPos();
            bool followSel = cur >= 0 && cur == old - 1;
            bool atEnd = old == 0 || grid.LastRowVisible();
            CatchUp(from, true);
            if (viewCount != old)
            {
                grid.SetView(view, viewCount);
                grid.Hits = hitMask;
                if (followSel || old == 0) SelectPos(viewCount - 1, 2);
                else if (atEnd) grid.ScrollToEnd();
            }
            UpdateStatus();
            UpdateFindCount();
        }

        // ---------- filtering ----------

        sealed class Spec
        {
            public int Types;
            public int Comp;
            public int Thread;
            public long From;
            public long To;
            public Regex Rx;
            public bool Exclude;
            public bool Any;
            public int Hl;
            public byte[] Marks;
        }

        Spec MakeSpec()
        {
            Spec f = new Spec();
            f.Types = types;
            f.Thread = threadF;
            f.From = fromK;
            f.To = toK;
            f.Rx = filterRx;
            f.Exclude = bExcl.Checked;
            f.Comp = -1;
            if (compName != null)
            {
                int id = st.CompIndex(compName);
                f.Comp = id >= 0 ? id : -2;
            }
            f.Hl = markRules.Length > 0 ? hlFilter : -1;
            f.Marks = marks;
            f.Any = f.Types != 7 || f.Comp != -1 || f.Thread >= 0 || f.From != long.MinValue || f.To != long.MaxValue || f.Rx != null || f.Hl >= 0;
            return f;
        }

        static bool Pass(Spec f, int i, byte[] sev, int[] comp, int[] thr, long[] key, string[] msg)
        {
            if ((f.Types & (1 << (sev[i] - 1))) == 0) return false;
            if (f.Comp != -1 && comp[i] != f.Comp) return false;
            if (f.Thread >= 0 && thr[i] != f.Thread) return false;
            long k = key[i];
            if (k < f.From || k > f.To) return false;
            if (f.Hl >= 0)
            {
                byte[] m = f.Marks;
                int hk = m != null && i < m.Length ? m[i] : 0;
                if (f.Hl == 0 ? hk == 0 : hk != f.Hl) return false;
            }
            if (f.Rx != null && f.Rx.IsMatch(msg[i]) == f.Exclude) return false;
            return true;
        }

        void RebuildView(bool goEnd)
        {
            if (loading) return;
            int gen = ++viewGen;
            Spec f = MakeSpec();
            Store s = st;
            int n = s.Count;
            int[] order = s.Order;
            if (!f.Any)
            {
                int[] all = new int[Math.Max(n, 16)];
                Array.Copy(order, all, n);
                ApplyView(gen, all, n, n, goEnd);
                return;
            }
            filtering = true;
            UpdateStatus();
            byte[] sev = s.Sev;
            int[] comp = s.Comp, thr = s.Thread;
            long[] key = s.Key;
            string[] msg = s.Msg;
            Task.Factory.StartNew(delegate
            {
                int[] v = new int[Math.Max(n, 16)];
                int m = 0;
                for (int o = 0; o < n; o++)
                {
                    if ((o & 0xFFFF) == 0 && gen != viewGen) return;
                    int i = order[o];
                    if (Pass(f, i, sev, comp, thr, key, msg)) v[m++] = i;
                }
                int count = m;
                SafeInvoke(delegate { ApplyView(gen, v, count, n, goEnd); });
            });
        }

        void ApplyView(int gen, int[] v, int m, int n0, bool goEnd)
        {
            if (gen != viewGen || loading) return;
            filtering = false;
            view = v;
            viewCount = m;
            CatchUp(n0, false);
            int target;
            if (goEnd) target = viewCount - 1;
            else if (selEntry >= 0 && selEntry < st.Count) target = NearestPos(selEntry);
            else target = viewCount > 0 ? 0 : -1;
            suppress = true;
            grid.ResetSelection();
            grid.SetView(view, viewCount);
            suppress = false;
            grid.EmptyTitle = st.Count == 0 ? "No log entries found" : "No entries match the current filters";
            grid.EmptyText = st.Count == 0 ? "The file is empty or is not a text log." : "Loosen a filter, or select Clear filters to show every entry again.";
            if (target >= 0) SelectPos(target, goEnd ? 2 : 1);
            else ShowDetails();
            SyncFilterUi();
            RebuildFind();
            UpdateStatus();
        }

        void CatchUp(int from, bool live)
        {
            int n = st.Count;
            if (from >= n) return;
            Spec f = MakeSpec();
            for (int o = from; o < n; o++)
            {
                int i = st.Order[o];
                if (f.Any && !Pass(f, i, st.Sev, st.Comp, st.Thread, st.Key, st.Msg)) continue;
                if (viewCount == view.Length) Array.Resize(ref view, Math.Max(16, view.Length * 2));
                view[viewCount] = i;
                if (live) MarkHit(viewCount);
                viewCount++;
            }
        }

        void MarkHit(int p)
        {
            if (findRx == null || hitMask == null) return;
            int i = view[p];
            if (!findRx.IsMatch(st.Msg[i])) return;
            if (i >= hitMask.Length) Array.Resize(ref hitMask, Math.Max(st.Cap, i + 1));
            hitMask[i] = 1;
            if (hitCount == hitPos.Length) Array.Resize(ref hitPos, Math.Max(16, hitPos.Length * 2));
            hitPos[hitCount++] = p;
        }

        int NearestPos(int e)
        {
            int[] rank = st.Rank;
            int r = rank[e], lo = 0, hi = viewCount;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (rank[view[mid]] < r) lo = mid + 1; else hi = mid;
            }
            return Math.Min(lo, viewCount - 1);
        }

        Regex MakeRx(string text, out bool bad)
        {
            bad = false;
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                return new Regex(bRegex.Checked ? text : Regex.Escape(text), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }
            catch (ArgumentException)
            {
                bad = true;
                return null;
            }
        }

        void ApplyFilterText()
        {
            bool bad;
            Regex rx = MakeRx(tFilter.Text, out bad);
            tFilter.Invalid = bad;
            tFilter.Badge = bad ? "Invalid pattern" : "";
            if (bad) return;
            filterRx = rx;
            RebuildView(false);
        }

        void ApplyThreadText()
        {
            string s = tThread.Text.Trim();
            int v = -1;
            bool bad = false;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) bad = true;
            }
            else if (s.Length > 0 && !int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out v)) bad = true;
            if (s.Length == 0) v = -1;
            tThread.Invalid = bad;
            if (bad) return;
            threadF = v;
            RebuildView(false);
        }

        void ClearFilters()
        {
            suppress = true;
            try
            {
                types = 7;
                chipErr.Checked = chipWarn.Checked = chipInfo.Checked = true;
                compName = null;
                threadF = -1;
                tThread.Text = "";
                tThread.Invalid = false;
                fromK = long.MinValue;
                toK = long.MaxValue;
                bFrom.Visible = bTo.Visible = false;
                tFilter.Text = "";
                tFilter.Invalid = false;
                tFilter.Badge = "";
                filterRx = null;
                bExcl.Checked = false;
                hlFilter = -1;
            }
            finally { suppress = false; }
            UpdateCompButton();
            UpdateHlButton();
            filterBar.PerformLayout();
            RebuildView(false);
        }

        void SyncFilterUi()
        {
            UpdateCompButton();
        }

        void UpdateCompButton()
        {
            string t = compName ?? "All components";
            if (t.Length > 28) t = t.Substring(0, 27) + "…";
            bComp.Text = t;
            bComp.Checked = compName != null;
            bComp.Width = bComp.GetPreferredSize(Size.Empty).Width;
            filterBar.PerformLayout();
        }

        void ShowCompMenu()
        {
            compMenu.Items.Clear();
            ToolStripMenuItem all = Item("All components", "", true, delegate { compName = null; UpdateCompButton(); RebuildView(false); });
            all.Checked = compName == null;
            compMenu.ShowCheckMargin = true;
            compMenu.Items.Add(all);
            int n = st.CompNames.Count;
            if (n > 0) compMenu.Items.Add(new ToolStripSeparator());
            int[] ids = new int[n];
            for (int k = 0; k < n; k++) ids[k] = k;
            int[] cnt = st.CompCount.ToArray();
            Array.Sort(ids, delegate (int a, int b) { int c = cnt[b].CompareTo(cnt[a]); return c != 0 ? c : a.CompareTo(b); });
            foreach (int id in ids)
            {
                string name = st.CompNames[id];
                ToolStripMenuItem mi = Item(name, cnt[id].ToString("N0"), true, delegate { compName = name; UpdateCompButton(); RebuildView(false); });
                mi.Checked = name == compName;
                compMenu.Items.Add(mi);
            }
            compMenu.Show(bComp, new Point(0, bComp.Height + Dpi.S(2)));
        }

        void ShowRecentMenu()
        {
            recentMenu.Items.Clear();
            List<string[]> list = Settings.Recent();
            if (list.Count == 0) recentMenu.Items.Add(Item("No recent files", "", false, null));
            foreach (string[] paths in list)
            {
                string detail;
                string label = Settings.RecentLabel(paths, out detail);
                bool exists = true;
                foreach (string p in paths)
                {
                    if (!EventLogs.Available(p)) exists = false;
                }
                string[] pp = paths;
                string dir = detail.Length > 48 ? "…" + detail.Substring(detail.Length - 47) : detail;
                ToolStripMenuItem mi = Item(label, dir, exists, delegate { OpenFiles(pp, false, false); });
                mi.ToolTipText = string.Join("\r\n", paths);
                recentMenu.Items.Add(mi);
            }
            if (list.Count > 0)
            {
                recentMenu.Items.Add(new ToolStripSeparator());
                recentMenu.Items.Add(Item("Clear recent files", "", true, delegate { Settings.ClearRecent(); start.SetRecent(Settings.Recent()); }));
            }
            recentMenu.Show(bRecent, new Point(0, bRecent.Height + Dpi.S(2)));
        }

        void ShowRowMenu(Point at)
        {
            int p = CurPos();
            if (p < 0) return;
            int i = view[p];
            rowMenu.Items.Clear();
            int n = grid.SelHi - grid.SelLo + 1;
            rowMenu.Items.Add(Item(n > 1 ? string.Format("Copy {0:N0} rows", n) : "Copy row", "Ctrl+C", true, delegate { CopySelection(false); }));
            rowMenu.Items.Add(Item("Copy message text", "", true, delegate { CopySelection(true); }));
            rowMenu.Items.Add(new ToolStripSeparator());
            int th = st.Thread[i];
            int ci = st.Comp[i];
            rowMenu.Items.Add(Item(th >= 0 ? "Show only thread " + th.ToString(CultureInfo.InvariantCulture) : "Show only this thread", "", th >= 0, delegate { OnlyThread(); }));
            rowMenu.Items.Add(Item(ci >= 0 ? "Show only " + st.CompNames[ci] : "Show only this component", "", ci >= 0, delegate { OnlyComp(); }));
            bool hasTime = st.Time[i] != Store.NoTime;
            rowMenu.Items.Add(Item("Hide rows before this one", "", hasTime, delegate { HideBefore(true); }));
            rowMenu.Items.Add(Item("Hide rows after this one", "", hasTime, delegate { HideBefore(false); }));
            List<uint> found = Codes.Find(st.Msg[i]);
            if (found.Count > 0)
            {
                rowMenu.Items.Add(new ToolStripSeparator());
                uint u = found[0];
                string code = Codes.Describe(u).Title;
                rowMenu.Items.Add(Item("Look up " + code, "", true, delegate { ShowLookup(code); }));
            }
            rowMenu.Items.Add(new ToolStripSeparator());
            rowMenu.Items.Add(Item(ci >= 0 ? "Highlight " + st.CompNames[ci] + "\u2026" : "Add highlight rule\u2026", "", true, delegate { AddHighlightFromRow(); }));
            rowMenu.Items.Add(Item("Clear filters", "", true, delegate { ClearFilters(); }));
            rowMenu.Show(grid, at);
        }

        void OnlyThread()
        {
            int p = CurPos();
            if (p < 0) return;
            int th = st.Thread[view[p]];
            if (th < 0) return;
            threadF = th;
            suppress = true;
            tThread.Text = th.ToString(CultureInfo.InvariantCulture);
            tThread.Invalid = false;
            suppress = false;
            RebuildView(false);
        }

        void OnlyComp()
        {
            int p = CurPos();
            if (p < 0) return;
            int c = st.Comp[view[p]];
            if (c < 0) return;
            compName = st.CompNames[c];
            UpdateCompButton();
            RebuildView(false);
        }

        void HideBefore(bool earlier)
        {
            int p = CurPos();
            if (p < 0) return;
            int i = view[p];
            if (st.Time[i] == Store.NoTime) return;
            string label = Fmt.Time(st.Time[i]).Substring(11);
            if (earlier)
            {
                fromK = st.Key[i];
                bFrom.Text = "From " + label;
                bFrom.Checked = false;
                bFrom.Visible = true;
            }
            else
            {
                toK = st.Key[i];
                bTo.Text = "Until " + label;
                bTo.Visible = true;
            }
            filterBar.PerformLayout();
            RebuildView(false);
        }

        // ---------- find ----------

        void OnFindKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (findTimer.Enabled) { findTimer.Stop(); ApplyFindText(); }
                FindStep(e.Shift ? -1 : 1);
            }
            else if (e.KeyCode == Keys.Escape && tFind.Text.Length > 0)
            {
                e.SuppressKeyPress = true;
                tFind.Text = "";
                findTimer.Stop();
                ApplyFindText();
            }
        }

        void ApplyFindText()
        {
            bool bad;
            Regex rx = MakeRx(tFind.Text, out bad);
            tFind.Invalid = bad;
            if (bad) { tFind.Badge = "Invalid"; return; }
            findRx = rx;
            jumpOnFind = rx != null;
            RebuildFind();
        }

        void RebuildFind()
        {
            int gen = ++findGen;
            Regex rx = findRx;
            grid.Highlight = rx;
            if (rx == null)
            {
                hitMask = null;
                hitCount = 0;
                grid.Hits = null;
                grid.Invalidate();
                UpdateFindCount();
                return;
            }
            int[] v = view;
            int m = viewCount;
            string[] msg = st.Msg;
            int cap = Math.Max(st.Cap, 1);
            tFind.Badge = "Searching…";
            Task.Factory.StartNew(delegate
            {
                byte[] mask = new byte[cap];
                int[] pos = new int[1024];
                int k = 0;
                for (int p = 0; p < m; p++)
                {
                    if ((p & 0x3FFF) == 0 && gen != findGen) return;
                    int i = v[p];
                    if (rx.IsMatch(msg[i]))
                    {
                        mask[i] = 1;
                        if (k == pos.Length) Array.Resize(ref pos, pos.Length * 2);
                        pos[k++] = p;
                    }
                }
                int found = k;
                int[] hits = pos;
                SafeInvoke(delegate { ApplyFind(gen, mask, hits, found, m); });
            });
        }

        void ApplyFind(int gen, byte[] mask, int[] pos, int k, int scanned)
        {
            if (gen != findGen || loading) return;
            hitMask = mask;
            hitPos = pos;
            hitCount = k;
            for (int p = scanned; p < viewCount; p++) MarkHit(p);
            grid.Hits = hitMask;
            grid.Invalidate();
            if (jumpOnFind)
            {
                jumpOnFind = false;
                int cur = CurPos();
                bool onHit = cur >= 0 && hitMask != null && view[cur] < hitMask.Length && hitMask[view[cur]] != 0;
                if (hitCount > 0 && !onHit) FindStep(1);
            }
            UpdateFindCount();
        }

        void FindStep(int dir)
        {
            if (findRx == null) { tFind.Box.Focus(); return; }
            if (hitCount == 0) { Note("No matches in the current view."); return; }
            int cur = CurPos(), lo = 0, hi = hitCount;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (hitPos[mid] <= cur) lo = mid + 1; else hi = mid;
            }
            int k;
            if (dir > 0) k = lo < hitCount ? lo : 0;
            else
            {
                k = lo - 1;
                if (k >= 0 && hitPos[k] == cur) k--;
                if (k < 0) k = hitCount - 1;
            }
            SelectPos(hitPos[k], 1);
        }

        void ErrStep(int dir)
        {
            int n = viewCount;
            if (n == 0) return;
            int cur = CurPos();
            int start0 = cur < 0 ? (dir > 0 ? -1 : n) : cur;
            for (int s = 1; s <= n; s++)
            {
                int p = (int)((((long)start0 + (long)dir * s) % n + n) % n);
                if (st.Sev[view[p]] == 3) { SelectPos(p, 1); return; }
            }
            Note("No errors in the current view.");
        }

        void UpdateFindCount()
        {
            if (findRx == null) { tFind.Badge = ""; return; }
            if (hitCount == 0) { tFind.Badge = "No matches"; return; }
            int cur = CurPos(), lo = 0, hi = hitCount;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (hitPos[mid] < cur) lo = mid + 1; else hi = mid;
            }
            tFind.Badge = lo < hitCount && hitPos[lo] == cur
                ? string.Format("{0:N0} / {1:N0}", lo + 1, hitCount)
                : string.Format("{0:N0} matches", hitCount);
        }

        // ---------- selection and details ----------

        int CurPos()
        {
            int c = grid.Cur;
            return c >= 0 && c < viewCount ? c : -1;
        }

        void SelectPos(int p, int align)
        {
            if (viewCount == 0) return;
            grid.Select(p, false, align);
        }

        void OnSelChanged()
        {
            int p = CurPos();
            if (p >= 0) selEntry = view[p];
            ShowDetails();
            UpdateStatus();
            UpdateFindCount();
        }

        void ShowDetails()
        {
            int p = CurPos();
            bool has = p >= 0;
            metaFlow.SuspendLayout();
            foreach (FluentButton b in new FluentButton[] { aThread, aComp, aFrom, aTo, aCopy }) b.Enabled = has;
            if (!has)
            {
                badge.Visible = false;
                foreach (MetaItem m in new MetaItem[] { mTime, mDelta, mLine, mComp, mThread, mSource, mFile, mHl }) m.Set("", "");
                metaFlow.ResumeLayout(true);
                actFlow.Visible = false;
                links = new List<Codes.Span>();
                tMsg.Text = loading ? "" : HelpText;
                tMsg.ForeColor = theme.Text3;
                codes.Visible = false;
                return;
            }
            actFlow.Visible = true;
            int i = view[p];
            byte sv = st.Sev[i];
            long t = st.Time[i];
            badge.Set(sv);
            badge.Visible = true;
            short b0 = st.Bias[i];
            mTime.Set(t != Store.NoTime && b0 != Store.NoBias ? "Time · " + Fmt.Utc(b0) : "Time", t == Store.NoTime ? "—" : Fmt.Time(t));
            int pv = p > 0 ? view[p - 1] : -1;
            mDelta.Set("Since previous row", pv >= 0 && t != Store.NoTime && st.Time[pv] != Store.NoTime ? Fmt.Delta(t - st.Time[pv]) : "");
            int ln = st.Line[i];
            string fmt = st.Sources[st.Src[i]].Format;
            mLine.Set(fmt == "etl" ? "Event" : fmt == "evt" ? "Record" : "Line", ln > 0 ? ln.ToString("N0", CultureInfo.CurrentCulture) : "");
            HighlightRule hr = grid.MarkAt(i);
            mHl.Set("Highlight", hr != null ? hr.Name : "");
            int c = st.Comp[i];
            mComp.Set("Component", c >= 0 ? st.CompNames[c] : "");
            int th = st.Thread[i];
            mThread.Set("Thread", th >= 0 ? string.Format(CultureInfo.InvariantCulture, "{0} (0x{0:X4})", th) : "");
            int cf = st.CFile[i];
            mSource.Set("Source", cf >= 0 ? st.CFNames[cf] : "");
            mFile.Set("Log file", st.Sources.Count > 1 ? st.Sources[st.Src[i]].Name : "");
            metaFlow.ResumeLayout(true);
            aThread.Enabled = th >= 0;
            aComp.Enabled = c >= 0;
            aFrom.Enabled = aTo.Enabled = t != Store.NoTime;
            aCopy.Text = grid.SelHi > grid.SelLo ? string.Format("Copy {0:N0} rows", grid.SelHi - grid.SelLo + 1) : "Copy";
            aCopy.Width = aCopy.GetPreferredSize(Size.Empty).Width;
            string msg = st.Msg[i];
            tMsg.ForeColor = theme.Text;
            tMsg.Text = msg.IndexOf('\n') >= 0 ? msg.Replace("\r\n", "\n").Replace("\n", "\r\n") : msg;
            MarkLinks();
            List<uint> found = Codes.Find(msg);
            codes.SetCodes(found);
            codes.Visible = found.Count > 0;
        }

        void MarkLinks()
        {
            string shown = tMsg.Text;
            links = Codes.Spans(shown);
            tipLink = -2;
            if (links.Count == 0) return;
            if (linkFont == null || linkFont.Name != tMsg.Font.Name || Math.Abs(linkFont.Size - tMsg.Font.Size) > 0.01f)
                linkFont = new Font(tMsg.Font, FontStyle.Underline);
            foreach (Codes.Span sp in links)
            {
                if (sp.Start + sp.Length > shown.Length) continue;
                tMsg.Select(sp.Start, sp.Length);
                tMsg.SelectionColor = theme.Accent;
                tMsg.SelectionFont = linkFont;
            }
            tMsg.Select(0, 0);
        }

        int LinkAt(Point p)
        {
            if (links.Count == 0 || tMsg.TextLength == 0) return -1;
            int idx = tMsg.GetCharIndexFromPosition(p);
            Point cp = tMsg.GetPositionFromCharIndex(idx);
            if (p.X < cp.X && idx > 0)
            {
                Point prev = tMsg.GetPositionFromCharIndex(idx - 1);
                if (prev.Y == cp.Y) { idx--; cp = prev; }
            }
            int cw = Math.Max(1, Draw.Measure("0", tMsg.Font).Width);
            if (p.Y < cp.Y - 1 || p.Y > cp.Y + tMsg.Font.Height + 1 || p.X < cp.X - 1 || p.X > cp.X + cw + 1) return -1;
            for (int k = 0; k < links.Count; k++)
                if (idx >= links[k].Start && idx < links[k].Start + links[k].Length) return k;
            return -1;
        }

        // ---------- highlights ----------

        void RebuildMarks()
        {
            int gen = ++markGen;
            List<HighlightRule> active = new List<HighlightRule>();
            foreach (HighlightRule r in rules) if (r.On && r.Rx != null && active.Count < 250) active.Add(r);
            HighlightRule[] rs = active.ToArray();
            markRules = rs;
            grid.MarkRules = rs;
            if (hlFilter > rs.Length) hlFilter = -1;
            if (rs.Length == 0 || st.Count == 0)
            {
                bool wasFiltered = hlFilter >= 0;
                marks = null;
                grid.Marks = null;
                markCounts = new int[rs.Length];
                markAny = 0;
                if (rs.Length == 0) hlFilter = -1;
                UpdateHlButton();
                grid.Invalidate();
                if (wasFiltered && rs.Length == 0) RebuildView(false);
                else ShowDetails();
                return;
            }
            Store s = st;
            int n = s.Count, cap = Math.Max(s.Cap, n);
            string[] msg = s.Msg;
            int[] comp = s.Comp;
            string[] names = s.CompNames.ToArray();
            Task.Factory.StartNew(delegate
            {
                byte[] m = new byte[cap];
                int[] counts = new int[rs.Length];
                int any = 0;
                for (int i = 0; i < n; i++)
                {
                    if ((i & 0x3FFF) == 0 && gen != markGen) return;
                    int c = comp[i];
                    string cn = c >= 0 && c < names.Length ? names[c] : null;
                    for (int k = 0; k < rs.Length; k++)
                    {
                        if (rs[k].Matches(msg[i], cn)) { m[i] = (byte)(k + 1); counts[k]++; any++; break; }
                    }
                }
                SafeInvoke(delegate
                {
                    if (gen != markGen || s != st) return;
                    marks = m;
                    markCounts = counts;
                    markAny = any;
                    MarkRange(n, st.Count);
                    grid.Marks = marks;
                    UpdateHlButton();
                    if (hlFilter >= 0) RebuildView(false);
                    else { grid.Invalidate(); ShowDetails(); }
                });
            });
        }

        void MarkRange(int from, int to)
        {
            if (marks == null || markRules.Length == 0) return;
            if (marks.Length < to) { Array.Resize(ref marks, Math.Max(st.Cap, to)); grid.Marks = marks; }
            for (int i = from; i < to; i++)
            {
                int c = st.Comp[i];
                string cn = c >= 0 && c < st.CompNames.Count ? st.CompNames[c] : null;
                for (int k = 0; k < markRules.Length; k++)
                {
                    if (markRules[k].Matches(st.Msg[i], cn))
                    {
                        marks[i] = (byte)(k + 1);
                        if (k < markCounts.Length) markCounts[k]++;
                        markAny++;
                        break;
                    }
                }
            }
            if (to > from) UpdateHlButton();
        }

        void UpdateHlButton()
        {
            bool show = markRules.Length > 0;
            string t = hlFilter < 0 ? "Highlights" : hlFilter == 0 ? "Any highlight" : markRules[hlFilter - 1].Name;
            if (t.Length > 24) t = t.Substring(0, 23) + "\u2026";
            bHl.Text = t;
            bHl.Count = show ? markAny.ToString("N0") : "";
            bHl.Checked = hlFilter >= 0;
            bHl.Width = bHl.GetPreferredSize(Size.Empty).Width;
            if (bHl.Visible != show) bHl.Visible = show;
            bHl.Invalidate();
            filterBar.PerformLayout();
        }

        static Bitmap Dot(Color c)
        {
            int s = Dpi.S(16);
            Bitmap b = new Bitmap(s, s);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, s * 0.2f, s * 0.2f, s * 0.6f, s * 0.6f);
            }
            return b;
        }

        void ShowHlMenu()
        {
            hlMenu.Items.Clear();
            hlMenu.ShowCheckMargin = true;
            hlMenu.ShowImageMargin = true;
            ToolStripMenuItem all = Item("All rows", "", true, delegate { SetHlFilter(-1); });
            all.Checked = hlFilter < 0;
            hlMenu.Items.Add(all);
            ToolStripMenuItem any = Item("Rows with any highlight", markAny.ToString("N0"), true, delegate { SetHlFilter(0); });
            any.Checked = hlFilter == 0;
            hlMenu.Items.Add(any);
            if (markRules.Length > 0) hlMenu.Items.Add(new ToolStripSeparator());
            for (int k = 0; k < markRules.Length; k++)
            {
                int idx = k + 1;
                ToolStripMenuItem mi = Item(markRules[k].Name, (k < markCounts.Length ? markCounts[k] : 0).ToString("N0"), true, delegate { SetHlFilter(idx); });
                mi.Image = Dot(markRules[k].Color);
                mi.Checked = hlFilter == idx;
                hlMenu.Items.Add(mi);
            }
            hlMenu.Items.Add(new ToolStripSeparator());
            hlMenu.Items.Add(Item("Edit highlight rules\u2026", "", true, delegate { OpenSettings(1); }));
            hlMenu.Show(bHl, new Point(0, bHl.Height + Dpi.S(2)));
        }

        void SetHlFilter(int v)
        {
            hlFilter = v;
            UpdateHlButton();
            RebuildView(false);
        }

        void HlStep(int dir)
        {
            int n = viewCount;
            if (n == 0) return;
            if (marks == null || markRules.Length == 0) { Note("No highlight rules match. Add rules under Settings > Highlights."); return; }
            int cur = CurPos();
            int start0 = cur < 0 ? (dir > 0 ? -1 : n) : cur;
            for (int k = 1; k <= n; k++)
            {
                int p = (int)((((long)start0 + (long)dir * k) % n + n) % n);
                int i = view[p];
                if (i < marks.Length && marks[i] != 0) { SelectPos(p, 1); return; }
            }
            Note("No highlighted rows in the current view.");
        }

        void AddHighlightFromRow()
        {
            int p = CurPos();
            string seed = tMsg.SelectionLength > 0 && tMsg.SelectionLength < 200 ? tMsg.SelectedText.Trim() : null;
            if (string.IsNullOrEmpty(seed) && p >= 0)
            {
                int c = st.Comp[view[p]];
                if (c >= 0) seed = st.CompNames[c];
            }
            HighlightRule r = new HighlightRule();
            r.Pattern = seed ?? "";
            r.Label = seed ?? "";
            OpenSettings(1);
            settingsView.AddRule(r);
        }

        void GoToLine()
        {
            if (viewKind != ViewKind.Logs || st.Count == 0 || loading) return;
            LeaveHome();
            int p0 = CurPos();
            int src = p0 >= 0 ? st.Src[view[p0]] : 0;
            string name = st.Sources[src].Name;
            using (LineForm f = new LineForm(name, theme))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                int want = f.Value;
                int best = -1, bestLine = int.MaxValue;
                for (int p = 0; p < viewCount; p++)
                {
                    int i = view[p];
                    if (st.Src[i] != src) continue;
                    int ln = st.Line[i];
                    if (ln == want) { best = p; break; }
                    if (ln > want && ln < bestLine) { best = p; bestLine = ln; }
                }
                if (best < 0) { Note("Line " + want.ToString("N0") + " is past the end of " + name + " or hidden by filters."); return; }
                SelectPos(best, 1);
                grid.Focus();
                if (st.Line[view[best]] != want) Note("Line " + want.ToString("N0") + " has no entry of its own in this view. Showing the next one.");
            }
        }

        void UpdateStatus()
        {
            if (loading)
            {
                status.LeftText = "Reading…";
            }
            else if (st.Sources.Count == 0)
            {
                status.LeftText = "No log open";
            }
            else
            {
                status.LeftText = filtering ? "Filtering…" : viewCount == st.Count
                    ? string.Format("{0:N0} entries", st.Count)
                    : string.Format("{0:N0} of {1:N0} entries", viewCount, st.Count);
            }
            if (!loading && !noteTimer.Enabled)
            {
                int n = viewCount > 0 && grid.Cur >= 0 ? grid.SelHi - grid.SelLo + 1 : 0;
                int p = CurPos();
                string sel = n > 1 ? string.Format("{0:N0} rows selected", n) : p >= 0 ? string.Format("Row {0:N0}", p + 1) : "";
                status.MidText = sel.Length > 0 && loadSummary.Length > 0 ? sel + "   ·   " + loadSummary : sel + loadSummary;
            }
            status.Invalidate();
        }

        void RefreshCounts()
        {
            chipErr.Count = st.SevCount[3].ToString("N0");
            chipWarn.Count = st.SevCount[2].ToString("N0");
            chipInfo.Count = (st.SevCount[1] + st.SevCount[0]).ToString("N0");
            foreach (FluentButton b in new FluentButton[] { chipErr, chipWarn, chipInfo }) b.Invalidate();
            filterBar.PerformLayout();
        }

        void RefreshFilesUi()
        {
            int n = st.Sources.Count;
            if (n == 0)
            {
                status.RightText = "";
                Text = TitlePrefix + "LogVice";
            }
            else if (n == 1)
            {
                LogSource s0 = st.Sources[0];
                status.RightText = s0.Name + "  ·  " + Fmt.Size(Math.Max(s0.Pos, s0.Size)) + "  ·  " + Parser.FormatName(s0.Format) + "  ·  " + s0.EncName + (s0.Partial ? "  ·  partial" : "");
                Text = TitlePrefix + s0.Name + " - LogVice";
            }
            else
            {
                long size = 0;
                foreach (LogSource s in st.Sources) size += Math.Max(s.Pos, s.Size);
                status.RightText = n + " files merged by time  ·  " + Fmt.Size(size);
                Text = TitlePrefix + n + " files - LogVice";
            }
            status.Invalidate();
        }

        void CopySelection(bool messageOnly)
        {
            if (viewCount == 0 || grid.Cur < 0) return;
            int lo = grid.SelLo, hi = grid.SelHi;
            int cnt = hi - lo + 1;
            const int limit = 50000;
            StringBuilder sb = new StringBuilder();
            for (int p = lo; p <= hi && p - lo < limit; p++)
            {
                if (p < 0 || p >= viewCount) continue;
                int i = view[p];
                if (messageOnly)
                {
                    sb.Append(st.Msg[i]).Append("\r\n");
                    continue;
                }
                int c = st.Comp[i], th = st.Thread[i];
                sb.Append(Fmt.Time(st.Time[i])).Append('\t')
                  .Append(c >= 0 ? st.CompNames[c] : "").Append('\t')
                  .Append(th >= 0 ? th.ToString(CultureInfo.InvariantCulture) : "").Append('\t')
                  .Append(st.Msg[i]).Append("\r\n");
            }
            if (sb.Length == 0) return;
            try
            {
                Clipboard.SetText(sb.ToString());
                Note(cnt > limit ? string.Format("Copied the first {0:N0} of {1:N0} rows.", limit, cnt) : string.Format("Copied {0:N0} row{1}.", cnt, cnt == 1 ? "" : "s"));
            }
            catch (Exception)
            {
                Note("The clipboard is busy. Try again.");
            }
        }

        void ShowLookup(string code)
        {
            if (lookup == null || lookup.IsDisposed)
            {
                lookup = new LookupForm();
                lookup.Owner = this;
                lookup.ApplyTheme(theme);
                lookup.StartPosition = FormStartPosition.Manual;
                lookup.Location = new Point(Left + Math.Max(0, (Width - lookup.Width) / 2), Top + Dpi.S(150));
            }
            if (code != null) lookup.SetCode(code);
            if (!lookup.Visible) lookup.Show();
            lookup.Activate();
            lookup.FocusInput();
        }
    }
}
