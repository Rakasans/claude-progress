namespace ClaudeProgress;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Tylko jedna instancja w zasobniku.
        using var mutex = new Mutex(true, "ClaudeProgress.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp());
    }
}
