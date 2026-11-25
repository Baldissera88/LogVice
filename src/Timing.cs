namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // --timing: writes how long each step of the start took to %TEMP%\LogVice-start.txt, to find out why a start is slow.
    public static class StartTiming
    {
        public static bool On;
        static readonly Stopwatch watch = new Stopwatch();
        static readonly List<string> lines = new List<string>();
        static DateTime mainAt;

        public static void Begin()
        {
            On = true;
            mainAt = DateTime.Now;
            watch.Start();
            string before = "unknown";
            try
            {
                using (Process p = Process.GetCurrentProcess())
                    before = ((long)(mainAt - p.StartTime).TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
            }
            catch (Exception) { }
            lines.Add("LogVice " + Program.Version + " started " + mainAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            lines.Add("Before LogVice's own code ran (Windows, .NET, antivirus): " + before);
            lines.Add("");
            Mark("Main");
        }

        public static void Mark(string step)
        {
            if (!On) return;
            lines.Add(watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(7) + " ms  " + step);
            Write();
        }

        static void Write()
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "LogVice-start.txt"), string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine, Encoding.UTF8); }
            catch (Exception) { }
        }
    }
}
