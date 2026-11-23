using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CleanUninstall
{
    public class ResidueForm : Form
    {
        private ProgramInfo prog;
        private List<ResidueItem> items;
        private ListView lv;
        private Label lblHead, lblSub, lblProgress;
        private Button btnAll, btnNone, btnClean, btnBackup;
        private Panel header, listHost, footer;
        private bool running = false;
        private int regBackupCount = 0;

        public ResidueForm(ProgramInfo p, List<ResidueItem> list)
        {
            this.prog = p;
            this.items = list;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.ClientSize = new Size(960, 640);
            this.MinimumSize = new Size(820, 520);
            this.BackColor = T.Bg;
            this.Font = T.F9;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.Text = "清理残留";

            long total = 0;
            for (int i = 0; i < items.Count; i++) total += items[i].Size;

            // 顶部
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 104;
            header.BackColor = T.Panel;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pn = new Pen(T.Line)) e.Graphics.DrawLine(pn, 0, header.ClientSize.Height - 1, header.ClientSize.Width, header.ClientSize.Height - 1);
            };
            this.Controls.Add(header);

            lblHead = T.Lbl("发现 " + items.Count + " 项残留", T.F16, T.Text);
            lblHead.Location = new Point(26, 16);
            header.Controls.Add(lblHead);

            string pname = (prog != null && prog.Name.Length > 0) ? prog.Name : "该软件";
            lblSub = T.Lbl("「" + pname + "」留下的文件和注册表 · 合计 " + ResidueItem.FormatSize(total), T.F9, T.Sub);
            lblSub.Location = new Point(29, 56);
            lblSub.AutoSize = false;
            lblSub.Width = 640;
            lblSub.Height = 22;
            lblSub.AutoEllipsis = true;
            header.Controls.Add(lblSub);

            Label hint = T.Lbl("绿色勾选表示建议清理；风险「高」的默认不选，请自己确认后再勾。", T.F85, T.Sub);
            hint.Location = new Point(29, 78);
            header.Controls.Add(hint);

            btnBackup = T.Ghost("打开备份文件夹");
            btnBackup.Size = new Size(140, 36);
            btnBackup.Click += delegate { OpenBackup(); };
            header.Controls.Add(btnBackup);
            header.Resize += delegate
            {
                btnBackup.Location = new Point(header.ClientSize.Width - 26 - btnBackup.Width, 32);
            };

            // 列表
            listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = T.Bg;
            listHost.Padding = new Padding(26, 4, 26, 8);
            this.Controls.Add(listHost);

            lv = new ListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.CheckBoxes = true;
            lv.FullRowSelect = true;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = T.Panel;
            lv.ForeColor = T.Text;
            lv.Font = T.F9;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.Columns.Add("类型", 130);
            lv.Columns.Add("位置", 500);
            lv.Columns.Add("大小", 96);
            lv.Columns.Add("风险", 60);
            lv.Resize += delegate { AdjustColumns(); };
            lv.ItemChecked += delegate { UpdateCount(); };
            lv.DoubleClick += delegate { OpenSelected(); };
            ImageList il = new ImageList();
            il.ImageSize = new Size(1, 26);
            il.ColorDepth = ColorDepth.Depth32Bit;
            il.Images.Add(new Bitmap(1, 26));
            lv.SmallImageList = il;

            for (int i = 0; i < items.Count; i++)
            {
                ResidueItem it = items[i];
                ListViewItem li = new ListViewItem(new string[] {
                    it.TypeName,
                    it.Path,
                    it.Kind == ResidueItem.KindRegistry ? "-" : ResidueItem.FormatSize(it.Size),
                    it.RiskText
                });
                li.Checked = it.Checked;
                li.Tag = it;
                li.ForeColor = T.Text;
                if (it.Risk == 2) li.ForeColor = Color.FromArgb(232, 150, 120);
                lv.Items.Add(li);
            }
            listHost.Controls.Add(lv);

            // 底部
            footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 96;
            footer.BackColor = T.Panel;
            footer.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pn = new Pen(T.Line)) e.Graphics.DrawLine(pn, 0, 0, footer.ClientSize.Width, 0);
            };
            this.Controls.Add(footer);

            Label footHint = T.Lbl("文件默认移到回收站（可以还原）；注册表删除前会自动导出备份", T.F85, T.Sub);
            footHint.Location = new Point(26, 14);
            footer.Controls.Add(footHint);

            btnAll = T.Ghost("全选");
            btnAll.Size = new Size(80, 36);
            btnAll.Location = new Point(26, 48);
            btnAll.Click += delegate { SetAll(true); };
            footer.Controls.Add(btnAll);

            btnNone = T.Ghost("全不选");
            btnNone.Size = new Size(90, 36);
            btnNone.Location = new Point(114, 48);
            btnNone.Click += delegate { SetAll(false); };
            footer.Controls.Add(btnNone);

            lblProgress = T.Lbl("", T.F85, T.Sub);
            lblProgress.Location = new Point(220, 58);
            footer.Controls.Add(lblProgress);

            btnClean = T.DangerBtn("彻底清理 0 项");
            btnClean.Size = new Size(186, 44);
            btnClean.Click += delegate { StartClean(); };
            footer.Controls.Add(btnClean);

            footer.Resize += delegate
            {
                btnClean.Location = new Point(footer.ClientSize.Width - 26 - btnClean.Width, 44);
            };

            UpdateCount();
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
            ThemeApplier.FitToScreen(this, false);
        }

        private void AdjustColumns()
        {
            if (lv.Columns.Count < 4) return;
            int fixedWidth = 130 + 96 + 60;
            int pathWidth = lv.ClientSize.Width - fixedWidth - 20;
            if (pathWidth < 300) pathWidth = 300;
            lv.Columns[0].Width = 130;
            lv.Columns[1].Width = pathWidth;
            lv.Columns[2].Width = 96;
            lv.Columns[3].Width = 60;
        }

        private void UpdateCount()
        {
            int n = 0;
            long size = 0;
            for (int i = 0; i < lv.Items.Count; i++)
            {
                ListViewItem li = lv.Items[i];
                ResidueItem it = li.Tag as ResidueItem;
                if (li.Checked && it != null) { n++; size += it.Size; }
            }
            btnClean.Text = "彻底清理 " + n + " 项";
            btnClean.Enabled = n > 0 && !running;
            if (running) btnClean.Enabled = false;
            if (lblProgress.Text.Length == 0 || !running)
                lblProgress.Text = n > 0 ? ("已选中 " + ResidueItem.FormatSize(size)) : "";
        }

        private void SetAll(bool value)
        {
            if (running) return;
            lv.BeginUpdate();
            for (int i = 0; i < lv.Items.Count; i++) lv.Items[i].Checked = value;
            lv.EndUpdate();
            UpdateCount();
        }

        private void OpenSelected()
        {
            if (lv.SelectedItems.Count == 0) return;
            ResidueItem it = lv.SelectedItems[0].Tag as ResidueItem;
            if (it == null) return;

            if (it.Kind == ResidueItem.KindRegistry)
            {
                MessageBox.Show("这是注册表项，不能用资源管理器打开：\n\n" + it.Path, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                if (Directory.Exists(it.Path))
                    Process.Start("explorer.exe", "\"" + it.Path + "\"");
                else if (File.Exists(it.Path))
                    Process.Start("explorer.exe", "/select,\"" + it.Path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenBackup()
        {
            try
            {
                Directory.CreateDirectory(Cleaner.BackupDir);
                Process.Start("explorer.exe", "\"" + Cleaner.BackupDir + "\"");
            }
            catch { }
        }

        private void StartClean()
        {
            if (running) return;

            List<ResidueItem> sel = new List<ResidueItem>();
            for (int i = 0; i < lv.Items.Count; i++)
            {
                if (lv.Items[i].Checked)
                {
                    ResidueItem it = lv.Items[i].Tag as ResidueItem;
                    if (it != null) sel.Add(it);
                }
            }
            if (sel.Count == 0)
            {
                MessageBox.Show("还没有勾选任何项目。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long total = 0;
            for (int i = 0; i < sel.Count; i++) total += sel[i].Size;

            ChooseDeleteForm cdf = new ChooseDeleteForm(prog, sel.Count, total);
            cdf.ShowDialog(this);
            if (cdf.Choice < 0) return;
            bool perm = (cdf.Choice == 1);

            running = true;
            SetBusy(true);
            lblProgress.Text = "正在清理… 0/" + sel.Count;

            Thread th = new Thread(new ThreadStart(delegate { DoClean(sel, perm); }));
            th.IsBackground = true;
            th.Start();
        }

        private void SetBusy(bool busy)
        {
            running = busy;
            lv.Enabled = !busy;
            btnAll.Enabled = !busy;
            btnNone.Enabled = !busy;
            btnBackup.Enabled = !busy;
            btnClean.Enabled = !busy && lv.CheckedItems.Count > 0;
            this.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void DoClean(List<ResidueItem> sel, bool permanent)
        {
            StringBuilder log = new StringBuilder();
            Cleaner.AppendLog(log, "开始清理 " + (prog != null ? prog.Name : "") + " 的残留，共 " + sel.Count + " 项");

            int ok = 0;
            int fail = 0;
            int regOk = 0;
            StringBuilder errs = new StringBuilder();

            for (int i = 0; i < sel.Count; i++)
            {
                ResidueItem it = sel[i];
                bool success = false;
                string err = "";

                if (it.Kind == ResidueItem.KindRegistry)
                {
                    string bk = "";
                    string be = "";
                    if (!Cleaner.BackupRegistry(it.Path, out bk, out be))
                    {
                        success = false;
                        err = "备份失败，已跳过（" + be + "）";
                    }
                    else
                    {
                        success = Cleaner.DeleteRegistry(it.Path, out err);
                        if (success) regOk++;
                    }
                    Cleaner.AppendLog(log, (success ? "删除注册表项 OK: " : "删除注册表项失败: ") + it.Path + (err.Length > 0 ? " | " + err : ""));
                }
                else
                {
                    success = Cleaner.DeletePath(it.Path, permanent, out err);
                    Cleaner.AppendLog(log, (success ? "删除 OK: " : "删除失败: ") + it.Path + (err.Length > 0 ? " | " + err : ""));
                }

                if (success) ok++;
                else
                {
                    fail++;
                    errs.AppendLine("· " + it.Path + "  →  " + err);
                    errs.AppendLine();
                }

                int done = i + 1;
                try
                {
                    this.BeginInvoke(new Action(delegate
                    {
                        if (!lblProgress.IsDisposed) lblProgress.Text = "正在清理… " + done + "/" + sel.Count;
                    }));
                }
                catch { }
            }

            Cleaner.AppendLog(log, "清理结束：成功 " + ok + " 项，失败 " + fail + " 项");
            string logPath = Cleaner.NewLogPath();
            try { File.WriteAllText(logPath, log.ToString(), Encoding.UTF8); }
            catch { }

            regBackupCount = regOk;
            string errText = errs.ToString();
            try
            {
                this.BeginInvoke(new Action(delegate { FinishClean(ok, fail, errText, logPath, permanent); }));
            }
            catch { }
        }

        private void FinishClean(int ok, int fail, string errText, string logPath, bool permanent)
        {
            running = false;
            SetBusy(false);
            if (lblProgress.Text.Length == 0) lblProgress.Text = "";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("清理完成");
            sb.AppendLine();
            sb.AppendLine("成功清理 " + ok + " 项，失败 " + fail + " 项。");
            sb.AppendLine();
            if (ok > 0)
            {
                if (permanent) sb.AppendLine("这次是永久删除，文件没有进回收站。");
                else sb.AppendLine("删除的文件都在回收站里，随时可以还原。");
            }
            if (regBackupCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("注册表改动已备份到：");
                sb.AppendLine(Cleaner.BackupDir);
            }
            if (fail > 0 && errText.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("没清理掉的项目（通常是文件正在被占用，重启电脑后再试）：");
                sb.AppendLine(errText);
            }
            sb.AppendLine();
            sb.AppendLine("详细记录：" + logPath);

            MessageBox.Show(sb.ToString(), fail > 0 ? "清理完成（有失败项）" : "清理完成",
                MessageBoxButtons.OK, fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}


