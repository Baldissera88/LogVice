namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    public static class Program
    {
        public const string BuildDate = "24 November 2026";
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
            Run(StartOptions.Parse(args));
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

        // What was given on the command line for this run.
        public static StartOptions Options = new StartOptions();

        public static void Run(StartOptions options)
        {
            Options = options ?? new StartOptions();
            if (!initialized)
            {
                initialized = true;
                Settings.ReadOnly = Options.Shot != null;
                Settings.Migrate();
                Theme.Override = Options.Theme;
                if (Options.Shot == null) Theme.KeepEarlierChoice();
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
                if (Options.Scale != null) ShotScale.Apply(Options.Scale);
                Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    MessageBox.Show(e.Exception.Message, "LogVice", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
            }
            using (MainForm f = new MainForm(Options.Paths))
            {
                Application.Run(f);
            }
        }
    }

    // The command line of one run: the files to open, where to land in them, and the options for demos and screenshots.
    public sealed class StartOptions
    {
        public string[] Paths = new string[0];
        public bool Demo;
        // --shot <png>: draw the window into a PNG and exit. Nothing is saved in this run.
        public string Shot;
        // --theme <name>: the theme for this run only.
        public string Theme;
        // --page <name>, --select <text>, --scale <percent>: what a screenshot shows.
        public string Page, Select, Scale;
        public Size Size;
        // --line <n>: the line of the first file to land on. 0 when not given.
        public int Line;
        // --time "<yyyy-MM-dd HH:mm:ss[.fff]>": land on the first row at or after this time. NoTime when not given.
        public long Time = Store.NoTime;
        // --find <text> and --filter <text>: search from the row it landed on, or only show the rows with this text.
        public string Find, Filter;
        // --errors: only errors and warnings.
        public bool Errors;
        // Options that were ignored, each with the reason. The window says so in its status bar.
        public readonly List<string> Ignored = new List<string>();

        static readonly string[] WithValue = { "--shot", "--theme", "--page", "--select", "--scale", "--size", "--line", "--time", "--find", "--filter" };
        static readonly string[] TimeFormats =
        {
            "yyyy-MM-dd HH:mm:ss.fffffff", "yyyy-MM-dd HH:mm:ss.ffffff", "yyyy-MM-dd HH:mm:ss.fffff", "yyyy-MM-dd HH:mm:ss.ffff",
            "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.ff", "yyyy-MM-dd HH:mm:ss.f", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss.fffffff", "yyyy-MM-dd'T'HH:mm:ss.ffffff", "yyyy-MM-dd'T'HH:mm:ss.fffff", "yyyy-MM-dd'T'HH:mm:ss.ffff",
            "yyyy-MM-dd'T'HH:mm:ss.fff", "yyyy-MM-dd'T'HH:mm:ss.ff", "yyyy-MM-dd'T'HH:mm:ss.f", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm"
        };

        // The ticks of a time as written for --time, or NoTime when it cannot be read.
        public static long ParseTime(string text)
        {
            DateTime d;
            if (text != null && DateTime.TryParseExact(text.Trim(), TimeFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return d.Ticks;
            return Store.NoTime;
        }

        // A sentence for the status bar about the ignored options, or null.
        public string IgnoredNote()
        {
            if (Ignored.Count == 0) return null;
            return (Ignored.Count == 1 ? "Ignored this command line option: " : "Ignored these command line options: ") + string.Join("; ", Ignored.ToArray());
        }

        // The path of a saved session, when one was given.
        public string SessionPath
        {
            get
            {
                foreach (string p in Paths) if (LogVice.Session.IsSessionFile(p)) return p;
                return null;
            }
        }

        public static StartOptions Parse(string[] args)
        {
            StartOptions o = new StartOptions();
            List<string> rest = new List<string>();
            args = Split(args ?? new string[0]);
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i] ?? "";
                string low = a.ToLowerInvariant();
                bool more = i + 1 < args.Length;
                if (low == "--demo") o.Demo = true;
                else if (low == "--errors") o.Errors = true;
                else if (low == "--shot" && more) o.Shot = args[++i];
                else if (low == "--theme" && more) o.Theme = args[++i];
                else if (low == "--page" && more) o.Page = args[++i];
                else if (low == "--select" && more) o.Select = args[++i];
                else if (low == "--scale" && more) o.Scale = args[++i];
                else if (low == "--find" && more) o.Find = args[++i];
                else if (low == "--filter" && more) o.Filter = args[++i];
                else if (low == "--size" && more)
                {
                    string[] wh = args[++i].ToLowerInvariant().Split('x');
                    int w, h;
                    if (wh.Length == 2 && int.TryParse(wh[0], out w) && int.TryParse(wh[1], out h) && w >= 200 && h >= 200) o.Size = new Size(w, h);
                }
                else if (low == "--line" && more)
                {
                    string v = args[++i];
                    int n;
                    if (int.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n) && n > 0) o.Line = n;
                    else o.Ignored.Add("--line " + v + " (not a line number)");
                }
                else if (low == "--time" && more)
                {
                    string v = args[++i];
                    o.Time = ParseTime(v);
                    if (o.Time == Store.NoTime) o.Ignored.Add("--time \"" + v + "\" (use yyyy-MM-dd HH:mm:ss)");
                }
                else if (low.StartsWith("--", StringComparison.Ordinal) && low.Length > 2)
                {
                    if (Array.IndexOf(WithValue, low) >= 0) o.Ignored.Add(a + " (needs a value)");
                    else o.Ignored.Add(a);
                }
                else rest.Add(a);
            }
            o.Paths = ExpandArgs(rest.ToArray());
            return o;
        }

        // --line=5 is the same as --line 5.
        static string[] Split(string[] args)
        {
            List<string> list = new List<string>();
            foreach (string a in args)
            {
                int eq = a == null || !a.StartsWith("--", StringComparison.Ordinal) ? -1 : a.IndexOf('=');
                if (eq > 2 && Array.IndexOf(WithValue, a.Substring(0, eq).ToLowerInvariant()) >= 0)
                {
                    list.Add(a.Substring(0, eq));
                    list.Add(a.Substring(eq + 1));
                }
                else list.Add(a);
            }
            return list.ToArray();
        }

        public static string[] ExpandArgs(string[] args)
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
    }
}
