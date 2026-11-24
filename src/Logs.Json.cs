namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    // The JSON reader: JSON Lines (one object per line, also called NDJSON) and files that are one array of objects.
    // Field names are matched without regard to case and the first name that is found wins.
    public static class JsonLogs
    {
        public const string Id = "json";
        public const string Name = "JSON Lines";
        // An array of objects is read whole, so a file above this size is opened as plain text.
        public const long MaxArrayBytes = 64L << 20;

        public static readonly string[] TimeKeys = { "@t", "@timestamp", "timestamp", "time", "ts", "datetime", "date", "eventTime", "TimeCreated", "Timestamp" };
        public static readonly string[] LevelKeys = { "@l", "level", "severity", "lvl", "loglevel", "Level" };
        public static readonly string[] MessageKeys = { "@m", "message", "msg", "Message", "text", "@mt" };
        public static readonly string[] ComponentKeys = { "source", "logger", "component", "category", "SourceContext", "module", "app" };
        public static readonly string[] ThreadKeys = { "thread", "threadId", "tid", "ThreadId" };

        static readonly Regex RxTemplate = new Regex(@"\{([@$]?)([A-Za-z_][A-Za-z0-9_.]*)(?:,-?\d+)?(?::[^}]*)?\}", RegexOptions.Compiled);
        static readonly Regex RxIso = new Regex(@"^\d{4}-\d{2}-\d{2}", RegexOptions.Compiled);
        static readonly Regex RxOffset = new Regex(@"(?<=\d)(Z|z|[+-]\d{2}(?::?\d{2})?)$", RegexOptions.Compiled);

        // ---------- detection ----------

        // True when the first line that has text is a JSON object and most of the first ten are. A few lines of something else
        // between them, such as a start-up banner or a stack trace, do not matter. A last line that was cut off by the size of
        // the sample is ignored.
        public static bool IsLines(string sample, bool cutOff)
        {
            if (string.IsNullOrEmpty(sample)) return false;
            int p = 0, seen = 0, objects = 0;
            while (p < sample.Length && seen < 10)
            {
                int nl = sample.IndexOf('\n', p);
                bool last = nl < 0;
                if (last) nl = sample.Length;
                string line = sample.Substring(p, nl - p).Trim().TrimStart('\uFEFF');
                p = nl + 1;
                if (line.Length == 0) continue;
                if (last && cutOff) break;
                bool complete = false;
                object v = line[0] == '{' ? Json.Parse(line, out complete) : null;
                bool isObject = complete && v is Dictionary<string, object>;
                if (seen == 0 && !isObject) return false;
                seen++;
                if (isObject) objects++;
            }
            return seen > 0 && objects * 10 >= seen * 6;
        }

        // True when the text is an array whose first element is an object.
        public static bool StartsArray(string sample)
        {
            if (sample == null) return false;
            int i = 0;
            while (i < sample.Length && (char.IsWhiteSpace(sample[i]) || sample[i] == '\uFEFF')) i++;
            if (i >= sample.Length || sample[i] != '[') return false;
            i++;
            while (i < sample.Length && char.IsWhiteSpace(sample[i])) i++;
            return i < sample.Length && sample[i] == '{';
        }

        // ---------- reading ----------

        // One line of a JSON Lines file. Returns false when the line is not a JSON object, so the caller keeps it as plain text.
        public static bool Line(Store st, LogSource src, string line)
        {
            string t = line.Trim().TrimStart('\uFEFF');
            if (t.Length == 0 || t[0] != '{') return false;
            bool complete;
            Dictionary<string, object> o = Json.Parse(t, out complete) as Dictionary<string, object>;
            if (!complete || o == null) return false;
            AddObject(st, src, o, t);
            return true;
        }

        // A file that is one array of objects. Elements that cannot be read, and what follows them, are kept as plain text rows.
        public static void Array(Store st, LogSource src, string text)
        {
            int n = text.Length, p = 0, line = 1, counted = 0;
            while (p < n && (char.IsWhiteSpace(text[p]) || text[p] == '\uFEFF')) p++;
            if (p >= n || text[p] != '[')
            {
                PlainRest(st, src, text, 0, 1);
                return;
            }
            p++;
            while (true)
            {
                while (p < n && (char.IsWhiteSpace(text[p]) || text[p] == ',')) p++;
                if (p >= n || text[p] == ']') return;
                line += Count(text, counted, p);
                counted = p;
                int end;
                bool complete;
                object v = Json.Parse(text, p, false, out end, out complete);
                Dictionary<string, object> o = v as Dictionary<string, object>;
                if (!complete || o == null)
                {
                    PlainRest(st, src, text, p, line);
                    return;
                }
                src.CurLine = line;
                AddObject(st, src, o, text.Substring(p, end - p));
                p = end;
            }
        }

        static int Count(string s, int from, int to)
        {
            int c = 0;
            for (int i = from; i < to; i++) if (s[i] == '\n') c++;
            return c;
        }

        static void PlainRest(Store st, LogSource src, string text, int from, int line)
        {
            int p = from;
            while (p < text.Length)
            {
                int nl = text.IndexOf('\n', p);
                if (nl < 0) nl = text.Length;
                string l = text.Substring(p, nl - p).TrimEnd('\r');
                src.CurLine = line;
                if (l.Trim().Length > 0) Parser.PlainLine(st, src, l);
                p = nl + 1;
                line++;
            }
        }

        // ---------- one entry ----------

        // The text of a field as a person reads it: strings as they are, other values as compact JSON.
        static string Show(object v)
        {
            return v == null ? "null" : Json.Text(v);
        }

        static bool Find(Dictionary<string, object> o, string[] keys, out string key, out object value)
        {
            foreach (string k in keys)
            {
                object v;
                if (o.TryGetValue(k, out v) && v != null)
                {
                    key = k;
                    value = v;
                    return true;
                }
            }
            key = null;
            value = null;
            return false;
        }

        public static void AddObject(Store st, LogSource src, Dictionary<string, object> o, string raw)
        {
            List<string> used = new List<string>();
            string key;
            object v;

            long t = Store.NoTime;
            short bias = Store.NoBias;
            foreach (string k in TimeKeys)
            {
                object tv;
                if (!o.TryGetValue(k, out tv) || tv == null) continue;
                if (ReadTime(tv, out t, out bias)) { used.Add(k); break; }
                t = Store.NoTime;
                bias = Store.NoBias;
            }

            string message = null;
            if (Find(o, MessageKeys, out key, out v))
            {
                used.Add(key);
                if (string.Equals(key, "@mt", StringComparison.OrdinalIgnoreCase)) message = Template(Json.Text(v), o);
                else message = Json.Text(v);
            }

            byte sev = 0;
            bool hasLevel = false;
            if (Find(o, LevelKeys, out key, out v))
            {
                used.Add(key);
                hasLevel = true;
                sev = Level(v);
            }

            string comp = null;
            if (Find(o, ComponentKeys, out key, out v) && (v is string || v is long || v is double))
            {
                used.Add(key);
                comp = Json.Text(v);
            }

            int thread = -1;
            if (Find(o, ThreadKeys, out key, out v))
            {
                long? tn = Json.Long(v);
                if (tn.HasValue && tn.Value >= 0 && tn.Value <= int.MaxValue)
                {
                    used.Add(key);
                    thread = (int)tn.Value;
                }
            }

            string text;
            if (message == null)
            {
                // No message field: the row shows the whole object.
                text = raw;
            }
            else text = message + Others(o, used);
            if (!hasLevel) sev = Parser.Sev(message ?? raw);
            st.Add(src, text, t, bias, sev, st.CompId(comp), thread, -1);
        }

        // The message first, then every other field as a "key: value" line.
        static string Others(Dictionary<string, object> o, List<string> used)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, object> kv in o)
            {
                if (used.Exists(delegate (string u) { return string.Equals(u, kv.Key, StringComparison.OrdinalIgnoreCase); })) continue;
                string val = Show(kv.Value).Replace("\r\n", "\n").Replace("\n", "\n    ");
                sb.Append('\n').Append(kv.Key).Append(": ").Append(val);
            }
            return sb.ToString();
        }

        // Fills in a message template such as "Disk {Drive} has {Free} GB left" with the values of the object.
        public static string Template(string template, Dictionary<string, object> o)
        {
            if (string.IsNullOrEmpty(template)) return template;
            return RxTemplate.Replace(template, delegate (Match m)
            {
                object v;
                if (!o.TryGetValue(m.Groups[2].Value, out v)) return m.Value;
                return Show(v);
            });
        }

        // ---------- levels ----------

        public static byte Level(object v)
        {
            long? n = v is string ? null : Json.Long(v);
            string s = v as string;
            if (s != null)
            {
                s = s.Trim();
                long num;
                if (long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out num)) n = num;
            }
            if (n.HasValue) return n.Value >= 0 && n.Value <= 3 ? (byte)3 : n.Value == 4 ? (byte)2 : (byte)1;
            if (s == null) return 1;
            switch (s.ToLowerInvariant())
            {
                case "error": case "err": case "fatal": case "critical": case "crit": case "alert": case "emerg": case "emergency": case "e":
                    return 3;
                case "warn": case "warning": case "w":
                    return 2;
                default:
                    return 1;
            }
        }

        // ---------- times ----------

        // ISO 8601 with Z or an offset is shown in local time, without an offset it is taken as local time, a number is Unix seconds
        // (10 digits) or milliseconds (13 digits).
        public static bool ReadTime(object v, out long ticks, out short bias)
        {
            ticks = Store.NoTime;
            bias = Store.NoBias;
            string s = v as string;
            double d;
            if (v is long) d = (long)v;
            else if (v is double) d = (double)v;
            else if (s != null)
            {
                s = s.Trim();
                if (s.Length == 0) return false;
                if (!RxIso.IsMatch(s) && IsNumber(s))
                {
                    if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return false;
                    return FromUnix(d, out ticks, out bias);
                }
                return FromText(s, out ticks, out bias);
            }
            else return false;
            return FromUnix(d, out ticks, out bias);
        }

        static bool IsNumber(string s)
        {
            foreach (char c in s) if (!((c >= '0' && c <= '9') || c == '.' || c == '-')) return false;
            return true;
        }

        static bool FromUnix(double d, out long ticks, out short bias)
        {
            ticks = Store.NoTime;
            bias = Store.NoBias;
            double abs = Math.Abs(d);
            DateTime utc;
            if (abs >= 1e9 && abs < 1e10) utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(d);
            else if (abs >= 1e12 && abs < 1e13) utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(d);
            else return false;
            Local(utc, out ticks, out bias);
            return true;
        }

        static void Local(DateTime utc, out long ticks, out short bias)
        {
            DateTime local = utc.ToLocalTime();
            ticks = new DateTime(local.Ticks, DateTimeKind.Unspecified).Ticks;
            int west = -(int)TimeZoneInfo.Local.GetUtcOffset(utc).TotalMinutes;
            bias = (short)west;
        }

        static bool FromText(string s, out long ticks, out short bias)
        {
            ticks = Store.NoTime;
            bias = Store.NoBias;
            bool hasOffset = s.IndexOf(':') > 0 && RxOffset.IsMatch(s);
            if (hasOffset)
            {
                DateTimeOffset dto;
                if (!DateTimeOffset.TryParse(Fix(s), CultureInfo.InvariantCulture, DateTimeStyles.None, out dto)) return false;
                Local(dto.UtcDateTime, out ticks, out bias);
                return true;
            }
            DateTime dt;
            if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return false;
            if (dt.Year < 1990 || dt.Year > 2100) return false;
            ticks = new DateTime(dt.Ticks, DateTimeKind.Unspecified).Ticks;
            return true;
        }

        // DateTimeOffset needs +01:00; +0100 and +01 are common in logs.
        static string Fix(string s)
        {
            Match m = Regex.Match(s, @"([+-])(\d{2})(\d{2})?$");
            if (!m.Success) return s;
            return s.Substring(0, m.Index) + m.Groups[1].Value + m.Groups[2].Value + ":" + (m.Groups[3].Success ? m.Groups[3].Value : "00");
        }
    }
}
