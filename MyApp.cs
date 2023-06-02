using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    public class MyApp : INotifyPropertyChanged
    {
        private string _name;
        private string _path;
        private string _args;
        private string _installationURL;
        private string _exeName;
        private string _webAppURL;
        private bool _isEnabled;

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



}