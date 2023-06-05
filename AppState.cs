using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    public class AppState : INotifyPropertyChanged
    {
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

        public ObservableCollection<MyApp> Apps { get; set; }

        // Modify CloseAllAppsOnExit property to implement INotifyPropertyChanged
        private bool _closeAllAppsOnExit;
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

        private ObservableCollection<Profile> _profiles;
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

        private Profile _currentProfile;
        public Profile CurrentProfile
        {
            get { return _currentProfile; }
            set
            {
                if (_currentProfile != value)
                {
                    _currentProfile = value;
                    OnPropertyChanged();
                }
            }
        }

        private AppState()
        {
            Apps = new ObservableCollection<MyApp>();
            Profiles = new ObservableCollection<Profile>();
            CloseAllAppsOnExit = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
