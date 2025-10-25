using Elite_Dangerous_Addon_Launcher_V2.Commands;
using Serilog;
using System;
using System.Windows.Input;
using Microsoft.Win32;

namespace Elite_Dangerous_Addon_Launcher_V2.ViewModels
{
    public class EliteLauncherDialogViewModel : ViewModelBase
    {
        private EliteLauncherDialog.LauncherType _selectedLauncher;
        private string _manualPath;
        private bool _useAutoRun = true;
        private bool _useAutoQuit = true;
        private bool _useVrMode;
        private bool _isEditing;
        private MyApp _existingApp;
        private string _currentConfigText;
        private bool _showCurrentConfig;
        private bool _showSteamWarning;

        public EliteLauncherDialogViewModel(bool isEditing = false, MyApp existingApp = null, string preferredLauncherType = "Standard")
        {
            _isEditing = isEditing;
            _existingApp = existingApp;

            // Initialize commands
            SelectStandardCommand = new RelayCommand(_ => SelectLauncher(EliteLauncherDialog.LauncherType.Standard));
            SelectSteamCommand = new RelayCommand(_ => SelectLauncher(EliteLauncherDialog.LauncherType.Steam));
            SelectEpicCommand = new RelayCommand(_ => SelectLauncher(EliteLauncherDialog.LauncherType.Epic));
            SelectLegendaryCommand = new RelayCommand(_ => SelectLauncher(EliteLauncherDialog.LauncherType.Legendary));
            BrowseCommand = new RelayCommand(_ => BrowseForManual());

            // Parse preferred launcher type
            if (Enum.TryParse<EliteLauncherDialog.LauncherType>(preferredLauncherType, true, out var preferredType))
            {
                SelectedLauncher = preferredType;
                Log.Debug("Successfully parsed '{PreferredLauncherType}' to {PreferredType}", preferredLauncherType, preferredType);
            }
            else
            {
                SelectedLauncher = EliteLauncherDialog.LauncherType.Standard;
                Log.Debug("Failed to parse '{PreferredLauncherType}', using Standard fallback", preferredLauncherType);
            }

            // If editing, pre-populate with existing app settings
            if (isEditing && existingApp != null)
            {
                Log.Debug("Editing existing app: Name='{Name}', ExeName='{ExeName}', WebAppURL='{WebAppURL}'",
                    existingApp.Name, existingApp.ExeName, existingApp.WebAppURL);

                // Parse arguments
                if (!string.IsNullOrEmpty(existingApp.Args))
                {
                    UseAutoRun = existingApp.Args.Contains("/autorun");
                    UseAutoQuit = existingApp.Args.Contains("/autoquit");
                    UseVrMode = existingApp.Args.Contains("/vr");
                }

                // Detect current launcher type
                var (currentType, displayText) = DetectCurrentLauncherType(existingApp);
                SelectedLauncher = currentType;
                CurrentConfigText = displayText;
                ShowCurrentConfig = true;
            }
        }

        #region Properties

        public EliteLauncherDialog.LauncherType SelectedLauncher
        {
            get => _selectedLauncher;
            set
            {
                if (SetProperty(ref _selectedLauncher, value))
                {
                    ShowSteamWarning = (value == EliteLauncherDialog.LauncherType.Steam);
                }
            }
        }

        public string ManualPath
        {
            get => _manualPath;
            set => SetProperty(ref _manualPath, value);
        }

        public bool UseAutoRun
        {
            get => _useAutoRun;
            set => SetProperty(ref _useAutoRun, value);
        }

        public bool UseAutoQuit
        {
            get => _useAutoQuit;
            set => SetProperty(ref _useAutoQuit, value);
        }

        public bool UseVrMode
        {
            get => _useVrMode;
            set => SetProperty(ref _useVrMode, value);
        }

        public string CurrentConfigText
        {
            get => _currentConfigText;
            set => SetProperty(ref _currentConfigText, value);
        }

        public bool ShowCurrentConfig
        {
            get => _showCurrentConfig;
            set => SetProperty(ref _showCurrentConfig, value);
        }

        public bool ShowSteamWarning
        {
            get => _showSteamWarning;
            set => SetProperty(ref _showSteamWarning, value);
        }

        public string DialogTitle => _isEditing ? "Edit Elite Dangerous" : "Add Elite Dangerous";

        public string MainMessage => _isEditing
            ? "How would you like to launch Elite Dangerous?"
            : "Elite Dangerous was not found in this profile. How would you like to add it?";

        #endregion

        #region Commands

        public ICommand SelectStandardCommand { get; }
        public ICommand SelectSteamCommand { get; }
        public ICommand SelectEpicCommand { get; }
        public ICommand SelectLegendaryCommand { get; }
        public ICommand BrowseCommand { get; }

        #endregion

        #region Methods

        private void SelectLauncher(EliteLauncherDialog.LauncherType type)
        {
            SelectedLauncher = type;
            Log.Information("Selected launcher type: {LauncherType}", type);
        }

        private void BrowseForManual()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Elite Dangerous|edlaunch.exe;EliteDangerous64.exe|All Executables|*.exe",
                Title = "Select Elite Dangerous Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                ManualPath = dialog.FileName;
                SelectedLauncher = EliteLauncherDialog.LauncherType.Manual;
                Log.Information("Manual path selected: {Path}", ManualPath);
            }
        }

        public string GetArgumentsString()
        {
            string args = "";
            if (UseAutoRun) args += " /autorun";
            if (UseAutoQuit) args += " /autoquit";
            if (UseVrMode) args += " /vr";
            return args.Trim();
        }

        private (EliteLauncherDialog.LauncherType type, string displayText) DetectCurrentLauncherType(MyApp app)
        {
            var type = EliteLauncherDialog.LauncherType.Standard;
            string displayText = "Standard Installation";
            string pathInfo = "";

            if (!string.IsNullOrEmpty(app.WebAppURL))
            {
                if (app.WebAppURL.Contains("steam://"))
                {
                    type = EliteLauncherDialog.LauncherType.Steam;
                    displayText = "Steam Version";
                    pathInfo = $"\nURL: {app.WebAppURL}";
                }
                else if (app.WebAppURL.Contains("epic"))
                {
                    type = EliteLauncherDialog.LauncherType.Epic;
                    displayText = "Epic Games Launcher";
                    pathInfo = $"\nURL: {app.WebAppURL}";
                }
                else if (app.WebAppURL.Contains("legendary"))
                {
                    type = EliteLauncherDialog.LauncherType.Legendary;
                    displayText = "Legendary Launcher";
                    pathInfo = $"\nURL: {app.WebAppURL}";
                }
            }
            else if (!string.IsNullOrEmpty(app.ExeName))
            {
                if (app.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase))
                {
                    type = EliteLauncherDialog.LauncherType.Standard;
                    displayText = "Standard Installation";
                    string fullPath = System.IO.Path.Combine(app.Path, app.ExeName);
                    pathInfo = $"\nPath: {fullPath}";
                }
                else
                {
                    type = EliteLauncherDialog.LauncherType.Manual;
                    displayText = $"Manual Path ({app.ExeName})";
                    string fullPath = System.IO.Path.Combine(app.Path, app.ExeName);
                    pathInfo = $"\nPath: {fullPath}";
                    ManualPath = fullPath;
                }
            }

            return (type, $"Currently configured as: {displayText}{pathInfo}");
        }

        #endregion
    }
}
