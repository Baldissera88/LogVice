namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;
    using Microsoft.Win32;

    public sealed class SettingsView : Panel, IThemed
    {
        Theme theme = Theme.Light();
        readonly BarPanel header;
        readonly FluentButton bBack;
        readonly TitleText title;
        readonly Segmented tabs;
        readonly Panel scroll, colSettings, colHigh;
        readonly List<Control> settingsItems = new List<Control>();
        readonly List<Control> highItems = new List<Control>();
        readonly Segmented segTheme, segSize, segDensity;
        readonly FluentToggle togTint, togLines, togLive, togMerge, togReopen, togUpdates;
        List<HighlightRule> rules = new List<HighlightRule>();
        readonly SettingRow rowRecent, rowFound;
        readonly ToolTip tips;
        int locationsStart, locationsEnd;
        bool loading;

        public event EventHandler BackRequested;
        public event EventHandler SettingsChanged;
        public event EventHandler FoldersChanged;
        public event EventHandler BrowseRequested;
        public event EventHandler RecentCleared;
        public event EventHandler HighlightsChanged;
        public event EventHandler ElevateRequested;

        public SettingsView(ToolTip tips)
        {
            this.tips = tips;
            header = new BarPanel();
            header.Dock = DockStyle.Top;
            header.Height = Dpi.S(64);
            header.Padding = new Padding(0, Dpi.S(4), 0, Dpi.S(4));
            bBack = new FluentButton(ButtonKind.Subtle, Glyph.Back, "");
            bBack.Tip = "Back to the log view (Esc)";
            tips.SetToolTip(bBack, bBack.Tip);
            bBack.Click += delegate { if (BackRequested != null) BackRequested(this, EventArgs.Empty); };
            title = new TitleText("Settings");
            tabs = new Segmented("Settings", "Highlights");
            tabs.Changed += delegate { ShowTab(tabs.Selected); };
            header.AddLeft(bBack, title);
            header.AddRight(tabs);

            scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.Resize += delegate { Relayout(); };
            scroll.HandleCreated += delegate { NativeMethods.ScrollTheme(scroll, theme.IsDark); };
            colSettings = new Panel();
            colHigh = new Panel();
            colHigh.Visible = false;
            scroll.Controls.Add(colSettings);
            scroll.Controls.Add(colHigh);
            Controls.Add(scroll);
            Controls.Add(header);

            segTheme = new Segmented(Palettes.Choices);
            segTheme.Changed += delegate { if (!loading) { Settings.Set("Theme", Palettes.Choices[segTheme.Selected]); Changed(); } };
            segSize = new Segmented("Small", "Medium", "Large");
            segSize.Changed += delegate { if (!loading) { Settings.Set("TextSize", segSize.Selected.ToString()); Changed(); } };
            segDensity = new Segmented("Compact", "Standard", "Relaxed");
            segDensity.Changed += delegate { if (!loading) { Settings.Set("Density", segDensity.Selected.ToString()); Changed(); } };
            togTint = Toggle("Tint", true);
            togLines = Toggle("LineNumbers", true);
            togLive = Toggle("Live", true);
            togMerge = Toggle("AutoMerge", Settings.GetBool("History", true));
            togReopen = Toggle("ReopenLast", false);
            togUpdates = Toggle("UpdateCheck", true);

            AddS(new SectionTitle("Appearance"));
            AddS(new SettingRow(Glyph.Palette, "Theme", "Choose a theme, or System to follow the Windows app mode.", segTheme));
            AddS(new SettingRow(Glyph.Text, "Log text size", "Size of the text in the log list and the detail pane.", segSize));
            AddS(new SettingRow(Glyph.Rows, "Row spacing", "How tightly rows are packed in the log list.", segDensity));
            AddS(new SettingRow(Glyph.Warning, "Tint error and warning rows", "Colour the whole row red or amber, not only the severity icon. Your own highlight rules are set under Highlights.", togTint));
            AddS(new SettingRow(Glyph.Rows, "Show line numbers", "Show the line in the log file for each row. For ETL traces this is the event number, and for event logs the record ID. Press Ctrl+G to go to a line.", togLines));

            AddS(new SectionTitle("Opening logs"));
            AddS(new SettingRow(Glyph.Live, "Start with Live on", "Follow new lines as they are written, from the moment LogVice starts.", togLive));
            AddS(new SettingRow(Glyph.Merge, "Auto-merge spanned logs", "When you open a log, also open the other parts it was split into and merge them by time: X.lo_, dated copies such as AppEnforce-20261104-101500.log or CbsPersist_*.log, and every WindowsUpdate.*.etl in the folder. Up to " + LogLocations.MaxParts + " of the newest parts are added.", togMerge));
            AddS(new SettingRow(Glyph.Reload, "Reopen the last logs at start", "Open the most recent log or merged set when LogVice starts.", togReopen));
            if (Program.IsAdmin)
            {
                SettingRow adminRow = new SettingRow(Glyph.Shield, "Administrator rights", "LogVice is running as administrator, so it can read protected logs on this computer.", null);
                adminRow.Value = "On";
                AddS(adminRow);
            }
            else
            {
                FluentButton bAdmin = Button("Restart as administrator", "Restart LogVice with a UAC prompt and open the same logs again");
                bAdmin.Glyph = Glyph.Shield;
                bAdmin.Width = bAdmin.GetPreferredSize(Size.Empty).Width;
                bAdmin.Click += delegate { if (ElevateRequested != null) ElevateRequested(this, EventArgs.Empty); };
                AddS(new SettingRow(Glyph.Shield, "Administrator rights", "Some logs, such as Windows Temp or a site server's logs, can only be read by an administrator. LogVice asks when it meets one, or restart it here.", bAdmin));
            }
            FluentButton bApps = Button("Open Default apps", "Open Windows settings to choose the default app for .log files");
            bApps.Click += delegate
            {
                try { Process.Start("ms-settings:defaultapps"); } catch (Exception) { }
            };
            AddS(new SettingRow(Glyph.Open, "Default app for .log files", "After installing the MSI, choose LogVice for .log and .lo_ under Default apps > Choose defaults by file type.", bApps));

            AddS(new SectionTitle("Log folders"));
            FluentButton bBrowse = Button("Browse logs", "Open the log browser");
            bBrowse.Click += delegate { if (BrowseRequested != null) BrowseRequested(this, EventArgs.Empty); };
            rowFound = new SettingRow(Glyph.Browse, "Found on this computer", "Looking for log folders…", bBrowse);
            AddS(rowFound);
            locationsStart = locationsEnd = settingsItems.Count;
            FluentButton bAdd = Button("Add folder", "Add a folder with logs, for example a share with collected logs");
            bAdd.Click += delegate { AddFolder(); };
            AddS(new SettingRow(Glyph.Plus, "Add a folder", "Add your own log folder. It appears on the start page and in Browse logs.", bAdd));

            AddS(new SectionTitle("Updates"));
            if (Updates.BlockedByPolicy)
            {
                SettingRow pol = new SettingRow(Glyph.Info, "Check for updates", "Your organization has turned this off with the policy value DisableUpdateCheck.", null);
                pol.Value = "Off";
                AddS(pol);
            }
            else AddS(new SettingRow(Glyph.Info, "Check for updates", "Once a day at start, LogVice asks GitHub which version is the latest and shows an Update button when there is a newer one. Nothing about you or your logs is sent. Administrators can turn it off for everyone with the DWORD DisableUpdateCheck = 1 under HKLM\\SOFTWARE\\Policies\\LogVice.", togUpdates));

            AddS(new SectionTitle("Data"));
            FluentButton bClear = Button("Clear", "Remove all recent files");
            bClear.Click += delegate
            {
                Settings.ClearRecent();
                UpdateRecentRow();
                if (RecentCleared != null) RecentCleared(this, EventArgs.Empty);
            };
            rowRecent = new SettingRow(Glyph.Recent, "Recent files", "", bClear);
            AddS(rowRecent);
            FluentButton bReset = Button("Reset", "Restore every setting to its default");
            bReset.Click += delegate
            {
                if (MessageBox.Show(FindForm(), "Reset all settings? Recent files, your own folders and your highlight rules are removed too.", "LogVice", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Settings.ResetAll();
                rules = new List<HighlightRule>();
                BuildHighlights();
                if (HighlightsChanged != null) HighlightsChanged(this, EventArgs.Empty);
                LoadValues();
                Changed();
                if (FoldersChanged != null) FoldersChanged(this, EventArgs.Empty);
                if (RecentCleared != null) RecentCleared(this, EventArgs.Empty);
            };
            AddS(new SettingRow(Glyph.Reload, "Reset settings", "Restore all settings to their defaults. Stored in " + Settings.Location + ".", bReset));

            rules = HighlightRule.Load();
            BuildHighlights();
            LoadValues();
            foreach (Control c in settingsItems) HookWheel(c);
            HookWheel(header);
        }

        void HookWheel(Control c)
        {
            c.MouseWheel += OnChildWheel;
            foreach (Control k in c.Controls) HookWheel(k);
        }

        void OnChildWheel(object sender, MouseEventArgs e)
        {
            HandledMouseEventArgs h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
            int y = -scroll.AutoScrollPosition.Y - e.Delta / 120 * Dpi.S(80);
            scroll.AutoScrollPosition = new Point(0, Math.Max(0, y));
        }

        FluentToggle Toggle(string name, bool def)
        {
            FluentToggle t = new FluentToggle();
            t.SetSilently(Settings.GetBool(name, def));
            t.Tag = name;
            t.Changed += delegate { if (!loading) { Settings.SetBool(name, t.On); Changed(); } };
            return t;
        }

        FluentButton Button(string text, string tip)
        {
            FluentButton b = new FluentButton(ButtonKind.Standard, Glyph.None, text);
            b.Width = b.GetPreferredSize(Size.Empty).Width;
            b.Tip = tip;
            tips.SetToolTip(b, tip);
            return b;
        }

        void AddS(Control c)
        {
            settingsItems.Add(c);
            colSettings.Controls.Add(c);
        }


        void Changed()
        {
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        public void LoadValues()
        {
            loading = true;
            segTheme.SetSilently(Math.Max(0, Array.IndexOf(Palettes.Choices, Theme.SavedName())));
            segSize.SetSilently(Settings.GetInt("TextSize", 1, 0, 2));
            segDensity.SetSilently(Settings.GetInt("Density", 1, 0, 2));
            togTint.SetSilently(Settings.GetBool("Tint", true));
            togLines.SetSilently(Settings.GetBool("LineNumbers", true));
            togLive.SetSilently(Settings.GetBool("Live", true));
            togMerge.SetSilently(Settings.GetBool("AutoMerge", Settings.GetBool("History", true)));
            togReopen.SetSilently(Settings.GetBool("ReopenLast", false));
            togUpdates.SetSilently(Settings.GetBool("UpdateCheck", true));
            loading = false;
            UpdateRecentRow();
        }

        public void SetAutoMerge(bool on)
        {
            togMerge.SetSilently(on);
        }

        public List<HighlightRule> Rules
        {
            get { return rules; }
        }

        void BuildHighlights()
        {
            colHigh.SuspendLayout();
            foreach (Control c in highItems) { colHigh.Controls.Remove(c); c.Dispose(); }
            highItems.Clear();
            AddH(new SectionTitle("Highlight rules"));
            FluentButton bAdd = Button("Add rule", "Add a highlight rule");
            bAdd.Kind = ButtonKind.Accent;
            bAdd.Click += delegate { EditRule(-1); };
            AddH(new SettingRow(Glyph.Palette, "Colour rows with your own rules", "Rows whose message or component matches a rule get its colour and label, so you are not limited to red for errors and amber for warnings. Rules are checked from the top and the first match wins. F7 and Shift+F7 jump between highlighted rows, and the Highlights filter shows only the rows of one rule.", bAdd));
            if (rules.Count == 0)
            {
                FluentButton bEx = Button("Add examples", "Add rules for exit codes, detection, downloads, restarts and success");
                bEx.Click += delegate
                {
                    rules.AddRange(HighlightRule.Examples());
                    RulesChanged();
                };
                AddH(new SettingRow(Glyph.Info, "No rules yet", "Add your own, or start with examples for exit codes, detection, downloads, restarts and success.", bEx));
            }
            for (int i = 0; i < rules.Count; i++)
            {
                HighlightRule r = rules[i];
                int idx = i;
                FluentToggle on = new FluentToggle();
                on.SetSilently(r.On);
                on.Changed += delegate { rules[idx].On = on.On; RulesChanged(); };
                FluentButton up = new FluentButton(ButtonKind.Subtle, Glyph.Up, "");
                up.Enabled = i > 0;
                up.Tip = "Move up. Rules higher in the list win.";
                tips.SetToolTip(up, up.Tip);
                up.Click += delegate
                {
                    if (idx <= 0) return;
                    HighlightRule t = rules[idx - 1];
                    rules[idx - 1] = rules[idx];
                    rules[idx] = t;
                    RulesChanged();
                };
                FluentButton edit = Button("Edit", "Change this rule");
                edit.Click += delegate { EditRule(idx); };
                FluentButton del = Button("Remove", "Remove this rule");
                del.Click += delegate
                {
                    rules.RemoveAt(idx);
                    RulesChanged();
                };
                SettingRow row = new SettingRow(Glyph.Palette, r.Name, (r.IsRegex ? "regex  " : "contains  ") + r.Pattern, new ButtonStrip(on, up, edit, del));
                row.Swatch = r.Color;
                row.MonoDesc = true;
                row.Dim = !r.On;
                AddH(row);
            }
            foreach (Control c in highItems)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(theme);
                HookWheel(c);
            }
            colHigh.ResumeLayout(false);
            Relayout();
            colHigh.Invalidate(true);
        }

        void AddH(Control c)
        {
            highItems.Add(c);
            colHigh.Controls.Add(c);
        }

        void EditRule(int idx)
        {
            HighlightRule r = idx >= 0 ? rules[idx].Clone() : new HighlightRule();
            if (idx < 0) r.Color = HighlightRule.Palette[rules.Count % HighlightRule.Palette.Length];
            using (HighlightEditor d = new HighlightEditor(r, theme, idx < 0))
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK || d.Result == null) return;
                if (idx >= 0) { d.Result.On = rules[idx].On; rules[idx] = d.Result; }
                else rules.Add(d.Result);
            }
            RulesChanged();
        }

        public void AddRule(HighlightRule seed)
        {
            ShowTab(1);
            HighlightRule r = seed;
            r.Color = HighlightRule.Palette[rules.Count % HighlightRule.Palette.Length];
            using (HighlightEditor d = new HighlightEditor(r, theme, true))
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK || d.Result == null) return;
                rules.Add(d.Result);
            }
            RulesChanged();
        }

        void RulesChanged()
        {
            HighlightRule.Save(rules);
            if (HighlightsChanged != null) HighlightsChanged(this, EventArgs.Empty);
            int y = scroll.AutoScrollPosition.Y;
            Action rebuild = delegate
            {
                BuildHighlights();
                scroll.AutoScrollPosition = new Point(0, -y);
            };
            if (IsHandleCreated) BeginInvoke(rebuild); else rebuild();
        }

        void UpdateRecentRow()
        {
            int n = Settings.Recent().Count;
            rowRecent.Desc = n == 0 ? "No recent files are stored." : n == 1 ? "1 file or merged set is remembered." : n + " files or merged sets are remembered.";
            rowRecent.Invalidate();
        }

        public void SetLocations(List<LogLocation> list)
        {
            colSettings.SuspendLayout();
            for (int i = locationsEnd - 1; i >= locationsStart; i--)
            {
                Control c = settingsItems[i];
                settingsItems.RemoveAt(i);
                colSettings.Controls.Remove(c);
                c.Dispose();
            }
            int k = locationsStart;
            int found = 0;
            foreach (LogLocation l in list)
            {
                SettingRow row;
                if (l.Custom)
                {
                    FluentButton bRemove = Button("Remove", "Remove this folder from LogVice. The folder itself is not changed.");
                    string path = l.Path;
                    bRemove.Click += delegate { RemoveFolder(path); };
                    row = new SettingRow(Glyph.Folder, l.Name, l.Path + (l.FileCount > 0 ? "  ·  " + l.Summary : "  ·  no logs found"), bRemove);
                }
                else
                {
                    found++;
                    row = new SettingRow(l.Glyph, l.Group + "  ·  " + l.Name, l.Where, null);
                    row.Value = l.Summary;
                }
                row.ApplyTheme(theme);
                HookWheel(row);
                settingsItems.Insert(k++, row);
                colSettings.Controls.Add(row);
            }
            locationsEnd = k;
            rowFound.Desc = found == 0
                ? "No ConfigMgr, Intune or Windows log folders were found on this computer."
                : found + (found == 1 ? " folder" : " folders") + " with logs. LogVice looks for ConfigMgr site server, management point, distribution point, console, client and task sequence logs, Intune Management Extension logs and Windows logs.";
            colSettings.ResumeLayout(false);
            Relayout();
            Invalidate(true);
        }

        void AddFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Choose a folder with log files";
                d.ShowNewFolderButton = false;
                if (d.ShowDialog(FindForm()) != DialogResult.OK || string.IsNullOrEmpty(d.SelectedPath)) return;
                List<string> list = Settings.GetList("Folders");
                foreach (string s in list) if (string.Equals(s, d.SelectedPath, StringComparison.OrdinalIgnoreCase)) return;
                list.Add(d.SelectedPath);
                Settings.SetList("Folders", list);
                if (FoldersChanged != null) FoldersChanged(this, EventArgs.Empty);
            }
        }

        void RemoveFolder(string path)
        {
            List<string> list = Settings.GetList("Folders");
            list.RemoveAll(delegate (string s) { return string.Equals(s, path, StringComparison.OrdinalIgnoreCase); });
            Settings.SetList("Folders", list);
            if (FoldersChanged != null) FoldersChanged(this, EventArgs.Empty);
        }

        public void ShowTab(int i)
        {
            if (tabs.Selected != i) tabs.SetSilently(i);
            colSettings.Visible = i == 0;
            colHigh.Visible = i == 1;
            title.Text = i == 0 ? "Settings" : "Highlights";
            title.Fit();
            header.PerformLayout();
            scroll.AutoScrollPosition = new Point(0, 0);
            if (i == 0) UpdateRecentRow();
            Relayout();
        }

        void Relayout()
        {
            LayoutCol(colSettings, settingsItems);
            LayoutCol(colHigh, highItems);
        }

        void LayoutCol(Panel col, List<Control> items)
        {
            if (!col.Visible) return;
            int avail = scroll.ClientSize.Width;
            int w = Math.Min(Dpi.S(1000), avail - Dpi.S(8));
            if (w < Dpi.S(200)) return;
            int x = Math.Max(0, (avail - w) / 2);
            int y = 0;
            col.SuspendLayout();
            foreach (Control c in items)
            {
                IMeasured m = c as IMeasured;
                int h = m != null ? m.Measure(w) : c.Height;
                if (c is SectionTitle && y > 0) y += Dpi.S(14);
                c.SetBounds(0, y, w, h);
                y += h + (c is SectionTitle ? Dpi.S(2) : Dpi.S(4));
            }
            col.ResumeLayout(true);
            col.SetBounds(x, scroll.AutoScrollPosition.Y, w, y + Dpi.S(24));
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Bg;
            header.ApplyTheme(t);
            scroll.BackColor = t.Bg;
            colSettings.BackColor = colHigh.BackColor = t.Bg;
            foreach (Control c in header.Controls) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            foreach (Control c in settingsItems) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            foreach (Control c in highItems) { IThemed th = c as IThemed; if (th != null) th.ApplyTheme(t); }
            NativeMethods.ScrollTheme(scroll, t.IsDark);
            Invalidate(true);
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
