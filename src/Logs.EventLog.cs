namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Eventing.Reader;
    using System.Globalization;
    using System.IO;
    using System.Security;
    using System.Text;

    public static class EventLogs
    {
        public const string Prefix = "evt:";
        public const string Group = "Event logs";
        const long AuditFailure = 0x10000000000000L;

        public static readonly string[] Common =
        {
            "Application",
            "System",
            "Security",
            "Setup",
            "Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin",
            "Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Operational",
            "Microsoft-Windows-AAD/Operational",
            "Microsoft-Windows-User Device Registration/Admin",
            "Microsoft-Windows-ModernDeployment-Diagnostics-Provider/Autopilot",
            "Microsoft-Windows-Provisioning-Diagnostics-Provider/Admin",
            "Microsoft-Windows-WindowsUpdateClient/Operational",
            "Microsoft-Windows-Bits-Client/Operational",
            "Microsoft-Windows-GroupPolicy/Operational",
            "Microsoft-Windows-BitLocker/BitLocker Management",
            "Microsoft-Windows-Windows Defender/Operational",
            "Microsoft-Windows-AppLocker/EXE and DLL",
            "Microsoft-Windows-AppLocker/MSI and Script",
            "Microsoft-Windows-CodeIntegrity/Operational",
            "Microsoft-Windows-AppxDeploymentServer/Operational",
            "Microsoft-Windows-PowerShell/Operational",
            "Microsoft-Windows-TaskScheduler/Operational",
            "Microsoft-Windows-WMI-Activity/Operational",
            "Microsoft-Windows-WinRM/Operational"
        };

        public static bool IsChannel(string p)
        {
            return p != null && p.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFile(string p)
        {
            return p != null && p.EndsWith(".evtx", StringComparison.OrdinalIgnoreCase) && !IsChannel(p);
        }

        public static bool IsEvent(string p)
        {
            return IsChannel(p) || IsFile(p);
        }

        public static bool Available(string p)
        {
            if (IsChannel(p)) return true;
            try { return System.IO.File.Exists(p); } catch (Exception) { return false; }
        }

        public static long Size(string p)
        {
            try
            {
                string computer, channel;
                Split(p, out computer, out channel);
                using (EventLogSession s = Session(computer))
                    return s.GetLogInformation(channel, PathType.LogName).FileSize ?? 0;
            }
            catch (Exception) { return 0; }
        }

        public static void Open(string computer)
        {
            try
            {
                if (string.IsNullOrEmpty(computer) || LogLocations.IsLocal(computer)) System.Diagnostics.Process.Start("eventvwr.msc");
                else System.Diagnostics.Process.Start("eventvwr.exe", computer);
            }
            catch (Exception) { }
        }

        public static string Make(string computer, string channel)
        {
            return string.IsNullOrEmpty(computer) ? Prefix + channel : Prefix + @"\\" + computer + @"\" + channel;
        }

        public static void Split(string p, out string computer, out string channel)
        {
            computer = null;
            channel = p.Substring(Prefix.Length);
            if (channel.StartsWith(@"\\", StringComparison.Ordinal))
            {
                int i = channel.IndexOf('\\', 2);
                if (i > 2)
                {
                    computer = channel.Substring(2, i - 2);
                    channel = channel.Substring(i + 1);
                }
            }
        }

        public static string Short(string channel)
        {
            const string ms = "Microsoft-Windows-";
            return channel.StartsWith(ms, StringComparison.OrdinalIgnoreCase) ? channel.Substring(ms.Length) : channel;
        }

        public static string DisplayName(string p)
        {
            if (!IsChannel(p)) return System.IO.Path.GetFileName(p);
            string computer, channel;
            Split(p, out computer, out channel);
            return Short(channel) + (computer != null ? " on " + computer : "");
        }

        public static EventLogSession Session(string computer)
        {
            if (string.IsNullOrEmpty(computer) || LogLocations.IsLocal(computer)) return new EventLogSession();
            string domain, user;
            SecureString pwd;
            if (RemoteLogin.SessionCredential(computer, out domain, out user, out pwd))
                return new EventLogSession(computer, domain, user, pwd, SessionAuthentication.Default);
            return new EventLogSession(computer);
        }

        public static int CountChannels(string computer, bool all)
        {
            using (EventLogSession s = Session(computer))
            {
                int n = 0;
                if (all)
                {
                    foreach (string name in s.GetLogNames()) n++;
                    return n;
                }
                foreach (string name in s.GetLogNames())
                    foreach (string c in Common)
                        if (string.Equals(c, name, StringComparison.OrdinalIgnoreCase)) { n++; break; }
                return n;
            }
        }

        public static List<LogFile> Channels(string computer, bool all)
        {
            List<LogFile> list = new List<LogFile>();
            using (EventLogSession s = Session(computer))
            {
                List<string> names = new List<string>();
                if (all) names.AddRange(s.GetLogNames());
                else names.AddRange(Common);
                foreach (string name in names)
                {
                    LogFile f = new LogFile();
                    f.Path = Make(computer, name);
                    f.Rel = Short(name);
                    try
                    {
                        EventLogInformation info = s.GetLogInformation(name, PathType.LogName);
                        long records = info.RecordCount ?? 0;
                        if (records == 0 && all) continue;
                        f.Size = info.FileSize ?? 0;
                        f.Modified = info.LastWriteTime ?? DateTime.MinValue;
                    }
                    catch (EventLogNotFoundException) { continue; }
                    catch (UnauthorizedAccessException)
                    {
                        f.Rel += "  (administrator)";
                    }
                    catch (Exception)
                    {
                        if (all) continue;
                    }
                    list.Add(f);
                    if (list.Count >= 5000) break;
                }
            }
            return list;
        }

        public static void Read(Store st, LogSource src, LoadToken tok, Action<long> progress)
        {
            src.Format = "evt";
            src.EncName = "Event log";
            bool file = IsFile(src.Path);
            string computer = null, target = src.Path;
            if (!file) Split(src.Path, out computer, out target);
            src.EventChannel = !file && (computer == null || LogLocations.IsLocal(computer));
            EventLogSession session = file ? new EventLogSession() : Session(computer);
            src.Handle = session;
            PathType pt = file ? PathType.FilePath : PathType.LogName;
            long total = 0;
            try
            {
                EventLogInformation info = session.GetLogInformation(target, pt);
                total = info.RecordCount ?? 0;
                src.Size = info.FileSize ?? 0;
            }
            catch (EventLogNotFoundException)
            {
                throw new IOException(file ? "The file is not a Windows event log." : "There is no event log called " + target + (computer != null ? " on " + computer : "") + ".");
            }
            catch (UnauthorizedAccessException) { throw; }
            catch (EventLogException ex)
            {
                throw new IOException(Explain(ex, computer));
            }
            catch (Exception) { }
            if (file && src.Size == 0) { try { src.Size = new FileInfo(src.Path).Length; } catch (Exception) { } }
            EventLogQuery q = new EventLogQuery(target, pt);
            q.Session = session;
            long n = 0;
            try
            {
                using (EventLogReader r = new EventLogReader(q))
                {
                    r.BatchSize = 256;
                    while (tok.State == 0)
                    {
                        EventRecord e = r.ReadEvent();
                        if (e == null) break;
                        using (e) Add(st, src, e);
                        n++;
                        if ((n & 255) == 0)
                        {
                            src.Pos = total > 0 ? (long)(src.Size * Math.Min(1.0, n / (double)total)) : 0;
                            if (progress != null) progress(src.Pos);
                        }
                    }
                }
            }
            catch (EventLogNotFoundException)
            {
                throw new IOException("There is no event log called " + target + (computer != null ? " on " + computer : "") + ".");
            }
            catch (EventLogException ex)
            {
                if (n == 0) throw new IOException(Explain(ex, computer));
                src.Note = "Reading " + src.Name + " stopped after " + n.ToString("N0", CultureInfo.CurrentCulture) + " events: " + ex.Message;
            }
            if (tok.State != 0) src.Partial = true;
            src.Pos = src.Size;
        }

        public static void ReadMore(Store st, LogSource src)
        {
            if (!src.EventChannel) return;
            EventLogSession session = src.Handle as EventLogSession;
            if (session == null) return;
            string computer, channel;
            Split(src.Path, out computer, out channel);
            EventLogQuery q = new EventLogQuery(channel, PathType.LogName, "*[System[EventRecordID>" + src.LastRecord.ToString(CultureInfo.InvariantCulture) + "]]");
            q.Session = session;
            using (EventLogReader r = new EventLogReader(q))
            {
                for (int k = 0; k < 5000; k++)
                {
                    EventRecord e = r.ReadEvent();
                    if (e == null) break;
                    using (e) Add(st, src, e);
                }
            }
        }

        public static string Explain(Exception ex, string computer)
        {
            string m = ex.Message ?? "";
            if (computer != null && (m.IndexOf("RPC", StringComparison.OrdinalIgnoreCase) >= 0 || ex.HResult == unchecked((int)0x800706BA) || ex.HResult == 1722))
                return "LogVice could not reach the event logs on " + computer + ". Enable the firewall rule group Remote Event Log Management on that computer, or open its exported .evtx files instead.";
            return m;
        }

        static void Add(Store st, LogSource src, EventRecord e)
        {
            long t = Store.NoTime;
            short bias = Store.NoBias;
            DateTime? tc = e.TimeCreated;
            if (tc.HasValue)
            {
                DateTime local = tc.Value.Kind == DateTimeKind.Utc ? tc.Value.ToLocalTime() : tc.Value;
                t = local.Ticks;
                bias = (short)Math.Round(-TimeZoneInfo.Local.GetUtcOffset(local).TotalMinutes);
            }
            byte level = e.Level ?? 4;
            long kw = e.Keywords ?? 0;
            byte sev = level == 1 || level == 2 ? (byte)3 : level == 3 ? (byte)2 : (kw & AuditFailure) != 0 ? (byte)2 : (byte)1;
            string desc = null;
            try { desc = e.FormatDescription(); } catch (Exception) { }
            if (string.IsNullOrEmpty(desc)) desc = Properties(e);
            int id = e.Id;
            string msg = "[" + id.ToString(CultureInfo.InvariantCulture) + "] " + (desc ?? "").Trim();
            string task = null;
            try { task = e.TaskDisplayName; } catch (Exception) { }
            long rec = e.RecordId ?? 0;
            if (rec > src.LastRecord) src.LastRecord = rec;
            src.CurLine = (int)Math.Min(int.MaxValue, Math.Max(0, rec));
            string source = "Event ID " + id.ToString(CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(task) ? "" : "  ·  " + task);
            st.Add(src, msg, t, bias, sev, st.CompId(e.ProviderName ?? "Event log"), e.ThreadId ?? -1, st.CFileId(source));
        }

        static string Properties(EventRecord e)
        {
            try
            {
                IList<EventProperty> props = e.Properties;
                if (props == null || props.Count == 0) return "(no message text)";
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < props.Count && i < 32; i++)
                {
                    object v = props[i].Value;
                    if (v == null) continue;
                    string s = v is byte[] ? BitConverter.ToString((byte[])v).Replace("-", "") : Convert.ToString(v, CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(s)) continue;
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(s);
                }
                return sb.Length > 0 ? sb.ToString() : "(no message text)";
            }
            catch (Exception) { return "(no message text)"; }
        }
    }
}
