namespace ExplorerWeb.Models;

public sealed class ExplorerRuntimeConfig
{
    public string Url { get; set; } = "http://127.0.0.1:5054";

    public bool StartServerOnLaunch { get; set; } = true;

    public bool OpenBrowserOnLaunch { get; set; }

    public bool NotificationsEnabled { get; set; } = true;

    public string IconsFolder { get; set; } = "icons";

    public string TrayIconFile { get; set; } = "tray.ico";

    public string NotificationIconFile { get; set; } = "notify.ico";
}
