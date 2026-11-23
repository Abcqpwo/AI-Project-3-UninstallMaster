using System;
using Microsoft.Win32;

namespace CleanUninstall
{
    public class ProgramInfo
    {
        public string Name = "";
        public string Version = "";
        public string Publisher = "";
        public string UninstallString = "";
        public string QuietUninstallString = "";
        public string InstallLocation = "";
        public string DisplayIcon = "";
        public long SizeKB = 0;
        public string HiveName = "HKLM";      // HKLM / HKCU
        public string SubPath = "";           // 相对路径
        public bool Is64 = true;
        public bool IsUser = false;

        public RegistryKey OpenKey()
        {
            try
            {
                RegistryKey root = (HiveName == "HKCU") ? Registry.CurrentUser : Registry.LocalMachine;
                return root.OpenSubKey(SubPath, false);
            }
            catch { return null; }
        }

        public string FullRegPath
        {
            get { return HiveName + "\\" + SubPath; }
        }
    }

    public class ResidueItem
    {
        public const int KindFolder = 0;
        public const int KindFile = 1;
        public const int KindShortcut = 2;
        public const int KindRegistry = 3;

        public int Kind;
        public string Path = "";        // 文件路径 或 注册表全路径
        public string TypeName = "";
        public long Size = 0;
        public int Risk = 0;            // 0 低 1 中 2 高
        public bool Checked = true;

        public string RiskText
        {
            get { return Risk == 0 ? "低" : (Risk == 1 ? "中" : "高"); }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "-";
            double v = bytes;
            if (v < 1024) return v.ToString("0") + " B";
            if (v < 1024 * 1024) return (v / 1024).ToString("0.0") + " KB";
            if (v < 1024.0 * 1024 * 1024) return (v / 1024 / 1024).ToString("0.0") + " MB";
            return (v / 1024 / 1024 / 1024).ToString("0.00") + " GB";
        }
    }
}
