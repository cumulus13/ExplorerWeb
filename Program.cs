using ExplorerWeb.Tray;
using ExplorerWeb.Services;

namespace ExplorerWeb;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "ExplorerWeb.Tray.Singleton", out var createdNew);
        if (!createdNew)
        {
            TryOpenExistingInstance();
            return;
        }

        ApplicationConfiguration.Initialize();
        using var context = new ExplorerTrayApplicationContext(args);
        Application.Run(context);
        GC.KeepAlive(mutex);
    }

    private static void TryOpenExistingInstance()
    {
        try
        {
            var runtimeConfig = new RuntimeConfigService();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(runtimeConfig.Config.Url)
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
