namespace LogVice.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // The JSON reader, saved sessions and history, Copy for ticket, and the command line options for landing on a row.
    static partial class Program
    {
        static string JsonData(string name)
        {
            return Path.Combine(data, "json", name);
        }

        static string Tmp()
        {
            string dir = Path.Combine(Path.GetTempPath(), "LogVice-extras-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        static string Sev3(Store st, int i)
        {
            return st.Sev[i] == 3 ? "error" : st.Sev[i] == 2 ? "warning" : "info";
        }

        // ---------- recent files checked in the background ----------

        static void RecentFilesCheck()
        {
            Check(RecentCheck.Host(@"\\CONTOSO-LT0417\C$\Windows\CCM\Logs\AppEnforce.log") == "CONTOSO-LT0417", "the computer of a UNC path is found");
            Check(RecentCheck.Host(@"C:\Windows\CCM\Logs\AppEnforce.log") == "", "a local path has no computer");
            string dir = Tmp();
            string there = Path.Combine(dir, "there.log"), gone = Path.Combine(dir, "gone.log");
            File.WriteAllText(there, "x");
            Check(!RecentCheck.Missing(gone), "a recent file not checked yet counts as there, so the window never waits");
            Check(!RecentCheck.Missing("evt:Application"), "an event log channel is never missing");
            System.Threading.ManualResetEvent done = new System.Threading.ManualResetEvent(false);
            Action changed = delegate { if (RecentCheck.Missing(gone)) done.Set(); };
            RecentCheck.Changed += changed;
            RecentCheck.Check(new string[] { there, gone, "evt:Application" });
            bool finished = done.WaitOne(10000);
            RecentCheck.Changed -= changed;
            Check(finished && RecentCheck.Missing(gone) && !RecentCheck.Missing(there), "the background check marks a missing recent file and keeps the one that is there");
            RecentCheck.Reset();
            Check(!RecentCheck.Missing(gone), "Reset forgets the results");
            UpdateInfo early = new UpdateInfo();
            early.Newer = true; early.Latest = "9.0.0";
            Check(Updates.StillPublishing(early) && !early.Newer && early.Error != null && early.Error.Contains("being published"), "a release without its files yet is not offered");
            UpdateInfo ready = new UpdateInfo();
            ready.Newer = true; ready.Latest = "9.0.0"; ready.SumsUrl = "https://github.com/Baldissera88/LogVice/releases/download/v9.0.0/SHA256SUMS.txt";
            Check(!Updates.StillPublishing(ready) && ready.Newer, "a release with its files is offered");
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        // ---------- JSON reader ----------

        static void JsonDetection()
        {
            Check(Load(JsonData("lines.jsonl")).Sources[0].Format == "json", "JSON Lines are detected");
            Check(Load(JsonData("serilog.jsonl")).Sources[0].Format == "json", "JSON Lines with @t and @mt are detected");
            Check(Load(JsonData("array.json")).Sources[0].Format == "json", "a file that is an array of objects is detected");
            Check(Load(JsonData("badlines.jsonl")).Sources[0].Format == "json", "JSON Lines with bad lines further down are detected");
            Check(Load(JsonData("not-json.txt")).Sources[0].Format == "plain", "lines that start with { but are not JSON stay plain text");
            Check(Load(JsonData("brackets.txt")).Sources[0].Format == "plain", "lines that start with [ but are not an array of objects stay plain text");
            Check(Parser.FormatName("json") == "JSON Lines", "the JSON format is called JSON Lines");
            Check(JsonLogs.IsLines("{\"a\":1}\n{\"b\":2}\n", false) && !JsonLogs.IsLines("{\"a\":1}\nplain\n", false) && !JsonLogs.IsLines("", false) && !JsonLogs.IsLines("[1,2]", false), "JSON Lines detection looks at the first lines");
            Check(!JsonLogs.IsLines("banner\n{\"a\":1}\n{\"b\":2}\n", false) && JsonLogs.IsLines("{\"a\":1}\nnote\n{\"b\":2}\n{\"c\":3}\n", false), "the first line must be an object, and most of the first ten must be");
            Check(JsonLogs.IsLines("{\"a\":1}\n{\"b\":", true) && !JsonLogs.IsLines("{\"a\":1}\n{\"b\":", false), "a last line cut off by the size of the sample is ignored");
            Check(JsonLogs.StartsArray("  [\n {\"a\":1}") && !JsonLogs.StartsArray("[1,2,3]") && !JsonLogs.StartsArray("{\"a\":[{}]}") && !JsonLogs.StartsArray("[INFO] x"), "an array is detected when it starts with an object");

            string dir = Tmp();
            try
            {
                // Bytes with a byte order mark and a UTF-16 file are read too.
                string utf8 = Path.Combine(dir, "bom.jsonl");
                File.WriteAllText(utf8, "{\"time\":\"2026-11-24 09:00:00\",\"message\":\"caf\u00e9\"}\n", new UTF8Encoding(true));
                Store a = Load(utf8);
                Check(a.Count == 1 && a.Sources[0].Format == "json" && a.Msg[0] == "caf\u00e9", "JSON Lines with a byte order mark are read");
                string u16 = Path.Combine(dir, "utf16.jsonl");
                File.WriteAllText(u16, "{\"time\":\"2026-11-24 09:00:00\",\"message\":\"one\"}\n{\"time\":\"2026-11-24 09:00:01\",\"message\":\"two\"}\n", Encoding.Unicode);
                Store b = Load(u16);
                Check(b.Count == 2 && b.Sources[0].Format == "json" && b.Msg[1] == "two", "UTF-16 JSON Lines are read");
                string big = Path.Combine(dir, "big.json");
                using (FileStream fs = new FileStream(big, FileMode.Create))
                {
                    byte[] head = Encoding.ASCII.GetBytes("[\n{\"message\":\"x\"}\n");
                    fs.Write(head, 0, head.Length);
                    fs.SetLength(JsonLogs.MaxArrayBytes + 10);
                }
                Store c = Load(big);
                Check(c.Sources[0].Format == "plain" && c.Sources[0].Note != null && c.Sources[0].Note.Contains("64 MB"), "a JSON array of 64 MB or more is opened as plain text with a note");
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static void JsonFields()
        {
            Store st = Load(JsonData("lines.jsonl"));
            Check(st.Count == 7, "lines.jsonl has 7 rows");
            Check(st.Time[0] == new DateTime(2026, 11, 24, 8, 0, 1, 250).Ticks && st.Bias[0] == Store.NoBias, "timestamp without an offset is local time as written");
            Check(st.CompNames[st.Comp[0]] == "Contoso.Agent.Heartbeat" && st.Thread[0] == 14 && st.Sev[0] == 1, "timestamp, level, logger and thread are found");
            Check(st.Msg[0] == "Heartbeat sent\nhost: CONTOSO-LT0417", "other fields follow the message as key: value lines");
            Check(st.Time[1] == new DateTime(2026, 11, 24, 8, 0, 2).Ticks && st.Sev[1] == 2 && st.CompNames[st.Comp[1]] == "Fabrikam.Sync" && st.Thread[1] == 7, "time, severity, source and tid are found");
            Check(st.Msg[1] == "Retry 1 of 3\nattempt: 1\ndetail: {\"uri\":\"https://sync.fabrikam.example\",\"code\":503}", "msg is the message and a nested object is shown as compact JSON");
            Check(st.Time[2] == new DateTime(2026, 11, 24, 8, 0, 3, 500).Ticks && st.Sev[2] == 3 && st.CompNames[st.Comp[2]] == "Northwind.Upload" && st.Thread[2] == 9 && st.Msg[2] == "Upload failed with 0x80072EE7", "@timestamp, lvl, component, threadId and Message are found");
            Check(st.Time[3] == new DateTime(2026, 11, 24, 8, 0, 4).Ticks && st.Sev[3] == 3 && st.CompNames[st.Comp[3]] == "Woodgrove.Policy" && st.Thread[3] == 3 && st.Msg[3] == "Policy evaluation finished", "ts, loglevel 3, category, ThreadId and text are found");
            Check(st.Sev[4] == 1 && st.CompNames[st.Comp[4]] == "Contoso.Cache" && st.Msg[4] == "cache warmed" && st.Time[4] == new DateTime(2026, 11, 24, 8, 0, 5).Ticks, "datetime, Level debug, module and text are found");
            Check(st.Sev[5] == 3 && st.CompNames[st.Comp[5]] == "Contoso.Agent" && st.Time[5] == new DateTime(2026, 11, 24, 8, 0, 6).Ticks, "eventTime and app are found, and a message without a level uses the keyword rule");
            Check(st.Sev[6] == 3 && st.Thread[6] == -1 && st.Msg[6] == "Agent stopped\nthread: worker-2" && st.Time[6] == new DateTime(2026, 11, 24, 8, 0, 7).Ticks, "date is a time, crit is an error, and a thread that is not a number stays a field");
            Check(st.Line[0] == 1 && st.Line[6] == 7, "JSON Lines keep their line numbers");
            Check(Fmt.Display(st.Msg[0], true) == "Heartbeat sent" && Fmt.Display(st.Msg[0]).EndsWith("\u21b5", StringComparison.Ordinal), "extra JSON fields do not get the multi-line mark in the list");
            TicketInput jt = new TicketInput();
            jt.Store = st; jt.Rows = new int[] { 0, 1 }; jt.Marked = -1; jt.Now = new DateTime(2026, 11, 24, 21, 40, 15); jt.Version = "2.0.0";
            string jtext = Ticket.Build(jt);
            Check(jtext.Contains("Heartbeat sent") && !jtext.Contains("CONTOSO-LT0417") && !jtext.Contains("attempt: 1"), "Copy for ticket puts only the message of a JSON row on the line");

            Store s2 = Load(JsonData("serilog.jsonl"));
            Check(s2.Count == 4 && s2.Msg[0] == "Disk C: has 11 GB left\nDrive: C:\nFree: 11" && s2.Sev[0] == 2 && s2.CompNames[s2.Comp[0]] == "Contoso.Disk" && s2.Time[0] == new DateTime(2026, 11, 24, 8, 10, 0, 123).Ticks, "@mt is filled in with the values of the object");
            Check(s2.Msg[1] == "Sync finished: 12 items\n@mt: Sync finished: {Count} items\nCount: 12", "@m wins over @mt and @mt stays a field");
            Check(s2.Msg[2] == "User avery signed in from Woodgrove\nUser: avery\nSite: Woodgrove" && s2.Sev[2] == 1, "a template with several values is filled in");
            Check(s2.Sev[3] == 3 && s2.Msg[3].StartsWith("Cannot reach dc01.contoso.example\n", StringComparison.Ordinal) && s2.Msg[3].Contains("@x: System.Net.Sockets.SocketException"), "@l Error is an error and @x stays a field");

            Store arr = Load(JsonData("array.json"));
            Check(arr.Count == 3 && arr.Sev[0] == 1 && arr.Sev[1] == 2 && arr.Sev[2] == 3 && arr.Msg[1] == "Slow disk\nms: 912", "an array of objects gives one row per object");
            Check(arr.Line[0] == 2 && arr.Line[1] == 8 && arr.Line[2] == 15, "rows of an array keep the line where their object starts (" + arr.Line[0] + ", " + arr.Line[1] + ", " + arr.Line[2] + ")");

            Store broken = Load(JsonData("array-broken.json"));
            Check(broken.Count == 3 && broken.Msg[0] == "one" && broken.Msg[1].StartsWith("{\"time\":\"2026-11-24 08:50:01\"", StringComparison.Ordinal) && broken.Msg[2] == "]", "what cannot be read in an array is kept as plain rows");

            Dictionary<string, object> o = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            o["Name"] = "Ann";
            o["Count"] = 5L;
            Check(JsonLogs.Template("Hello {Name}, {Count:N0} and {Missing} {@Name}", o) == "Hello Ann, 5 and {Missing} Ann", "templates keep names they cannot fill in and accept formats and @");
        }

        static void JsonLevelsAndTimes()
        {
            string[] errors = { "error", "ERR", "Fatal", "critical", "crit", "alert", "emerg", "e", " Error " };
            bool ok = true;
            foreach (string e in errors) if (JsonLogs.Level(e) != 3) { ok = false; Fail("level " + e + " is an error"); }
            Check(ok, "error, err, fatal, critical, crit, alert, emerg and e are errors");
            Check(JsonLogs.Level("warn") == 2 && JsonLogs.Level("Warning") == 2 && JsonLogs.Level("w") == 2, "warn, warning and w are warnings");
            Check(JsonLogs.Level("Information") == 1 && JsonLogs.Level("debug") == 1 && JsonLogs.Level("verbose") == 1 && JsonLogs.Level("anything") == 1, "any other level is information");
            Check(JsonLogs.Level(0L) == 3 && JsonLogs.Level(1L) == 3 && JsonLogs.Level(3L) == 3 && JsonLogs.Level(4L) == 2 && JsonLogs.Level(5L) == 1 && JsonLogs.Level(7L) == 1 && JsonLogs.Level("3") == 3 && JsonLogs.Level("4") == 2, "syslog numbers 0 to 3 are errors, 4 is a warning, the rest are information");

            long t;
            short bias;
            DateTime utc = new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);
            long local = utc.ToLocalTime().Ticks;
            short localBias = (short)(-(int)TimeZoneInfo.Local.GetUtcOffset(utc).TotalMinutes);
            Check(JsonLogs.ReadTime("2026-11-24T08:00:00Z", out t, out bias) && t == local && bias == localBias, "ISO 8601 with Z is shown in local time");
            Check(JsonLogs.ReadTime("2026-11-24T10:00:00+02:00", out t, out bias) && t == local && bias == localBias, "ISO 8601 with +02:00 is shown in local time");
            Check(JsonLogs.ReadTime("2026-11-24T10:00:00.000+0200", out t, out bias) && t == local, "an offset without a colon is read");
            Check(JsonLogs.ReadTime("2026-11-24T03:00:00-05:00", out t, out bias) && t == local, "a negative offset is read");
            Check(JsonLogs.ReadTime("2026-11-24T08:00:00.5", out t, out bias) && t == new DateTime(2026, 11, 24, 8, 0, 0, 500).Ticks && bias == Store.NoBias, "a time without an offset is local time as written");
            Check(JsonLogs.ReadTime("2026-11-24 08:00:00", out t, out bias) && t == new DateTime(2026, 11, 24, 8, 0, 0).Ticks, "a space between date and time is read");
            long sec = (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Check(JsonLogs.ReadTime(sec, out t, out bias) && t == local, "a number of 10 digits is Unix seconds");
            Check(JsonLogs.ReadTime(sec * 1000 + 250, out t, out bias) && t == local + 250 * 10000, "a number of 13 digits is Unix milliseconds");
            Check(JsonLogs.ReadTime(sec.ToString(CultureInfo.InvariantCulture), out t, out bias) && t == local, "Unix seconds written as text are read");
            Check(JsonLogs.ReadTime(sec + ".5", out t, out bias) && t == local + 5000000, "Unix seconds with a fraction are read");
            Check(!JsonLogs.ReadTime(123456789L, out t, out bias) && !JsonLogs.ReadTime("yesterday", out t, out bias) && !JsonLogs.ReadTime("", out t, out bias) && !JsonLogs.ReadTime(true, out t, out bias), "numbers of other lengths and text that is not a time are not times");

            string dir = Tmp();
            try
            {
                string p = Path.Combine(dir, "zones.jsonl");
                File.WriteAllText(p, "{\"@t\":\"2026-11-24T08:00:00Z\",\"@m\":\"utc\"}\n{\"time\":" + sec + ",\"message\":\"seconds\"}\n{\"time\":" + (sec * 1000) + ",\"message\":\"millis\"}\n", new UTF8Encoding(false));
                Store st = Load(p);
                Check(st.Count == 3 && st.Time[0] == local && st.Time[1] == local && st.Time[2] == local && st.Bias[0] == localBias, "times in a file are shown in local time");
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static void JsonBadLinesAndMerge()
        {
            Store st = Load(JsonData("badlines.jsonl"));
            Check(st.Sources[0].Format == "json" && st.Count == 6, "bad lines are not dropped (" + st.Count + " rows)");
            Check(st.Msg[0] == "first line is fine" && st.Msg[2] == "this line is not JSON at all" && st.Msg[4].StartsWith("{\"timestamp\":\"2026-11-24 08:30:04\"", StringComparison.Ordinal) && st.Msg[5] == "last line is fine", "bad lines keep their raw text between the good rows");
            Check(st.Line[2] == 3 && st.Line[3] == 4 && st.Line[4] == 5 && st.Line[5] == 6 && st.Sev[3] == 2 && st.Sev[5] == 3, "bad lines keep their line numbers and the rows after them are read");

            string dir = Tmp();
            try
            {
                string[] all = Demo.Write(dir, new DateTime(2026, 11, 24));
                string[] look = Demo.LookAround(all);
                Check(look.Length == 9 && Path.GetFileName(look[8]) == "contoso-agent.jsonl" && Path.GetFileName(look[0]) == "AppEnforce.log", "Look around opens the 8 CMTrace logs and the JSON Lines log");
                Store m = Load(look);
                bool sorted = true;
                for (int o = 1; o < m.Count; o++) if (m.Key[m.Order[o]] < m.Key[m.Order[o - 1]]) sorted = false;
                int jsonRows = 0, jsonInFirst = 0;
                for (int o = 0; o < m.Count; o++)
                {
                    bool isJson = m.Sources[m.Src[m.Order[o]]].Format == "json";
                    if (isJson) jsonRows++;
                    if (isJson && o < 400) jsonInFirst++;
                }
                Check(m.Sources[8].Format == "json" && jsonRows > 400 && jsonInFirst > 20 && sorted, "the JSON Lines log merges by time with the CMTrace logs (" + jsonRows + " JSON rows, " + jsonInFirst + " of them in the first 400)");
                Check(m.SevCount[3] >= 17, "the merged demo has errors from the JSON Lines log too (" + m.SevCount[3] + ")");
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }

            Dictionary<string, object> dict = Json.Parse("{\"A\":1,\"b\":[true,null,\"x\\n\\u0041\"]}") as Dictionary<string, object>;
            Check(dict != null && Json.Long(Json.Get(dict, "a")) == 1 && Json.Text(Json.Get(dict, "B")) == "[true,null,\"x\\nA\"]", "Json reads objects, ignores the case of keys and writes compact text");
        }

        // ---------- sessions and history ----------

        static Session SampleSession(string a, string b)
        {
            Session s = Session.ForPaths(new string[] { a, b, "evt:Application", "evt:\\\\CONTOSO-PC12\\System", "\\\\CONTOSO-PC12\\C$\\Windows\\CCM\\Logs\\CAS.log" });
            s.Filter.Text = "exit \"code\" \\ 1603";
            s.Filter.Regex = true;
            s.Filter.Exclude = true;
            s.Filter.Severities = 6;
            s.Filter.Component = "AppEnforce";
            s.Filter.Thread = "0x1020";
            s.Filter.From = new DateTime(2026, 11, 24, 8, 44, 0, 500).Ticks;
            s.Filter.To = new DateTime(2026, 11, 24, 9, 0, 0).Ticks;
            s.Filter.Highlight = "Exit code";
            s.Find = "Timesheet \u00e9";
            s.SelectedSource = 1;
            s.SelectedLine = 42;
            return s;
        }

        static void SessionRoundTrip()
        {
            string dir = Tmp();
            try
            {
                string a = Path.Combine(dir, "AppEnforce.log"), b = Path.Combine(dir, "N\u00e5me with spaces.log");
                Session s = SampleSession(a, b);
                s.Saved = "2026-11-24T21:40:00+01:00";
                string path = Path.Combine(dir, "test.logvice");
                s.Save(path);
                string text = File.ReadAllText(path, Encoding.UTF8);
                Check(text.Contains("\"logvice\": 1") && text.Contains("\"sources\"") && text.Contains("\"severities\"") && text.Contains("\"selected\"") && text.Contains("\"saved\""), "a session file has the logvice version, sources, filter, selected and saved");
                byte[] raw = File.ReadAllBytes(path);
                Check(!(raw.Length > 2 && raw[0] == 0xEF && raw[1] == 0xBB), "a session file is UTF-8 without a byte order mark");
                bool complete;
                Check(Json.Parse(text, out complete) is Dictionary<string, object> && complete, "a session file is JSON");
                Session r = Session.Load(path);
                Check(r.Sources.Count == 5 && r.Sources[0].Path == a && r.Sources[1].Path == b, "paths come back as they were written, with accents and spaces");
                Check(r.Sources[2].Channel == "Application" && r.Sources[2].Computer == null && r.Sources[3].Channel == "System" && r.Sources[3].Computer == "CONTOSO-PC12", "event logs come back with their computer");
                Check(r.Sources[4].Computer == "CONTOSO-PC12" && r.Sources[4].Path == "C:\\Windows\\CCM\\Logs\\CAS.log", "a file on an admin share is a computer and a path");
                string[] paths = r.Paths();
                Check(paths[2] == "evt:Application" && paths[3] == "evt:\\\\CONTOSO-PC12\\System" && paths[4] == "\\\\CONTOSO-PC12\\C$\\Windows\\CCM\\Logs\\CAS.log" && paths[0] == a, "sources turn back into the paths LogVice opens");
                Check(r.Merged && r.Filter.Text == s.Filter.Text && r.Filter.Regex && r.Filter.Exclude && r.Filter.Severities == 6 && r.Filter.Component == "AppEnforce" && r.Filter.Thread == "0x1020" && r.Filter.Highlight == "Exit code", "the filter comes back");
                Check(r.Filter.From == s.Filter.From && r.Filter.To == s.Filter.To && r.Find == s.Find && r.SelectedSource == 1 && r.SelectedLine == 42 && r.Saved != null, "times, search, selected row and saved time come back");
                Check(r.ToJson() == s.ToJson() && r.Saved == s.Saved, "a session written again says the same");

                Session plain = Session.ForPaths(new string[] { a });
                plain.Save(path);
                Session p2 = Session.Load(path);
                Check(!p2.Merged && p2.Filter.IsEmpty && p2.Filter.Severities == 7 && p2.SelectedSource == -1 && p2.Find == "" && p2.Filter.From == long.MinValue && p2.Filter.To == long.MaxValue, "an unfiltered session reads back unfiltered");

                File.WriteAllText(a, "x");
                Session m = Session.ForPaths(new string[] { a, Path.Combine(dir, "gone.log"), "evt:Application" });
                List<string> found, missing;
                m.Split(out found, out missing);
                Check(found.Count == 2 && found[0] == a && found[1] == "evt:Application" && missing.Count == 1 && missing[0] == Path.Combine(dir, "gone.log"), "a session names the logs that are missing and keeps the rest");

                bool bad1 = false, bad2 = false, bad3 = false;
                try { Session.Parse("not json at all"); } catch (FormatException) { bad1 = true; }
                try { Session.Parse("{\"sources\":[]}"); } catch (FormatException) { bad2 = true; }
                try { Session.Parse(""); } catch (FormatException) { bad3 = true; }
                Check(bad1 && bad2 && bad3, "text that is not a session is refused with a message");
                Session lenient = Session.Parse("{\"logvice\":1,\"sources\":[{\"path\":\"C:\\\\a.log\"},{\"nothing\":1},5],\"filter\":{\"severities\":[\"nonsense\"],\"from\":\"nope\"},\"selected\":{\"source\":9,\"line\":3}}");
                Check(lenient.Sources.Count == 1 && lenient.Filter.Severities == 7 && lenient.Filter.From == long.MinValue && lenient.SelectedSource == -1, "odd values in a session are skipped, never a crash");
                Check(Session.IsSessionFile("a.LOGVICE") && !Session.IsSessionFile("a.log") && !Session.IsSessionFile(null), "sessions are told by the .logvice extension");
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static Session Tag(string name)
        {
            return Session.ForPaths(new string[] { name });
        }

        static void HistoryLimits()
        {
            SessionHistory h = new SessionHistory();
            Check(!h.CanBack && !h.CanForward && h.Back(Tag("now")) == null && h.Forward(Tag("now")) == null, "a new history has nothing to go back or forward to");
            h.Push(Tag("a"));
            Check(h.CanBack && !h.CanForward, "after a change there is something to go back to");
            h.Push(new Session());
            Check(h.BackCount == 1, "nothing is kept for a state without logs");
            h.Push(Tag("b"));
            Session back = h.Back(Tag("c"));
            Check(back != null && back.Sources[0].Path == "b" && h.CanForward && h.BackCount == 1, "Back returns the last state and Forward can return to where it was");
            Session back2 = h.Back(Tag("b"));
            Check(back2.Sources[0].Path == "a" && !h.CanBack && h.ForwardCount == 2, "Back goes on to the one before");
            Session fwd = h.Forward(Tag("a"));
            Check(fwd.Sources[0].Path == "b" && h.CanBack && h.ForwardCount == 1, "Forward returns what Back left");
            h.Push(Tag("x"));
            Check(!h.CanForward && h.BackCount == 2, "a new change drops what was ahead");
            Check(h.Back(null).Sources[0].Path == "x" && !h.CanForward, "going back from nothing keeps no way forward");

            SessionHistory big = new SessionHistory();
            for (int i = 0; i < 45; i++) big.Push(Tag("s" + i));
            Check(big.BackCount == SessionHistory.Max && SessionHistory.Max == 30, "at most 30 states are kept");
            Session oldest = null;
            int n = 0;
            while (big.CanBack) { oldest = big.Back(Tag("now")); n++; }
            Check(n == 30 && oldest.Sources[0].Path == "s15" && big.ForwardCount == 30, "the oldest states are dropped first (" + n + ", " + (oldest != null ? oldest.Sources[0].Path : "-") + ")");
            int f = 0;
            Session last = null;
            while (big.CanForward) { last = big.Forward(Tag("then")); f++; }
            Check(f == 30 && last.Sources[0].Path == "now", "Forward goes all the way back to where Back started");
        }

        // ---------- Copy for ticket ----------

        static void TicketTexts()
        {
            string dir = Tmp();
            try
            {
                Demo.Write(dir, new DateTime(2026, 11, 24));
                string log = Path.Combine(dir, "AppEnforce.log");
                Store st = Load(log);
                int hit = -1;
                for (int i = 0; i < st.Count; i++) if (st.Msg[i].Contains("Unmatched exit code (1603)")) hit = i;
                Check(hit > 6, "the demo AppEnforce.log has the 1603 row");

                int first, last, marked, omitted;
                Ticket.Range(10, 10, 50, out first, out last, out marked, out omitted);
                Check(first == 5 && last == 15 && marked == 10 && omitted == 0, "one selected row gets 5 rows on each side");
                Ticket.Range(2, 2, 50, out first, out last, out marked, out omitted);
                Check(first == 0 && last == 7 && marked == 2, "the context stops at the start of the list");
                Ticket.Range(48, 48, 50, out first, out last, out marked, out omitted);
                Check(first == 43 && last == 49 && marked == 48, "the context stops at the end of the list");
                Ticket.Range(3, 8, 50, out first, out last, out marked, out omitted);
                Check(first == 3 && last == 8 && marked == -1 && omitted == 0, "several selected rows are copied as they are");
                Ticket.Range(0, 299, 300, out first, out last, out marked, out omitted);
                Check(first == 0 && last == 199 && omitted == 100 && Ticket.MaxRows == 200, "at most 200 rows are copied");

                SessionFilter f = new SessionFilter();
                f.Severities = 6;
                f.Component = "AppEnforce";
                Check(Ticket.Describe(f) == "errors and warnings \u00b7 component AppEnforce", "the filter is described in words");
                Check(Ticket.Describe(new SessionFilter()) == null, "no filter gives no line");
                SessionFilter g = new SessionFilter();
                g.Text = "timeout";
                g.Exclude = true;
                g.Severities = 4;
                g.Thread = "4128";
                Check(Ticket.Describe(g) == "errors \u00b7 without \"timeout\" \u00b7 thread 4128", "an excluding text filter is described");

                DateTime now = new DateTime(2026, 11, 24, 21, 40, 15);
                TicketInput one = new TicketInput();
                one.Store = st;
                int[] rows = new int[11];
                for (int k = 0; k < rows.Length; k++) rows[k] = hit - 5 + k;
                one.Rows = rows;
                one.Marked = 5;
                one.Computer = "CONTOSO-LT0417";
                one.Now = now;
                one.Version = "2.0.0";
                one.State = Session.ForPaths(new string[] { log });
                one.State.Filter = f;
                string text = Ticket.Build(one);
                string[] lines = text.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                Check(lines[0].StartsWith("LogVice 2.0.0 \u00b7 CONTOSO-LT0417 \u00b7 copied 2026-11-24 21:40 (UTC", StringComparison.Ordinal) && lines[0].EndsWith(")", StringComparison.Ordinal), "the first line has the version, the computer and when it was copied");
                Check(lines[1] == "Logs: " + log && lines[2] == "Filter: errors and warnings \u00b7 component AppEnforce" && lines[3] == "", "the logs and the filter follow");
                int marks = 0, dated = 0;
                string markedLine = null;
                foreach (string l in lines)
                {
                    if (l.StartsWith("> ", StringComparison.Ordinal)) { marks++; markedLine = l; }
                    if (l.Length > 25 && (l.StartsWith("  2026-", StringComparison.Ordinal) || l.StartsWith("> 2026-", StringComparison.Ordinal))) dated++;
                }
                Check(marks == 1 && dated == 11 && markedLine.Contains("Error") && markedLine.Contains("AppEnforce") && markedLine.Contains("Unmatched exit code (1603) is considered an execution failure."), "a single row is marked with > among its 10 context rows");
                Check(markedLine.Contains("[4128]") && markedLine.Contains(Fmt.Time(st.Time[hit])), "a row has its time, level, component, thread and message");
                Check(text.Contains("\r\nError codes in these lines\r\n") && text.Contains("1603 (0x00000643)  MSI: Fatal error during installation.") && text.Contains("Add /l*v to the msiexec command line"), "error codes come with what they mean and what to do");

                TicketInput many = new TicketInput();
                many.Store = st;
                many.Rows = new int[] { 1, 2, 3 };
                many.Computer = "";
                many.Now = now;
                many.Version = "2.0.0";
                string text2 = Ticket.Build(many);
                Check(!text2.Contains("\r\n> ") && !text2.StartsWith("> ", StringComparison.Ordinal) && text2.StartsWith("LogVice 2.0.0 \u00b7 copied", StringComparison.Ordinal), "several rows have no marker and no empty computer name");

                // 250 rows are given, 200 are printed.
                string big = Path.Combine(dir, "big.log");
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 300; i++)
                    sb.Append("<![LOG[Row " + i + " exit code 1603]LOG]!><time=\"08:00:" + (i % 60).ToString("00") + ".000+-60\" date=\"11-24-2026\" component=\"Comp\" context=\"\" type=\"1\" thread=\"" + (100 + i % 3) + "\" file=\"\">\r\n");
                File.WriteAllText(big, sb.ToString(), new UTF8Encoding(false));
                Store bs = Load(big);
                Ticket.Range(0, 299, bs.Count, out first, out last, out marked, out omitted);
                int[] r2 = new int[last - first + 1];
                for (int k = 0; k < r2.Length; k++) r2[k] = first + k;
                TicketInput cap = new TicketInput();
                cap.Store = bs;
                cap.Rows = r2;
                cap.Omitted = omitted;
                cap.Computer = "PC";
                cap.Now = now;
                cap.Version = "2.0.0";
                string text3 = Ticket.Build(cap);
                int rowLines = 0;
                foreach (string l in text3.Split(new string[] { "\r\n" }, StringSplitOptions.None)) if (l.StartsWith("2026-11-24", StringComparison.Ordinal)) rowLines++;
                Check(rowLines == 200 && text3.Contains("100 more selected rows are not copied"), "the ticket has at most 200 rows and says how many were left out (" + rowLines + ")");
                Check(text3.IndexOf("1603 (0x00000643)", StringComparison.Ordinal) == text3.LastIndexOf("1603 (0x00000643)", StringComparison.Ordinal), "an error code is listed once");

                Check(Ticket.ComputerFor(new string[] { "C:\\a.log" }, "THISPC") == "THISPC" && Ticket.ComputerFor(new string[] { "evt:\\\\SRV01\\System" }, "THISPC") == "SRV01" && Ticket.ComputerFor(new string[] { "\\\\SRV02\\C$\\x.log" }, "THISPC") == "SRV02" && Ticket.ComputerFor(new string[] { "evt:Application" }, "THISPC") == "THISPC", "the computer is the remote one when the logs come from one, else this PC");
                string line1 = Ticket.CodeLine(0x87D00324);
                Check(line1.StartsWith("0x87D00324  ", StringComparison.Ordinal) && line1.Contains("not detected after installation") && line1.Contains("Check the detection method"), "a code of LogVice gets the advice of the error base");
                Check(ErrorAdvice.Next(0x80070005) != null && ErrorAdvice.Next(0x12345678) == null && ErrorAdvice.Meaning(5) == null, "advice is found for known codes, also as an HRESULT, and not for others");
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        // ---------- command line ----------

        static void CommandLineOptions()
        {
            StartOptions o = StartOptions.Parse(new string[] { "a.log", "--line", "42", "--time", "2026-11-24 21:21:57.123", "--find", "Contoso Timesheet", "--filter", "exit code", "--errors", "--theme", "vice", "b.log" });
            Check(o.Paths.Length == 2 && o.Paths[0] == "a.log" && o.Paths[1] == "b.log" && o.Line == 42 && o.Time == new DateTime(2026, 11, 24, 21, 21, 57, 123).Ticks && o.Find == "Contoso Timesheet" && o.Filter == "exit code" && o.Errors && o.Theme == "vice" && o.Ignored.Count == 0, "every landing option is read");
            Check(StartOptions.Parse(new string[0]).Line == 0 && StartOptions.Parse(new string[0]).Time == Store.NoTime && !StartOptions.Parse(new string[0]).Errors && StartOptions.Parse(new string[0]).Find == null, "without options nothing is set");
            StartOptions t = StartOptions.Parse(new string[] { "--time", "2026-11-24 21:21:57" });
            Check(t.Time == new DateTime(2026, 11, 24, 21, 21, 57).Ticks, "--time reads seconds without a fraction");
            Check(StartOptions.ParseTime("2026-11-24T21:21:57.5") == new DateTime(2026, 11, 24, 21, 21, 57, 500).Ticks && StartOptions.ParseTime("2026-11-24 21:21") == new DateTime(2026, 11, 24, 21, 21, 0).Ticks && StartOptions.ParseTime("21:21:57") == Store.NoTime && StartOptions.ParseTime("") == Store.NoTime && StartOptions.ParseTime(null) == Store.NoTime, "--time reads T, one to seven digits of fraction and minutes, and refuses the rest");
            StartOptions eq = StartOptions.Parse(new string[] { "--line=7", "--find=a=b", "--theme=light" });
            Check(eq.Line == 7 && eq.Find == "a=b" && eq.Theme == "light" && eq.Ignored.Count == 0, "--option=value is the same as --option value");
            StartOptions unknown = StartOptions.Parse(new string[] { "x.log", "--nonsense", "--line", "abc", "--time", "soon", "--frobnicate", "7", "--find" });
            Check(unknown.Paths.Length == 2 && unknown.Paths[0] == "x.log" && unknown.Paths[1] == "7" && unknown.Line == 0 && unknown.Time == Store.NoTime && unknown.Find == null, "unknown options and bad values are ignored and do not set anything");
            Check(unknown.Ignored.Count == 5 && unknown.Ignored[0] == "--nonsense" && unknown.Ignored[1].StartsWith("--line abc", StringComparison.Ordinal) && unknown.Ignored[2].StartsWith("--time \"soon\"", StringComparison.Ordinal) && unknown.Ignored[3] == "--frobnicate" && unknown.Ignored[4].StartsWith("--find", StringComparison.Ordinal), "each ignored option is listed (" + string.Join(" | ", unknown.Ignored.ToArray()) + ")");
            Check(unknown.IgnoredNote().StartsWith("Ignored these command line options: ", StringComparison.Ordinal) && StartOptions.Parse(new string[] { "--zzz" }).IgnoredNote() == "Ignored this command line option: --zzz" && StartOptions.Parse(new string[] { "a.log" }).IgnoredNote() == null, "a note for the status bar says what was ignored");
            Check(StartOptions.Parse(new string[] { "--line", "0" }).Ignored.Count == 1 && StartOptions.Parse(new string[] { "--line", "-3" }).Ignored.Count == 1, "line numbers start at 1");
            StartOptions s = StartOptions.Parse(new string[] { "C:\\logs\\work.logvice", "--errors" });
            Check(s.SessionPath == "C:\\logs\\work.logvice" && s.Errors && StartOptions.Parse(new string[] { "a.log" }).SessionPath == null, "a .logvice path is a session");
            StartOptions ev = StartOptions.Parse(new string[] { "evt:Application", "evt:\\\\PC\\System", "--find", "error", "--time", "2026-11-24 08:00:00" });
            Check(ev.Paths.Length == 2 && ev.Paths[0] == "evt:Application" && ev.Paths[1] == "evt:\\\\PC\\System" && ev.Find == "error" && ev.Time != Store.NoTime, "event logs go with the landing options");
            StartOptions old = StartOptions.Parse(new string[] { "--demo", "--shot", "x.png", "--size", "1400x860", "--theme", "Vice", "--page", "about", "--select", "text", "--scale", "125" });
            Check(old.Demo && old.Shot == "x.png" && old.Size.Width == 1400 && old.Size.Height == 860 && old.Theme == "Vice" && old.Page == "about" && old.Select == "text" && old.Scale == "125" && old.Ignored.Count == 0, "the options for demos and screenshots still work");
            string dir = Tmp();
            try
            {
                File.WriteAllText(Path.Combine(dir, "a.log"), "x");
                File.WriteAllText(Path.Combine(dir, "b.log"), "x");
                StartOptions w = StartOptions.Parse(new string[] { Path.Combine(dir, "*.log"), "--errors" });
                Check(w.Paths.Length == 2 && w.Errors, "a wildcard is expanded next to the options");
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static void HeaderWithSave()
        {
            string[] names = HeaderFold.Order;
            Check(Array.IndexOf(names, "Save") == Array.IndexOf(names, "Live") + 1, "Save sits after Live in the header");
            Check(Array.IndexOf(HeaderFold.FoldOrder, "Save") > Array.IndexOf(HeaderFold.FoldOrder, "Follow") && Array.IndexOf(HeaderFold.FoldOrder, "Save") < Array.IndexOf(HeaderFold.FoldOrder, "Browse"), "Save folds into More after Follow and before Browse");
            Check(Fmt.Display("a\nb") == "a  \u21b5", "a message with several lines shows its first line and a mark");
        }

        static void Ellipsis()
        {
            Func<string, int> seven = delegate (string t) { return t.Length * 7; };
            Check(Draw.Ellipsis("short", 100, seven) == "short", "text that fits is not cut");
            string cut = Draw.Ellipsis("Contoso.Agent.Heartbeat", 100, seven);
            Check(cut == "Contoso.Agent\u2026" && seven(cut) <= 100, "text that does not fit ends with an ellipsis (" + cut + ")");
            Check(Draw.Ellipsis("Contoso.Agent", 6, seven) == "" && Draw.Ellipsis("abc", 0, seven) == "abc" && Draw.Ellipsis("", 50, seven) == "", "no room gives no text");
            Check(Draw.Ellipsis("ab cd", 28, seven) == "ab\u2026", "a space before the cut is dropped");
        }
    }
}
