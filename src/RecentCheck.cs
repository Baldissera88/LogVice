namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    // Whether the files of the recent list still exist, checked in the background. A log on another computer that is off
    // or out of reach can take half a minute to answer, so the window never waits for it.
    public static class RecentCheck
    {
        static readonly Dictionary<string, bool> known = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly object gate = new object();

        // Called on a background thread when results came in.
        public static event Action Changed;

        // True only when the path was checked and is not there. Paths not checked yet count as there.
        public static bool Missing(string path)
        {
            if (string.IsNullOrEmpty(path) || EventLogs.IsChannel(path)) return false;
            lock (gate)
            {
                bool there;
                return known.TryGetValue(path, out there) && !there;
            }
        }

        // The computer of a UNC path, or "" for a path on this PC.
        public static string Host(string path)
        {
            if (path == null || !path.StartsWith(@"\\", StringComparison.Ordinal)) return "";
            int end = path.IndexOf('\\', 2);
            return end < 0 ? path.Substring(2) : path.Substring(2, end - 2);
        }

        // Starts checking the paths not checked yet: one background thread per computer, so one computer that does not
        // answer does not hold up the others.
        public static void Check(IEnumerable<string> paths)
        {
            Dictionary<string, List<string>> byHost = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            lock (gate)
            {
                foreach (string p in paths)
                {
                    if (string.IsNullOrEmpty(p) || EventLogs.IsChannel(p) || known.ContainsKey(p) || !queued.Add(p)) continue;
                    string h = Host(p);
                    List<string> list;
                    if (!byHost.TryGetValue(h, out list)) { list = new List<string>(); byHost[h] = list; }
                    list.Add(p);
                }
            }
            foreach (List<string> group in byHost.Values)
            {
                List<string> work = group;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool hostDown = false;
                    foreach (string p in work)
                    {
                        bool there = false;
                        if (!hostDown)
                        {
                            try { there = File.Exists(p); } catch (Exception) { there = false; }
                            // When the first file on another computer is not there, the computer is often not reachable:
                            // the other files on it are not asked for, so the check does not wait again for each one.
                            if (!there && Host(p).Length > 0) hostDown = !ShareReachable(p);
                        }
                        lock (gate) { known[p] = there; queued.Remove(p); }
                    }
                    Action c = Changed;
                    if (c != null) { try { c(); } catch (Exception) { } }
                });
            }
        }

        static bool ShareReachable(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                return !string.IsNullOrEmpty(root) && Directory.Exists(root);
            }
            catch (Exception) { return false; }
        }

        // Forgets the results, for example after the recent list was cleared.
        public static void Reset()
        {
            lock (gate) known.Clear();
        }
    }
}
