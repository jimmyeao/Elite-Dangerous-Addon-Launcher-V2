using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for managing profiles with migration support
    /// </summary>
    public class ProfileService : IProfileService
    {
        public async Task<ObservableCollection<Profile>> LoadProfilesAsync()
        {
            Constants.EnsureAppDataFolderExists();

            Log.Information("ProfileService.LoadProfilesAsync() called");
            Log.Information("New profiles path: {NewPath}", Constants.ProfilesPath);
            Log.Information("Legacy profiles path: {LegacyPath}", Constants.LegacyProfilesPath);
            Log.Information("New path exists: {NewExists}", File.Exists(Constants.ProfilesPath));
            Log.Information("Legacy path exists: {LegacyExists}", File.Exists(Constants.LegacyProfilesPath));

            // First, try to load from the new location
            if (File.Exists(Constants.ProfilesPath))
            {
                Log.Information("Loading profiles from new location: {FilePath}", Constants.ProfilesPath);
                return await LoadFromPathAsync(Constants.ProfilesPath);
            }

            // Fall back to legacy location
            if (File.Exists(Constants.LegacyProfilesPath))
            {
                Log.Information("Found profiles in legacy location: {LegacyPath}", Constants.LegacyProfilesPath);
                Log.Information("Migrating profiles to: {NewPath}", Constants.ProfilesPath);

                var profiles = await LoadFromPathAsync(Constants.LegacyProfilesPath);

                Log.Information("Loaded {Count} profiles from legacy location", profiles.Count);

                // Save to new location
                await SaveProfilesAsync(profiles);

                Log.Information("Saved profiles to new location");

                // Delete old file after successful migration
                try
                {
                    File.Delete(Constants.LegacyProfilesPath);
                    Log.Information("Successfully migrated and deleted legacy profiles file");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete legacy profiles file at {Path}", Constants.LegacyProfilesPath);
                }

                return profiles;
            }

            // No profiles file found, return empty collection
            Log.Warning("Profiles file does not exist in either location, returning empty collection");
            Log.Warning("Checked paths: New={NewPath}, Legacy={LegacyPath}", Constants.ProfilesPath, Constants.LegacyProfilesPath);
            return new ObservableCollection<Profile>();
        }

        public async Task SaveProfilesAsync(ObservableCollection<Profile> profiles)
        {
            Constants.EnsureAppDataFolderExists();

            Log.Information("Saving {Count} profiles to: {FilePath}", profiles.Count, Constants.ProfilesPath);

            var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            await File.WriteAllTextAsync(Constants.ProfilesPath, json);

            Log.Information("Profiles saved successfully");
        }

        public Profile GetDefaultProfile(ObservableCollection<Profile> profiles)
        {
            if (profiles == null || profiles.Count == 0)
                return null;

            return profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.FirstOrDefault();
        }

        public Profile GetProfileByName(ObservableCollection<Profile> profiles, string profileName)
        {
            if (profiles == null || string.IsNullOrWhiteSpace(profileName))
                return null;

            return profiles.FirstOrDefault(p => p.Name == profileName);
        }

        private async Task<ObservableCollection<Profile>> LoadFromPathAsync(string path)
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);

                // Deserialize as List<Profile> first (matching original implementation)
                var loadedProfiles = JsonConvert.DeserializeObject<List<Profile>>(json);

                if (loadedProfiles == null || loadedProfiles.Count == 0)
                {
                    Log.Warning("No profiles found in {Path}", path);
                    return new ObservableCollection<Profile>();
                }

                // Convert to ObservableCollection
                var profiles = new ObservableCollection<Profile>(loadedProfiles);

                Log.Information("Loaded {Count} profiles from {Path}", profiles.Count, path);
                foreach (var profile in profiles)
                {
                    Log.Information("  - Profile: {ProfileName}, Apps: {AppCount}, IsDefault: {IsDefault}",
                        profile.Name, profile.Apps?.Count ?? 0, profile.IsDefault);
                }

                return profiles;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load profiles from {Path}", path);
                return new ObservableCollection<Profile>();
            }
        }
    }
}
