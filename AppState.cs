using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Elite_Dangerous_Addon_Launcher_V2
{
    public class AppState : INotifyPropertyChanged
    {
        #region Private Fields

        private static AppState _instance;

        // Modify CloseAllAppsOnExit property to implement INotifyPropertyChanged
        private bool _closeAllAppsOnExit;

        private Profile _currentProfile;

        private ObservableCollection<Profile> _profiles;

        private MyApp _selectedApp;

        #endregion Private Fields

        #region Private Constructors
        public IEnumerable<Profile> OtherProfiles
        {
            get
            {
                if (Profiles == null || CurrentProfile == null)
                {
                    return Enumerable.Empty<Profile>();
                }

                return Profiles.Except(new[] { CurrentProfile });
            }
        }

        private AppState()
        {
            Apps = new ObservableCollection<MyApp>();
            Profiles = new ObservableCollection<Profile>();
            CloseAllAppsOnExit = false;
        }

        #endregion Private Constructors

        #region Public Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

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

        public ObservableCollection<MyApp> Apps { get; set; }

        public bool CloseAllAppsOnExit
        {
            get { return _closeAllAppsOnExit; }
            set
            {
                if (_closeAllAppsOnExit != value)
                {
                    _closeAllAppsOnExit = value;
                    OnPropertyChanged();
                }
            }
        }
        public MyApp CurrentApp { get; set; }


        public Profile CurrentProfile
        {
            get { return _currentProfile; }
            set
            {
                if (_currentProfile != value)
                {
                    _currentProfile = value;
                    OnPropertyChanged();
                    // Notify that OtherProfiles may have changed when CurrentProfile changes
                    OnPropertyChanged(nameof(OtherProfiles));
                }
            }
        }

        public ObservableCollection<Profile> Profiles
        {
            get { return _profiles; }
            set
            {
                if (_profiles != value)
                {
                    _profiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public MyApp SelectedApp
        {
            get { return _selectedApp; }
            set
            {
                if (_selectedApp != value)
                {
                    _selectedApp = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion Public Properties

        #region Protected Methods

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Protected Methods
    }
}