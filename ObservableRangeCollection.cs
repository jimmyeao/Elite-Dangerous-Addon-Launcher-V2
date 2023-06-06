//using System;
//using System.Collections.ObjectModel;
//using System.Collections.Specialized;

//namespace Elite_Dangerous_Addon_Launcer_V2
//{
//   public class ObservableRangeCollection<T> : ObservableCollection<T>
//    {
//        #region Private Fields

//        private bool _itemRemoved = false;
//    private T _removedItem = default;

//    #endregion Private Fields

//    #region Public Events

//    public event Action<T> ItemMoved;

//    #endregion Public Events

//    #region Public Methods

//    public void MoveItem(T item, int newIndex)
//    {
//        var oldIndex = this.IndexOf(item);
//        this.Move(oldIndex, newIndex);
//    }

//    #endregion Public Methods

//    #region Protected Methods

//    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
//    {
//        base.OnCollectionChanged(e);

//        switch (e.Action)
//        {
//            case NotifyCollectionChangedAction.Remove:
//                _itemRemoved = true;
//                _removedItem = (T)e.OldItems[0];  // Assuming single item removal
//                break;

//            case NotifyCollectionChangedAction.Add:
//                if (_itemRemoved && e.NewItems[0].Equals(_removedItem))
//                {
//                    // If an item was just removed and the same item was added, treat it as a move.
//                    ItemMoved?.Invoke(_removedItem);
//                    _itemRemoved = false;
//                    _removedItem = default;
//                }
//                break;
//        }
//    }

//    #endregion Protected Methods
//}
//}