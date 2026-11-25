using System;
using System.Windows.Input;

namespace AxisPanel
{
    /// <summary>把一个按钮绑定到一个方法。</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) { _execute = execute; }

        public event EventHandler CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute();
    }
}
