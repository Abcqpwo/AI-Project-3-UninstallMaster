using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CleanUninstall
{
    public static class InstalledApps
    {
        public static List<ProgramInfo> GetAll()
        {
            List<ProgramInfo> list = new List<ProgramInfo>();
            ScanHive(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM", true, false, list);
            ScanHive(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM", false, false, list);
            ScanHive(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU", true, true, list);
            ScanHive(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU", false, true, list);

            list.Sort(delegate(ProgramInfo a, ProgramInfo b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        private static void ScanHive(RegistryKey root, string path, string hiveName, bool is64, bool isUser, List<ProgramInfo> outList)
        {
            RegistryKey k = null;
            try { k = root.OpenSubKey(path, false); }
            catch { return; }
            if (k == null) return;

            string[] subs = null;
            try { subs = k.GetSubKeyNames(); }
            catch { k.Close(); return; }

            for (int i = 0; i < subs.Length; i++)
            {
                RegistryKey sk = null;
                try { sk = k.OpenSubKey(subs[i], false); }
                catch { continue; }
                if (sk == null) continue;

                try
                {
                    string name = Str(sk, "DisplayName");
                    if (name.Length == 0) continue;
                    if (Str(sk, "SystemComponent") == "1") continue;

                    string rel = Str(sk, "ReleaseType");
                    if (rel == "Update" || rel == "Hotfix" || rel == "Security Update" || rel == "ServicePack" || rel == "Update Rollup")
                        continue;

                    string parentKey = Str(sk, "ParentKeyName");
                    if (parentKey.Length > 0 && rel.Length > 0) continue;

                    if (Regex.IsMatch(name, @"^KB\d{5,}")) continue;

                    ProgramInfo p = new ProgramInfo();
                    p.Name = name.Trim();
                    p.Version = Str(sk, "DisplayVersion");
                    p.Publisher = Str(sk, "Publisher");
                    p.UninstallString = Str(sk, "UninstallString");
                    p.QuietUninstallString = Str(sk, "QuietUninstallString");
                    p.InstallLocation = Str(sk, "InstallLocation");
                    p.DisplayIcon = Str(sk, "DisplayIcon");
                    p.HiveName = hiveName;
                    p.SubPath = path + "\\" + subs[i];
                    p.Is64 = is64;
                    p.IsUser = isUser;

                    object sz = null;
                    try { sz = sk.GetValue("EstimatedSize"); }
                    catch { }
                    if (sz != null)
                    {
                        try { p.SizeKB = Convert.ToInt64(sz); }
                        catch { p.SizeKB = 0; }
                    }

                    outList.Add(p);
                }
                catch { }
                finally
                {
                    try { sk.Close(); } catch { }
                }
            }

            try { k.Close(); } catch { }
        }

        private static string Str(RegistryKey k, string name)
        {
            object o = null;
            try { o = k.GetValue(name, null); }
            catch { return ""; }
            if (o == null) return "";
            string s = o as string;
            if (s == null) return "";
            return s.Trim();
        }

        // 把 UninstallString 拆成 程序 + 参数
        public static void ParseCommand(string cmd, out string file, out string args)
        {
            file = "";
            args = "";
            string s = (cmd == null ? "" : cmd.Trim());
            if (s.Length == 0) return;

            if (s.StartsWith("\""))
            {
                int end = s.IndexOf('"', 1);
                if (end > 0)
                {
                    file = s.Substring(1, end - 1).Trim();
                    args = s.Substring(end + 1).Trim();
                    return;
                }
            }

            string lower = s.ToLowerInvariant();
            int idx = lower.IndexOf(".exe", StringComparison.Ordinal);
            if (idx > 0)
            {
                file = s.Substring(0, idx + 4).Trim();
                string rest = s.Substring(idx + 4).Trim();
                // 排除 xxx.exe.exe 之类的误判：如果 .exe 之后紧跟的是字母/点，继续往后找
                while (rest.Length > 0 && (rest[0] == '_' || rest[0] == '.' || Char.IsLetter(rest[0])))
                {
                    string l2 = rest.ToLowerInvariant();
                    int i2 = l2.IndexOf(".exe", StringComparison.Ordinal);
                    if (i2 < 0) break;
                    file = file + rest.Substring(0, i2 + 4).Trim();
                    rest = rest.Substring(i2 + 4).Trim();
                }
                args = rest;
                if (args.StartsWith(",")) { file = file + args; args = ""; }

                // msiexec /I{GUID} 打开的是"修改"界面，直接换成 /X 走卸载
                string fileName = "";
                try { fileName = System.IO.Path.GetFileName(file).ToLowerInvariant(); }
                catch { }
                if (fileName == "msiexec.exe" && args.Length >= 2 &&
                    (args.StartsWith("/I", StringComparison.OrdinalIgnoreCase) || args.StartsWith("-I", StringComparison.OrdinalIgnoreCase)))
                {
                    args = "/X" + args.Substring(2);
                }
                return;
            }

            file = s;
        }

        public static string CleanName(string name)
        {
            if (name == null) return "";
            string s = name;
            s = Regex.Replace(s, @"\([^)]*\)", " ");
            s = Regex.Replace(s, @"\[[^\]]*\]", " ");
            s = Regex.Replace(s, @"（[^）]*）", " ");
            s = Regex.Replace(s, @"(?i)\bversion\s*\d+(\.\d+)*", " ");
            s = Regex.Replace(s, @"(?i)\bv\s*\d+(\.\d+)*", " ");
            s = Regex.Replace(s, @"\b\d+(\.\d+){1,}\S*", " ");
            s = Regex.Replace(s, @"(?i)\b(x64|x86|arm64|64-bit|64bit|32-bit|32bit|portable|setup|installer|update|beta|alpha)\b", " ");
            s = s.Replace("™", " ").Replace("®", " ").Replace("©", " ");
            s = Regex.Replace(s, @"\s{2,}", " ");
            s = s.Trim(' ', '-', '_', '.', ',', ':');
            if (s.Length < 3) s = name.Trim();
            return s;
        }

        public static string CleanPublisher(string pub)
        {
            if (pub == null) return "";
            string s = pub;
            s = Regex.Replace(s, @"(?i)\(.*?\)", " ");
            s = Regex.Replace(s, @"(?i)\b(inc\.?|incorporated|corporation|corp\.?|ltd\.?|limited|llc|co\.,?\s*ltd\.?|gmbh|s\.a\.|pty\.?|co\.)\b", " ");
            s = Regex.Replace(s, @"\s{2,}", " ");
            s = s.Trim(' ', ',', '.');
            return s;
        }
    }
}
