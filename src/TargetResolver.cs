using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CleanUninstall
{
    // 从右键点中的目标（exe / 快捷方式 / 文件夹）反查是哪个已安装软件
    public static class TargetResolver
    {
        public static string ShortcutTarget(string lnkPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return "";
                object shell = Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                if (sc == null) return "";
                object t = sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null);
                return t == null ? "" : t.ToString();
            }
            catch { return ""; }
        }

        public static string ShortcutWorkingDir(string lnkPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return "";
                object shell = Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                if (sc == null) return "";
                object t = sc.GetType().InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, sc, null);
                return t == null ? "" : t.ToString();
            }
            catch { return ""; }
        }

        public static string RealTarget(string target)
        {
            string p = Clean(target);
            if (p.Length == 0) return "";
            if (p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(p))
            {
                string t = ShortcutTarget(p);
                if (t.Length > 0) return t;
            }
            return p;
        }

        private static string Clean(string s)
        {
            if (s == null) return "";
            return s.Trim().Trim('"');
        }

        private static bool Under(string path, string dir)
        {
            if (path == null || dir == null) return false;
            string p = path.TrimEnd('\\');
            string d = dir.TrimEnd('\\');
            if (d.Length < 4 || p.Length == 0) return false;
            if (p.Equals(d, StringComparison.OrdinalIgnoreCase)) return true;
            if (p.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static int Depth(string p)
        {
            if (p == null) return 0;
            string[] parts = p.TrimEnd('\\').Split('\\');
            int n = 0;
            for (int i = 0; i < parts.Length; i++) if (parts[i].Length > 0) n++;
            return n;
        }

        private static readonly string[] GenericNames = new string[] {
            "program files", "program files (x86)", "programdata", "program data",
            "appdata", "roaming", "local", "locallow", "windows", "users", "public",
            "desktop", "documents", "downloads", "temp", "start menu", "programs", "common files"
        };

        // 只有足够具体的目录才允许做"包含判断"，避免在 Program Files 上右键时乱匹配
        private static bool SafeDir(string dir)
        {
            if (dir == null) return false;
            string d = dir.TrimEnd('\\');
            if (d.Length < 4) return false;
            if (Depth(d) < 3) return false;
            string leaf = "";
            int i = d.LastIndexOf('\\');
            if (i >= 0) leaf = d.Substring(i + 1);
            else leaf = d;
            leaf = leaf.Trim().ToLowerInvariant();
            for (int j = 0; j < GenericNames.Length; j++)
                if (leaf == GenericNames[j]) return false;
            return true;
        }

        public static string NameHint(string target)
        {
            string p = Clean(target);
            try
            {
                if (p.Length == 0) return "";
                if (p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileNameWithoutExtension(p);
                if (Directory.Exists(p))
                    return Path.GetFileName(p.TrimEnd('\\'));
                return Path.GetFileNameWithoutExtension(p);
            }
            catch { return ""; }
        }

        public static string DirHint(string target)
        {
            string p = Clean(target);
            try
            {
                if (Directory.Exists(p)) return p.TrimEnd('\\');
                string real = RealTarget(p);
                if (Directory.Exists(real)) return real.TrimEnd('\\');
                if (File.Exists(real)) return Path.GetDirectoryName(real);
            }
            catch { }
            return "";
        }

        // 从 exe 的版本信息里取产品名，绿色软件也能靠它认出来
        private static void AddFileInfoHints(string exePath, List<string> hints)
        {
            try
            {
                if (exePath == null || !exePath.ToLowerInvariant().EndsWith(".exe")) return;
                if (!File.Exists(exePath)) return;
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(exePath);
                if (vi.ProductName != null && vi.ProductName.Trim().Length >= 2) hints.Add(vi.ProductName.Trim());
                if (vi.FileDescription != null && vi.FileDescription.Trim().Length >= 2) hints.Add(vi.FileDescription.Trim());
                if (vi.CompanyName != null && vi.CompanyName.Trim().Length >= 2) hints.Add(vi.CompanyName.Trim());
            }
            catch { }
        }

        private static List<string> BuildHints(string target, string real)
        {
            List<string> hints = new List<string>();
            string a = NameHint(real);
            string b = NameHint(target);
            if (a.Length > 0) hints.Add(a);
            if (b.Length > 0 && !b.Equals(a, StringComparison.OrdinalIgnoreCase)) hints.Add(b);
            AddFileInfoHints(real, hints);

            string wd = "";
            if (Clean(target).EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) wd = ShortcutWorkingDir(Clean(target));
            if (wd.Length > 0)
            {
                string wdn = "";
                try { wdn = Path.GetFileName(wd.TrimEnd('\\')); }
                catch { }
                if (wdn.Length >= 2) hints.Add(wdn);
            }

            // 变体：去掉括号、取前两段
            List<string> outList = new List<string>();
            for (int i = 0; i < hints.Count; i++)
            {
                string h = hints[i];
                outList.Add(h);
                int p1 = h.IndexOf('(');
                if (p1 > 0)
                {
                    string cut = h.Substring(0, p1).Trim();
                    if (cut.Length >= 3) outList.Add(cut);
                }
                string[] words = h.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 3)
                {
                    string two = words[0] + " " + words[1];
                    if (two.Length >= 3) outList.Add(two);
                }
            }
            return outList;
        }

        private static int NameScore(ProgramInfo a, List<string> hints)
        {
            string an = ResidueScanner.Norm(InstalledApps.CleanName(a.Name));
            string raw = ResidueScanner.Norm(a.Name);
            if (an.Length < 3 && raw.Length < 3) return 0;

            int best = 0;
            for (int i = 0; i < hints.Count; i++)
            {
                string hn = ResidueScanner.Norm(hints[i]);
                if (hn.Length < 3) continue;
                for (int k = 0; k < 2; k++)
                {
                    string cn = (k == 0) ? an : raw;
                    if (cn.Length < 3) continue;
                    int s = 0;
                    if (cn == hn) s = 100;
                    else if (cn.StartsWith(hn, StringComparison.Ordinal) || hn.StartsWith(cn, StringComparison.Ordinal)) s = 72;
                    else if (cn.IndexOf(hn, StringComparison.Ordinal) >= 0 || hn.IndexOf(cn, StringComparison.Ordinal) >= 0) s = 64;
                    if (s > 0)
                    {
                        int diff = Math.Abs(cn.Length - hn.Length);
                        if (diff > 40) diff = 40;
                        s -= diff;
                    }
                    if (s > best) best = s;
                }
            }
            return best;
        }

        public static ProgramInfo Resolve(string target, List<ProgramInfo> apps)
        {
            string p = Clean(target);
            if (p.Length == 0 || apps == null) return null;

            string real = RealTarget(p);
            string dirHint = DirHint(p);
            bool dirSafe = SafeDir(dirHint);

            List<string> hints = BuildHints(p, real);

            ProgramInfo best = null;
            int bestScore = 0;

            for (int i = 0; i < apps.Count; i++)
            {
                ProgramInfo a = apps[i];
                int score = 0;

                // 1) 安装目录
                string loc = Clean(a.InstallLocation).TrimEnd('\\');
                if (loc.Length >= 4)
                {
                    if (Under(real, loc) || Under(p, loc)) score = 1000 + Math.Min(200, loc.Length);
                    else if (dirSafe && Under(loc, dirHint)) score = 900 + Math.Min(200, loc.Length);
                }

                // 2) 卸载程序所在目录
                if (a.UninstallString.Length > 0)
                {
                    string file = "";
                    string args = "";
                    try { InstalledApps.ParseCommand(a.UninstallString, out file, out args); }
                    catch { file = ""; }
                    string ud = "";
                    if (file.Length >= 4)
                    {
                        try { ud = Path.GetDirectoryName(file).TrimEnd('\\'); }
                        catch { ud = ""; }
                    }
                    if (ud.Length >= 4)
                    {
                        if (Under(real, ud) || Under(p, ud)) score = Math.Max(score, 800 + Math.Min(200, ud.Length));
                        else if (dirSafe && Under(ud, dirHint)) score = Math.Max(score, 700 + Math.Min(200, ud.Length));
                    }
                }

                // 3) 图标目录
                string icon = Clean(a.DisplayIcon);
                if (icon.Length >= 4)
                {
                    int comma = icon.IndexOf(',');
                    if (comma > 0) icon = icon.Substring(0, comma);
                    string id = "";
                    try { id = Path.GetDirectoryName(icon.TrimEnd('\\')).TrimEnd('\\'); }
                    catch { id = ""; }
                    if (id.Length >= 4 && !id.ToLowerInvariant().Contains("\\windows\\"))
                    {
                        if (Under(real, id) || (dirSafe && Under(p, id))) score = Math.Max(score, 600 + Math.Min(200, id.Length));
                    }
                    else if (id.Length >= 4 && (Under(real, id) || (dirSafe && Under(p, id))))
                    {
                        score = Math.Max(score, 300);
                    }
                }

                // 4) 名字相似度
                int ns = NameScore(a, hints);
                if (ns >= 40) score = Math.Max(score, ns);

                if (score > bestScore) { bestScore = score; best = a; }
            }

            if (best != null && bestScore >= 40) return best;
            return null;
        }
    }
}
