namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    // A small JSON reader (case-insensitive keys) and writer. Objects are Dictionary<string, object>, arrays are List<object>,
    // numbers are long or double.
    public static class Json
    {
        const int MaxDepth = 64;

        public static object Parse(string s)
        {
            bool complete;
            return Parse(s, out complete);
        }

        public static object Parse(string s, out bool complete)
        {
            int end;
            object v = Parse(s, 0, false, out end, out complete);
            if (complete && s != null)
            {
                while (end < s.Length && char.IsWhiteSpace(s[end])) end++;
                if (end < s.Length) complete = false;
            }
            return v;
        }

        // Reads one value starting at start. strict: control characters end a string as an error,
        // so a value cut off in a log line does not swallow the next line.
        public static object Parse(string s, int start, bool strict, out int end, out bool complete)
        {
            end = start;
            complete = false;
            if (s == null || start < 0 || start >= s.Length) return null;
            Reader r = new Reader(s, start, strict);
            object v = null;
            try { v = r.Value(); }
            catch (Exception) { r.Error = true; }
            end = r.Pos;
            complete = !r.Error;
            return v;
        }

        public static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && key != null && d.TryGetValue(key, out v) ? v : null;
        }

        public static long? Long(object v)
        {
            if (v == null) return null;
            if (v is long) return (long)v;
            if (v is double)
            {
                double d = (double)v;
                if (d == Math.Floor(d) && Math.Abs(d) < 9e15) return (long)d;
                return null;
            }
            string s = v as string;
            long n;
            return s != null && long.TryParse(s.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n) ? (long?)n : null;
        }

        public static string Text(object v)
        {
            if (v == null) return null;
            string s = v as string;
            if (s != null) return s;
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is long) return ((long)v).ToString(CultureInfo.InvariantCulture);
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            StringBuilder b = new StringBuilder();
            Write(b, v, 0);
            return b.ToString();
        }

        static void Write(StringBuilder b, object v, int depth)
        {
            if (b.Length > 20000) return;
            if (depth > MaxDepth) { b.Append("\u2026"); return; }
            Dictionary<string, object> d = v as Dictionary<string, object>;
            List<object> l = v as List<object>;
            string s = v as string;
            if (v == null) b.Append("null");
            else if (s != null) b.Append(JsonWriter.Quote(s));
            else if (d != null)
            {
                b.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in d)
                {
                    if (!first) b.Append(',');
                    first = false;
                    b.Append(JsonWriter.Quote(kv.Key)).Append(':');
                    Write(b, kv.Value, depth + 1);
                }
                b.Append('}');
            }
            else if (l != null)
            {
                b.Append('[');
                for (int i = 0; i < l.Count; i++)
                {
                    if (i > 0) b.Append(',');
                    Write(b, l[i], depth + 1);
                }
                b.Append(']');
            }
            else b.Append(Text(v));
        }

        sealed class Reader
        {
            readonly string s;
            readonly int n;
            readonly bool strict;
            int depth;
            public int Pos;
            public bool Error;

            public Reader(string text, int start, bool strictStrings)
            {
                s = text;
                n = text.Length;
                Pos = start;
                strict = strictStrings;
            }

            void Ws()
            {
                while (Pos < n)
                {
                    char c = s[Pos];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\uFEFF') Pos++;
                    else break;
                }
            }

            public object Value()
            {
                Ws();
                if (Pos >= n) { Error = true; return null; }
                char c = s[Pos];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == '-' || (c >= '0' && c <= '9')) return Num();
                if (Lit("true")) return true;
                if (Lit("false")) return false;
                if (Lit("null")) return null;
                Error = true;
                return null;
            }

            bool Lit(string w)
            {
                if (string.CompareOrdinal(s, Pos, w, 0, w.Length) != 0) return false;
                Pos += w.Length;
                return true;
            }

            Dictionary<string, object> Obj()
            {
                Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (++depth > MaxDepth) { Error = true; depth--; return d; }
                Pos++;
                Ws();
                if (Pos < n && s[Pos] == '}') { Pos++; depth--; return d; }
                while (!Error)
                {
                    Ws();
                    if (Pos >= n || s[Pos] != '"') { Error = true; break; }
                    string k = Str();
                    if (Error) break;
                    Ws();
                    if (Pos >= n || s[Pos] != ':') { Error = true; break; }
                    Pos++;
                    object v = Value();
                    if (Error)
                    {
                        if (v is Dictionary<string, object> || v is List<object>) d[k] = v;
                        break;
                    }
                    d[k] = v;
                    Ws();
                    if (Pos >= n) { Error = true; break; }
                    if (s[Pos] == ',') { Pos++; continue; }
                    if (s[Pos] == '}') { Pos++; break; }
                    Error = true;
                }
                depth--;
                return d;
            }

            List<object> Arr()
            {
                List<object> l = new List<object>();
                if (++depth > MaxDepth) { Error = true; depth--; return l; }
                Pos++;
                Ws();
                if (Pos < n && s[Pos] == ']') { Pos++; depth--; return l; }
                while (!Error)
                {
                    object v = Value();
                    if (Error)
                    {
                        if (v is Dictionary<string, object> || v is List<object>) l.Add(v);
                        break;
                    }
                    l.Add(v);
                    Ws();
                    if (Pos >= n) { Error = true; break; }
                    if (s[Pos] == ',') { Pos++; continue; }
                    if (s[Pos] == ']') { Pos++; break; }
                    Error = true;
                }
                depth--;
                return l;
            }

            string Str()
            {
                Pos++;
                StringBuilder b = null;
                int run = Pos;
                while (Pos < n)
                {
                    char c = s[Pos];
                    if (c == '"')
                    {
                        string r = b == null ? s.Substring(run, Pos - run) : b.Append(s, run, Pos - run).ToString();
                        Pos++;
                        return r;
                    }
                    if (c == '\\')
                    {
                        if (b == null) b = new StringBuilder();
                        b.Append(s, run, Pos - run);
                        Pos++;
                        if (Pos >= n) break;
                        char e = s[Pos++];
                        switch (e)
                        {
                            case 'n': b.Append('\n'); break;
                            case 'r': b.Append('\r'); break;
                            case 't': b.Append('\t'); break;
                            case 'b': b.Append('\b'); break;
                            case 'f': b.Append('\f'); break;
                            case 'u':
                                int code;
                                if (Pos + 4 <= n && int.TryParse(s.Substring(Pos, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                                {
                                    b.Append((char)code);
                                    Pos += 4;
                                }
                                else b.Append('u');
                                break;
                            default: b.Append(e); break;
                        }
                        run = Pos;
                        continue;
                    }
                    if (strict && c < ' ') break;
                    Pos++;
                }
                Error = true;
                return null;
            }

            object Num()
            {
                int st = Pos;
                if (s[Pos] == '-') Pos++;
                while (Pos < n)
                {
                    char c = s[Pos];
                    if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') Pos++;
                    else break;
                }
                // Inside an object or array, a number that runs to the end of the text may be cut off.
                if (Pos >= n && depth > 0) { Error = true; return null; }
                string t = s.Substring(st, Pos - st);
                long l;
                if (long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l)) return l;
                double d;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                Error = true;
                return null;
            }
        }
    }

    // Writes indented JSON for the files LogVice saves.
    public sealed class JsonWriter
    {
        readonly StringBuilder b = new StringBuilder();
        readonly List<bool> first = new List<bool>();

        public override string ToString()
        {
            return b.ToString();
        }

        void Sep(string key)
        {
            if (first.Count > 0)
            {
                if (!first[first.Count - 1]) b.Append(',');
                first[first.Count - 1] = false;
                b.Append("\n").Append(' ', first.Count * 2);
            }
            if (key != null) b.Append(Quote(key)).Append(": ");
        }

        public JsonWriter Begin(string key)
        {
            Sep(key);
            b.Append('{');
            first.Add(true);
            kinds.Push('}');
            return this;
        }

        public JsonWriter BeginArray(string key)
        {
            Sep(key);
            b.Append('[');
            first.Add(true);
            kinds.Push(']');
            return this;
        }

        public JsonWriter End()
        {
            bool empty = first[first.Count - 1];
            first.RemoveAt(first.Count - 1);
            if (!empty) b.Append("\n").Append(' ', first.Count * 2);
            b.Append(kinds.Pop());
            return this;
        }

        readonly Stack<char> kinds = new Stack<char>();

        public JsonWriter Str(string key, string value)
        {
            Sep(key);
            b.Append(value == null ? "null" : Quote(value));
            return this;
        }

        public JsonWriter Num(string key, long value)
        {
            Sep(key);
            b.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonWriter Bool(string key, bool value)
        {
            Sep(key);
            b.Append(value ? "true" : "false");
            return this;
        }

        public static string Quote(string s)
        {
            StringBuilder q = new StringBuilder(s.Length + 2);
            q.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': q.Append("\\\""); break;
                    case '\\': q.Append("\\\\"); break;
                    case '\n': q.Append("\\n"); break;
                    case '\r': q.Append("\\r"); break;
                    case '\t': q.Append("\\t"); break;
                    default:
                        if (c < ' ') q.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else q.Append(c);
                        break;
                }
            }
            return q.Append('"').ToString();
        }
    }
}
