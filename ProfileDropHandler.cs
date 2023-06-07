using GongSolutions.Wpf.DragDrop;
using System.Windows;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    public class ProfileDropHandler : IDropTarget
    {
        #region Private Fields

        private Profile _profile;

        #endregion Private Fields

        #region Public Constructors

        public ProfileDropHandler(Profile profile)
        {
            _profile = profile;
        }

        #endregion Public Constructors

        #region Public Methods

        public void DragOver(IDropInfo dropInfo)
        {
            var sourceItem = dropInfo.Data as MyApp;
            var targetItem = dropInfo.TargetItem as MyApp;

            if (sourceItem != null && targetItem != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var sourceItem = dropInfo.Data as MyApp;
            var targetItem = dropInfo.TargetItem as MyApp;

            if (sourceItem != null && targetItem != null)
            {
                int sourceIndex = _profile.Apps.IndexOf(sourceItem);
                int targetIndex = _profile.Apps.IndexOf(targetItem);

                // Remove and insert the source item at the correct location
                _profile.Apps.RemoveAt(sourceIndex);
                _profile.Apps.Insert(targetIndex, sourceItem);

                // Update the order property of all apps
                for (int i = 0; i < _profile.Apps.Count; i++)
                {
                    _profile.Apps[i].Order = i;
                }
            }
        }

        #endregion Public Methods
    }
}