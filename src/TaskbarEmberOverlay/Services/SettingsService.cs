using System;
using System.IO;
using System.Text.Json;
using TaskbarEmberOverlay.Models;

namespace TaskbarEmberOverlay.Services
{
    public class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string DefaultSettingsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarEmberOverlay");

        public static string DefaultSettingsFilePath =>
            Path.Combine(DefaultSettingsDirectory, "settings.json");

        private readonly string _filePath;
        private readonly object _lock = new();

        public SettingsService(string? customFilePath = null)
        {
            _filePath = customFilePath ?? DefaultSettingsFilePath;
        }

        public AppSettings LoadOrCreate()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_filePath))
                    {
                        string json = File.ReadAllText(_filePath);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                        if (settings != null)
                        {
                            settings.Sanitize();
                            return settings;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] Error loading settings: {ex.Message}");
                }

                var defaultSettings = new AppSettings();
                defaultSettings.Sanitize();
                Save(defaultSettings);
                return defaultSettings;
            }
        }

        public void Save(AppSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    settings.Sanitize();
                    string dir = Path.GetDirectoryName(_filePath) ?? DefaultSettingsDirectory;
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string tempPath = _filePath + ".tmp";
                    string json = JsonSerializer.Serialize(settings, JsonOptions);
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, _filePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
                }
            }
        }
    }
}
