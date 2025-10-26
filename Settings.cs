namespace Elite_Dangerous_Addon_Launcher_V2
{
    public class Settings
    {
        #region Public Properties

        public bool CloseAllAppsOnExit { get; set; }
        public bool AlsoCloseThisApp { get; set; }
        public string Theme { get; set; }
        public string EliteInstallType { get; set; } = "Standard"; // Default to Standard if not set

        #endregion Public Properties
    }
}