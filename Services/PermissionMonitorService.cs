using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClaudeAutoResponse.Models;

namespace ClaudeAutoResponse.Services
{
    /// <summary>
    /// Auto-approves Claude Code permission prompts by sending keystroke '1' (Yes).
    /// - Foreground: Direct SendInput (instant, no flicker)
    /// - Background: Quick focus switch with retry (brief flicker ~150ms)
    /// </summary>
    public class PermissionMonitorService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
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
            StatusChanged?.Invoke(this, "Active");
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

            foreach (var window in windowsToProcess)
            {
                // Check if window still exists
                if (!IsWindow(window.Handle))
                    continue;

                var currentForeground = GetForegroundWindow();
                bool isForeground = (currentForeground == window.Handle);

                if (isForeground)
                {
                    // Already in foreground - just send keystroke directly
                    SendKeystroke('1');
                    StatusChanged?.Invoke(this, $"Sent: 1 (Yes) - {DateTime.Now:HH:mm:ss}");
                }
                else
                {
                    // Background - quick focus switch with retry
                    bool success = QuickFocusSwitchAndSend(window.Handle, currentForeground);
                    if (success)
                    {
                        StatusChanged?.Invoke(this, $"Sent: 1 (Yes) [bg] - {DateTime.Now:HH:mm:ss}");
                    }
                }
            }
        }

        private bool QuickFocusSwitchAndSend(IntPtr targetWindow, IntPtr originalForeground)
        {
            const int MAX_RETRIES = 3;
            const int FOCUS_DELAY_MS = 100;

            for (int retry = 0; retry < MAX_RETRIES; retry++)
            {
                // Bring target window to foreground
                SetForegroundWindow(targetWindow);
                System.Threading.Thread.Sleep(FOCUS_DELAY_MS);

                // Verify focus actually changed
                if (GetForegroundWindow() == targetWindow)
                {
                    // Send keystroke
                    SendKeystroke('1');
                    System.Threading.Thread.Sleep(50);

                    // Restore original foreground window
                    if (originalForeground != IntPtr.Zero && originalForeground != targetWindow)
                    {
                        SetForegroundWindow(originalForeground);
                    }

                    System.Diagnostics.Debug.WriteLine($"QuickFocusSwitch succeeded on retry {retry}");
                    return true;
                }

                System.Threading.Thread.Sleep(50); // Brief wait before retry
            }

            System.Diagnostics.Debug.WriteLine("QuickFocusSwitch failed after all retries");
            return false;
        }

        private void SendKeystroke(char key)
        {
            try
            {
                // Virtual key code for '1' is 0x31
                ushort vkCode = (ushort)key;

                var inputs = new INPUT[2];

                // Key down
                inputs[0] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = vkCode,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // Key up
                inputs[1] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = vkCode,
                            wScan = 0,
                            dwFlags = KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                uint result = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                System.Diagnostics.Debug.WriteLine($"SendInput '{key}', result: {result}");
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
