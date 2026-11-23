using System;
using System.Threading;
using System.Windows.Forms;

namespace CleanUninstall
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                MessageBox.Show("程序出错：\n" + e.Exception.Message, "卸载大师",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            string target = null;
            if (args != null && args.Length >= 2)
            {
                if (args[0].Equals("/target", StringComparison.OrdinalIgnoreCase))
                {
                    target = args[1];
                    if (target != null && target.Length == 0) target = null;
                }
            }

            try
            {
                Application.Run(new MainForm(target));
            }
            catch (Exception ex)
            {
                MessageBox.Show("程序启动失败：\n" + ex.Message, "卸载大师",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
