using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System.Text.Json;
using System;
using System.Threading.Tasks;
using CryptoSelfBot.Wpf.Services;
using CryptoSelfBot.Engine.Services;
using CryptoSelfBot.Engine;

namespace CryptoSelfBot.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private NewsService? _newsService;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Intentar leer appsettings.json si existe y aplicar valores básicos a AppDataService
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string cfgPath = Path.Combine(baseDir, "appsettings.json");
            if (File.Exists(cfgPath))
            {
                var doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
                if (doc.RootElement.TryGetProperty("ApiSettings", out var api))
                {
                    if (api.TryGetProperty("ApiEndpoint", out var ep))
                        AppDataService.Configuration.ApiEndpoint = ep.GetString();
                    if (api.TryGetProperty("PythonPath", out var py))
                        AppDataService.Configuration.PythonPath = py.GetString();
                    if (api.TryGetProperty("FREDApiKey", out var fred))
                        AppDataService.Configuration.FREDApiKey = fred.GetString();
                    if (api.TryGetProperty("EcbScraperEnabled", out var ecb))
                        AppDataService.Configuration.EcbScraperEnabled = ecb.GetBoolean();
                }
                // Persistir configuración leída en el AppDataService para uso runtime
                try { AppDataService.Save(); } catch { }
            }
        }
        catch { }
            try
            {
                var db = new CryptoSelfBot.Engine.Services.DatabaseService();
                _newsService = new CryptoSelfBot.Engine.Services.NewsService(db, CryptoSelfBot.Wpf.Services.AppDataService.Configuration.StoragePath);
                _newsService.StartBackgroundTasks();
                try
                {
                    var appDataType = typeof(CryptoSelfBot.Wpf.Services.AppDataService);
                    var prop = appDataType.GetProperty("NewsService", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(null, _newsService);
                    }
                }
                catch { }
            }
            catch { }
        // Register global handlers to capture startup exceptions and log them
        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            try { LogException(ev.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException"); }
            catch { }
        };

        this.DispatcherUnhandledException += (s, ev) =>
        {
            try { LogException(ev.Exception, "Application.DispatcherUnhandledException"); } catch { }
            // let default handler proceed so the debugger can break if attached
        };

        TaskScheduler.UnobservedTaskException += (s, ev) =>
        {
            try { LogException(ev.Exception, "TaskScheduler.UnobservedTaskException"); ev.SetObserved(); } catch { }
        };
        try
        {
            // Apply persisted theme if available
            var theme = CryptoSelfBot.Wpf.Services.AppDataService.Configuration.Theme;
            if (!string.IsNullOrEmpty(theme))
            {
                // Use ThemeManager which handles pack and relative/absolute URIs and logs failures
                try
                {
                    CryptoSelfBot.Wpf.Services.ThemeManager.ApplyTheme(theme);
                }
                catch (Exception ex)
                {
                    // Log to temp and continue startup
                    try
                    {
                        string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CryptoSelfBot_startup.log");
                        System.IO.File.AppendAllText(file, "Failed to apply persisted theme: " + ex + Environment.NewLine);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private void LogException(Exception? ex, string source)
    {
        try
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "CryptoSelfBot_startup.log");
            using var sw = new StreamWriter(file, true);
            sw.WriteLine("--- " + DateTime.Now.ToString("s") + " ---");
            sw.WriteLine("Source: " + source);
            if (ex != null)
            {
                sw.WriteLine(ex.ToString());
            }
            else
            {
                sw.WriteLine("(no exception object)");
            }
            sw.WriteLine();
            MessageBox.Show($"Se ha producido un error en el arranque. Revisa el archivo de registro: {file}", "Error de arranque", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}
