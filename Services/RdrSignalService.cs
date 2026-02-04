using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using ClaudeAutoResponse.Models;

namespace ClaudeAutoResponse.Services
{
    /// <summary>
    /// Monitors Auto-Claude projects for RDR signal files and sends check messages to Claude Code.
    ///
    /// State machine:
    /// - IDLE: Just watching for signal files (no polling)
    /// - ACTIVE: Signal detected, processing + polling every 60s until no more tasks need intervention
    /// </summary>
    public class RdrSignalService : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly PermissionMonitorService _permissionService;
        private readonly List<TrackedWindow> _trackedWindows;
        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _followUpTimer;
        private string? _pendingSignalPath;
        private string? _activeProjectPath;
        private bool _isProcessing = false;

        private const int FOLLOW_UP_INTERVAL_MS = 60000;  // 60 seconds

        public event EventHandler<string>? StatusChanged;

        public RdrSignalService(PermissionMonitorService permissionService, List<TrackedWindow> trackedWindows)
        {
            _permissionService = permissionService;
            _trackedWindows = trackedWindows;

            // Debounce timer to avoid rapid-fire processing
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _debounceTimer.Tick += DebounceTimer_Tick;

            // Follow-up timer for checking if more work needed
            _followUpTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FOLLOW_UP_INTERVAL_MS) };
            _followUpTimer.Tick += FollowUpTimer_Tick;
        }

        public void Start(string[] projectPaths)
        {
            foreach (var projectPath in projectPaths)
            {
                var signalDir = Path.Combine(projectPath, ".auto-claude");

                // Create directory if it doesn't exist
                if (!Directory.Exists(signalDir))
                {
                    try
                    {
                        Directory.CreateDirectory(signalDir);
                    }
                    catch
                    {
                        continue;  // Skip if we can't create
                    }
                }

                try
                {
                    var watcher = new FileSystemWatcher(signalDir)
                    {
                        Filter = "rdr-pending.json",
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += OnSignalFileChanged;
                    watcher.Created += OnSignalFileChanged;

                    _watchers.Add(watcher);
                    System.Diagnostics.Debug.WriteLine($"[RDR] Watching: {signalDir}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RDR] Failed to watch {signalDir}: {ex.Message}");
                }
            }

            StatusChanged?.Invoke(this, $"RDR: Idle (watching {_watchers.Count} projects)");
        }

        private void OnSignalFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce: wait for file to be fully written
            _pendingSignalPath = e.FullPath;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (_pendingSignalPath == null || !File.Exists(_pendingSignalPath))
                return;

            try
            {
                var json = File.ReadAllText(_pendingSignalPath);
                var signal = JsonSerializer.Deserialize<RdrSignal>(json);

                if (signal != null && signal.Batches?.Length > 0)
                {
                    _isProcessing = true;
                    // .auto-claude is parent, project path is grandparent
                    _activeProjectPath = Path.GetDirectoryName(Path.GetDirectoryName(_pendingSignalPath));

                    ProcessSignal(signal);

                    // Delete signal file after processing
                    try
                    {
                        File.Delete(_pendingSignalPath);
                    }
                    catch { }

                    // Start follow-up polling to check for more work
                    _followUpTimer.Start();
                    StatusChanged?.Invoke(this, $"RDR: Active ({signal.Batches.Length} batches). Polling every 60s...");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RDR] Signal processing failed: {ex.Message}");
                StatusChanged?.Invoke(this, $"RDR: Error - {ex.Message}");
            }

            _pendingSignalPath = null;
        }

        private void FollowUpTimer_Tick(object? sender, EventArgs e)
        {
            // Check if there are still tasks that need intervention
            if (_activeProjectPath == null)
            {
                StopFollowUp();
                return;
            }

            var hasMoreWork = CheckForPendingTasks(_activeProjectPath);

            if (hasMoreWork)
            {
                // Still have work - ask Claude Code to check again
                SendCheckMessage();
                StatusChanged?.Invoke(this, "RDR: Still tasks pending, sent check request...");
            }
            else
            {
                // No more work - return to idle
                StopFollowUp();
                StatusChanged?.Invoke(this, $"RDR: Idle (all tasks processed)");
            }
        }

        private bool CheckForPendingTasks(string projectPath)
        {
            var specsDir = Path.Combine(projectPath, ".auto-claude", "specs");
            if (!Directory.Exists(specsDir))
                return false;

            try
            {
                foreach (var specDir in Directory.GetDirectories(specsDir))
                {
                    var planPath = Path.Combine(specDir, "implementation_plan.json");
                    if (!File.Exists(planPath))
                        continue;

                    try
                    {
                        var json = File.ReadAllText(planPath);
                        // Check if status is human_review
                        if (json.Contains("\"status\"") && json.Contains("human_review"))
                        {
                            return true;  // Still have tasks in human_review
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return false;  // No tasks need intervention
        }

        private void StopFollowUp()
        {
            _followUpTimer.Stop();
            _isProcessing = false;
            _activeProjectPath = null;
        }

        private void ProcessSignal(RdrSignal signal)
        {
            SendCheckMessage();
        }

        private void SendCheckMessage()
        {
            // Find VS Code window for this project
            var targetWindow = FindWindowForProject(_activeProjectPath);

            if (targetWindow == null)
            {
                StatusChanged?.Invoke(this, "RDR: No VS Code window found for project");
                return;
            }

            // Build the message to send
            var message = "Check RDR batches and fix errored tasks";

            // Send to Claude Code via the permission service's message sending
            _permissionService.SendMessageToClaudeCode(message, targetWindow.Handle);
        }

        private TrackedWindow? FindWindowForProject(string? projectPath)
        {
            if (projectPath == null) return null;

            var projectName = Path.GetFileName(projectPath);

            // Try to find a window that contains the project name in its title
            // VS Code titles are like: "filename.ts - ProjectName - Visual Studio Code"
            return _trackedWindows.FirstOrDefault(w =>
                w.Title.Contains(projectName, StringComparison.OrdinalIgnoreCase) ||
                w.Title.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase));
        }

        public bool IsProcessing => _isProcessing;

        public void Dispose()
        {
            _debounceTimer.Stop();
            _followUpTimer.Stop();

            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }

    public class RdrSignal
    {
        public string? ProjectId { get; set; }
        public string? Timestamp { get; set; }
        public string? Source { get; set; }
        public RdrBatchInfo[]? Batches { get; set; }
        public string? Prompt { get; set; }
    }

    public class RdrBatchInfo
    {
        public string? Type { get; set; }
        public int TaskCount { get; set; }
        public string[]? TaskIds { get; set; }
    }
}
