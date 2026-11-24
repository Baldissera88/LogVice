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

        // Writes the demo logs and opens the ConfigMgr and Intune ones, merged by time.
        void OpenDemo()
        {
            string[] paths;
            try { paths = Demo.CmPaths(Demo.Write()); }
            catch (Exception ex)
            {
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
            shotTimer.Stop();
            try { ShotSave(); }
            catch (Exception ex) { Console.Error.WriteLine("Screenshot failed: " + ex.Message); }
            Close();
        }

        void ShotSetup()
        {
            string sel = Program.Options.Select;
            // A demo screenshot opens on the failed install, so the detail pane shows something worth reading.
            if (sel == null && Program.Options.Demo) sel = "Unmatched exit code (1603)";
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
            // The settings page of this version is a view in the window. A later version replaces it with a dialog.
            string page = Program.Options.Page;
            if (page == "settings") OpenSettings(0);
            else if (page == "welcome") { ShotWelcomeData(); ShowWelcome(); }
            else if (page == AboutForm.AboutPart || page == AboutForm.ShortcutsPart || page == AboutForm.CommandLinePart)
            {
                shotPage = MakeAbout(page);
                shotPage.StartPosition = FormStartPosition.Manual;
                shotPage.Location = new Point(Left + Dpi.S(40), Top + Dpi.S(30));
                if (!Program.Options.Size.IsEmpty) shotPage.Size = new Size(Math.Min(Dpi.S(900), Width - Dpi.S(80)), Math.Min(Dpi.S(760), Height - Dpi.S(60)));
                shotPage.Show(this);
                Application.DoEvents();
            }
            // Keep the pointer out of the picture, so nothing shows as hovered.
            try { Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 2, Screen.PrimaryScreen.Bounds.Bottom - 2); } catch (Exception) { }
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
