using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClaudeAutoResponse.Models;

namespace ClaudeAutoResponse.Services
{
    /// <summary>
    /// Sends keystrokes to auto-approve Claude Code permission prompts.
    /// When mode is enabled, sends '1' (Yes) or '2' (Yes Always) every second.
    /// If no prompt is visible, the keystroke does nothing (harmless).
    /// </summary>
    public class PermissionMonitorService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
            public uint padding1;
            public uint padding2;
        }

        private readonly DispatcherTimer _timer;
        private readonly List<TrackedWindow> _trackedWindows;
        private readonly object _lock = new();

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? ButtonClicked;

        public PermissionMonitorService(List<TrackedWindow> trackedWindows)
        {
            _trackedWindows = trackedWindows;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000) // 1 second
            };
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _timer.Start();
            StatusChanged?.Invoke(this, "Active - sending keystrokes");
        }

        public void Stop()
        {
            _timer.Stop();
            StatusChanged?.Invoke(this, "Stopped");
        }

        public void SetPollingInterval(int milliseconds)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            List<TrackedWindow> windowsToProcess;
            lock (_lock)
            {
                windowsToProcess = _trackedWindows
                    .Where(w => w.Mode != AutoResponseMode.Off)
                    .ToList();
            }

            if (windowsToProcess.Count == 0)
                return;

            var foreground = GetForegroundWindow();

            foreach (var window in windowsToProcess)
            {
                // Only send keystroke if this window is in foreground
                if (foreground != window.Handle)
                    continue;

                // Send the appropriate keystroke
                char key = window.Mode == AutoResponseMode.YesAlways ? '2' : '1';
                SendKeystroke(key);

                var action = key == '2' ? "2 (Yes Always)" : "1 (Yes)";
                StatusChanged?.Invoke(this, $"Sent: {action}");

                // Only process one window per tick
                break;
            }
        }

        private void SendKeystroke(char key)
        {
            try
            {
                // Get virtual key code for the character
                short vk = VkKeyScan(key);
                if (vk == -1)
                    return;

                ushort virtualKey = (ushort)(vk & 0xFF);

                INPUT[] inputs = new INPUT[2];

                // Key down
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].ki.wVk = virtualKey;
                inputs[0].ki.dwFlags = 0;

                // Key up
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].ki.wVk = virtualKey;
                inputs[1].ki.dwFlags = KEYEVENTF_KEYUP;

                uint result = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                System.Diagnostics.Debug.WriteLine($"Sent keystroke '{key}', result: {result}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendKeystroke failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
