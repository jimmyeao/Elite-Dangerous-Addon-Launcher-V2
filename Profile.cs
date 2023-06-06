using GongSolutions.Wpf.DragDrop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    public class Profile : INotifyPropertyChanged
    {
        #region Private Fields

        private ObservableCollection<MyApp> _apps;
        private bool _isDefault;
        private string _name;

        #endregion Private Fields

        #region Public Constructors

        public Profile()
        {
            Apps = new ObservableCollection<MyApp>();
            Apps.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Apps));
            DropHandler = new ProfileDropHandler(this);
        }

        #endregion Public Constructors

        #region Public Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

        public ObservableCollection<MyApp> Apps
        {
            get { return _apps; }
            set
            {
                if (_apps != value)
                {
                    _apps = value;
                    OnPropertyChanged();
                }
            }
        }

        public IDropTarget DropHandler { get; private set; }

        public bool IsDefault
        {
            get { return _isDefault; }
            set
            {
                if (_isDefault != value)
                {
                    _isDefault = value;
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

        #endregion Public Properties

        #region Protected Methods

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Protected Methods
    }
}