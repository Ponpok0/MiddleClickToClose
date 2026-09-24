using System.Windows.Forms;

namespace MiddleClickToClose;

internal static class Program
{
    private const string MutexName = "Global\\MiddleClickToClose_SingleInstance";

    [STAThread]
    static void Main()
    {
        // 多重起動防止
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("[MiddleClickToClose] Already running.");
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using var app = new TrayApplicationContext();
        Application.Run(app);
    }
}
