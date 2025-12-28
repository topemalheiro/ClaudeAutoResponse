using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ClaudeAutoResponse.Services
{
    public class GlobalHotkeyService
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Hotkey IDs
        private const int HOTKEY_ID_MODE_OFF = 9100;      // Ctrl+Alt+1
        private const int HOTKEY_ID_MODE_YES = 9101;      // Ctrl+Alt+2
        private const int HOTKEY_ID_MODE_YES_ALWAYS = 9102; // Ctrl+Alt+3

        private const int WM_HOTKEY = 0x0312;

        // Modifiers
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;

        // Virtual key codes for 1, 2, 3
        private const uint VK_1 = 0x31;
        private const uint VK_2 = 0x32;
        private const uint VK_3 = 0x33;

        private IntPtr _windowHandle;
        private HwndSource? _source;

        private Action? _onModeOff;
        private Action? _onModeYes;
        private Action? _onModeYesAlways;

        public void RegisterHotkeys(Window window, Action onModeOff, Action onModeYes, Action onModeYesAlways)
        {
            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.Handle;

            _onModeOff = onModeOff;
            _onModeYes = onModeYes;
            _onModeYesAlways = onModeYesAlways;

            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);

            uint modifiers = MOD_CONTROL | MOD_ALT;

            // Register Ctrl+Alt+1 (Off)
            RegisterHotKey(_windowHandle, HOTKEY_ID_MODE_OFF, modifiers, VK_1);

            // Register Ctrl+Alt+2 (Yes)
            RegisterHotKey(_windowHandle, HOTKEY_ID_MODE_YES, modifiers, VK_2);

            // Register Ctrl+Alt+3 (Yes Always)
            RegisterHotKey(_windowHandle, HOTKEY_ID_MODE_YES_ALWAYS, modifiers, VK_3);
        }

        public void UnregisterHotkeys()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID_MODE_OFF);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_MODE_YES);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_MODE_YES_ALWAYS);
            }

            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                switch (id)
                {
                    case HOTKEY_ID_MODE_OFF:
                        _onModeOff?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_ID_MODE_YES:
                        _onModeYes?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_ID_MODE_YES_ALWAYS:
                        _onModeYesAlways?.Invoke();
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }
    }
}
