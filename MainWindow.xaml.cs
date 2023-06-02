using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Elite_Dangerous_Addon_Launcer_V2

{
  
    public class MainViewModel
    {
        public AppState AppStateInstance => AppState.Instance;
    }


    public partial class MainWindow : Window
    {
        #region Private Fields

        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

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

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProfilesAsync();
        }

        #endregion Private Fields

        #region Public Properties

      

        #endregion Public Properties

        #region Private Methods

         public async Task LaunchApp(MyApp app)
        {
          
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
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            var app = (MyApp)checkBox.Tag;
            if (app != null)
            {
                app.IsEnabled = false;
                _ = SaveProfilesAsync();
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

        private void Btn_Launch_Click(object sender, RoutedEventArgs e)
        {
            // Code to launch all enabled apps
            foreach (var app in AppState.Instance.CurrentProfile.Apps)
            {
                if (app.IsEnabled)
                {
                    _ = LaunchApp(app);
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
                    UpdateListView();
                    DefaultCheckBox.IsChecked = selectedProfile.IsDefault;
                }
            }
        }



        private void CloseAllAppsCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            AppState.Instance.CloseAllAppsOnExit = true;
            _ = SaveProfilesAsync();
        }

        private void CloseAllAppsCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppState.Instance.CloseAllAppsOnExit = false;
            _ = SaveProfilesAsync();
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

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button button)
            {
                if (button.Tag is MyApp app)
                {
                    var addAppWindow = new AddApp()
                    {
                        MainPageReference = this,
                        SelectedProfile = AppState.Instance.CurrentProfile,
                        AppToEdit = app
                    };
                    addAppWindow.ShowDialog();
                }
            }
        }

        private MyApp JsonToMyApp(string jsonString)
        {
            return JsonConvert.DeserializeObject<MyApp>(jsonString);
        }

        public void UpdateListView()
        {
            // Assuming 'AddonListView' is the name of your ListView control
            // Check if Profiles have been initialized and there's a selected profile
            if (AppState.Instance.Profiles != null)
            {
                var selectedProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);

                if (selectedProfile != null)
                {
                    AddonListView.ItemsSource = selectedProfile.Apps;
                }
                else
                {
                    // No profile is selected. Clear the list view.
                    AddonListView.ItemsSource = null;
                }
            }
            else
            {
                // Profiles is null. Clear the list view.
                AddonListView.ItemsSource = null;
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
                AddonListView.ItemsSource = AppState.Instance.CurrentProfile.Apps;

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
                UpdateListView();
            }
        }


        private void Bt_RemoveProfile_Click_1(object sender, RoutedEventArgs e)
        {
            Profile profileToRemove = (Profile)Cb_Profiles.SelectedItem;

            if (profileToRemove != null)
            {
                AppState.Instance.Profiles.Remove(profileToRemove);
                _ = SaveProfilesAsync();
                UpdateListView();
            }
        }


    }
    #endregion Private Methods

}