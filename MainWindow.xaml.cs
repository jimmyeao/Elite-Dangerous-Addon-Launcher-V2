using GongSolutions.Wpf.DragDrop;
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
//using static MaterialDesignThemes.Wpf.Theme;

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

            // Only save manual window size changes, not auto-sized dimensions
            if (SizeToContent == SizeToContent.Manual)
            {
                Properties.Settings.Default.MainWindowSize = new System.Drawing.Size((int)this.Width, (int)this.Height);
                Properties.Settings.Default.MainWindowLocation = new System.Drawing.Point((int)this.Left, (int)this.Top);
                Properties.Settings.Default.Save();
            }
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

            // Force window to resize based on new content
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
        private void Btn_LaunchSingle_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            MyApp appToLaunch = button.CommandParameter as MyApp;

            if (appToLaunch != null)
            {
                // Launch just this one app
                LaunchApp(appToLaunch, sender as Button);
                Log.Information("Launching single app: {AppName}", appToLaunch.Name);
            }
        }
        private void AddEliteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // If Elite is already in this profile, we should open the edit dialog instead
            bool eliteAlreadyExists = false;
            MyApp existingElite = null;

            foreach (var app in AppState.Instance.CurrentProfile.Apps)
            {
                if (IsEliteApp(app))
                {
                    eliteAlreadyExists = true;
                    existingElite = app;
                    break;
                }
            }

            if (eliteAlreadyExists)
            {
                EditEliteEntry(existingElite);
            }
            else
            {
                AddEliteToProfile();
            }
        }
        private void AddEliteToProfile()
        {
            var eliteLauncherDialog = new EliteLauncherDialog();
            eliteLauncherDialog.Owner = Application.Current.MainWindow;
            eliteLauncherDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (eliteLauncherDialog.ShowDialog() == true)
            {
                string args = eliteLauncherDialog.GetArgumentsString();

                switch (eliteLauncherDialog.SelectedLauncher)
                {
                    case EliteLauncherDialog.LauncherType.Standard:
                        // Scan for installation
                        ScanAndAddElite(args);
                        break;

                    case EliteLauncherDialog.LauncherType.Steam:
                        // Add Steam launcher
                        AddEliteWebApp("Elite Dangerous (Steam)",
                                      "steam://rungameid/359320",
                                      args);
                        break;

                    case EliteLauncherDialog.LauncherType.Epic:
                        // Add Epic launcher
                        AddEliteWebApp("Elite Dangerous (Epic)",
                                      "com.epicgames.launcher://apps/ed2aa564c5324fabab5af9d553a5c665%3A3c3d4ff38d0d4e889a1b399628d1ca7a%3Aaee9f2f7f3264eaa9c209943d3287e7d?action=launch&silent=true",
                                      args);
                        break;

                    case EliteLauncherDialog.LauncherType.Legendary:
                        // Add Legendary launcher
                        AddEliteWebApp("Elite Dangerous (Legendary)",
                                      "legendary://launch/ed2aa564c5324fabab5af9d553a5c665:3c3d4ff38d0d4e889a1b399628d1ca7a:aee9f2f7f3264eaa9c209943d3287e7d",
                                      args);
                        break;

                    case EliteLauncherDialog.LauncherType.Manual:
                        // Add manually selected path
                        if (!string.IsNullOrEmpty(eliteLauncherDialog.ManualPath))
                        {
                            AddEliteManualPath(eliteLauncherDialog.ManualPath,
                                             args);
                        }
                        break;
                }
            }
        }        // New method to launch a single app without minimizing the window
        private void LaunchSingleApp(MyApp app)
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

            if (File.Exists(path))
            {
                try
                {
                    var info = new ProcessStartInfo(path);
                    info.Arguments = args;
                    info.UseShellExecute = true;
                    info.WorkingDirectory = app.Path;
                    Process proc = Process.Start(info);

                    // Only add to process list if it's Elite Dangerous
                    if (proc.ProcessName == "EDLaunch" || proc.ProcessName == "EliteDangerous64")
                    {
                        proc.EnableRaisingEvents = true;
                        processList.Add(proc.ProcessName);
                        proc.Exited += new EventHandler(ProcessExitHandler);
                    }

                    Thread.Sleep(50);
                    proc.Refresh();

                    UpdateStatus($"Launched {app.Name}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"An error occurred trying to launch {app.Name}");
                    Log.Error(ex, "Error launching app {AppName}", app.Name);
                }
            }
            else if (!string.IsNullOrEmpty(app.WebAppURL))
            {
                string target = app.WebAppURL;
                try
                {
                    Process proc = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

                    // If launching Elite through a URL handler
                    if (target.Contains("rungameid/359320") ||
                        target.Contains("epic://launch") ||
                        target.Contains("legendary://launch"))
                    {
                        // Add monitoring logic here similar to in LaunchApp method
                        // But don't minimize the window

                        // Need a longer delay as launcher process needs to start Elite
                        Thread.Sleep(5000);

                        // Try to find Elite processes - either EDLaunch or EliteDangerous64
                        Process edLaunchProc = Process.GetProcessesByName("EDLaunch").FirstOrDefault();
                        if (edLaunchProc == null)
                        {
                            edLaunchProc = Process.GetProcessesByName("EliteDangerous64").FirstOrDefault();
                        }

                        if (edLaunchProc != null)
                        {
                            edLaunchProc.EnableRaisingEvents = true;
                            edLaunchProc.Exited += new EventHandler(ProcessExitHandler);
                            processList.Add(edLaunchProc.ProcessName);
                        }
                    }

                    UpdateStatus("Launching " + app.Name);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"An error occurred trying to launch {app.Name}");
                    Log.Error(ex, "Error launching web app {AppName}", app.Name);
                }
            }
            else
            {
                UpdateStatus($"Unable to launch {app.Name} - file not found");
            }
        }
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
                // Check if Elite is missing from the profile
                bool eliteExists = AppState.Instance.CurrentProfile.Apps.Any(app => IsEliteApp(app));

                if (!eliteExists)
                {
                    // Ask if they want to add Elite specifically
                    CustomDialog dialog = new CustomDialog("Would you like to add Elite Dangerous?");
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    dialog.ShowDialog();

                    if (dialog.Result == MessageBoxResult.Yes)
                    {
                        AddEliteToProfile();
                        return;
                    }
                }

                // Continue with normal app adding if they don't want to add Elite or Elite already exists
                AddApp addAppWindow = new AddApp()
                {
                    MainPageReference = this,
                    SelectedProfile = AppState.Instance.CurrentProfile,
                };
                // Set the owner and startup location
                addAppWindow.Owner = this;
                addAppWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                addAppWindow.Show();
                AddonDataGrid.ItemsSource = AppState.Instance.CurrentProfile.Apps;
            }
            else
            {
                // Handle the case when no profile is selected.
                CustomDialog dialog = new CustomDialog("Please create and select a profile first.");
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();
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
                // Check if this is an Elite Dangerous entry
                bool isEliteEntry = appToEdit.Name.Contains("Elite Dangerous") ||
                                    appToEdit.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase) ||
                                    appToEdit.ExeName.Equals("EliteDangerous64.exe", StringComparison.OrdinalIgnoreCase) ||
                                    appToEdit.WebAppURL?.Contains("rungameid/359320") == true ||
                                    appToEdit.WebAppURL?.Contains("epic://launch") == true ||
                                    appToEdit.WebAppURL?.Contains("legendary://launch") == true;

                if (isEliteEntry)
                {
                    // Show the Elite Launcher dialog instead
                    EditEliteEntry(appToEdit);
                }
                else
                {
                    // Show the standard app editor
                    AddApp addAppWindow = new AddApp();
                    addAppWindow.AppToEdit = appToEdit;
                    addAppWindow.MainPageReference = this;
                    addAppWindow.Owner = this;
                    addAppWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    addAppWindow.Title = "Edit App";
                    addAppWindow.ShowDialog();
                }
            }
        }
        private void EditEliteEntry(MyApp eliteApp)
        {
            var eliteLauncherDialog = new EliteLauncherDialog(true, eliteApp);
            eliteLauncherDialog.Owner = Application.Current.MainWindow;
            eliteLauncherDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (eliteLauncherDialog.ShowDialog() == true)
            {
                string args = eliteLauncherDialog.GetArgumentsString();

                switch (eliteLauncherDialog.SelectedLauncher)
                {
                    case EliteLauncherDialog.LauncherType.Standard:
                        // Keep existing entry if it's already edlaunch.exe
                        if (!string.IsNullOrEmpty(eliteApp.ExeName) &&
                            eliteApp.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            // Just update the arguments
                            eliteApp.Args = args;
                            eliteApp.WebAppURL = string.Empty; // Clear any web URL if present
                        }
                        else
                        {
                            ScanAndUpdateElite(eliteApp, args);
                        }
                        break;

                    case EliteLauncherDialog.LauncherType.Steam:
                        // For Steam, we'll set the Steam URI but also try to find a direct launcher path
                        UpdateEliteToWebApp(eliteApp, "Elite Dangerous (Steam)", "steam://rungameid/359320", args);
                        break;

                    case EliteLauncherDialog.LauncherType.Epic:
                        // Update to Epic version
                        UpdateEliteToWebApp(eliteApp, "Elite Dangerous (Epic)",
                            "com.epicgames.launcher://apps/ed2aa564c5324fabab5af9d553a5c665%3A3c3d4ff38d0d4e889a1b399628d1ca7a%3Aaee9f2f7f3264eaa9c209943d3287e7d?action=launch&silent=true", args);
                        break;

                    case EliteLauncherDialog.LauncherType.Legendary:
                        // Update to Legendary version
                        UpdateEliteToWebApp(eliteApp, "Elite Dangerous (Legendary)",
                            "legendary://launch/ed2aa564c5324fabab5af9d553a5c665:3c3d4ff38d0d4e889a1b399628d1ca7a:aee9f2f7f3264eaa9c209943d3287e7d", args);
                        break;

                    case EliteLauncherDialog.LauncherType.Manual:
                        // Update to manually selected path
                        if (!string.IsNullOrEmpty(eliteLauncherDialog.ManualPath))
                        {
                            UpdateEliteToManualPath(eliteApp, eliteLauncherDialog.ManualPath, args);
                        }
                        break;
                }

                SaveProfilesAsync();
            }
        }
        private async void ScanAndUpdateElite(MyApp eliteApp, string args)
        {
            var edLaunchPaths = await ScanComputerForEdLaunch();
            if (edLaunchPaths.Count > 0)
            {
                eliteApp.Name = "Elite Dangerous";
                eliteApp.ExeName = "edlaunch.exe";
                eliteApp.Path = Path.GetDirectoryName(edLaunchPaths[0]);
                eliteApp.WebAppURL = null;
                eliteApp.Args = args;
            }
            else
            {
                ShowErrorMessage("Elite Dangerous not found",
                                 "Elite Dangerous was not found on your computer. No changes were made.");
            }
        }
        private void UpdateEliteToWebApp(MyApp eliteApp, string name, string url, string args)
        {
            eliteApp.Name = name;
            eliteApp.WebAppURL = url;
            eliteApp.ExeName = string.Empty;
            eliteApp.Path = string.Empty;
            eliteApp.Args = args; // Store args even for Steam, so they're preserved if switching back to direct launch
        }

        private void UpdateEliteToManualPath(MyApp eliteApp, string fullPath, string args)
        {
            eliteApp.Name = "Elite Dangerous";
            eliteApp.ExeName = Path.GetFileName(fullPath);
            eliteApp.Path = Path.GetDirectoryName(fullPath);
            eliteApp.WebAppURL = null;
            eliteApp.Args = args;
        }

        private void UpdateEliteToWebApp(MyApp eliteApp, string name, string url)
        {
            eliteApp.Name = name;
            eliteApp.WebAppURL = url;
            eliteApp.ExeName = string.Empty;
            eliteApp.Path = string.Empty;
            SaveProfilesAsync();
        }

        private void UpdateEliteToManualPath(MyApp eliteApp, string fullPath)
        {
            eliteApp.Name = "Elite Dangerous";
            eliteApp.ExeName = Path.GetFileName(fullPath);
            eliteApp.Path = Path.GetDirectoryName(fullPath);
            eliteApp.WebAppURL = null;
            SaveProfilesAsync();
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

            ListItem listItem1 = new ListItem(new Paragraph(new Run("Fixed bug with adding profiles causing a crash on first run")));
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
            Log.Information("Launching all enabled apps..");
            foreach (var app in AppState.Instance.CurrentProfile.Apps)
            {
                if (app.IsEnabled)
                {
                    Btn_Launch.IsEnabled = false;
                    LaunchApp(app, sender as Button);
                    Log.Information("Launching {AppName}..", app.Name);
                }
            }

            UpdateStatus("All apps launched, waiting for EDLaunch Exit..");
            // Minimize the window when using the main Launch button
            this.WindowState = WindowState.Minimized;
        }
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            // If window was manually resized by the user
            if (WindowState == WindowState.Normal)
            {
                // Switch to manual sizing to preserve the user's size
                SizeToContent = SizeToContent.Manual;
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
        // Add a field to track the launch source
        private Button _launchSource = null;

        // Modify LaunchApp to accept the source
        private void LaunchApp(MyApp app, Button source = null)
        {
            // Different apps have different args, so lets set up a string to hold them
            string args;
            const string quote = "\"";

            // For web launches (Steam, Epic, Legendary), just launch the URI
            if (!string.IsNullOrEmpty(app.WebAppURL))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(app.WebAppURL) { UseShellExecute = true });
                    UpdateStatus($"Launching {app.Name} via {app.WebAppURL}");

                    // Only minimize window when using the main Launch button, not individual app launches
                    if (app.Name.Contains("Elite Dangerous") && source != null && source.Equals(Btn_Launch))
                    {
                        this.WindowState = WindowState.Minimized;
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error launching {app.Name}: {ex.Message}");
                }
                return;
            }

            var path = $"{app.Path}/{app.ExeName}";

            // Are we launching TARGET?
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

            if (File.Exists(path))
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

                    if (proc.ProcessName == "EDLaunch")
                    {
                        proc.Exited += new EventHandler(ProcessExitHandler);
                    }

                    Thread.Sleep(50);
                    proc.Refresh();

                    // Only minimize window when using the main Launch button
                    if (app.Name.Contains("Elite Dangerous") && source != null && source.Equals(Btn_Launch))
                    {
                        this.WindowState = WindowState.Minimized;
                    }
                }
                catch
                {
                    // oh dear, something went horribly wrong..
                    UpdateStatus($"An error occurred trying to launch {app.Name}..");
                }
            }
            else
            {
                UpdateStatus($"Unable to launch {app.Name}..");
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
                    AdjustWindowSizeToContent();
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
            AdjustWindowSizeToContent();
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
            string targetFile = "edlaunch.exe";
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            SearchProgressWindow progressWindow = new SearchProgressWindow();

            progressWindow.Owner = Application.Current.MainWindow;
            progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            progressWindow.Closing += (s, e) => tokenSource.Cancel();

            progressWindow.Show(); // Show the window before starting the task

            try
            {
                await Task.Run(() =>
                {
                    // Common installation locations to check first
                    string[] commonLocations = new string[]
                    {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Frontier", "EDLaunch"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Elite Dangerous"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier_Developments", "Products")
                    };

                    // Check common locations first
                    foreach (string location in commonLocations)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        try
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressWindow.searchStatusTextBlock.Text = $"Checking: {location}";
                            });

                            if (Directory.Exists(location))
                            {
                                string edLaunchPath = Path.Combine(location, targetFile);
                                if (File.Exists(edLaunchPath))
                                {
                                    foundPaths.Add(edLaunchPath);
                                    break;
                                }

                                // Search subdirectories 
                                foreach (string subDir in Directory.GetDirectories(location))
                                {
                                    if (token.IsCancellationRequested)
                                        break;

                                    string subEdLaunchPath = Path.Combine(subDir, targetFile);
                                    if (File.Exists(subEdLaunchPath))
                                    {
                                        foundPaths.Add(subEdLaunchPath);
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Error checking location {Location}", location);
                        }
                    }

                    // If not found in common locations, do a more extensive search
                    if (foundPaths.Count == 0)
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
                                    TraverseDirectories(driveRoot, "Elite Dangerous", targetFile, foundPaths, progressWindow, 4, token);
                                    if (foundPaths.Count > 0)
                                        break;

                                    // Also check for Frontier folder
                                    TraverseDirectories(driveRoot, "Frontier", targetFile, foundPaths, progressWindow, 4, token);
                                    if (foundPaths.Count > 0)
                                        break;
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "Error scanning drive {Drive}", drive.Name);
                                }
                            }
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Elite Dangerous scan was canceled by user");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Elite Dangerous installation scan");
            }
            finally
            {
                if (progressWindow.IsVisible)
                    progressWindow.Close();
            }

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


        // Helper method to add Elite Dangerous web link (Steam/Epic/Legendary)
        private async void AddEliteWebApp(string name, string url, string args)
        {
            var app = new MyApp
            {
                Name = name,
                WebAppURL = url,
                Args = args,
                IsEnabled = true,
                Order = 0
            };

            AppState.Instance.CurrentProfile.Apps.Add(app);
            await SaveProfilesAsync();
            UpdateDataGrid();
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
                 app.WebAppURL.Contains("epic://launch") ||
                 app.WebAppURL.Contains("legendary://launch")))
            {
                return true;
            }

            // Check if the name explicitly indicates it's Elite Dangerous
            if (!string.IsNullOrEmpty(app.Name) &&
                app.Name.StartsWith("Elite Dangerous", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
        private void AddEliteDirectly()
        {
            // If Elite is already in this profile, we should open the edit dialog instead
            bool eliteAlreadyExists = false;
            MyApp existingElite = null;

            foreach (var app in AppState.Instance.CurrentProfile.Apps)
            {
                if (IsEliteApp(app))
                {
                    eliteAlreadyExists = true;
                    existingElite = app;
                    break;
                }
            }

            if (eliteAlreadyExists)
            {
                EditEliteEntry(existingElite);
            }
            else
            {
                AddEliteToProfile();
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

            // Check if Elite Dangerous exists in the current profile using our helper method
            if (!currentProfile.Apps.Any(IsEliteApp))
            {
                // Show our new enhanced dialog
                var eliteLauncherDialog = new EliteLauncherDialog();
                eliteLauncherDialog.Owner = Application.Current.MainWindow;
                eliteLauncherDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                if (eliteLauncherDialog.ShowDialog() == true)
                {
                    switch (eliteLauncherDialog.SelectedLauncher)
                    {
                        case EliteLauncherDialog.LauncherType.Standard:
                            // Show a progress dialog while scanning
                            Log.Information("Starting scan for Elite Dangerous installation");
                            var edLaunchPaths = await ScanComputerForEdLaunch();

                            if (edLaunchPaths != null && edLaunchPaths.Count > 0)
                            {
                                string path = edLaunchPaths[0];
                                Log.Information("Found Elite Dangerous at: {Path}", path);
                                string exeName = Path.GetFileName(path);
                                string dirPath = Path.GetDirectoryName(path);

                                var edlaunch = new MyApp
                                {
                                    Name = "Elite Dangerous",
                                    ExeName = exeName,
                                    Path = dirPath,
                                    Args = eliteLauncherDialog.GetArgumentsString(),
                                    IsEnabled = true,
                                    Order = 0
                                };

                                AppState.Instance.CurrentProfile.Apps.Add(edlaunch);
                                await SaveProfilesAsync();
                                Log.Information("Added Elite Dangerous to profile with arguments: {Args}",
                                    eliteLauncherDialog.GetArgumentsString());
                            }
                            else
                            {
                                ShowErrorMessage("Elite Dangerous not found",
                                                "Elite Dangerous was not found on your computer. Please select another option.");
                                Log.Warning("No Elite Dangerous installation found during scan");
                            }
                            break;

                            // Other cases remain the same...
                    }
                }
            }
        }        // Helper method to add Elite Dangerous to current profile from file path

        private async void AddEliteManualPath(string fullPath, string args)
        {
            var app = new MyApp
            {
                Name = "Elite Dangerous",
                ExeName = Path.GetFileName(fullPath),
                Path = Path.GetDirectoryName(fullPath),
                Args = args,
                IsEnabled = true,
                Order = 0
            };

            AppState.Instance.CurrentProfile.Apps.Add(app);
            await SaveProfilesAsync();
            UpdateDataGrid();
        }
        private async void ScanAndAddElite(string args)
        {
            var edLaunchPaths = await ScanComputerForEdLaunch();
            if (edLaunchPaths.Count > 0)
            {
                var edlaunch = new MyApp
                {
                    Name = "Elite Dangerous",
                    ExeName = "edlaunch.exe",
                    Path = Path.GetDirectoryName(edLaunchPaths[0]),
                    Args = args,
                    IsEnabled = true,
                    Order = 0
                };

                AppState.Instance.CurrentProfile.Apps.Add(edlaunch);
                await SaveProfilesAsync();
                UpdateDataGrid();
            }
            else
            {
                ShowErrorMessage("Elite Dangerous not found",
                              "Elite Dangerous was not found on your computer. Please select another option.");
            }
        }



        // Helper method to show error message
        private void ShowErrorMessage(string title, string message)
        {
            ErrorDialog dialog = new ErrorDialog(message);
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Title = title;
            dialog.ShowDialog();
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