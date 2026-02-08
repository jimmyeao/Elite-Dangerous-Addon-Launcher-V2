using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for managing application settings
    /// </summary>
    public class SettingsService : ISettingsService
    {
        public async Task<Settings> LoadSettingsAsync()
        {
            Constants.EnsureAppDataFolderExists();

            Log.Information("SettingsService.LoadSettingsAsync() called");
            Log.Information("Settings path: {Path}", Constants.SettingsPath);

            if (File.Exists(Constants.SettingsPath))
            {
                Log.Information("Loading settings from: {FilePath}", Constants.SettingsPath);
                return await LoadFromPathAsync(Constants.SettingsPath);
            }

            // Try migrating from legacy location
            if (File.Exists(Constants.LegacySettingsPath))
            {
                Log.Information("Migrating settings from legacy location: {LegacyPath} to {NewPath}",
                    Constants.LegacySettingsPath, Constants.SettingsPath);
                try
                {
                    File.Copy(Constants.LegacySettingsPath, Constants.SettingsPath);
                    Log.Information("Settings migration successful");
                    return await LoadFromPathAsync(Constants.SettingsPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to migrate settings from legacy location");
                }
            }

            // No settings file found, return defaults
            Log.Information("Settings file does not exist, returning defaults");
            return new Settings
            {
                CloseAllAppsOnExit = false,
                Theme = "Light",
                EliteInstallType = "Standard"
            };
        }

        public async Task SaveSettingsAsync(Settings settings)
        {
            Constants.EnsureAppDataFolderExists();

            Log.Information("Saving settings - EliteInstallType: {Type}, Theme: {Theme}, CloseAllAppsOnExit: {Close}",
                settings.EliteInstallType, settings.Theme, settings.CloseAllAppsOnExit);

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);

            Log.Information("Settings JSON to save: {Json}", json);

            await File.WriteAllTextAsync(Constants.SettingsPath, json);

            Log.Information("Settings saved to: {Path}", Constants.SettingsPath);
        }

        private async Task<Settings> LoadFromPathAsync(string path)
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);

                Log.Information("Settings JSON content: {Json}", json);

                var settings = JsonConvert.DeserializeObject<Settings>(json);

                if (settings != null)
                {
                    Log.Information("Deserialized EliteInstallType: {Type}", settings.EliteInstallType);
                }

                return settings ?? new Settings
                {
                    CloseAllAppsOnExit = false,
                    Theme = "Light",
                    EliteInstallType = "Standard"
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings from {Path}", path);
                return new Settings
                {
                    CloseAllAppsOnExit = false,
                    Theme = "Light",
                    EliteInstallType = "Standard"
                };
            }
        }
    }
}
