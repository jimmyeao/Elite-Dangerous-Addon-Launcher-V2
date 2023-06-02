using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    /// <summary>
    /// Interaction logic for AddApp.xaml
    /// </summary>
    public partial class AddApp : Window
    {
        public MainWindow MainPageReference { get; set; }
        public Profile SelectedProfile { get; set; }
        public MyApp AppToEdit { get; set; }
        public List<MyApp> MyAppList { get; set; }

        public AddApp()
        {
            InitializeComponent();
            MyAppList = new List<MyApp>();  // initialize the list

            this.Loaded += AddApp_Loaded;
        }

        private void AddApp_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppToEdit != null)
            {
                Tb_App_Name.Text = AppToEdit.Name;
                Tb_AppPath.Text = AppToEdit.Path;
                Tb_App_Args.Text = AppToEdit.Args;
                Tb_InstallationURL.Text = AppToEdit.InstallationURL;
                Tb_AppExeName.Text = AppToEdit.ExeName;
                Tb_WebApURL.Text = AppToEdit.WebAppURL;
                Cb_Enable.IsChecked = AppToEdit.IsEnabled;
            }
        }

        private void Bt_BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string fullPath = openFileDialog.FileName;

                var fileName = Path.GetFileName(fullPath);
                var directory = Path.GetDirectoryName(fullPath);

                Tb_AppPath.Text = directory;
                Tb_AppExeName.Text = fileName;
            }
        }

        private void addApp(object sender, RoutedEventArgs e)
        {
            var appName = Tb_App_Name.Text;
            var appPath = Tb_AppPath.Text;
            var appArgs = Tb_App_Args.Text;
            var installationURL = Tb_InstallationURL.Text;
            var exeName = Tb_AppExeName.Text;
            var webAppURL = Tb_WebApURL.Text;
            var isEnabled = Cb_Enable.IsChecked;

            if (AppToEdit != null)
            {
                AppToEdit.Name = appName;
                AppToEdit.Path = appPath;
                AppToEdit.Args = appArgs;
                AppToEdit.InstallationURL = installationURL;
                AppToEdit.ExeName = exeName;
                AppToEdit.WebAppURL = webAppURL;
                AppToEdit.IsEnabled = isEnabled.HasValue ? isEnabled.Value : false;
            }
            else
            {
                var newApp = new MyApp
                {
                    Name = appName,
                    Path = appPath,
                    Args = appArgs,
                    InstallationURL = installationURL,
                    ExeName = exeName,
                    WebAppURL = webAppURL,
                    IsEnabled = isEnabled.HasValue ? isEnabled.Value : false,
                };

                newApp.PropertyChanged += MyApp_PropertyChanged;

                SelectedProfile.Apps.Add(newApp);

                // Handle the case when no profile is selected.
            }

            // Refresh the ItemsSource of your ListView
            MainPageReference.UpdateAddonListView();

            this.Close(); // Close the current window after the app is added.
        }

        private void MyApp_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MyApp.IsEnabled))
            {
                MainPageReference.SaveProfilesAsync();
            }
        }

        private void cancelButton(object sender, RoutedEventArgs e)
        {
            // Closes the current window without adding a new app, or clearing the text boxes, etc.
            this.Close();
        }
    }
}
