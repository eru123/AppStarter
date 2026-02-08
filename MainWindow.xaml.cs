using System.Windows;
using AppStarter.ViewModels;

using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AppStarter.Helpers;

namespace AppStarter;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private TrayIconManager? _trayManager;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Handling custom title bar commands
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SystemCommands.CloseWindowCommand, (s, e) => SystemCommands.CloseWindow(this)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SystemCommands.MaximizeWindowCommand, (s, e) => SystemCommands.MaximizeWindow(this)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SystemCommands.MinimizeWindowCommand, (s, e) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SystemCommands.RestoreWindowCommand, (s, e) => SystemCommands.RestoreWindow(this)));
        
        // Set window icon programmatically
        try
        {
            using (var icon = IconGenerator.CreateBananaIcon(64))
            {
                this.Icon = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
        }
        
        _viewModel = DataContext as MainViewModel;
        
        if (_viewModel != null)
        {
            // Initialize tray icon only if Admin
            if (App.IsAdmin)
            {
                _trayManager = new TrayIconManager(this, _viewModel);
            }
            
            // Auto-scroll logs
            _viewModel.Logs.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        try 
                        {
                            LogList.ScrollIntoView(e.NewItems[e.NewItems.Count - 1]);
                        }
                        catch { } // Ignore scrolling errors
                    });
                }
            };
        }
        
        // Handle window closing
        Closing += MainWindow_Closing;
        
        // Handle minimize to tray
        StateChanged += MainWindow_StateChanged;
    }
    
    private LogsWindow? _logsWindow;

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Don't auto-hide/show if user explicitly toggled logs, 
        // unless we forced it? For now, keep the simple responsive logic.
        if (ActualHeight < 600)
        {
            LogsRow.Height = new GridLength(0);
            LogsRow.MinHeight = 0;
            LogsSplitter.Visibility = Visibility.Collapsed;
            LogsSection.Visibility = Visibility.Collapsed;
            LogsButton.Visibility = Visibility.Visible;
        }
        else
        {
            if (LogsRow.Height.Value == 0)
            {
                LogsRow.Height = new GridLength(1, GridUnitType.Star);
                LogsRow.MinHeight = 150;
            }
            LogsSplitter.Visibility = Visibility.Visible;
            LogsSection.Visibility = Visibility.Visible;
            LogsButton.Visibility = Visibility.Collapsed;
            
            if (_logsWindow != null)
            {
                _logsWindow.Close();
                _logsWindow = null;
            }
        }
    }
    
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        // Force full exit
        if (_trayManager != null)
        {
            _trayManager.IsExiting = true;
        }
        Close();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow();
        about.Owner = this;
        about.ShowDialog();
    }

    private void ToggleLogs_Click(object sender, RoutedEventArgs e)
    {
        if (LogsSection.Visibility == Visibility.Visible)
        {
            // Hide
            LogsRow.Height = new GridLength(0);
            LogsRow.MinHeight = 0;
            LogsSplitter.Visibility = Visibility.Collapsed;
            LogsSection.Visibility = Visibility.Collapsed;
            LogsButton.Visibility = Visibility.Visible; // Optionally show button or just hide everything
        }
        else
        {
            // Show
            LogsRow.Height = new GridLength(1, GridUnitType.Star);
            LogsRow.MinHeight = 150;
            LogsSplitter.Visibility = Visibility.Visible;
            LogsSection.Visibility = Visibility.Visible;
            LogsButton.Visibility = Visibility.Collapsed;
        }
    }

    private void LogsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_logsWindow == null)
        {
            if (_viewModel != null)
            {
                _logsWindow = new LogsWindow(_viewModel);
                _logsWindow.Owner = this;
                _logsWindow.Closed += (s, args) => _logsWindow = null;
                _logsWindow.Show();
            }
        }
        else
        {
            _logsWindow.Activate();
            if (_logsWindow.WindowState == WindowState.Minimized)
            {
                _logsWindow.WindowState = WindowState.Normal;
            }
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // When minimized, hide to tray if enabled
        if (WindowState == WindowState.Minimized && _trayManager != null)
        {
            _trayManager.MinimizeToTray();
        }
    }
    
    private bool _isCleanExit = false;

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isCleanExit) 
        {
            // Final cleanup (quick dispose)
            _trayManager?.Dispose();
            // ViewModel cleanup already done or needs simple dispose
            return;
        }

        if (!App.IsAdmin)
        {
            var result = System.Windows.MessageBox.Show(
                "All running command process will stop.\nContinue to exit the application?",
                "Confirm Exit",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            // If Yes, proceed to exit
            e.Cancel = true;
            PerformExitAsync();
            return;
        }

        // If not explicitly exiting through tray menu, minimize to tray instead
        // Only if tray is active (and we are admin or otherwise allowed)
        if (_trayManager != null && !_trayManager.IsExiting)
        {
            e.Cancel = true;
            _trayManager.MinimizeToTray();
            
            // Also hide logs window if main is minimized to tray
            _logsWindow?.Hide();
            return;
        }
        
        // Final exit sequence (bypass tray check if we got here)
        e.Cancel = true;
        PerformExitAsync();
    }
    
    private void PerformExitAsync()
    {
        var overlay = new LoadingOverlayWindow("Stopping services...");
        overlay.Owner = this;
        
        // Center manually if needed, but WindowStartupLocation="CenterOwner" usually works
        // Make sure Owner is visible, otherwise CenterScreen
        if (this.Visibility != Visibility.Visible)
        {
             overlay.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        overlay.Show();
        
        Task.Run(async () =>
        {
            if (_viewModel != null)
            {
                await _viewModel.CleanupAsync();
            }
            
            Dispatcher.Invoke(() =>
            {
                _logsWindow?.Close();
                overlay.Close();
                _isCleanExit = true;
                Close();
            });
        });
    }
}