using Elite_Dangerous_Addon_Launcher_V2.Services;
using Elite_Dangerous_Addon_Launcher_V2.ViewModels;
using GongSolutions.Wpf.DragDrop;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Elite_Dangerous_Addon_Launcher_V2.Views
{
    /// <summary>
    /// Refactored MainWindow following MVVM pattern
    /// Business logic is in MainWindowViewModel
    /// View-specific logic remains here (dialogs, window state, theme)
    /// </summary>
    public partial class MainWindow : Window, IDropTarget
    {
        private readonly MainWindowViewModel _viewModel;
        private bool isDarkTheme = false;

        public MainWindow(string profileName = null)
        {
            InitializeComponent();
            LoggingConfig.Configure();

            // Copy user settings from previous application version if necessary
            if (Properties.Settings.Default.UpdateSettings)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                Properties.Settings.Default.Save();
            }

            // Set minimum dimensions
            this.MinWidth = 741;
            this.MinHeight = 300;

            // Apply saved window width (height is auto-sized to content)
            var storedWidth = Properties.Settings.Default.MainWindowSize.Width;
            this.Width = (storedWidth >= this.MinWidth) ? storedWidth : 741;
            // Height is controlled by SizeToContent="Height" in XAML

            // Initialize services
            var profileService = new ProfileService();
            var settingsService = new SettingsService();
            var processLaunchService = new ProcessLaunchService();

            // Create ViewModel
            _viewModel = new MainWindowViewModel(profileService, settingsService, processLaunchService);

            // Set DataContext
            this.DataContext = _viewModel;

            // Subscribe to ViewModel events for view-specific actions
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Assign window event handlers
            this.Loaded += MainWindow_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;
        }

        #region Window Lifecycle Events

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Wait for ViewModel to finish loading settings
            while (_viewModel.IsLoading)
            {
                await Task.Delay(50);
            }

            // Apply theme based on loaded settings
            ApplyTheme(_viewModel.CurrentTheme);

            // Apply saved column width
            if (AddonDataGrid.Columns.Count > 0 && Properties.Settings.Default.AppNameColumnWidth > 0)
            {
                var appNameColumn = AddonDataGrid.Columns[0];
                // Ensure width is reasonable (between 200 and 500)
                double savedWidth = Properties.Settings.Default.AppNameColumnWidth;
                if (savedWidth >= 200 && savedWidth <= 500)
                {
                    appNameColumn.Width = new DataGridLength(savedWidth);
                }
            }

            // Show what's new if version changed
            ShowWhatsNewIfUpdated();

            // Note: Profile creation prompt is handled by ViewModel.InitializeAsync()
            // Don't check here to avoid race condition
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup ViewModel
            _viewModel?.Cleanup();

            base.OnClosed(e);

            // Save window size and position (height is auto-calculated but saved for compatibility)
            Properties.Settings.Default.MainWindowSize = new System.Drawing.Size((int)this.Width, (int)this.Height);
            Properties.Settings.Default.MainWindowLocation = new System.Drawing.Point((int)this.Left, (int)this.Top);
            Properties.Settings.Default.Save();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Position window
            if (Properties.Settings.Default.MainWindowLocation == new System.Drawing.Point(0, 0))
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                double left = Properties.Settings.Default.MainWindowLocation.X;
                double top = Properties.Settings.Default.MainWindowLocation.Y;

                bool isOnScreen = left < SystemParameters.VirtualScreenWidth &&
                                 top < SystemParameters.VirtualScreenHeight &&
                                 left + this.Width > SystemParameters.VirtualScreenLeft &&
                                 top + this.Height > SystemParameters.VirtualScreenTop;

                if (isOnScreen)
                {
                    this.Left = left;
                    this.Top = top;
                }
                else
                {
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Save column width if changed (constrained between 200 and 500)
            if (AddonDataGrid.Columns.Count > 0)
            {
                var appNameColumn = AddonDataGrid.Columns[0];
                double actualWidth = appNameColumn.ActualWidth;

                // Only save if width is reasonable and different from saved value
                if (actualWidth >= 200 && actualWidth <= 500 &&
                    Math.Abs(actualWidth - Properties.Settings.Default.AppNameColumnWidth) > 1)
                {
                    Properties.Settings.Default.AppNameColumnWidth = actualWidth;
                    Properties.Settings.Default.Save();
                }
            }
        }

        #endregion

        #region ViewModel Event Handlers

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.CurrentProfile):
                    // Profile changed, check if Elite Dangerous needs to be added
                    CheckForEliteDangerous();
                    break;

                case nameof(MainWindowViewModel.IsLaunchButtonEnabled):
                    // When launch button is disabled, it means apps are being launched
                    // Minimize the window (only on the transition from enabled to disabled with apps present)
                    if (!_viewModel.IsLaunchButtonEnabled &&
                        _viewModel.CurrentProfile != null &&
                        _viewModel.CurrentProfile.Apps.Any(a => a.IsEnabled))
                    {
                        this.WindowState = WindowState.Minimized;
                    }
                    break;

                case nameof(MainWindowViewModel.CurrentTheme):
                    // Theme changed, apply it
                    ApplyTheme(_viewModel.CurrentTheme);
                    break;
            }
        }

        #endregion

        #region Button Click Handlers (Showing Dialogs)

        private async void Bt_AddApp_Click_1(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentProfile == null)
                return;

            var addAppDialog = new AddApp
            {
                SelectedProfile = _viewModel.CurrentProfile,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            if (addAppDialog.ShowDialog() == true)
            {
                var newApp = addAppDialog.MyAppList.FirstOrDefault();
                if (newApp != null)
                {
                    _viewModel.CurrentProfile.Apps.Add(newApp);
                    await _viewModel.SaveProfilesAsync();
                }
            }
        }

        private async void Btn_Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MyApp appToEdit)
            {
                // Check if this is Elite Dangerous
                if (IsEliteApp(appToEdit))
                {
                    await ShowEliteLauncherDialog(true, appToEdit);
                }
                else
                {
                    await ShowEditAppDialog(appToEdit);
                }
            }
        }

        private async Task ShowEditAppDialog(MyApp appToEdit)
        {
            var editDialog = new AddApp
            {
                SelectedProfile = _viewModel.CurrentProfile,
                AppToEdit = appToEdit,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            if (editDialog.ShowDialog() == true)
            {
                var updatedApp = editDialog.MyAppList.FirstOrDefault();
                if (updatedApp != null)
                {
                    // Update properties
                    appToEdit.Name = updatedApp.Name;
                    appToEdit.Path = updatedApp.Path;
                    appToEdit.ExeName = updatedApp.ExeName;
                    appToEdit.Args = updatedApp.Args;
                    appToEdit.WebAppURL = updatedApp.WebAppURL;
                    appToEdit.InstallationURL = updatedApp.InstallationURL;
                    appToEdit.IsEnabled = updatedApp.IsEnabled;

                    await _viewModel.SaveProfilesAsync();
                }
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MyApp appToDelete)
            {
                var result = CustomDialog.Show(
                    $"Are you sure you want to delete {appToDelete.Name}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    this);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.CurrentProfile?.Apps.Remove(appToDelete);
                    await _viewModel.SaveProfilesAsync();
                }
            }
        }

        private async void Bt_AddProfile_Click_1(object sender, RoutedEventArgs e)
        {
            await ShowAddProfileDialog();
        }

        private async Task ShowAddProfileDialog()
        {
            var dialog = new AddProfileDialog
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string profileName = dialog.ProfileName;
                var newProfile = new Profile { Name = profileName, IsDefault = false };

                // If this is the first profile, make it default
                if (AppState.Instance.Profiles.Count == 0)
                {
                    newProfile.IsDefault = true;
                }

                AppState.Instance.Profiles.Add(newProfile);
                _viewModel.CurrentProfile = newProfile;

                await _viewModel.SaveProfilesAsync();

                // Check if Elite Dangerous needs to be added
                CheckForEliteDangerous();
            }
        }

        private async void Bt_RemoveProfile_Click_1(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentProfile == null)
                return;

            var result = CustomDialog.Show(
                $"Are you sure you want to delete the profile '{_viewModel.CurrentProfile.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                this);

            if (result == MessageBoxResult.Yes)
            {
                AppState.Instance.Profiles.Remove(_viewModel.CurrentProfile);

                // Select first remaining profile or null
                _viewModel.CurrentProfile = AppState.Instance.Profiles.FirstOrDefault();

                await _viewModel.SaveProfilesAsync();
            }
        }

        private async void Bt_CopyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentProfile == null)
                return;

            var dialog = new AddProfileDialog
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var newProfile = new Profile
                {
                    Name = dialog.ProfileName,
                    IsDefault = false
                };

                // Deep copy apps
                foreach (var app in _viewModel.CurrentProfile.Apps)
                {
                    newProfile.Apps.Add(app.DeepCopy());
                }

                AppState.Instance.Profiles.Add(newProfile);
                await _viewModel.SaveProfilesAsync();
            }
        }

        private async void Bt_RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentProfile == null)
                return;

            var dialog = new RenameProfileDialog(_viewModel.CurrentProfile.Name)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.CurrentProfile.Name = dialog.NewName;
                await _viewModel.SaveProfilesAsync();
            }
        }

        private void ExportProfiles(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON file (*.json)|*.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string json = JsonConvert.SerializeObject(AppState.Instance.Profiles);
                File.WriteAllText(saveFileDialog.FileName, json);
                CustomDialog.Show("Profiles exported successfully!", "Export Complete", MessageBoxButton.OK, this);
            }
        }

        private async void ImportProfiles(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON file (*.json)|*.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var importedProfiles = JsonConvert.DeserializeObject<List<Profile>>(json);

                var result = CustomDialog.Show(
                    "This will remove all current profiles. Are you sure?",
                    "Confirm Import",
                    MessageBoxButton.YesNo,
                    this);

                if (result == MessageBoxResult.Yes)
                {
                    AppState.Instance.Profiles.Clear();

                    foreach (var profile in importedProfiles)
                    {
                        AppState.Instance.Profiles.Add(profile);
                    }

                    _viewModel.CurrentProfile = AppState.Instance.Profiles.FirstOrDefault();
                    await _viewModel.SaveProfilesAsync();

                    CustomDialog.Show("Profiles imported successfully!", "Import Complete", MessageBoxButton.OK, this);
                }
            }
        }

        private void Btn_ShowLogs(object sender, RoutedEventArgs e)
        {
            string logpath = LoggingConfig.logFileFullPath;

            if (logpath != null && File.Exists(logpath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logpath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to open log file");
                    CustomDialog.Show($"Failed to open log file: {ex.Message}", "Error", MessageBoxButton.OK, this);
                }
            }
            else
            {
                CustomDialog.Show("Log file not found.", "Error", MessageBoxButton.OK, this);
            }
        }

        #endregion

        #region Theme Management

        private async void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle theme in ViewModel (which will save and notify)
            await _viewModel.ToggleThemeAsync();
            // Theme will be applied automatically via PropertyChanged event
        }

        private void ApplyTheme(string theme)
        {
            var paletteHelper = new PaletteHelper();
            var currentTheme = paletteHelper.GetTheme();

            if (theme == "Dark")
            {
                currentTheme.SetBaseTheme(BaseTheme.Dark);
                isDarkTheme = true;
            }
            else
            {
                currentTheme.SetBaseTheme(BaseTheme.Light);
                isDarkTheme = false;
            }

            paletteHelper.SetTheme(currentTheme);
            Log.Information("Theme applied: {Theme}", theme);
        }

        #endregion

        #region Elite Dangerous Management

        private async void CheckForEliteDangerous()
        {
            if (_viewModel.CurrentProfile == null)
                return;

            // Check if Elite Dangerous is in the profile
            bool hasElite = _viewModel.CurrentProfile.Apps.Any(app => IsEliteApp(app));

            if (!hasElite)
            {
                Log.Information("Elite Dangerous not found in profile, prompting user");
                await ShowEliteLauncherDialog(false, null);
            }
        }

        private async Task ShowEliteLauncherDialog(bool isEditing, MyApp existingApp)
        {
            // Get preferred launcher type from settings
            string preferredType = "Standard"; // Default
            // You could load this from settings if needed

            var dialog = new EliteLauncherDialog(isEditing, existingApp, preferredType)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var launcherType = dialog.SelectedLauncher;
                var arguments = dialog.GetArgumentsString();

                if (isEditing && existingApp != null)
                {
                    // Update existing app
                    await UpdateEliteApp(existingApp, launcherType, dialog.ManualPath, arguments);
                }
                else
                {
                    // Add new Elite app
                    await AddEliteApp(launcherType, dialog.ManualPath, arguments);
                }
            }
        }

        private async Task AddEliteApp(EliteLauncherDialog.LauncherType launcherType, string manualPath, string arguments)
        {
            MyApp eliteApp = null;

            switch (launcherType)
            {
                case EliteLauncherDialog.LauncherType.Standard:
                    var paths = await ScanForEdLaunch();
                    if (paths.Count > 0)
                    {
                        eliteApp = new MyApp
                        {
                            Name = "Elite Dangerous",
                            ExeName = "edlaunch.exe",
                            Path = System.IO.Path.GetDirectoryName(paths[0]),
                            Args = arguments,
                            IsEnabled = true,
                            Order = 0
                        };
                    }
                    break;

                case EliteLauncherDialog.LauncherType.Steam:
                    eliteApp = new MyApp
                    {
                        Name = "Elite Dangerous (Steam)",
                        WebAppURL = "steam://rungameid/359320",
                        Args = arguments,
                        IsEnabled = true,
                        Order = 0
                    };
                    break;

                case EliteLauncherDialog.LauncherType.Epic:
                    eliteApp = new MyApp
                    {
                        Name = "Elite Dangerous (Epic)",
                        WebAppURL = "com.epicgames.launcher://apps/9C203B6ED35846E8A4A9FF1E45A45B19%3A0a2d9f6403244d12969e11da6713137b%3A9C203B6ED35846E8A4A9FF1E45A45B19?action=launch&silent=true",
                        Args = arguments,
                        IsEnabled = true,
                        Order = 0
                    };
                    break;

                case EliteLauncherDialog.LauncherType.Legendary:
                    eliteApp = new MyApp
                    {
                        Name = "Elite Dangerous (Legendary)",
                        WebAppURL = "legendary://launch",
                        Args = arguments,
                        IsEnabled = true,
                        Order = 0
                    };
                    break;

                case EliteLauncherDialog.LauncherType.Manual:
                    if (!string.IsNullOrEmpty(manualPath) && File.Exists(manualPath))
                    {
                        eliteApp = new MyApp
                        {
                            Name = "Elite Dangerous",
                            ExeName = System.IO.Path.GetFileName(manualPath),
                            Path = System.IO.Path.GetDirectoryName(manualPath),
                            Args = arguments,
                            IsEnabled = true,
                            Order = 0
                        };
                    }
                    break;
            }

            if (eliteApp != null)
            {
                _viewModel.CurrentProfile.Apps.Insert(0, eliteApp);
                await _viewModel.SaveProfilesAsync();
            }
            else
            {
                CustomDialog.Show("Failed to add Elite Dangerous. Please try again.", "Error", MessageBoxButton.OK, this);
            }
        }

        private async Task UpdateEliteApp(MyApp existingApp, EliteLauncherDialog.LauncherType launcherType, string manualPath, string arguments)
        {
            // Similar logic to AddEliteApp but updates existing app
            existingApp.Args = arguments;

            switch (launcherType)
            {
                case EliteLauncherDialog.LauncherType.Standard:
                    var paths = await ScanForEdLaunch();
                    if (paths.Count > 0)
                    {
                        existingApp.ExeName = "edlaunch.exe";
                        existingApp.Path = System.IO.Path.GetDirectoryName(paths[0]);
                        existingApp.WebAppURL = null;
                    }
                    break;

                case EliteLauncherDialog.LauncherType.Steam:
                    existingApp.WebAppURL = "steam://rungameid/359320";
                    existingApp.ExeName = null;
                    existingApp.Path = null;
                    break;

                case EliteLauncherDialog.LauncherType.Epic:
                    existingApp.WebAppURL = "com.epicgames.launcher://apps/9C203B6ED35846E8A4A9FF1E45A45B19%3A0a2d9f6403244d12969e11da6713137b%3A9C203B6ED35846E8A4A9FF1E45A45B19?action=launch&silent=true";
                    existingApp.ExeName = null;
                    existingApp.Path = null;
                    break;

                case EliteLauncherDialog.LauncherType.Legendary:
                    existingApp.WebAppURL = "legendary://launch";
                    existingApp.ExeName = null;
                    existingApp.Path = null;
                    break;

                case EliteLauncherDialog.LauncherType.Manual:
                    if (!string.IsNullOrEmpty(manualPath) && File.Exists(manualPath))
                    {
                        existingApp.ExeName = System.IO.Path.GetFileName(manualPath);
                        existingApp.Path = System.IO.Path.GetDirectoryName(manualPath);
                        existingApp.WebAppURL = null;
                    }
                    break;
            }

            await _viewModel.SaveProfilesAsync();
        }

        private async Task<List<string>> ScanForEdLaunch()
        {
            // Simplified version - just check common locations
            List<string> foundPaths = new List<string>();
            string[] commonLocations = new string[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Frontier", "EDLaunch", "edlaunch.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Elite Dangerous", "edlaunch.exe")
            };

            foreach (var location in commonLocations)
            {
                if (File.Exists(location))
                {
                    foundPaths.Add(location);
                }
            }

            return foundPaths;
        }

        private bool IsEliteApp(MyApp app)
        {
            if (app == null)
                return false;

            if (!string.IsNullOrEmpty(app.ExeName) &&
                (app.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase) ||
                 app.ExeName.Equals("EliteDangerous64.exe", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(app.WebAppURL) &&
                (app.WebAppURL.Contains("rungameid/359320") ||
                 app.WebAppURL.Contains("com.epicgames.launcher://apps") ||
                 app.WebAppURL.Contains("legendary://launch")))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(app.Name))
            {
                string name = app.Name.Trim();
                if (name.Equals("Elite Dangerous", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (name.StartsWith("Elite Dangerous (", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(")", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #endregion

        #region What's New Dialog

        private void ShowWhatsNewIfUpdated()
        {
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var lastSeenVersion = Properties.Settings.Default.LastSeenVersion;

            if (string.IsNullOrEmpty(lastSeenVersion) || new Version(lastSeenVersion) < assemblyVersion)
            {
                ShowWhatsNew();
                Properties.Settings.Default.LastSeenVersion = assemblyVersion.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void ShowWhatsNew()
        {
            var whatsNewWindow = new WhatsNewWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // Configure what's new content
            Paragraph titleParagraph = new Paragraph();
            titleParagraph.Inlines.Add(new Bold(new Run("New for this version")));
            whatsNewWindow.WhatsNewText.Document.Blocks.Add(titleParagraph);

            List list = new List();
            ListItem listItem1 = new ListItem(new Paragraph(new Run("Refactored to MVVM architecture with proper separation of concerns")));
            list.ListItems.Add(listItem1);
            ListItem listItem2 = new ListItem(new Paragraph(new Run("Settings now saved to application-specific folder with automatic migration")));
            list.ListItems.Add(listItem2);

            whatsNewWindow.WhatsNewText.Document.Blocks.Add(list);
            whatsNewWindow.ShowDialog();
        }

        #endregion

        #region DataGrid Events

        private void AddonDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Save after edit
            _ = _viewModel.SaveProfilesAsync();
        }

        private void AddonDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Context menu is defined in XAML
        }

        private async void DefaultCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentProfile == null)
                return;

            // Uncheck all other profiles
            foreach (var profile in AppState.Instance.Profiles)
            {
                if (profile != _viewModel.CurrentProfile)
                {
                    profile.IsDefault = false;
                }
            }

            _viewModel.CurrentProfile.IsDefault = true;
            await _viewModel.SaveProfilesAsync();
        }

        private async void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentProfile != null)
            {
                _viewModel.CurrentProfile.IsDefault = false;
                await _viewModel.SaveProfilesAsync();
            }
        }

        private void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is Profile targetProfile &&
                _viewModel.SelectedApp != null)
            {
                // Copy app to target profile
                var copiedApp = _viewModel.SelectedApp.DeepCopy();
                targetProfile.Apps.Add(copiedApp);
                _ = _viewModel.SaveProfilesAsync();

                CustomDialog.Show($"App copied to profile '{targetProfile.Name}'", "Success", MessageBoxButton.OK, this);
            }
        }

        #endregion

        #region Drag and Drop

        public void DragOver(IDropInfo dropInfo)
        {
            // Handled by ProfileDropHandler
        }

        public void Drop(IDropInfo dropInfo)
        {
            // Handled by ProfileDropHandler
            _ = _viewModel.SaveProfilesAsync();
        }

        #endregion

        #region Public Methods for External Access (AddApp, etc.)

        /// <summary>
        /// Public method for other dialogs to save profiles
        /// </summary>
        public async Task SaveProfilesAsync()
        {
            await _viewModel.SaveProfilesAsync();
        }

        /// <summary>
        /// Public method for backward compatibility - data binding handles updates automatically
        /// </summary>
        public void UpdateDataGrid()
        {
            // With proper MVVM binding, the DataGrid updates automatically
            // This method is kept for compatibility but does nothing
            Log.Debug("UpdateDataGrid called - no action needed with MVVM bindings");
        }

        #endregion

        #region ViewModel Command Handlers

        // Launch button now uses Command binding directly
        // Window minimization is handled by subscribing to ViewModel property changes

        #endregion
    }
}
