using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeAutoResponse.Services
{
    public class WindowDetectionService
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public List<(IntPtr Handle, string Title)> GetAllVSCodeWindows()
        {
            var windows = new List<(IntPtr, string)>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrEmpty(title))
                    return true;

                // Check if it's a VS Code window
                if (IsVSCodeWindow(hWnd, title))
                {
                    windows.Add((hWnd, title));
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public (IntPtr Handle, string Title)? GetForegroundVSCodeWindow()
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return null;

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(title))
                return null;

            if (IsVSCodeWindow(hWnd, title))
            {
                return (hWnd, title);
            }

            return null;
        }

        private bool IsVSCodeWindow(IntPtr hWnd, string title)
        {
            // First check title for VS Code patterns
            if (title.Contains("Visual Studio Code") || title.Contains("- Code"))
            {
                return true;
            }

            // Also check process name
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(260);
                        if (GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, (uint)sb.Capacity) > 0)
                        {
                            var processPath = sb.ToString().ToLower();
                            return processPath.Contains("code.exe");
                        }
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }
            }
            catch
            {
                // Ignore errors checking process
            }

            return false;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0)
                return string.Empty;

            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public bool IsWindowStillValid(IntPtr hWnd)
        {
            return IsWindowVisible(hWnd) && !string.IsNullOrEmpty(GetWindowTitle(hWnd));
        }
    }
}
