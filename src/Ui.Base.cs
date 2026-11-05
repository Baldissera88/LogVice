namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Text;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;
    using Microsoft.Win32;

    public static class Dpi
    {
        public static float Scale = 1f;

        public static int S(float v)
        {
            return (int)Math.Round(v * Scale);
        }

        public static float F(float v)
        {
            return v * Scale;
        }
    }

    public sealed class Theme
    {
        public bool IsDark;
        public string Name = "";
        public Color Bg, Card, CardAlt, Stroke, Divider, Text, Text2, Text3;
        public Color Accent, AccentHover, OnAccent, AccentSoft;
        public Color Hover, Pressed, Field, FieldFocus, FieldStroke, FieldBottom;
        public Color ErrBg, ErrFg, WarnBg, WarnFg, OkFg, Mark, OnMark, Thumb;
        public Color Zebra, Header, Glow;

        public static Theme Light()
        {
            Theme t = new Theme();
            t.Name = "Light";
            t.IsDark = false;
            t.Bg = Hex(0xEEF1F6);
            t.Card = Hex(0xFFFFFF);
            t.CardAlt = Hex(0xF5F7FA);
            t.Stroke = Hex(0xDCE1E8);
            t.Divider = Hex(0xE6E9EF);
            t.Text = Hex(0x111418);
            t.Text2 = Hex(0x454B54);
            t.Text3 = Hex(0x6E7580);
            t.Accent = Hex(0x005FB8);
            t.AccentHover = Mix(t.Accent, Color.White, 0.12f);
            t.OnAccent = Color.White;
            t.AccentSoft = Mix(t.Bg, t.Accent, 0.12f);
            t.Hover = Color.FromArgb(14, 0, 0, 0);
            t.Pressed = Color.FromArgb(8, 0, 0, 0);
            t.Field = Hex(0xFFFFFF);
            t.FieldFocus = Hex(0xFFFFFF);
            t.FieldStroke = Hex(0xE0E0E0);
            t.FieldBottom = Hex(0x8A8A8A);
            t.ErrBg = Hex(0xFDE3E5);
            t.ErrFg = Hex(0xC42B1C);
            t.WarnBg = Hex(0xFFF1C7);
            t.WarnFg = Hex(0x9D5D00);
            t.OkFg = Hex(0x0F7B0F);
            t.Mark = Hex(0xFFE45C);
            t.OnMark = Hex(0x1B1B1B);
            t.Thumb = Color.FromArgb(120, 0, 0, 0);
            t.Zebra = Hex(0xF7F9FC);
            t.Header = Hex(0xF7F9FC);
            t.Glow = Mix(t.Card, t.Accent, 0.10f);
            return t;
        }

        public static Theme Dark()
        {
            Theme t = new Theme();
            t.Name = "Dark";
            t.IsDark = true;
            t.Bg = Hex(0x191B20);
            t.Card = Hex(0x24272D);
            t.CardAlt = Hex(0x2C3037);
            t.Stroke = Hex(0x131519);
            t.Divider = Hex(0x363A42);
            t.Text = Hex(0xF5F7FA);
            t.Text2 = Hex(0xD3D7DE);
            t.Text3 = Hex(0xA2A8B2);
            t.Accent = Hex(0x60CDFF);
            t.AccentHover = Mix(t.Accent, Color.Black, 0.1f);
            t.OnAccent = Hex(0x000000);
            t.AccentSoft = Mix(t.Bg, t.Accent, 0.16f);
            t.Hover = Color.FromArgb(16, 255, 255, 255);
            t.Pressed = Color.FromArgb(10, 255, 255, 255);
            t.Field = Hex(0x2A2E35);
            t.FieldFocus = Hex(0x1D2025);
            t.FieldStroke = Hex(0x3A3F48);
            t.FieldBottom = Hex(0x9A9A9A);
            t.ErrBg = Hex(0x47292B);
            t.ErrFg = Hex(0xFF99A4);
            t.WarnBg = Hex(0x45391D);
            t.WarnFg = Hex(0xFCE100);
            t.OkFg = Hex(0x6CCB5F);
            t.Mark = Hex(0xFCE100);
            t.OnMark = Hex(0x000000);
            t.Thumb = Color.FromArgb(150, 255, 255, 255);
            t.Zebra = Hex(0x282B32);
            t.Header = Hex(0x292C33);
            t.Glow = Mix(t.Card, t.Accent, 0.12f);
            return t;
        }

        public static Theme Vice()
        {
            Theme t = new Theme();
            t.Name = "Vice";
            t.IsDark = true;
            t.Bg = Hex(0x120D26);
            t.Card = Hex(0x1C1535);
            t.CardAlt = Hex(0x251C45);
            t.Stroke = Hex(0x0B0819);
            t.Divider = Hex(0x352A60);
            t.Text = Hex(0xFFF5FD);
            t.Text2 = Hex(0xE6D5F7);
            t.Text3 = Hex(0xB39CD6);
            t.Accent = Hex(0xFF4FB4);
            t.AccentHover = Mix(t.Accent, Color.White, 0.12f);
            t.OnAccent = Hex(0x1A0718);
            t.AccentSoft = Mix(t.Bg, t.Accent, 0.18f);
            t.Hover = Color.FromArgb(22, 255, 120, 220);
            t.Pressed = Color.FromArgb(12, 255, 120, 220);
            t.Field = Hex(0x211A3F);
            t.FieldFocus = Hex(0x170F2E);
            t.FieldStroke = Hex(0x3D3070);
            t.FieldBottom = Hex(0x2EE6D6);
            t.ErrBg = Hex(0x4A1638);
            t.ErrFg = Hex(0xFF6FA8);
            t.WarnBg = Hex(0x4A2C1F);
            t.WarnFg = Hex(0xFFB36B);
            t.OkFg = Hex(0x2EE6D6);
            t.Mark = Hex(0x2EE6D6);
            t.OnMark = Hex(0x07131A);
            t.Thumb = Color.FromArgb(170, 255, 120, 220);
            t.Zebra = Hex(0x20183D);
            t.Header = Hex(0x221A42);
            t.Glow = Mix(t.Card, t.Accent, 0.38f);
            return t;
        }

        public Color OnSeverity
        {
            get { return IsDark ? Color.Black : Color.White; }
        }

        public static Color Hex(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(255,
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Over(Color under, Color over)
        {
            return Mix(under, Color.FromArgb(255, over.R, over.G, over.B), over.A / 255f);
        }

        public static bool SystemPrefersDark()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", null);
                if (v is int) return (int)v == 0;
            }
            catch (Exception) { }
            return false;
        }
    }

    public interface IThemed
    {
        void ApplyTheme(Theme t);
    }

    public static class Fonts
    {
        public static Font Ui, UiSemibold, Small, SmallSemibold, Title, Hero, Mono, MonoSmall, Detail;
        public static int LogSize = 1;
        static Dictionary<string, bool> installed;
        static string monoName;

        public static void Init()
        {
            installed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (InstalledFontCollection c = new InstalledFontCollection())
                {
                    foreach (FontFamily f in c.Families) installed[f.Name] = true;
                }
            }
            catch (Exception) { }
            string ui = Pick(SystemFonts.MessageBoxFont.FontFamily.Name, "Segoe UI Variable Text", "Segoe UI");
            string semi = Pick(null, "Segoe UI Variable Text Semibold", "Segoe UI Semibold");
            string disp = Pick(null, "Segoe UI Variable Display Semibold", "Segoe UI Semibold");
            string mono = Pick(null, "Cascadia Mono", "Consolas", "Lucida Console");
            Ui = new Font(ui, 9.75f);
            UiSemibold = semi != null ? new Font(semi, 9.75f) : new Font(ui, 9.75f, FontStyle.Bold);
            Small = new Font(ui, 8.25f);
            SmallSemibold = semi != null ? new Font(semi, 8.25f) : new Font(ui, 8.25f, FontStyle.Bold);
            Title = disp != null ? new Font(disp, 16f) : new Font(ui, 16f, FontStyle.Bold);
            Hero = disp != null ? new Font(disp, 21f) : new Font(ui, 21f, FontStyle.Bold);
            monoName = mono;
            MonoSmall = mono != null ? new Font(mono, 9f) : new Font(FontFamily.GenericMonospace, 9f);
            SetLogSize(LogSize);
        }

        public static void SetLogSize(int level)
        {
            level = Math.Max(0, Math.Min(2, level));
            if (Mono != null && level == LogSize) return;
            LogSize = level;
            float pt = LogSize == 0 ? 9f : LogSize == 2 ? 11.25f : 9.75f;
            Mono = monoName != null ? new Font(monoName, pt) : new Font(FontFamily.GenericMonospace, pt);
            Detail = monoName != null ? new Font(monoName, pt + 0.75f) : new Font(FontFamily.GenericMonospace, pt + 0.75f);
        }

        public static string MonoName
        {
            get { return Mono != null ? Mono.Name : ""; }
        }

        public static string UiName
        {
            get { return Ui != null ? Ui.Name : ""; }
        }

        static string Pick(string fallback, params string[] names)
        {
            foreach (string n in names) if (installed.ContainsKey(n)) return n;
            return fallback;
        }
    }

    static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int FormatMessage(int dwFlags, IntPtr lpSource, uint dwMessageId, int dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr arguments);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        public static void Cue(TextBox tb, string text)
        {
            try { SendMessage(tb.Handle, 0x1501, (IntPtr)1, text); } catch (Exception) { }
        }

        static void Attr(IntPtr h, int attr, int value)
        {
            try { DwmSetWindowAttribute(h, attr, ref value, 4); } catch (Exception) { }
        }

        static int ColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        public static void StyleWindow(IntPtr h, Theme t)
        {
            if (h == IntPtr.Zero) return;
            Attr(h, 20, t.IsDark ? 1 : 0);
            Attr(h, 19, t.IsDark ? 1 : 0);
            Attr(h, 33, 2);
            Attr(h, 35, ColorRef(t.Bg));
            Attr(h, 36, ColorRef(t.Text));
        }

        public static void RoundPopup(IntPtr h)
        {
            if (h != IntPtr.Zero) Attr(h, 33, 3);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ShareInfo0
        {
            public string Name;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetShareEnum(string server, int level, out IntPtr buffer, int prefMaxLen, out int entriesRead, out int totalEntries, ref int resumeHandle);

        [DllImport("netapi32.dll")]
        static extern int NetApiBufferFree(IntPtr buffer);

        public static List<string> Shares(string server)
        {
            List<string> list = new List<string>();
            IntPtr buf = IntPtr.Zero;
            try
            {
                int read, total, resume = 0;
                if (NetShareEnum(server, 0, out buf, -1, out read, out total, ref resume) != 0) return list;
                int size = Marshal.SizeOf(typeof(ShareInfo0));
                for (int i = 0; i < read; i++)
                {
                    ShareInfo0 si = (ShareInfo0)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + (long)i * size), typeof(ShareInfo0));
                    if (!string.IsNullOrEmpty(si.Name)) list.Add(si.Name);
                }
            }
            catch (Exception) { }
            finally
            {
                try { if (buf != IntPtr.Zero) NetApiBufferFree(buf); } catch (Exception) { }
            }
            return list;
        }

        public static void ScrollTheme(Control c, bool dark)
        {
            try
            {
                if (c.IsHandleCreated) SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch (Exception) { }
        }
    }

    public static class Settings
    {
        const string KeyPath = @"Software\LogVice";
        const string OldKeyPath = @"Software\Trace Lens";

        public static void Migrate()
        {
            try
            {
                using (RegistryKey now = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (now != null) return;
                }
                using (RegistryKey old = Registry.CurrentUser.OpenSubKey(OldKeyPath))
                {
                    if (old == null) return;
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                    {
                        if (k == null) return;
                        foreach (string name in old.GetValueNames())
                        {
                            object v = old.GetValue(name);
                            if (v != null) k.SetValue(name, v, old.GetValueKind(name));
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        public static string Get(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (k == null) return null;
                    object v = k.GetValue(name);
                    return v == null ? null : v.ToString();
                }
            }
            catch (Exception) { return null; }
        }

        public static void Set(string name, string value)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (k != null) k.SetValue(name, value ?? "", RegistryValueKind.String);
                }
            }
            catch (Exception) { }
        }

        public static bool GetBool(string name, bool def)
        {
            string v = Get(name);
            if (string.IsNullOrEmpty(v)) return def;
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static void SetBool(string name, bool value)
        {
            Set(name, value ? "1" : "0");
        }

        public static int GetInt(string name, int def, int min, int max)
        {
            int v;
            if (!int.TryParse(Get(name), out v)) return def;
            return Math.Max(min, Math.Min(max, v));
        }

        public static List<string> GetList(string name)
        {
            List<string> list = new List<string>();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    string[] v = k == null ? null : k.GetValue(name) as string[];
                    if (v != null) foreach (string e in v) if (!string.IsNullOrEmpty(e)) list.Add(e);
                }
            }
            catch (Exception) { }
            return list;
        }

        public static void SetList(string name, List<string> values)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (k != null) k.SetValue(name, values.ToArray(), RegistryValueKind.MultiString);
                }
            }
            catch (Exception) { }
        }

        public static void ResetAll()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false); } catch (Exception) { }
        }

        public static string Location
        {
            get { return @"HKEY_CURRENT_USER\" + KeyPath; }
        }

        public static List<string[]> Recent()
        {
            List<string[]> list = new List<string[]>();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    string[] v = k == null ? null : k.GetValue("Recent") as string[];
                    if (v == null) return list;
                    foreach (string e in v)
                    {
                        if (string.IsNullOrEmpty(e)) continue;
                        list.Add(e.Split('|'));
                    }
                }
            }
            catch (Exception) { }
            return list;
        }

        public static void AddRecent(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            string entry = string.Join("|", paths);
            List<string> keep = new List<string>();
            keep.Add(entry);
            foreach (string[] e in Recent())
            {
                string s = string.Join("|", e);
                if (!string.Equals(s, entry, StringComparison.OrdinalIgnoreCase) && keep.Count < 10) keep.Add(s);
            }
            SaveRecent(keep);
        }

        public static void ClearRecent()
        {
            SaveRecent(new List<string>());
        }

        static void SaveRecent(List<string> entries)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (k != null) k.SetValue("Recent", entries.ToArray(), RegistryValueKind.MultiString);
                }
            }
            catch (Exception) { }
        }

        public static string RecentLabel(string[] paths, out string detail)
        {
            detail = "";
            if (paths.Length == 0) return "";
            if (EventLogs.IsChannel(paths[0]))
            {
                if (paths.Length == 1)
                {
                    detail = "Windows event log";
                    return EventLogs.DisplayName(paths[0]);
                }
                StringBuilder eb = new StringBuilder();
                for (int i = 0; i < paths.Length && i < 3; i++)
                {
                    if (i > 0) eb.Append(", ");
                    eb.Append(EventLogs.DisplayName(paths[i]));
                }
                if (paths.Length > 3) eb.Append(", +").Append(paths.Length - 3);
                detail = paths.Length + " logs merged";
                return eb.ToString();
            }
            string dir = Path.GetDirectoryName(paths[0]) ?? "";
            if (paths.Length == 1)
            {
                detail = dir;
                return Path.GetFileName(paths[0]);
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < paths.Length && i < 3; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Path.GetFileName(paths[i]));
            }
            if (paths.Length > 3) sb.Append(", +").Append(paths.Length - 3);
            detail = paths.Length + " files merged · " + dir;
            return sb.ToString();
        }
    }

    public enum Glyph
    {
        None, Open, Merge, Reload, Live, Lookup, Search, Filter, Up, Down, ErrUp, ErrDown, Copy, Moon, Sun, Close, Chevron, Recent, Folder, Stop, Check,
        Gear, Back, Computer, File, Server, Plus, Info, Browse, Palette, Text, Rows, Warning, Home, User, Highlight, Shield, Events
    }

    public static class Draw
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            if (c.A == 0) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Round(r, rad))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
            g.SmoothingMode = old;
        }

        public static void Elevation(Graphics g, RectangleF r, float rad, Theme t)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath clip = Round(r, rad))
            {
                g.SetClip(clip);
                Color shade = t.IsDark ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(22, 20, 40, 80);
                using (Pen p = new Pen(shade, Dpi.F(1.5f)))
                    g.DrawLine(p, r.X + rad, r.Bottom - Dpi.F(0.75f), r.Right - rad, r.Bottom - Dpi.F(0.75f));
            }
            g.Restore(st);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float width)
        {
            if (c.A == 0) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = width / 2f;
            using (GraphicsPath p = Round(new RectangleF(r.X + h, r.Y + h, r.Width - width, r.Height - width), rad))
            using (Pen pen = new Pen(c, width))
                g.DrawPath(pen, p);
            g.SmoothingMode = old;
        }

        public const TextFormatFlags Line = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        public const TextFormatFlags Wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;

        public static Size Measure(string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return new Size(0, f.Height);
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        public static int WrapHeight(string s, Font f, int width)
        {
            if (string.IsNullOrEmpty(s) || width <= 0) return 0;
            return TextRenderer.MeasureText(s, f, new Size(width, int.MaxValue), Wrap).Height;
        }

        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(s) || r.Width <= 0 || r.Height <= 0) return;
            TextRenderer.DrawText(g, s, f, r, c, flags);
        }

        public static void Severity(Graphics g, int sev, RectangleF r, Theme t)
        {
            if (sev < 2) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(r.Width, r.Height);
            float x = r.X + (r.Width - s) / 2f, y = r.Y + (r.Height - s) / 2f;
            Color on = t.OnSeverity;
            using (Pen p = new Pen(on, Math.Max(1.2f, s / 9f)))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                if (sev == 3)
                {
                    using (SolidBrush b = new SolidBrush(t.ErrFg)) g.FillEllipse(b, x, y, s, s);
                    float k = s * 0.3f;
                    float cx = x + s / 2f, cy = y + s / 2f;
                    g.DrawLine(p, cx - k, cy - k, cx + k, cy + k);
                    g.DrawLine(p, cx + k, cy - k, cx - k, cy + k);
                }
                else
                {
                    using (GraphicsPath tri = new GraphicsPath())
                    {
                        tri.AddPolygon(new PointF[] { new PointF(x + s / 2f, y + s * 0.04f), new PointF(x + s, y + s * 0.92f), new PointF(x, y + s * 0.92f) });
                        using (SolidBrush b = new SolidBrush(t.WarnFg)) g.FillPath(b, tri);
                        using (Pen edge = new Pen(t.WarnFg, s * 0.12f)) { edge.LineJoin = LineJoin.Round; g.DrawPath(edge, tri); }
                    }
                    float cx = x + s / 2f;
                    g.DrawLine(p, cx, y + s * 0.36f, cx, y + s * 0.6f);
                    using (SolidBrush b = new SolidBrush(on)) g.FillEllipse(b, cx - p.Width * 0.55f, y + s * 0.72f, p.Width * 1.1f, p.Width * 1.1f);
                }
            }
            g.SmoothingMode = old;
        }

        static readonly Dictionary<int, Bitmap> markCache = new Dictionary<int, Bitmap>();

        public static void AppMark(Graphics g, RectangleF r, Theme t)
        {
            int n = Math.Max(8, (int)Math.Round(Math.Min(r.Width, r.Height)));
            Bitmap bmp;
            if (!markCache.TryGetValue(n, out bmp))
            {
                if (markCache.Count > 24)
                {
                    foreach (Bitmap old in markCache.Values) old.Dispose();
                    markCache.Clear();
                }
                bmp = RenderMark(n);
                markCache[n] = bmp;
            }
            float x = r.X + (r.Width - n) / 2f, y = r.Y + (r.Height - n) / 2f;
            g.DrawImage(bmp, new RectangleF(x, y, n, n));
        }

        static Bitmap RenderMark(int size)
        {
            const int ss = 3;
            int big = size * ss;
            using (Bitmap hi = new Bitmap(big, big, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(hi))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    float n = big, inset = n * 0.03f;
                    RectangleF tile = new RectangleF(inset, inset, n - inset * 2, n - inset * 2);
                    using (GraphicsPath clip = Round(tile, n * 0.2f))
                    using (LinearGradientBrush sky = new LinearGradientBrush(new RectangleF(0, -1, n, n + 2), Theme.Hex(0x140A33), Theme.Hex(0x3D0B5E), 90f))
                    {
                        g.SetClip(clip);
                        g.FillRectangle(sky, 0, 0, n, n);
                        float yh = (float)Math.Round(n * 0.60f), cx = n / 2f, cy = n * 0.47f, rr = n * 0.31f;
                        if (size >= 48)
                        {
                            float[][] stars = { new float[] { 0.18f, 0.16f, 1.2f }, new float[] { 0.80f, 0.13f, 1f }, new float[] { 0.88f, 0.36f, 0.8f }, new float[] { 0.12f, 0.42f, 0.8f }, new float[] { 0.30f, 0.07f, 0.7f } };
                            using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                                foreach (float[] st in stars)
                                {
                                    float sr = Math.Max(ss, n * 0.008f * st[2]);
                                    g.FillEllipse(sb, st[0] * n - sr, st[1] * n - sr, sr * 2, sr * 2);
                                }
                        }
                        using (GraphicsPath gp = new GraphicsPath())
                        {
                            float gr = rr * 1.4f;
                            gp.AddEllipse(cx - gr, cy - gr, gr * 2, gr * 2);
                            using (PathGradientBrush pb = new PathGradientBrush(gp))
                            {
                                pb.CenterColor = Color.FromArgb(105, 255, 60, 160);
                                pb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 60, 160) };
                                g.FillPath(pb, gp);
                            }
                        }
                        g.SetClip(new RectangleF(0, 0, n, yh), CombineMode.Intersect);
                        using (LinearGradientBrush sun = new LinearGradientBrush(new RectangleF(0, cy - rr - 1, n, yh - (cy - rr) + 2), Theme.Hex(0xFFE45C), Theme.Hex(0xFF2D95), 90f))
                            g.FillEllipse(sun, cx - rr, cy - rr, rr * 2, rr * 2);
                        for (int i = 0; i < 3; i++)
                        {
                            float f = (i + 0.55f) / 3f;
                            float th = Math.Max(ss, n * (0.012f + 0.008f * i));
                            g.FillRectangle(sky, 0, cy + (yh - cy) * f - th / 2f, n, th);
                        }
                        g.SetClip(clip);
                        using (SolidBrush ground = new SolidBrush(Theme.Hex(0x12062B))) g.FillRectangle(ground, 0, yh, n, n - yh);
                        float ht = Math.Max(ss, n / 64f);
                        using (SolidBrush hz = new SolidBrush(Theme.Hex(0xFF9BEA))) g.FillRectangle(hz, 0, yh, n, ht);
                        float lw = Math.Max(ss, n / 90f);
                        using (SolidBrush gb = new SolidBrush(Theme.Hex(0xFF4FD8)))
                        using (Pen gpn = new Pen(Theme.Hex(0xFF4FD8), lw * 0.85f))
                        {
                            float bottom = n - inset;
                            for (int j = 0; j < 4; j++)
                            {
                                float f = (float)Math.Pow((j + 1) / 4.6, 1.5);
                                g.FillRectangle(gb, 0, yh + ht + (bottom - yh - ht) * f, n, lw);
                            }
                            float[] offs = { -1.6f, -0.9f, -0.42f, 0f, 0.42f, 0.9f, 1.6f };
                            foreach (float o in offs) g.DrawLine(gpn, cx, yh + ht, cx + o * n * 0.55f, n);
                        }
                        g.ResetClip();
                        float w = Math.Max(ss, n / 85f);
                        using (GraphicsPath rim = Round(new RectangleF(tile.X + w / 2f, tile.Y + w / 2f, tile.Width - w, tile.Height - w), n * 0.2f))
                        using (Pen rp = new Pen(Color.FromArgb(150, 255, 155, 234), w))
                            g.DrawPath(rp, rim);
                    }
                }
                Bitmap small = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics g2 = Graphics.FromImage(small))
                {
                    g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g2.CompositingQuality = CompositingQuality.HighQuality;
                    g2.Clear(Color.Transparent);
                    g2.DrawImage(hi, new Rectangle(0, 0, size, size));
                }
                return small;
            }
        }

        public static void Icon(Graphics g, Glyph glyph, RectangleF box, Color c)
        {
            if (glyph == Glyph.None) return;
            float k = Math.Min(box.Width, box.Height) / 16f;
            float ox = box.X + (box.Width - 16 * k) / 2f, oy = box.Y + (box.Height - 16 * k) / 2f;
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(k, k);
            using (Pen p = new Pen(c, 1.3f))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                switch (glyph)
                {
                    case Glyph.Open:
                    case Glyph.Folder:
                        g.DrawLines(p, new PointF[] { P(1.5f, 4.5f), P(6f, 4.5f), P(7.5f, 6f), P(14.5f, 6f), P(14.5f, 12.8f), P(1.5f, 12.8f), P(1.5f, 4.5f) });
                        if (glyph == Glyph.Open) g.DrawLine(p, 1.5f, 8.3f, 14.5f, 8.3f);
                        break;
                    case Glyph.Merge:
                        g.DrawLine(p, 1.8f, 4f, 11f, 4f);
                        g.DrawLine(p, 1.8f, 7.6f, 11f, 7.6f);
                        g.DrawLine(p, 1.8f, 11.2f, 7.2f, 11.2f);
                        g.DrawLine(p, 12.2f, 8.6f, 12.2f, 14.2f);
                        g.DrawLine(p, 9.4f, 11.4f, 15f, 11.4f);
                        break;
                    case Glyph.Reload:
                        {
                            float r = 5.6f;
                            g.DrawArc(p, 8 - r, 8 - r, 2 * r, 2 * r, -15, 290);
                            double a = (-15 + 290) * Math.PI / 180.0;
                            PointF tip = P(8 + r * (float)Math.Cos(a), 8 + r * (float)Math.Sin(a));
                            float dx = -(float)Math.Sin(a), dy = (float)Math.Cos(a);
                            float nx = -dy, ny = dx;
                            g.DrawLines(p, new PointF[] { P(tip.X - 3f * dx + 2.6f * nx, tip.Y - 3f * dy + 2.6f * ny), tip, P(tip.X - 3f * dx - 2.6f * nx, tip.Y - 3f * dy - 2.6f * ny) });
                        }
                        break;
                    case Glyph.Live:
                        g.DrawLine(p, 8f, 1.8f, 8f, 10.8f);
                        g.DrawLines(p, new PointF[] { P(4.4f, 7.4f), P(8f, 11f), P(11.6f, 7.4f) });
                        g.DrawLine(p, 2.8f, 14f, 13.2f, 14f);
                        break;
                    case Glyph.Lookup:
                        g.DrawLine(p, 6.4f, 2.2f, 5.2f, 13.8f);
                        g.DrawLine(p, 11.2f, 2.2f, 10f, 13.8f);
                        g.DrawLine(p, 2.4f, 5.8f, 13.8f, 5.8f);
                        g.DrawLine(p, 2.2f, 10.2f, 13.6f, 10.2f);
                        break;
                    case Glyph.Search:
                        g.DrawEllipse(p, 2f, 2f, 8.6f, 8.6f);
                        g.DrawLine(p, 9.5f, 9.5f, 14f, 14f);
                        break;
                    case Glyph.Filter:
                        g.DrawPolygon(p, new PointF[] { P(2f, 3f), P(14f, 3f), P(9.4f, 8.6f), P(9.4f, 13.6f), P(6.6f, 12.2f), P(6.6f, 8.6f) });
                        break;
                    case Glyph.Up:
                        g.DrawLines(p, new PointF[] { P(3.5f, 10.4f), P(8f, 5.9f), P(12.5f, 10.4f) });
                        break;
                    case Glyph.Down:
                        g.DrawLines(p, new PointF[] { P(3.5f, 5.9f), P(8f, 10.4f), P(12.5f, 5.9f) });
                        break;
                    case Glyph.Chevron:
                        g.DrawLines(p, new PointF[] { P(4.6f, 6.6f), P(8f, 10f), P(11.4f, 6.6f) });
                        break;
                    case Glyph.ErrUp:
                    case Glyph.ErrDown:
                        g.DrawEllipse(p, 1.2f, 3.8f, 8.4f, 8.4f);
                        g.DrawLine(p, 5.4f, 5.9f, 5.4f, 8.5f);
                        g.FillEllipse(b, 4.65f, 9.45f, 1.5f, 1.5f);
                        if (glyph == Glyph.ErrUp) g.DrawLines(p, new PointF[] { P(10.8f, 9.6f), P(12.9f, 7.1f), P(15f, 9.6f) });
                        else g.DrawLines(p, new PointF[] { P(10.8f, 7.1f), P(12.9f, 9.6f), P(15f, 7.1f) });
                        break;
                    case Glyph.Copy:
                        using (GraphicsPath gp = Round(new RectangleF(5.5f, 5f, 8.5f, 9.5f), 1.6f)) g.DrawPath(p, gp);
                        g.DrawLines(p, new PointF[] { P(2.5f, 11.2f), P(2.5f, 3.4f), P(3.4f, 2.5f), P(10.4f, 2.5f) });
                        break;
                    case Glyph.Moon:
                        Crescent(g, b);
                        break;
                    case Glyph.Sun:
                        g.DrawEllipse(p, 5f, 5f, 6f, 6f);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4.0;
                            float cx = (float)Math.Cos(a), cy = (float)Math.Sin(a);
                            g.DrawLine(p, 8 + 5f * cx, 8 + 5f * cy, 8 + 6.6f * cx, 8 + 6.6f * cy);
                        }
                        break;
                    case Glyph.Close:
                        g.DrawLine(p, 4.6f, 4.6f, 11.4f, 11.4f);
                        g.DrawLine(p, 11.4f, 4.6f, 4.6f, 11.4f);
                        break;
                    case Glyph.Recent:
                        g.DrawEllipse(p, 2f, 2f, 12f, 12f);
                        g.DrawLines(p, new PointF[] { P(8f, 4.8f), P(8f, 8f), P(10.4f, 9.6f) });
                        break;
                    case Glyph.Stop:
                        using (GraphicsPath gp = Round(new RectangleF(4f, 4f, 8f, 8f), 1.5f)) g.FillPath(b, gp);
                        break;
                    case Glyph.Check:
                        g.DrawLines(p, new PointF[] { P(3f, 8.4f), P(6.4f, 11.6f), P(13f, 4.6f) });
                        break;
                    case Glyph.Gear:
                        {
                            PointF[] pts = new PointF[32];
                            for (int i = 0; i < 32; i++)
                            {
                                double a = (i - 1.5) * Math.PI / 16.0;
                                float r = (i % 4) < 2 ? 6.9f : 5.3f;
                                pts[i] = P(8 + r * (float)Math.Cos(a), 8 + r * (float)Math.Sin(a));
                            }
                            g.DrawPolygon(p, pts);
                            g.DrawEllipse(p, 5.7f, 5.7f, 4.6f, 4.6f);
                        }
                        break;
                    case Glyph.Back:
                        g.DrawLine(p, 13.5f, 8f, 2.8f, 8f);
                        g.DrawLines(p, new PointF[] { P(7.4f, 3.4f), P(2.8f, 8f), P(7.4f, 12.6f) });
                        break;
                    case Glyph.Computer:
                        using (GraphicsPath gp = Round(new RectangleF(1.5f, 2.2f, 13f, 9f), 1.4f)) g.DrawPath(p, gp);
                        g.DrawLine(p, 8f, 11.2f, 8f, 13.6f);
                        g.DrawLine(p, 4.8f, 13.8f, 11.2f, 13.8f);
                        break;
                    case Glyph.File:
                        g.DrawLines(p, new PointF[] { P(9.6f, 1.6f), P(3.4f, 1.6f), P(3.4f, 14.4f), P(12.6f, 14.4f), P(12.6f, 4.6f), P(9.6f, 1.6f), P(9.6f, 4.6f), P(12.6f, 4.6f) });
                        break;
                    case Glyph.Server:
                        using (GraphicsPath gp = Round(new RectangleF(2.4f, 1.8f, 11.2f, 5.4f), 1.4f)) g.DrawPath(p, gp);
                        using (GraphicsPath gp = Round(new RectangleF(2.4f, 8.8f, 11.2f, 5.4f), 1.4f)) g.DrawPath(p, gp);
                        g.FillEllipse(b, 4.4f, 3.8f, 1.6f, 1.6f);
                        g.FillEllipse(b, 4.4f, 10.8f, 1.6f, 1.6f);
                        g.DrawLine(p, 8f, 4.6f, 11.4f, 4.6f);
                        g.DrawLine(p, 8f, 11.6f, 11.4f, 11.6f);
                        break;
                    case Glyph.Plus:
                        g.DrawLine(p, 8f, 2.8f, 8f, 13.2f);
                        g.DrawLine(p, 2.8f, 8f, 13.2f, 8f);
                        break;
                    case Glyph.Info:
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.DrawLine(p, 8f, 7.2f, 8f, 11.4f);
                        g.FillEllipse(b, 7.2f, 4.2f, 1.6f, 1.6f);
                        break;
                    case Glyph.Browse:
                        g.DrawLines(p, new PointF[] { P(7.2f, 12.8f), P(1.5f, 12.8f), P(1.5f, 3.5f), P(6f, 3.5f), P(7.5f, 5f), P(14.5f, 5f), P(14.5f, 7.6f) });
                        g.DrawEllipse(p, 8.6f, 8.2f, 4.2f, 4.2f);
                        g.DrawLine(p, 12.2f, 11.8f, 14.6f, 14.2f);
                        break;
                    case Glyph.Palette:
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.FillEllipse(b, 4.4f, 6.4f, 1.8f, 1.8f);
                        g.FillEllipse(b, 6.6f, 3.9f, 1.8f, 1.8f);
                        g.FillEllipse(b, 9.6f, 4.6f, 1.8f, 1.8f);
                        g.DrawEllipse(p, 8.2f, 8.6f, 3.2f, 3.2f);
                        break;
                    case Glyph.Rows:
                        g.DrawLine(p, 2f, 3.5f, 14f, 3.5f);
                        g.DrawLine(p, 2f, 8f, 14f, 8f);
                        g.DrawLine(p, 2f, 12.5f, 14f, 12.5f);
                        break;
                    case Glyph.Warning:
                        g.DrawPolygon(p, new PointF[] { P(8f, 1.9f), P(14.6f, 13.8f), P(1.4f, 13.8f) });
                        g.DrawLine(p, 8f, 6.2f, 8f, 9.6f);
                        g.FillEllipse(b, 7.2f, 11f, 1.6f, 1.6f);
                        break;
                    case Glyph.Shield:
                        g.DrawLines(p, new PointF[] { P(8f, 1.6f), P(13.6f, 3.6f), P(13.6f, 7.6f) });
                        g.DrawBezier(p, P(13.6f, 7.6f), P(13.6f, 11f), P(11f, 13.4f), P(8f, 14.6f));
                        g.DrawBezier(p, P(8f, 14.6f), P(5f, 13.4f), P(2.4f, 11f), P(2.4f, 7.6f));
                        g.DrawLines(p, new PointF[] { P(2.4f, 7.6f), P(2.4f, 3.6f), P(8f, 1.6f) });
                        g.DrawLine(p, 8f, 3.4f, 8f, 12.8f);
                        break;
                    case Glyph.Home:
                        g.DrawLines(p, new PointF[] { P(1.8f, 7.6f), P(8f, 2.2f), P(14.2f, 7.6f) });
                        g.DrawLines(p, new PointF[] { P(3.6f, 6.2f), P(3.6f, 13.8f), P(12.4f, 13.8f), P(12.4f, 6.2f) });
                        g.DrawLines(p, new PointF[] { P(6.6f, 13.8f), P(6.6f, 9.6f), P(9.4f, 9.6f), P(9.4f, 13.8f) });
                        break;
                    case Glyph.User:
                        g.DrawEllipse(p, 5.2f, 1.8f, 5.6f, 5.6f);
                        g.DrawArc(p, 2.2f, 9.2f, 11.6f, 9.6f, 180, 180);
                        break;
                    case Glyph.Highlight:
                        g.DrawLines(p, new PointF[] { P(9.6f, 2.2f), P(13.8f, 6.4f), P(7.4f, 12.8f), P(3.2f, 8.6f), P(9.6f, 2.2f) });
                        g.DrawLine(p, 3.2f, 8.6f, 1.8f, 13.4f);
                        g.DrawLine(p, 1.8f, 13.4f, 7.4f, 12.8f);
                        g.DrawLine(p, 1.6f, 15f, 14.4f, 15f);
                        break;
                    case Glyph.Events:
                        using (GraphicsPath gp = Round(new RectangleF(1.8f, 2f, 12.4f, 12f), 1.6f)) g.DrawPath(p, gp);
                        g.DrawLine(p, 1.8f, 5.2f, 14.2f, 5.2f);
                        g.FillEllipse(b, 4f, 7.1f, 1.7f, 1.7f);
                        g.FillEllipse(b, 4f, 10.3f, 1.7f, 1.7f);
                        g.DrawLine(p, 7.4f, 8f, 11.8f, 8f);
                        g.DrawLine(p, 7.4f, 11.2f, 11.8f, 11.2f);
                        break;
                    case Glyph.Text:
                        g.DrawLines(p, new PointF[] { P(2.6f, 13.4f), P(6.6f, 2.6f), P(10.6f, 13.4f) });
                        g.DrawLine(p, 4.1f, 9.4f, 9.1f, 9.4f);
                        g.DrawLine(p, 12.2f, 9.2f, 12.2f, 13.4f);
                        g.DrawArc(p, 10.2f, 9.2f, 4f, 4.2f, 0, 360);
                        break;
                }
            }
            g.Restore(state);
        }

        static void Crescent(Graphics g, Brush b)
        {
            float cx1 = 8f, cy1 = 8.4f, r1 = 6f;
            float cx2 = 11.4f, cy2 = 4.8f, r2 = 5f;
            double dx = cx2 - cx1, dy = cy2 - cy1, d = Math.Sqrt(dx * dx + dy * dy);
            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d), h = Math.Sqrt(Math.Max(0, r1 * r1 - a * a));
            double mx = cx1 + a * dx / d, my = cy1 + a * dy / d;
            double p1x = mx + h * dy / d, p1y = my - h * dx / d;
            double p2x = mx - h * dy / d, p2y = my + h * dx / d;
            float a1 = Deg(Math.Atan2(p1y - cy1, p1x - cx1)), a2 = Deg(Math.Atan2(p2y - cy1, p2x - cx1));
            float b1 = Deg(Math.Atan2(p1y - cy2, p1x - cx2)), b2 = Deg(Math.Atan2(p2y - cy2, p2x - cx2));
            using (GraphicsPath path = new GraphicsPath())
            {
                float sweepOuter = Norm(a1 - a2);
                path.AddArc(cx1 - r1, cy1 - r1, 2 * r1, 2 * r1, a2, sweepOuter);
                float sweepInner = Norm(b1 - b2);
                path.AddArc(cx2 - r2, cy2 - r2, 2 * r2, 2 * r2, b1, -sweepInner);
                path.CloseFigure();
                g.FillPath(b, path);
            }
        }

        static float Deg(double rad)
        {
            return (float)(rad * 180.0 / Math.PI);
        }

        static float Norm(float a)
        {
            while (a < 0) a += 360;
            while (a >= 360) a -= 360;
            return a;
        }

        static PointF P(float x, float y)
        {
            return new PointF(x, y);
        }
    }
}
