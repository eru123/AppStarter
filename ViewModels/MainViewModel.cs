using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppStarter.Models;
using AppStarter.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AppStarter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly ProcessManager _processManager;
    private readonly LogService _logService;
    private readonly SchedulerService _schedulerService;
    private readonly WindowsServiceManager _serviceManager;
    private readonly DatabaseService _databaseService;
    
    [ObservableProperty]
    private ObservableCollection<CommandConfig> commands = new();
    
    [ObservableProperty]
    private CommandConfig? selectedCommand;
    
    [ObservableProperty]
    private ObservableCollection<LogEntry> logs = new();
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstallServiceVisible))]
    [NotifyPropertyChangedFor(nameof(IsUninstallServiceVisible))]
    private bool isServiceInstalled;
    
    public bool IsInstallServiceVisible => !IsServiceInstalled && IsUserAdmin;
    public bool IsUninstallServiceVisible => IsServiceInstalled && IsUserAdmin;

    [ObservableProperty]
    private string serviceStatus = "Unknown";
    
    [ObservableProperty]
    private string searchText = string.Empty;
    
    [ObservableProperty]
    private bool showOnlyRunning;

    public bool IsUserAdmin => App.IsAdmin;
    
    public MainViewModel()
    {
        _logService = new LogService();
        _databaseService = new Services.DatabaseService(_logService);
        _logService.SetDatabaseService(_databaseService);
        
        _configService = new ConfigService(_databaseService);
        _processManager = new ProcessManager(_logService);
        _schedulerService = new SchedulerService(_processManager, _logService);
        _serviceManager = new WindowsServiceManager(_logService);
        
        // Subscribe to log events
        _logService.LogAdded += OnLogAdded;
        
        // Subscribe to process events
        _processManager.ProcessStarted += (s, c) => Application.Current.Dispatcher.Invoke(() => RefreshCommandStatus(c));
        _processManager.ProcessStopped += (s, c) => Application.Current.Dispatcher.Invoke(() => RefreshCommandStatus(c));
        _processManager.ProcessExited += (s, e) => Application.Current.Dispatcher.Invoke(() => RefreshCommandStatus(e.Command));
        
        LoadCommands();
        UpdateServiceStatus();
        
        // Start scheduler
        _schedulerService.Start();
        
        // Start commands with OnAppStart trigger
        StartOnAppStartCommands();
    }
    
    private void OnLogAdded(object? sender, LogEntry entry)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Logs.Add(entry);
            
            // Keep only last 500 entries in UI
            while (Logs.Count > 500)
            {
                Logs.RemoveAt(0);
            }
        });
    }
    
    private void LoadCommands()
    {
        var config = _configService.Load();
        Commands.Clear();
        
        foreach (var command in config.Commands)
        {
            Commands.Add(command);
            
            // Schedule if needed
            if (command.StartTrigger.HasFlag(StartTrigger.Scheduled))
            {
                _schedulerService.ScheduleCommand(command);
            }
        }
    }
    
    private void RefreshCommandStatus(CommandConfig command)
    {
        // Force UI update
        var index = Commands.IndexOf(command);
        if (index >= 0)
        {
            OnPropertyChanged(nameof(Commands));
        }
    }
    
    private async void StartOnAppStartCommands()
    {
        foreach (var command in Commands.Where(c => c.Enabled && c.StartTrigger.HasFlag(StartTrigger.OnAppStart)))
        {
            await _processManager.StartAsync(command);
        }
    }
    
    [RelayCommand]
    private async Task StartCommand(CommandConfig? command)
    {
        if (command == null) return;
        await _processManager.StartAsync(command);
    }
    
    [RelayCommand]
    private async Task StopCommand(CommandConfig? command)
    {
        if (command == null) return;
        await _processManager.StopAsync(command);
    }
    
    [RelayCommand]
    private async Task RestartCommand(CommandConfig? command)
    {
        if (command == null) return;
        await _processManager.RestartAsync(command);
    }
    
    [RelayCommand]
    private void AddCommand()
    {
        var newCommand = new CommandConfig
        {
            Name = "New Command",
            Command = "cmd.exe",
            Arguments = "/c echo Hello World"
        };
        
        var dialog = new CommandEditorWindow(newCommand);
        if (dialog.ShowDialog() == true)
        {
            _configService.AddCommand(dialog.Command);
            Commands.Add(dialog.Command);
            SelectedCommand = dialog.Command;
        }
    }
    
    [RelayCommand]
    private void EditCommand(CommandConfig? command)
    {
        if (command == null) return;
        
        var dialog = new CommandEditorWindow(command);
        if (dialog.ShowDialog() == true)
        {
            // Update config
            _configService.UpdateCommand(dialog.Command);
            
            // Update list
            var index = Commands.IndexOf(command);
            if (index >= 0)
            {
                Commands[index] = dialog.Command;
            }
            
            // Update selection if needed
            if (SelectedCommand == command)
            {
                SelectedCommand = dialog.Command;
            }
            
            // Update scheduler
            if (dialog.Command.StartTrigger.HasFlag(StartTrigger.Scheduled))
            {
                _schedulerService.ScheduleCommand(dialog.Command);
            }
            else
            {
                _schedulerService.UnscheduleCommand(dialog.Command.Id);
            }
        }
    }

    [RelayCommand]
    private async Task DeleteCommand(CommandConfig? command)
    {
        if (command == null) return;
        
        var result = MessageBox.Show(
            $"Are you sure you want to delete '{command.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            await _processManager.StopAsync(command);
            _schedulerService.UnscheduleCommand(command.Id);
            _configService.RemoveCommand(command.Id);
            Commands.Remove(command);
            
            if (SelectedCommand == command)
            {
                SelectedCommand = null;
            }
        }
    }
    
    // SaveCommand removed as editing is now modal
    
    [RelayCommand]
    private void ViewDetails(CommandConfig? command)
    {
        if (command == null) return;
        
        var window = new CommandDetailsWindow(command);
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    [RelayCommand]
    private void DuplicateCommand(CommandConfig? command)
    {
        if (command == null) return;
        
        var clone = new CommandConfig
        {
            Name = $"{command.Name} (Copy)",
            Description = command.Description,
            Command = command.Command,
            Arguments = command.Arguments,
            WorkingDirectory = command.WorkingDirectory,
            RestartPolicy = command.RestartPolicy,
            RestartDelaySeconds = command.RestartDelaySeconds,
            MaxRestartAttempts = command.MaxRestartAttempts,
            StartTrigger = command.StartTrigger,
            CronExpression = command.CronExpression,
            Enabled = command.Enabled,
            Priority = command.Priority,
            EnvironmentVariables = new Dictionary<string, string>(command.EnvironmentVariables),
            RunAsAdmin = command.RunAsAdmin,
            HideWindow = command.HideWindow
        };
        
        // Open dialog for the copy
        var dialog = new CommandEditorWindow(clone);
        if (dialog.ShowDialog() == true)
        {
            _configService.AddCommand(dialog.Command);
            Commands.Add(dialog.Command);
            SelectedCommand = dialog.Command;
        }
    }
    
    [RelayCommand]
    private async Task StartAllCommands()
    {
        foreach (var command in Commands.Where(c => c.Enabled && c.Status == CommandStatus.Stopped))
        {
            await _processManager.StartAsync(command);
        }
    }
    
    [RelayCommand]
    private async Task StopAllCommands()
    {
        await _processManager.StopAllAsync();
    }
    
    [RelayCommand]
    private async Task InstallService()
    {
        var success = await _serviceManager.InstallServiceAsync();
        UpdateServiceStatus();
        
        if (success)
        {
            MessageBox.Show("Service installed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    [RelayCommand]
    private async Task UninstallService()
    {
        var result = MessageBox.Show(
            "Are you sure you want to uninstall the AppStarter service?",
            "Confirm Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            var success = await _serviceManager.UninstallServiceAsync();
            UpdateServiceStatus();
            
            if (success)
            {
                MessageBox.Show("Service uninstalled successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
    
    [RelayCommand]
    private async Task StartService()
    {
        await _serviceManager.StartServiceAsync();
        UpdateServiceStatus();
    }
    
    [RelayCommand]
    private async Task StopService()
    {
        await _serviceManager.StopServiceAsync();
        UpdateServiceStatus();
    }
    
    private void UpdateServiceStatus()
    {
        IsServiceInstalled = _serviceManager.IsServiceInstalled();
        var status = _serviceManager.GetServiceStatus();
        ServiceStatus = status?.ToString() ?? (IsServiceInstalled ? "Unknown" : "Not Installed");
    }
    
    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
    }
    
    [RelayCommand]
    private void ClearCommandLogs(CommandConfig? command)
    {
        if (command == null) return;
        _logService.ClearLogs(command.Id);
    }
    
    [RelayCommand]
    private void OpenLogFile(CommandConfig? command)
    {
        if (command == null) return;
        
        var logPath = _logService.GetLogFilePath(command.Id);
        if (System.IO.File.Exists(logPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("Log file not found", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    
    public async Task CleanupAsync()
    {
        _schedulerService.Stop();
        await _processManager.StopAllAsync();
        _schedulerService.Dispose();
        _processManager.Dispose();
    }

    public void Cleanup()
    {
        // Blocking cleanup for synchronous exit scenarios
        CleanupAsync().Wait(5000);
    }
}
