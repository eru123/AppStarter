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
                "Do you want to stop all running services before exiting?",
                "Confirm Exit",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                e.Cancel = true;
                PerformExitAsync();
                return;
            }
            
            // If No, proceed to regular exit (will trigger tray check or final exit)
        }

        // If not explicitly exiting through tray menu, minimize to tray instead
        // Only if tray is active (and we are admin or otherwise allowed)
        if (_trayManager != null && !_trayManager.IsExiting)
        {
            e.Cancel = true;
            _trayManager.MinimizeToTray();
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
                overlay.Close();
                _isCleanExit = true;
                Close();
            });
        });
    }
}