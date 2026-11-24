namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Windows.Forms;

    // --demo and --shot: open the made-up logs, and draw the window into a PNG for the documents.
    public sealed partial class MainForm
    {
        bool demoNext, demoMode;
        Timer shotTimer;
        bool shotReady;
        Form shotPage;
        int shotWaited;
        // Steps a screenshot page takes one after the other, each once the one before has finished loading.
        readonly Queue<Action> shotQueue = new Queue<Action>();

        // Writes the demo logs and opens the ConfigMgr and Intune ones, merged by time.
        void OpenDemo()
        {
            string[] paths;
            try { paths = Demo.LookAround(Demo.Write()); }
            catch (Exception ex)
            {
                landing = null;
                Note("The demo logs could not be written: " + ex.Message);
                return;
            }
            demoNext = true;
            OpenFiles(paths, false, false);
            if (Program.Options.Shot != null) StartShot();
        }

        // Called when the window is shown. A start page with nothing open is ready at once.
        void ShotAtStart()
        {
            if (Program.Options.Shot == null || Program.Options.Demo) return;
            StartShot();
        }

        void StartShot()
        {
            if (shotTimer != null) return;
            shotTimer = new Timer();
            shotTimer.Interval = 250;
            shotTimer.Tick += delegate { ShotTick(); };
            shotTimer.Start();
        }

        void ShotTick()
        {
            shotWaited += shotTimer.Interval;
            bool busy = loading || filtering || loadTok != null;
            if (!shotReady)
            {
                if (busy && shotWaited < 90000) return;
                shotReady = true;
                ShotSetup();
                // A short wait after loading, so the list, the marks and the counts are drawn.
                shotTimer.Interval = 1500;
                return;
            }
            if (busy && shotWaited < 90000) return;
            if (shotQueue.Count > 0)
            {
                shotQueue.Dequeue()();
                return;
            }
            shotTimer.Stop();
            try { ShotSave(); }
            catch (Exception ex) { Console.Error.WriteLine("Screenshot failed: " + ex.Message); }
            Close();
        }

        void ShotSetup()
        {
            // Mono draws a text cursor in the read-only message box while it keeps its selection visible; a picture shows none.
            tMsg.HideSelection = true;
            grid.Focus();
            string sel = Program.Options.Select;
            // A demo screenshot opens on the failed install, so the detail pane shows something worth reading.
            StartOptions so = Program.Options;
            bool placed = so.Line > 0 || so.Time != Store.NoTime || so.Find != null || so.SessionPath != null;
            if (sel == null && so.Demo && !placed) sel = "Unmatched exit code (1603)";
            if (!string.IsNullOrEmpty(sel) && viewKind == ViewKind.Logs)
            {
                for (int p = 0; p < viewCount; p++)
                {
                    if (st.Msg[view[p]].IndexOf(sel, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SelectPos(p, 1);
                        break;
                    }
                }
            }
            string page = Program.Options.Page;
            if (page == "welcome") { ShotWelcomeData(); ShowWelcome(); }
            else if (page == AboutForm.AboutPart || page == AboutForm.ShortcutsPart || page == AboutForm.CommandLinePart)
                ShowShotPage(MakeAbout(page), true);
            else if (SettingsDialog.IsShotPage(page))
            {
                Form f = SettingsDialog.ForShot(theme, page, Program.Options.Select, DemoLocations());
                if (page == "settings-" + SettingsCatalog.Updates) UpdatesUi.Demo = UpdatesUi.DemoInfo();
                ShowShotPage(f, Program.Options.Select != "narrow");
            }
            else if (page == "browse")
            {
                // The first folder is the folder of the made-up logs, so its list has files.
                localLocations = DemoLocations();
                try
                {
                    string[] all = Demo.Write();
                    localLocations[0].Path = Path.GetDirectoryName(all[0]);
                    localLocations[0].FileCount = Directory.GetFiles(localLocations[0].Path, "*.log").Length;
                }
                catch (Exception) { }
                OpenBrowse(localLocations[0]);
            }
            else if (page == "follow") ShowShotPage(ShotFollow(), true);
            else if (page == "lookup") ShowShotPage(ShotLookup(), true);
            else if (page == "ticket") ShowShotPage(TicketPage(), true);
            else if (page == "history") ShotHistory();
            else if (page == "whatsnew")
                ShowShotPage(new NotesForm(theme, "What's new", "What's new in LogVice", "You have " + Program.Version + ". Every version is listed, newest first.", ReleaseNotes.Sections(ReleaseNotes.Changelog, null, Program.Version)), true);
            else if (page == "update") ShowShotPage(ShotUpdate(page), true);
            else if (page == "update-note") ShotUpdate(page);
            else if (page == "feedback" || page == "feature") ShowShotPage(ShotFeedback(page), true);
            // Keep the pointer out of the picture, so nothing shows as hovered.
            try { Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 2, Screen.PrimaryScreen.Bounds.Bottom - 2); } catch (Exception) { }
        }

        static FollowItem Item(string kind, string name, string id, string state, string note)
        {
            FollowItem it = new FollowItem();
            it.Kind = kind; it.Name = name; it.Id = id; it.State = state; it.Note = note;
            it.Terms.Add(id);
            return it;
        }

        Form ShotFollow()
        {
            List<FollowItem> items = new List<FollowItem>();
            items.Add(Item("Application", "Contoso Timesheet 4.2", "ScopeId_8B1F6A2C-4D3E-4F50-9A6B-7C8D9E0F1A2B/Application_5c1e9d0a", "Failed", "ConfigMgr client"));
            items.Add(Item("Intune Win32 app", "Fabrikam PDF Reader 11", "e1bdab12-2e72-4351-bf06-908c6a2b7d11", "Installed", "Intune Management Extension"));
            items.Add(Item("Intune Win32 app", "Woodgrove VPN Client 5.3", "3f6b2a47-9c1d-4e58-b0a2-61d7c4e90f33", "Required", "Intune Management Extension"));
            items.Add(Item("Software update", "2026-10 Cumulative Update for Windows 11 (KB5044285)", "KB5044285", "Failed", "0x87D00324"));
            items.Add(Item("Package", "Northwind Inventory Agent 3.1", "CON00142", "Installed", "ConfigMgr client"));
            items.Add(Item("Remediation", "Contoso disk cleanup", "7a9d2c11-5e04-4b8f-8d2a-0c3e6f1b94aa", "Detected, fixed", "Intune Management Extension"));
            items.Add(Item("Platform script", "Platform script 4d1f0c2e", "4d1f0c2e-8a6b-4c3d-9e7f-1b2a3c4d5e6f", "Success", "Intune Management Extension"));
            items.Add(Item("Installed app", "Microsoft Visual C++ 2022 Redistributable (x64)", "{8A3E5C9F-1B2D-4F60-A7C8-3D9E0B1F2A45}", "14.38.33135", "Apps & features"));
            FollowForm.DemoItems = items;
            FollowForm f = new FollowForm(null);
            f.ApplyTheme(theme);
            return f;
        }

        Form ShotLookup()
        {
            LookupForm f = new LookupForm();
            f.ApplyTheme(theme);
            f.SetCode("0x87D00324");
            return f;
        }

        // Opens two other logs and goes back once, so Back and Forward are both ready.
        void ShotHistory()
        {
            string cas = null, enforce = null;
            foreach (string p in OpenPaths())
            {
                if (Path.GetFileName(p) == "CAS.log") cas = p;
                if (Path.GetFileName(p) == "AppEnforce.log") enforce = p;
            }
            if (cas == null || enforce == null) return;
            shotQueue.Enqueue(delegate { OpenFiles(new string[] { cas }, false, false); });
            shotQueue.Enqueue(delegate { OpenFiles(new string[] { enforce }, false, false); });
            shotQueue.Enqueue(delegate { NavGo(false); });
        }

        List<LogLocation> DemoLocations()
        {
            List<LogLocation> locs = new List<LogLocation>();
            string[][] rows =
            {
                new string[] { "Client logs", LogLocations.ConfigMgr, @"C:\Windows\CCM\Logs", "42" },
                new string[] { "Intune Management Extension", LogLocations.Intune, @"C:\ProgramData\Microsoft\IntuneManagementExtension\Logs", "9" },
                new string[] { "Windows Update", LogLocations.Windows, @"C:\Windows\Logs\WindowsUpdate", "17" },
                new string[] { "Windows", LogLocations.Windows, @"C:\Windows\Logs\CBS", "6" }
            };
            foreach (string[] r in rows)
            {
                LogLocation l = new LogLocation();
                l.Name = r[0];
                l.Group = r[1];
                l.Path = r[2];
                l.FileCount = int.Parse(r[3]);
                l.Newest = DateTime.Now.AddMinutes(-12);
                l.Glyph = Glyph.Folder;
                locs.Add(l);
            }
            LogLocation mine = new LogLocation();
            mine.Name = "Collected logs";
            mine.Group = LogLocations.Yours;
            mine.Path = @"\\fileserver\logs\CONTOSO-LT0417";
            mine.FileCount = 24;
            mine.Newest = DateTime.Now.AddHours(-3);
            mine.Glyph = Glyph.Folder;
            mine.Custom = true;
            locs.Add(mine);
            return locs;
        }

        void ShowShotPage(Form f, bool fit)
        {
            if (f == null) return;
            shotPage = f;
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(Left + Dpi.S(40), Top + Dpi.S(30));
            if (fit && !Program.Options.Size.IsEmpty) f.Size = new Size(Math.Min(f.Width, Width - Dpi.S(80)), Math.Min(f.Height, Height - Dpi.S(60)));
            f.Show(this);
            Application.DoEvents();
        }

        // The start page of a screenshot lists made-up recent entries and folders, so it shows what a used page looks like.
        void ShotWelcomeData()
        {
            try
            {
                string[] all = Demo.Write();
                string[] cm = Demo.CmPaths(all);
                List<RecentEntry> list = new List<RecentEntry>();
                RecentEntry merged = new RecentEntry();
                merged.Paths = cm;
                merged.Pinned = true;
                RecentEntry cbs = new RecentEntry();
                cbs.Paths = new string[] { Array.Find(all, delegate (string p) { return Path.GetFileName(p) == "CBS.log"; }) };
                RecentEntry app = new RecentEntry();
                app.Paths = new string[] { "evt:Application" };
                RecentEntry one = new RecentEntry();
                one.Paths = new string[] { cm[0] };
                list.Add(one);
                list.Add(merged);
                list.Add(cbs);
                list.Add(app);
                start.SetRecent(list);
            }
            catch (Exception) { }
            List<LogLocation> locs = DemoLocations();
            start.SetLocations(locs);
        }

        void ShotSave()
        {
            string path = Program.Options.Shot;
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            Refresh();
            Rectangle area = shotPage != null ? shotPage.RectangleToScreen(shotPage.ClientRectangle) : RectangleToScreen(ClientRectangle);
            using (Bitmap bmp = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height)))
            {
                // The picture is taken from the screen: DrawToBitmap does not draw the controls of a form on Mono.
                try
                {
                    using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
                }
                catch (Exception)
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
