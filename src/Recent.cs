namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    // One entry of the recent list: the files or event logs that were opened together, and whether it is pinned.
    public sealed class RecentEntry
    {
        public string[] Paths;
        public bool Pinned;

        public string Key
        {
            get { return string.Join("|", Paths); }
        }

        public string Label
        {
            get
            {
                string detail;
                return Settings.RecentLabel(Paths, out detail);
            }
        }

        public string Detail
        {
            get
            {
                string detail;
                Settings.RecentLabel(Paths, out detail);
                return detail;
            }
        }
    }

    // The recent list as it is kept in the settings: one string per entry, paths joined with |, a leading * for a pinned entry.
    // The list is stored with the newest entry first.
    public static class RecentList
    {
        public const int MaxUnpinned = 10;

        public static RecentEntry Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            RecentEntry e = new RecentEntry();
            if (raw[0] == '*')
            {
                e.Pinned = true;
                raw = raw.Substring(1);
            }
            if (raw.Length == 0) return null;
            e.Paths = raw.Split('|');
            return e;
        }

        public static string Format(RecentEntry e)
        {
            return (e.Pinned ? "*" : "") + e.Key;
        }

        public static List<RecentEntry> ParseAll(IEnumerable<string> raw)
        {
            List<RecentEntry> list = new List<RecentEntry>();
            if (raw == null) return list;
            foreach (string r in raw)
            {
                RecentEntry e = Parse(r);
                if (e != null) list.Add(e);
            }
            return list;
        }

        public static List<string> FormatAll(List<RecentEntry> list)
        {
            List<string> raw = new List<string>();
            foreach (RecentEntry e in list) raw.Add(Format(e));
            return raw;
        }

        // Pinned entries first, then the others, each group newest first.
        public static List<RecentEntry> Ordered(List<RecentEntry> list)
        {
            List<RecentEntry> o = new List<RecentEntry>();
            foreach (RecentEntry e in list) if (e.Pinned) o.Add(e);
            foreach (RecentEntry e in list) if (!e.Pinned) o.Add(e);
            return o;
        }

        // Entries that contain every word of the text in their name, folder or paths.
        public static List<RecentEntry> Search(List<RecentEntry> list, string text)
        {
            string[] words = (text ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return new List<RecentEntry>(list);
            List<RecentEntry> found = new List<RecentEntry>();
            foreach (RecentEntry e in list)
            {
                string hay = e.Label + "\n" + e.Detail + "\n" + e.Key;
                bool all = true;
                foreach (string w in words)
                    if (hay.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0) { all = false; break; }
                if (all) found.Add(e);
            }
            return found;
        }

        static int IndexOf(List<RecentEntry> list, string[] paths)
        {
            string key = string.Join("|", paths);
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].Key, key, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        // Puts the files on top. An entry that was pinned stays pinned. Only the newest MaxUnpinned others are kept.
        public static List<RecentEntry> Add(List<RecentEntry> list, string[] paths)
        {
            List<RecentEntry> next = new List<RecentEntry>(list);
            if (paths == null || paths.Length == 0) return next;
            int at = IndexOf(next, paths);
            RecentEntry e;
            if (at >= 0)
            {
                e = next[at];
                next.RemoveAt(at);
            }
            else
            {
                e = new RecentEntry();
                e.Paths = (string[])paths.Clone();
            }
            next.Insert(0, e);
            int others = 0;
            for (int i = 0; i < next.Count; i++)
            {
                if (next[i].Pinned) continue;
                if (++others > MaxUnpinned) { next.RemoveAt(i); i--; }
            }
            return next;
        }

        public static List<RecentEntry> SetPinned(List<RecentEntry> list, string[] paths, bool pinned)
        {
            List<RecentEntry> next = new List<RecentEntry>(list);
            int at = IndexOf(next, paths);
            if (at >= 0) next[at].Pinned = pinned;
            return next;
        }

        public static List<RecentEntry> Remove(List<RecentEntry> list, string[] paths)
        {
            List<RecentEntry> next = new List<RecentEntry>(list);
            int at = IndexOf(next, paths);
            if (at >= 0) next.RemoveAt(at);
            return next;
        }
    }
}
