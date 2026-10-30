using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpencodeDpiFix
{
    internal static class Program
    {
        public const string Version = "1.1.0";
        public const string ProjectUrl = "https://github.com/opencode-dpi-fix/opencode-dpi-fix";

        private const uint AttachParentProcess = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [STAThread]
        private static int Main(string[] args)
        {
            Strings.Initialize();

            if (args.Length > 0)
            {
                // Консольный режим: подключаемся к консоли родителя, чтобы вывод был виден.
                if (!AttachConsole(AttachParentProcess)) AllocConsole();
                try
                {
                    return Cli.Run(args);
                }
                finally
                {
                    FreeConsole();
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!HostsFix.IsAdmin() && !HostsFix.AdminCheckBypassed && !Elevate())
            {
                MessageBox.Show(
                    Strings.NoAdmin + "\r\n\r\n" +
                    (Strings.English
                        ? "Right-click the file and choose “Run as administrator”."
                        : "Щёлкните по файлу правой кнопкой и выберите «Запуск от имени администратора»."),
                    Strings.WindowTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return 1;
            }

            Application.Run(new MainForm());
            return 0;
        }

        private static bool Elevate()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                // пользователь отказался от UAC
                return false;
            }
        }
    }
}
