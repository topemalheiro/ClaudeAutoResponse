using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ClaudeAutoResponse.Models;
using ClaudeAutoResponse.Services;

namespace ClaudeAutoResponse.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly WindowDetectionService _windowDetection;
        private string _statusMessage = "Ready";

        public ObservableCollection<TrackedWindow> TrackedWindows { get; } = new();
        public UserSettings Settings { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            Settings = UserSettings.Load();
            _windowDetection = new WindowDetectionService();
            RefreshWindows();
        }

        public void RefreshWindows()
        {
            var currentWindows = _windowDetection.GetAllVSCodeWindows();

            // Remove windows that no longer exist
            var toRemove = TrackedWindows
                .Where(tw => !currentWindows.Any(cw => cw.Handle == tw.Handle))
                .ToList();

            foreach (var window in toRemove)
            {
                TrackedWindows.Remove(window);
            }

            // Add new windows
            foreach (var (handle, title) in currentWindows)
            {
                if (!TrackedWindows.Any(tw => tw.Handle == handle))
                {
                    TrackedWindows.Add(new TrackedWindow
                    {
                        Handle = handle,
                        Title = title,
                        Mode = AutoResponseMode.Off
                    });
                }
                else
                {
                    // Update title if changed
                    var existing = TrackedWindows.First(tw => tw.Handle == handle);
                    if (existing.Title != title)
                    {
                        existing.Title = title;
                    }
                }
            }

            StatusMessage = $"{TrackedWindows.Count} window(s) tracked";
        }

        public void SetModeForForegroundWindow(AutoResponseMode mode)
        {
            var foreground = _windowDetection.GetForegroundVSCodeWindow();
            if (foreground == null)
            {
                StatusMessage = "No VS Code window in focus";
                return;
            }

            var (handle, title) = foreground.Value;

            // Find or add the window
            var tracked = TrackedWindows.FirstOrDefault(tw => tw.Handle == handle);
            if (tracked == null)
            {
                tracked = new TrackedWindow
                {
                    Handle = handle,
                    Title = title,
                    Mode = mode
                };
                TrackedWindows.Add(tracked);
            }
            else
            {
                tracked.Mode = mode;
            }

            tracked.LastActivity = DateTime.Now;

            var modeStr = mode switch
            {
                AutoResponseMode.Off => "Off",
                AutoResponseMode.Yes => "Yes",
                AutoResponseMode.YesAlways => "Yes+Always",
                _ => "Unknown"
            };

            StatusMessage = $"Mode: {modeStr}";
        }

        public void UpdateStatus(string message)
        {
            StatusMessage = message;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
