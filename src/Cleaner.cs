using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

namespace CleanUninstall
{
    public static class Cleaner
    {
        public static string DataDir = "";
        public static string BackupDir = "";
        public static string LogDir = "";

        public static void Init()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            DataDir = Path.Combine(appData, "CleanUninstall");
            BackupDir = Path.Combine(DataDir, "backup");
            LogDir = Path.Combine(DataDir, "logs");
            try { Directory.CreateDirectory(BackupDir); } catch { }
            try { Directory.CreateDirectory(LogDir); } catch { }
        }

        public static string NewLogPath()
        {
            string name = "clean-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
            return Path.Combine(LogDir, name);
        }

        public static void AppendLog(StringBuilder sb, string line)
        {
            sb.AppendLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line);
        }

        public static bool DeletePath(string path, bool permanent, out string err)
        {
            err = "";
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    err = "路径不存在";
                    return false;
                }
                if (Directory.Exists(path))
                {
                    if (permanent)
                        Directory.Delete(path, true);
                    else
                        FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                }
                else
                {
                    // 去掉只读属性，避免删不掉
                    try
                    {
                        FileAttributes fa = File.GetAttributes(path);
                        if ((fa & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            File.SetAttributes(path, fa & ~FileAttributes.ReadOnly);
                    }
                    catch { }

                    if (permanent)
                        File.Delete(path);
                    else
                        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                }
                return !(Directory.Exists(path) || File.Exists(path));
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }

        public static bool BackupRegistry(string fullPath, out string backupFile, out string err)
        {
            backupFile = "";
            err = "";
            try
            {
                string safe = fullPath.Replace("\\", "_").Replace(":", "").Replace(" ", "");
                if (safe.Length > 120) safe = safe.Substring(0, 120);
                backupFile = Path.Combine(BackupDir, safe + ".reg");

                ProcessStartInfo psi = new ProcessStartInfo("reg.exe", "export \"" + fullPath + "\" \"" + backupFile + "\" /y");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(30000);
                    if (p.HasExited && p.ExitCode == 0 && File.Exists(backupFile))
                        return true;
                }
                err = "导出注册表备份失败";
                return false;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }

        public static bool DeleteRegistry(string fullPath, out string err)
        {
            err = "";
            try
            {
                string hive = "";
                string sub = "";
                int i = fullPath.IndexOf('\\');
                if (i <= 0)
                {
                    err = "注册表路径无效";
                    return false;
                }
                hive = fullPath.Substring(0, i);
                sub = fullPath.Substring(i + 1);

                RegistryKey baseKey = null;
                if (hive == "HKLM" || hive == "HKEY_LOCAL_MACHINE") baseKey = Registry.LocalMachine;
                else if (hive == "HKCU" || hive == "HKEY_CURRENT_USER") baseKey = Registry.CurrentUser;
                else if (hive == "HKCR" || hive == "HKEY_CLASSES_ROOT") baseKey = Registry.ClassesRoot;
                else if (hive == "HKU" || hive == "HKEY_USERS") baseKey = Registry.Users;
                else
                {
                    err = "不支持的注册表根：" + hive;
                    return false;
                }

                using (RegistryKey probe = baseKey.OpenSubKey(sub, false))
                {
                    if (probe == null)
                    {
                        err = "注册表项不存在";
                        return false;
                    }
                }
                baseKey.DeleteSubKeyTree(sub, false);
                return true;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }

        public static string GetShortcutTarget(string lnkPath)
        {
            return "";
        }
    }
}
