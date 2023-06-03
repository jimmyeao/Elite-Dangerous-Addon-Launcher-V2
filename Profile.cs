using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    public class Profile : INotifyPropertyChanged
    {
        private string _name;
        private bool _isDefault;
        private ObservableCollection<MyApp> _apps;
        public IDropTarget DropHandler { get; private set; }
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

        public Profile()
        {
            Apps = new ObservableCollection<MyApp>();
            DropHandler = new ProfileDropHandler(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


   


}
