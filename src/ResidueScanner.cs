using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CleanUninstall
{
    public static class ResidueScanner
    {
        private static readonly string[] UserProfileSkip = new string[] {
            "desktop","documents","downloads","music","pictures","videos","favorites",
            "links","searches","saved games","onedrive","appdata","contacts","3d objects",
            "source","templates","cookies","local settings","my documents"
        };

        private static readonly string[] ProgramFilesSkip = new string[] {
            "microsoft","common files","windowsapps","package cache","reference assemblies",
            "internet explorer","msbuild","uninstall information","windows defender",
            "windows nt","windows mail","windows media player","windows portable devices"
        };

        private static readonly string[] ProgramDataSkip = new string[] {
            "microsoft","package cache","packages","temp","usoshared","regid.1991-06.com.microsoft",
            "softwaredistribution","application data"
        };

        private static readonly string[] LocalAppDataSkip = new string[] {
            "microsoft","packages","temp","tempstate","crashdumps","connecteddevicesplatform",
            "d3dscache","history","iconcache","placeholdertilelogotransparent"
        };

        private static readonly string[] RoamingSkip = new string[] {
            "microsoft","temp","identities","windows"
        };

        // 这些目录连下钻都不做（太大或权限受限）
        private static readonly string[] HardSkip = new string[] {
            "windowsapps","package cache","packages","temp","windows defender","windows nt",
            "windows mail","windows media player","windows portable devices","softwaredistribution",
            "system32","syswow64"
        };

        // 太通用的词不做"弱关键词"，否则容易误伤
        private static readonly string[] WeakBlacklist = new string[] {
            "app","apps","tool","tools","pro","home","client","player","service","update",
            "setup","version","free","lite","plus","core","runtime","desktop","manager",
            "studio","suite","data","cache","temp","user","users","local","roaming"
        };

        private static string _appNorm = "";
        private static string _pubNorm = "";

        // 弱匹配到的目录，只有在"厂商/品牌"对得上时才可信
        private static bool VendorPlausible(string parentName)
        {
            string pn = Norm(parentName);
            if (pn.Length == 0) return false;

            string[] roots = new string[] {
                "programfiles", "programfilesx86", "programdata", "local", "roaming",
                "appdata", "documents", "public", "userdata", "temp"
            };
            for (int i = 0; i < roots.Length; i++)
            {
                if (pn == roots[i]) return true;
            }
            if (pn == Norm(Environment.UserName)) return true;

            if (_pubNorm.Length >= 3 && (pn.IndexOf(_pubNorm, StringComparison.Ordinal) >= 0 || _pubNorm.IndexOf(pn, StringComparison.Ordinal) >= 0))
                return true;
            if (pn.Length >= 3 && _appNorm.IndexOf(pn, StringComparison.Ordinal) >= 0)
                return true;

            return false;
        }

        public static List<ResidueItem> Scan(ProgramInfo p, Action<string> status)
        {
            List<ResidueItem> items = new List<ResidueItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> keys = BuildKeys(p);
            List<string> weak = BuildWeakKeys(p);

            _appNorm = Norm(InstalledApps.CleanName(p.Name == null ? "" : p.Name));
            _pubNorm = Norm(InstalledApps.CleanPublisher(p.Publisher == null ? "" : p.Publisher));

            Report(status, "正在检查安装目录…");

            // 1) 注册表里记录的安装目录
            string loc = (p.InstallLocation == null ? "" : p.InstallLocation.Trim().Trim('"'));
            if (loc.Length > 0)
            {
                int comma = loc.IndexOf(',');
                if (comma > 0) loc = loc.Substring(0, comma);
                if (Directory.Exists(loc))
                {
                    string nm = Path.GetFileName(loc.TrimEnd('\\'));
                    int risk = RiskOf(loc, nm);
                    if (risk < 2)
                        AddFolder(items, seen, loc, "安装目录", risk);
                }
            }

            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string commonStart = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string start = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            string commonDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);

            Report(status, "正在扫描 Program Files…");
            ScanFolders(pf, 2, ProgramFilesSkip, items, seen, keys, weak, "残留文件夹");
            if (!string.Equals(pf, pf86, StringComparison.OrdinalIgnoreCase))
            {
                Report(status, "正在扫描 Program Files (x86)…");
                ScanFolders(pf86, 2, ProgramFilesSkip, items, seen, keys, weak, "残留文件夹（32位）");
            }

            Report(status, "正在扫描 ProgramData…");
            ScanFolders(programData, 2, ProgramDataSkip, items, seen, keys, weak, "程序数据");

            Report(status, "正在扫描 AppData…");
            ScanFolders(roaming, 2, RoamingSkip, items, seen, keys, weak, "用户数据");
            ScanFolders(localApp, 2, LocalAppDataSkip, items, seen, keys, weak, "本地数据");

            Report(status, "正在扫描用户目录…");
            ScanFolders(profile, 1, UserProfileSkip, items, seen, keys, weak, "用户目录");
            ScanFolders(commonDocs, 1, null, items, seen, keys, weak, "公共文档");

            Report(status, "正在扫描快捷方式…");
            ScanLinks(commonDesktop, 1, items, seen, keys, weak);
            ScanLinks(desktop, 1, items, seen, keys, weak);
            ScanLinks(commonStart, 3, items, seen, keys, weak);
            ScanLinks(start, 3, items, seen, keys, weak);

            Report(status, "正在扫描注册表…");
            ScanRegistry(p, items, seen, keys, weak);

            items.Sort(delegate(ResidueItem a, ResidueItem b)
            {
                if (a.Kind != b.Kind) return a.Kind.CompareTo(b.Kind);
                return b.Size.CompareTo(a.Size);
            });

            Report(status, "扫描完成");
            return items;
        }

        private static void Report(Action<string> status, string msg)
        {
            if (status != null) { try { status(msg); } catch { } }
        }

        public static string Norm(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            string low = s.ToLowerInvariant();
            for (int i = 0; i < low.Length; i++)
            {
                char c = low[i];
                if (c == ' ' || c == '-' || c == '_' || c == '.' || c == ',' || c == '\'' ||
                    c == '(' || c == ')' || c == '（' || c == '）' || c == '[' || c == ']' ||
                    c == '™' || c == '®' || c == '©' || c == '·' || c == '+' || c == '&')
                    continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static void AddKey(List<string> keys, string raw)
        {
            if (raw == null) return;
            string k = Norm(raw);
            if (k.Length < 2) return;
            if (!keys.Contains(k)) keys.Add(k);
        }

        public static List<string> BuildKeys(ProgramInfo p)
        {
            List<string> keys = new List<string>();
            string n = p.Name == null ? "" : p.Name;

            AddKey(keys, InstalledApps.CleanName(n));

            string[] seps = new string[] { " - ", " — ", " – ", " | ", "|", " -- " };
            for (int i = 0; i < seps.Length; i++)
            {
                int pos = n.IndexOf(seps[i], StringComparison.OrdinalIgnoreCase);
                if (pos > 0)
                {
                    string first = n.Substring(0, pos);
                    AddKey(keys, InstalledApps.CleanName(first));
                }
            }

            string baseKey = InstalledApps.CleanName(n);
            string[] tails = new string[] { " pro", " premium", " professional", " free", " edition",
                " client", " desktop", " app", " suite", " home", " business", " player", " studio",
                " plus", " lite", " classic", " portable", " x64", " x86" };
            for (int i = 0; i < tails.Length; i++)
            {
                if (baseKey.EndsWith(tails[i], StringComparison.OrdinalIgnoreCase))
                {
                    string trimmed = baseKey.Substring(0, baseKey.Length - tails[i].Length).Trim();
                    if (trimmed.Length >= 3) AddKey(keys, trimmed);
                }
            }

            return keys;
        }

        // 弱关键词：名称里最有辨识度的最后一个单词（如 Google Chrome -> Chrome）
        public static List<string> BuildWeakKeys(ProgramInfo p)
        {
            List<string> weak = new List<string>();
            string clean = InstalledApps.CleanName(p.Name == null ? "" : p.Name);
            string[] parts = clean.Split(new char[] { ' ', '-', '_', '/', '|', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return weak;

            for (int i = parts.Length - 1; i >= 0 && i >= parts.Length - 2; i--)
            {
                string w = parts[i].Trim();
                if (w.Length < 4) continue;
                bool digitsOnly = true;
                for (int j = 0; j < w.Length; j++) { if (!char.IsDigit(w[j])) { digitsOnly = false; break; } }
                if (digitsOnly) continue;

                string low = w.ToLowerInvariant();
                bool blocked = false;
                for (int j = 0; j < WeakBlacklist.Length; j++)
                {
                    if (low == WeakBlacklist[j]) { blocked = true; break; }
                }
                if (blocked) continue;

                string k = Norm(w);
                if (k.Length >= 4 && !weak.Contains(k)) weak.Add(k);
                break;
            }
            return weak;
        }

        private static int Strength(string name, List<string> keys, List<string> weak)
        {
            string nm = Norm(name);
            if (nm.Length == 0) return 0;
            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
                if (k.Length >= 4)
                {
                    if (nm.IndexOf(k, StringComparison.Ordinal) >= 0) return 2;
                }
                else if (k.Length >= 2)
                {
                    if (nm == k || nm.StartsWith(k, StringComparison.Ordinal) || nm.EndsWith(k, StringComparison.Ordinal)) return 2;
                }
            }
            if (weak != null)
            {
                for (int i = 0; i < weak.Count; i++)
                {
                    string w = weak[i];
                    if (w.Length < 4) continue;
                    if (nm == w) return 1;
                    if (nm.StartsWith(w, StringComparison.Ordinal))
                    {
                        if (AllowedTail(nm.Substring(w.Length))) return 1;
                    }
                    else if (nm.EndsWith(w, StringComparison.Ordinal))
                    {
                        if (AllowedTail(nm.Substring(0, nm.Length - w.Length))) return 1;
                    }
                }
            }
            return 0;
        }

        private static readonly string[] TailWords = new string[] {
            "app","apps","application","applications","version","versions","data","cache","config",
            "configs","settings","setting","update","updater","updates","user","users","profile",
            "profiles","bin","x64","x86","portable","desktop","client","lite","pro","setup",
            "installer","runtime","core","plugin","plugins","extension","extensions","temp","logs",
            "log","64","32","bit","files","file","local","roaming","common","shared","library",
            "libraries","store","storage","assets","asset","resources","resource","package",
            "packages","service","services","driver","drivers","helper","helpers","agent","agents",
            "host","hosts","module","modules","component","components","theme","themes","lang",
            "language","languages","fonts","font","icon","icons","images","image","media","audio",
            "video","content","contents","old","new","backup","beta","alpha","dev","canary",
            "stable","nightly","current","default","main","program","programs","files64","files86"
        };

        // 弱关键词只允许接这些"修饰词"，避免 Edge -> EdgeWebView 这类跨产品误伤
        private static bool AllowedTail(string tail)
        {
            if (tail.Length == 0) return true;
            if (tail.Length > 14) return false;
            for (int i = 0; i < TailWords.Length; i++)
            {
                if (tail == TailWords[i]) return true;
                if (tail.StartsWith(TailWords[i], StringComparison.Ordinal))
                {
                    string rest = tail.Substring(TailWords[i].Length);
                    if (rest.Length == 0) return true;
                    if (AllowedTail(rest)) return true;
                }
            }
            return false;
        }

        private static int RiskOf(string fullPath, string name)
        {
            string low = fullPath.ToLowerInvariant();
            string nm = (name == null ? "" : name.ToLowerInvariant().Trim());

            if (low.IndexOf("\\windows\\", StringComparison.Ordinal) >= 0 ||
                low.IndexOf("\\winsxs", StringComparison.Ordinal) >= 0 ||
                low.IndexOf("\\system32", StringComparison.Ordinal) >= 0 ||
                low.IndexOf("\\syswow64", StringComparison.Ordinal) >= 0 ||
                low.IndexOf("\\windowsapps", StringComparison.Ordinal) >= 0 ||
                low.IndexOf("\\drivers\\", StringComparison.Ordinal) >= 0)
                return 2;

            if (nm == "microsoft" || nm == "windows" || nm == "common files" || nm == "package cache" ||
                nm == "packages" || nm == "temp" || nm == "cache" || nm == "program files" ||
                nm == "programdata" || nm == "appdata" || nm == "users" || nm == "system32")
                return 2;

            if (nm.Length <= 3) return 2;

            // 共享运行时 / 包管理器缓存：删了会连带破坏别的软件，一律标高风险且不自动删
            string[] sharedHints = new string[] {
                "\\uv\\", "\\node_modules\\", "\\site-packages\\", "\\cargo\\registry",
                "\\rustup\\", "\\go\\pkg\\", "\\miniconda", "\\anaconda", "\\nuget\\",
                "\\.venv\\", "\\venv\\", "\\virtualenv", "\\pyenv", "\\nvm\\", "\\fnm\\",
                "\\pip\\cache", "\\npm-cache\\", "\\nodejs\\node_modules"
            };
            for (int i = 0; i < sharedHints.Length; i++)
            {
                if (low.IndexOf(sharedHints[i], StringComparison.Ordinal) >= 0) return 2;
            }

            if (low.IndexOf("\\microsoft\\", StringComparison.Ordinal) >= 0) return 1;
            if (low.IndexOf("\\program files\\", StringComparison.Ordinal) >= 0 && nm.Length <= 5) return 1;

            return 0;
        }

        private static bool InSkip(string name, string[] skip)
        {
            if (skip == null) return false;
            string nm = name.ToLowerInvariant();
            for (int i = 0; i < skip.Length; i++)
            {
                if (nm == skip[i]) return true;
            }
            return false;
        }

        private static bool IsHardSkip(string name)
        {
            string nm = name.ToLowerInvariant();
            for (int i = 0; i < HardSkip.Length; i++)
            {
                if (nm == HardSkip[i]) return true;
            }
            return false;
        }

        private static void AddFolder(List<ResidueItem> items, HashSet<string> seen, string path, string typeName, int risk)
        {
            string full = path.Trim().Trim('"').TrimEnd('\\');
            if (full.Length < 4) return;
            if (seen.Contains(full)) return;
            if (!Directory.Exists(full)) return;

            string nm = "";
            try { nm = Path.GetFileName(full); }
            catch { return; }
            if (nm.Length == 0) return;

            seen.Add(full);
            ResidueItem it = new ResidueItem();
            it.Kind = ResidueItem.KindFolder;
            it.Path = full;
            it.TypeName = typeName;
            it.Risk = risk;
            // 高风险目录不算体积（省掉大目录的遍历时间）
            it.Size = risk >= 2 ? 0 : DirSize(full, 60000);
            it.Checked = risk < 2;
            items.Add(it);
        }

        private static void AddFile(List<ResidueItem> items, HashSet<string> seen, string path, string typeName, int risk)
        {
            if (seen.Contains(path)) return;
            if (!File.Exists(path)) return;
            seen.Add(path);
            ResidueItem it = new ResidueItem();
            it.Kind = ResidueItem.KindShortcut;
            it.Path = path;
            it.TypeName = typeName;
            it.Risk = risk;
            try { it.Size = new FileInfo(path).Length; }
            catch { it.Size = 0; }
            it.Checked = risk < 2;
            items.Add(it);
        }

        private static void ScanFolders(string root, int depth, string[] skip, List<ResidueItem> items,
            HashSet<string> seen, List<string> keys, List<string> weak, string typeName)
        {
            if (root == null || root.Length == 0) return;
            string[] dirs = null;
            try
            {
                if (!Directory.Exists(root)) return;
                dirs = Directory.GetDirectories(root);
            }
            catch { return; }
            if (dirs == null) return;

            for (int i = 0; i < dirs.Length; i++)
            {
                string d = dirs[i];
                string nm = "";
                try { nm = Path.GetFileName(d); }
                catch { continue; }
                if (nm.Length == 0) continue;

                int strength = Strength(nm, keys, weak);

                if (InSkip(nm, skip))
                {
                    // 公共/系统目录本身不算残留，但仍往下看一眼（如 Program Files\Microsoft\Edge）
                    if (depth > 1 && !IsHardSkip(nm))
                        ScanFolders(d, depth - 1, null, items, seen, keys, weak, typeName);
                    continue;
                }

                if (strength > 0)
                {
                    int risk = RiskOf(d, nm);
                    if (strength == 1)
                    {
                        string parent = "";
                        try { parent = Path.GetFileName(Path.GetDirectoryName(d)); }
                        catch { }
                        if (VendorPlausible(parent))
                        {
                            if (risk < 1) risk = 1;
                        }
                        else
                        {
                            risk = 2;   // 厂商对不上，很可能是别的软件的目录
                        }
                    }
                    AddFolder(items, seen, d, typeName, risk);
                }
                else if (depth > 1 && !IsHardSkip(nm))
                {
                    ScanFolders(d, depth - 1, ProgramDataSkip, items, seen, keys, weak, typeName);
                }
            }
        }

        private static void ScanLinks(string root, int depth, List<ResidueItem> items, HashSet<string> seen,
            List<string> keys, List<string> weak)
        {
            if (root == null || root.Length == 0) return;
            try
            {
                if (!Directory.Exists(root)) return;
                string[] files = Directory.GetFiles(root, "*.lnk");
                for (int i = 0; i < files.Length; i++)
                {
                    string nm = Path.GetFileNameWithoutExtension(files[i]);
                    int strength = Strength(nm, keys, weak);
                    if (strength > 0)
                        AddFile(items, seen, files[i], "开始菜单/桌面快捷方式", strength == 1 ? 1 : 0);
                }

                if (depth > 1)
                {
                    string[] dirs = Directory.GetDirectories(root);
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        ScanLinks(dirs[i], depth - 1, items, seen, keys, weak);
                    }
                }
            }
            catch { }
        }

        private static void ScanRegistry(ProgramInfo p, List<ResidueItem> items, HashSet<string> seen,
            List<string> keys, List<string> weak)
        {
            // 卸载项残留
            try
            {
                RegistryKey k = p.OpenKey();
                if (k != null)
                {
                    try
                    {
                        string disp = "";
                        object o = k.GetValue("DisplayName");
                        if (o != null) disp = o.ToString();
                        if (disp.Length > 0)
                            AddReg(items, seen, p.FullRegPath, "卸载注册项", 0);
                    }
                    catch { }
                    try { k.Close(); } catch { }
                }
            }
            catch { }

            ScanRegBranch(Registry.CurrentUser, "HKCU", "Software", keys, weak, items, seen);
            ScanRegBranch(Registry.LocalMachine, "HKLM", "SOFTWARE", keys, weak, items, seen);
            ScanRegBranch(Registry.LocalMachine, "HKLM", "SOFTWARE\\WOW6432Node", keys, weak, items, seen);
        }

        private static void ScanRegBranch(RegistryKey baseKey, string hive, string subRoot,
            List<string> keys, List<string> weak, List<ResidueItem> items, HashSet<string> seen)
        {
            RegistryKey root = null;
            try { root = baseKey.OpenSubKey(subRoot, false); }
            catch { return; }
            if (root == null) return;

            string[] subs = null;
            try { subs = root.GetSubKeyNames(); } catch { }

            if (subs != null)
            {
                for (int i = 0; i < subs.Length; i++)
                {
                    string nm = subs[i];
                    if (nm.Length == 0) continue;
                    if (IsHardSkip(nm)) continue;

                    int strength = Strength(nm, keys, null);
                    if (strength == 2)
                    {
                        AddReg(items, seen, hive + "\\" + subRoot + "\\" + nm, "软件注册表项", 1);
                        continue;
                    }

                    RegistryKey child = null;
                    try { child = root.OpenSubKey(nm, false); }
                    catch { continue; }
                    if (child == null) continue;
                    string[] subs2 = null;
                    try { subs2 = child.GetSubKeyNames(); } catch { }
                    if (subs2 != null)
                    {
                        for (int j = 0; j < subs2.Length; j++)
                        {
                            string nm2 = subs2[j];
                            if (nm2.Length == 0) continue;
                            if (Strength(nm2, keys, weak) > 0)
                                AddReg(items, seen, hive + "\\" + subRoot + "\\" + nm + "\\" + nm2, "软件注册表项", 1);
                        }
                    }
                    try { child.Close(); } catch { }
                }
            }

            try { root.Close(); } catch { }
        }

        private static void AddReg(List<ResidueItem> items, HashSet<string> seen, string path, string typeName, int risk)
        {
            if (seen.Contains(path)) return;
            seen.Add(path);
            ResidueItem it = new ResidueItem();
            it.Kind = ResidueItem.KindRegistry;
            it.Path = path;
            it.TypeName = typeName;
            it.Risk = risk;
            it.Size = 0;
            it.Checked = risk < 2;
            items.Add(it);
        }

        public static long DirSize(string dir, int maxEntries)
        {
            long total = 0;
            int count = 0;
            Stack<string> stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0 && count < maxEntries)
            {
                string cur = stack.Pop();
                try
                {
                    foreach (string f in Directory.EnumerateFiles(cur))
                    {
                        try { total += new FileInfo(f).Length; }
                        catch { }
                        count++;
                        if (count >= maxEntries) break;
                    }
                }
                catch { }
                try
                {
                    foreach (string d in Directory.EnumerateDirectories(cur))
                        stack.Push(d);
                }
                catch { }
            }
            return total;
        }
    }
}
