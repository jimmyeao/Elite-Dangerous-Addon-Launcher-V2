using System;
using System.Collections.Generic;
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
        private ObservableRangeCollection<MyApp> _apps;
        private bool _selected;
        private bool _isDefault;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged();
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDefault)));
                }
            }
        }

        public ObservableRangeCollection<MyApp> Apps
        {
            get { return _apps; }
            set
            {
                if (_apps != value)
                {
                    if (_apps != null)
                    {
                        _apps.ItemMoved -= Apps_ItemMoved;
                    }

                    _apps = value;

                    if (_apps != null)
                    {
                        _apps.ItemMoved += Apps_ItemMoved;
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public event Action ProfileChanged;

        public Profile()
        {
            Apps = new ObservableRangeCollection<MyApp>();
            Apps.ItemMoved += Apps_ItemMoved;
            Selected = false;
        }

        private void Apps_ItemMoved(MyApp movedApp)
        {
            ProfileChanged?.Invoke();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
