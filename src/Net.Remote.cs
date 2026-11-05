namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;

    public static class RemoteLogin
    {
        public const int ErrBadPassword = 1326;
        public const int ErrConflict = 1219;
        public const int ErrCancelled = 1223;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CredUiInfo
        {
            public int Size;
            public IntPtr Parent;
            public string Message;
            public string Caption;
            public IntPtr Banner;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        sealed class NetResource
        {
            public int Scope;
            public int Type;
            public int DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct Credential
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public int WrittenLow;
            public int WrittenHigh;
            public int BlobSize;
            public IntPtr Blob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        static extern int CredUIPromptForWindowsCredentials(ref CredUiInfo info, int authError, ref uint package, IntPtr inBuffer, uint inSize, out IntPtr outBuffer, out uint outSize, ref bool save, int flags);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CredPackAuthenticationBuffer(int flags, string user, string password, IntPtr buffer, ref int size);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CredUnPackAuthenticationBuffer(int flags, IntPtr buffer, uint size, StringBuilder user, ref int userLen, StringBuilder domain, ref int domainLen, StringBuilder password, ref int passwordLen);

        [DllImport("ole32.dll")]
        static extern void CoTaskMemFree(IntPtr p);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetAddConnection2(NetResource resource, string password, string user, int flags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetCancelConnection2(string name, int flags, bool force);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredWriteW")]
        static extern bool CredWrite(ref Credential credential, int flags);

        static readonly Dictionary<string, string> sessions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, System.Security.SecureString> secrets = new Dictionary<string, System.Security.SecureString>(StringComparer.OrdinalIgnoreCase);

        public static bool SessionCredential(string computer, out string domain, out string user, out System.Security.SecureString password)
        {
            string host = Host(computer), full;
            domain = null;
            user = null;
            password = null;
            if (!sessions.TryGetValue(host, out full) || !secrets.TryGetValue(host, out password)) return false;
            int i = full.IndexOf('\\');
            if (i > 0) { domain = full.Substring(0, i); user = full.Substring(i + 1); }
            else user = full;
            return true;
        }

        public static string Host(string computer)
        {
            return (computer ?? "").Trim().TrimStart('\\').TrimEnd('\\');
        }

        public static string AccountFor(string computer)
        {
            string user;
            return sessions.TryGetValue(Host(computer), out user) ? user : null;
        }

        public static string RememberedUser(string computer)
        {
            string host = Host(computer);
            foreach (string e in Settings.GetList("Accounts"))
            {
                int i = e.IndexOf('|');
                if (i > 0 && string.Equals(e.Substring(0, i), host, StringComparison.OrdinalIgnoreCase)) return e.Substring(i + 1);
            }
            return Settings.Get("LastAccount");
        }

        static void RememberUser(string computer, string user)
        {
            string host = Host(computer);
            List<string> keep = new List<string>();
            keep.Add(host + "|" + user);
            foreach (string e in Settings.GetList("Accounts"))
            {
                int i = e.IndexOf('|');
                if (i > 0 && !string.Equals(e.Substring(0, i), host, StringComparison.OrdinalIgnoreCase) && keep.Count < 50) keep.Add(e);
            }
            Settings.SetList("Accounts", keep);
            Settings.Set("LastAccount", user);
        }

        public static bool Prompt(IWin32Window owner, string computer, int lastError, ref string user, out string password, out bool save)
        {
            password = null;
            save = false;
            CredUiInfo ui = new CredUiInfo();
            ui.Size = Marshal.SizeOf(typeof(CredUiInfo));
            ui.Parent = owner != null ? owner.Handle : IntPtr.Zero;
            ui.Caption = "Connect to " + Host(computer);
            ui.Message = "Enter an account that is an administrator on " + Host(computer) + ". For a local account on that computer, use " + Host(computer).Split('.')[0] + "\\name.";
            IntPtr inBuf = IntPtr.Zero;
            int inSize = 0;
            IntPtr outBuf = IntPtr.Zero;
            uint outSize = 0;
            try
            {
                if (!string.IsNullOrEmpty(user))
                {
                    CredPackAuthenticationBuffer(4, user, "", IntPtr.Zero, ref inSize);
                    if (inSize > 0)
                    {
                        inBuf = Marshal.AllocCoTaskMem(inSize);
                        if (!CredPackAuthenticationBuffer(4, user, "", inBuf, ref inSize)) { Marshal.FreeCoTaskMem(inBuf); inBuf = IntPtr.Zero; inSize = 0; }
                    }
                }
                uint package = 0;
                int r = CredUIPromptForWindowsCredentials(ref ui, lastError, ref package, inBuf, (uint)inSize, out outBuf, out outSize, ref save, 0x1 | 0x2);
                if (r != 0) return false;
                StringBuilder u = new StringBuilder(514), d = new StringBuilder(338), p = new StringBuilder(514);
                int ul = u.Capacity, dl = d.Capacity, pl = p.Capacity;
                if (!CredUnPackAuthenticationBuffer(1, outBuf, outSize, u, ref ul, d, ref dl, p, ref pl))
                {
                    u.Length = 0; d.Length = 0; p.Length = 0;
                    ul = u.Capacity; dl = d.Capacity; pl = p.Capacity;
                    if (!CredUnPackAuthenticationBuffer(0, outBuf, outSize, u, ref ul, d, ref dl, p, ref pl))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                string name = u.ToString();
                if (d.Length > 0 && name.IndexOf('\\') < 0 && name.IndexOf('@') < 0) name = d + "\\" + name;
                user = name;
                password = p.ToString();
                p.Length = 0;
                return user.Length > 0;
            }
            finally
            {
                if (inBuf != IntPtr.Zero) Marshal.FreeCoTaskMem(inBuf);
                if (outBuf != IntPtr.Zero)
                {
                    for (int i = 0; i < outSize; i++) Marshal.WriteByte(outBuf, i, 0);
                    CoTaskMemFree(outBuf);
                }
            }
        }

        static readonly Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string PathHost(string computer)
        {
            string host = Host(computer), a;
            return aliases.TryGetValue(host, out a) ? a : host;
        }

        static int Add(string target, string user, string password)
        {
            NetResource nr = new NetResource();
            nr.Type = 0;
            nr.RemoteName = @"\\" + target + @"\IPC$";
            return WNetAddConnection2(nr, password, user, 0x4);
        }

        static List<string> Alternates(string host)
        {
            List<string> list = new List<string>();
            try
            {
                System.Net.IPHostEntry e = System.Net.Dns.GetHostEntry(host);
                string fqdn = e.HostName ?? "";
                string shortName = host.Split('.')[0];
                if (fqdn.Length > 0 && !string.Equals(fqdn, host, StringComparison.OrdinalIgnoreCase)) list.Add(fqdn);
                if (host.IndexOf('.') > 0 && !string.Equals(shortName, host, StringComparison.OrdinalIgnoreCase)) list.Add(shortName);
                foreach (System.Net.IPAddress ip in e.AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { list.Add(ip.ToString()); break; }
            }
            catch (Exception) { }
            return list;
        }

        public static int Connect(string computer, string user, string password, out string note)
        {
            note = null;
            string host = Host(computer);
            string target = host;
            int r = Add(target, user, password);
            if (r == ErrConflict)
            {
                try { WNetCancelConnection2(@"\\" + host + @"\IPC$", 0, false); } catch (Exception) { }
                r = Add(target, user, password);
            }
            if (r == ErrConflict)
            {
                foreach (string alt in Alternates(host))
                {
                    int r2 = Add(alt, user, password);
                    if (r2 == 0)
                    {
                        target = alt;
                        r = 0;
                        note = "Your own account was already connected to " + host + ", so LogVice connected through " + alt + " instead.";
                        break;
                    }
                    if (r2 != ErrConflict) { r = r2; break; }
                }
            }
            if (r == 0)
            {
                sessions[host] = user;
                System.Security.SecureString ss = new System.Security.SecureString();
                foreach (char ch in password ?? "") ss.AppendChar(ch);
                ss.MakeReadOnly();
                secrets[host] = ss;
                if (!string.Equals(target, host, StringComparison.OrdinalIgnoreCase)) aliases[host] = target; else aliases.Remove(host);
                RememberUser(host, user);
            }
            return r;
        }

        public static bool Save(string computer, string user, string password)
        {
            byte[] blob = Encoding.Unicode.GetBytes(password ?? "");
            IntPtr mem = Marshal.AllocHGlobal(Math.Max(blob.Length, 1));
            try
            {
                Marshal.Copy(blob, 0, mem, blob.Length);
                Credential c = new Credential();
                c.Type = 2;
                c.TargetName = Host(computer);
                c.UserName = user;
                c.BlobSize = blob.Length;
                c.Blob = mem;
                c.Persist = 2;
                c.Comment = "Saved by LogVice";
                return CredWrite(ref c, 0);
            }
            catch (Exception) { return false; }
            finally
            {
                for (int i = 0; i < blob.Length; i++) Marshal.WriteByte(mem, i, 0);
                Array.Clear(blob, 0, blob.Length);
                Marshal.FreeHGlobal(mem);
            }
        }

        public static void Disconnect(string computer)
        {
            string host = Host(computer);
            if (!sessions.ContainsKey(host)) return;
            try { WNetCancelConnection2(@"\\" + PathHost(host) + @"\IPC$", 0, true); } catch (Exception) { }
            sessions.Remove(host);
            aliases.Remove(host);
            System.Security.SecureString old;
            if (secrets.TryGetValue(host, out old)) { old.Dispose(); secrets.Remove(host); }
        }

        public static void DisconnectAll()
        {
            foreach (string host in new List<string>(sessions.Keys)) Disconnect(host);
        }

        public static string Explain(int error, string computer)
        {
            string host = Host(computer);
            switch (error)
            {
                case 5:
                case 1385:
                    return "The account was accepted, but " + host + " does not let it read the admin shares. A local account other than the built-in Administrator is blocked over the network by Remote UAC. Use a domain account that is an administrator on " + host + ", or the built-in Administrator.";
                case ErrConflict:
                    return "You are already connected to " + host + " with another account, for example through a mapped drive or an open File Explorer window. Close that connection, or type the computer's full DNS name or IP address instead, which Windows treats as a separate server.";
                case ErrBadPassword:
                    return "The user name or password is incorrect.";
                case 53:
                case 67:
                case 1231:
                    return "The network path to " + host + " was not found. Check the name, and that the computer is on and can be reached over SMB.";
                case 1311:
                case 1355:
                    return "The domain could not be contacted to check this account.";
                case 1909:
                    return "This account is locked out.";
                case 1330:
                    return "The password of this account has expired.";
                default:
                    return "Windows could not connect to " + host + " (error " + error.ToString(CultureInfo.InvariantCulture) + ": " + new System.ComponentModel.Win32Exception(error).Message + ").";
            }
        }
    }
}
