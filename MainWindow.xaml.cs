using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using NovaNet.Services;
using NovaNet.ViewModels;

namespace NovaNet;
{
    public partial class MainWindow : Window
    {
        readonly TrayService _trayService;
        readonly MainViewModel _viewModel;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public MainWindow(TrayService trayService, MainViewModel viewModel)
        {
            _trayService = trayService;
            _viewModel = viewModel;
            DataContext = _viewModel;

            InitializeComponent();

            SourceInitialized += (_, _) =>
            {
                int dark = 1; // dark title bar
                DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
            };

            Loaded += OnLoaded;
            Closed += OnClosed;
            StateChanged += OnStateChanged;

            _trayService.SetMainWindow(this);
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ViewModels handle their own initialization
        }

        void OnStateChanged(object? sender, EventArgs e)
        {
            // Handled by TrayService
        }

        void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.Dispose();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var settings = App.Services.GetRequiredService<SettingsService>();
            if (settings.Settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                WindowState = WindowState.Minimized;
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}