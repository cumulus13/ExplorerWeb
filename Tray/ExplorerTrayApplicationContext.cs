using System.Drawing;
using System.Windows.Forms;
using ExplorerWeb.Services;
using Microsoft.Extensions.Logging;

namespace ExplorerWeb.Tray;

public sealed class ExplorerTrayApplicationContext : ApplicationContext
{
    private readonly string[] _args;
    private readonly RuntimeConfigService _runtimeConfig;
    private readonly TrayIconService _trayIconService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ExplorerServerHost _serverHost;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;
    private readonly ToolStripMenuItem _openMenuItem;
    private bool _isExiting;

    public ExplorerTrayApplicationContext(string[] args)
    {
        _args = args;
        _runtimeConfig = new RuntimeConfigService();
        _trayIconService = new TrayIconService();
        _loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
        _serverHost = new ExplorerServerHost(_args, _runtimeConfig, _loggerFactory);

        _startMenuItem = new ToolStripMenuItem("Start", null, async (_, _) => await StartServerFromMenuAsync());
        _stopMenuItem = new ToolStripMenuItem("Stop", null, async (_, _) => await StopServerFromMenuAsync());
        _openMenuItem = new ToolStripMenuItem("Open Explorer", null, (_, _) => OpenBrowser());

        var restartMenuItem = new ToolStripMenuItem("Restart", null, async (_, _) => await RestartServerAsync());
        var configMenuItem = new ToolStripMenuItem("Open Config", null, (_, _) => OpenConfig());
        var folderMenuItem = new ToolStripMenuItem("Open Install Folder", null, (_, _) => OpenInstallFolder());
        var quitMenuItem = new ToolStripMenuItem("Quit", null, async (_, _) => await ExitApplicationAsync());

        _notifyIcon = new NotifyIcon
        {
            Text = "ExplorerWeb",
            Icon = _trayIconService.GetTrayIcon(_runtimeConfig),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.AddRange(
        [
            _openMenuItem,
            new ToolStripSeparator(),
            _startMenuItem,
            _stopMenuItem,
            restartMenuItem,
            new ToolStripSeparator(),
            configMenuItem,
            folderMenuItem,
            new ToolStripSeparator(),
            quitMenuItem
        ]);

        _notifyIcon.DoubleClick += (_, _) => OpenBrowser();

        UpdateMenuState();

        if (_runtimeConfig.Config.StartServerOnLaunch)
        {
            _ = StartServerAsync(notify: true, openBrowser: _runtimeConfig.Config.OpenBrowserOnLaunch);
        }
        else
        {
            ShowNotification("ExplorerWeb", "Tray started. The web server is currently stopped.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIconService.Dispose();
            _loggerFactory.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task StartServerFromMenuAsync()
    {
        await StartServerAsync(notify: true, openBrowser: false);
    }

    private async Task StopServerFromMenuAsync()
    {
        if (!_serverHost.IsRunning)
        {
            ShowNotification("ExplorerWeb", "The web server is already stopped.");
            return;
        }

        ShowNotification("ExplorerWeb", "Stopping the background web server...");
        await _serverHost.StopAsync();
        UpdateMenuState();
        ShowNotification("ExplorerWeb", "Background web server stopped.");
    }

    private async Task RestartServerAsync()
    {
        ShowNotification("ExplorerWeb", "Restarting the background web server...");

        if (_serverHost.IsRunning)
        {
            await _serverHost.StopAsync();
        }

        await StartServerAsync(notify: true, openBrowser: false);
    }

    private async Task StartServerAsync(bool notify, bool openBrowser)
    {
        if (_serverHost.IsRunning)
        {
            UpdateMenuState();
            if (notify)
            {
                ShowNotification("ExplorerWeb", $"The background web server is already running at {_serverHost.Url}.");
            }
            return;
        }

        try
        {
            if (notify)
            {
                ShowNotification("ExplorerWeb", "Starting the background web server...");
            }

            await _serverHost.StartAsync();
            UpdateMenuState();

            if (notify)
            {
                ShowNotification("ExplorerWeb", $"Background web server started at {_serverHost.Url}.");
            }

            if (openBrowser)
            {
                OpenBrowser();
            }
        }
        catch (Exception ex)
        {
            UpdateMenuState();
            ShowNotification("ExplorerWeb", $"Unable to start the background server: {ex.Message}");
        }
    }

    private void OpenBrowser()
    {
        if (!_serverHost.IsRunning)
        {
            ShowNotification("ExplorerWeb", "Start the server first before opening the web UI.");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_serverHost.Url) { UseShellExecute = true });
            ShowNotification("ExplorerWeb", "ExplorerWeb opened in your default browser.");
        }
        catch (Exception ex)
        {
            ShowNotification("ExplorerWeb", $"Unable to open the browser: {ex.Message}");
        }
    }

    private void OpenConfig()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_runtimeConfig.ConfigPath) { UseShellExecute = true });
            ShowNotification("ExplorerWeb", "Opened the runtime config file.");
        }
        catch (Exception ex)
        {
            ShowNotification("ExplorerWeb", $"Unable to open the config file: {ex.Message}");
        }
    }

    private void OpenInstallFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_runtimeConfig.ContentRootPath) { UseShellExecute = true });
            ShowNotification("ExplorerWeb", "Opened the install folder.");
        }
        catch (Exception ex)
        {
            ShowNotification("ExplorerWeb", $"Unable to open the install folder: {ex.Message}");
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        ShowNotification("ExplorerWeb", "Closing ExplorerWeb...");

        if (_serverHost.IsRunning)
        {
            await _serverHost.StopAsync();
        }

        _notifyIcon.Visible = false;
        ExitThread();
    }

    private void UpdateMenuState()
    {
        var running = _serverHost.IsRunning;
        _startMenuItem.Enabled = !running;
        _stopMenuItem.Enabled = running;
        _openMenuItem.Enabled = running;
        _notifyIcon.Text = running ? $"ExplorerWeb ({_serverHost.Url})" : "ExplorerWeb (stopped)";
    }

    private void ShowNotification(string title, string message)
    {
        if (!_runtimeConfig.Config.NotificationsEnabled)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(3000);
    }
}
