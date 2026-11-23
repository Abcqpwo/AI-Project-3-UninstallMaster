using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CleanUninstall
{
    public static class T
    {
        public static readonly Color Bg = Color.FromArgb(13, 15, 19);
        public static readonly Color Panel = Color.FromArgb(21, 24, 31);
        public static readonly Color PanelAlt = Color.FromArgb(25, 29, 37);
        public static readonly Color Card = Color.FromArgb(30, 35, 44);
        public static readonly Color Line = Color.FromArgb(38, 43, 55);
        public static readonly Color Text = Color.FromArgb(233, 237, 245);
        public static readonly Color Sub = Color.FromArgb(140, 150, 168);
        public static readonly Color Accent = Color.FromArgb(59, 130, 246);
        public static readonly Color AccentHover = Color.FromArgb(96, 156, 255);
        public static readonly Color Danger = Color.FromArgb(214, 66, 66);
        public static readonly Color DangerHover = Color.FromArgb(238, 88, 88);
        public static readonly Color Sel = Color.FromArgb(34, 49, 76);
        public static readonly Color Hover = Color.FromArgb(28, 33, 42);
        public static readonly Color Ok = Color.FromArgb(40, 180, 110);
        public static readonly Color Warn = Color.FromArgb(224, 168, 60);

        public static Font F8;
        public static Font F85;
        public static Font F9;
        public static Font F95;
        public static Font F10;
        public static Font FBold;
        public static Font FBoldSmall;
        public static Font FTiny;
        public static Font FTitle;
        public static Font F16;

        static T()
        {
            string fam = "Microsoft YaHei UI";
            F8 = new Font(fam, 8f, FontStyle.Regular, GraphicsUnit.Point);
            F85 = new Font(fam, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            F9 = new Font(fam, 9f, FontStyle.Regular, GraphicsUnit.Point);
            F95 = new Font(fam, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            F10 = new Font(fam, 10f, FontStyle.Regular, GraphicsUnit.Point);
            FBold = new Font(fam, 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            FBoldSmall = new Font(fam, 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            FTiny = new Font(fam, 8f, FontStyle.Regular, GraphicsUnit.Point);
            FTitle = new Font(fam, 19f, FontStyle.Bold, GraphicsUnit.Point);
            F16 = new Font(fam, 15f, FontStyle.Bold, GraphicsUnit.Point);
        }

        public static Button Btn(string text, Color bg, Color hover, Font font)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg;
            b.ForeColor = Color.White;
            b.Font = font;
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            b.UseVisualStyleBackColor = false;
            Color normal = bg;
            Color hi = hover;
            b.MouseEnter += delegate { b.BackColor = hi; };
            b.MouseLeave += delegate { b.BackColor = normal; };
            return b;
        }

        public static Button Primary(string text)
        {
            return Btn(text, Accent, AccentHover, FBold);
        }

        public static Button DangerBtn(string text)
        {
            return Btn(text, Danger, DangerHover, FBold);
        }

        public static Button Ghost(string text)
        {
            Button b = Btn(text, Color.FromArgb(31, 36, 46), Color.FromArgb(40, 46, 58), F9);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Line;
            b.ForeColor = Text;
            return b;
        }

        public static Label Lbl(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            return l;
        }

        public static void DrawBorder(Graphics g, Rectangle r, Color c)
        {
            using (Pen p = new Pen(c))
            {
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
        }
    }

    // 带提示文字的输入框
    public class HintBox : TextBox    {
        public string Hint = "";
        private const int WM_PAINT = 0x000F;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && this.Text.Length == 0 && !this.Focused && Hint.Length > 0)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    Rectangle r = new Rectangle(3, 3, this.ClientSize.Width - 6, this.ClientSize.Height - 6);
                    TextRenderer.DrawText(g, Hint, this.Font, r, T.Sub, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            }
        }
    }

    // 让窗口标题栏、滚动条也变深色
    public static class ThemeApplier
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SetWindowTheme(IntPtr hwnd, string app, string id);

        public static void DarkTitleBar(Form f)
        {
            if (f == null || !f.IsHandleCreated) return;
            try
            {
                int v = 1;
                int r = DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int));
                if (r != 0) DwmSetWindowAttribute(f.Handle, 19, ref v, sizeof(int));
            }
            catch { }
        }

        public static void DarkScroll(Control c)
        {
            if (c == null || !c.IsHandleCreated) return;
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); }
            catch { }
        }

        // 把窗口限制在屏幕工作区内并居中
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT r);

        public static void FitToScreen(Form f, bool center)
        {
            if (f == null || !f.IsHandleCreated) return;

            Rectangle wa = Screen.FromControl(f).WorkingArea;
            RECT r;
            int w = f.Width;
            int h = f.Height;

            if (GetWindowRect(f.Handle, out r))
            {
                if (r.Right > wa.Right) w -= (r.Right - wa.Right);
                if (r.Left < wa.Left) w -= (wa.Left - r.Left);
                if (r.Bottom > wa.Bottom) h -= (r.Bottom - wa.Bottom);
                if (r.Top < wa.Top) h -= (wa.Top - r.Top);
            }

            if (w < f.MinimumSize.Width) w = f.MinimumSize.Width;
            if (h < f.MinimumSize.Height) h = f.MinimumSize.Height;

            if (w != f.Width) f.Width = w;
            if (h != f.Height) f.Height = h;

            if (center)
                f.Location = new Point(wa.X + (wa.Width - f.Width) / 2, wa.Y + (wa.Height - f.Height) / 2);
        }
    }
}
