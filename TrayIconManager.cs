using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using AppStarter.Helpers;
using AppStarter.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AppStarter;

/// <summary>
/// Manages the system tray icon and its context menu
/// </summary>
public class TrayIconManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly MainViewModel _viewModel;
    private readonly Window _mainWindow;
    private bool _disposed;
    private bool _isExiting;
    
    public TrayIconManager(Window mainWindow, MainViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
        
        InitializeTrayIcon();
    }
    
    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = IconGenerator.CreateSimpleBananaIcon(32),
            Text = "AppStarter - Process Manager",
            Visible = true
        };
        
        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        // Show/Hide Window
        var showItem = new ToolStripMenuItem("Show AppStarter");
        showItem.Click += (s, e) => ShowWindow();
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(showItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        // Start All
        var startAllItem = new ToolStripMenuItem("▶ Start All Commands");
        startAllItem.Click += async (s, e) => await _viewModel.StartAllCommandsCommand.ExecuteAsync(null);
        contextMenu.Items.Add(startAllItem);
        
        // Stop All
        var stopAllItem = new ToolStripMenuItem("■ Stop All Commands");
        stopAllItem.Click += async (s, e) => await _viewModel.StopAllCommandsCommand.ExecuteAsync(null);
        contextMenu.Items.Add(stopAllItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        // Running count
        var statusItem = new ToolStripMenuItem("Status: 0 commands running");
        statusItem.Enabled = false;
        contextMenu.Items.Add(statusItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        // Exit
        var exitItem = new ToolStripMenuItem("Exit AppStarter");
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);
        
        // Update status before showing
        contextMenu.Opening += (s, e) =>
        {
            var runningCount = _viewModel.Commands.Count(c => c.Status == Models.CommandStatus.Running);
            statusItem.Text = $"Status: {runningCount} command(s) running";
        };
        
        _notifyIcon.ContextMenuStrip = contextMenu;
        
        // Double-click to show window
        _notifyIcon.DoubleClick += (s, e) => ShowWindow();
    }
    
    public void ShowWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }
    
    public void HideWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _mainWindow.Hide();
        });
    }
    
    public void MinimizeToTray()
    {
        HideWindow();
        ShowBalloonTip("AppStarter minimized", "AppStarter is running in the background. Double-click the tray icon to open.");
    }
    
    public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
    }
    
    public void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        
        var runningCount = _viewModel.Commands.Count(c => c.Status == Models.CommandStatus.Running);
        
        if (runningCount > 0)
        {
            var result = MessageBox.Show(
                $"There are {runningCount} command(s) still running.\n\nDo you want to stop all commands and exit?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.No)
            {
                _isExiting = false;
                return;
            }
        }
        
        // Stop all processes and cleanup
        _viewModel.Cleanup();
        
        // Close the application
        Application.Current.Dispatcher.Invoke(() =>
        {
            Application.Current.Shutdown();
        });
    }
    
    public bool IsExiting => _isExiting;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
