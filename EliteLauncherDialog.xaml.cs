using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Elite_Dangerous_Addon_Launcher_V2
{
    public partial class EliteLauncherDialog : Window, INotifyPropertyChanged
    {
        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private LauncherType _selectedLauncher;
        public LauncherType SelectedLauncher
        {
            get => _selectedLauncher;
            private set
            {
                _selectedLauncher = value;
                OnPropertyChanged();
            }
        }
        public enum LauncherType
        {
            Standard,
            Steam,
            Epic,
            Legendary,
            Manual
        }

    
        public string ManualPath { get; private set; }
        public bool UseAutoRun { get; private set; }
        public bool UseAutoQuit { get; private set; }
        public bool UseVrMode { get; private set; }

        private bool _isEditing;
        private MyApp _existingApp;

        public EliteLauncherDialog(bool isEditing = false, MyApp existingApp = null)
        {
            InitializeComponent();
            _isEditing = isEditing;
            _existingApp = existingApp;

            // Set the title based on whether we're adding or editing
            this.Title = isEditing ? "Edit Elite Dangerous" : "Add Elite Dangerous";

            // Set the message text based on whether we're adding or editing
            MainMessageText.Text = isEditing
                ? "How would you like to launch Elite Dangerous?"
                : "Elite Dangerous was not found in this profile. How would you like to add it?";

            // Initialize with default values
            SelectedLauncher = LauncherType.Standard;
            UseAutoRun = true;
            UseAutoQuit = true;
            UseVrMode = false;

            // If editing, pre-populate with existing app settings
            if (isEditing && existingApp != null)
            {
                // Parse arguments
                if (!string.IsNullOrEmpty(existingApp.Args))
                {
                    UseAutoRun = existingApp.Args.Contains("/autorun");
                    UseAutoQuit = existingApp.Args.Contains("/autoquit");
                    UseVrMode = existingApp.Args.Contains("/vr");
                }

                // Update checkboxes after UI initialization
                Loaded += (s, e) => {
                    AutoRunCheckBox.IsChecked = UseAutoRun;
                    AutoQuitCheckBox.IsChecked = UseAutoQuit;
                    VrModeCheckBox.IsChecked = UseVrMode;
                };

                // Set launcher type
                if (!string.IsNullOrEmpty(existingApp.WebAppURL))
                {
                    if (existingApp.WebAppURL.Contains("steam://"))
                        SelectedLauncher = LauncherType.Steam;
                    else if (existingApp.WebAppURL.Contains("epic"))
                        SelectedLauncher = LauncherType.Epic;
                    else if (existingApp.WebAppURL.Contains("legendary"))
                        SelectedLauncher = LauncherType.Legendary;
                }
                else if (!string.IsNullOrEmpty(existingApp.ExeName))
                {
                    if (existingApp.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase))
                        SelectedLauncher = LauncherType.Standard;
                    else
                    {
                        SelectedLauncher = LauncherType.Manual;
                        ManualPath = Path.Combine(existingApp.Path, existingApp.ExeName);
                    }
                }
            }

            this.Loaded += EliteLauncherDialog_Loaded;
        }

        private void EliteLauncherDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Set appropriate UI based on selected launcher
            UpdateLauncherUI();

            // Subscribe to checkbox events after UI initialization
            AutoRunCheckBox.Checked += (s, evt) => UseAutoRun = true;
            AutoRunCheckBox.Unchecked += (s, evt) => UseAutoRun = false;

            AutoQuitCheckBox.Checked += (s, evt) => UseAutoQuit = true;
            AutoQuitCheckBox.Unchecked += (s, evt) => UseAutoQuit = false;

            VrModeCheckBox.Checked += (s, evt) => UseVrMode = true;
            VrModeCheckBox.Unchecked += (s, evt) => UseVrMode = false;
        }

        private void UpdateLauncherUI()
        {
           // Reset all button backgrounds first
           StandardButton.Background = null;
           SteamButton.Background = null;
           EpicButton.Background = null;
           LegendaryButton.Background = null;
           BrowseButton.Background = null;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            // Get the correct accent brush from resources - this is more theme-compliant
            var accentBrush = (Brush)Application.Current.TryFindResource("MaterialDesignPrimaryColorBrush") ??
                 (Brush)Application.Current.TryFindResource("MaterialDesignPrimaryMidBrush");

            // Highlight the selected launcher button
            switch (SelectedLauncher)
            {
                case LauncherType.Standard:
                    StandardButton.Background = accentBrush;
                    break;
                case LauncherType.Steam:
                    SteamButton.Background = accentBrush;
                    break;
                case LauncherType.Epic:
                    EpicButton.Background = accentBrush;
                    break;
                case LauncherType.Legendary:
                    LegendaryButton.Background = accentBrush;
                    break;
                case LauncherType.Manual:
                    BrowseButton.Background = accentBrush;
                    break;
            }

            // Show warning for Steam launcher
            SteamWarningText.Visibility = (SelectedLauncher == LauncherType.Steam)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void StandardButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedLauncher = LauncherType.Standard;
            UpdateLauncherUI();
        }

        private void SteamButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedLauncher = LauncherType.Steam;
            UpdateLauncherUI();
        }

        private void EpicButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedLauncher = LauncherType.Epic;
            UpdateLauncherUI();
        }

        private void LegendaryButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedLauncher = LauncherType.Legendary;
            UpdateLauncherUI();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Elite Dangerous|edlaunch.exe;EliteDangerous64.exe|All Executables|*.exe",
                Title = "Select Elite Dangerous Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                ManualPath = dialog.FileName;
                SelectedLauncher = LauncherType.Manual;
                UpdateLauncherUI();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Helper method to get formatted arguments string
        public string GetArgumentsString()
        {
            string args = "";
            if (UseAutoRun) args += " /autorun";
            if (UseAutoQuit) args += " /autoquit";
            if (UseVrMode) args += " /vr";
            return args.Trim();
        }
    }
}