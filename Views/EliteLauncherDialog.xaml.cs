using Elite_Dangerous_Addon_Launcher_V2.ViewModels;
using System.Windows;

namespace Elite_Dangerous_Addon_Launcher_V2.Views
{
    /// <summary>
    /// Refactored EliteLauncherDialog following MVVM pattern
    /// </summary>
    public partial class EliteLauncherDialog : Window
    {
        private readonly EliteLauncherDialogViewModel _viewModel;

        public enum LauncherType
        {
            Standard,
            Steam,
            Epic,
            Legendary,
            Manual
        }

        public EliteLauncherDialog(bool isEditing = false, MyApp existingApp = null, string preferredLauncherType = "Standard")
        {
            InitializeComponent();

            // Create ViewModel
            _viewModel = new EliteLauncherDialogViewModel(isEditing, existingApp, preferredLauncherType);

            // Set DataContext
            this.DataContext = _viewModel;
        }

        // Public properties for accessing ViewModel data after dialog closes
        public LauncherType SelectedLauncher => _viewModel.SelectedLauncher;
        public string ManualPath => _viewModel.ManualPath;
        public bool UseAutoRun => _viewModel.UseAutoRun;
        public bool UseAutoQuit => _viewModel.UseAutoQuit;
        public bool UseVrMode => _viewModel.UseVrMode;

        public string GetArgumentsString()
        {
            return _viewModel.GetArgumentsString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

