namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    // Settings > Updates, and what the window does with the answer from GitHub.
    public static class UpdatesUi
    {
        // The last answer from GitHub in this run, for Settings.
        public static UpdateInfo Last;
        // Set by the main window: asks and installs.
        public static Action<UpdateInfo> Install;
        // Screenshots show this instead of asking GitHub.
        public static UpdateInfo Demo;

        public static UpdateInfo DemoInfo()
        {
            UpdateInfo r = new UpdateInfo();
            r.Latest = "2.1.0";
            r.Newer = true;
            r.Url = Updates.ReleasesUrl + "/tag/v2.1.0";
            r.MsiUrl = Updates.ReleasesUrl + "/download/v2.1.0/LogVice.msi";
            r.ExeUrl = Updates.ReleasesUrl + "/download/v2.1.0/LogVice.exe";
            r.SumsUrl = Updates.ReleasesUrl + "/download/v2.1.0/SHA256SUMS.txt";
            r.CheckedUtc = DateTime.UtcNow.AddMinutes(-3);
            r.Notes = "### What's new\n\n- Open a log at a line or a time from the command line.\n- The log list keeps its columns readable in a narrow window.\n- Error codes in a Copy for ticket come with what to do next.\n\n### What changed\n\n- The detail pane scrolls when the window is low.";
            return r;
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(url); }
            catch (Exception) { Safe.Say("Could not open " + url); }
        }

        static string WhenText(DateTime utc)
        {
            return utc.ToLocalTime().ToString("d MMMM yyyy HH:mm", CultureInfo.CurrentCulture);
        }

        // The rows of Settings > Updates that LogVice makes: the check, and the version with Check now and Install.
        public static List<Control> SettingRows(Theme theme, MethodInvoker relayout)
        {
            List<Control> l = new List<Control>();
            string blocked = Demo != null ? null : Updates.Blocked;
            bool policy = Demo == null && blocked != null && blocked.IndexOf(UpdateGate.PolicyValue, StringComparison.Ordinal) >= 0;
            if (policy)
            {
                SettingRow pol = new SettingRow(Glyph.Reload, "Check for updates", blocked, null);
                pol.Value = "Off";
                l.Add(pol);
            }
            else
            {
                FluentToggle tog = new FluentToggle();
                tog.SetSilently(Settings.GetBool("UpdateCheck", true));
                tog.Changed += Safe.Wrap(delegate { if (Demo == null) Settings.SetBool("UpdateCheck", tog.On); });
                l.Add(new SettingRow(Glyph.Reload, "Check for updates", "", tog));
            }

            FlatButton b = new FlatButton("Check now", Glyph.None);
            b.Compact = true;
            b.Outline = true;
            SettingRow row = new SettingRow(Glyph.Info, "Updates", "", b);
            UpdateInfo shown = Demo ?? Last;
            MethodInvoker show = null;
            show = delegate
            {
                UpdateInfo r = shown;
                string desc, text;
                DateTime? last = Demo != null ? (DateTime?)Demo.CheckedUtc : Updates.LastChecked;
                string when = last.HasValue ? " Last checked " + WhenText(last.Value) + "." : " Not checked yet.";
                string where = " Releases are published on " + Updates.ReleasesUrl + ".";
                if (blocked != null && !policy) { desc = blocked + where; text = "Check now"; }
                else if (policy) { desc = where.TrimStart(); text = "Check now"; }
                else if (r == null) { desc = "You have LogVice " + Program.Version + "." + when + where; text = "Check now"; }
                else if (r.Error != null) { desc = r.Error + when + where; text = "Check now"; }
                else if (r.Newer)
                {
                    bool can = Demo != null || r.CanInstall;
                    desc = "LogVice " + r.Latest + " is out. You have " + Program.Version + "." + when + " " + (can ? "Install downloads it from GitHub, checks it against the release's checksum and installs it. LogVice then opens again with the same logs." : "Download opens the release page.");
                    text = (can ? "Install " : "Download ") + r.Latest;
                }
                else { desc = "You have the latest version, " + Program.Version + "." + when; text = "Check now"; }
                row.Desc = desc;
                b.Text = text;
                b.AutoWidth();
                b.Width += Dpi.S(8);
                b.Enabled = blocked == null;
                row.PerformLayout();
                row.Invalidate();
                if (relayout != null) relayout();
            };
            b.Click += Safe.Wrap(delegate
            {
                UpdateInfo r = shown;
                if (Demo != null) return;
                if (r != null && r.Newer)
                {
                    if (r.CanInstall && Install != null)
                    {
                        Form f = row.FindForm();
                        UpdateInfo go = r;
                        if (f != null && f.Modal)
                        {
                            f.FormClosed += delegate { Install(go); };
                            f.Close();
                        }
                        else Install(go);
                    }
                    else OpenUrl(r.Url);
                    return;
                }
                blocked = Updates.Blocked;
                if (blocked != null) { show(); return; }
                b.Enabled = false;
                row.Desc = "Checking GitHub…";
                row.Invalidate();
                Updates.CheckAsync(delegate (UpdateInfo res)
                {
                    try
                    {
                        if (row.IsDisposed || !row.IsHandleCreated) return;
                        row.BeginInvoke((MethodInvoker)delegate
                        {
                            shown = res;
                            if (res.Error == null) Last = res;
                            show();
                        });
                    }
                    catch (InvalidOperationException) { }
                });
            });
            b.ApplyTheme(theme);
            show();
            l.Add(row);
            return l;
        }
    }

    // In the status bar: a newer LogVice is out. Installs only when clicked.
    public sealed class UpdateNote : FlatControl
    {
        public event EventHandler ActionClicked;
        string action = "Install";
        bool working;

        public UpdateNote()
        {
            Cursor = Cursors.Hand;
            TabStop = false;
            Visible = false;
        }

        public void Say(string text, string actionText)
        {
            Text = text;
            action = actionText;
            working = actionText == null;
            Cursor = working ? Cursors.Default : Cursors.Hand;
            Width = Dpi.S(12) + Draw.Measure(Text, Fonts.Small).Width + (working ? 0 : Dpi.S(30) + Draw.Measure(action, Fonts.SmallSemibold).Width) + Dpi.S(4);
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!working && ActionClicked != null) ActionClicked(this, e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : T.Bg);
            int tw = Draw.Measure(Text, Fonts.Small).Width;
            int x = Dpi.S(6);
            Draw.Text(g, Text, Fonts.Small, new Rectangle(x, 0, tw + 2, Height), T.Text2, Draw.Line);
            if (working) return;
            x += tw + Dpi.S(8);
            Draw.Text(g, "·", Fonts.Small, new Rectangle(x, 0, Dpi.S(10), Height), T.Text3, Draw.Line);
            Font f = hover ? new Font(Fonts.SmallSemibold, FontStyle.Underline) : Fonts.SmallSemibold;
            try { Draw.Text(g, action, f, new Rectangle(x + Dpi.S(14), 0, Math.Max(0, Width - x - Dpi.S(14)), Height), T.Accent, Draw.Line); }
            finally { if (hover) f.Dispose(); }
        }
    }

    // The release notes in a card, as high as the notes are.
    public sealed class NotesCard : Panel, IThemed, IMeasured
    {
        Theme theme = Theme.Light();
        readonly NotesView view = new NotesView();

        public NotesCard(string markdown)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Controls.Add(view);
            view.Markdown = string.IsNullOrEmpty(markdown) ? "No release notes were published with this version." : markdown;
        }

        public int Measure(int width)
        {
            int w = Math.Max(Dpi.S(100), width - Dpi.S(40));
            view.Width = w;
            view.LayoutNotes();
            view.Location = new Point(Dpi.S(20), Dpi.S(12));
            return view.Height + Dpi.S(24);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Card;
            view.ApplyTheme(t);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : theme.Bg);
            Draw.Card(e.Graphics, ClientRectangle, theme);
        }
    }

    // Asks before an update is downloaded: Install, Release page or Not now.
    public sealed class UpdateDialog : PageForm
    {
        public UpdateDialog(Theme t, UpdateInfo r, bool msi) : base("Update LogVice", t)
        {
            Size = new Size(Dpi.S(720), Dpi.S(620));
            Add(new SectionTitle("LogVice " + r.Latest + " is out"));
            Add(new SettingRow(Glyph.Info, "Install", "You have " + Program.Version + ". Install downloads " + (msi ? "LogVice.msi" : "LogVice.exe") + " from GitHub to a new folder under Temp, checks it against the release's SHA256SUMS.txt and installs it. LogVice closes and opens again with the same logs." + (msi ? " Windows asks for administrator rights." : " The new LogVice.exe replaces this one in " + System.IO.Path.GetDirectoryName(Program.Location) + "."), null));
            Add(new SectionTitle("What's new"));
            Add(new NotesCard(r.Notes));
            CloseAs("Not now", false);
            // Docked from the right: Not now, then Release page, then Install.
            Footer("Release page", false, delegate { DialogResult = DialogResult.No; Close(); });
            Footer("Install", true, delegate { DialogResult = DialogResult.Yes; Close(); });
        }
    }

    public sealed partial class MainForm
    {
        UpdateNote updateNote;
        UpdateInfo pendingUpdate;
        bool installingUpdate;

        // What stops an update check or an install here. Runs on any thread.
        string UpdateBlocked()
        {
            UpdateContext c = new UpdateContext();
            c.Window = true;
            c.Screenshot = Program.Options.Shot != null;
            bool system = false;
            try
            {
                using (System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent()) system = id.IsSystem;
            }
            catch (Exception) { }
            bool ts = false;
            try
            {
                foreach (string n in new string[] { "TSManager", "TsProgressUI" })
                {
                    Process[] ps = Process.GetProcessesByName(n);
                    if (ps.Length > 0) ts = true;
                    foreach (Process p in ps) p.Dispose();
                }
            }
            catch (Exception) { }
            UpdateGate.ReadDevice(c, new LiveRegistry(), Environment.UserName, system, ts);
            return UpdateGate.Reason(c);
        }

        // Once a day at start.
        void StartUpdateCheck()
        {
            Updates.Gate = UpdateBlocked;
            UpdatesUi.Install = delegate (UpdateInfo r) { SafeInvoke(delegate { InstallUpdate(r); }); };
            if (Program.Options.Shot != null) return;
            if (!Updates.Enabled || !Updates.DueToday) return;
            Updates.CheckAsync(delegate (UpdateInfo r) { SafeInvoke(delegate { UpdateFound(r); }); });
        }

        void UpdateFound(UpdateInfo r)
        {
            if (r == null || r.Error != null) return;
            UpdatesUi.Last = r;
            if (!r.Newer) return;
            pendingUpdate = r;
            ShowUpdateNote(r);
        }

        void ShowUpdateNote(UpdateInfo r)
        {
            if (updateNote == null)
            {
                updateNote = new UpdateNote();
                updateNote.ActionClicked += Safe.Wrap(delegate
                {
                    if (pendingUpdate == null) return;
                    if (pendingUpdate.CanInstall) InstallUpdate(pendingUpdate);
                    else UpdatesUi.OpenUrl(pendingUpdate.Url);
                });
                status.Controls.Add(updateNote);
                status.Note = updateNote;
                updateNote.ApplyTheme(theme);
            }
            updateNote.Say("LogVice " + r.Latest + " is out", pendingUpdate != null && pendingUpdate.CanInstall ? "Install" : "Release page");
            updateNote.Visible = true;
            status.Invalidate();
        }

        void InstallUpdate(UpdateInfo r)
        {
            if (r == null || installingUpdate) return;
            string why = UpdateBlocked();
            if (why != null)
            {
                string text = "LogVice " + r.Latest + " is out. " + why.Replace("No update check", "It is not installed") + "\r\n\r\nOpen the release page?";
                if (MessageBox.Show(this, text, "Update LogVice", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) UpdatesUi.OpenUrl(r.Url);
                return;
            }
            bool msi = Updates.IsInstalled;
            DialogResult a;
            using (UpdateDialog d = new UpdateDialog(theme, r, msi)) a = d.ShowDialog(this);
            if (a == DialogResult.No) { UpdatesUi.OpenUrl(r.Url); return; }
            if (a != DialogResult.Yes) return;
            installingUpdate = true;
            if (updateNote != null) updateNote.Say("Downloading LogVice " + r.Latest + "…", null);
            string[] reopen = OpenPaths();
            Task.Factory.StartNew(delegate
            {
                string file = null, err = null;
                try { file = Updates.Prepare(r, delegate (int pct) { SafeInvoke(delegate { if (updateNote != null) updateNote.Say("Downloading LogVice " + r.Latest + "… " + pct + " %", null); }); }); }
                catch (Exception ex) { err = ex.Message; }
                SafeInvoke(delegate
                {
                    installingUpdate = false;
                    if (err == null)
                    {
                        try
                        {
                            if (updateNote != null) updateNote.Say("Installing LogVice " + r.Latest + "…", null);
                            Updates.Launch(file, reopen);
                            Updates.HandedOff = true;
                            Hide();
                            Close();
                            return;
                        }
                        catch (Exception ex) { err = ex.Message; }
                    }
                    if (updateNote != null) updateNote.Say("LogVice " + r.Latest + " is out", "Install");
                    if (MessageBox.Show(this, "LogVice " + r.Latest + " could not be installed automatically.\r\n\r\n" + err + "\r\n\r\nOpen the release page to download it?", "Update LogVice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        UpdatesUi.OpenUrl(r.Url);
                });
            });
        }

        // Screenshots: the question before Install, and the line in the status bar.
        Form ShotUpdate(string page)
        {
            UpdateInfo demo = UpdatesUi.DemoInfo();
            if (page == "update") return new UpdateDialog(theme, demo, true);
            pendingUpdate = demo;
            ShowUpdateNote(demo);
            return null;
        }
    }
}
