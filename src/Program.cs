namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    public static class Program
    {
        public const string BuildDate = "6 November 2026";
        public const string Developer = "Daniel L. Baldissera";
        public static string ScriptPath;

        public static bool IsAdmin
        {
            get
            {
                try
                {
                    return new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
                catch (Exception) { return false; }
            }
        }

        public static bool RestartElevated(IWin32Window owner, string[] files)
        {
            System.Text.StringBuilder args = new System.Text.StringBuilder();
            string exe;
            if (!string.IsNullOrEmpty(ScriptPath))
            {
                exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
                args.Append("-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File \"").Append(ScriptPath).Append('"');
            }
            else exe = Application.ExecutablePath;
            if (files != null)
                foreach (string f in files)
                    if (!string.IsNullOrEmpty(f)) args.Append(" \"").Append(f.TrimEnd('\\')).Append('"');
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe, args.ToString().Trim());
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1223)
                    MessageBox.Show(owner, "LogVice could not start as administrator: " + ex.Message, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "LogVice could not start as administrator: " + ex.Message, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        public static string Version
        {
            get
            {
                object[] a = typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
                return a.Length > 0 ? ((System.Reflection.AssemblyInformationalVersionAttribute)a[0]).InformationalVersion : typeof(Program).Assembly.GetName().Version.ToString();
            }
        }

        public static string Location
        {
            get
            {
                try
                {
                    string p = typeof(Program).Assembly.Location;
                    return string.IsNullOrEmpty(p) ? Application.ExecutablePath : p;
                }
                catch (Exception) { return Application.ExecutablePath; }
            }
        }

        static Icon icon;
        static bool iconLoaded;
        static bool initialized;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                MessageBox.Show(ex != null ? ex.ToString() : "Unknown error", "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            if (args != null && args.Length > 0 && args[0] == "--apply-update")
            {
                try { Application.EnableVisualStyles(); } catch (InvalidOperationException) { }
                Environment.ExitCode = Updates.Apply(args);
                return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Updates.CleanUp(); });
            Run(ExpandArgs(args));
        }

        public static Icon AppIcon()
        {
            if (iconLoaded) return icon;
            iconLoaded = true;
            try
            {
                using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("LogVice.ico"))
                {
                    if (s != null) icon = new Icon(s);
                }
            }
            catch (Exception) { }
            return icon;
        }

        static string[] ExpandArgs(string[] args)
        {
            List<string> list = new List<string>();
            foreach (string a in args ?? new string[0])
            {
                if (string.IsNullOrEmpty(a)) continue;
                string arg = a.Trim('"');
                if (arg.IndexOf('*') < 0 && arg.IndexOf('?') < 0)
                {
                    list.Add(arg);
                    continue;
                }
                try
                {
                    string dir = Path.GetDirectoryName(arg);
                    if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
                    string[] found = Directory.GetFiles(dir, Path.GetFileName(arg));
                    Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                    list.AddRange(found);
                }
                catch (Exception) { }
            }
            return list.ToArray();
        }

        public static void Run(string[] paths)
        {
            if (!initialized)
            {
                initialized = true;
                Settings.Migrate();
                try { NativeMethods.SetProcessDPIAware(); } catch (Exception) { }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                }
                catch (InvalidOperationException) { }
                try
                {
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Dpi.Scale = Math.Max(1f, g.DpiX / 96f);
                }
                catch (Exception) { }
                Fonts.Init();
                Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    MessageBox.Show(e.Exception.Message, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
            }
            using (MainForm f = new MainForm(paths))
            {
                Application.Run(f);
            }
        }
    }
}
