using System.Drawing;
using System.Runtime.InteropServices;

namespace ExplorerWeb.Services;

public sealed class TrayIconService : IDisposable
{
    private Icon? _generatedIcon;

    public Icon GetTrayIcon(RuntimeConfigService runtimeConfig)
    {
        var configuredPath = runtimeConfig.ResolveIconPath(runtimeConfig.Config.TrayIconFile);
        var configuredIcon = LoadIcon(configuredPath);
        if (configuredIcon is not null)
        {
            return configuredIcon;
        }

        var appIcon = LoadIcon(Path.Combine(runtimeConfig.ContentRootPath, "app.ico"));
        if (appIcon is not null)
        {
            return appIcon;
        }

        _generatedIcon ??= CreateEmojiIcon("🗂");
        return _generatedIcon;
    }

    public string? GetNotificationImagePath(RuntimeConfigService runtimeConfig)
    {
        var configuredPath = runtimeConfig.ResolveIconPath(runtimeConfig.Config.NotificationIconFile);
        if (File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var pngFallback = Path.Combine(runtimeConfig.ContentRootPath, "app.png");
        if (File.Exists(pngFallback))
        {
            return pngFallback;
        }

        return null;
    }

    public void Dispose()
    {
        _generatedIcon?.Dispose();
    }

    private static Icon? LoadIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                return new Icon(stream);
            }

            using var bitmap = new Bitmap(path);
            var hIcon = bitmap.GetHicon();
            try
            {
                using var icon = Icon.FromHandle(hIcon);
                return (Icon)icon.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Icon CreateEmojiIcon(string emoji)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI Emoji", 30, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(32, 32, 32));
        graphics.DrawString(emoji, font, brush, new PointF(6, 8));

        var hIcon = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
