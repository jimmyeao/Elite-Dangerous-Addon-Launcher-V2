using System;
using System.Windows.Input;

namespace Elite_Dangerous_Addon_Launcher_V2.Commands
{
    /// <summary>
    /// A command whose sole purpose is to relay its functionality to other objects by invoking delegates.
    /// </summary>
    public class RelayCommand : IRelayCommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(
                execute != null ? new Action<object>(_ => execute()) : null,
                canExecute != null ? new Predicate<object>(_ => canExecute()) : null)
        {
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
