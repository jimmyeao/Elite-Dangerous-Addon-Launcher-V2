using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for managing application settings with migration support
    /// </summary>
    public class SettingsService : ISettingsService
    {
        public async Task<Settings> LoadSettingsAsync()
        {
            Constants.EnsureAppDataFolderExists();

            Log.Information("SettingsService.LoadSettingsAsync() called");
            Log.Information("New settings path: {NewPath}", Constants.SettingsPath);
            Log.Information("Legacy settings path: {LegacyPath}", Constants.LegacySettingsPath);
            Log.Information("New path exists: {NewExists}", File.Exists(Constants.SettingsPath));
            Log.Information("Legacy path exists: {LegacyExists}", File.Exists(Constants.LegacySettingsPath));

            // First, try to load from the new location
            if (File.Exists(Constants.SettingsPath))
            {
                Log.Information("Loading settings from new location: {FilePath}", Constants.SettingsPath);
                return await LoadFromPathAsync(Constants.SettingsPath);
            }

            // Fall back to legacy location
            if (File.Exists(Constants.LegacySettingsPath))
            {
                Log.Information("Found settings in legacy location: {LegacyPath}", Constants.LegacySettingsPath);
                Log.Information("Migrating settings to: {NewPath}", Constants.SettingsPath);

                var settings = await LoadFromPathAsync(Constants.LegacySettingsPath);

                Log.Information("Loaded settings from legacy location: Theme={Theme}, CloseAllAppsOnExit={CloseAllAppsOnExit}",
                    settings.Theme, settings.CloseAllAppsOnExit);

                // Save to new location
                await SaveSettingsAsync(settings);

                Log.Information("Saved settings to new location");

                // Delete old file after successful migration
                try
                {
                    File.Delete(Constants.LegacySettingsPath);
                    Log.Information("Successfully migrated and deleted legacy settings file");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete legacy settings file at {Path}", Constants.LegacySettingsPath);
                }

                return settings;
            }

            // No settings file found, return defaults
            Log.Warning("Settings file does not exist in either location, using defaults");
            Log.Warning("Checked paths: New={NewPath}, Legacy={LegacyPath}", Constants.SettingsPath, Constants.LegacySettingsPath);
            var defaultSettings = new Settings
            {
                Theme = "Default",
                EliteInstallType = "Standard",
                CloseAllAppsOnExit = false
            };

            // Save defaults for next time
            await SaveSettingsAsync(defaultSettings);

            return defaultSettings;
        }

        public async Task SaveSettingsAsync(Settings settings)
        {
            Constants.EnsureAppDataFolderExists();

            Log.Information("Saving settings - EliteInstallType: {EliteInstallType}, Theme: {Theme}, CloseAllAppsOnExit: {CloseAllAppsOnExit}",
                settings.EliteInstallType, settings.Theme, settings.CloseAllAppsOnExit);

            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            Log.Information("Settings JSON to save: {Json}", json);

            await File.WriteAllTextAsync(Constants.SettingsPath, json);
            Log.Information("Settings saved to: {FilePath}", Constants.SettingsPath);
        }

        private async Task<Settings> LoadFromPathAsync(string path)
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);
                Log.Information("Settings JSON content: {Json}", json);

                var settings = JsonConvert.DeserializeObject<Settings>(json);
                Log.Information("Deserialized EliteInstallType: {EliteInstallType}", settings?.EliteInstallType ?? "null");

                return settings ?? new Settings { Theme = "Default", EliteInstallType = "Standard" };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings from {Path}", path);
                return new Settings { Theme = "Default", EliteInstallType = "Standard" };
            }
        }
    }
}
