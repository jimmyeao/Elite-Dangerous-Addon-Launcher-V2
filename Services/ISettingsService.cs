using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for managing application settings
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Loads settings from disk. Migrates from old location if necessary.
        /// </summary>
        Task<Settings> LoadSettingsAsync();

        /// <summary>
        /// Saves settings to disk
        /// </summary>
        Task SaveSettingsAsync(Settings settings);
    }
}
