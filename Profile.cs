using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Elite_Dangerous_Addon_Launcher_V2
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

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            // Initialize DropHandler after deserialization
            if (DropHandler == null)
            {
                DropHandler = new ProfileDropHandler(this);
            }
        }

        public ObservableCollection<MyApp> Apps
        {
            get { return _apps; }
            set
            {
                if (_apps != value)
                {
                    _apps = value;
                    if (_apps != null)
                    {
                        _apps.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Apps));
                    }
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
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