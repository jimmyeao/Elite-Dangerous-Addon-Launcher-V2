using System;
using System.IO;

namespace Elite_Dangerous_Addon_Launcher_V2
{
    /// <summary>
    /// Application-wide constants and paths
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Application name used for folder creation
        /// </summary>
        public const string ApplicationName = "Elite Dangerous Addon Launcher V2";

        /// <summary>
        /// Application data folder path: %LocalAppData%\Elite Dangerous Addon Launcher V2
        /// </summary>
        public static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationName);

        /// <summary>
        /// Location for profiles.json
        /// </summary>
        public static readonly string ProfilesPath = Path.Combine(AppDataFolder, "profiles.json");

        /// <summary>
        /// Location for settings.json
        /// </summary>
        public static readonly string SettingsPath = Path.Combine(AppDataFolder, "settings.json");

        /// <summary>
        /// Logs folder path
        /// </summary>
        public static readonly string LogsFolder = Path.Combine(AppDataFolder, "logs");

        /// <summary>
        /// Ensures the application data folder exists
        /// </summary>
        public static void EnsureAppDataFolderExists()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            if (!Directory.Exists(LogsFolder))
            {
                Directory.CreateDirectory(LogsFolder);
            }
        }
    }
}
