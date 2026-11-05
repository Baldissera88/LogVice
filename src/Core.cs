namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    public sealed class LogSource : IDisposable
    {
        public string Path;
        public string Name;
        public int Index;
        public string Format;
        public Encoding Enc;
        public string EncName = "";
        public FileStream Fs;
        public Decoder Dec;
        public string Carry = "";
        public long Pos;
        public long Size;
        public long LastKey = long.MinValue;
        public int Entries;
        public bool Partial;
        public bool Denied;
        public bool EventChannel;
        public long LastRecord;
        public IDisposable Handle;
        public string Error;
        public string Note;
        public int LineBase, ScanPos, ScanLine, CurLine;

        public LogSource(string path, int index)
        {
            Path = path;
            Name = EventLogs.IsChannel(path) ? EventLogs.DisplayName(path) : System.IO.Path.GetFileName(path);
            Index = index;
        }

        public void Dispose()
        {
            if (Fs != null)
            {
                try { Fs.Dispose(); } catch (Exception) { }
                Fs = null;
            }
            if (Handle != null)
            {
                try { Handle.Dispose(); } catch (Exception) { }
                Handle = null;
            }
        }
    }

    public sealed class LoadToken
    {
        public volatile int State;
        public volatile bool Oom;
    }

    public sealed class Store
    {
        public const short NoBias = short.MinValue;
        public const long NoTime = long.MinValue;

        public int Count;
        public int Cap;
        public string[] Msg = new string[0];
        public long[] Time = new long[0];
        public long[] Key = new long[0];
        public short[] Bias = new short[0];
        public byte[] Sev = new byte[0];
        public int[] Comp = new int[0];
        public int[] Thread = new int[0];
        public int[] CFile = new int[0];
        public ushort[] Src = new ushort[0];
        public int[] Line = new int[0];
        public int[] Order = new int[0];
        public int[] Rank = new int[0];
        public readonly int[] SevCount = new int[4];
        public readonly List<string> CompNames = new List<string>();
        public readonly List<int> CompCount = new List<int>();
        public readonly List<string> CFNames = new List<string>();
        public readonly List<LogSource> Sources = new List<LogSource>();
        readonly Dictionary<string, int> compMap = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> cfMap = new Dictionary<string, int>(StringComparer.Ordinal);

        void Grow()
        {
            int cap = Cap < 65536 ? 65536 : (int)Math.Min(int.MaxValue - 64L, Cap * 2L);
            Array.Resize(ref Msg, cap);
            Array.Resize(ref Time, cap);
            Array.Resize(ref Key, cap);
            Array.Resize(ref Bias, cap);
            Array.Resize(ref Sev, cap);
            Array.Resize(ref Comp, cap);
            Array.Resize(ref Thread, cap);
            Array.Resize(ref CFile, cap);
            Array.Resize(ref Src, cap);
            Array.Resize(ref Line, cap);
            Cap = cap;
        }

        public void Add(LogSource src, string msg, long t, short bias, byte sev, int comp, int thread, int cfile)
        {
            if (Count == Cap) Grow();
            int i = Count;
            long k = t != NoTime ? t : src.LastKey;
            src.LastKey = k;
            Msg[i] = msg;
            Time[i] = t;
            Key[i] = k;
            Bias[i] = bias;
            Sev[i] = sev;
            Comp[i] = comp;
            Thread[i] = thread;
            CFile[i] = cfile;
            Src[i] = (ushort)src.Index;
            Line[i] = src.CurLine;
            SevCount[sev]++;
            if (comp >= 0) CompCount[comp]++;
            src.Entries++;
            Count = i + 1;
        }

        public void Trim(int headroom)
        {
            int cap = Count + headroom;
            if (cap >= Cap) return;
            Array.Resize(ref Msg, cap);
            Array.Resize(ref Time, cap);
            Array.Resize(ref Key, cap);
            Array.Resize(ref Bias, cap);
            Array.Resize(ref Sev, cap);
            Array.Resize(ref Comp, cap);
            Array.Resize(ref Thread, cap);
            Array.Resize(ref CFile, cap);
            Array.Resize(ref Src, cap);
            Array.Resize(ref Line, cap);
            Cap = cap;
        }

        public void Absorb(Store o)
        {
            int n = o.Count;
            if (n == 0) return;
            int[] cmap = new int[o.CompNames.Count];
            for (int c = 0; c < cmap.Length; c++) cmap[c] = CompId(o.CompNames[c]);
            int[] fmap = new int[o.CFNames.Count];
            for (int c = 0; c < fmap.Length; c++) fmap[c] = CFileId(o.CFNames[c]);
            while (Cap < Count + n) Grow();
            int b = Count;
            Array.Copy(o.Msg, 0, Msg, b, n);
            Array.Copy(o.Time, 0, Time, b, n);
            Array.Copy(o.Key, 0, Key, b, n);
            Array.Copy(o.Bias, 0, Bias, b, n);
            Array.Copy(o.Sev, 0, Sev, b, n);
            Array.Copy(o.Thread, 0, Thread, b, n);
            Array.Copy(o.Src, 0, Src, b, n);
            Array.Copy(o.Line, 0, Line, b, n);
            for (int i = 0; i < n; i++)
            {
                int c = o.Comp[i];
                Comp[b + i] = c >= 0 ? cmap[c] : -1;
                int f = o.CFile[i];
                CFile[b + i] = f >= 0 ? fmap[f] : -1;
            }
            for (int v = 0; v < SevCount.Length; v++) SevCount[v] += o.SevCount[v];
            for (int c = 0; c < cmap.Length; c++) CompCount[cmap[c]] += o.CompCount[c];
            Count = b + n;
        }

        public int CompId(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int id;
            if (!compMap.TryGetValue(name, out id))
            {
                id = CompNames.Count;
                CompNames.Add(name);
                CompCount.Add(0);
                compMap[name] = id;
            }
            return id;
        }

        public int CompIndex(string name)
        {
            int id;
            return name != null && compMap.TryGetValue(name, out id) ? id : -1;
        }

        int lastComp = -1, lastCf = -1;

        public int CompIdAt(string b, int a, int e)
        {
            int len = e - a;
            if (len <= 0) return -1;
            if (lastComp >= 0)
            {
                string c = CompNames[lastComp];
                if (c.Length == len && string.CompareOrdinal(b, a, c, 0, len) == 0) return lastComp;
            }
            lastComp = CompId(b.Substring(a, len));
            return lastComp;
        }

        public int CFileIdAt(string b, int a, int e)
        {
            int len = e - a;
            if (len <= 0) return -1;
            if (lastCf >= 0)
            {
                string c = CFNames[lastCf];
                if (c.Length == len && string.CompareOrdinal(b, a, c, 0, len) == 0) return lastCf;
            }
            lastCf = CFileId(b.Substring(a, len));
            return lastCf;
        }

        public int CFileId(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int id;
            if (!cfMap.TryGetValue(name, out id))
            {
                id = CFNames.Count;
                CFNames.Add(name);
                cfMap[name] = id;
            }
            return id;
        }

        public void BuildOrder()
        {
            int n = Count;
            Order = new int[Math.Max(n, 16)];
            Rank = new int[Math.Max(n, 16)];
            int k = Sources.Count;
            if (k <= 1)
            {
                for (int i = 0; i < n; i++) { Order[i] = i; Rank[i] = i; }
                return;
            }
            int[] cnt = new int[k];
            for (int i = 0; i < n; i++) cnt[Src[i]]++;
            int[][] lists = new int[k][];
            for (int s = 0; s < k; s++) lists[s] = new int[cnt[s]];
            int[] fill = new int[k];
            for (int i = 0; i < n; i++) { int s = Src[i]; lists[s][fill[s]++] = i; }
            int[] head = new int[k];
            for (int o = 0; o < n; o++)
            {
                int best = -1;
                long bk = 0;
                for (int s = 0; s < k; s++)
                {
                    if (head[s] < cnt[s])
                    {
                        long kk = Key[lists[s][head[s]]];
                        if (best < 0 || kk < bk) { best = s; bk = kk; }
                    }
                }
                int e = lists[best][head[best]++];
                Order[o] = e;
                Rank[e] = o;
            }
        }

        public void AppendOrder(int from)
        {
            int n = Count;
            if (Order.Length < n)
            {
                int cap = Math.Max(n, Order.Length * 2);
                Array.Resize(ref Order, cap);
                Array.Resize(ref Rank, cap);
            }
            int m = n - from;
            if (m <= 0) return;
            if (Sources.Count <= 1)
            {
                for (int i = from; i < n; i++) { Order[i] = i; Rank[i] = i; }
                return;
            }
            int[] idx = new int[m];
            for (int j = 0; j < m; j++) idx[j] = from + j;
            long[] key = Key;
            Array.Sort(idx, delegate (int a, int b)
            {
                int c = key[a].CompareTo(key[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (int j = 0; j < m; j++) { Order[from + j] = idx[j]; Rank[idx[j]] = from + j; }
        }
    }

    public static class ViewFilter
    {
        const int Chunk = 1 << 16;

        public delegate int RangeFilter(int from, int to, int[] output);

        public static int[] Run(int n, RangeFilter filter, Func<bool> cancelled, out int count)
        {
            count = 0;
            int chunks = (n + Chunk - 1) / Chunk;
            int cpus = Environment.ProcessorCount;
            if (chunks <= 1 || cpus == 1)
            {
                int[] v = new int[Math.Max(n, 16)];
                int m = 0;
                for (int a = 0; a < n; a += Chunk)
                {
                    if (cancelled != null && cancelled()) return null;
                    int b = Math.Min(n, a + Chunk);
                    int[] part = new int[b - a];
                    int c = filter(a, b, part);
                    Array.Copy(part, 0, v, m, c);
                    m += c;
                }
                count = m;
                return v;
            }
            int[][] parts = new int[chunks][];
            int[] counts = new int[chunks];
            bool stop = false;
            System.Threading.Tasks.Parallel.For(0, chunks, delegate (int c, System.Threading.Tasks.ParallelLoopState state)
            {
                if (stop || (cancelled != null && cancelled())) { stop = true; state.Stop(); return; }
                int a = c * Chunk, b = Math.Min(n, a + Chunk);
                int[] part = new int[b - a];
                counts[c] = filter(a, b, part);
                parts[c] = part;
            });
            if (stop) return null;
            int total = 0;
            for (int c = 0; c < chunks; c++) total += counts[c];
            int[] all = new int[Math.Max(n, 16)];
            int at = 0;
            for (int c = 0; c < chunks; c++)
            {
                Array.Copy(parts[c], 0, all, at, counts[c]);
                at += counts[c];
            }
            count = total;
            return all;
        }

        public static int Chunks(int n)
        {
            return (n + Chunk - 1) / Chunk;
        }

        public static bool ForChunks(int n, Action<int, int, int> body, Func<bool> cancelled)
        {
            int chunks = Chunks(n);
            if (chunks <= 1 || Environment.ProcessorCount == 1)
            {
                for (int c = 0; c < chunks; c++)
                {
                    if (cancelled != null && cancelled()) return false;
                    body(c, c * Chunk, Math.Min(n, (c + 1) * Chunk));
                }
                return true;
            }
            bool stop = false;
            System.Threading.Tasks.Parallel.For(0, chunks, delegate (int c, System.Threading.Tasks.ParallelLoopState state)
            {
                if (stop || (cancelled != null && cancelled())) { stop = true; state.Stop(); return; }
                body(c, c * Chunk, Math.Min(n, (c + 1) * Chunk));
            });
            return !stop;
        }

        public static int Count(Store st, Regex rx)
        {
            string[] msg = st.Msg;
            int[] order = st.Order;
            int count;
            Run(st.Count, delegate (int a, int b, int[] o)
            {
                int m = 0;
                for (int k = a; k < b; k++) { int i = order[k]; if (rx.IsMatch(msg[i])) o[m++] = i; }
                return m;
            }, null, out count);
            return count;
        }
    }

    public static class Parser
    {
        static readonly Regex RxCbs = new Regex(@"^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2}), (\w+)\s+(\S+)(?:\s+(.*))?$", RegexOptions.Compiled);
        static readonly Regex RxTs = new Regex(@"^\[?(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})[ T](\d{1,2}):(\d{2}):(\d{2})(?:[.,](\d{1,7}))?", RegexOptions.Compiled);
        static readonly Regex RxSms = new Regex(@"\$\$<[^>\n]*><\d{1,2}-\d{1,2}-\d{4} \d", RegexOptions.Compiled);

        public static string FormatName(string f)
        {
            switch (f)
            {
                case "cm": return "CMTrace";
                case "sms": return "SMS trace";
                case "cbs": return "CBS / Panther";
                case "etl": return "ETW trace (ETL)";
                case "evt": return "Windows event log";
                default: return "Plain text";
            }
        }

        public static string Detect(string buf)
        {
            string s = buf.Length > 65536 ? buf.Substring(0, 65536) : buf;
            if (s.IndexOf("<![LOG[", StringComparison.Ordinal) >= 0) return "cm";
            if (RxSms.IsMatch(s)) return "sms";
            int lines = 0, hits = 0, p = 0;
            while (p < s.Length && lines < 60)
            {
                int nl = s.IndexOf('\n', p);
                if (nl < 0) nl = s.Length;
                string line = s.Substring(p, nl - p).TrimEnd('\r');
                if (!Blank(line))
                {
                    lines++;
                    if (RxCbs.IsMatch(line)) hits++;
                }
                p = nl + 1;
            }
            return lines > 0 && hits * 10 >= lines * 3 ? "cbs" : "plain";
        }

        public static string Parse(Store st, LogSource src, string buf, bool fin)
        {
            src.ScanPos = 0;
            src.ScanLine = src.LineBase;
            string carry;
            switch (src.Format)
            {
                case "cm": carry = ParseCM(st, src, buf, fin); break;
                case "sms": carry = ParseLines(st, src, buf, fin, 1); break;
                case "cbs": carry = ParseLines(st, src, buf, fin, 2); break;
                default: carry = ParseLines(st, src, buf, fin, 0); break;
            }
            At(src, buf, buf.Length - carry.Length);
            src.LineBase = src.ScanLine;
            return carry;
        }

        static void At(LogSource src, string buf, int pos)
        {
            int p = src.ScanPos, n = src.ScanLine;
            if (pos > buf.Length) pos = buf.Length;
            if (pos <= p)
            {
                src.CurLine = n + 1;
                return;
            }
            while (p < pos)
            {
                int nl = buf.IndexOf('\n', p, pos - p);
                if (nl < 0) break;
                n++;
                p = nl + 1;
            }
            src.ScanPos = pos;
            src.ScanLine = n;
            src.CurLine = n + 1;
        }

        public static string ForceLine(Store st, LogSource src, string carry)
        {
            int nl = carry.IndexOf('\n');
            if (nl < 0) return carry;
            src.CurLine = src.LineBase + 1;
            PlainLine(st, src, carry.Substring(0, nl).TrimEnd('\r'));
            src.LineBase++;
            return carry.Substring(nl + 1);
        }

        static string ParseCM(Store st, LogSource src, string buf, bool fin)
        {
            int n = buf.Length, p = 0;
            while (p < n)
            {
                int s = buf.IndexOf("<![LOG[", p, StringComparison.Ordinal);
                if (s < 0)
                {
                    if (fin)
                    {
                        PlainRegion(st, src, buf, p, n);
                        p = n;
                    }
                    else if (n - 8 >= p)
                    {
                        int nl = buf.LastIndexOf('\n', n - 8);
                        if (nl >= p)
                        {
                            PlainRegion(st, src, buf, p, nl);
                            p = nl + 1;
                        }
                    }
                    break;
                }
                if (s > p) PlainRegion(st, src, buf, p, s);
                int me = buf.IndexOf("]LOG]!>", s + 7, StringComparison.Ordinal);
                int ae = me < 0 ? -1 : buf.IndexOf("\">", me + 7, StringComparison.Ordinal);
                if (ae < 0)
                {
                    if (fin)
                    {
                        PlainRegion(st, src, buf, s, n);
                        p = n;
                    }
                    else p = s;
                    break;
                }
                int a0 = me + 7, a1 = ae + 1;
                At(src, buf, s);
                CmAttrs at;
                ReadAttrs(buf, a0, a1, out at);
                short bias = Store.NoBias;
                long t = Store.NoTime;
                if (at.TimeB > at.TimeA && at.DateB > at.DateA) t = CmTime(buf, at.TimeA, at.TimeB, at.DateA, at.DateB, out bias);
                int tn;
                if (!Num(buf, at.ThreadA, at.ThreadB, out tn)) tn = -1;
                byte sev = 1;
                if (at.TypeB == at.TypeA + 1)
                {
                    char c = buf[at.TypeA];
                    if (c == '3') sev = 3;
                    else if (c == '2') sev = 2;
                }
                int comp = st.CompIdAt(buf, at.CompA, at.CompB);
                int cf = at.FileB > at.FileA ? st.CFileIdAt(buf, at.FileA, at.FileB) : -1;
                st.Add(src, buf.Substring(s + 7, me - s - 7), t, bias, sev, comp, tn, cf);
                p = ae + 2;
                while (p < n && (buf[p] == '\r' || buf[p] == '\n')) p++;
            }
            return p >= n ? "" : buf.Substring(p);
        }

        struct CmAttrs
        {
            public int TimeA, TimeB, DateA, DateB, CompA, CompB, TypeA, TypeB, ThreadA, ThreadB, FileA, FileB;
        }

        static void ReadAttrs(string b, int a0, int a1, out CmAttrs at)
        {
            if (!FastAttrs(b, a0, a1, out at)) SlowAttrs(b, a0, a1, out at);
        }

        static void SlowAttrs(string b, int a0, int a1, out CmAttrs at)
        {
            at = new CmAttrs();
            AttrPos(b, "time=\"", a0, a1, out at.TimeA, out at.TimeB);
            AttrPos(b, "date=\"", a0, a1, out at.DateA, out at.DateB);
            AttrPos(b, "type=\"", a0, a1, out at.TypeA, out at.TypeB);
            AttrPos(b, "thread=\"", a0, a1, out at.ThreadA, out at.ThreadB);
            AttrPos(b, "file=\"", a0, a1, out at.FileA, out at.FileB);
            AttrPos(b, "component=\"", a0, a1, out at.CompA, out at.CompB);
        }

        static void AttrPos(string b, string key, int a0, int a1, out int va, out int vb)
        {
            va = vb = 0;
            if (a1 <= a0) return;
            int i = b.IndexOf(key, a0, a1 - a0, StringComparison.Ordinal);
            if (i < 0) return;
            int v = i + key.Length;
            if (v >= a1) return;
            int e = b.IndexOf('"', v, a1 - v);
            if (e < 0) return;
            va = v;
            vb = e;
        }

        static bool FastAttrs(string b, int a0, int a1, out CmAttrs at)
        {
            at = new CmAttrs();
            int q = a0;
            while (q < a1)
            {
                while (q < a1 && (b[q] == ' ' || b[q] == '<' || b[q] == '\t')) q++;
                if (q >= a1 || b[q] == '>') return true;
                int eq = b.IndexOf('=', q, a1 - q);
                if (eq < 0 || eq + 1 >= a1 || b[eq + 1] != '"') return false;
                int vs = eq + 2;
                int ve = vs < a1 ? b.IndexOf('"', vs, a1 - vs) : -1;
                if (ve < 0) break;
                int ks = eq;
                while (ks > q && IsKeyChar(b[ks - 1])) ks--;
                if (ks != q) return false;
                int kl = eq - ks;
                if (kl == 4)
                {
                    if (Key(b, ks, "time")) { at.TimeA = vs; at.TimeB = ve; }
                    else if (Key(b, ks, "date")) { at.DateA = vs; at.DateB = ve; }
                    else if (Key(b, ks, "type")) { at.TypeA = vs; at.TypeB = ve; }
                    else if (Key(b, ks, "file")) { at.FileA = vs; at.FileB = ve; }
                }
                else if (kl == 6) { if (Key(b, ks, "thread")) { at.ThreadA = vs; at.ThreadB = ve; } }
                else if (kl == 9) { if (Key(b, ks, "component")) { at.CompA = vs; at.CompB = ve; } }
                q = ve + 1;
            }
            return true;
        }

        static bool IsKeyChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        static bool Key(string b, int at, string key)
        {
            return string.CompareOrdinal(b, at, key, 0, key.Length) == 0;
        }

        static string ParseLines(Store st, LogSource src, string buf, bool fin, int kind)
        {
            int p = 0, n = buf.Length;
            while (true)
            {
                int nl = buf.IndexOf('\n', p);
                if (nl < 0) break;
                int e = nl;
                if (e > p && buf[e - 1] == '\r') e--;
                At(src, buf, p);
                Line(st, src, buf.Substring(p, e - p), kind);
                p = nl + 1;
            }
            if (fin)
            {
                if (p < n)
                {
                    int e = n;
                    if (buf[e - 1] == '\r') e--;
                    At(src, buf, p);
                    if (e > p) Line(st, src, buf.Substring(p, e - p), kind);
                }
                return "";
            }
            return p >= n ? "" : buf.Substring(p);
        }

        static void Line(Store st, LogSource src, string line, int kind)
        {
            if (kind == 1) SmsLine(st, src, line);
            else if (kind == 2) CbsLine(st, src, line);
            else PlainLine(st, src, line);
        }

        static void PlainRegion(Store st, LogSource src, string buf, int a, int b)
        {
            int p = a;
            while (p < b)
            {
                int nl = buf.IndexOf('\n', p, b - p);
                if (nl < 0) nl = b;
                int e = nl;
                if (e > p && buf[e - 1] == '\r') e--;
                At(src, buf, p);
                if (e > p) PlainLine(st, src, buf.Substring(p, e - p));
                p = nl + 1;
            }
        }

        static void PlainLine(Store st, LogSource src, string line)
        {
            if (Blank(line)) return;
            long t = Store.NoTime;
            char c0 = line[0];
            if ((c0 >= '0' && c0 <= '9') || c0 == '[')
            {
                Match m = RxTs.Match(line);
                if (m.Success) t = MakeTicks(G(m, 1), G(m, 2), G(m, 3), G(m, 4), G(m, 5), G(m, 6), m.Groups[7].Success ? Frac(m.Groups[7].Value) : 0);
            }
            st.Add(src, line, t, Store.NoBias, Sev(line), -1, -1, -1);
        }

        static void SmsLine(Store st, LogSource src, string line)
        {
            int k = line.LastIndexOf("$$<", StringComparison.Ordinal);
            int i1 = k < 0 ? -1 : line.IndexOf("><", k + 3, StringComparison.Ordinal);
            int i2 = i1 < 0 ? -1 : line.IndexOf("><", i1 + 2, StringComparison.Ordinal);
            if (i2 < 0)
            {
                PlainLine(st, src, line);
                return;
            }
            string comp = line.Substring(k + 3, i1 - k - 3);
            string dt = line.Substring(i1 + 2, i2 - i1 - 2);
            string rest = line.Substring(i2 + 2);
            string msg = line.Substring(0, k).TrimEnd();
            short bias = Store.NoBias;
            long t = Store.NoTime;
            int sp = dt.IndexOf(' ');
            if (sp > 0) t = CmTime(dt, sp + 1, dt.Length, 0, sp, out bias);
            int th = -1;
            if (rest.StartsWith("thread=", StringComparison.Ordinal))
            {
                int e = 7;
                while (e < rest.Length && rest[e] >= '0' && rest[e] <= '9') e++;
                int v;
                if (Num(rest, 7, e, out v)) th = v;
            }
            st.Add(src, msg, t, bias, Sev(msg), st.CompId(comp), th, -1);
        }

        static void CbsLine(Store st, LogSource src, string line)
        {
            Match m = RxCbs.Match(line);
            if (!m.Success)
            {
                PlainLine(st, src, line);
                return;
            }
            string msg = m.Groups[9].Success ? m.Groups[9].Value : "";
            string lv = m.Groups[7].Value;
            byte sev;
            if (string.Equals(lv, "Error", StringComparison.OrdinalIgnoreCase)) sev = 3;
            else if (string.Equals(lv, "Warning", StringComparison.OrdinalIgnoreCase)) sev = 2;
            else sev = Sev(msg);
            long t = MakeTicks(G(m, 1), G(m, 2), G(m, 3), G(m, 4), G(m, 5), G(m, 6), 0);
            st.Add(src, msg, t, Store.NoBias, sev, st.CompId(m.Groups[8].Value), -1, -1);
        }

        static long CmTime(string b, int ta, int tb, int da0, int db, out short bias)
        {
            bias = Store.NoBias;
            int d1 = b.IndexOf('-', da0, db - da0);
            if (d1 < 0) return Store.NoTime;
            int d2 = d1 + 1 < db ? b.IndexOf('-', d1 + 1, db - d1 - 1) : -1;
            if (d2 < 0) return Store.NoTime;
            int mo, da, yr;
            if (!Num(b, da0, d1, out mo) || !Num(b, d1 + 1, d2, out da) || !Num(b, d2 + 1, db, out yr)) return Store.NoTime;
            return Clock(b, ta, tb, yr, mo, da, out bias);
        }

        static long Clock(string t, int a, int len, int yr, int mo, int da, out short bias)
        {
            bias = Store.NoBias;
            if (len <= a) return Store.NoTime;
            int c1 = t.IndexOf(':', a, len - a);
            if (c1 < 0) return Store.NoTime;
            int c2 = c1 + 1 < len ? t.IndexOf(':', c1 + 1, len - c1 - 1) : -1;
            if (c2 < 0) return Store.NoTime;
            int hh, mi;
            if (!Num(t, a, c1, out hh) || !Num(t, c1 + 1, c2, out mi)) return Store.NoTime;
            int j = c2 + 1, ss = 0, sd = 0;
            while (j < len && t[j] >= '0' && t[j] <= '9') { ss = ss * 10 + (t[j] - '0'); j++; sd++; }
            if (sd == 0 || sd > 2) return Store.NoTime;
            long frac = 0;
            if (j < len && (t[j] == '.' || t[j] == ','))
            {
                j++;
                int dg = 0;
                while (j < len && t[j] >= '0' && t[j] <= '9')
                {
                    if (dg < 7) { frac = frac * 10 + (t[j] - '0'); dg++; }
                    j++;
                }
                while (dg < 7) { frac *= 10; dg++; }
            }
            if (j < len)
            {
                int sign = 1, k = j;
                if (t[k] == '+') k++;
                else if (t[k] == '-') { sign = -1; k++; }
                int b;
                if (Num(t, k, len, out b) && b < 1440) bias = (short)(sign * b);
            }
            return MakeTicks(yr, mo, da, hh, mi, ss, frac);
        }

        static long MakeTicks(int y, int mo, int d, int h, int mi, int s, long frac)
        {
            if (y < 1 || y > 9999 || mo < 1 || mo > 12 || d < 1 || d > DateTime.DaysInMonth(y, mo) || h < 0 || h > 23 || mi < 0 || mi > 59 || s < 0 || s > 59) return Store.NoTime;
            return new DateTime(y, mo, d, h, mi, s).Ticks + frac;
        }

        static long Frac(string digits)
        {
            long f = 0;
            int dg = 0;
            for (int i = 0; i < digits.Length && dg < 7; i++, dg++) f = f * 10 + (digits[i] - '0');
            while (dg < 7) { f *= 10; dg++; }
            return f;
        }

        static int G(Match m, int g)
        {
            string s = m.Groups[g].Value;
            int v;
            return Num(s, 0, s.Length, out v) ? v : -1;
        }

        static bool Num(string s, int a, int b, out int v)
        {
            v = 0;
            if (b <= a || b - a > 9) return false;
            for (int i = a; i < b; i++)
            {
                char c = s[i];
                if (c < '0' || c > '9') return false;
                v = v * 10 + (c - '0');
            }
            return true;
        }

        static bool Blank(string s)
        {
            for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) return false;
            return true;
        }

        public static byte Sev(string m)
        {
            if (m.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("return value 3", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (m.IndexOf("warn", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            return 1;
        }
    }

    public static class Loader
    {
        public static Encoding Detect(byte[] h, int n, out int bom, out string name)
        {
            bom = 0;
            if (n >= 3 && h[0] == 0xEF && h[1] == 0xBB && h[2] == 0xBF) { bom = 3; name = "UTF-8"; return new UTF8Encoding(false); }
            if (n >= 2 && h[0] == 0xFF && h[1] == 0xFE) { bom = 2; name = "UTF-16 LE"; return new UnicodeEncoding(false, false); }
            if (n >= 2 && h[0] == 0xFE && h[1] == 0xFF) { bom = 2; name = "UTF-16 BE"; return new UnicodeEncoding(true, false); }
            int len = Math.Min(n, 4096), zo = 0, ze = 0;
            for (int i = 0; i < len; i++) if (h[i] == 0) { if ((i & 1) == 1) zo++; else ze++; }
            if (len >= 16 && zo > len / 5 && ze < len / 20) { name = "UTF-16 LE"; return new UnicodeEncoding(false, false); }
            if (len >= 16 && ze > len / 5 && zo < len / 20) { name = "UTF-16 BE"; return new UnicodeEncoding(true, false); }
            try
            {
                Decoder d = new UTF8Encoding(false, true).GetDecoder();
                char[] tmp = new char[n + 4];
                d.GetChars(h, 0, n, tmp, 0, false);
                name = "UTF-8";
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                name = "ANSI 1252";
                return Encoding.GetEncoding(1252);
            }
        }

        public static void ReadAll(Store st, LogSource src, LoadToken tok, Action<long> progress)
        {
            if (EventLogs.IsEvent(src.Path))
            {
                EventLogs.Read(st, src, tok, progress);
                return;
            }
            if (src.Path.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
            {
                EtlReader.Read(st, src, tok, progress);
                return;
            }
            FileStream fs = new FileStream(src.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
            src.Fs = fs;
            src.Size = fs.Length;
            byte[] head = new byte[65536];
            int hn = 0;
            while (hn < head.Length)
            {
                int r = fs.Read(head, hn, head.Length - hn);
                if (r <= 0) break;
                hn += r;
            }
            int bom;
            string encName;
            src.Enc = Detect(head, hn, out bom, out encName);
            src.EncName = encName;
            fs.Position = bom;
            src.Dec = src.Enc.GetDecoder();
            byte[] bb = new byte[4 << 20];
            char[] cb = new char[src.Enc.GetMaxCharCount(bb.Length)];
            string carry = "";
            while (tok.State == 0)
            {
                int nb = fs.Read(bb, 0, bb.Length);
                if (nb <= 0) break;
                int nc = src.Dec.GetChars(bb, 0, nb, cb, 0, false);
                string text = new string(cb, 0, nc);
                string buf = carry.Length > 0 ? carry + text : text;
                if (src.Format == null) src.Format = Parser.Detect(buf);
                carry = Parser.Parse(st, src, buf, false);
                if (carry.Length > (32 << 20)) carry = Parser.ForceLine(st, src, carry);
                src.Pos = fs.Position;
                progress(src.Pos);
            }
            if (tok.State != 0) src.Partial = true;
            if (src.Format == null) src.Format = "plain";
            if (carry.Length > 0 && src.Format != "cm") carry = Parser.Parse(st, src, carry, true);
            src.Carry = carry;
            src.Pos = fs.Position;
        }

        public static void ReadMore(Store st, LogSource src)
        {
            FileStream fs = src.Fs;
            fs.Position = src.Pos;
            byte[] bb = new byte[1 << 20];
            char[] cb = new char[src.Enc.GetMaxCharCount(bb.Length)];
            long budget = 64L << 20;
            while (budget > 0)
            {
                int nb = fs.Read(bb, 0, bb.Length);
                if (nb <= 0) break;
                budget -= nb;
                int nc = src.Dec.GetChars(bb, 0, nb, cb, 0, false);
                string buf = src.Carry + new string(cb, 0, nc);
                if (src.Format == null) src.Format = Parser.Detect(buf);
                src.Carry = Parser.Parse(st, src, buf, false);
            }
            src.Pos = fs.Position;
        }
    }

    public static class Codes
    {
        public sealed class Info
        {
            public string Title;
            public string Sub;
            public string Text;
            public string Foot;
        }

        static readonly Dictionary<uint, string> Hr = new Dictionary<uint, string>
        {
            { 0x80004001, "E_NOTIMPL: Not implemented" },
            { 0x80004002, "E_NOINTERFACE: No such interface supported" },
            { 0x80004003, "E_POINTER: Invalid pointer" },
            { 0x80004004, "E_ABORT: Operation aborted" },
            { 0x80004005, "E_FAIL: Unspecified error" },
            { 0x8000FFFF, "E_UNEXPECTED: Catastrophic failure" },
            { 0x80040154, "REGDB_E_CLASSNOTREG: Class not registered" },
            { 0x800401F0, "CO_E_NOTINITIALIZED: CoInitialize has not been called" },
            { 0x80041001, "WBEM_E_FAILED: WMI call failed" },
            { 0x80041002, "WBEM_E_NOT_FOUND: WMI object not found" },
            { 0x80041003, "WBEM_E_ACCESS_DENIED: WMI access denied" },
            { 0x80041006, "WBEM_E_OUT_OF_MEMORY: WMI out of memory" },
            { 0x8004100A, "WBEM_E_CRITICAL_ERROR: WMI critical error" },
            { 0x8004100C, "WBEM_E_NOT_SUPPORTED: Operation not supported by WMI" },
            { 0x8004100E, "WBEM_E_INVALID_NAMESPACE: Invalid WMI namespace" },
            { 0x80041010, "WBEM_E_INVALID_CLASS: Invalid WMI class" },
            { 0x80041011, "WBEM_E_PROVIDER_NOT_FOUND: WMI provider not found" },
            { 0x80041013, "WBEM_E_PROVIDER_LOAD_FAILURE: WMI provider could not be loaded" },
            { 0x80041014, "WBEM_E_INITIALIZATION_FAILURE: WMI component failed to initialize" },
            { 0x80041017, "WBEM_E_INVALID_QUERY: Invalid WMI query" },
            { 0x80041032, "WBEM_E_CALL_CANCELLED: WMI call cancelled" },
            { 0x80041033, "WBEM_E_SHUTTING_DOWN: WMI is shutting down" },
            { 0x800B0101, "CERT_E_EXPIRED: Certificate expired or not yet valid" },
            { 0x800B0109, "CERT_E_UNTRUSTEDROOT: Chain ends in an untrusted root" },
            { 0x80092012, "CRYPT_E_NO_REVOCATION_CHECK: Revocation could not be checked" },
            { 0x80092013, "CRYPT_E_REVOCATION_OFFLINE: Revocation server was offline" },
            { 0x80090016, "NTE_BAD_KEYSET: Keyset does not exist" },
            { 0x80090308, "SEC_E_INVALID_TOKEN: The token supplied is invalid" },
            { 0x8009030C, "SEC_E_LOGON_DENIED: The logon attempt failed" },
            { 0x8009030D, "SEC_E_UNKNOWN_CREDENTIALS: Credentials were not recognized" },
            { 0x80090322, "SEC_E_WRONG_PRINCIPAL: The target principal name is incorrect" },
            { 0x80090325, "SEC_E_UNTRUSTED_ROOT: Certificate chain issued by an untrusted authority" },
            { 0x80090327, "SEC_E_CERT_UNKNOWN: Unknown error processing the certificate" },
            { 0x800F081F, "CBS_E_SOURCE_MISSING: Source files could not be found" },
            { 0x800F0831, "CBS_E_STORE_CORRUPTION: Component store corruption" },
            { 0x800F0922, "CBS_E_INSTALLERS_FAILED: Advanced installers or generic commands failed" },
            { 0x8024000B, "WU_E_CALL_CANCELLED: Operation was cancelled" },
            { 0x80240016, "WU_E_INSTALL_NOT_ALLOWED: Another install in progress or restart pending" },
            { 0x80240017, "WU_E_NOT_APPLICABLE: No applicable updates" },
            { 0x8024001E, "WU_E_SERVICE_STOP: Service or system was shutting down" },
            { 0x80240020, "WU_E_NO_INTERACTIVE_USER: No interactive user logged on" },
            { 0x80240022, "WU_E_ALL_UPDATES_FAILED: Operation failed for all updates" },
            { 0x80242016, "WU_E_UH_POSTREBOOTUNEXPECTEDSTATE: Unexpected update state after restart" },
            { 0x80244010, "WU_E_PT_EXCEEDED_MAX_SERVER_TRIPS: Too many round trips to the server" },
            { 0x80244017, "WU_E_PT_HTTP_STATUS_DENIED: HTTP 401 access denied" },
            { 0x80244018, "WU_E_PT_HTTP_STATUS_FORBIDDEN: HTTP 403 forbidden" },
            { 0x80244019, "WU_E_PT_HTTP_STATUS_NOT_FOUND: HTTP 404 not found" },
            { 0x8024401B, "WU_E_PT_HTTP_STATUS_PROXY_AUTH_REQ: HTTP 407 proxy authentication required" },
            { 0x8024401C, "WU_E_PT_HTTP_STATUS_REQUEST_TIMEOUT: HTTP 408 request timeout" },
            { 0x8024401F, "WU_E_PT_HTTP_STATUS_SERVER_ERROR: HTTP 500 server error" },
            { 0x80244021, "WU_E_PT_HTTP_STATUS_BAD_GATEWAY: HTTP 502 bad gateway" },
            { 0x80244022, "WU_E_PT_HTTP_STATUS_SERVICE_UNAVAIL: HTTP 503 service unavailable" },
            { 0x80244023, "WU_E_PT_HTTP_STATUS_GATEWAY_TIMEOUT: HTTP 504 gateway timeout" },
            { 0x8024402C, "WU_E_PT_WINHTTP_NAME_NOT_RESOLVED: Proxy or server name could not be resolved" },
            { 0x80246007, "WU_E_DM_NOTDOWNLOADED: The update has not been downloaded" },
            { 0x87D00324, "The application was not detected after installation completed" },
            { 0x87D00325, "The application was still detected after uninstall completed" },
            { 0x87D00607, "Content not found" },
            { 0x87D00668, "Software update still detected as actionable after apply" },
            { 0x87D01106, "Failed to verify the executable file is valid or to construct the command line" },
            { 0x87D1041C, "The application was not detected after installation completed (Intune)" },
            { 0x00C0FFEE, "Out of coffee. Refill and retry the operation." },
            { 0xDEADBEEF, "Magic debug value used to mark freed or uninitialised memory. Not a Windows error." },
            { 0xBAADF00D, "Fill pattern the Windows debug heap uses for allocated but uninitialised memory. Not a Windows error." },
            { 0xCAFEBABE, "The first four bytes of every Java class file. Not a Windows error." },
            { 0xFEEDFACE, "Magic number at the start of a 32-bit Mach-O executable on macOS. Not a Windows error." },
            { 0x8BADF00D, "Apple's code for an iOS app ended by the watchdog for taking too long. Not a Windows error." }
        };

        static readonly Dictionary<int, string> W32 = new Dictionary<int, string>
        {
            { 0, "The operation completed successfully" },
            { 1, "Incorrect function" },
            { 2, "The system cannot find the file specified" },
            { 3, "The system cannot find the path specified" },
            { 5, "Access is denied" },
            { 6, "The handle is invalid" },
            { 8, "Not enough memory resources are available to process this command" },
            { 13, "The data is invalid" },
            { 14, "Not enough memory resources are available to complete this operation" },
            { 21, "The device is not ready" },
            { 23, "Data error (cyclic redundancy check)" },
            { 31, "A device attached to the system is not functioning" },
            { 32, "The process cannot access the file because it is being used by another process" },
            { 50, "The request is not supported" },
            { 53, "The network path was not found" },
            { 64, "The specified network name is no longer available" },
            { 67, "The network name cannot be found" },
            { 80, "The file exists" },
            { 87, "The parameter is incorrect" },
            { 112, "There is not enough space on the disk" },
            { 121, "The semaphore timeout period has expired" },
            { 126, "The specified module could not be found" },
            { 127, "The specified procedure could not be found" },
            { 183, "Cannot create a file when that file already exists" },
            { 193, "Not a valid Win32 application" },
            { 258, "The wait operation timed out" },
            { 995, "The I/O operation was aborted by a thread exit or an application request" },
            { 998, "Invalid access to memory location" },
            { 1053, "The service did not respond to the start or control request in a timely fashion" },
            { 1058, "The service cannot be started because it is disabled" },
            { 1060, "The specified service does not exist as an installed service" },
            { 1062, "The service has not been started" },
            { 1067, "The process terminated unexpectedly" },
            { 1068, "The dependency service or group failed to start" },
            { 1069, "The service did not start due to a logon failure" },
            { 1117, "The request failed because of an I/O device error" },
            { 1168, "Element not found" },
            { 1219, "Multiple connections to a server or share by the same user are not allowed" },
            { 1223, "The operation was canceled by the user" },
            { 1231, "The network location cannot be reached" },
            { 1311, "There are currently no logon servers available" },
            { 1314, "A required privilege is not held by the client" },
            { 1326, "The user name or password is incorrect" },
            { 1355, "The specified domain does not exist or could not be contacted" },
            { 1385, "The user has not been granted the requested logon type at this computer" },
            { 1392, "The file or directory is corrupted and unreadable" },
            { 1450, "Insufficient system resources exist to complete the requested service" },
            { 1460, "The timeout period expired" },
            { 1602, "MSI: User cancelled installation" },
            { 1603, "MSI: Fatal error during installation" },
            { 1605, "MSI: Action only valid for products that are currently installed" },
            { 1612, "MSI: The installation source for this product is not available" },
            { 1618, "MSI: Another installation is already in progress" },
            { 1619, "MSI: This installation package could not be opened" },
            { 1620, "MSI: Package could not be opened (not a valid Windows Installer package)" },
            { 1624, "MSI: Error applying transforms" },
            { 1625, "MSI: This installation is forbidden by system policy" },
            { 1633, "MSI: Package not supported by this processor type" },
            { 1638, "MSI: Another version of this product is already installed" },
            { 1641, "MSI: Success, the installer has started a restart" },
            { 1722, "The RPC server is unavailable" },
            { 3010, "Success, a restart is required to complete the install" },
            { 3017, "Failed, a restart is required to roll back changes" },
            { 10060, "Connection timed out (the remote host did not respond)" },
            { 10061, "Connection refused by the target machine" },
            { 12002, "WinHTTP / WinINet: The operation timed out" },
            { 12007, "WinHTTP / WinINet: The server name or address could not be resolved" },
            { 12029, "WinHTTP / WinINet: A connection with the server could not be established" },
            { 12175, "WinHTTP: A security error occurred (TLS or certificate)" }
        };

        static readonly Dictionary<int, string> Fac = new Dictionary<int, string>
        {
            { 0, "Null" }, { 1, "RPC" }, { 2, "Dispatch" }, { 3, "Storage" }, { 4, "COM / WMI" }, { 7, "Win32" },
            { 8, "Windows" }, { 9, "Security / Crypto" }, { 10, "Control" }, { 11, "Certificates" }, { 15, "Setup API / CBS" },
            { 24, "MDM enrollment" }, { 36, "Windows Update" }, { 2000, "Configuration Manager" }, { 2001, "ConfigMgr / Intune client" }
        };

        static readonly Regex RxHex = new Regex(@"0x([0-9a-fA-F]{8})(?![0-9a-fA-F])", RegexOptions.Compiled);
        static readonly Regex RxNeg = new Regex(@"(?<![\w.])-([12]\d{9})(?!\d)", RegexOptions.Compiled);
        static readonly Regex RxExit = new Regex(@"exit ?code[^\d\-\n]{0,12}(-?\d{1,10})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Dictionary<string, IntPtr> modules = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);

        public static List<uint> Find(string msg)
        {
            List<uint> list = new List<uint>();
            if (string.IsNullOrEmpty(msg)) return list;
            string s = msg.Length > 20000 ? msg.Substring(0, 20000) : msg;
            foreach (Match m in RxHex.Matches(s))
            {
                uint u;
                if (!uint.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u)) continue;
                if ((u >= 0x80000000 || Hr.ContainsKey(u)) && !list.Contains(u)) list.Add(u);
                if (list.Count >= 8) return list;
            }
            foreach (Match m in RxNeg.Matches(s))
            {
                long v;
                if (!long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out v)) continue;
                v = -v;
                if (v < int.MinValue) continue;
                uint u = unchecked((uint)(int)v);
                if (!list.Contains(u)) list.Add(u);
                if (list.Count >= 8) return list;
            }
            foreach (Match m in RxExit.Matches(s))
            {
                long v;
                if (!long.TryParse(m.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v)) continue;
                uint u;
                if (v < 0 && v >= int.MinValue) u = unchecked((uint)(int)v);
                else if (v > 0 && v < 0x10000 && W32.ContainsKey((int)v)) u = (uint)v;
                else continue;
                if (!list.Contains(u)) list.Add(u);
                if (list.Count >= 8) return list;
            }
            return list;
        }

        public struct Span
        {
            public int Start;
            public int Length;
            public uint Code;
        }

        public static List<Span> Spans(string msg)
        {
            List<Span> list = new List<Span>();
            if (string.IsNullOrEmpty(msg)) return list;
            string s = msg.Length > 20000 ? msg.Substring(0, 20000) : msg;
            foreach (Match m in RxHex.Matches(s))
            {
                uint u;
                if (!uint.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u)) continue;
                if (u >= 0x80000000 || Hr.ContainsKey(u)) AddSpan(list, m.Index, m.Length, u);
            }
            foreach (Match m in RxNeg.Matches(s))
            {
                long v;
                if (!long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out v)) continue;
                v = -v;
                if (v < int.MinValue) continue;
                AddSpan(list, m.Index, m.Length, unchecked((uint)(int)v));
            }
            foreach (Match m in RxExit.Matches(s))
            {
                Group gr = m.Groups[1];
                long v;
                if (!long.TryParse(gr.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v)) continue;
                uint u;
                if (v < 0 && v >= int.MinValue) u = unchecked((uint)(int)v);
                else if (v >= 0 && v < 0x10000 && W32.ContainsKey((int)v)) u = (uint)v;
                else continue;
                AddSpan(list, gr.Index, gr.Length, u);
            }
            list.Sort(delegate (Span x, Span y) { return x.Start.CompareTo(y.Start); });
            return list;
        }

        static void AddSpan(List<Span> list, int start, int length, uint code)
        {
            foreach (Span e in list) if (start < e.Start + e.Length && e.Start < start + length) return;
            if (list.Count >= 200) return;
            Span sp = new Span();
            sp.Start = start;
            sp.Length = length;
            sp.Code = code;
            list.Add(sp);
        }

        public static uint? ParseCode(string s)
        {
            if (s == null) return null;
            s = s.Trim().Replace("(", "").Replace(")", "").Replace(" ", "");
            if (s.Length == 0) return null;
            uint u;
            long v;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (s.Length > 2 && s.Length <= 10 && uint.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u)) return u;
                return null;
            }
            if (Regex.IsMatch(s, "^[0-9a-fA-F]{1,8}$") && (Regex.IsMatch(s, "[a-fA-F]") || (s.Length == 8 && s[0] == '8')))
            {
                if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u)) return u;
            }
            if (Regex.IsMatch(s, @"^-?\d{1,10}$") && long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v) && v >= int.MinValue && v <= uint.MaxValue)
                return unchecked((uint)v);
            return null;
        }

        public static Info Describe(uint u)
        {
            Info d = new Info();
            string text;
            if (u < 0x10000)
            {
                uint hr = 0x80070000u | u;
                d.Title = u.ToString(CultureInfo.InvariantCulture);
                d.Sub = "exit / Win32 code";
                if (!W32.TryGetValue((int)u, out text)) text = SysMessage(u, null);
                d.Text = text;
                d.Foot = string.Format(CultureInfo.InvariantCulture, "As HRESULT: 0x{0:X8} ({1})", hr, unchecked((int)hr));
                return d;
            }
            int fac = (int)((u >> 16) & 0x1FFF), code = (int)(u & 0xFFFF);
            bool w32 = false;
            d.Title = string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", u);
            d.Sub = unchecked((int)u).ToString(CultureInfo.InvariantCulture);
            if (!Hr.TryGetValue(u, out text))
            {
                if (fac == 7)
                {
                    w32 = true;
                    if (!W32.TryGetValue(code, out text)) text = SysMessage((uint)code, null);
                    if (text == null && code >= 12000 && code < 12200) text = SysMessage((uint)code, "winhttp.dll");
                }
                else
                {
                    text = SysMessage(u, null);
                    if (text == null && fac == 36) text = SysMessage(u, "wuaueng.dll");
                    if (text == null && u >= 0xC0000000) text = SysMessage(u, "ntdll.dll");
                }
            }
            d.Text = text;
            string fn;
            string facText = Fac.TryGetValue(fac, out fn) ? fn + " (" + fac.ToString(CultureInfo.InvariantCulture) + ")" : fac.ToString(CultureInfo.InvariantCulture);
            d.Foot = "Facility " + facText + " \u00B7 code " + code.ToString(CultureInfo.InvariantCulture) + (w32 ? " \u00B7 Win32 " + code.ToString(CultureInfo.InvariantCulture) : "");
            return d;
        }

        public static void AppendText(StringBuilder sb, uint u)
        {
            Info d = Describe(u);
            sb.Append(d.Title).Append("   ").Append(d.Sub).Append("\r\n");
            sb.Append(d.Text ?? "Not in the built-in table or the Windows message tables").Append("\r\n");
            sb.Append(d.Foot).Append("\r\n\r\n");
        }

        static string SysMessage(uint id, string module)
        {
            try
            {
                IntPtr h = IntPtr.Zero;
                int flags = 0x200;
                if (module == null) flags |= 0x1000;
                else
                {
                    if (!modules.TryGetValue(module, out h))
                    {
                        h = NativeMethods.LoadLibraryEx(module, IntPtr.Zero, 0x2);
                        modules[module] = h;
                    }
                    if (h == IntPtr.Zero) return null;
                    flags |= 0x800;
                }
                StringBuilder sb = new StringBuilder(2048);
                int n = NativeMethods.FormatMessage(flags, h, id, 0, sb, sb.Capacity, IntPtr.Zero);
                if (n <= 0) return null;
                string s = sb.ToString().Trim();
                return s.Length == 0 ? null : s;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
