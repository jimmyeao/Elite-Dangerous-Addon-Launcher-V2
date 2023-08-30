using GongSolutions.Wpf.DragDrop;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
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
                AddApp addAppWindow = new AddApp();
                addAppWindow.AppToEdit = appToEdit; // Set the AppToEdit to the app you want to edit
                addAppWindow.MainPageReference = this; // Assuming this is done from MainWindow, else replace 'this' with the instance of MainWindow
                                                       // Set the owner and startup location
                addAppWindow.Owner = this; // Or replace 'this' with reference to the main window
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

            ListItem listItem1 = new ListItem(new Paragraph(new Run("Fixed bug DPI scaling on some monitors")));
            list.ListItems.Add(listItem1);

          //  ListItem listItem2 = new ListItem(new Paragraph(new Run("Profile Options for import/export and copy/rename/delete")));
          //  list.ListItems.Add(listItem2);

          //  ListItem listItem3 = new ListItem(new Paragraph(new Run("Added themed dialogs")));
          //  list.ListItems.Add(listItem3);

          //  ListItem listItem4 = new ListItem(new Paragraph(new Run("Fly safe, Cmdr! o7")));
          //  list.ListItems.Add(listItem4);

            whatsNewWindow.WhatsNewText.Document.Blocks.Add(list);

            whatsNewWindow.Owner = this; // Set owner to this MainWindow
            whatsNewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Center the window over its owner

            whatsNewWindow.ShowDialog();
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
                    if (!_isLoading) // Change here
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
                AppState.Instance.CurrentProfile.Apps.Remove(appToDelete);
                _ = SaveProfilesAsync();
            }
        }

        private void LaunchApp(MyApp app) // function to launch enabled applications
        {
            // set up a list to track which apps we launched

            // different apps have different args, so lets set up a string to hold them
            string args;
            // TARGET requires a path to a script, if that path has spaces, we need to quote them -
            // set a string called quote we can use to top and tail
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
                    processList.Add(proc.ProcessName);
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
                if (!string.IsNullOrEmpty(app.WebAppURL))
                {
                    string target = app.WebAppURL;
                    Process proc = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

                    // If the app we're launching is via the steam URL, we anticipate that EDLaunch will run
                    if (target.Equals("steam://rungameid/359320", StringComparison.OrdinalIgnoreCase))
                    {
                        // Small delay to give time for the EDLaunch process to start after Steam starts
                        Thread.Sleep(2000);

                        // Find the EDLaunch process and attach the event handler
                        Process edLaunchProc = Process.GetProcessesByName("EDLaunch").FirstOrDefault();
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
            // notifyIcon1.BalloonTipText = "All Apps running, waiting for exit"; <-- You'll need to
            // define notifyIcon1 first
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
            _isLoading = true;
            await LoadProfilesAsync(App.ProfileName);
            settings = await LoadSettingsAsync();
            isDarkTheme = settings.Theme == "Dark";
            ApplyTheme(settings.Theme);
            AppState.Instance.CloseAllAppsOnExit = settings.CloseAllAppsOnExit;
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
            bool closeAllApps = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                closeAllApps = CloseAllAppsCheckbox.IsChecked == true;
            });
            // if EDLaunch has quit, does the user want us to kill all the apps?
            if (closeAllApps)
            {
                try
                {
                    foreach (string p in processList)
                    {
                        foreach (Process process in Process.GetProcessesByName(p))
                        {
                            // Temp is a document which you need to kill.
                            if (process.ProcessName.Contains(p))
                                process.CloseMainWindow();
                        }
                    }
                }
                catch
                {
                    // if something went wrong, don't raise an exception
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


    }
}