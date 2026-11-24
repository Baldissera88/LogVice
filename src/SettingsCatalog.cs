namespace LogVice
{
    using System;
    using System.Collections.Generic;

    // Where settings are kept, so tests can use memory instead of HKCU\Software\LogVice.
    public interface ISettingStore
    {
        string Get(string name);
        // A null value removes the setting, so its default applies again.
        void Set(string name, string value);
    }

    public sealed class MemorySettingStore : ISettingStore
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Get(string name)
        {
            string v;
            return Values.TryGetValue(name, out v) ? v : null;
        }

        public void Set(string name, string value)
        {
            if (value == null) Values.Remove(name);
            else Values[name] = value;
        }
    }

    // One subject of the Settings window.
    public sealed class SettingPage
    {
        public string Id, Title, Summary;
    }

    // One setting as Settings shows and finds it.
    public sealed class SettingDef
    {
        public string Id, Page, Title, Summary, More, Keywords;
        // The title of the row when another part of LogVice makes the row, such as Updates.
        public string Row;
        // The values under HKCU\Software\LogVice and what each is when nothing is saved. Only settings with
        // defaults get the changed mark and are reset. Highlight rules, folders and recent files have none.
        public string[] Keys = new string[0];
        public string[] Defaults = new string[0];

        public bool Resettable
        {
            get { return Keys.Length > 0 && Defaults.Length == Keys.Length; }
        }

        public bool Changed(ISettingStore s)
        {
            if (!Resettable || s == null) return false;
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!Same(s.Get(Keys[i]), Defaults[i])) return true;
            }
            return false;
        }

        // Removes the saved values, so the defaults apply again.
        public int Reset(ISettingStore s)
        {
            if (!Resettable || s == null) return 0;
            int n = 0;
            foreach (string k in Keys)
            {
                if (s.Get(k) == null) continue;
                s.Set(k, null);
                n++;
            }
            return n;
        }

        // Nothing saved is the default. Yes and no are kept as 1 and 0, and some older values as true and false.
        static bool Same(string value, string fallback)
        {
            if (value == null) return true;
            string v = value.Trim();
            if (fallback == "1" || fallback == "0")
            {
                bool on = v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
                bool off = v == "0" || string.Equals(v, "false", StringComparison.OrdinalIgnoreCase);
                if (on || off) return on == (fallback == "1");
            }
            return string.Equals(v, fallback, StringComparison.OrdinalIgnoreCase);
        }
    }

    // The subjects of the Settings window and every setting on them, in the order they are shown.
    public static class SettingsCatalog
    {
        public const string General = "general", List = "list", Reading = "reading", Highlights = "highlights",
            Folders = "folders", Updates = "updates", Data = "data";

        // The subject Settings opened on last.
        public const string LastPagePref = "SettingsPage";

        public static readonly SettingPage[] Pages =
        {
            P(General, "General", "The theme, and what LogVice does when it opens."),
            P(List, "Log list", "How the log list and the detail pane look."),
            P(Reading, "Reading", "How LogVice opens logs and which rights it uses."),
            P(Highlights, "Highlights", "Rules that colour rows by their message or component."),
            P(Folders, "Folders", "Where LogVice looks for logs."),
            P(Updates, "Updates", "The version you have and the newest one on GitHub."),
            P(Data, "Data and privacy", "What LogVice keeps on this PC and what it sends."),
        };

        public static readonly SettingDef[] All =
        {
            // General
            D("theme", General, "Theme", "The colours of LogVice.",
                "System follows the Windows app mode. Terminal and Amber look like old green and amber screens, with numbers and times in a monospace font.",
                "appearance colour color dark light vice terminal amber synthwave system", "Theme", Palettes.Default),
            D("reopen", General, "Reopen the last logs at start", "Open the most recent log or merged set when LogVice starts.",
                "A command line with files, or --demo, is opened instead.", "start open last recent", "ReopenLast", "0"),
            D("live", General, "Start with Live on", "Follow new lines as they are written, from the moment LogVice starts.",
                "You can switch Live on and off in the header at any time.", "follow tail new lines paused", "Live", "1"),

            // Log list
            D("textsize", List, "Log text size", "Size of the text in the log list and the detail pane.",
                null, "font small medium large", "TextSize", "1"),
            D("density", List, "Row spacing", "How tightly rows are packed in the log list.",
                null, "compact standard relaxed height", "Density", "1"),
            D("tint", List, "Tint error and warning rows", "Colour the whole row red or amber, not only the severity icon.",
                "Your own highlight rules are set under Highlights.", "colour color red amber severity row", "Tint", "1"),
            D("linenumbers", List, "Show line numbers", "Show the line in the log file for each row.",
                "For ETL traces this is the event number, and for event logs the record ID. Press Ctrl+G to go to a line.", "line number goto", "LineNumbers", "1"),

            // Reading
            D("automerge", Reading, "Auto-merge spanned logs", "When you open a log, also open the other parts it was split into and merge them by time.",
                "That is X.lo_, dated copies such as AppEnforce-20261104-101500.log or CbsPersist_*.log, and every WindowsUpdate.*.etl in the folder. Up to " + LogLocations.MaxParts + " of the newest parts are added.",
                "history merge parts lo_ rolled split", "AutoMerge", "1", "History", "1"),
            D("admin", Reading, "Administrator rights", "Some logs, such as Windows Temp or a site server's logs, can only be read by an administrator.",
                "LogVice asks when it meets one, or you can restart it as administrator here. It opens the same logs again.", "elevated restart uac"),
            D("defaultapp", Reading, "Default app for .log files", "After installing the MSI, choose LogVice for .log and .lo_ under Default apps.",
                "In Windows settings: Default apps > Choose defaults by file type.", "file type association open with msi"),

            // Highlights
            D("rules", Highlights, "Highlight rules", "Rows whose message or component matches a rule get its colour and label.",
                "Rules are checked from the top and the first match wins. F7 and Shift+F7 jump between highlighted rows, and the Highlights filter shows only the rows of one rule.",
                "colour color match regex examples order edit remove add"),

            // Folders
            D("found", Folders, "Found on this computer", "ConfigMgr, Intune and Windows log folders that exist on this PC.",
                "LogVice looks for ConfigMgr site server, management point, distribution point, console, client and task sequence logs, Intune Management Extension logs and Windows logs.",
                "browse discover locations client ccm intune"),
            D("addfolder", Folders, "Add a folder", "Add your own log folder. It appears on the start page and in Browse logs.",
                "For example a share with collected logs. Removing a folder here does not change the folder.", "custom share path remove", "Folders", null),

            // Updates
            D("updatecheck", Updates, "Check for updates", "Once a day at start, LogVice asks GitHub for the latest version.",
                "A line in the status bar shows when there is a newer one. Nothing about you or your logs is sent. Never on the command line, as SYSTEM, in Windows PE, during Windows setup or the ESP, or while a task sequence runs. Administrators can turn it off for everyone with the DWORD DisableUpdateCheck = 1 under HKLM\\SOFTWARE\\Policies\\LogVice.",
                "github version release policy", "UpdateCheck", "1"),
            Row(D("updates", Updates, "Updates", "The version you have and the newest on GitHub, with Check now and Install.", null, "github version release install download"), "Updates"),
            D("releasepage", Updates, "Release page", "The releases of LogVice on GitHub.", null, "github download msi exe"),
            D("whatsnew", Updates, "What's new", "Read what changed in this version and the ones before it.", null, "changelog notes release"),

            // Data and privacy
            D("recent", Data, "Recent files", "The logs and merged sets on the start page and under Open.",
                "Clear removes them all, except the ones you pinned.", "history clear forget pins"),
            D("settingsstore", Data, "Where settings are stored", "Settings are kept for your Windows account in the registry.", null, "registry hkcu"),
            D("writes", Data, "What LogVice writes", "Only its settings, in the registry. Logs are opened read-only and are never changed.",
                "Demo logs are written to the Temp folder when you choose Look around. An update is downloaded to a folder under Temp and removed later.", "files temp demo cache"),
            D("sends", Data, "What LogVice sends", "Only what a feature you use needs, to the place it needs.",
                "GitHub: which version is the latest, once a day, and the download when you choose Install. Another computer: only when you connect to it in Browse logs. Nothing is sent to the author of LogVice.",
                "privacy network github telemetry"),
            D("resetall", Data, "Reset all settings", "Put back every default. Recent files, your own folders and your highlight rules are removed too.",
                null, "delete registry clear everything"),
        };

        static SettingPage P(string id, string title, string summary)
        {
            SettingPage p = new SettingPage();
            p.Id = id;
            p.Title = title;
            p.Summary = summary;
            return p;
        }

        // keyDefault holds pairs of a key and its default. A null default means the value has none, so it is not reset.
        static SettingDef D(string id, string page, string title, string summary, string more, string keywords, params string[] keyDefault)
        {
            SettingDef d = new SettingDef();
            d.Id = id;
            d.Page = page;
            d.Title = title;
            d.Summary = summary;
            d.More = more;
            d.Keywords = keywords;
            List<string> keys = new List<string>(), defaults = new List<string>();
            for (int i = 0; i + 1 < keyDefault.Length; i += 2)
            {
                keys.Add(keyDefault[i]);
                if (keyDefault[i + 1] != null) defaults.Add(keyDefault[i + 1]);
            }
            d.Keys = keys.ToArray();
            d.Defaults = defaults.ToArray();
            return d;
        }

        static SettingDef Row(SettingDef d, string row)
        {
            d.Row = row;
            return d;
        }

        public static SettingPage Page(string id)
        {
            foreach (SettingPage p in Pages) if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        public static SettingDef Find(string id)
        {
            foreach (SettingDef d in All) if (d.Id == id) return d;
            return null;
        }

        public static List<SettingDef> On(string page)
        {
            List<SettingDef> l = new List<SettingDef>();
            foreach (SettingDef d in All) if (d.Page == page) l.Add(d);
            return l;
        }

        // The saved subject, or General when none is saved or it is not known any more.
        public static string Remembered(string stored)
        {
            SettingPage p = Page(stored);
            return p != null ? p.Id : General;
        }

        public static bool Resettable(string page)
        {
            foreach (SettingDef d in On(page)) if (d.Resettable) return true;
            return false;
        }

        public static List<SettingDef> ChangedOn(string page, ISettingStore s)
        {
            List<SettingDef> l = new List<SettingDef>();
            foreach (SettingDef d in On(page)) if (d.Changed(s)) l.Add(d);
            return l;
        }

        // Puts back the defaults of one subject. Returns how many saved values were removed.
        public static int Reset(string page, ISettingStore s)
        {
            int n = 0;
            foreach (SettingDef d in On(page)) n += d.Reset(s);
            return n;
        }

        // Settings whose name, description or subject has every word of the text. Names that match come first.
        public static List<SettingDef> Search(string text)
        {
            List<SettingDef> hits = new List<SettingDef>();
            string q = (text ?? "").Trim().ToLowerInvariant();
            if (q.Length == 0) return hits;
            string[] words = q.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            List<int> ranks = new List<int>();
            foreach (SettingDef d in All)
            {
                string title = d.Title.ToLowerInvariant();
                SettingPage p = Page(d.Page);
                string all = title + " " + (d.Summary ?? "") + " " + (d.More ?? "") + " " + (d.Keywords ?? "") + " " + (p != null ? p.Title : "");
                all = all.ToLowerInvariant();
                bool every = true, inTitle = true;
                foreach (string w in words)
                {
                    if (all.IndexOf(w, StringComparison.Ordinal) < 0) every = false;
                    if (title.IndexOf(w, StringComparison.Ordinal) < 0) inTitle = false;
                }
                if (!every) continue;
                int rank = title == q ? 0 : title.StartsWith(q, StringComparison.Ordinal) ? 1 : inTitle ? 2 : 3;
                int at = 0;
                while (at < hits.Count && ranks[at] <= rank) at++;
                hits.Insert(at, d);
                ranks.Insert(at, rank);
            }
            return hits;
        }
    }
}
