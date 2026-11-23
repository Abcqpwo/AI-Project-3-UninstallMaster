using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LightNote
{
    static class C
    {
        public const string Name = "轻便记事";
        public const string Version = "1.0.0";
        public const string Publisher = "豆包测试";
        public const string KeyUninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LightNote";
        public const string KeyApp = @"Software\LightNote";
        public const string ExeName = "轻便记事.exe";
    }

    static class P
    {
        public static string DefaultInstallDir()
        {
            return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), "LightNote");
        }

        public static string RoamingDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LightNote");
        }

        public static string LocalDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightNote");
        }

        public static string ExePath(string dir)
        {
            return Path.Combine(dir, C.ExeName);
        }

        public static string InstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(C.KeyUninstall))
                {
                    if (k != null)
                    {
                        object o = k.GetValue("InstallLocation");
                        if (o != null)
                        {
                            string s = o.ToString().Trim().Trim('"').TrimEnd('\\');
                            if (s.Length > 0) return s;
                        }
                    }
                }
            }
            catch { }
            return DefaultInstallDir();
        }

        public static bool IsInstalled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(C.KeyUninstall))
                {
                    return k != null;
                }
            }
            catch { return false; }
        }
    }

    static class Shortcut
    {
        public static bool Create(string lnkPath, string target, string args, string workDir, string desc, string icon)
        {
            try
            {
                string dir = Path.GetDirectoryName(lnkPath);
                if (dir.Length > 0 && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return false;
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnkPath });
                if (sc == null) return false;
                Type sct = sc.GetType();
                sct.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                sct.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args });
                sct.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
                sct.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
                sct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { icon });
                sct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                return true;
            }
            catch { return false; }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = "auto";
            if (args != null && args.Length > 0)
            {
                string a = args[0].Trim().ToLowerInvariant();
                if (a == "/install" || a == "-install") mode = "install";
                else if (a == "/run" || a == "-run") mode = "run";
                else if (a == "/uninstall" || a == "-uninstall") mode = "uninstall";
            }

            if (mode == "auto")
            {
                bool installed = P.IsInstalled();
                string self = Application.ExecutablePath;
                string target = P.ExePath(P.InstalledDir());
                if (installed && string.Equals(self, target, StringComparison.OrdinalIgnoreCase)) mode = "run";
                else mode = "install";
            }

            try
            {
                if (mode == "run") Application.Run(new NoteForm());
                else if (mode == "uninstall") Application.Run(new UninstallForm());
                else Application.Run(new SetupForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show("程序出错：\n" + ex.Message, C.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ---------------- 安装向导 ----------------
    class SetupForm : Form
    {
        private Panel pWelcome, pProgress, pDone;
        private TextBox txtDir;
        private ProgressBar bar;
        private Label lblStep, lblPct;
        private CheckBox chkRun;
        private Button btnMain, btnCancel;
        private string installDir;
        private bool installing = false;

        public SetupForm()
        {
            this.Text = C.Name + " 安装程序";
            this.ClientSize = new Size(620, 430);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", 9f);

            installDir = P.DefaultInstallDir();

            // 顶部条
            Panel top = new Panel();
            top.BackColor = Color.FromArgb(37, 99, 235);
            top.Dock = DockStyle.Top;
            top.Height = 64;
            Label t1 = new Label();
            t1.Text = C.Name;
            t1.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            t1.ForeColor = Color.White;
            t1.AutoSize = true;
            t1.BackColor = Color.Transparent;
            t1.Location = new Point(24, 10);
            top.Controls.Add(t1);
            Label t2 = new Label();
            t2.Text = "版本 " + C.Version + " · " + C.Publisher;
            t2.ForeColor = Color.FromArgb(210, 228, 255);
            t2.AutoSize = true;
            t2.BackColor = Color.Transparent;
            t2.Location = new Point(26, 38);
            top.Controls.Add(t2);
            this.Controls.Add(top);

            // 底部按钮区
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 62;
            bottom.BackColor = Color.FromArgb(247, 248, 250);
            bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pn = new Pen(Color.FromArgb(226, 229, 234)))
                    e.Graphics.DrawLine(pn, 0, 0, bottom.ClientSize.Width, 0);
            };
            this.Controls.Add(bottom);

            btnMain = MakeButton("开始安装", true);
            btnMain.Size = new Size(130, 34);
            btnMain.Location = new Point(620 - 24 - 130, 14);
            btnMain.Click += delegate { OnMain(); };
            bottom.Controls.Add(btnMain);

            btnCancel = MakeButton("取消", false);
            btnCancel.Size = new Size(90, 34);
            btnCancel.Location = new Point(btnMain.Left - 10 - 90, 14);
            btnCancel.Click += delegate { this.Close(); };
            bottom.Controls.Add(btnCancel);

            // 第 1 页
            pWelcome = new Panel();
            pWelcome.Dock = DockStyle.Fill;
            pWelcome.BackColor = Color.White;
            this.Controls.Add(pWelcome);

            Label w1 = new Label();
            w1.Text = "欢迎安装 " + C.Name;
            w1.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            w1.ForeColor = Color.FromArgb(32, 36, 44);
            w1.AutoSize = true;
            w1.Location = new Point(26, 24);
            pWelcome.Controls.Add(w1);

            Label w2 = new Label();
            w2.Text = "一个很小的记事本，用来测试「卸载大师」的清理效果。\n它会把程序装进安装目录，并创建快捷方式、写入注册表和缓存数据。";
            w2.ForeColor = Color.FromArgb(110, 116, 128);
            w2.AutoSize = true;
            w2.Location = new Point(28, 62);
            pWelcome.Controls.Add(w2);

            Label w3 = new Label();
            w3.Text = "安装位置";
            w3.ForeColor = Color.FromArgb(32, 36, 44);
            w3.AutoSize = true;
            w3.Location = new Point(28, 130);
            pWelcome.Controls.Add(w3);

            txtDir = new TextBox();
            txtDir.Text = installDir;
            txtDir.Location = new Point(30, 156);
            txtDir.Size = new Size(452, 26);
            txtDir.BorderStyle = BorderStyle.FixedSingle;
            pWelcome.Controls.Add(txtDir);

            Button btnBrowse = MakeButton("浏览…", false);
            btnBrowse.Size = new Size(78, 28);
            btnBrowse.Location = new Point(490, 155);
            btnBrowse.Click += delegate
            {
                FolderBrowserDialog fb = new FolderBrowserDialog();
                fb.Description = "选择安装到哪里（会在这个文件夹里新建 LightNote 目录）";
                if (fb.ShowDialog(this) == DialogResult.OK)
                {
                    txtDir.Text = Path.Combine(fb.SelectedPath, "LightNote");
                }
            };
            pWelcome.Controls.Add(btnBrowse);

            Label w4 = new Label();
            w4.Text = "不需要管理员权限，装在你自己的用户目录里。默认位置是上面这个。";
            w4.ForeColor = Color.FromArgb(140, 146, 158);
            w4.AutoSize = true;
            w4.Location = new Point(30, 190);
            pWelcome.Controls.Add(w4);

            Label w5 = new Label();
            w5.Text = "安装后会有：\r\n" +
                      "  · 桌面快捷方式   · 开始菜单快捷方式\r\n" +
                      "  · 注册表安装信息（卸载大师能识别到它）\r\n" +
                      "  · 笔记数据目录和缓存目录（约 1 MB）";
            w5.ForeColor = Color.FromArgb(70, 76, 88);
            w5.AutoSize = true;
            w5.Location = new Point(30, 226);
            pWelcome.Controls.Add(w5);

            // 第 2 页（进度）
            pProgress = new Panel();
            pProgress.Dock = DockStyle.Fill;
            pProgress.BackColor = Color.White;
            pProgress.Visible = false;
            this.Controls.Add(pProgress);

            Label g1 = new Label();
            g1.Text = "正在安装…";
            g1.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            g1.ForeColor = Color.FromArgb(32, 36, 44);
            g1.AutoSize = true;
            g1.Location = new Point(26, 40);
            pProgress.Controls.Add(g1);

            bar = new ProgressBar();
            bar.Location = new Point(28, 86);
            bar.Size = new Size(560, 22);
            bar.Minimum = 0;
            bar.Maximum = 100;
            pProgress.Controls.Add(bar);

            lblStep = new Label();
            lblStep.Text = "准备中…";
            lblStep.ForeColor = Color.FromArgb(110, 116, 128);
            lblStep.AutoSize = true;
            lblStep.Location = new Point(28, 118);
            pProgress.Controls.Add(lblStep);

            lblPct = new Label();
            lblPct.Text = "0%";
            lblPct.ForeColor = Color.FromArgb(37, 99, 235);
            lblPct.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            lblPct.AutoSize = true;
            lblPct.Location = new Point(540, 40);
            pProgress.Controls.Add(lblPct);

            Label g2 = new Label();
            g2.Text = "安装过程中请不要关闭窗口。";
            g2.ForeColor = Color.FromArgb(140, 146, 158);
            g2.AutoSize = true;
            g2.Location = new Point(28, 210);
            pProgress.Controls.Add(g2);

            // 第 3 页（完成）
            pDone = new Panel();
            pDone.Dock = DockStyle.Fill;
            pDone.BackColor = Color.White;
            pDone.Visible = false;
            this.Controls.Add(pDone);

            Label d1 = new Label();
            d1.Text = "安装完成";
            d1.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            d1.ForeColor = Color.FromArgb(22, 140, 90);
            d1.AutoSize = true;
            d1.Location = new Point(26, 30);
            pDone.Controls.Add(d1);

            Label d2 = new Label();
            d2.Text = "「" + C.Name + "」已经装好了。桌面和开始菜单里都能找到它。\n\n现在可以用「卸载大师」来试：在桌面右键那个快捷方式，选「用卸载大师卸载」。";
            d2.ForeColor = Color.FromArgb(70, 76, 88);
            d2.AutoSize = true;
            d2.Location = new Point(28, 76);
            pDone.Controls.Add(d2);

            chkRun = new CheckBox();
            chkRun.Text = "立即运行 " + C.Name;
            chkRun.Checked = true;
            chkRun.AutoSize = true;
            chkRun.Location = new Point(30, 190);
            pDone.Controls.Add(chkRun);

            Label d3 = new Label();
            d3.Text = "安装位置：" + installDir;
            d3.ForeColor = Color.FromArgb(140, 146, 158);
            d3.AutoSize = false;
            d3.Size = new Size(560, 20);
            d3.AutoEllipsis = true;
            d3.Location = new Point(30, 230);
            pDone.Controls.Add(d3);

            this.AcceptButton = btnMain;
            this.CancelButton = btnCancel;
            this.FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (installing) e.Cancel = true;
            };
        }

        private Button MakeButton(string text, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.Font = new Font("Microsoft YaHei UI", 9f, primary ? FontStyle.Bold : FontStyle.Regular);
            if (primary)
            {
                b.BackColor = Color.FromArgb(37, 99, 235);
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Color.FromArgb(60, 66, 78);
                b.FlatAppearance.BorderColor = Color.FromArgb(206, 210, 218);
            }
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private void OnMain()
        {
            if (pWelcome.Visible)
            {
                string d = txtDir.Text.Trim();
                if (d.Length == 0)
                {
                    MessageBox.Show("请先选一个安装位置。", C.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (Directory.Exists(d) && Directory.GetFileSystemEntries(d).Length > 0)
                {
                    DialogResult dr = MessageBox.Show(
                        "这个文件夹里已经有东西了：\n" + d + "\n\n继续会把文件装进这个文件夹，可能弄乱它。确定继续吗？",
                        C.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr != DialogResult.Yes) return;
                }
                installDir = d;
                pWelcome.Visible = false;
                pProgress.Visible = true;
                btnMain.Text = "请稍候…";
                btnMain.Enabled = false;
                btnCancel.Enabled = false;
                installing = true;
                DoInstall();
                return;
            }

            if (pDone.Visible)
            {
                installing = false;
                string exe = P.ExePath(installDir);
                if (chkRun.Checked && File.Exists(exe))
                {
                    try { Process.Start(new ProcessStartInfo(exe, "/run") { UseShellExecute = true }); }
                    catch { }
                }
                this.Close();
            }
        }

        private void Step(int pct, string text)
        {
            bar.Value = pct;
            lblPct.Text = pct + "%";
            lblStep.Text = text;
            Application.DoEvents();
        }

        private void DoInstall()
        {
            try
            {
                Step(5, "检查安装目录…");
                if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);

                Step(15, "复制程序文件…");
                string target = P.ExePath(installDir);
                File.Copy(Application.ExecutablePath, target, true);

                Step(30, "写入说明文件和资源…");
                File.WriteAllText(Path.Combine(installDir, "readme.txt"),
                    C.Name + " " + C.Version + "\r\n这是一个用来测试卸载工具的小程序。\r\n把它卸载以后，你可以看看有没有残留被清理掉。\r\n",
                    Encoding.UTF8);

                string plugins = Path.Combine(installDir, "plugins");
                if (!Directory.Exists(plugins)) Directory.CreateDirectory(plugins);

                string res = Path.Combine(installDir, "resources");
                if (!Directory.Exists(res)) Directory.CreateDirectory(res);
                WriteDummy(Path.Combine(res, "sample.dat"), 1024 * 1024);
                File.WriteAllText(Path.Combine(res, "version.json"),
                    "{\r\n  \"name\": \"" + C.Name + "\",\r\n  \"version\": \"" + C.Version + "\"\r\n}\r\n", Encoding.UTF8);

                Step(50, "创建数据目录…");
                if (!Directory.Exists(P.RoamingDir())) Directory.CreateDirectory(P.RoamingDir());
                File.WriteAllText(Path.Combine(P.RoamingDir(), "config.ini"),
                    "[app]\r\nname=" + C.Name + "\r\nversion=" + C.Version + "\r\nfirstrun=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n",
                    Encoding.UTF8);
                string notes = Path.Combine(P.RoamingDir(), "notes.txt");
                if (!File.Exists(notes))
                    File.WriteAllText(notes, "这里可以随便写点东西。\r\n写下来的内容会自动保存到这个文件夹里。\r\n", Encoding.UTF8);

                if (!Directory.Exists(P.LocalDir())) Directory.CreateDirectory(P.LocalDir());
                string cache = Path.Combine(P.LocalDir(), "cache");
                if (!Directory.Exists(cache)) Directory.CreateDirectory(cache);
                for (int i = 0; i < 4; i++)
                    WriteDummy(Path.Combine(cache, "cache_" + i + ".bin"), 64 * 1024);

                Step(65, "写入注册表…");
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(C.KeyApp))
                {
                    k.SetValue("InstallDir", installDir, RegistryValueKind.String);
                    k.SetValue("Version", C.Version, RegistryValueKind.String);
                    k.SetValue("FirstRun", DateTime.Now.ToString("s"), RegistryValueKind.String);
                    k.SetValue("Theme", "light", RegistryValueKind.String);
                }

                long sizeBytes = DirSize(installDir);
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(C.KeyUninstall))
                {
                    k.SetValue("DisplayName", C.Name, RegistryValueKind.String);
                    k.SetValue("DisplayVersion", C.Version, RegistryValueKind.String);
                    k.SetValue("Publisher", C.Publisher, RegistryValueKind.String);
                    k.SetValue("InstallLocation", installDir, RegistryValueKind.String);
                    k.SetValue("DisplayIcon", target + ",0", RegistryValueKind.String);
                    k.SetValue("UninstallString", "\"" + target + "\" /uninstall", RegistryValueKind.String);
                    k.SetValue("QuietUninstallString", "\"" + target + "\" /uninstall /silent", RegistryValueKind.String);
                    k.SetValue("EstimatedSize", (int)Math.Max(1, sizeBytes / 1024), RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                }

                Step(80, "创建快捷方式…");
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                Shortcut.Create(Path.Combine(desktop, C.Name + ".lnk"), target, "/run", installDir, C.Name, target + ",0");
                Shortcut.Create(Path.Combine(programs, C.Name + ".lnk"), target, "/run", installDir, C.Name, target + ",0");

                Step(100, "完成");
                pProgress.Visible = false;
                pDone.Visible = true;
                btnMain.Text = "完成";
                btnMain.Enabled = true;
                btnCancel.Visible = false;
                installing = false;
            }
            catch (Exception ex)
            {
                installing = false;
                pProgress.Visible = false;
                pWelcome.Visible = true;
                btnMain.Text = "开始安装";
                btnMain.Enabled = true;
                btnCancel.Enabled = true;
                MessageBox.Show("安装失败：\n" + ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void WriteDummy(string path, int bytes)
        {
            byte[] buf = new byte[Math.Min(bytes, 8192)];
            for (int i = 0; i < buf.Length; i++) buf[i] = (byte)('A' + (i % 26));
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                int left = bytes;
                while (left > 0)
                {
                    int n = Math.Min(left, buf.Length);
                    fs.Write(buf, 0, n);
                    left -= n;
                }
            }
        }

        private static long DirSize(string dir)
        {
            long total = 0;
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                FileInfo[] files = di.GetFiles("*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++) total += files[i].Length;
            }
            catch { }
            return total;
        }
    }

    // ---------------- 应用本体 ----------------
    class NoteForm : Form
    {
        private TextBox txt;
        private Label lblState;
        private string notesPath;
        private bool dirty = false;

        public NoteForm()
        {
            this.Text = C.Name + " " + C.Version;
            this.ClientSize = new Size(640, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", 9f);
            this.MinimumSize = new Size(420, 320);

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 46;
            top.BackColor = Color.FromArgb(37, 99, 235);
            this.Controls.Add(top);

            Label title = new Label();
            title.Text = C.Name;
            title.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            title.Location = new Point(16, 12);
            top.Controls.Add(title);

            Button btnSave = new Button();
            btnSave.Text = "保存";
            btnSave.Size = new Size(76, 28);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 1;
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(200, 220, 255);
            btnSave.BackColor = Color.FromArgb(59, 118, 240);
            btnSave.ForeColor = Color.White;
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += delegate { SaveNotes(true); };
            btnSave.Location = new Point(640 - 16 - 76, 9);
            btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            top.Controls.Add(btnSave);

            Label tip = new Label();
            tip.Text = "改完点保存，或者直接关窗口";
            tip.ForeColor = Color.FromArgb(205, 224, 255);
            tip.AutoSize = true;
            tip.BackColor = Color.Transparent;
            tip.Location = new Point(110, 16);
            top.Controls.Add(tip);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 30;
            bottom.BackColor = Color.FromArgb(247, 248, 250);
            this.Controls.Add(bottom);

            lblState = new Label();
            lblState.Text = "就绪";
            lblState.ForeColor = Color.FromArgb(120, 126, 138);
            lblState.AutoSize = true;
            lblState.Location = new Point(10, 7);
            bottom.Controls.Add(lblState);

            txt = new TextBox();
            txt.Multiline = true;
            txt.Dock = DockStyle.Fill;
            txt.ScrollBars = ScrollBars.Vertical;
            txt.BorderStyle = BorderStyle.None;
            txt.Font = new Font("Microsoft YaHei UI", 11f);
            txt.BackColor = Color.White;
            txt.ForeColor = Color.FromArgb(32, 36, 44);
            txt.TextChanged += delegate
            {
                dirty = true;
                lblState.Text = "未保存 · " + txt.TextLength + " 字";
            };
            this.Controls.Add(txt);

            notesPath = Path.Combine(P.RoamingDir(), "notes.txt");
            LoadNotes();
            WriteSession();

            this.FormClosing += delegate
            {
                if (dirty) SaveNotes(false);
            };

            // 每 20 秒自动保存一次
            Timer t = new Timer();
            t.Interval = 20000;
            t.Tick += delegate { if (dirty) SaveNotes(false); };
            t.Start();
        }

        private void LoadNotes()
        {
            try
            {
                if (File.Exists(notesPath))
                {
                    txt.Text = File.ReadAllText(notesPath, Encoding.UTF8);
                    dirty = false;
                    lblState.Text = "已读取 " + txt.TextLength + " 字";
                }
                else
                {
                    txt.Text = "这个记事本的文字会保存到：\r\n" + notesPath + "\r\n\r\n把它卸载以后，这个文件会留在那里 —— 正好用来测试清理功能。\r\n";
                    dirty = false;
                    lblState.Text = "新建";
                }
            }
            catch { }
        }

        private void SaveNotes(bool showTip)
        {
            try
            {
                if (!Directory.Exists(P.RoamingDir())) Directory.CreateDirectory(P.RoamingDir());
                File.WriteAllText(notesPath, txt.Text, Encoding.UTF8);
                File.WriteAllText(Path.Combine(P.RoamingDir(), "config.ini"),
                    "[app]\r\nname=" + C.Name + "\r\nversion=" + C.Version + "\r\nlastsave=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n",
                    Encoding.UTF8);
                dirty = false;
                lblState.Text = "已保存 · " + DateTime.Now.ToString("HH:mm:ss");
                if (showTip) MessageBox.Show("已保存。", C.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (showTip) MessageBox.Show("保存失败：" + ex.Message, C.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void WriteSession()
        {
            try
            {
                string cache = Path.Combine(P.LocalDir(), "cache");
                if (!Directory.Exists(cache)) Directory.CreateDirectory(cache);
                File.WriteAllText(Path.Combine(cache, "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log"),
                    "启动时间: " + DateTime.Now.ToString("s") + "\r\n进程: " + Process.GetCurrentProcess().Id + "\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }
    }

    // ---------------- 卸载 ----------------
    class UninstallForm : Form
    {
        private Label lblStep;
        private Button btnGo, btnCancel;
        private string dir;
        private bool working = false;

        public UninstallForm()
        {
            this.Text = "卸载 " + C.Name;
            this.ClientSize = new Size(540, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", 9f);
            dir = P.InstalledDir();

            Panel top = new Panel();
            top.BackColor = Color.FromArgb(214, 66, 66);
            top.Dock = DockStyle.Top;
            top.Height = 56;
            Label t1 = new Label();
            t1.Text = "卸载 " + C.Name;
            t1.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            t1.ForeColor = Color.White;
            t1.AutoSize = true;
            t1.BackColor = Color.Transparent;
            t1.Location = new Point(22, 12);
            top.Controls.Add(t1);
            this.Controls.Add(top);

            Label msg = new Label();
            msg.Text = "会把程序文件删掉，并移除安装记录。";
            msg.ForeColor = Color.FromArgb(60, 66, 78);
            msg.AutoSize = true;
            msg.Location = new Point(24, 84);
            this.Controls.Add(msg);

            Label msg2 = new Label();
            msg2.Text = "注意：这个测试程序故意留了点东西（数据目录、缓存、快捷方式），\n用来验证「卸载大师」能不能找出来清掉。";
            msg2.ForeColor = Color.FromArgb(140, 146, 158);
            msg2.AutoSize = true;
            msg2.Location = new Point(24, 116);
            this.Controls.Add(msg2);

            Label msg3 = new Label();
            msg3.Text = "安装位置：" + dir;
            msg3.ForeColor = Color.FromArgb(140, 146, 158);
            msg3.AutoSize = false;
            msg3.Size = new Size(480, 20);
            msg3.AutoEllipsis = true;
            msg3.Location = new Point(24, 170);
            this.Controls.Add(msg3);

            lblStep = new Label();
            lblStep.Text = "";
            lblStep.ForeColor = Color.FromArgb(37, 99, 235);
            lblStep.AutoSize = true;
            lblStep.Location = new Point(24, 206);
            this.Controls.Add(lblStep);

            btnGo = new Button();
            btnGo.Text = "卸载";
            btnGo.Size = new Size(110, 34);
            btnGo.Location = new Point(540 - 24 - 110, 244);
            btnGo.FlatStyle = FlatStyle.Flat;
            btnGo.BackColor = Color.FromArgb(214, 66, 66);
            btnGo.ForeColor = Color.White;
            btnGo.FlatAppearance.BorderColor = Color.FromArgb(214, 66, 66);
            btnGo.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            btnGo.UseVisualStyleBackColor = false;
            btnGo.Cursor = Cursors.Hand;
            btnGo.Click += delegate { DoUninstall(); };
            this.Controls.Add(btnGo);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Size = new Size(90, 34);
            btnCancel.Location = new Point(btnGo.Left - 10 - 90, 244);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.BackColor = Color.White;
            btnCancel.ForeColor = Color.FromArgb(60, 66, 78);
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(206, 210, 218);
            btnCancel.UseVisualStyleBackColor = false;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += delegate { this.Close(); };
            this.Controls.Add(btnCancel);

            this.CancelButton = btnCancel;
            this.FormClosing += delegate(object s, FormClosingEventArgs e) { if (working) e.Cancel = true; };
        }

        private void DoUninstall()
        {
            if (working) return;
            working = true;
            btnGo.Enabled = false;
            btnCancel.Enabled = false;

            try
            {
                lblStep.Text = "移除安装记录…";
                Application.DoEvents();
                try { Registry.CurrentUser.DeleteSubKeyTree(C.KeyUninstall, false); }
                catch { }

                lblStep.Text = "删除程序文件…";
                Application.DoEvents();
                string exe = P.ExePath(dir);
                if (File.Exists(exe))
                {
                    try
                    {
                        // 自己还在运行，交给 cmd 稍后删掉
                        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                        psi.Arguments = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + exe + "\"";
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;
                        psi.WindowStyle = ProcessWindowStyle.Hidden;
                        Process.Start(psi);
                    }
                    catch { }
                }

                lblStep.Text = "完成";
                Application.DoEvents();
                working = false;

                MessageBox.Show(
                    "「" + C.Name + "」已卸载。\n\n" +
                    "下面这些东西被故意留下了，可以去「卸载大师」里清理：\n" +
                    "· 安装目录里剩下的文件\n" +
                    "· 笔记和配置（%AppData%\\LightNote）\n" +
                    "· 缓存（%LocalAppData%\\LightNote）\n" +
                    "· 桌面和开始菜单的快捷方式\n" +
                    "· 注册表里的设置项",
                    "卸载完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.Close();
            }
            catch (Exception ex)
            {
                working = false;
                btnGo.Enabled = true;
                btnCancel.Enabled = true;
                MessageBox.Show("卸载失败：" + ex.Message, "卸载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
