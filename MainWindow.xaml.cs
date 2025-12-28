using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaudeAutoResponse.ViewModels;

namespace ClaudeAutoResponse
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set dark title bar
            SetDarkTitleBar();

            // Restore window position
            var settings = _viewModel.Settings;
            if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            {
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void SetDarkTitleBar()
        {
            try
            {
                var windowHelper = new WindowInteropHelper(this);
                IntPtr hwnd = windowHelper.Handle;

                if (hwnd != IntPtr.Zero)
                {
                    int useDarkMode = 1;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                }
            }
            catch
            {
                // Silently fail on older Windows versions
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Save window position
            _viewModel.Settings.WindowLeft = Left;
            _viewModel.Settings.WindowTop = Top;
            _viewModel.Settings.Save();

            // Hide instead of close (minimize to tray)
            e.Cancel = true;
            Hide();
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            PinIcon.Opacity = Topmost ? 1.0 : 0.6;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshWindows();
        }

        public void ForceClose()
        {
            Closing -= MainWindow_Closing;
            Close();
        }

        public MainViewModel ViewModel => _viewModel;
    }
}
