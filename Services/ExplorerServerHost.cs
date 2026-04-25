using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExplorerWeb.Services;

public sealed class ExplorerServerHost
{
    private readonly string[] _args;
    private readonly RuntimeConfigService _runtimeConfig;
    private readonly ILoggerFactory _loggerFactory;
    private WebApplication? _app;

    public ExplorerServerHost(string[] args, RuntimeConfigService runtimeConfig, ILoggerFactory loggerFactory)
    {
        _args = args;
        _runtimeConfig = runtimeConfig;
        _loggerFactory = loggerFactory;
    }

    public bool IsRunning => _app is not null;

    public string Url => _runtimeConfig.Config.Url;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var options = new WebApplicationOptions
        {
            Args = _args,
            ContentRootPath = _runtimeConfig.ContentRootPath
        };

        var builder = WebApplication.CreateBuilder(options);
        builder.WebHost.UseUrls(_runtimeConfig.Config.Url);
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddSimpleConsole();

        builder.Services.AddSingleton(_runtimeConfig);
        builder.Services.AddSingleton<FileSystemService>();
        builder.Services.AddControllers();

        var app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();

        await app.StartAsync(cancellationToken);
        _app = app;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            await _app.StopAsync(cancellationToken);
        }
        finally
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
