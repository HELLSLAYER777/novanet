using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovaNet.Services;
using NovaNet.ViewModels;

namespace NovaNet;

public partial class App : Application
{
    public static IHost? Host { get; private set; }
    public static IServiceProvider Services => Host!.Services;

    static void Crash(Exception ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaNet");
            Directory.CreateDirectory(dir);
            File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), $"[{DateTime.Now}] {ex}\n\n");
        }
        catch { }
        MessageBox.Show(ex.Message + "\n\nDetails saved to %AppData%\\NovaNet\\crash.log", "NovaNet", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, ex) => { Crash(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Crash((Exception)ex.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Crash(ex.Exception); ex.SetObserved(); };

        Host = CreateHostBuilder().Build();
        Host.Start();

        // Initialize theme
        var theme = Services.GetRequiredService<ThemeService>();
        
        // Create and show main window
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Initialize viewmodels
        var mainVm = Services.GetRequiredService<MainViewModel>();
        mainVm.Initialize();

        // Check for updates in background
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000); // Wait for UI to settle
            var updateService = Services.GetRequiredService<UpdateService>();
            await updateService.CheckAndNotifyAsync();
        });
    }

    static IHostBuilder CreateHostBuilder() => Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) =>
        {
            // Core services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<TrayService>();
            services.AddSingleton<UpdateService>();

            // ViewModels
            services.AddSingleton<LiveViewModel>();
            services.AddSingleton<OptimizeViewModel>();
            services.AddSingleton<DnsViewModel>();
            services.AddSingleton<NetViewModel>();
            services.AddSingleton<SpeedViewModel>();
            services.AddSingleton<GuideViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();
            services.AddTransient<SettingsWindow>();
        });

    protected override async void OnExit(ExitEventArgs e)
    {
        Host?.Dispose();
        base.OnExit(e);
    }
}
