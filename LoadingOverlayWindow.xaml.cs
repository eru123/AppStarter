using System.Windows;

namespace AppStarter;

public partial class LoadingOverlayWindow : Window
{
    public LoadingOverlayWindow(string message = "Stopping services...")
    {
        InitializeComponent();
        MessageText.Text = message;
    }
}
