﻿using GongSolutions.Wpf.DragDrop;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using Serilog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Elite_Dangerous_Addon_Launcher_V2.Services;
using Newtonsoft.Json.Linq;

namespace Elite_Dangerous_Addon_Launcher_V2

{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Private Fields

        public List<string> processList = new List<string>();

        private string _applicationVersion;
        private bool _isChecking = false;
        private string _appVersion;
        private bool _isLoading = true;
        // The row that will be dragged.
        private DataGridRow _rowToDrag;
        private string logpath;
        // Store the position where the mouse button is clicked.
        private Point _startPoint;

        private bool isDarkTheme = false;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private Settings settings;

        #endregion Private Fields

        #region Public Properties

        public string ApplicationVersion
        {
            get { return _appVersion; }
            set
            {
                if (_appVersion != value)
                {
                    _appVersion = value;
                    OnPropertyChanged(nameof(ApplicationVersion));
                }
            }
        }

        #endregion Public Properties

        #region Public Constructors

        public MainWindow(string profileName = null)
        {
            InitializeComponent();
            LoggingConfig.Configure();
            if (!string.IsNullOrEmpty(profileName))
            {
                // Use the profileName to load the appropriate profile
                //LoadProfile(profileName);
            }
            // Copy user settings from previous application version if necessary
            if (Properties.Settings.Default.UpdateSettings)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                Properties.Settings.Default.Save();
            }
            this.SizeToContent = SizeToContent.Manual;
            this.Width = Properties.Settings.Default.MainWindowSize.Width;
            this.Height = Properties.Settings.Default.MainWindowSize.Height;
            this.Top = Properties.Settings.Default.MainWindowLocation.Y;
            this.Left = Properties.Settings.Default.MainWindowLocation.X;
            // Assign the event handler to the Loaded event
            this.Loaded += MainWindow_Loaded;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            ApplicationVersion = $"{version.Major}.{version.Minor}.{version.Build}"; // format as desired
            // Set the data context to AppState instance
            this.DataContext = AppState.Instance;
            CloseAllAppsCheckbox.IsChecked = Properties.Settings.Default.CloseAllAppsOnExit;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Properties.Settings.Default.MainWindowSize = new System.Drawing.Size((int)this.Width, (int)this.Height);
            Properties.Settings.Default.MainWindowLocation = new System.Drawing.Point((int)this.Left, (int)this.Top);
            Properties.Settings.Default.Save();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (Properties.Settings.Default.MainWindowLocation == new System.Drawing.Point(0, 0))
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        #endregion Public Constructors

        #region Public Methods

        public event PropertyChangedEventHandler PropertyChanged;

        public Profile CurrentProfile { get; set; }

        public List<Profile> OtherProfiles
        {
            get
            {
                if (Profiles == null || CurrentProfile == null)
                {
                    return new List<Profile>();
                }

                var otherProfiles = Profiles.Except(new List<Profile> { CurrentProfile }).ToList();
                Debug.WriteLine($"OtherProfiles: {string.Join(", ", otherProfiles.Select(p => p.Name))}");
                return otherProfiles;
            }
        }

        public List<Profile> Profiles { get; set; }

        public void DragOver(IDropInfo dropInfo)
        {
            MyApp sourceItem = dropInfo.Data as MyApp;
            MyApp targetItem = dropInfo.TargetItem as MyApp;

            if (sourceItem != null && targetItem != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
              
                SaveProfilesAsync();
            }
        }

