using System.Windows;
using AppStarter.Models;

namespace AppStarter;

public partial class CommandDetailsWindow : Window
{
    public CommandConfig Command { get; }

    public CommandDetailsWindow(CommandConfig command)
    {
        InitializeComponent();
        Command = command;
        DataContext = Command;
    }
}
