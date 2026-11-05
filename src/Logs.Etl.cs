namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class EtlReader
    {
        const uint ModeEventRecord = 0x10000000;
        const uint ErrInsufficientBuffer = 122;
        const ushort FlagStringOnly = 0x0004;
        const ushort Flag32Bit = 0x0020;
        const ushort Flag64Bit = 0x0040;
        const int PropStruct = 0x1, PropParamLength = 0x2, PropParamCount = 0x4;
        static readonly Guid EventTraceGuid = new Guid("68fdd900-4a3e-11d1-84f4-0000f80464e3");
        static readonly Regex RxInsert = new Regex(@"%(\d+)(?:![^!]*!)?", RegexOptions.Compiled);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void RecordCallback(IntPtr record);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate uint BufferCallback(IntPtr logfile);

        [DllImport("advapi32.dll", EntryPoint = "OpenTraceW", SetLastError = true)]
        static extern ulong OpenTrace(IntPtr logfile);

        [DllImport("advapi32.dll")]
        static extern uint ProcessTrace(ulong[] handles, uint count, IntPtr start, IntPtr end);

        [DllImport("advapi32.dll")]
        static extern uint CloseTrace(ulong handle);

        [DllImport("tdh.dll")]
        static extern uint TdhGetEventInformation(IntPtr record, uint contextCount, IntPtr context, IntPtr buffer, ref uint size);

        [DllImport("tdh.dll", CharSet = CharSet.Unicode)]
        static extern uint TdhGetEventMapInformation(IntPtr record, string mapName, IntPtr buffer, ref uint size);

        [DllImport("tdh.dll")]
        static extern uint TdhFormatProperty(IntPtr info, IntPtr map, uint pointerSize, ushort inType, ushort outType, ushort length, ushort userDataLength, IntPtr userData, ref uint bufferSize, IntPtr buffer, out ushort consumed);

        sealed class State
        {
            public Store St;
            public LogSource Src;
            public LoadToken Tok;
            public Action<long> Progress;
            public IntPtr Logfile;
            public IntPtr Info = IntPtr.Zero;
            public uint InfoSize;
            public IntPtr Text = IntPtr.Zero;
            public uint TextSize;
            public IntPtr Map = IntPtr.Zero;
            public uint MapSize;
            public int Seq, Skipped, Decoded;
        }

        public static void Read(Store st, LogSource src, LoadToken tok, Action<long> progress)
        {
            src.Format = "etl";
            src.EncName = "ETW binary";
            src.Size = new FileInfo(src.Path).Length;
            string temp = null;
            string path = src.Path;
            try
            {
                temp = Path.Combine(Path.GetTempPath(), "LogVice-" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".etl");
                using (FileStream fin = new FileStream(src.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (FileStream fout = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fin.CopyTo(fout, 1 << 20);
                }
                path = temp;
            }
            catch (UnauthorizedAccessException)
            {
                if (temp != null) { try { File.Delete(temp); } catch (Exception) { } }
                throw;
            }
            catch (Exception)
            {
                if (temp != null) { try { File.Delete(temp); } catch (Exception) { } }
                temp = null;
                path = src.Path;
            }
            try
            {
                Process(st, src, tok, progress, path);
            }
            finally
            {
                if (temp != null) { try { File.Delete(temp); } catch (Exception) { } }
            }
        }

        static int Ptr
        {
            get { return IntPtr.Size; }
        }

        static void Process(Store st, LogSource src, LoadToken tok, Action<long> progress, string path)
        {
            int size = Ptr == 8 ? 448 : 416;
            int modeOff = Ptr == 8 ? 28 : 20;
            int bufferCbOff = Ptr == 8 ? 400 : 384;
            int recordCbOff = Ptr == 8 ? 424 : 400;
            State s = new State();
            s.St = st;
            s.Src = src;
            s.Tok = tok;
            s.Progress = progress;
            IntPtr lf = Marshal.AllocHGlobal(size);
            IntPtr name = Marshal.StringToHGlobalUni(path);
            RecordCallback rc = delegate (IntPtr rec) { OnRecord(s, rec); };
            BufferCallback bc = delegate (IntPtr l) { return OnBuffer(s, l); };
            ulong handle = 0;
            bool opened = false;
            try
            {
                for (int i = 0; i < size; i++) Marshal.WriteByte(lf, i, 0);
                Marshal.WriteIntPtr(lf, 0, name);
                Marshal.WriteInt32(lf, modeOff, unchecked((int)ModeEventRecord));
                Marshal.WriteIntPtr(lf, bufferCbOff, Marshal.GetFunctionPointerForDelegate(bc));
                Marshal.WriteIntPtr(lf, recordCbOff, Marshal.GetFunctionPointerForDelegate(rc));
                s.Logfile = lf;
                handle = OpenTrace(lf);
                if (handle == ulong.MaxValue || handle == 0xFFFFFFFFUL)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 5) throw new UnauthorizedAccessException("Access to the path '" + src.Path + "' is denied.");
                    throw new IOException("Windows could not open the trace (error " + err.ToString(CultureInfo.InvariantCulture) + ": " + (new System.ComponentModel.Win32Exception(err).Message) + ").");
                }
                opened = true;
                uint r = ProcessTrace(new ulong[] { handle }, 1, IntPtr.Zero, IntPtr.Zero);
                if (r != 0 && r != 1223 && s.Decoded == 0 && s.Skipped == 0)
                    throw new IOException("Windows could not read the trace (error " + r.ToString(CultureInfo.InvariantCulture) + ": " + (new System.ComponentModel.Win32Exception((int)r).Message) + ").");
                if (tok.State != 0) src.Partial = true;
            }
            finally
            {
                if (opened) { try { CloseTrace(handle); } catch (Exception) { } }
                GC.KeepAlive(rc);
                GC.KeepAlive(bc);
                Marshal.FreeHGlobal(name);
                Marshal.FreeHGlobal(lf);
                if (s.Info != IntPtr.Zero) Marshal.FreeHGlobal(s.Info);
                if (s.Text != IntPtr.Zero) Marshal.FreeHGlobal(s.Text);
                if (s.Map != IntPtr.Zero) Marshal.FreeHGlobal(s.Map);
            }
            src.Pos = src.Size;
            if (s.Skipped > 0)
            {
                src.Note = string.Format(CultureInfo.InvariantCulture, "{0:N0} of {1:N0} events in {2} could not be decoded on this computer", s.Skipped, s.Skipped + s.Decoded, src.Name) +
                    (s.Decoded == 0 ? ". This trace needs decoding files that Windows does not have here. For Windows Update logs from Windows Server 2016 or Windows 10 1607 and earlier, run Get-WindowsUpdateLog and open the WindowsUpdate.log it writes." : ".");
            }
        }

        static uint OnBuffer(State s, IntPtr lf)
        {
            try
            {
                int readOff = Ptr == 8 ? 24 : 16;
                int sizeOff = Ptr == 8 ? 408 : 388;
                long read = (uint)Marshal.ReadInt32(lf, readOff);
                long bsz = (uint)Marshal.ReadInt32(lf, sizeOff);
                long pos = Math.Min(s.Src.Size, read * bsz);
                s.Src.Pos = pos;
                if (s.Progress != null) s.Progress(pos);
            }
            catch (Exception) { }
            return s.Tok.State == 0 ? 1u : 0u;
        }

        static void OnRecord(State s, IntPtr rec)
        {
            try { Decode(s, rec); }
            catch (OutOfMemoryException)
            {
                s.Tok.Oom = true;
                s.Tok.State = 1;
            }
            catch (Exception) { s.Skipped++; }
        }

        static string Str(IntPtr basePtr, uint size, int off)
        {
            if (off <= 0 || off >= size) return null;
            string v = Marshal.PtrToStringUni(new IntPtr(basePtr.ToInt64() + off));
            if (v == null) return null;
            v = v.Trim();
            return v.Length == 0 ? null : v;
        }

        static void Decode(State s, IntPtr rec)
        {
            ushort flags = (ushort)Marshal.ReadInt16(rec, 4);
            int thread = Marshal.ReadInt32(rec, 8);
            long stamp = Marshal.ReadInt64(rec, 16);
            byte[] gb = new byte[16];
            Marshal.Copy(new IntPtr(rec.ToInt64() + 24), gb, 0, 16);
            Guid provider = new Guid(gb);
            if (provider == EventTraceGuid) return;
            byte level = Marshal.ReadByte(rec, 44);
            ushort dataLen = (ushort)Marshal.ReadInt16(rec, 86);
            IntPtr data = Marshal.ReadIntPtr(rec, Ptr == 8 ? 96 : 92);
            uint ptrSize = (flags & Flag32Bit) != 0 ? 4u : (flags & Flag64Bit) != 0 ? 8u : (uint)Ptr;

            s.Seq++;
            long t = Store.NoTime;
            short bias = Store.NoBias;
            try
            {
                DateTime utc = DateTime.FromFileTimeUtc(stamp);
                DateTime local = utc.ToLocalTime();
                t = local.Ticks;
                bias = (short)Math.Round((utc - local).TotalMinutes);
            }
            catch (ArgumentException) { }

            string msg, comp, providerName;
            if ((flags & FlagStringOnly) != 0)
            {
                msg = dataLen > 0 && data != IntPtr.Zero ? Marshal.PtrToStringUni(data, dataLen / 2).TrimEnd('\0') : "";
                comp = null;
                providerName = provider.ToString("B");
            }
            else
            {
                uint size = s.InfoSize;
                uint r = s.Info == IntPtr.Zero ? ErrInsufficientBuffer : TdhGetEventInformation(rec, 0, IntPtr.Zero, s.Info, ref size);
                if (r == ErrInsufficientBuffer)
                {
                    if (s.Info != IntPtr.Zero) Marshal.FreeHGlobal(s.Info);
                    size = Math.Max(size, 4096);
                    s.Info = Marshal.AllocHGlobal((int)size);
                    s.InfoSize = size;
                    r = TdhGetEventInformation(rec, 0, IntPtr.Zero, s.Info, ref size);
                }
                if (r != 0)
                {
                    s.Skipped++;
                    return;
                }
                IntPtr info = s.Info;
                int source = Marshal.ReadInt32(info, 48);
                providerName = Str(info, size, Marshal.ReadInt32(info, 52)) ?? provider.ToString("B");
                string task = Str(info, size, Marshal.ReadInt32(info, 68));
                string evName = source == 3 ? Str(info, size, Marshal.ReadInt32(info, 92)) : null;
                string format = source == 0 ? Str(info, size, Marshal.ReadInt32(info, 76)) : null;
                comp = evName ?? task;
                int top = Marshal.ReadInt32(info, 104);
                int total = Marshal.ReadInt32(info, 100);
                List<string> names = new List<string>();
                List<string> values = new List<string>();
                long[] ints = new long[Math.Max(total, 1)];
                IntPtr cur = data;
                int left = dataLen;
                Props(s, rec, info, size, ptrSize, 0, top, ints, ref cur, ref left, names, values);
                msg = Compose(format, names, values);
            }
            byte sev = level == 1 || level == 2 ? (byte)3 : level == 3 ? (byte)2 : Parser.Sev(msg);
            if (string.IsNullOrEmpty(comp)) comp = ShortProvider(providerName);
            s.Src.CurLine = s.Seq;
            s.St.Add(s.Src, msg, t, bias, sev, s.St.CompId(comp), thread, s.St.CFileId(providerName));
            s.Decoded++;
        }

        static string ShortProvider(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int i = name.LastIndexOfAny(new char[] { '.', '-' });
            return i > 0 && i < name.Length - 1 ? name.Substring(i + 1) : name;
        }

        static void Props(State s, IntPtr rec, IntPtr info, uint infoSize, uint ptrSize, int first, int count, long[] ints, ref IntPtr cur, ref int left, List<string> names, List<string> values)
        {
            for (int i = first; i < first + count; i++)
            {
                if (left < 0) return;
                int p = 112 + i * 24;
                if (p + 24 > infoSize) return;
                int pflags = Marshal.ReadInt32(info, p);
                string pname = Str(info, infoSize, Marshal.ReadInt32(info, p + 4)) ?? ("Field" + (i + 1).ToString(CultureInfo.InvariantCulture));
                ushort inType = (ushort)Marshal.ReadInt16(info, p + 8);
                ushort outType = (ushort)Marshal.ReadInt16(info, p + 10);
                int mapOff = Marshal.ReadInt32(info, p + 12);
                ushort cnt = (ushort)Marshal.ReadInt16(info, p + 16);
                ushort len = (ushort)Marshal.ReadInt16(info, p + 18);
                int n = (pflags & PropParamCount) != 0 ? (int)Clamp(ints, cnt) : Math.Max((int)cnt, 1);
                if ((pflags & PropParamLength) != 0) len = (ushort)Clamp(ints, len);
                bool array = (pflags & PropParamCount) != 0 || cnt > 1;
                if ((pflags & PropStruct) != 0)
                {
                    ushort start = inType, members = outType;
                    List<string> parts = new List<string>();
                    for (int k = 0; k < n; k++)
                    {
                        List<string> mn = new List<string>(), mv = new List<string>();
                        Props(s, rec, info, infoSize, ptrSize, start, members, ints, ref cur, ref left, mn, mv);
                        StringBuilder sb = new StringBuilder();
                        for (int m = 0; m < mn.Count; m++)
                        {
                            if (m > 0) sb.Append(", ");
                            sb.Append(mn[m]).Append('=').Append(mv[m]);
                        }
                        parts.Add("{" + sb + "}");
                        if (left <= 0) break;
                    }
                    names.Add(pname);
                    values.Add(array ? "[" + string.Join(", ", parts.ToArray()) + "]" : (parts.Count > 0 ? parts[0] : ""));
                    continue;
                }
                IntPtr map = IntPtr.Zero;
                string mapName = mapOff > 0 ? Str(info, infoSize, mapOff) : null;
                if (mapName != null) map = MapFor(s, rec, mapName);
                if (inType == 14 && outType == 24 && len == 0) len = 16;
                List<string> vals = new List<string>();
                for (int k = 0; k < n; k++)
                {
                    if (left <= 0 && !(inType == 1 || inType == 2)) break;
                    if (k == 0) Remember(ints, i, inType, cur, left);
                    string v = Format(s, info, map, ptrSize, inType, outType, len, ref cur, ref left);
                    if (v == null) { left = -1; break; }
                    vals.Add(v);
                    if (vals.Count >= 64) { vals.Add("…"); break; }
                }
                names.Add(pname);
                values.Add(array ? "[" + string.Join(", ", vals.ToArray()) + "]" : (vals.Count > 0 ? vals[0] : ""));
            }
        }

        static long Clamp(long[] ints, ushort index)
        {
            if (index >= ints.Length) return 0;
            long v = ints[index];
            return v < 0 ? 0 : v > 65535 ? 65535 : v;
        }

        static void Remember(long[] ints, int i, ushort inType, IntPtr cur, int left)
        {
            if (i >= ints.Length || cur == IntPtr.Zero) return;
            try
            {
                switch (inType)
                {
                    case 3: case 4: if (left >= 1) ints[i] = Marshal.ReadByte(cur); break;
                    case 5: case 6: if (left >= 2) ints[i] = (ushort)Marshal.ReadInt16(cur); break;
                    case 7: case 8: case 20: if (left >= 4) ints[i] = (uint)Marshal.ReadInt32(cur); break;
                    case 9: case 10: case 21: if (left >= 8) ints[i] = Marshal.ReadInt64(cur); break;
                }
            }
            catch (Exception) { }
        }

        static IntPtr MapFor(State s, IntPtr rec, string mapName)
        {
            uint size = s.MapSize;
            uint r = s.Map == IntPtr.Zero ? ErrInsufficientBuffer : TdhGetEventMapInformation(rec, mapName, s.Map, ref size);
            if (r == ErrInsufficientBuffer)
            {
                if (s.Map != IntPtr.Zero) Marshal.FreeHGlobal(s.Map);
                size = Math.Max(size, 1024);
                s.Map = Marshal.AllocHGlobal((int)size);
                s.MapSize = size;
                r = TdhGetEventMapInformation(rec, mapName, s.Map, ref size);
            }
            return r == 0 ? s.Map : IntPtr.Zero;
        }

        static string Format(State s, IntPtr info, IntPtr map, uint ptrSize, ushort inType, ushort outType, ushort len, ref IntPtr cur, ref int left)
        {
            if (s.Text == IntPtr.Zero)
            {
                s.TextSize = 4096;
                s.Text = Marshal.AllocHGlobal((int)s.TextSize);
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                uint bsz = s.TextSize;
                ushort used;
                uint r = TdhFormatProperty(info, map, ptrSize, inType, outType, len, (ushort)Math.Max(0, left), cur, ref bsz, s.Text, out used);
                if (r == ErrInsufficientBuffer && attempt == 0)
                {
                    Marshal.FreeHGlobal(s.Text);
                    s.TextSize = Math.Max(bsz, s.TextSize * 2);
                    s.Text = Marshal.AllocHGlobal((int)s.TextSize);
                    continue;
                }
                if (r != 0 && map != IntPtr.Zero && attempt == 0)
                {
                    map = IntPtr.Zero;
                    continue;
                }
                if (r != 0) return null;
                cur = new IntPtr(cur.ToInt64() + used);
                left -= used;
                return Marshal.PtrToStringUni(s.Text) ?? "";
            }
            return null;
        }

        static string Compose(string format, List<string> names, List<string> values)
        {
            if (format != null)
            {
                string f = format.Replace("%n", "\n").Replace("%t", "\t").Replace("%%", "%");
                string m = RxInsert.Replace(f, delegate (Match mm)
                {
                    int k;
                    if (!int.TryParse(mm.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out k)) return mm.Value;
                    return k >= 1 && k <= values.Count ? values[k - 1] : "";
                });
                m = m.Trim();
                if (m.Length > 0) return m;
            }
            if (values.Count == 0) return "";
            if (values.Count == 1) return values[0];
            int text = -1;
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i].ToLowerInvariant();
                if (n == "message" || n == "msg" || n == "info" || n == "text" || n == "description")
                {
                    text = i;
                    break;
                }
            }
            StringBuilder sb = new StringBuilder();
            if (text >= 0) sb.Append(values[text]);
            for (int i = 0; i < names.Count; i++)
            {
                if (i == text) continue;
                if (sb.Length > 0) sb.Append(text >= 0 && i == (text == 0 ? 1 : 0) ? "   " : "; ");
                sb.Append(names[i]).Append(": ").Append(values[i]);
            }
            return sb.ToString();
        }
    }
}
