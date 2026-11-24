namespace LogVice
{
    using System;
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
            if (Program.Options.Page == "settings") OpenSettings(0);
            // Keep the pointer out of the picture, so nothing shows as hovered.
            try { Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 2, Screen.PrimaryScreen.Bounds.Bottom - 2); } catch (Exception) { }
        }

        void ShotSave()
        {
            string path = Program.Options.Shot;
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            Refresh();
            Rectangle area = RectangleToScreen(ClientRectangle);
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
