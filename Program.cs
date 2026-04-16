using ExplorerWeb.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<FileSystemService>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

var url = "http://localhost:5054";
app.Lifetime.ApplicationStarted.Register(() =>
{
    try { System.Diagnostics.Process.Start(
        new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
    catch { }
});

app.Run(url);
