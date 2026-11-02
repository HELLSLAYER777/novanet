using System.Windows;
using System.Windows.Forms;
using NovaNet.Models;
using Application = System.Windows.Application;

namespace NovaNet.Services;

/// <summary>Manages system tray icon and background monitoring.</summary>
public sealed class TrayService : IDisposable
{
    readonly NotifyIcon _trayIcon;
    readonly SettingsService _settings;
    readonly LiveViewModel _liveVm;
    Window? _mainWindow;
    bool _isMinimized;

    public TrayService(SettingsService settings, LiveViewModel liveVm)
    {
        _settings = settings;
        _liveVm = liveVm;

        _trayIcon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon("Assets/app.ico"),
            Text = "NovaNet - Network Optimizer",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _trayIcon.DoubleClick += OnTrayDoubleClick;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
        window.StateChanged += OnWindowStateChanged;
    }

    void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == WindowState.Minimized && _settings.Settings.MinimizeToTray)
        {
            _isMinimized = true;
            _mainWindow.Hide();
        }
    }

    void OnTrayDoubleClick(object? sender, EventArgs e) => ShowWindow();

    void ShowWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _isMinimized = false;
        }
    }

    ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        
        var showItem = new ToolStripMenuItem("Show NovaNet", null, (_, _) => ShowWindow());
        showItem.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        menu.Items.Add(showItem);
        
        menu.Items.Add(new ToolStripSeparator());
        
        var liveItem = new ToolStripMenuItem("Live Monitoring", null, (_, _) => 
        {
            ShowWindow();
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                // Navigate to Live page
            });
        });
        menu.Items.Add(liveItem);
        
        menu.Items.Add(new ToolStripSeparator());
        
        var settingsItem = new ToolStripMenuItem("Settings", null, (_, _) => 
        {
            ShowWindow();
            // Navigate to settings
        });
        menu.Items.Add(settingsItem);
        
        menu.Items.Add(new ToolStripSeparator());
        
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => 
        {
            Application.Current?.Shutdown();
        });
        menu.Items.Add(exitItem);
        
        return menu;
    }

    void OnSettingsChanged(AppSettings s)
    {
        // Update tray behavior based on settings
    }

    public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_settings.Settings.EnableNotifications)
            _trayIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        _trayIcon.DoubleClick -= OnTrayDoubleClick;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        if (_mainWindow != null) _mainWindow.StateChanged -= OnWindowStateChanged;
    }
}