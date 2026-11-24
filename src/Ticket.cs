namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    // What to do next for error codes that deployments often hit. LogVice's own Codes table says what a code means,
    // this adds what to try.
    public static class ErrorAdvice
    {
        sealed class Entry
        {
            public string Meaning;
            public string Next;

            public Entry(string meaning, string next)
            {
                Meaning = meaning;
                Next = next;
            }
        }

        static readonly Dictionary<uint, Entry> Table = new Dictionary<uint, Entry>
        {
            { 1602, new Entry("The user cancelled the installation.", "Make the install command silent, for example msiexec /qn, so no one can cancel it.") },
            { 1603, new Entry("A fatal error occurred during installation.", "Add /l*v to the msiexec command line and look for \"return value 3\" in the MSI log.") },
            { 1605, new Entry("This action is only valid for products that are installed. Usually an uninstall ran for a product that is not on the device.", "Check the product code in the uninstall command against the one that is installed.") },
            { 1612, new Entry("The installation source for this product is not available.", "Windows Installer needs the original MSI to repair, update or remove the product. Check that the cached package or the source folder still exists.") },
            { 1618, new Entry("Another installation is already in progress.", "Another installation was running. The agent normally retries later.") },
            { 1619, new Entry("The installation package could not be opened.", "Check that the MSI is in the content and that the command line names it correctly, relative to the working folder.") },
            { 1620, new Entry("The installation package could not be opened. It may not be a valid Windows Installer package.", "The file is often damaged or incomplete. Redistribute or upload the content again.") },
            { 1624, new Entry("Error applying transforms.", "Check that the MST file named in TRANSFORMS= is in the content and was made for this MSI.") },
            { 1638, new Entry("Another version of this product is already installed.", "Remove the old version first, or set up an upgrade: supersedence, or a major upgrade in the MSI.") },
            { 1641, new Entry("The installer started a restart. This counts as success.", "Nothing to fix. Items after it usually wait for the restart.") },
            { 3010, new Entry("A restart is required to finish the install. This counts as success.", "Restart the device to finish the install.") },
            { 0x80070002, new Entry("The system cannot find the file specified.", "A file the install needs is missing: check the content, the command line and the working folder.") },
            { 0x80070005, new Entry("Access is denied.", "Check the install context (system or user). Security software or application control often blocks the installer; check its logs.") },
            { 0x80070020, new Entry("The file is in use by another process.", "Close the app or service that holds the file, or install while it is not running. A restart usually frees it.") },
            { 0x80070070, new Entry("There is not enough space on the disk.", "Free disk space. The content is downloaded and unpacked before it installs, so it needs room for both.") },
            { 0x800704C7, new Entry("The operation was cancelled.", "Often a restart, a sign-out or a time limit stopped the install. Check what happened on the device at that time.") },
            { 0x80072EE2, new Entry("The connection to the server timed out.", "Check the network path to the service: proxy, firewall and VPN. It often clears on a later retry.") },
            { 0x80072EE7, new Entry("The server name could not be resolved.", "Check DNS and that the device has a working network connection.") },
            { 0x80072F8F, new Entry("A security error occurred while connecting to the server.", "Usually a wrong date and time on the device, or a proxy that inspects TLS. Check the clock and the proxy.") },
            { 0x80072F76, new Entry("The HTTP header that was asked for was not found.", "The download was cut off or changed on the way, often by a proxy. Check the proxy and the download logs.") },
            { 0x80190190, new Entry("The server answered HTTP 400 (bad request).", "The request was not valid for the service. Check the address and that the device's clock and registration are right.") },
            { 0x80190191, new Entry("The server answered HTTP 401 (unauthorized).", "The device or user could not sign in to the service. Check the device registration and the user's sign-in.") },
            { 0x80190193, new Entry("The server answered HTTP 403 (forbidden).", "The service refused the request. In enrollment this often means the user may not enroll or has no licence.") },
            { 0x80190194, new Entry("The server answered HTTP 404 (not found).", "The content or address does not exist on the server. Check the content location or the URL.") },
            { 0x87D1041C, new Entry("The application was not detected after installation completed successfully.", "The installer finished, but the detection rule did not find the app. Check the detection rule against what the installer really writes.") },
            { 0x87D30065, new Entry("Failed to retrieve content information.", "The agent could not get the app's content details from Intune. Check the network and proxy, and that the content finished uploading.") },
            { 0x87D30067, new Entry("Error unzipping downloaded content.", "Check free disk space. If it keeps happening, upload the .intunewin file again.") },
            { 0x87D300C9, new Entry("The unmonitored process is in progress, however it may time out.", "The install command was still running when the agent stopped watching it. Check that the command is silent and does not wait for input.") },
            { 0x80004005, new Entry("Unspecified error.", "A general error. The log lines just before it usually name the real problem.") },
            { 0x80070490, new Entry("Element not found.", "In a task sequence this often means a driver, disk or setting the step expected is missing. Check the step's lines in smsts.log.") },
            { 0x800700A1, new Entry("The specified path is not valid.", "In a task sequence this often means the drive or partition the step uses does not exist. Check the disk and partition steps.") },
            { 0x80091007, new Entry("The hash value is not correct.", "The downloaded content does not match the package. Update the distribution points or redistribute the package.") },
            { 0x8007000E, new Entry("Not enough memory resources are available.", "In Windows PE, give the device or virtual machine more memory, or raise the scratch space of the boot image.") },
            { 0x87D00324, new Entry("The application was not detected after installation completed.", "The installer finished, but the detection method did not find the app. Check the detection method against what the installer writes.") },
            { 0x87D00607, new Entry("The content for this deployment was not found on a distribution point.", "Distribute the content to a distribution point in the device's boundary group, or allow fallback.") },
            { 0x87D00213, new Entry("The job timed out before it finished.", "The installer ran longer than the maximum run time. Check that it is silent and does not wait for input.") }
        };

        static Entry Find(uint u)
        {
            Entry e;
            if (Table.TryGetValue(u, out e)) return e;
            if ((u & 0xFFFF0000) == 0x80070000 && Table.TryGetValue(u & 0xFFFF, out e)) return e;
            return null;
        }

        public static string Meaning(uint code)
        {
            Entry e = Find(code);
            return e == null ? null : e.Meaning;
        }

        public static string Next(uint code)
        {
            Entry e = Find(code);
            return e == null ? null : e.Next;
        }
    }

    // What goes into "Copy for ticket": plain text that reads well in a ticket or an e-mail.
    public sealed class TicketInput
    {
        public Store Store;
        // The entries to print, in the order of the list.
        public int[] Rows = new int[0];
        // The index into Rows of the row that was selected, or -1 when several rows were selected.
        public int Marked = -1;
        // Rows that were selected but are not printed because of the limit.
        public int Omitted;
        public string Computer = "";
        public DateTime Now;
        public string Version = "";
        public Session State;
    }

    public static class Ticket
    {
        public const int MaxRows = 200;
        public const int Context = 5;
        const int MaxLinesPerRow = 12;

        // Which list positions to print. lo..hi is the selection. A single row also gets Context rows on both sides.
        // first..last are positions; marked is the position of the single row or -1; omitted counts the selected rows over the limit.
        public static void Range(int lo, int hi, int count, out int first, out int last, out int marked, out int omitted)
        {
            marked = -1;
            omitted = 0;
            if (count <= 0 || hi < lo) { first = 0; last = -1; return; }
            lo = Math.Max(0, Math.Min(lo, count - 1));
            hi = Math.Max(lo, Math.Min(hi, count - 1));
            if (lo == hi)
            {
                marked = lo;
                first = Math.Max(0, lo - Context);
                last = Math.Min(count - 1, lo + Context);
                return;
            }
            first = lo;
            last = hi;
            if (last - first + 1 > MaxRows)
            {
                omitted = last - first + 1 - MaxRows;
                last = first + MaxRows - 1;
            }
        }

        // The computer the logs come from: another computer when the logs are read from one, else this PC.
        public static string ComputerFor(string[] paths, string thisPc)
        {
            foreach (string p in paths ?? new string[0])
            {
                SessionSource s = SessionSource.From(p);
                if (!string.IsNullOrEmpty(s.Computer)) return s.Computer;
                Match m = Regex.Match(p ?? "", @"^\\\\([^\\]+)\\");
                if (m.Success) return m.Groups[1].Value;
            }
            return thisPc;
        }

        // The filter in words, or null when nothing is filtered.
        public static string Describe(SessionFilter f)
        {
            if (f == null) return null;
            List<string> parts = new List<string>();
            if ((f.Severities & 7) != 7)
            {
                List<string> kinds = new List<string>();
                if ((f.Severities & 4) != 0) kinds.Add("errors");
                if ((f.Severities & 2) != 0) kinds.Add("warnings");
                if ((f.Severities & 1) != 0) kinds.Add("information");
                parts.Add(string.Join(" and ", kinds.ToArray()));
            }
            if (!string.IsNullOrEmpty(f.Text)) parts.Add((f.Exclude ? "without " : "text ") + "\"" + f.Text + "\"" + (f.Regex ? " (regular expression)" : ""));
            if (!string.IsNullOrEmpty(f.Component)) parts.Add("component " + f.Component);
            if (!string.IsNullOrEmpty(f.Thread)) parts.Add("thread " + f.Thread);
            if (f.From != long.MinValue) parts.Add("from " + Fmt.Time(f.From));
            if (f.To != long.MaxValue) parts.Add("until " + Fmt.Time(f.To));
            if (!string.IsNullOrEmpty(f.Highlight)) parts.Add(f.Highlight == "*" ? "highlighted rows" : "highlight " + f.Highlight);
            return parts.Count == 0 ? null : string.Join(" \u00B7 ", parts.ToArray());
        }

        static string Utc(DateTime now)
        {
            TimeSpan o = TimeZoneInfo.Local.GetUtcOffset(now);
            return "UTC" + (o < TimeSpan.Zero ? "-" : "+") + Math.Abs(o.Hours).ToString("00", CultureInfo.InvariantCulture) + ":" + Math.Abs(o.Minutes).ToString("00", CultureInfo.InvariantCulture);
        }

        static string LogName(string path)
        {
            if (EventLogs.IsChannel(path)) return "Event log " + EventLogs.DisplayName(path);
            return Demo.Show(path);
        }

        static string Level(byte sev)
        {
            return sev >= 3 ? "Error" : sev == 2 ? "Warning" : "Info";
        }

        public static string Build(TicketInput input)
        {
            Store st = input.Store;
            StringBuilder sb = new StringBuilder();
            sb.Append("LogVice ").Append(input.Version);
            if (!string.IsNullOrEmpty(input.Computer)) sb.Append(" \u00B7 ").Append(input.Computer);
            sb.Append(" \u00B7 copied ").Append(input.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" (").Append(Utc(input.Now)).Append(")\r\n");

            string[] paths = input.State != null ? input.State.Paths() : new string[0];
            if (paths.Length > 0)
            {
                List<string> names = new List<string>();
                for (int i = 0; i < paths.Length && i < 6; i++) names.Add(LogName(paths[i]));
                string logs = string.Join(", ", names.ToArray());
                if (paths.Length > 6) logs += " and " + (paths.Length - 6) + " more";
                sb.Append("Logs: ").Append(logs).Append(paths.Length > 1 ? " (merged by time)" : "").Append("\r\n");
            }
            string filter = input.State != null ? Describe(input.State.Filter) : null;
            if (filter != null) sb.Append("Filter: ").Append(filter).Append("\r\n");
            sb.Append("\r\n");

            int n = input.Rows.Length;
            int compW = 0, thrW = 0;
            foreach (int i in input.Rows)
            {
                int c = st.Comp[i];
                if (c >= 0) compW = Math.Max(compW, Math.Min(24, st.CompNames[c].Length));
                if (st.Thread[i] >= 0) thrW = Math.Max(thrW, st.Thread[i].ToString(CultureInfo.InvariantCulture).Length + 2);
            }
            bool marks = input.Marked >= 0;
            List<uint> codes = new List<uint>();
            for (int r = 0; r < n; r++)
            {
                int i = input.Rows[r];
                StringBuilder line = new StringBuilder();
                if (marks) line.Append(r == input.Marked ? "> " : "  ");
                line.Append(st.Time[i] == Store.NoTime ? new string(' ', 23) : Fmt.Time(st.Time[i])).Append("  ");
                line.Append(Level(st.Sev[i]).PadRight(7)).Append("  ");
                if (compW > 0)
                {
                    int c = st.Comp[i];
                    string cn = c >= 0 ? st.CompNames[c] : "";
                    if (cn.Length > 24) cn = cn.Substring(0, 23) + "\u2026";
                    line.Append(cn.PadRight(compW)).Append("  ");
                }
                if (thrW > 0)
                {
                    string t = st.Thread[i] >= 0 ? "[" + st.Thread[i].ToString(CultureInfo.InvariantCulture) + "]" : "";
                    line.Append(t.PadRight(thrW)).Append("  ");
                }
                string[] parts = st.Msg[i].Replace("\r\n", "\n").Split('\n');
                // A JSON row: only the message goes on the ticket line, not the extra fields.
                if (parts.Length > 1 && st.Sources[st.Src[i]].Format == JsonLogs.Id) parts = new string[] { parts[0] };
                string indent = new string(' ', line.Length);
                line.Append(parts[0].Replace("\t", " ").Trim());
                for (int k = 1; k < parts.Length; k++)
                {
                    if (k >= MaxLinesPerRow) { line.Append("\r\n").Append(indent).Append("\u2026"); break; }
                    line.Append("\r\n").Append(indent).Append(parts[k].Replace("\t", "    ").TrimEnd());
                }
                sb.Append(line).Append("\r\n");
                foreach (uint u in Codes.Find(st.Msg[i])) if (!codes.Contains(u)) codes.Add(u);
            }
            if (input.Omitted > 0) sb.Append("\u2026 ").Append(input.Omitted.ToString("N0", CultureInfo.InvariantCulture)).Append(" more selected rows are not copied. The limit is ").Append(MaxRows).Append(" rows.\r\n");

            if (codes.Count > 0)
            {
                sb.Append("\r\nError codes in these lines\r\n");
                foreach (uint u in codes) sb.Append(CodeLine(u)).Append("\r\n");
            }
            return sb.ToString();
        }

        // 1603 (0x00000643)  Fatal error during installation. What to try.
        public static string CodeLine(uint u)
        {
            Codes.Info d = Codes.Describe(u);
            string label = u < 0x10000 ? d.Title + " (0x" + u.ToString("X8", CultureInfo.InvariantCulture) + ")" : d.Title;
            string meaning = d.Text;
            if (string.IsNullOrEmpty(meaning)) meaning = ErrorAdvice.Meaning(u);
            StringBuilder sb = new StringBuilder(label).Append("  ");
            sb.Append(string.IsNullOrEmpty(meaning) ? "Not in the built-in table." : Sentence(meaning));
            string next = ErrorAdvice.Next(u);
            if (!string.IsNullOrEmpty(next)) sb.Append(' ').Append(Sentence(next));
            return sb.ToString();
        }

        static string Sentence(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return s;
            char c = s[s.Length - 1];
            return c == '.' || c == '!' || c == '?' ? s : s + ".";
        }
    }
}
