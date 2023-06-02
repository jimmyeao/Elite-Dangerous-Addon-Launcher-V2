using System;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Newtonsoft.Json;


namespace Elite_Dangerous_Addon_Launcer_V2

{
    public class AppState : INotifyPropertyChanged
    {
        #region Private Fields
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        // Singleton pattern implementation.
        private static AppState _instance;
        public static AppState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AppState();
                }

                return _instance;
            }
        }

        public Profile CurrentProfile { get; set; }
        private ObservableCollection<Profile> _profiles;

        #endregion Private Fields

        #region Private Constructors

        private AppState()
        {
            Profiles = new ObservableCollection<Profile>();
        }

        #endregion Private Constructors

        #region Public Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties


        public ObservableCollection<Profile> Profiles
        {
            get { return _profiles; }
            set
            {
                if (_profiles != value)
                {
                    _profiles = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #endregion Public Properties

        #region Private Methods

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Private Methods
    }

    public partial class MainWindow : Window
    {
        #region Private Fields

        private ObservableCollection<Profile> _profiles;
        private static object _lock = new object();
        public MainWindowViewModel MainWindowViewModel { get; set; }
        public static MainWindow Instance { get; private set; }
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public MainWindow()
        {
            InitializeComponent();

            // Create a new instance of MainWindow State
            MainWindowViewModel = new MainWindowViewModel();

            // Set the ItemsSource of the ComboBox to the Profiles in MainWindow State
            // This should now be done through XAML binding, not explicitly in code-behind
            //Cb_Profiles.ItemsSource = MainWindow State.Profiles;
            this.DataContext = MainWindowViewModel;
            // Assign the event handler to the Loaded event
            this.Loaded += MainWindow_Loaded;

            // Set the instance of MainWindow  to this
            Instance = this;

            if (Properties.Settings.Default.CloseAllAppsOnExit)
            {
                CloseAllAppsCheckbox.IsChecked = Properties.Settings.Default.CloseAllAppsOnExit;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load the profiles asynchronously and then update the ListView
            await LoadProfilesAsync();
            UpdateListView();

            // If there are no profiles in AppState
            if (!AppState.Instance.Profiles.Any())
            {
                // Create a new profile called "Default" and set it to be the default
                var defaultProfileInit = new Profile { Name = "Default", IsDefault = true };
                // Add the profile to AppState
                AppState.Instance.Profiles.Add(defaultProfileInit);
                // Set the selected profile to the default profile
                AppState.Instance.CurrentProfile = defaultProfileInit;
                // Save the new profile
                await SaveProfilesAsync();
            }

            // Set the ComboBox's selected item to the default profile
            Profile defaultProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);
            if (defaultProfile != null)
            {
                Cb_Profiles.SelectedItem = defaultProfile;
            }
            AppState.Instance.CurrentProfile.ProfileChanged += HandleProfileChanged;
        }




        #endregion Public Constructors

        #region Public Properties

        public MainWindow State { get; set; }

        #endregion Public Properties

        #region Private Methods
        public void UpdateAddonListView()
        {
            // Refresh the ItemsSource of your ListView
            addonListView.ItemsSource = null;
            if (AppState.Instance.CurrentProfile != null)
            {
                addonListView.ItemsSource = AppState.Instance.CurrentProfile.Apps;
            }
        }
        public void UpdateSelectedProfile(Profile profile)
        {
            if (AppState.Instance.CurrentProfile != null)
            {
                AppState.Instance.CurrentProfile.ProfileChanged -= Instance.HandleProfileChanged;
            }

            AppState.Instance.CurrentProfile = profile;

            if (AppState.Instance.CurrentProfile != null)
            {
                AppState.Instance.CurrentProfile.ProfileChanged += async () => await SaveProfilesAsync();
            }
        }



        public async Task SaveProfilesAsync()
        {
            await semaphore.WaitAsync();

            try
            {
                // Serialize the profiles into a JSON string
                var profilesJson = JsonConvert.SerializeObject(AppState.Instance.Profiles);

                // Create a file called "profiles.json" in the local folder
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "profiles.json");

                // Write the JSON string to the file
                await File.WriteAllTextAsync(path, profilesJson);
            }
            finally
            {
                semaphore.Release();
            }
        }


        public async void HandleProfileChanged()
        {
            if (AppState.Instance.CurrentProfile != null)
            {
                await SaveProfilesAsync();
            }
        }



        private void Bt_AddApp_Click(object sender, RoutedEventArgs e)
        {
            var selectedProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);
            if (selectedProfile != null)
            {
                // Open new Window or Dialog here. You will need to implement the window/dialog first.
            }
            else
            {
                // Handle the case when no profile is selected.
            }
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

        public async Task LaunchApp(MyApp app)
        {
            if (!string.IsNullOrEmpty(app.WebAppURL))
            {
                // Launch web app
                Process.Start(new ProcessStartInfo(app.WebAppURL) { UseShellExecute = true });
            }
            else
            {
                // Launch local app
                if (File.Exists(app.Path))
                {
                    // Prepare the arguments.
                    string arguments = app.Args;

                    // Launch the app.
                    Process.Start(new ProcessStartInfo(app.Path, arguments));
                }
                else
                {
                    // Handle the case when the file does not exist.
                }
            }
        }



        private MyApp JsonToMyApp(string jsonString)
        {
            // You will need to implement this function to convert a JSON string to a MyApp instance
            throw new NotImplementedException();
        }

        private void Bt_AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddProfileDialog();  // Assuming you have created this dialog window

            if (window.ShowDialog() == true)
            {
                string profileName = window.ProfileName;  // Assuming you have a property to get the TextBox Text
                var newProfile = new Profile { Name = profileName };
                AppState.Instance.Profiles.Add(newProfile);

                AppState.Instance.CurrentProfile = newProfile;

                SaveProfilesAsync();
            }
        }


        private async void Bt_RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (Cb_Profiles.SelectedItem is Profile selectedProfile)
            {
                AppState.Instance.Profiles.Remove(selectedProfile);
                await SaveProfilesAsync();

                // Select the 'Default' profile if it exists or the first one
                Profile defaultProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.Name == "Default")
                                         ?? AppState.Instance.Profiles.FirstOrDefault();

                Cb_Profiles.SelectedItem = defaultProfile;
                await SaveProfilesAsync();
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            var app = (MyApp)checkBox.DataContext;
            app.IsEnabled = true;
            SaveProfilesAsync();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            var app = (MyApp)checkBox.DataContext;
            if (app != null)
            {
                app.IsEnabled = false;
                SaveProfilesAsync();
            }

        }



        private void Cb_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Cb_Profiles.SelectedItem is Profile selectedProfile)
            {
                // Deselect the previously selected profile
                var previouslySelectedProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.Selected);
                if (previouslySelectedProfile != null)
                {
                    previouslySelectedProfile.Selected = false;
                    Debug.WriteLine($"Deselected profile: {previouslySelectedProfile.Name}");
                }

                // Fetch the name of the currently selected profile
                var selectedProfileName = selectedProfile.Name;

                // Select the currently selected profile
                var currentSelectedProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.Name == selectedProfileName);
                if (currentSelectedProfile != null)
                {
                    currentSelectedProfile.Selected = true;
                    Debug.WriteLine($"Selected profile: {currentSelectedProfile.Name}");
                    UpdateListView();
                    DefaultCheckBox.IsChecked = selectedProfile.IsDefault;
                }
            }
        }

        private void DefaultCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            // Check if the currently selected item in the Cb_Profiles ComboBox is a Profile object
            if (Cb_Profiles.SelectedItem is Profile selectedProfile)
            {
                // Find the previously default profile in the AppState.Instance.Profiles collection
                var previouslyDefaultProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.IsDefault);

                // If a previously default profile was found, set its IsDefault property to false
                if (previouslyDefaultProfile != null)
                {
                    previouslyDefaultProfile.IsDefault = false;
                }

                // Set the IsDefault property of the currently selected profile to true
                selectedProfile.IsDefault = true;
            }
        }

        private void CloseAllAppsCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            // Assuming that you have a CloseAllAppsOnExit property in your Settings
            Properties.Settings.Default.CloseAllAppsOnExit = true;
            Properties.Settings.Default.Save();
        }


        private void CloseAllAppsCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Assuming that you have a CloseAllAppsOnExit property in your Settings
            Properties.Settings.Default.CloseAllAppsOnExit = false;
            Properties.Settings.Default.Save();
        }



        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // This code checks if the source of the event is a button and if the button has a tag of type MyApp.
            // If so, it opens the AddApp window, passing in the selected profile, the main page, and the MyApp object.

            // Check if the source of the event is a button
            if (e.OriginalSource is Button button)
            {
                // Check if the button has a tag of type MyApp
                if (button.Tag is MyApp app)
                {
                    // Instantiate the AddApp window, passing in the selected profile, the main page, and the MyApp object
                    var addAppWindow = new AddApp()
                    {
                        MainPageReference = this, // Assuming you have this property to set the reference to the main window
                        SelectedProfile = MainWindowViewModel.SelectedProfile, // Assuming you have this property to set the selected profile
                        AppToEdit = app // Assuming you have this property to set the app to edit
                    };

                    // Open the AddApp window
                    addAppWindow.ShowDialog();
                }
            }
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
                    ObservableCollection<Profile> loadedProfiles = JsonConvert.DeserializeObject<ObservableCollection<Profile>>(json);

                    // Set the loaded profiles to AppState.Instance.Profiles
                    AppState.Instance.Profiles = loadedProfiles ?? new ObservableCollection<Profile>();
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


        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the MyApp object associated with the clicked button
            MyApp appToRemove = (MyApp)((Button)sender).Tag;

            // Remove the MyApp object from the list
            var currentProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.Selected);
            if (currentProfile != null)
            {
                currentProfile.Apps.Remove(appToRemove);
            }
            await SaveProfilesAsync();

            // Update the ListView
            UpdateListView();
        }

        private void UpdateListView()
        {
            // Check if Profiles have been initialized and there's a selected profile
            if (AppState.Instance.Profiles != null)
            {
                var selectedProfile = AppState.Instance.Profiles.FirstOrDefault(p => p.Selected);

                if (selectedProfile != null)
                {
                    addonListView.ItemsSource = selectedProfile.Apps;
                }
                else
                {
                    // No profile is selected. Clear the list view.
                    addonListView.ItemsSource = null;
                }
            }
            else
            {
                // Profiles is null. Clear the list view.
                addonListView.ItemsSource = null;
            }
        }


        #endregion Private Methods
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private ObservableCollection<Profile> _profiles;
        private Profile _selectedProfile;

        #endregion Private Fields

        #region Public Constructors

        public MainWindowViewModel()
        {
            AppState.Instance.PropertyChanged += AppState_PropertyChanged;
            _profiles = new ObservableCollection<Profile>();
        }

        #endregion Public Constructors

        #region Public Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

        public ObservableCollection<Profile> Profiles
        {
            get { return _profiles; }
            set
            {
                if (_profiles != value)
                {
                    _profiles = value;
                    OnPropertyChanged(nameof(Profiles));
                }
            }
        }

        public Profile SelectedProfile
        {
            get { return _selectedProfile; }
            set
            {
                if (_selectedProfile != value)
                {
                    _selectedProfile = value;
                    OnPropertyChanged(nameof(SelectedProfile));
                }
            }
        }

        #endregion Public Properties

        #region Private Methods

        private void AppState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppState.Instance.Profiles))
            {
                Profiles = AppState.Instance.Profiles;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Private Methods
    }

}
