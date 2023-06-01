using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        private bool _itemRemoved = false;
        private T _removedItem = default;

        public event Action<T> ItemMoved;

        public void MoveItem(T item, int newIndex)
        {
            var oldIndex = this.IndexOf(item);
            this.Move(oldIndex, newIndex);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                    _itemRemoved = true;
                    _removedItem = (T)e.OldItems[0];  // Assuming single item removal
                    break;

                case NotifyCollectionChangedAction.Add:
                    if (_itemRemoved && e.NewItems[0].Equals(_removedItem))
                    {
                        // If an item was just removed and the same item was added, treat it as a move.
                        ItemMoved?.Invoke(_removedItem);
                        _itemRemoved = false;
                        _removedItem = default;
                    }
                    break;
            }
        }
    }



}
