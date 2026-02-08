using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace AppStarter
{
    public partial class AboutWindow : Window
    {
        public ICommand CheckUpdateCommand { get; }

        public AboutWindow()
        {
            InitializeComponent();
            CheckUpdateCommand = new RelayCommand(CheckUpdate);
            DataContext = this;

            // Custom Window Chrome Support
            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (s, e) => SystemCommands.CloseWindow(this)));
        }

        private void CheckUpdate()
        {
            System.Windows.MessageBox.Show("You are running the latest version (v1.0.0).", "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Simple RelayCommand helper
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
