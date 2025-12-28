using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ClaudeAutoResponse.Models;
using ClaudeAutoResponse.Services;

namespace ClaudeAutoResponse
{
    public partial class App : Application
    {
        private MainWindow? _mainWindow;
        private TrayIconService? _trayIcon;
        private GlobalHotkeyService? _hotkeyService;
        private PermissionMonitorService? _monitorService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create main window (hidden initially)
            _mainWindow = new MainWindow();

            // Initialize tray icon
            _trayIcon = new TrayIconService();
            _trayIcon.ShowWindowRequested += (s, _) => ShowMainWindow();
            _trayIcon.ExitRequested += (s, _) => ExitApplication();
            _trayIcon.Initialize();

            // Initialize hotkey service (needs window handle)
            _hotkeyService = new GlobalHotkeyService();

            // Show window briefly to get handle, then hide
            _mainWindow.Show();
            _mainWindow.Hide();

            // Register hotkeys
            _hotkeyService.RegisterHotkeys(
                _mainWindow,
                () => Dispatcher.Invoke(() => SetMode(AutoResponseMode.Off)),
                () => Dispatcher.Invoke(() => SetMode(AutoResponseMode.Yes)),
                () => Dispatcher.Invoke(() => SetMode(AutoResponseMode.YesAlways))
            );

            // Initialize permission monitor
            var trackedWindows = _mainWindow.ViewModel.TrackedWindows.ToList();
            _monitorService = new PermissionMonitorService(trackedWindows);
            _monitorService.StatusChanged += (s, msg) => Dispatcher.Invoke(() => _mainWindow.ViewModel.UpdateStatus(msg));
            _monitorService.ButtonClicked += (s, msg) =>
            {
                Dispatcher.Invoke(() => _mainWindow.ViewModel.UpdateStatus(msg));
                _trayIcon?.ShowBalloon("Auto-Response", msg);
            };

            // Keep the tracked windows list in sync
            _mainWindow.ViewModel.TrackedWindows.CollectionChanged += (s, args) =>
            {
                // Update the monitor's reference
                trackedWindows.Clear();
                trackedWindows.AddRange(_mainWindow.ViewModel.TrackedWindows);
            };

            _monitorService.Start();

            // Update tray tooltip
            _trayIcon.UpdateTooltip("Claude Auto-Response - Running");

            // Show window if not starting minimized
            var settings = UserSettings.Load();
            if (!settings.StartMinimized)
            {
                ShowMainWindow();
            }
        }

        private void SetMode(AutoResponseMode mode)
        {
            _mainWindow?.ViewModel.SetModeForForegroundWindow(mode);

            var modeStr = mode switch
            {
                AutoResponseMode.Off => "Off",
                AutoResponseMode.Yes => "Yes",
                AutoResponseMode.YesAlways => "Yes+Always",
                _ => "Unknown"
            };

            _trayIcon?.ShowBalloon("Mode Changed", $"Set to: {modeStr}");
        }

        private void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.Activate();
                _mainWindow.ViewModel.RefreshWindows();
            }
        }

        private void ExitApplication()
        {
            _monitorService?.Dispose();
            _hotkeyService?.UnregisterHotkeys();
            _trayIcon?.Dispose();
            _mainWindow?.ForceClose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _monitorService?.Dispose();
            _hotkeyService?.UnregisterHotkeys();
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
