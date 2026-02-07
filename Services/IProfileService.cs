using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for managing profiles
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Loads profiles from disk. Migrates from old location if necessary.
        /// </summary>
        Task<ObservableCollection<Profile>> LoadProfilesAsync();

        /// <summary>
        /// Saves profiles to disk
        /// </summary>
        Task SaveProfilesAsync(ObservableCollection<Profile> profiles);

        /// <summary>
        /// Gets the default profile or the first profile if no default is set
        /// </summary>
        Profile GetDefaultProfile(ObservableCollection<Profile> profiles);

        /// <summary>
        /// Gets a profile by name
        /// </summary>
        Profile GetProfileByName(ObservableCollection<Profile> profiles, string profileName);
    }
}
