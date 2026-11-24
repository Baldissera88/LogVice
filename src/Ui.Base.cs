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

    // The colours of the window, made from a Palette (Palettes.cs).
    public sealed class Theme
    {
        public bool IsDark, MonoNumbers;
        public float Corners = 1f;
        public string Name = "";
        public Color Bg, Card, CardAlt, Stroke, Divider, Text, Text2, Text3;
        public Color Accent, AccentHover, OnAccent, AccentSoft;
        public Color Hover, Pressed, Field, FieldFocus, FieldStroke, FieldBottom;
        public Color ErrBg, ErrFg, WarnBg, WarnFg, OkFg, Mark, OnMark, Thumb;
        public Color Zebra, Header, Glow;

        public static Theme Light() { return From(Palettes.Light()); }
        public static Theme Dark() { return From(Palettes.Dark()); }
        public static Theme Vice() { return From(Palettes.Vice()); }
        public static Theme Terminal() { return From(Palettes.Terminal()); }
        public static Theme Amber() { return From(Palettes.Amber()); }
        public static Theme Synthwave() { return From(Palettes.Synthwave()); }

        public static Theme From(Palette p)
        {
            Theme t = new Theme();
            t.Name = p.Name;
            t.IsDark = p.IsDark;
            t.Corners = p.Corners;
            t.MonoNumbers = p.MonoNumbers;
            t.Bg = Hex(p.Bg);
            t.Card = Hex(p.Card);
            t.CardAlt = Hex(p.CardAlt);
            t.Stroke = Hex(p.Stroke);
            t.Divider = Hex(p.Divider);
            t.Text = Hex(p.Text);
            t.Text2 = Hex(p.Text2);
            t.Text3 = Hex(p.Text3);
            t.Accent = Hex(p.Accent);
            t.AccentHover = Hex(p.AccentHover);
            t.OnAccent = Hex(p.OnAccent);
            t.AccentSoft = Hex(p.AccentSoft);
            t.Hover = Tint(p.HoverTint, p.HoverAlpha);
            t.Pressed = Tint(p.HoverTint, p.PressedAlpha);
            t.Field = Hex(p.Field);
            t.FieldFocus = Hex(p.FieldFocus);
            t.FieldStroke = Hex(p.FieldStroke);
            t.FieldBottom = Hex(p.FieldBottom);
            t.ErrBg = Hex(p.ErrBg);
            t.ErrFg = Hex(p.ErrFg);
            t.WarnBg = Hex(p.WarnBg);
            t.WarnFg = Hex(p.WarnFg);
            t.OkFg = Hex(p.OkFg);
            t.Mark = Hex(p.Mark);
            t.OnMark = Hex(p.OnMark);
            t.Thumb = Tint(p.ThumbTint, p.ThumbAlpha);
            t.Zebra = Hex(p.Zebra);
            t.Header = Hex(p.Header);
            t.Glow = Hex(p.Glow);
            return t;
        }

        // System, or a name that is not known, follows the Windows app mode.
        public static Theme Named(string name)
        {
            Palette p = Palettes.Find(name);
            if (p != null) return From(p);
            return SystemPrefersDark() ? Dark() : Light();
        }

        // A theme name given on the command line (--theme). It is used in this run and never saved.
        public static string Override;

        // The theme name saved in Settings, or Vice when none is saved yet or the name is not known.
        public static string SavedName()
        {
            return Palettes.SavedName(!string.IsNullOrEmpty(Override) ? Override : Settings.Get("Theme"));
        }

        // The one place that turns the saved choice into a theme.
        public static Theme Chosen()
        {
            return Named(SavedName());
        }

        // A first start of a version that has themes: someone who used LogVice before and never picked a theme
        // followed the Windows app mode, so they keep that. A new install gets Vice.
        public static void KeepEarlierChoice()
        {
            if (!string.IsNullOrEmpty(Settings.Get("Theme"))) return;
            bool used = Settings.Get("LastVersion") != null || Settings.Get("UpdateChecked") != null || Settings.Get("Window") != null || Settings.Recent().Count > 0;
            if (used) Settings.Set("Theme", "System");
        }

        // How round the corners of the theme in use are. Set when a theme is put on a window, read by Draw.
        public static float CornerScale = 1f;

        public void Activate()
        {
            CornerScale = Corners;
        }

        // A number, time or duration in the theme's font for numbers: the monospace font in Terminal and Amber.
        public Font Num(Font normal)
        {
            if (!MonoNumbers || normal == null) return normal;
            if (normal == Fonts.Title) return Fonts.MonoTitle;
            if (normal == Fonts.Small || normal == Fonts.SmallSemibold) return Fonts.MonoSmall;
            if (normal == Fonts.UiSemibold) return Fonts.MonoSemibold;
            if (normal == Fonts.Ui) return Fonts.Mono;
            return normal;
        }

        public Color OnSeverity
        {
            get { return IsDark ? Color.Black : Color.White; }
        }

        public static Color Hex(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        static Color Tint(int rgb, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
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
        public static Font Ui, UiSemibold, Small, SmallSemibold, Title, Hero, Mono, MonoSmall, MonoSemibold, MonoTitle, Detail, Icons, IconsSmall;
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
            string icons = Pick(null, "Segoe Fluent Icons", "Segoe MDL2 Assets");
            Ui = new Font(ui, 9.75f);
            UiSemibold = semi != null ? new Font(semi, 9.75f) : new Font(ui, 9.75f, FontStyle.Bold);
            Small = new Font(ui, 8.25f);
            SmallSemibold = semi != null ? new Font(semi, 8.25f) : new Font(ui, 8.25f, FontStyle.Bold);
            Title = disp != null ? new Font(disp, 14f) : new Font(ui, 14f, FontStyle.Bold);
            Hero = disp != null ? new Font(disp, 21f) : new Font(ui, 21f, FontStyle.Bold);
            monoName = mono;
            MonoSmall = mono != null ? new Font(mono, 9f) : new Font(FontFamily.GenericMonospace, 9f);
            MonoSemibold = new Font(MonoSmall.FontFamily, 9.25f, FontStyle.Bold);
            MonoTitle = new Font(MonoSmall.FontFamily, 13f, FontStyle.Bold);
            Icons = icons != null ? new Font(icons, 10.5f) : null;
            IconsSmall = icons != null ? new Font(icons, 7.5f) : null;
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

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        public static void RefreshFileIcons()
        {
            try { SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero); } catch (Exception) { }
        }

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

        // Set for a screenshot run (--shot): nothing is written to the registry.
        public static bool ReadOnly;

        public static void Migrate()
        {
            if (ReadOnly) return;
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
            if (ReadOnly) return;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (k != null) k.SetValue(name, value ?? "", RegistryValueKind.String);
                }
            }
            catch (Exception) { }
        }

        // Removes a saved value, so its default applies again.
        public static void Remove(string name)
        {
            if (ReadOnly) return;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath, true))
                {
                    if (k != null) k.DeleteValue(name, false);
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
            if (ReadOnly) return;
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
            if (ReadOnly) return;
            try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false); } catch (Exception) { }
        }

        public static string Location
        {
            get { return @"HKEY_CURRENT_USER\" + KeyPath; }
        }

        // The paths of the recent entries, the newest first.
        public static List<string[]> Recent()
        {
            List<string[]> list = new List<string[]>();
            foreach (RecentEntry e in RecentEntries()) list.Add(e.Paths);
            return list;
        }

        // The recent entries as stored, the newest first, with their pins.
        public static List<RecentEntry> RecentEntries()
        {
            List<string> raw = new List<string>();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    string[] v = k == null ? null : k.GetValue("Recent") as string[];
                    if (v != null) raw.AddRange(v);
                }
            }
            catch (Exception) { }
            return RecentList.ParseAll(raw);
        }

        public static void AddRecent(string[] paths)
        {
            SaveRecent(RecentList.Add(RecentEntries(), paths));
        }

        public static void PinRecent(string[] paths, bool pinned)
        {
            SaveRecent(RecentList.SetPinned(RecentEntries(), paths, pinned));
        }

        public static void RemoveRecent(string[] paths)
        {
            SaveRecent(RecentList.Remove(RecentEntries(), paths));
        }

        // Clears the list except for the pinned entries.
        public static void ClearRecent()
        {
            List<RecentEntry> keep = new List<RecentEntry>();
            foreach (RecentEntry e in RecentEntries()) if (e.Pinned) keep.Add(e);
            SaveRecent(keep);
        }

        static void SaveRecent(List<RecentEntry> entries)
        {
            if (ReadOnly) return;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (k != null) k.SetValue("Recent", RecentList.FormatAll(entries).ToArray(), RegistryValueKind.MultiString);
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
            string dir = Demo.Show(Path.GetDirectoryName(paths[0]) ?? "");
            if (paths.Length == 1 && Session.IsSessionFile(paths[0]))
            {
                detail = "Session \u00b7 " + dir;
                return Path.GetFileNameWithoutExtension(paths[0]);
            }
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
        Gear, Back, Computer, File, Server, Plus, Info, Browse, Palette, Text, Rows, Warning, Home, User, Highlight, Shield, Events,
        Forward, Help, Pin, Keyboard, Code, More, Follow, Github, Save, Session
    }

    public static class Draw
    {
        // The radius is scaled by the corners of the theme in use: 0 draws square corners.
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            rad *= Theme.CornerScale;
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
                    g.DrawLine(p, r.X + rad * Theme.CornerScale, r.Bottom - Dpi.F(0.75f), r.Right - rad * Theme.CornerScale, r.Bottom - Dpi.F(0.75f));
            }
            g.Restore(st);
        }

        // A card: the theme's card colour with a thin line round it.
        public static void Card(Graphics g, Rectangle r, Theme t)
        {
            RectangleF f = new RectangleF(r.X, r.Y, r.Width, r.Height);
            Fill(g, f, Dpi.F(6), t.Card);
            Stroke(g, f, Dpi.F(6), t.Divider, 1f);
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
            if ((flags & TextFormatFlags.EndEllipsis) != 0 && (flags & TextFormatFlags.SingleLine) != 0) s = Ellipsis(s, f, r.Width);
            TextRenderer.DrawText(g, s, f, r, c, flags);
        }

        // The text cut with an ellipsis so that it fits in the width. The system only does this on some platforms, and a
        // narrow column of the log list must never end in a hard cut.
        public static string Ellipsis(string s, Font f, int width)
        {
            if (string.IsNullOrEmpty(s) || width <= 0) return s;
            // A text this short cannot be wider than the room (no character is wider than the font is high): no need to measure it.
            if (s.Length * f.Height <= width) return s;
            return Ellipsis(s, width, delegate (string t) { return Measure(t, f).Width; });
        }

        // measure gives the width of a text. The text is cut at the longest start that fits with the ellipsis after it.
        public static string Ellipsis(string s, int width, Func<string, int> measure)
        {
            if (string.IsNullOrEmpty(s) || width <= 0) return s;
            if (measure(s) <= width) return s;
            const string dots = "\u2026";
            if (measure(dots) > width) return "";
            int lo = 0, hi = s.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(s.Substring(0, mid).TrimEnd() + dots) <= width) lo = mid;
                else hi = mid - 1;
            }
            return s.Substring(0, lo).TrimEnd() + dots;
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
            const int ss = 4;
            int big = size * ss;
            using (Bitmap hi = new Bitmap(big, big, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(hi))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    float k = big / 1024f;
                    float min = size <= 16 ? 1.3f : size <= 20 ? 1.4f : size <= 24 ? 1.5f : size <= 32 ? 1.6f : size <= 40 ? 1.8f : size <= 48 ? 1.9f : size <= 64 ? 2.3f : 0f;
                    float w = Math.Max(36f, min * 1024f / size);
                    float lens = size > 24 ? 1f : 1.12f;
                    using (GraphicsPath tile = Round(new RectangleF(20 * k, 20 * k, 984 * k, 984 * k), 150 * k))
                    using (SolidBrush navy = new SolidBrush(Theme.Hex(0x011133)))
                        g.FillPath(navy, tile);
                    float[][] lines = size > 16
                        ? new float[][] { new float[] { 155, 444, 358 }, new float[] { 155, 412, 483 }, new float[] { 155, 358, 608 } }
                        : new float[][] { new float[] { 155, 444, 400 }, new float[] { 155, 380, 590 } };
                    float cx = size <= 20 ? 652 : 642, cy = size <= 20 ? 470 : 462, r = 150 * lens;
                    float hx = cx + (r + w * 0.9f) * 0.707f, hy = cy + (r + w * 0.9f) * 0.722f;
                    using (Pen cyan = new Pen(Theme.Hex(0x00EEFE), w * k))
                    using (Pen pink = new Pen(Theme.Hex(0xFF199A), w * k))
                    {
                        cyan.StartCap = cyan.EndCap = LineCap.Round;
                        foreach (float[] l in lines) g.DrawLine(cyan, (l[0] + w / 2f) * k, l[2] * k, l[1] * k, l[2] * k);
                        g.DrawEllipse(pink, (cx - r) * k, (cy - r) * k, r * 2 * k, r * 2 * k);
                        g.DrawLine(cyan, hx * k, hy * k, (hx + 118 * lens) * k, (hy + 126 * lens) * k);
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
                    case Glyph.Save:
                        g.DrawPolygon(p, new PointF[] { P(2.4f, 2.4f), P(11f, 2.4f), P(13.6f, 5f), P(13.6f, 13.6f), P(2.4f, 13.6f) });
                        g.DrawLines(p, new PointF[] { P(5f, 2.6f), P(5f, 5.6f), P(10f, 5.6f), P(10f, 2.6f) });
                        g.DrawRectangle(p, 4.8f, 9f, 6.4f, 4.4f);
                        break;
                    case Glyph.Session:
                        g.DrawLines(p, new PointF[] { P(9.6f, 1.6f), P(3.4f, 1.6f), P(3.4f, 14.4f), P(12.6f, 14.4f), P(12.6f, 4.6f), P(9.6f, 1.6f), P(9.6f, 4.6f), P(12.6f, 4.6f) });
                        g.DrawLine(p, 6f, 8f, 10f, 8f);
                        g.DrawLine(p, 6f, 10.6f, 10f, 10.6f);
                        break;
                    case Glyph.Forward:
                        g.DrawLine(p, 2.5f, 8f, 13.2f, 8f);
                        g.DrawLines(p, new PointF[] { P(8.6f, 3.4f), P(13.2f, 8f), P(8.6f, 12.6f) });
                        break;
                    case Glyph.Help:
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.DrawArc(p, 5.8f, 4.4f, 4.4f, 4.2f, 180, 235);
                        g.DrawLine(p, 8f, 8.6f, 8f, 9.6f);
                        g.FillEllipse(b, 7.2f, 10.6f, 1.6f, 1.6f);
                        break;
                    case Glyph.Pin:
                        g.DrawLines(p, new PointF[] { P(5.4f, 2.2f), P(10.6f, 2.2f), P(9.8f, 7.4f), P(12f, 9.4f), P(4f, 9.4f), P(6.2f, 7.4f), P(5.4f, 2.2f) });
                        g.DrawLine(p, 8f, 9.6f, 8f, 14.4f);
                        break;
                    case Glyph.Keyboard:
                        using (GraphicsPath gp = Round(new RectangleF(1.4f, 3.6f, 13.2f, 8.8f), 1.6f)) g.DrawPath(p, gp);
                        g.FillEllipse(b, 3.6f, 5.8f, 1.3f, 1.3f);
                        g.FillEllipse(b, 6.1f, 5.8f, 1.3f, 1.3f);
                        g.FillEllipse(b, 8.6f, 5.8f, 1.3f, 1.3f);
                        g.FillEllipse(b, 11.1f, 5.8f, 1.3f, 1.3f);
                        g.DrawLine(p, 4.6f, 9.6f, 11.4f, 9.6f);
                        break;
                    case Glyph.Code:
                        g.DrawLines(p, new PointF[] { P(5.4f, 4f), P(1.6f, 8f), P(5.4f, 12f) });
                        g.DrawLines(p, new PointF[] { P(10.6f, 4f), P(14.4f, 8f), P(10.6f, 12f) });
                        g.DrawLine(p, 9.2f, 2.6f, 6.8f, 13.4f);
                        break;
                    case Glyph.More:
                        g.FillEllipse(b, 2.2f, 6.9f, 2.2f, 2.2f);
                        g.FillEllipse(b, 6.9f, 6.9f, 2.2f, 2.2f);
                        g.FillEllipse(b, 11.6f, 6.9f, 2.2f, 2.2f);
                        break;
                    case Glyph.Follow:
                        g.DrawLines(p, new PointF[] { P(3.6f, 4f), P(8f, 4f), P(8f, 12f), P(12.4f, 12f) });
                        g.DrawEllipse(p, 1.4f, 2.2f, 3.6f, 3.6f);
                        g.DrawEllipse(p, 11f, 10.2f, 3.6f, 3.6f);
                        break;
                    case Glyph.Github:
                        g.DrawEllipse(p, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.DrawLines(p, new PointF[] { P(6.2f, 13.6f), P(6.2f, 10.6f), P(9.8f, 10.6f), P(9.8f, 13.6f) });
                        g.DrawLines(p, new PointF[] { P(4.6f, 9f), P(6.2f, 10.6f) });
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
