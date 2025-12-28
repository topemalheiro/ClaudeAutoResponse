using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeAutoResponse.Services
{
    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;

        public event EventHandler? ShowWindowRequested;
        public event EventHandler? ExitRequested;

        public void Initialize()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("Open Control Panel", null, (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty));
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Exit", null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateDefaultIcon(),
                Text = "Claude Auto-Response",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private Icon CreateDefaultIcon()
        {
            // Create a simple icon programmatically (16x16 blue square with "C")
            using var bitmap = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bitmap);

            // Fill with Claude orange-ish color
            g.Clear(Color.FromArgb(217, 119, 87));

            // Draw "C" in white
            using var font = new Font("Segoe UI", 10, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            g.DrawString("C", font, brush, -1, 0);

            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void UpdateTooltip(string text)
        {
            if (_notifyIcon != null)
            {
                // Truncate to 63 chars (NotifyIcon limit)
                if (text.Length > 63)
                    text = text.Substring(0, 60) + "...";
                _notifyIcon.Text = text;
            }
        }

        public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _contextMenu?.Dispose();
            _contextMenu = null;
        }
    }
}
