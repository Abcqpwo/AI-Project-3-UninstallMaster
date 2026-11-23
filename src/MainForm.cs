using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CleanUninstall
{
    public class MainForm : Form
    {
        private Panel header, bar, footer, listHost;
        private ListView lv;
        private HintBox txtSearch;
        private Label lblCount, lblStatus;
        private Button btnRefresh, btnScanOther, btnUninstall, btnScanOnly;
        private LinkLabel lnkRecords, lnkMenu;
        private List<ProgramInfo> allApps = new List<ProgramInfo>();
        private int hoverIndex = -1;
        private string startTarget;

        public MainForm() : this(null) { }

        public MainForm(string target)
        {
            startTarget = target;
            Cleaner.Init();
            ShellMenu.EnsureDefault();
            BuildUi();
            RefreshList();
            if (startTarget != null) this.ShowInTaskbar = false;
        }

        private void BuildUi()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(940, 560);
            BackColor = T.Bg;
            Text = "卸载大师";
            Font = T.F9;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            // ---- 顶部 ----
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = T.Panel;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(T.Line)) e.Graphics.DrawLine(p, 0, header.ClientSize.Height - 1, header.ClientSize.Width, header.ClientSize.Height - 1);
            };

            Label title = T.Lbl("卸载大师", T.FTitle, T.Text);
            title.Location = new Point(26, 14);
            header.Controls.Add(title);

            Label sub = T.Lbl("卸载软件，并把它的文件、缓存、快捷方式和注册表一并清干净", T.F9, T.Sub);
            sub.Location = new Point(29, 56);
            header.Controls.Add(sub);

            btnRefresh = T.Ghost("刷新列表");
            btnRefresh.Size = new Size(104, 36);
            btnRefresh.Click += delegate { RefreshList(); };
            header.Controls.Add(btnRefresh);

            btnScanOther = T.Ghost("扫描其它残留");
            btnScanOther.Size = new Size(130, 36);
            btnScanOther.Click += delegate { ScanOthers(); };
            header.Controls.Add(btnScanOther);

            header.Resize += delegate { LayoutHeader(); };

            // ---- 搜索条 ----
            bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 64;
            bar.BackColor = T.Bg;

            txtSearch = new HintBox();
            txtSearch.Hint = "搜索软件名称 / 发布者…";
            txtSearch.Location = new Point(26, 15);
            txtSearch.Size = new Size(320, 30);
            txtSearch.BackColor = T.PanelAlt;
            txtSearch.ForeColor = T.Text;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.Font = T.F10;
            txtSearch.TextChanged += delegate { FillList(); };
            bar.Controls.Add(txtSearch);

            lblCount = T.Lbl("", T.F9, T.Sub);
            lblCount.Location = new Point(360, 23);
            bar.Controls.Add(lblCount);

            btnUninstall = T.Primary("卸载并彻底清理");
            btnUninstall.Size = new Size(180, 42);
            btnUninstall.Click += delegate { UninstallSelected(); };
            bar.Controls.Add(btnUninstall);

            btnScanOnly = T.Ghost("只扫描残留");
            btnScanOnly.Size = new Size(118, 42);
            btnScanOnly.Click += delegate { ScanOnlySelected(); };
            bar.Controls.Add(btnScanOnly);

            bar.Resize += delegate { LayoutBar(); };

            // ---- 列表 ----
            listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = T.Bg;
            listHost.Padding = new Padding(26, 2, 26, 8);

            lv = new ListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = T.Panel;
            lv.ForeColor = T.Text;
            lv.Font = T.F95;
            lv.OwnerDraw = true;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.Columns.Add("软件名称", 360);
            lv.Columns.Add("版本", 110);
            lv.Columns.Add("发布者", 200);
            lv.Columns.Add("大小", 96);
            lv.Columns.Add("类型", 76);
            lv.DrawColumnHeader += LvDrawHeader;
            lv.DrawItem += LvDrawItem;
            lv.DrawSubItem += LvDrawSubItem;
            lv.DoubleClick += delegate { UninstallSelected(); };
            lv.Resize += delegate { AdjustColumns(); lv.Invalidate(); };
            lv.MouseMove += delegate(object s, MouseEventArgs e)
            {
                int idx = -1;
                ListViewHitTestInfo info = lv.HitTest(e.X, e.Y);
                if (info.Item != null) idx = info.Item.Index;
                if (idx != hoverIndex) { hoverIndex = idx; lv.Invalidate(); }
            };
            lv.MouseLeave += delegate { if (hoverIndex != -1) { hoverIndex = -1; lv.Invalidate(); } };

            ImageList il = new ImageList();
            il.ImageSize = new Size(1, 28);
            il.ColorDepth = ColorDepth.Depth32Bit;
            il.Images.Add(new Bitmap(1, 28));
            lv.SmallImageList = il;

            listHost.Controls.Add(lv);

            // ---- 底部 ----
            footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 44;
            footer.BackColor = T.Panel;
            footer.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pn = new Pen(T.Line)) e.Graphics.DrawLine(pn, 0, 0, footer.ClientSize.Width, 0);
            };

            lblStatus = T.Lbl("删除的文件默认放进回收站，随时可以还原", T.F85, T.Sub);
            lblStatus.Location = new Point(26, 14);
            footer.Controls.Add(lblStatus);

            lnkMenu = new LinkLabel();
            lnkMenu.AutoSize = true;
            lnkMenu.Font = T.F85;
            lnkMenu.LinkColor = T.Accent;
            lnkMenu.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkMenu.BackColor = Color.Transparent;
            lnkMenu.Location = new Point(400, 14);
            lnkMenu.LinkClicked += delegate { ToggleMenu(); };
            footer.Controls.Add(lnkMenu);

            lnkRecords = new LinkLabel();
            lnkRecords.Text = "清理记录 / 注册表备份";
            lnkRecords.AutoSize = true;
            lnkRecords.Font = T.F85;
            lnkRecords.LinkColor = T.Accent;
            lnkRecords.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkRecords.BackColor = Color.Transparent;
            lnkRecords.Location = new Point(600, 14);
            lnkRecords.LinkClicked += delegate { OpenDataFolder(); };
            footer.Controls.Add(lnkRecords);

            footer.Resize += delegate
            {
                lnkRecords.Location = new Point(footer.ClientSize.Width - 26 - lnkRecords.Width, 14);
                lnkMenu.Location = new Point(lnkRecords.Left - 22 - lnkMenu.Width, 14);
            };

            Controls.Add(listHost);
            Controls.Add(footer);
            Controls.Add(bar);
            Controls.Add(header);

            LayoutHeader();
            LayoutBar();
            UpdateMenuLink();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeApplier.DarkTitleBar(this);
            ThemeApplier.DarkScroll(lv);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (startTarget != null)
            {
                this.Opacity = 0.0;
                this.BeginInvoke(new Action(RunTargetFlow));
                return;
            }
            ThemeApplier.FitToScreen(this, true);
            lv.Invalidate();
        }

        private void UpdateMenuLink()
        {
            if (ShellMenu.IsRegistered())
            {
                lnkMenu.Text = "右键菜单：已启用";
                lnkMenu.LinkColor = T.Ok;
            }
            else
            {
                lnkMenu.Text = "启用右键菜单";
                lnkMenu.LinkColor = T.Accent;
            }
        }

        private void ToggleMenu()
        {
            if (ShellMenu.IsRegistered())
            {
                DialogResult dr = MessageBox.Show(
                    "关掉右键菜单后，在桌面右键就找不到「用卸载大师卸载」了。\n\n确定关掉吗？",
                    "卸载大师", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
                ShellMenu.Unregister();
                UpdateMenuLink();
                lblStatus.Text = "右键菜单已关闭";
                return;
            }

            if (ShellMenu.Register())
            {
                UpdateMenuLink();
                MessageBox.Show(
                    "右键菜单已启用。\n\n现在在桌面（或资源管理器里）对准一个快捷方式、exe 文件或程序文件夹点右键，" +
                    "在菜单里选「用卸载大师卸载」，就会直接进入卸载流程。\n\n" +
                    "Windows 11 如果一级右键菜单里没看到，点一下「显示更多选项」就能找到。",
                    "启用成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("写入右键菜单失败，可能是系统限制。\n可以先用主界面列表卸载。",
                    "启用失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LayoutHeader()
        {
            int w = header.ClientSize.Width;
            btnRefresh.Location = new Point(w - 26 - btnRefresh.Width, 32);
            btnScanOther.Location = new Point(w - 26 - btnRefresh.Width - 8 - btnScanOther.Width, 32);
        }

        private void LayoutBar()
        {
            int w = bar.ClientSize.Width;
            btnUninstall.Location = new Point(w - 26 - btnUninstall.Width, 10);
            btnScanOnly.Location = new Point(w - 26 - btnUninstall.Width - 8 - btnScanOnly.Width, 10);
        }

        private void AdjustColumns()
        {
            if (lv.Columns.Count < 5) return;
            int fixedWidth = 110 + 200 + 96 + 76;
            int nameWidth = lv.ClientSize.Width - fixedWidth - 20;
            if (nameWidth < 240) nameWidth = 240;
            lv.Columns[0].Width = nameWidth;
            lv.Columns[1].Width = 110;
            lv.Columns[2].Width = 200;
            lv.Columns[3].Width = 96;
            lv.Columns[4].Width = 76;
        }

        private void OpenDataFolder()
        {
            try
            {
                Directory.CreateDirectory(Cleaner.DataDir);
                Process.Start("explorer.exe", "\"" + Cleaner.DataDir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开文件夹失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshList()
        {
            this.Cursor = Cursors.WaitCursor;
            try
            {
                allApps = InstalledApps.GetAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取已安装软件列表失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                allApps = new List<ProgramInfo>();
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
            FillList();
        }

        private void FillList()
        {
            string q = txtSearch.Text.Trim();
            List<ProgramInfo> filtered = new List<ProgramInfo>();
            for (int i = 0; i < allApps.Count; i++)
            {
                ProgramInfo p = allApps[i];
                if (q.Length == 0 ||
                    p.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    p.Publisher.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    filtered.Add(p);
                }
            }

            lv.BeginUpdate();
            lv.Items.Clear();
            for (int i = 0; i < filtered.Count; i++)
            {
                ProgramInfo p = filtered[i];
                ListViewItem li = new ListViewItem(new string[] {
                    p.Name,
                    p.Version.Length > 0 ? p.Version : "-",
                    p.Publisher.Length > 0 ? p.Publisher : "-",
                    FormatKB(p.SizeKB),
                    p.IsUser ? "用户" : "系统"
                });
                li.Tag = p;
                lv.Items.Add(li);
            }
            lv.EndUpdate();

            AdjustColumns();
            lblCount.Text = string.Format("共 {0} 个软件 · 显示 {1} 个", allApps.Count, filtered.Count);
            hoverIndex = -1;
        }

        private static string FormatKB(long kb)
        {
            if (kb <= 0) return "-";
            if (kb < 1024) return kb.ToString() + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.0") + " MB";
            return (mb / 1024.0).ToString("0.00") + " GB";
        }

        // ---------------- 自绘 ----------------
        private void LvDrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = false;
            using (SolidBrush b = new SolidBrush(T.PanelAlt))
                e.Graphics.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(T.Line))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            Rectangle r = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, T.FBoldSmall, r, T.Sub,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void LvDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            Color bg = T.Panel;
            if (e.Item.Selected) bg = T.Sel;
            else if (e.ItemIndex == hoverIndex) bg = T.Hover;
            Rectangle r = new Rectangle(0, e.Bounds.Y, lv.ClientSize.Width, e.Bounds.Height);
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, r);
            using (Pen p = new Pen(T.Line))
                e.Graphics.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
        }

        private void LvDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = false;
            Color bg = T.Panel;
            if (e.Item.Selected) bg = T.Sel;
            else if (e.ItemIndex == hoverIndex) bg = T.Hover;
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, e.Bounds);

            Color c;
            if (e.ColumnIndex == 0) c = e.Item.Selected ? Color.White : T.Text;
            else if (e.ColumnIndex == 3) c = e.Item.Selected ? Color.FromArgb(210, 224, 250) : T.Text;
            else c = e.Item.Selected ? Color.FromArgb(196, 210, 236) : T.Sub;

            Rectangle r = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.ColumnIndex == 0 ? T.F95 : T.F9, r, c,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // ---------------- 右键进来的流程 ----------------
        private void RunTargetFlow()
        {
            try
            {
                string target = startTarget;
                ProgramInfo p = TargetResolver.Resolve(target, allApps);

                if (p == null)
                {
                    string hint = TargetResolver.NameHint(target);
                    if (hint.Length == 0) hint = "这个程序";
                    DialogResult dr = MessageBox.Show(
                        "没有找到「" + hint + "」的卸载信息。\n\n" +
                        "它可能是免安装的绿色软件，或者已经被卸载过了。\n\n" +
                        "要直接扫描它留下的残留（文件夹、缓存、快捷方式、注册表）吗？",
                        "没找到卸载信息", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dr == DialogResult.Yes)
                    {
                        ProgramInfo fake = new ProgramInfo();
                        fake.Name = hint;
                        fake.InstallLocation = TargetResolver.DirHint(target);
                        ScanAndClean(fake);
                    }
                    return;
                }

                UninstallFlow(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show("处理失败：\n" + ex.Message, "卸载大师", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Close();
            }
        }

        // ---------------- 列表里的动作 ----------------
        private ProgramInfo Selected()
        {
            if (lv.SelectedItems.Count == 0) return null;
            return lv.SelectedItems[0].Tag as ProgramInfo;
        }

        private void UninstallSelected()
        {
            ProgramInfo p = Selected();
            if (p == null)
            {
                MessageBox.Show("请先在列表里点选要卸载的软件。", "还没有选择软件",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            UninstallFlow(p);
        }

        private void ScanOnlySelected()
        {
            ProgramInfo p = Selected();
            if (p == null)
            {
                MessageBox.Show("请先在列表里点选一个软件，再点「只扫描残留」。", "还没有选择软件",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ScanAndClean(p);
        }

        private void ScanOthers()
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "输入软件名称（比如 Chrome、微信、Photoshop），我会在常见位置查找它的残留：",
                "扫描其它残留", "");
            if (name == null) return;
            name = name.Trim();
            if (name.Length < 2)
            {
                MessageBox.Show("请输入至少 2 个字的软件名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ProgramInfo fake = new ProgramInfo();
            fake.Name = name;
            ScanAndClean(fake);
        }

        // ---------------- 卸载主流程 ----------------
        private void UninstallFlow(ProgramInfo p)
        {
            if (p.UninstallString.Length == 0)
            {
                DialogResult dr0 = MessageBox.Show(
                    "「" + p.Name + "」没有提供卸载命令，可能已经被卸载了。\n\n要扫描并清理它留下的残留吗？",
                    "无法自动卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr0 == DialogResult.Yes) ScanAndClean(p);
                return;
            }

            DialogResult dr = MessageBox.Show(
                "是否卸载「" + p.Name + "」？\n\n" +
                "点“是”会打开它自带的卸载程序。卸载完成后，我再扫描它的残留，" +
                "由你决定要不要清掉。",
                "卸载确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            string file = "";
            string args = "";
            InstalledApps.ParseCommand(p.UninstallString, out file, out args);

            Process proc = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file);
                psi.Arguments = args;
                psi.UseShellExecute = true;
                string dir = "";
                try { dir = Path.GetDirectoryName(file); }
                catch { }
                if (dir.Length > 0 && Directory.Exists(dir)) psi.WorkingDirectory = dir;
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动卸载程序失败：\n" + ex.Message +
                    "\n\n你可以手动卸载后，用「只扫描残留」清理它的残留。",
                    "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (proc == null)
            {
                MessageBox.Show("没有启动任何卸载程序。\n\n可以手动卸载后，用「只扫描残留」清理残留。",
                    "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WaitingForm wf = new WaitingForm(p, proc);
            wf.ShowDialog(this);
            if (!wf.Go) return;

            ScanAndClean(p);
        }

        // ---------------- 扫描 + 询问 + 展示 ----------------
        private void ScanAndClean(ProgramInfo p)
        {
            BusyForm bf = null;
            bf = new BusyForm("正在查找「" + p.Name + "」的残留…", delegate
            {
                return ResidueScanner.Scan(p, delegate(string msg) { bf.SetState(msg); });
            });
            bf.ShowDialog(this);

            if (bf.Error != null)
            {
                MessageBox.Show("扫描时出错：\n" + bf.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            List<ResidueItem> items = bf.Result as List<ResidueItem>;
            if (items == null || items.Count == 0)
            {
                MessageBox.Show("没有发现残留，这个软件走得很干净。", "扫描完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (startTarget == null) RefreshList();
                return;
            }

            long total = 0;
            for (int i = 0; i < items.Count; i++) total += items[i].Size;

            DialogResult dr = MessageBox.Show(
                "找到 " + items.Count + " 项残留，共 " + ResidueItem.FormatSize(total) + "。\n\n" +
                "包括：安装目录、缓存、空壳文件夹、开始菜单和桌面的快捷方式、注册表项。\n\n" +
                "要现在检查并清理这些残留吗？\n" +
                "（下一步会列出每一处残留，默认只勾风险低的，你可以自己改。）",
                "删除剩余残留？", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes)
            {
                MessageBox.Show("好的，残留保持原样，没有删除。", "已取消",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (startTarget == null) RefreshList();
                return;
            }

            ResidueForm rf = new ResidueForm(p, items);
            rf.ShowDialog(this);
            if (startTarget == null) RefreshList();
        }
    }
}
