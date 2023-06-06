using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Elite_Dangerous_Addon_Launcer_V2

{
    public partial class MainWindow : Window
    {
        #region Private Fields

        // The row that will be dragged.
        private DataGridRow _rowToDrag;

        // Store the position where the mouse button is clicked.
        private Point _startPoint;

        private bool isDarkTheme = false;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private Settings settings;

        #endregion Private Fields

        #region Public Constructors

        public MainWindow()
        {
            InitializeComponent();

            // Assign the event handler to the Loaded event
            this.Loaded += MainWindow_Loaded;

            // Set the data context to AppState instance
            this.DataContext = AppState.Instance;
            if (Properties.Settings.Default.CloseAllAppsOnExit)
            {
                CloseAllAppsCheckbox.IsChecked = Properties.Settings.Default.CloseAllAppsOnExit;
            }
        }

        #endregion Public Constructors

        #region Public Methods

        public void DragOver(IDropInfo dropInfo)
        {
            MyApp sourceItem = dropInfo.Data as MyApp;
            MyApp targetItem = dropInfo.TargetItem as MyApp;

            if (sourceItem != null && targetItem != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            MyApp sourceItem = dropInfo.Data as MyApp;
            MyApp targetItem = dropInfo.TargetItem as MyApp;

            int sourceIndex = AppState.Instance.CurrentProfile.Apps.IndexOf(sourceItem);
            int targetIndex = AppState.Instance.CurrentProfile.Apps.IndexOf(targetItem);

            AppState.Instance.CurrentProfile.Apps.Move(sourceIndex, targetIndex);
        }

        public async Task LoadProfilesAsync()
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

                    // Set the CurrentProfile to the default profile (or first one if no default exists)
                    AppState.Instance.CurrentProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault)
                        ?? AppState.Instance.Profiles.FirstOrDefault();
                    Cb_Profiles.SelectedItem = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);
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
        }

        public void UpdateDataGrid()
        {
            // Assuming 'AddonDataGrid' is the name of your DataGrid control
            if (AppState.Instance.CurrentProfile != null)
            {
                // Set DataGrid's ItemsSource to the apps of the currently selected profile
                AddonDataGrid.ItemsSource = AppState.Instance.CurrentProfile.Apps;
            }
            else
            {
                // No profile is selected. Clear the data grid.
                AddonDataGrid.ItemsSource = null;
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void AddonDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Write the MyApp instances to the data source here, e.g.:
            // SaveAppStateToFile();
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

        private void Bt_RemoveProfile_Click_1(object sender, RoutedEventArgs e)
        {
            Profile profileToRemove = (Profile)Cb_Profiles.SelectedItem;

            if (profileToRemove != null)
            {
                AppState.Instance.Profiles.Remove(profileToRemove);
                _ = SaveProfilesAsync();
                UpdateDataGrid();
            }
        }

        private void Btn_Edit_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            MyApp appToEdit = button.CommandParameter as MyApp;

            if (appToEdit != null)
            {
                AddApp addAppWindow = new AddApp();
                addAppWindow.AppToEdit = appToEdit; // Set the AppToEdit to the app you want to edit
                addAppWindow.MainPageReference = this; // Assuming this is done from MainWindow, else replace 'this' with the instance of MainWindow

                addAppWindow.ShowDialog();
            }
        }

        private void Btn_Launch_Click(object sender, RoutedEventArgs e)
        {
            // Code to launch all enabled apps
            foreach (var app in AppState.Instance.CurrentProfile.Apps)
            {
                if (app.IsEnabled)
                {
                    LaunchApp(app);
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

            // retrieve the item associated with this button
            var appToDelete = (MyApp)button.DataContext;

            // remove the item from the collection
            AppState.Instance.CurrentProfile.Apps.Remove(appToDelete);
            _ = SaveProfilesAsync();
        }

        private MyApp JsonToMyApp(string jsonString)
        {
            return JsonConvert.DeserializeObject<MyApp>(jsonString);
        }

        private void LaunchApp(MyApp app) // function to launch enabled applications
        {
            // set up a list to track which apps we launched

            // different apps have different args, so lets set up a string to hold them
            string args;
            // TARGET requires a path to a script, if that path has spaces, we need to quote them - set a string called quote we can use to top and tail
            const string quote = "\"";
            var path = $"{app.Path}/{app.ExeName}";
            // are we launching TARGET?
            if (string.Equals(app.ExeName, "targetgui.exe", StringComparison.OrdinalIgnoreCase))
            {
                // -r is to specify a script
                args = "-r " + quote + app.Args + quote;
            }
            else
            {
                // ok its not target, leave the arguments as is
                args = app.Args;
            }

            if (File.Exists(path))      // worth checking the app we want to launch actually exists...
            {
                try
                {
                    var info = new ProcessStartInfo(path);
                    info.Arguments = args;
                    info.UseShellExecute = true;
                    info.WorkingDirectory = app.Path;
                    Process proc = Process.Start(info);
                    proc.EnableRaisingEvents = true;
                    // processList.Add(proc.ProcessName); <-- You'll need to define processList first
                    if (proc.ProcessName == "EDLaunch")
                    {
                        proc.Exited += new EventHandler(ProcessExitHandler);
                    }
                    Thread.Sleep(50);
                    proc.Refresh();
                }
                catch
                {
                    // oh dear, something went horribly wrong..
                    UpdateStatus($"An error occurred trying to launch {app.Name}..");
                }
            }
            else
            {
                // yeah, that path didn't exist...
                // are we launching a web app?
                if (!string.IsNullOrEmpty(app.WebAppURL))
                {
                    // ok, let's launch it in the default browser
                    UpdateStatus("Launching " + app.Name);
                    string target = app.WebAppURL;
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                }
                else
                {
                    UpdateStatus($"Unable to launch {app.Name}..");
                }
            }
            UpdateStatus("All apps launched, waiting for EDLaunch Exit..");
            // notifyIcon1.BalloonTipText = "All Apps running, waiting for exit"; <-- You'll need to define notifyIcon1 first
            this.WindowState = WindowState.Minimized;
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

            return settings;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProfilesAsync();
            settings = await LoadSettingsAsync();
            isDarkTheme = settings.Theme == "Dark";
            ApplyTheme(settings.Theme);
            AppState.Instance.CloseAllAppsOnExit = settings.CloseAllAppsOnExit;
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
                _=SaveProfilesAsync();
            }
        }

        // Somewhere where you handle profile change:
        private void OnProfileChanged(Profile oldProfile, Profile newProfile)
        {
            UnsubscribeFromAppEvents(oldProfile);
            SubscribeToAppEvents(newProfile);
        }

        // Define your ProcessExitHandler and UpdateStatus methods
        private void ProcessExitHandler(object sender, EventArgs e)
        {
            // Define what happens when the process exits
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
    }
}