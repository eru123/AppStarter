using System.Windows;
using AppStarter.Models;
using MessageBox = System.Windows.MessageBox;

namespace AppStarter;

public partial class CommandEditorWindow : Window
{
    public CommandConfig Command { get; private set; }

    public CommandEditorWindow(CommandConfig commandToEdit)
    {
        InitializeComponent();
        
        // Clone the command so we don't edit the original directly until saved
        Command = CloneCommand(commandToEdit);
        DataContext = Command;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Command.Name))
        {
            MessageBox.Show("Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Command.Command))
        {
            MessageBox.Show("Command is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private CommandConfig CloneCommand(CommandConfig original)
    {
        return new CommandConfig
        {
            Id = original.Id, // Keep ID to update existing
            Name = original.Name,
            Description = original.Description,
            Command = original.Command,
            Arguments = original.Arguments,
            WorkingDirectory = original.WorkingDirectory,
            RestartPolicy = original.RestartPolicy,
            RestartDelaySeconds = original.RestartDelaySeconds,
            MaxRestartAttempts = original.MaxRestartAttempts,
            StartTrigger = original.StartTrigger,
            CronExpression = original.CronExpression,
            Enabled = original.Enabled,
            Priority = original.Priority,
            EnvironmentVariables = new Dictionary<string, string>(original.EnvironmentVariables),
            RunAsAdmin = original.RunAsAdmin,
            HideWindow = original.HideWindow,
            Status = original.Status // Keep status (though strictly config shouldn't have status, but the model has it)
        };
    }
}
