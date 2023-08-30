using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Elite_Dangerous_Addon_Launcher_V2
{
    public class MyApp : INotifyPropertyChanged
    {
        #region Private Fields

        private string _args;
        private string _exeName;
        private string _installationURL;
        private bool _isEnabled;
        private string _name;
        private int _order;
        private string _path;
        private string _webAppURL;

        #endregion Private Fields

        #region Public Events
        public MyApp DeepCopy()
        {
            return new MyApp
            {
                Args = this.Args,
                ExeName = this.ExeName,
                InstallationURL = this.InstallationURL,
                IsEnabled = this.IsEnabled,
                Name = this.Name,
                Order = this.Order,
                Path = this.Path,
                WebAppURL = this.WebAppURL
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

        public string Args
        {
            get { return _args; }
            set
            {
                if (_args != value)
                {
                    _args = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExeName
        {
            get { return _exeName; }
            set
            {
                if (_exeName != value)
                {
                    _exeName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InstallationURL
        {
            get { return _installationURL; }
            set
            {
                if (_installationURL != value)
                {
                    _installationURL = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Order
        {
            get { return _order; }
            set
            {
                if (_order != value)
                {
                    _order = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Path
        {
            get { return _path; }
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WebAppURL
        {
            get { return _webAppURL; }
            set
            {
                if (_webAppURL != value)
                {
                    _webAppURL = value;
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