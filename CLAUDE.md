# ClaudeAutoResponse

A Windows WPF application that auto-approves Claude Code permission prompts in VS Code windows.

## Purpose

When using Claude Code in VS Code, permission prompts appear asking for approval (Yes/No buttons). This app automatically sends keystroke '1' (which selects "Yes") to tracked VS Code windows, allowing unattended operation.

## Architecture

```
ClaudeAutoResponse/
├── Models/
│   ├── TrackedWindow.cs      # Window handle + mode (Yes/YesAlways/Off)
│   └── AutoResponseMode.cs   # Enum for response modes
├── Services/
│   ├── PermissionMonitorService.cs  # Core keystroke injection logic
│   └── WindowDiscoveryService.cs    # Finds VS Code windows
├── MainWindow.xaml(.cs)      # WPF UI for window management
└── App.xaml(.cs)             # Application entry point
```

## How It Works

### Window Tracking
- Uses `EnumWindows` Win32 API to find all VS Code windows
- User selects which windows to track and sets mode per window
- Modes: `Yes` (send 1), `YesAlways` (send 1), `Off` (ignore)

### Keystroke Injection

**The Challenge:** VS Code is an Electron (Chromium) app. Chromium has security features that discard keyboard input when the window is not focused. This prevents background input injection.

**Approaches Tried & Failed:**
| Approach | Result | Why |
|----------|--------|-----|
| `PostMessage WM_CHAR` | ❌ Background only | Chromium discards - no focus validation |
| `PostMessage WM_KEYDOWN/UP` | ❌ Background only | Same - Chromium security |
| `AttachThreadInput` | ❌ Broke everything | Cross-process, destabilized input queue |
| UI Automation `InvokePattern` | ❌ Can't see buttons | Webview content not in accessibility tree |

**Final Solution:** Robust focus switch with `SendInput`

```csharp
// Foreground: Direct SendInput (instant, no flicker)
if (GetForegroundWindow() == targetWindow)
{
    SendKeystroke('1');
}
// Background: Quick focus switch with retry
else
{
    for (int retry = 0; retry < 3; retry++)
    {
        SetForegroundWindow(targetWindow);
        Thread.Sleep(100);

        if (GetForegroundWindow() == targetWindow)
        {
            SendKeystroke('1');
            Thread.Sleep(50);
            SetForegroundWindow(originalForeground); // Restore
            break;
        }
    }
}
```

### Key Implementation Details

**PermissionMonitorService.cs** - Core service:
- Uses `DispatcherTimer` polling (default 1 second interval)
- `SendInput` Win32 API for keystroke injection (key down + key up)
- `SetForegroundWindow` for background window activation
- Retry logic (3 attempts) for reliable focus switching
- 100ms delay after focus switch for stabilization
- Automatic restoration of original foreground window

**Why '1' is Always Safe:**
- Claude Code permission prompts always have "Yes" as button 1
- Question prompts (which shouldn't be auto-approved) use radio buttons, not numbered buttons
- Pressing '1' on a question prompt does nothing harmful

## Behavior

| Scenario | Behavior |
|----------|----------|
| VS Code in foreground | Instant keystroke, no flicker |
| VS Code in background | Brief flicker (~150ms) as focus switches and returns |
| Multiple VS Code windows | Each tracked window processed in sequence |

## Technical Constraints

**Chromium Security Model:**
- Browser process receives Windows messages
- Renderer process (webview) runs in sandbox
- Keyboard events validated for focus before forwarding to renderer
- No focus = event discarded (prevents input injection attacks)

**Windows SetForegroundWindow Restrictions:**
- Windows throttles `SetForegroundWindow` calls from background apps
- Only works reliably when caller is foreground or recently was
- Hence retry logic and focus verification

## Building & Running

```powershell
cd c:\Users\topem\scripts\ClaudeAutoResponse
dotnet build
.\bin\Debug\net8.0-windows\ClaudeAutoResponse.exe
```

## Project File

- Target: .NET 8.0 Windows
- Uses WPF for UI
- Uses WinForms for NotifyIcon (system tray)
- Package: System.Text.Json for settings persistence

## Future Improvements

1. **True Background Operation** - Would require either:
   - VS Code launched with `--force-renderer-accessibility` flag
   - Chrome DevTools Protocol (CDP) with `--remote-debugging-port`
   - Neither is practical for general use

2. **Smarter Detection** - Could detect if a permission prompt is actually showing before sending keystroke (currently sends on every poll interval)

3. **Settings Persistence** - Save tracked windows between sessions

## Files Modified During Development

- `Services/PermissionMonitorService.cs` - Multiple iterations to find working approach
- `ClaudeAutoResponse.csproj` - Added WinForms reference for NotifyIcon
- Created test files to trigger permission prompts during development
