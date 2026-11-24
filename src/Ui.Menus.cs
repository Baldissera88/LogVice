namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Windows.Forms;

    // The header menus (Open, Help and More) and the pages Help opens.
    public sealed partial class MainForm
    {
        static void Check(FlatButton b, bool on)
        {
            if (b.Checked == on) return;
            b.Checked = on;
            b.Invalidate();
        }

        // Gives a submenu the look of the menu it opens from.
        static void StyleSub(ToolStripMenuItem item, ContextMenuStrip parent)
        {
            ToolStripDropDownMenu d = item.DropDown as ToolStripDropDownMenu;
            if (d == null) return;
            d.Renderer = parent.Renderer;
            d.ShowImageMargin = false;
            d.ShowCheckMargin = false;
            d.BackColor = parent.BackColor;
            d.ForeColor = parent.ForeColor;
            d.Font = parent.Font;
        }

        ContextMenuStrip OpenMenu()
        {
            ContextMenuStrip m = Menus.New(theme);
            Menus.Add(m, "Open files\u2026", "Ctrl+O", delegate { PickFiles(false, null); });
            Menus.Add(m, "Merge in\u2026", "Ctrl+Shift+O", delegate { PickFiles(true, null); }).Enabled = st.Sources.Count > 0 && !loading;
            ToolStripMenuItem recent = Menus.Sub(m, "Recent files");
            StyleSub(recent, m);
            FillRecent(recent, m);
            Menus.Separator(m);
            Menus.Add(m, "Look around with demo logs", delegate { OpenDemo(); });
            return m;
        }

        void FillRecent(ToolStripMenuItem parent, ContextMenuStrip menu)
        {
            List<RecentEntry> list = RecentList.Ordered(Settings.RecentEntries());
            if (list.Count == 0)
            {
                Menus.Add(parent.DropDown, "No recent files", null).Enabled = false;
                return;
            }
            foreach (RecentEntry e in list)
            {
                bool exists = true;
                foreach (string p in e.Paths) if (!EventLogs.Available(p)) exists = false;
                string[] paths = e.Paths;
                string detail = e.Detail;
                if (detail.Length > 48) detail = "\u2026" + detail.Substring(detail.Length - 47);
                ToolStripMenuItem mi = Menus.Add(parent.DropDown, (e.Pinned ? "\u2022 " : "") + e.Label, detail, delegate { OpenFiles(paths, false, false); });
                mi.Enabled = exists;
                mi.ToolTipText = string.Join("\r\n", paths);
            }
            Menus.Separator(parent.DropDown);
            Menus.Add(parent.DropDown, "Clear recent files", delegate { Settings.ClearRecent(); RefreshRecent(); });
        }

        void RefreshRecent()
        {
            start.SetRecent(Program.Options.Shot != null ? new List<RecentEntry>() : Settings.RecentEntries());
        }

        // The buttons that do not fit in the header.
        ContextMenuStrip MoreMenu()
        {
            ContextMenuStrip m = Menus.New(theme);
            foreach (FlatButton b in header.Folded)
            {
                FlatButton button = b;
                string text = string.IsNullOrEmpty(b.Text) ? "Theme" : b.Text;
                string tip = tips.GetToolTip(b);
                ToolStripMenuItem mi = Menus.Add(m, text, delegate { button.PerformClick(); });
                mi.Checked = b.Checked && b.Name == HeaderFold.Live;
                mi.Enabled = b.Enabled;
                if (b.Name == HeaderFold.Live) m.ShowCheckMargin = true;
                if (!string.IsNullOrEmpty(tip)) mi.ToolTipText = tip;
            }
            return m;
        }

        public const string GitHubUrl = "https://github.com/" + Updates.Repo;

        ContextMenuStrip HelpMenu()
        {
            ContextMenuStrip m = Menus.New(theme);
            Menus.Add(m, "Welcome", delegate { ShowWelcome(); });
            Menus.Add(m, "What's new", delegate { ShowAllNotes(); });
            Menus.Separator(m);
            Menus.Add(m, "Keyboard shortcuts", delegate { ShowAbout(AboutForm.ShortcutsPart); });
            Menus.Add(m, "Command line", delegate { ShowAbout(AboutForm.CommandLinePart); });
            Menus.Separator(m);
            string why = FeedbackBlocked();
            ToolStripMenuItem report = Menus.Add(m, "Report a problem", delegate { OpenProblem(); });
            ToolStripMenuItem feature = Menus.Add(m, "Suggest a feature", delegate { OpenFeature(); });
            if (why != null)
            {
                report.Enabled = feature.Enabled = false;
                report.ToolTipText = feature.ToolTipText = why;
            }
            Menus.Separator(m);
            Menus.Add(m, "LogVice on GitHub", delegate { OpenLink(GitHubUrl); });
            Menus.Add(m, "About LogVice", delegate { ShowAbout(AboutForm.AboutPart); });
            return m;
        }

        void OpenLink(string url)
        {
            try { Process.Start(url); }
            catch (Exception) { Note("Could not open " + url); }
        }

        // Help > Welcome: the start page, also while logs are open.
        void ShowWelcome()
        {
            if (viewKind != ViewKind.Logs) ShowView(ViewKind.Logs);
            if (st.Sources.Count > 0 && !loading) { homeMode = true; ShowStart(); }
        }

        AboutForm MakeAbout(string part)
        {
            AboutForm f = new AboutForm(theme, part, pendingUpdate);
            f.InstallRequested += delegate (UpdateInfo u) { f.Close(); InstallUpdate(u); };
            f.WhatsNewRequested += delegate { ShowAllNotes(); };
            f.EasterEgg += delegate { f.Close(); ShowEgg(); };
            return f;
        }

        void ShowAbout(string part)
        {
            using (AboutForm f = MakeAbout(part)) f.ShowDialog(this);
        }

        // The text under the title: what is open.
        void UpdateSubtitle()
        {
            header.Subtitle = HeaderText.Subtitle(demoMode, Demo.Computer, OpenPaths());
        }

        void SetEntriesChip()
        {
            cEntries.Value = viewCount.ToString("N0");
            cEntries.Label = viewCount == st.Count ? "Entries" : "of " + st.Count.ToString("N0") + " entries";
            cEntries.Fit();
            cEntries.Invalidate();
        }
    }
}
