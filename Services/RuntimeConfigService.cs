using System.Text.Json;
using ExplorerWeb.Models;

namespace ExplorerWeb.Services;

public sealed class RuntimeConfigService
{
    private const string ConfigFileName = "explorerweb.runtime.json";
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public RuntimeConfigService()
    {
        ConfigPath = LocateOrCreateConfig(AppContext.BaseDirectory);
        ConfigDirectory = Path.GetDirectoryName(ConfigPath) ?? AppContext.BaseDirectory;
        ContentRootPath = AppContext.BaseDirectory;
        Config = Load();
    }

    public string ConfigPath { get; }

    public string ConfigDirectory { get; }

    public string ContentRootPath { get; }

    public ExplorerRuntimeConfig Config { get; private set; }

    public ExplorerRuntimeConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Config = new ExplorerRuntimeConfig();
            Save(Config);
            return Config;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            Config = JsonSerializer.Deserialize<ExplorerRuntimeConfig>(json, _serializerOptions) ?? new ExplorerRuntimeConfig();
        }
        catch
        {
            Config = new ExplorerRuntimeConfig();
        }

        return Config;
    }

    public void Save(ExplorerRuntimeConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? AppContext.BaseDirectory);
        var json = JsonSerializer.Serialize(config, _serializerOptions);
        File.WriteAllText(ConfigPath, json);
        Config = config;
    }

    public string ResolveIconsDirectory()
    {
        return LocatePathUpwards(ConfigDirectory, Config.IconsFolder, mustBeDirectory: true)
            ?? LocatePathUpwards(ContentRootPath, Config.IconsFolder, mustBeDirectory: true)
            ?? Path.Combine(ContentRootPath, Config.IconsFolder);
    }

    public string ResolveIconPath(string fileName)
    {
        var iconsDirectory = ResolveIconsDirectory();
        var candidates = new[]
        {
            fileName,
            Path.GetFileNameWithoutExtension(fileName) + ".ico",
            Path.GetFileNameWithoutExtension(fileName) + ".png"
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var direct = LocatePathUpwards(ConfigDirectory, candidate, mustBeDirectory: false);
            if (direct is not null)
            {
                return direct;
            }

            var withinIcons = Path.Combine(iconsDirectory, candidate);
            if (File.Exists(withinIcons))
            {
                return withinIcons;
            }
        }

        return string.Empty;
    }

    private string LocateOrCreateConfig(string startDirectory)
    {
        var existing = LocatePathUpwards(startDirectory, ConfigFileName, mustBeDirectory: false);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var target = Path.Combine(startDirectory, ConfigFileName);
        Directory.CreateDirectory(startDirectory);
        var json = JsonSerializer.Serialize(new ExplorerRuntimeConfig(), _serializerOptions);
        File.WriteAllText(target, json);
        return target;
    }

    private static string? LocatePathUpwards(string startDirectory, string relativeName, bool mustBeDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativeName);
            if (mustBeDirectory)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            else if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
