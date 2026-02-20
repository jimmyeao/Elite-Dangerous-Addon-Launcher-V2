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
using System.Windows.Threading;

namespace Elite_Dangerous_Addon_Launcher_V2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IProfileService _profileService;
        private readonly ISettingsService _settingsService;
        private readonly IProcessLaunchService _processLaunchService;
        private readonly DispatcherTimer _statusUpdateTimer;

        private Profile _currentProfile;
        private MyApp _selectedApp;
        private string _statusMessage;
        private bool _isLaunchButtonEnabled = true;
        private bool _closeAllAppsOnExit;
        private bool _alsoCloseThisApp;
        private bool _minimizeToTray;
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

            // Subscribe to launch progress updates
            _processLaunchService.LaunchProgress += OnLaunchProgress;

            // Set application version
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            ApplicationVersion = $"{version.Major}.{version.Minor}.{version.Build}";

            // Set up timer to periodically update running status (every 3 seconds)
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _statusUpdateTimer.Tick += (s, e) => UpdateRunningStatus();
            _statusUpdateTimer.Start();

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
                var previous = _currentProfile;
                if (SetProperty(ref _currentProfile, value))
                {
                    // Unsubscribe from previous profile's app events
                    if (previous?.Apps != null)
                    {
                        previous.Apps.CollectionChanged -= OnAppsCollectionChanged;
                        foreach (var app in previous.Apps)
                            app.PropertyChanged -= OnAppPropertyChanged;
                    }

                    AppState.Instance.CurrentProfile = value;
                    OnPropertyChanged(nameof(Apps));
                    OnPropertyChanged(nameof(OtherProfiles));
                    ((IRelayCommand)LaunchAllCommand).RaiseCanExecuteChanged();
                    ((IRelayCommand)DeleteProfileCommand).RaiseCanExecuteChanged();
                    ((IRelayCommand)RenameProfileCommand).RaiseCanExecuteChanged();

                    // Subscribe to new profile's app events so any property change triggers a save
                    if (value?.Apps != null)
                    {
                        value.Apps.CollectionChanged += OnAppsCollectionChanged;
                        foreach (var app in value.Apps)
                            app.PropertyChanged += OnAppPropertyChanged;
                    }

                    // Update running status for apps in the new profile
                    UpdateRunningStatus();
                }
            }
        }

        private void OnAppsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MyApp app in e.NewItems)
                    app.PropertyChanged += OnAppPropertyChanged;

            if (e.OldItems != null)
                foreach (MyApp app in e.OldItems)
                    app.PropertyChanged -= OnAppPropertyChanged;
        }

        private void OnAppPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // IsRunning is a UI-only runtime property and should not trigger a save
            if (e.PropertyName == nameof(MyApp.IsRunning))
                return;

            _ = SaveProfilesAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Log.Error(t.Exception, "Error auto-saving profiles after app property change");
            }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
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

        public bool AlsoCloseThisApp
        {
            get => _alsoCloseThisApp;
            set
            {
                if (SetProperty(ref _alsoCloseThisApp, value))
                {
                    if (_settings != null)
                    {
                        _settings.AlsoCloseThisApp = value;
                        _ = _settingsService.SaveSettingsAsync(_settings);
                    }
                }
            }
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (SetProperty(ref _minimizeToTray, value))
                {
                    if (_settings != null)
                    {
                        _settings.MinimizeToTray = value;
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
                AlsoCloseThisApp = _settings.AlsoCloseThisApp;
                MinimizeToTray = _settings.MinimizeToTray;

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

        public async Task LaunchAllAppsAsync()
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

        private void OnLaunchProgress(object sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = message;
            });
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

                    if (AlsoCloseThisApp)
                    {
                        Log.Information("AlsoCloseThisApp is enabled, closing application...");
                        StatusMessage = "Elite closed. All companion apps closed. Closing launcher...";

                        // Give a brief moment for the status message to display
                        Task.Delay(1000).ContinueWith(_ =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Application.Current.Shutdown();
                            });
                        });
                    }
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

        private void UpdateRunningStatus()
        {
            if (CurrentProfile == null || CurrentProfile.Apps == null)
                return;

            // Get list of currently running process names
            var runningProcesses = System.Diagnostics.Process.GetProcesses()
                .Select(p =>
                {
                    try { return p.ProcessName; }
                    catch { return null; }
                })
                .Where(n => n != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Check if Elite Dangerous is running
            bool eliteIsRunning = runningProcesses.Contains("EDLaunch") ||
                                  runningProcesses.Contains("EliteDangerous64");

            // Update IsRunning for each app based on whether its process is running
            foreach (var app in CurrentProfile.Apps)
            {
                // Special handling for Elite Dangerous apps
                if (IsEliteApp(app))
                {
                    app.IsRunning = eliteIsRunning;
                }
                else if (!string.IsNullOrEmpty(app.ExeName))
                {
                    // Remove .exe extension if present for comparison
                    var processName = app.ExeName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    app.IsRunning = runningProcesses.Contains(processName);
                }
                // Don't change IsRunning if ExeName is empty - leave it as is
            }
        }

        private bool IsEliteApp(MyApp app)
        {
            if (app == null)
                return false;

            // Check if it's a local Elite exe
            if (!string.IsNullOrEmpty(app.ExeName) &&
                (app.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase) ||
                 app.ExeName.Equals("EliteDangerous64.exe", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check if it's a web launcher for Elite
            if (!string.IsNullOrEmpty(app.WebAppURL) &&
                (app.WebAppURL.Contains("rungameid/359320") ||
                 app.WebAppURL.Contains("com.epicgames.launcher://apps") ||
                 app.WebAppURL.Contains("legendary://launch")))
            {
                return true;
            }

            // Check if the name is exactly "Elite Dangerous" or a launcher variant
            if (!string.IsNullOrEmpty(app.Name))
            {
                string name = app.Name.Trim();

                if (name.Equals("Elite Dangerous", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (name.StartsWith("Elite Dangerous (", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(")", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void Cleanup()
        {
            _statusUpdateTimer?.Stop();
            _processLaunchService.StopMonitoringEliteProcesses();
            _processLaunchService.AllEliteProcessesExited -= OnAllEliteProcessesExited;
            _processLaunchService.LaunchProgress -= OnLaunchProgress;
        }

        #endregion
    }
}
