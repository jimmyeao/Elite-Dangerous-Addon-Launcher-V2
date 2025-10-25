using System.Windows.Input;

namespace Elite_Dangerous_Addon_Launcher_V2.Commands
{
    /// <summary>
    /// Common interface for relay commands
    /// </summary>
    public interface IRelayCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }
}
