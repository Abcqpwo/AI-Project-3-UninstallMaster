using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CleanUninstall
{
    // 桌面/资源管理器右键菜单：用卸载大师卸载
    public static class ShellMenu
    {
        private const string Verb = "UninstallMaster";
        private const string StateKey = @"Software\CleanUninstall";
        private const string StateValue = "MenuState";

        private static readonly string[] Classes = new string[] { "exefile", "lnkfile", "Directory", "*" };

        private static string KeyPath(string cls)
        {
            return @"Software\Classes\" + cls + @"\shell\" + Verb;
        }

        public static bool IsRegistered()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath("exefile") + @"\command"))
                {
                    return k != null;
                }
            }
            catch { return false; }
        }

        private static void SetState(int v)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(StateKey))
                {
                    if (k != null) k.SetValue(StateValue, v, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static int GetState()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(StateKey))
                {
                    if (k == null) return -1;
                    object o = k.GetValue(StateValue);
                    if (o == null) return -1;
                    return Convert.ToInt32(o);
                }
            }
            catch { return -1; }
        }

        // 第一次运行自动挂上；用户手动关掉后不再自动恢复
        public static void EnsureDefault()
        {
            if (IsRegistered()) { if (GetState() == -1) SetState(1); return; }
            if (GetState() == 0) return;
            Register();
        }

        public static bool Register()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string cmd = "\"" + exe + "\" /target \"%1\"";

                for (int i = 0; i < Classes.Length; i++)
                {
                    string cls = Classes[i];
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath(cls)))
                    {
                        if (k == null) continue;
                        k.SetValue("", "用卸载大师卸载");
                        k.SetValue("Icon", exe + ",0");
                        k.SetValue("MultiSelectModel", "Single");
                        using (RegistryKey c = k.CreateSubKey("command"))
                        {
                            if (c != null) c.SetValue("", cmd);
                        }
                    }

                    // 让菜单项默认不抢"打开"的位置
                    try
                    {
                        using (RegistryKey pos = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + cls + @"\shell"))
                        {
                            if (pos != null && pos.GetValue("") == null) pos.SetValue("", "open");
                        }
                    }
                    catch { }
                }

                SetState(1);
                return true;
            }
            catch { return false; }
        }

        public static bool Unregister()
        {
            bool ok = true;
            for (int i = 0; i < Classes.Length; i++)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(KeyPath(Classes[i]), false);
                }
                catch { ok = false; }
            }
            SetState(0);
            return ok;
        }
    }
}
