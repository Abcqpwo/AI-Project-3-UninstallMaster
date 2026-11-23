using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CleanUninstall
{
    // 一条滚动的高亮条，用来表示"正在处理"
    public class AnimatedBar : Control
    {
        private Timer t;
        private int pos;
        public Color BarColor = T.Accent;

        public AnimatedBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            t = new Timer();
            t.Interval = 26;
            t.Tick += delegate
            {
                pos += 5;
                if (pos > this.ClientSize.Width) pos = -this.ClientSize.Width / 2;
                this.Invalidate();
            };
            t.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(33, 38, 48)))
                e.Graphics.FillRectangle(b, 0, 0, w, h);

            int blockW = Math.Max(50, w / 3);
            int x = pos - blockW;
            using (SolidBrush b = new SolidBrush(BarColor))
                e.Graphics.FillRectangle(b, x, 0, blockW, h);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && t != null) { t.Stop(); t.Dispose(); t = null; }
            base.Dispose(disposing);
        }
    }

    // 模态的"正在处理"窗口，工作在线程里跑，完成后自动关闭
    public class BusyForm : Form
    {
        public object Result;
        public Exception Error;

        private Func<object> work;
        private Label lblState;
        private BackgroundWorker bw;

        public BusyForm(string title, Func<object> work)
        {
            this.work = work;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.ClientSize = new Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.BackColor = T.Panel;
            this.Font = T.F9;
            this.Paint += delegate(object s, PaintEventArgs e)
            {
                T.DrawBorder(e.Graphics, new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height), T.Line);
            };

            Label t = T.Lbl(title, T.F10, T.Text);
            t.Location = new Point(24, 22);
            this.Controls.Add(t);

            lblState = T.Lbl("正在准备…", T.F85, T.Sub);
            lblState.Location = new Point(24, 56);
            lblState.AutoSize = false;
            lblState.Width = 350;
            lblState.Height = 20;
            lblState.AutoEllipsis = true;
            this.Controls.Add(lblState);

            AnimatedBar bar = new AnimatedBar();
            bar.Location = new Point(24, 92);
            bar.Size = new Size(352, 6);
            this.Controls.Add(bar);

            Label tip = T.Lbl("请稍等，正在读取文件信息…", T.FTiny, T.Sub);
            tip.Location = new Point(24, 110);
            this.Controls.Add(tip);

            bw = new BackgroundWorker();
            bw.DoWork += delegate(object s, DoWorkEventArgs e) { e.Result = this.work(); };
            bw.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null) this.Error = e.Error;
                else this.Result = e.Result;
                try { this.DialogResult = DialogResult.OK; } catch { }
                this.Close();
            };
        }

        public void SetState(string s)
        {
            if (this.IsDisposed) return;
            if (!this.IsHandleCreated) { lblState.Text = s; return; }
            try
            {
                this.BeginInvoke(new Action(delegate { if (!lblState.IsDisposed) lblState.Text = s; }));
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            bw.RunWorkerAsync();
        }
    }

    // 卸载进行中的提示窗口
    public class WaitingForm : Form
    {
        public bool Go = false;

        private ProgramInfo prog;
        private Process proc;
        private Label lblState;
        private Button btnGo;
        private Timer timer;

        public WaitingForm(ProgramInfo p, Process process)
        {
            this.prog = p;
            this.proc = process;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.ClientSize = new Size(520, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = T.Panel;
            this.Font = T.F9;
            this.Text = "正在卸载";

            Label t = T.Lbl("卸载进行中", T.F10, T.Text);
            t.Location = new Point(28, 24);
            this.Controls.Add(t);

            Label name = T.Lbl(p.Name, T.FBold, T.Accent);
            name.Location = new Point(28, 52);
            name.AutoSize = false;
            name.Width = 460;
            name.Height = 22;
            name.AutoEllipsis = true;
            this.Controls.Add(name);

            Label desc = new Label();
            desc.Text = "已经启动这个软件自带的卸载程序。\r\n请在弹出的卸载窗口里完成卸载（点下一步 / 卸载 / 完成都可以）。\r\n卸载程序关掉以后，点下面的按钮继续清残留。";
            desc.Font = T.F85;
            desc.ForeColor = T.Sub;
            desc.Location = new Point(28, 84);
            desc.Size = new Size(460, 66);
            desc.BackColor = Color.Transparent;
            this.Controls.Add(desc);

            AnimatedBar bar = new AnimatedBar();
            bar.Location = new Point(28, 160);
            bar.Size = new Size(464, 6);
            this.Controls.Add(bar);

            lblState = T.Lbl("正在等待卸载程序结束…", T.F85, T.Warn);
            lblState.Location = new Point(28, 178);
            this.Controls.Add(lblState);

            btnGo = T.Primary("继续清理残留 →");
            btnGo.Size = new Size(196, 44);
            btnGo.Location = new Point(296, 220);
            btnGo.Click += delegate { this.Go = true; this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.Add(btnGo);

            Button btnCancel = T.Ghost("先不清理");
            btnCancel.Size = new Size(104, 44);
            btnCancel.Location = new Point(180, 220);
            btnCancel.Click += delegate { this.Go = false; this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);

            timer = new Timer();
            timer.Interval = 700;
            timer.Tick += delegate
            {
                bool done = false;
                if (proc != null)
                {
                    try { done = proc.HasExited; }
                    catch { done = true; }
                }
                if (done)
                {
                    lblState.Text = "卸载程序已经退出 · 可以继续清理";
                    lblState.ForeColor = T.Ok;
                    btnGo.Text = "卸载完了，清理残留 →";
                    timer.Stop();
                }
            };
            timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            base.OnFormClosed(e);
        }
    }

    // 删除方式选择：回收站 / 永久删除 / 取消
    public class ChooseDeleteForm : Form
    {
        public int Choice = -1;   // 0 = 回收站, 1 = 永久删除

        public ChooseDeleteForm(ProgramInfo p, int count, long totalSize)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.ClientSize = new Size(560, 330);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = T.Panel;
            this.Font = T.F9;
            this.Text = "删除残留";

            string name = (p != null && p.Name.Length > 0) ? p.Name : "这个软件";

            Label t = T.Lbl("要删除「" + name + "」的残留吗？", T.F10, T.Text);
            t.Font = T.FBold;
            t.Location = new Point(28, 26);
            this.Controls.Add(t);

            Label sub = new Label();
            sub.Text = "共 " + count + " 项，约 " + ResidueItem.FormatSize(totalSize) + "。\n请选择一种删除方式：";
            sub.Font = T.F9;
            sub.ForeColor = T.Sub;
            sub.Location = new Point(28, 58);
            sub.Size = new Size(504, 46);
            sub.BackColor = Color.Transparent;
            this.Controls.Add(sub);

            Button b1 = T.Primary("移到回收站（以后可以还原）");
            b1.Size = new Size(504, 52);
            b1.Location = new Point(28, 108);
            b1.Click += delegate
            {
                this.Choice = 0;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(b1);

            Button b2 = T.DangerBtn("永久删除（删掉就找不回来）");
            b2.Size = new Size(504, 52);
            b2.Location = new Point(28, 172);
            b2.Click += delegate
            {
                this.Choice = 1;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(b2);

            Button b3 = T.Ghost("取消");
            b3.Size = new Size(120, 40);
            b3.Location = new Point(560 - 28 - 120, 246);
            b3.Click += delegate
            {
                this.Choice = -1;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(b3);

            Label tip = new Label();
            tip.Text = "回收站方案更安全；永久删除会跳过回收站，大文件清理更快。\n两种方式都会先给注册表做备份。";
            tip.Font = T.FTiny;
            tip.ForeColor = T.Sub;
            tip.Location = new Point(28, 246);
            tip.Size = new Size(360, 46);
            tip.BackColor = Color.Transparent;
            this.Controls.Add(tip);

            this.AcceptButton = b1;
            this.CancelButton = b3;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeApplier.DarkTitleBar(this);
        }
    }
}
