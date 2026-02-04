using System;
using System.IO;
using System.Text.Json;

namespace ClaudeAutoResponse.Models
{
    public class UserSettings
    {
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public int PollingIntervalMs { get; set; } = 500;
        public bool StartMinimized { get; set; } = true;

        /// <summary>
        /// Whether to enable RDR (Recover Debug Resend) signal file monitoring
        /// </summary>
        public bool RdrEnabled { get; set; } = true;

        /// <summary>
        /// Project paths to monitor for RDR signal files (.auto-claude/rdr-pending.json)
        /// </summary>
        public string[] RdrProjectPaths { get; set; } = new[]
        {
            @"C:\Users\topem\Desktop\CV Project",
            @"C:\Users\topem\source\repos\Auto-Claude Mod"
        };

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeAutoResponse",
            "settings.json");

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch
            {
                // Return defaults on error
            }
            return new UserSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail on save errors
            }
        }
    }
}
