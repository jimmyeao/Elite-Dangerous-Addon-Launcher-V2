using Elite_Dangerous_Addon_Launcher_V2.Commands;
using Elite_Dangerous_Addon_Launcher_V2.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Elite_Dangerous_Addon_Launcher_V2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IProfileService _profileService;
        private readonly ISettingsService _settingsService;
        private readonly IProcessLaunchService _processLaunchService;

        private Profile _currentProfile;
        private MyApp _selectedApp;
        private string _statusMessage;
        private bool _isLaunchButtonEnabled = true;
        private bool _closeAllAppsOnExit;
        private Settings _settings;
        private bool _isLoading;
        private string _applicationVersion;

        public MainWindowViewModel(
            IProfileService profileService,
            ISettingsService settingsService,
            IProcessLaunchService processLaunchService)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _processLaunchService = processLaunchService ?? throw new ArgumentNullException(nameof(processLaunchService));

            // Initialize commands
            LaunchAllCommand = new AsyncRelayCommand(LaunchAllAppsAsync, () => !IsLoading && CurrentProfile != null);
            LaunchSingleCommand = new AsyncRelayCommand(async (param) => await LaunchSingleAppAsync(param as MyApp), (param) => param is MyApp);
            AddAppCommand = new RelayCommand(AddApp);
            EditAppCommand = new RelayCommand(EditApp, (param) => SelectedApp != null);
            RemoveAppCommand = new AsyncRelayCommand(async (param) => await RemoveAppAsync(param as MyApp), (param) => param is MyApp);
            AddProfileCommand = new AsyncRelayCommand(AddProfileAsync);
            DeleteProfileCommand = new AsyncRelayCommand(DeleteProfileAsync, () => CurrentProfile != null);
            RenameProfileCommand = new AsyncRelayCommand(RenameProfileAsync, () => CurrentProfile != null);
            ExportProfilesCommand = new RelayCommand(ExportProfiles);
            ImportProfilesCommand = new AsyncRelayCommand(ImportProfilesAsync);
            ShowLogsCommand = new RelayCommand(ShowLogs);
            ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);

            // Subscribe to Elite process exit event
            _processLaunchService.AllEliteProcessesExited += OnAllEliteProcessesExited;

            // Set application version
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            ApplicationVersion = $"{version.Major}.{version.Minor}.{version.Build}";

            // Initialize
            _ = InitializeAsync();
        }

        #region Properties

        public ObservableCollection<Profile> Profiles => AppState.Instance.Profiles;

        public Profile CurrentProfile
        {
            get => _currentProfile;
            set
            {
                if (SetProperty(ref _currentProfile, value))
                {
                    AppState.Instance.CurrentProfile = value;
                    OnPropertyChanged(nameof(Apps));
                    OnPropertyChanged(nameof(OtherProfiles));
                    ((IRelayCommand)LaunchAllCommand).RaiseCanExecuteChanged();
                    ((IRelayCommand)DeleteProfileCommand).RaiseCanExecuteChanged();
                    ((IRelayCommand)RenameProfileCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<MyApp> Apps => CurrentProfile?.Apps;

        public IEnumerable<Profile> OtherProfiles
        {
            get
            {
                if (Profiles == null || CurrentProfile == null)
                    return Enumerable.Empty<Profile>();

                return Profiles.Except(new[] { CurrentProfile });
            }
        }

        public MyApp SelectedApp
        {
            get => _selectedApp;
            set
            {
                if (SetProperty(ref _selectedApp, value))
                {
                    AppState.Instance.SelectedApp = value;
                    ((IRelayCommand)EditAppCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLaunchButtonEnabled
        {
            get => _isLaunchButtonEnabled;
            set => SetProperty(ref _isLaunchButtonEnabled, value);
        }

        public bool CloseAllAppsOnExit
        {
            get => _closeAllAppsOnExit;
            set
            {
                if (SetProperty(ref _closeAllAppsOnExit, value))
                {
                    AppState.Instance.CloseAllAppsOnExit = value;
                    if (_settings != null)
                    {
                        _settings.CloseAllAppsOnExit = value;
                        _ = _settingsService.SaveSettingsAsync(_settings);
                    }
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    ((IRelayCommand)LaunchAllCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ApplicationVersion
        {
            get => _applicationVersion;
            set => SetProperty(ref _applicationVersion, value);
        }

        public string CurrentTheme
        {
            get => _settings?.Theme ?? "Light";
        }

        #endregion

        #region Commands

        public ICommand LaunchAllCommand { get; }
        public ICommand LaunchSingleCommand { get; }
        public ICommand AddAppCommand { get; }
        public ICommand EditAppCommand { get; }
        public ICommand RemoveAppCommand { get; }
        public ICommand AddProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand RenameProfileCommand { get; }
        public ICommand ExportProfilesCommand { get; }
        public ICommand ImportProfilesCommand { get; }
        public ICommand ShowLogsCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        #endregion

        #region Methods

        private async Task InitializeAsync()
        {
            IsLoading = true;

            try
            {
                Log.Information("Initializing MainWindowViewModel");

                // Load settings
                _settings = await _settingsService.LoadSettingsAsync();
                CloseAllAppsOnExit = _settings.CloseAllAppsOnExit;

                // Load profiles
                var profiles = await _profileService.LoadProfilesAsync();
                AppState.Instance.Profiles = profiles;
                OnPropertyChanged(nameof(Profiles)); // Notify UI that Profiles property has changed

                // Set current profile
                var profileName = App.ProfileName; // Get from command line args
                if (!string.IsNullOrWhiteSpace(profileName))
                {
                    CurrentProfile = _profileService.GetProfileByName(profiles, profileName);
                }
                else
                {
                    CurrentProfile = _profileService.GetDefaultProfile(profiles);
                }

                Log.Information("Initialization complete. Current profile: {ProfileName}", CurrentProfile?.Name ?? "None");

                // Check if there are no profiles
                if (Profiles == null || Profiles.Count == 0)
                {
                    Log.Information("No profiles found, prompting user to create one");
                    await AddProfileAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during initialization");
                StatusMessage = "Error loading application data";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LaunchAllAppsAsync()
        {
            if (CurrentProfile == null || !CurrentProfile.Apps.Any(a => a.IsEnabled))
                return;

            Log.Information("Launching all enabled apps");
            IsLaunchButtonEnabled = false;
            StatusMessage = "Launching apps...";

            await _processLaunchService.LaunchAllAppsAsync(CurrentProfile.Apps);

            StatusMessage = "All apps launched, waiting for Elite to exit...";
        }

        private async Task LaunchSingleAppAsync(MyApp app)
        {
            if (app == null)
                return;

            Log.Information("Launching single app: {AppName}", app.Name);
            StatusMessage = $"Launching {app.Name}...";

            await _processLaunchService.LaunchAppAsync(app);

            StatusMessage = $"Launched {app.Name}";
        }

        private void AddApp(object parameter)
        {
            Log.Information("AddApp command executed");
            // This will be handled by the View (opening dialog)
            // We'll use a messenger pattern or event aggregator in a more advanced implementation
        }

        private void EditApp(object parameter)
        {
            if (SelectedApp == null)
                return;

            Log.Information("EditApp command executed for: {AppName}", SelectedApp.Name);
            // This will be handled by the View
        }

        private async Task RemoveAppAsync(MyApp app)
        {
            if (app == null || CurrentProfile == null)
                return;

            Log.Information("RemoveApp command executed for: {AppName}", app.Name);

            CurrentProfile.Apps.Remove(app);
            await _profileService.SaveProfilesAsync(Profiles);
        }

        private async Task AddProfileAsync()
        {
            Log.Information("AddProfile command executed");
            // This will be handled by the View to show dialog
            // After profile is added, save
            await _profileService.SaveProfilesAsync(Profiles);
        }

        private async Task DeleteProfileAsync()
        {
            if (CurrentProfile == null)
                return;

            Log.Information("DeleteProfile command executed for: {ProfileName}", CurrentProfile.Name);

            Profiles.Remove(CurrentProfile);
            CurrentProfile = _profileService.GetDefaultProfile(Profiles);

            await _profileService.SaveProfilesAsync(Profiles);
        }

        private async Task RenameProfileAsync()
        {
            if (CurrentProfile == null)
                return;

            Log.Information("RenameProfile command executed for: {ProfileName}", CurrentProfile.Name);
            // This will be handled by the View to show dialog
            await _profileService.SaveProfilesAsync(Profiles);
        }

        private void ExportProfiles(object parameter)
        {
            Log.Information("ExportProfiles command executed");
            // This will be handled by the View
        }

        private async Task ImportProfilesAsync()
        {
            Log.Information("ImportProfiles command executed");
            // This will be handled by the View
            await _profileService.SaveProfilesAsync(Profiles);
        }

        private void ShowLogs(object parameter)
        {
            Log.Information("ShowLogs command executed");
            // This will be handled by the View
        }

        public async Task ToggleThemeAsync()
        {
            if (_settings == null)
                return;

            _settings.Theme = _settings.Theme == "Dark" ? "Light" : "Dark";
            await _settingsService.SaveSettingsAsync(_settings);

            OnPropertyChanged(nameof(CurrentTheme));
            Log.Information("Theme toggled to: {Theme}", _settings.Theme);
            // Theme application will be handled by the View
        }

        private void OnAllEliteProcessesExited(object sender, EventArgs e)
        {
            Log.Information("All Elite processes have exited");

            Application.Current.Dispatcher.Invoke(() =>
            {
                IsLaunchButtonEnabled = true;

                if (CloseAllAppsOnExit)
                {
                    _processLaunchService.CloseAllLaunchedApps();
                    StatusMessage = "Elite closed. All companion apps closed.";
                }
                else
                {
                    StatusMessage = "Elite closed.";
                }
            });
        }

        public async Task SaveProfilesAsync()
        {
            await _profileService.SaveProfilesAsync(Profiles);
        }

        public void Cleanup()
        {
            _processLaunchService.StopMonitoringEliteProcesses();
            _processLaunchService.AllEliteProcessesExited -= OnAllEliteProcessesExited;
        }

        #endregion
    }
}
