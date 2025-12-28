using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClaudeAutoResponse.Models
{
    public enum AutoResponseMode
    {
        Off,
        Yes,
        YesAlways
    }

    public class TrackedWindow : INotifyPropertyChanged
    {
        private IntPtr _handle;
        private string _title = string.Empty;
        private AutoResponseMode _mode = AutoResponseMode.Off;
        private DateTime _lastActivity = DateTime.Now;

        public IntPtr Handle
        {
            get => _handle;
            set { _handle = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); }
        }

        public AutoResponseMode Mode
        {
            get => _mode;
            set { _mode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModeIndicator)); }
        }

        public DateTime LastActivity
        {
            get => _lastActivity;
            set { _lastActivity = value; OnPropertyChanged(); }
        }

        public string ModeIndicator => Mode switch
        {
            AutoResponseMode.Yes => "[Y]",
            AutoResponseMode.YesAlways => "[Y+]",
            _ => "[--]"
        };

        public string DisplayTitle
        {
            get
            {
                // Shorten long titles for display
                var title = Title;
                if (title.Length > 50)
                    title = title.Substring(0, 47) + "...";
                return $"{ModeIndicator} {title}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
