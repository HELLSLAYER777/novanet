using System.Windows;

namespace NovaNet;

public partial class App : Application
{
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

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, ex) => { Crash(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Crash((Exception)ex.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Crash(ex.Exception); ex.SetObserved(); };
        base.OnStartup(e);
    }
}
