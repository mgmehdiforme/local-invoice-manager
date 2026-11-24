using System.Diagnostics;

namespace LocalInvoiceManager.Web.Infrastructure;

/// <summary>
/// Settings for the self-contained downloads (Windows, macOS, Linux without Docker):
/// listen on 127.0.0.1:8090 only, keep data in the user's app-data folder, and open the browser.
/// Docker, development and tests keep their own configuration.
/// </summary>
public static class NativeHost
{
    public const int DefaultPort = 8090;

    /// <summary>
    /// The downloaded app may be started from any folder (a shortcut, a launcher); its web files sit next to the
    /// executable. Returns that folder when the current folder has no UI, otherwise null (the default).
    /// </summary>
    public static string? ContentRoot()
    {
        static bool HasUi(string dir) => File.Exists(Path.Combine(dir, "wwwroot", "index.html"));
        var exeDir = AppContext.BaseDirectory;
        return HasUi(exeDir) && !HasUi(Directory.GetCurrentDirectory()) ? exeDir : null;
    }

    public static bool Configure(WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        if (inContainer || env.IsDevelopment() || env.IsEnvironment("Testing")) return false;

        var cfg = builder.Configuration;

        // Data folder: LIM_HOME, else %LOCALAPPDATA%\LocalInvoiceManager (Windows),
        // ~/Library/Application Support/LocalInvoiceManager (macOS), ~/.local/share/LocalInvoiceManager (Linux).
        var home = Environment.GetEnvironmentVariable("LIM_HOME") is { Length: > 0 } h
            ? h
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "LocalInvoiceManager");
        if (cfg["Storage:DataPath"] is null or "data") cfg["Storage:DataPath"] = Path.Combine(home, "data");
        if (cfg["Storage:BackupPath"] is null or "backups") cfg["Storage:BackupPath"] = Path.Combine(home, "backups");

        // Local only. `--port 9000` or APP_PORT=9000 changes the port; ASPNETCORE_URLS / --urls override everything.
        if (string.IsNullOrEmpty(cfg["urls"]) && string.IsNullOrEmpty(cfg["http_ports"]))
        {
            var port = int.TryParse(cfg["port"] ?? Environment.GetEnvironmentVariable("APP_PORT"), out var p) && p is > 0 and < 65536 ? p : DefaultPort;
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        }
        return true;
    }

    public static void OpenBrowserWhenReady(WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var url = app.Urls.FirstOrDefault()?.Replace("127.0.0.1", "localhost") ?? $"http://localhost:{DefaultPort}";
            Console.WriteLine($"Invoice Manager is running at {url}  (press Ctrl+C to stop)");
            try
            {
                if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                else if (OperatingSystem.IsMacOS()) Process.Start("open", url);
                else Process.Start("xdg-open", url);
            }
            catch (Exception e)
            {
                app.Logger.LogInformation("Could not open a browser ({Error}). Open {Url} yourself.", e.Message, url);
            }
        });
    }
}