        public async Task LoadProfilesAsync(string profileName = null)
        {
            try
            {
                string localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string filePath = Path.Combine(localFolder, "profiles.json");

                if (File.Exists(filePath))
                {
                    // Read the file to a string
                    string json = await File.ReadAllTextAsync(filePath);

                    // Deserialize the JSON string to a list of profiles
                    List<Profile> loadedProfiles = JsonConvert.DeserializeObject<List<Profile>>(json);

                    // Convert list to ObservableCollection
                    var profiles = new ObservableCollection<Profile>(loadedProfiles);

                    // Set the loaded profiles to AppState.Instance.Profiles
                    AppState.Instance.Profiles = profiles;

                    if (profileName != null)
                    {
                        // If profileName argument is provided, select that profile
                        AppState.Instance.CurrentProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.Name == profileName);
                        Cb_Profiles.SelectedItem = AppState.Instance.Profiles.FirstOrDefault(p => p.Name == profileName);
                    }
                    else
                    {
                        // Set the CurrentProfile to the default profile (or first one if no default exists)
                        AppState.Instance.CurrentProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault)
                            ?? AppState.Instance.Profiles.FirstOrDefault();
                        Cb_Profiles.SelectedItem = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);
                    }
                    SubscribeToAppEvents(AppState.Instance.CurrentProfile);
                }
                else
                {
                    // The profiles file doesn't exist, initialize Profiles with an empty collection
                    AppState.Instance.Profiles = new ObservableCollection<Profile>();
                }
            }
            catch (Exception ex)
            {
                // Handle other exceptions
            }
        }

        public async Task SaveProfilesAsync()
        {
            // Serialize the profiles into a JSON string
            var profilesJson = JsonConvert.SerializeObject(AppState.Instance.Profiles);

            // Create a file called "profiles.json" in the local folder
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "profiles.json");

            // Write the JSON string to the file
            await File.WriteAllTextAsync(path, profilesJson);
            AdjustWindowSizeToContent();
        }

        public void UpdateDataGrid()
        {
            // Set DataGrid's ItemsSource to the apps of the currently selected profile
            if (AppState.Instance.CurrentProfile != null)
            {
                AddonDataGrid.ItemsSource = AppState.Instance.CurrentProfile.Apps;
            }
            else
            {
                // No profile is selected. Clear the data grid.
                AddonDataGrid.ItemsSource = null;
            }

            // Resize the window to fit content
            AdjustWindowSizeToContent();
        }
        private void AdjustWindowSizeToContent()
        {
            // Give the UI time to update
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Calculate the required height based on the number of items in the grid
                    double headerHeight = 150;  // Space for controls at top
                    double footerHeight = 50;   // Increased padding at bottom for less cramped look
                    double rowHeight = 48;      // Approximate height of each row

                    int itemCount = 0;
                    if (AppState.Instance.CurrentProfile?.Apps != null)
                    {
                        itemCount = AppState.Instance.CurrentProfile.Apps.Count;
                    }

                    // Calculate the required window height
                    double requiredHeight = headerHeight + (itemCount * rowHeight) + footerHeight;

                    // Set minimum height
                    double minHeight = 300;
                    requiredHeight = Math.Max(requiredHeight, minHeight);

                    // Set maximum height to avoid excessively tall windows
                    double maxHeight = 800;
                    requiredHeight = Math.Min(requiredHeight, maxHeight);

                    // Set the window height directly
                    this.Height = requiredHeight;

                    // Ensure window is within screen bounds
                    if (this.Top + this.Height > SystemParameters.VirtualScreenHeight)
                    {
                        this.Top = Math.Max(0, SystemParameters.VirtualScreenHeight - this.Height);
                    }

                    // Force layout update
                    this.UpdateLayout();

                    Log.Information($"Window resized to height: {this.Height} for {itemCount} items");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error adjusting window size");
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Public Methods

        #region Private Methods

        private void AddonDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Write the MyApp instances to the data source here, e.g.: SaveAppStateToFile();
        }

        private void ApplyTheme(string themeName)
        {
            var darkThemeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml");
            var lightThemeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml");

            var themeUri = themeName == "Dark" ? darkThemeUri : lightThemeUri;

            var existingTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source == themeUri);
            if (existingTheme == null)
            {
                existingTheme = new ResourceDictionary() { Source = themeUri };
                Application.Current.Resources.MergedDictionaries.Add(existingTheme);
            }

            // Remove the current theme
            var currentTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source == (themeName == "Dark" ? lightThemeUri : darkThemeUri));
            if (currentTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(currentTheme);
            }
        }

        private void Bt_AddApp_Click_1(object sender, RoutedEventArgs e)
        {
            if (AppState.Instance.CurrentProfile != null)
            {
                AddApp addAppWindow = new AddApp()
                {
                    MainPageReference = this,
                    SelectedProfile = AppState.Instance.CurrentProfile,
                };
                // Set the owner and startup location
                addAppWindow.Owner = this; // Or replace 'this' with reference to the main window
                addAppWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                addAppWindow.Show();
                AddonDataGrid.ItemsSource = AppState.Instance.CurrentProfile.Apps;
            }
            else
            {
                // Handle the case when no profile is selected.
            }
        }

        private void Bt_AddProfile_Click_1(object sender, RoutedEventArgs e)
        {
            var window = new AddProfileDialog();
            // Center the dialog within the owner window
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Owner = this;  // Or replace 'this' with reference to the main window

            if (window.ShowDialog() == true)
            {
                string profileName = window.ProfileName;
                var newProfile = new Profile { Name = profileName };
                AppState.Instance.Profiles.Add(newProfile);

                AppState.Instance.CurrentProfile = newProfile;

                _ = SaveProfilesAsync();
                UpdateDataGrid();
            }
        }

        private async void Bt_RemoveProfile_Click_1(object sender, RoutedEventArgs e)
        {
            Profile profileToRemove = (Profile)Cb_Profiles.SelectedItem;

            if (profileToRemove != null)
            {
                CustomDialog dialog = new CustomDialog("Are you sure you want to delete this profile?");
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();

                if (dialog.Result == MessageBoxResult.Yes)
                {
                    AppState.Instance.Profiles.Remove(profileToRemove);

                    // Check if another profile can be selected
                    if (AppState.Instance.Profiles.Any())
                    {
                        // Select the next profile, or the first one if no next profile exists
                        AppState.Instance.CurrentProfile = AppState.Instance.Profiles.FirstOrDefault(p => p != profileToRemove) ?? AppState.Instance.Profiles.First();

                        // If no profile is set as default, set the current profile as the default
                        if (!AppState.Instance.Profiles.Any(p => p.IsDefault))
                        {
                            AppState.Instance.CurrentProfile.IsDefault = true;
                            DefaultCheckBox.IsChecked = true;
                        }
                    }
                    else
                    {
                        // No profiles left, so set CurrentProfile to null
                        AppState.Instance.CurrentProfile = null;
                    }

                    _ = SaveProfilesAsync();
                    UpdateDataGrid();
                }
            }
        }

        private void Btn_Edit_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            MyApp appToEdit = button.CommandParameter as MyApp;

            if (appToEdit != null)
            {
                string exePath = Path.Combine(appToEdit.Path, appToEdit.ExeName);

                if (appToEdit.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase)
                    && IsEpicInstalled(exePath))
                {
                    // Open Legendary settings window instead of AddApp
                    var legendarySettings = new Views.LegendarySettingsWindow
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    legendarySettings.ShowDialog();
                    return;
                }

                // Fallback: regular AddApp editing
                AddApp addAppWindow = new AddApp
                {
                    AppToEdit = appToEdit,
                    MainPageReference = this
                };

                addAppWindow.Owner = this;
                addAppWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                addAppWindow.Title = "Edit App";
                addAppWindow.ShowDialog();
            }
        }

        public void ShowWhatsNewIfUpdated()
        {
            // Get the current assembly version.
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            // Get the last seen version from the application settings.
            var lastSeenVersion = Properties.Settings.Default.LastSeenVersion;

            // If the last seen version is empty (which it will be the first time this method is run)
            // or if the assembly version is greater than the last seen version, show the what's new dialog.
            if (string.IsNullOrEmpty(lastSeenVersion) || new Version(lastSeenVersion) < assemblyVersion)
            {
                ShowWhatsNew();

                // Update the last seen version in the application settings.
                Properties.Settings.Default.LastSeenVersion = assemblyVersion.ToString();

                // Save the application settings.
                Properties.Settings.Default.Save();
            }
        }

        public void ShowWhatsNew()
        {
            WhatsNewWindow whatsNewWindow = new WhatsNewWindow();

            // Set the text to what's new
            Paragraph titleParagraph = new Paragraph();
            titleParagraph.Inlines.Add(new Bold(new Run("New for this version")));
            whatsNewWindow.WhatsNewText.Document.Blocks.Add(titleParagraph);

            List list = new List();

            ListItem listItem1 = new ListItem(new Paragraph(new Run("Fixed bug with renaming profiles causing a crash")));
            list.ListItems.Add(listItem1);

          //  ListItem listItem2 = new ListItem(new Paragraph(new Run("Profile Options for import/export and copy/rename/delete")));
          //  list.ListItems.Add(listItem2);


            whatsNewWindow.WhatsNewText.Document.Blocks.Add(list);

            whatsNewWindow.Owner = this; // Set owner to this MainWindow
            whatsNewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Center the window over its owner

            whatsNewWindow.ShowDialog();
        }








        private void Btn_Launch_Click(object sender, RoutedEventArgs e)
        {
            // Code to launch all enabled apps
            Log.Information("Launching all enabled apps..");
            foreach (var app in AppState.Instance.CurrentProfile.Apps)
            {
                if (app.IsEnabled)
                {
                    Btn_Launch.IsEnabled = false;
                    LaunchApp(app);
                    Log.Information("Launching {AppName}..", app.Name);
                }
            }
        }

        private void Cb_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Cb_Profiles.SelectedItem is Profile selectedProfile)
            {
                // Select the currently selected profile
                AppState.Instance.CurrentProfile = selectedProfile;

                if (AppState.Instance.CurrentProfile != null)
                {
                    UpdateDataGrid();
                    DefaultCheckBox.IsChecked = selectedProfile.IsDefault;
                    if (!_isLoading)
                    {
                        _isChecking = true;
                        CheckEdLaunchInProfile();
                        _isChecking = false;
                    }
                }
            }
        }


        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)  // this is the checkbox fo r the defaul profile
        {
            var checkBox = (CheckBox)sender;
            var app = (MyApp)checkBox.Tag;
            if (app != null)
            {
                app.IsEnabled = false;
                _ = SaveProfilesAsync();
            }
        }

        private async void CloseAllAppsCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            AppState.Instance.CloseAllAppsOnExit = true;
            settings.CloseAllAppsOnExit = true;
            await SaveSettingsAsync(settings);
        }

        private async void CloseAllAppsCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppState.Instance.CloseAllAppsOnExit = false;
            settings.CloseAllAppsOnExit = false;
            await SaveSettingsAsync(settings);
        }

        private void CopyToProfileSubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the clicked MenuItem
            var menuItem = (MenuItem)sender;

            // Get the bounded MyApp item
            var boundedApp = (MyApp)((MenuItem)e.OriginalSource).DataContext;

            // Get the selected profile
            var selectedProfile = (Profile)menuItem.Tag;

            // Now you can copy boundedApp to selectedProfile.
        }

        private void DefaultCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (Cb_Profiles.SelectedItem is Profile selectedProfile)
            {
                var previouslyDefaultProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);
                if (previouslyDefaultProfile != null)
                {
                    previouslyDefaultProfile.IsDefault = false;
                }
                selectedProfile.IsDefault = true;
                _ = SaveProfilesAsync();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // get the button that raised the event
            var button = (Button)sender;
            CustomDialog dialog = new CustomDialog("Are you sure?");
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ShowDialog();

            if (dialog.Result == MessageBoxResult.Yes)
            {
                // retrieve the item associated with this button
                var appToDelete = (MyApp)button.DataContext;

                // remove the item from the collection
                try
                {
                    AppState.Instance.CurrentProfile.Apps.Remove(appToDelete);
                    Log.Information($"App {appToDelete.Name} deleted..", appToDelete.Name);
                }

                catch (Exception ex)
                {
                    // handle exception
                    Log.Error(ex, "An error occurred trying to delete an app..");
                }
                _ = SaveProfilesAsync();
            }
        }

        private void LaunchApp(MyApp app)
        {
            string args;
            const string quote = "\"";
            var path = $"{app.Path}/{app.ExeName}";

            if (string.Equals(app.ExeName, "targetgui.exe", StringComparison.OrdinalIgnoreCase))
            {
                args = "-r " + quote + app.Args + quote;
            }
            else
            {
                args = app.Args;
            }
            // Handle .appref-ms ClickOnce apps
            if (app.ExeName.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase))
            {
                var shortcutPath = Path.Combine(app.Path, app.ExeName);

                if (File.Exists(shortcutPath))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = shortcutPath,
                            UseShellExecute = true
                        };

                        Process.Start(psi);

                        // Attempt to track the actual ClickOnce process by expected name
                        string expectedExeName = Path.GetFileNameWithoutExtension(app.ExeName) + ".exe";
                        processList.Add(expectedExeName); // Add to list for shutdown tracking

                        UpdateStatus($"Launching {app.Name} via .appref-ms shortcut...");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Failed to launch {app.Name} via .appref-ms: {ex.Message}");
                    }
                }
                else
                {
                    UpdateStatus($"Shortcut not found: {shortcutPath}");
                }

                this.WindowState = WindowState.Minimized;
                return;
            }


            if (File.Exists(path))
            {
                try
                {
                    if (string.Equals(app.ExeName, "edlaunch.exe", StringComparison.OrdinalIgnoreCase) &&
                        IsEpicInstalled(path))
                    {
                        if (!IsLegendaryInstalled())
                        {
                            MessageBox.Show("Elite Dangerous is installed via Epic Games, but 'legendary' is not found in PATH.\nPlease install it from https://github.com/derrod/legendary to enable Epic support.", "Legendary Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        LegendaryConfigManager.EnsureLegendaryConfig();

                        var psi = new ProcessStartInfo
                        {
                            FileName = "legendary",
                            Arguments = "launch elite",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        var legendaryProc = Process.Start(psi);
                        string output = legendaryProc.StandardOutput.ReadToEnd();
                        string error = legendaryProc.StandardError.ReadToEnd();
                        Debug.WriteLine("Legendary output:\n" + output);
                        Debug.WriteLine("Legendary errors:\n" + error);

                        // Start a watcher for EDLaunch process
                        Task.Run(() =>
                        {
                            const int maxRetries = 20;
                            int retries = 0;

                            while (retries < maxRetries)
                            {
                                var edProc = Process.GetProcessesByName("EDLaunch").FirstOrDefault();
                                if (edProc != null)
                                {
                                    edProc.EnableRaisingEvents = true;
                                    edProc.Exited += (s, e) =>
                                    {
                                        Application.Current.Dispatcher.Invoke(() => ProcessExitHandler(s, e));
                                    };

                                    break;
                                }

                                Thread.Sleep(500);
                                retries++;
                            }
                        });

                        UpdateStatus($"Launching {app.Name} (Epic) via Legendary...");
                        this.WindowState = WindowState.Minimized;
                        return;
                    }

                    // Default (non-Epic) launch
                    var info = new ProcessStartInfo(path)
                    {
                        Arguments = args,
                        UseShellExecute = true,
                        WorkingDirectory = app.Path
                    };

                    Process proc = Process.Start(info);
                    proc.EnableRaisingEvents = true;
                    processList.Add(proc.ProcessName);

                    if (proc.ProcessName.Equals("EDLaunch", StringComparison.OrdinalIgnoreCase))
                    {
                        proc.Exited += new EventHandler(ProcessExitHandler);
                    }

                    Thread.Sleep(50);
                    proc.Refresh();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"An error occurred trying to launch {app.Name}: {ex.Message}");
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(app.WebAppURL))
                {
                    string target = app.WebAppURL;
                    Process proc = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

                    if (target.Equals("steam://rungameid/359320", StringComparison.OrdinalIgnoreCase))
                    {
                        Thread.Sleep(2000);
                        var edLaunchProc = Process.GetProcessesByName("EDLaunch").FirstOrDefault();
                        if (edLaunchProc != null)
                        {
                            edLaunchProc.EnableRaisingEvents = true;
                            edLaunchProc.Exited += new EventHandler(ProcessExitHandler);
                        }
                    }

                    UpdateStatus("Launching " + app.Name);
                }
                else
                {
                    UpdateStatus($"Unable to launch {app.Name}..");
                }
            }

            UpdateStatus("All apps launched, waiting for EDLaunch Exit..");
            this.WindowState = WindowState.Minimized;
        }
     

        private bool IsLegendaryInstalled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "legendary",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(2000);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<Settings> LoadSettingsAsync()
        {
            string localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFilePath = Path.Combine(localFolder, "settings.json");

            Settings settings;
            if (File.Exists(settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(settingsFilePath);
                settings = JsonConvert.DeserializeObject<Settings>(json);
            }
            else
            {
                // If the settings file doesn't exist, use defaults
                settings = new Settings { Theme = "Default" };
            }
            Log.Information("Settings loaded: {Settings}", settings);
            return settings;
        }
        private void Btn_LaunchSingle_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            MyApp appToLaunch = button.CommandParameter as MyApp;

            if (appToLaunch != null)
            {
                // Disable the main launch button while an app is being launched
                Btn_Launch.IsEnabled = false;

                // Launch just this one app
                LaunchApp(appToLaunch);
                Log.Information("Launching single app: {AppName}", appToLaunch.Name);

                // If this is not Elite Dangerous (which would keep the button disabled),
                // re-enable the launch button
                if (!appToLaunch.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase) &&
                    !appToLaunch.WebAppURL?.Contains("rungameid/359320") == true &&
                    !appToLaunch.WebAppURL?.Contains("epic://launch") == true &&
                    !appToLaunch.WebAppURL?.Contains("legendary://launch") == true)
                {
                    Btn_Launch.IsEnabled = true;
                }
            }
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            await LoadProfilesAsync(App.ProfileName);
            settings = await LoadSettingsAsync();
            isDarkTheme = settings.Theme == "Dark";
            ApplyTheme(settings.Theme);
            AppState.Instance.CloseAllAppsOnExit = settings.CloseAllAppsOnExit;

            // Check if there are no profiles and invoke AddProfileDialog if none exist
            if (AppState.Instance.Profiles == null || AppState.Instance.Profiles.Count == 0)
            {
                var window = new AddProfileDialog();
                // Center the dialog within the owner window
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Owner = this;  // Or replace 'this' with reference to the main window

                if (window.ShowDialog() == true)
                {
                    string profileName = window.ProfileName;
                    var newProfile = new Profile { Name = profileName, IsDefault = true };

                    // Unmark any existing default profiles
                    foreach (var profile in AppState.Instance.Profiles)
                    {
                        profile.IsDefault = false;
                    }

                    AppState.Instance.Profiles.Add(newProfile);
                    AppState.Instance.CurrentProfile = newProfile;

                    await SaveProfilesAsync();
                    UpdateDataGrid();
                }
            }

            if (App.AutoLaunch)
            {
                foreach (var app in AppState.Instance.CurrentProfile.Apps)
                {
                    if (app.IsEnabled)
                    {
                        LaunchApp(app);
                    }
                }
            }
            _isLoading = false;
            if (_isChecking == false)
            {
                _isChecking = true;
                CheckEdLaunchInProfile();
                _isChecking = false;
            }
            ShowWhatsNewIfUpdated();
        }



        private void ModifyTheme(Uri newThemeUri)
        {
            var appResources = Application.Current.Resources;
            var oldTheme = appResources.MergedDictionaries.FirstOrDefault(d => d.Source.ToString().Contains("MaterialDesignTheme.Light.xaml") || d.Source.ToString().Contains("MaterialDesignTheme.Dark.xaml"));

            if (oldTheme != null)
            {
                appResources.MergedDictionaries.Remove(oldTheme);
            }

            appResources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = newThemeUri });
        }

        private void MyApp_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MyApp.IsEnabled) || e.PropertyName == nameof(MyApp.Order))
            {
                _ = SaveProfilesAsync();
            }
        }

        // maybe redundant now
        private void OnProfileChanged(Profile oldProfile, Profile newProfile)
        {
            UnsubscribeFromAppEvents(oldProfile);
            SubscribeToAppEvents(newProfile);
        }

        private void ProcessExitHandler(object sender, EventArgs e)  //triggered when EDLaunch exits
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Btn_Launch.IsEnabled = true;
                bool closeAllApps = CloseAllAppsCheckbox.IsChecked == true;

                // if EDLaunch has quit, does the user want us to kill all the apps?
                if (closeAllApps)
                {
                    Log.Information("CloseAllAppsOnExit is enabled, closing all apps..");
                    try
                    {
                        foreach (string p in processList)
                        {
                            Log.Information("Closing {0}", p);
                            foreach (string process in processList)
                            {
                                Log.Information("Closing {0}", p);
                                foreach (Process proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(p)))
                                {
                                    try
                                    {
                                        proc.CloseMainWindow();
                                        proc.WaitForExit(5000); // give it time to exit
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning("Failed to close process {0}: {1}", p, ex.Message);
                                    }
                                }
                            }

                        }
                    }
                    catch
                    {
                        // if something went wrong, don't raise an exception
                        Log.Error("An error occurred trying to close all apps..");
                    }
                    // doesn't seem to want to kill VoiceAttack nicely..
                    try
                    {
                        Process[] procs = Process.GetProcessesByName("VoiceAttack");
                        foreach (var proc in procs) { proc.Kill(); }        //sadly this means next time it starts, it will complain it was shutdown in an unclean fashion
                    }
                    catch
                    {
                        // if something went wrong, don't raise an exception
                    }
                    // Elite Dangerous Odyssey Materials Helper is a little strange, let's deal with its
                    // multiple running processes..
                    try
                    {
                        Process[] procs = Process.GetProcessesByName("Elite Dangerous Odyssey Materials Helper");
                        foreach (var proc in procs) { proc.CloseMainWindow(); }
                    }
                    catch
                    {
                        // if something went wrong, don't raise an exception
                    }
                    // sleep for 5 seconds then quit
                    for (int i = 5; i != 0; i--)
                    {
                        Thread.Sleep(1000);
                    }
                    Environment.Exit(0);
                }
            });
        }


        private async Task SaveSettingsAsync(Settings settings)
        {
            string localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFilePath = Path.Combine(localFolder, "settings.json");

            string json = JsonConvert.SerializeObject(settings);
            await File.WriteAllTextAsync(settingsFilePath, json);
        }

        private void SubscribeToAppEvents(Profile profile)
        {
            if (profile != null)
            {
                foreach (var app in profile.Apps)
                {
                    app.PropertyChanged += MyApp_PropertyChanged;
                }
            }
        }

        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            isDarkTheme = !isDarkTheme;

            settings.Theme = isDarkTheme ? "Dark" : "Light"; // <-- Add this
            _ = SaveSettingsAsync(settings);

            var darkThemeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml");
            var lightThemeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml");

            var themeUri = isDarkTheme ? darkThemeUri : lightThemeUri;

            System.Diagnostics.Debug.WriteLine("Before toggle:");
            foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
            {
                System.Diagnostics.Debug.WriteLine($" - {dictionary.Source}");
            }

            var existingTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source == themeUri);
            if (existingTheme == null)
            {
                existingTheme = new ResourceDictionary() { Source = themeUri };
                Application.Current.Resources.MergedDictionaries.Add(existingTheme);
            }

            // Remove the current theme
            var currentTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source == (isDarkTheme ? lightThemeUri : darkThemeUri));
            if (currentTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(currentTheme);
            }

            System.Diagnostics.Debug.WriteLine("After toggle:");
            foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
            {
                System.Diagnostics.Debug.WriteLine($" - {dictionary.Source}");
            }
            _ = SaveSettingsAsync(settings);
        }

        private void UnsubscribeFromAppEvents(Profile profile)
        {
            if (profile != null)
            {
                foreach (var app in profile.Apps)
                {
                    app.PropertyChanged -= MyApp_PropertyChanged;
                }
            }
        }

        private void UpdateStatus(string status)
        {
            // Define how you update the status in your application
        }

        #endregion Private Methods

        public static async Task<List<string>> ScanComputerForEdLaunch()
        {
            List<string> foundPaths = new List<string>();
            string targetFolder = "Elite Dangerous";
            string targetFile = "edlaunch.exe";
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            SearchProgressWindow progressWindow = new SearchProgressWindow();

            progressWindow.Owner = Application.Current.MainWindow;
            progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            progressWindow.Closing += (s, e) => tokenSource.Cancel();

            progressWindow.Show(); // Show the window before starting the task

            await Task.Run(() =>
            {
                try
                {
                    foreach (DriveInfo drive in DriveInfo.GetDrives())
                    {
                        if (token.IsCancellationRequested)
                            break;

                        if (drive.DriveType == DriveType.Fixed)
                        {
                            try
                            {
                                string driveRoot = drive.RootDirectory.ToString();
                                if (TraverseDirectories(driveRoot, targetFolder, targetFile, foundPaths, progressWindow, 7, token))
                                {
                                    break;
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                // If we don't have access to the directory, skip it
                            }
                            catch (IOException)
                            {
                                // If another error occurs, skip it
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // If operation is canceled, return
                    return;
                }
            }, token);

            if (progressWindow.IsVisible)
                progressWindow.Close();

            return foundPaths;
        }

        public static bool TraverseDirectories(string root, string targetFolder, string targetFile, List<string> foundPaths, SearchProgressWindow window, int maxDepth, CancellationToken token, int currentDepth = 0)
        {
            // Array of directories to exclude
            string[] excludeDirs = {
                "windows",
                "users",
                "OneDriveTemp",
                "ProgramData",
                "$Recycle.Bin",
                "OneDrive"
            };

            if (token.IsCancellationRequested)
                return false;

            // Make sure not to exceed maximum depth
            if (currentDepth > maxDepth) return false;

            foreach (string dir in Directory.GetDirectories(root))
            {
                // Check for excluded directories
                bool isExcluded = false;
                foreach (string excludeDir in excludeDirs)
                {
                    if (dir.ToLower().Contains(excludeDir.ToLower()))
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (isExcluded || token.IsCancellationRequested)
                {
                    continue;
                }

                try
                {
                    string dirName = new DirectoryInfo(dir).Name;

                    if (dirName.Equals(targetFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        // The folder has the name we're looking for, now we just need to check if
                        // the file is there
                        foreach (string file in Directory.GetFiles(dir))
                        {
                            if (Path.GetFileName(file).Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                            {
                                foundPaths.Add(file);
                                return true; // File has been found
                            }
                        }
                    }

                    // Trim the path for display in the UI
                    string trimmedPath = dir;
                    if (dir.Count(f => f == '\\') > 2)
                    {
                        var parts = dir.Split('\\');
                        trimmedPath = string.Join("\\", parts.Take(3)) + "\\...";
                    }

                    window.Dispatcher.Invoke(() =>
                    {
                        window.searchStatusTextBlock.Text = $"Checking: {trimmedPath}";
                    });

                    // Move on to the next level
                    bool found = TraverseDirectories(dir, targetFolder, targetFile, foundPaths, window, maxDepth, token, currentDepth + 1);

                    if (found)
                    {
                        return true; // File has been found in a subdirectory, so we stop the search
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // If we don't have access to the directory, skip it
                }
                catch (IOException)
                {
                    // If another error occurs, skip it
                }
            }

            return false; // If we get to this point, we haven't found the file
        }

        private static string ShortenPath(string fullPath, int maxParts)
        {
            var parts = fullPath.Split(Path.DirectorySeparatorChar);
            return string.Join(Path.DirectorySeparatorChar, parts.Take(maxParts));
        }

        private void AddonDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var dataGrid = (DataGrid)sender;
            var originalSource = (DependencyObject)e.OriginalSource;

            // Find the DataGridRow in the visual tree
            while ((originalSource != null) && !(originalSource is DataGridRow))
            {
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }

            // If we found a DataGridRow
            if (originalSource is DataGridRow dataGridRow)
            {
                // Get the MyApp instance from the row
                var app = (MyApp)dataGridRow.DataContext;
                AppState.Instance.CurrentApp = app;
            }
        }

        private void Bt_CopyProfile_Click(object sender, RoutedEventArgs e)
        {
            var currentProfile = AppState.Instance.CurrentProfile;

            if (currentProfile != null)
            {
                bool isUnique = false;
                string newName = currentProfile.Name;

                while (!isUnique)
                {
                    var dialog = new RenameProfileDialog(newName);
                    dialog.Title = "Copy Profile";
                    // Center the dialog within the owner window
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    dialog.Owner = this;  // Or replace 'this' with reference to the main window

                    if (dialog.ShowDialog() == true)
                    {
                        newName = dialog.NewName;

                        // Check if the profile name is unique
                        if (!AppState.Instance.Profiles.Any(p => p.Name == newName))
                        {
                            // Change profile name
                            var newProfile = new Profile
                            {
                                Name = newName,
                                Apps = new ObservableCollection<MyApp>(currentProfile.Apps.Select(app => app.DeepCopy()))
                            };
                            AppState.Instance.Profiles.Add(newProfile);
                            AppState.Instance.CurrentProfile = newProfile; // switch to the new profile
                            _ = SaveProfilesAsync();
                            Cb_Profiles.SelectedItem = newProfile; // update combobox selected item
                            isUnique = true;
                        }
                        else
                        {
                            ErrorDialog errdialog = new ErrorDialog("Profile name must be unique. Please enter a different name.");
                            errdialog.Owner = Application.Current.MainWindow;
                            errdialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            errdialog.ShowDialog();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void Bt_RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            var currentProfile = AppState.Instance.CurrentProfile;

            if (currentProfile != null)
            {
                // Show rename dialog and get result
                var dialog = new RenameProfileDialog(currentProfile.Name);
                // Center the dialog within the owner window
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = this;  // Or replace 'this' with reference to the main window

                if (dialog.ShowDialog() == true)
                {
                    // Change profile name
                    currentProfile.Name = dialog.NewName;
                    // need to save the settings
                    _ = SaveProfilesAsync();
                }
            }
        }

        private async void CheckEdLaunchInProfile()
        {
            // Get the current profile
            var currentProfile = AppState.Instance.CurrentProfile;
            if (currentProfile == null)
            {
                return;
            }
            // Check if edlaunch.exe exists in the current profile
            if (!currentProfile.Apps.Any(a => a.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase)
                                || a.WebAppURL?.Equals("steam://rungameid/359320", StringComparison.OrdinalIgnoreCase) == true))
            {
                // edlaunch.exe does not exist in the current profile Prompt the user with a dialog
                // offering to scan their computer for it
                CustomDialog dialog = new CustomDialog("Elite Dangerous does not exist in the current profile. Would you like to scan your computer for it?");
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();

                // If the user clicked Yes, call the method that scans the computer for edlaunch.exe
                if (dialog.Result == MessageBoxResult.Yes)
                {
                    var EdLuanchPaths = await ScanComputerForEdLaunch(); // Note the "await" keyword here
                    if (EdLuanchPaths.Count > 0)
                    {
                        // Add edlaunch.exe to the current profile
                        var edlaunch = new MyApp
                        {
                            Name = "Elite Dangerous",
                            ExeName = "edlaunch.exe",
                            Path = Path.GetDirectoryName(EdLuanchPaths[0]),
                            IsEnabled = true,
                            Order = 0
                        };
                        currentProfile.Apps.Add(edlaunch);
                        await SaveProfilesAsync(); // You can also "await" here since SaveProfilesAsync is probably asynchronous
                    }
                    else
                    {
                        MessageBox.Show(
                            "edlaunch.exe was not found on your computer. Please add it manually.",
                            "edlaunch.exe Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }
            }
        }

        private void CloseAllAppsCheckbox_Checked_1(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.CloseAllAppsOnExit = true;
            Properties.Settings.Default.Save();
        }

        private void CloseAllAppsCheckbox_Unchecked_1(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.CloseAllAppsOnExit = false;
            Properties.Settings.Default.Save();
        }

        private DataGridRow GetDataGridRow(MenuItem menuItem)
        {
            DependencyObject obj = menuItem;
            while (obj != null && obj.GetType() != typeof(DataGridRow))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }
            return obj as DataGridRow;
        }

        private void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)e.OriginalSource;
            var profile = (Profile)menuItem.DataContext;
            var app = AppState.Instance.CurrentApp;
            Debug.WriteLine($"Clicked on profile: {profile.Name}");

            // Now you can use profile and app. Make sure to create a copy of app, not to use the
            // same instance in multiple profiles.
            if (app != null)
            {
                Debug.WriteLine($"Clicked on app: {app.Name}");
                var appCopy = new MyApp
                {
                    Args = app.Args,
                    ExeName = app.ExeName,
                    InstallationURL = app.InstallationURL,
                    IsEnabled = app.IsEnabled,
                    Name = app.Name,
                    Order = app.Order,
                    Path = app.Path,
                    WebAppURL = app.WebAppURL
                };

                // Add the app to the profile
                profile.Apps.Add(appCopy);
                _ = SaveProfilesAsync();
            }
            else
            {
                Debug.WriteLine("No app selected");
            }
        }
        private void ExportProfiles(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON file (*.json)|*.json";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (saveFileDialog.ShowDialog() == true)
            {
                string json = JsonConvert.SerializeObject(AppState.Instance.Profiles);
                File.WriteAllText(saveFileDialog.FileName, json);
            }
        }
        private async void ImportProfiles(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON file (*.json)|*.json";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (openFileDialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var importedProfiles = JsonConvert.DeserializeObject<List<Profile>>(json);
                CustomDialog dialog = new CustomDialog("Are you sure you, this will remove all current profiles?");
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();

                if (dialog.Result == MessageBoxResult.No)
                {
                      return;
                }
                    // remove current profiles
                    AppState.Instance.Profiles.Clear();

                // add imported profiles
                foreach (var profile in importedProfiles)
                {
                    AppState.Instance.Profiles.Add(profile);
                }

                // save the changes
                _ = SaveProfilesAsync();

                // update the UI
                UpdateDataGrid();
                Cb_Profiles.SelectedIndex = 0; // if you want to automatically select the first imported profile
            }
        }
        private bool IsEpicInstalled(string exePath)
        {
            string manifestDir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
            if (!Directory.Exists(manifestDir))
                return false;

            string exeFileName = Path.GetFileName(exePath);
            string exeDirectory = Path.GetDirectoryName(exePath);

            foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var manifest = JObject.Parse(json);
                    string launchExe = manifest["LaunchExecutable"]?.ToString();
                    string installLocation = manifest["InstallLocation"]?.ToString();


                    if (!string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExe))
                    {
                        // Compare the full exe path
                        string expectedFullPath = Path.Combine(installLocation, launchExe);

                        if (string.Equals(Path.GetFullPath(expectedFullPath), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        // Or compare just folder + filename
                        if (string.Equals(Path.GetFullPath(installLocation), Path.GetFullPath(exeDirectory), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(launchExe, exeFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // Skip corrupt manifest
                }
            }

            return false;
        }

        private void Btn_ShowLogs(object sender, RoutedEventArgs e)
        {
            logpath = LoggingConfig.logFileFullPath;
            // Ensure the directory exists
            try
            {
                // Get the most recent log file

                if (logpath != null && File.Exists(logpath))
                {
                    try
                    {
                        ProcessStartInfo processStartInfo = new ProcessStartInfo
                        {
                            FileName = logpath,
                            UseShellExecute = true
                        };

                        Process.Start(processStartInfo);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error opening log file: {ex.Message}");
                    }
                }
                else
                {
                    Log.Error("Log file does not exist.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error opening log file: {ex.Message}");
            }
        }
    }
}