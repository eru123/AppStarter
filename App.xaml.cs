using System.IO;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AppStarter;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "AppStarter_SingleInstance_Mutex_v2";
    
    public static bool IsAdmin { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Check for single instance
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);
        
        if (!createdNew)
        {
            // Another instance is already running
            MessageBox.Show(
                "AppStarter is already running.\n\nCheck the system tray for the banana icon.",
                "AppStarter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            Shutdown();
            return;
        }

        // Check Admin Privileges
        IsAdmin = Helpers.SecurityHelper.IsAdministrator();
        
        if (!IsAdmin)
        {
            var result = MessageBox.Show(
                "You are running AppStarter as a non-admin user.\n\n" +
                "Some features will be restricted:\n" +
                "- No background execution (System Tray disabled)\n" +
                "- Cannot install Windows Services\n" +
                "- Exit confirmation enabled\n\n" +
                "Do you want to continue?",
                "Non-Admin Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.No)
            {
                Shutdown();
                return;
            }
        }
        
        base.OnStartup(e);
        
        // Check for service mode
        if (e.Args.Contains("--service"))
        {
            // Run as Windows service (background mode)
            RunAsService();
            return;
        }
        
        // Normal UI mode
        var mainWindow = new MainWindow();
        mainWindow.Show();
        
        // global error handling
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }
    
    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowErrorDialog(e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowErrorDialog(ex);
        }
    }

    private void ShowErrorDialog(Exception ex)
    {
        string message = $"An unexpected error occurred:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        MessageBox.Show(message, "AppStarter Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    
    private void RunAsService()
    {
        // In service mode, we don't show UI but run the background process manager
        var logService = new Services.LogService();
        var configService = new Services.ConfigService();
        var processManager = new Services.ProcessManager(logService);
        var schedulerService = new Services.SchedulerService(processManager, logService);
        
        logService.LogSystem("AppStarter service started");
        
        // Load and start configured commands
        var config = configService.Load();
        
        foreach (var command in config.Commands.Where(c => c.Enabled))
        {
            if (command.StartTrigger.HasFlag(Models.StartTrigger.OnBoot))
            {
                _ = processManager.StartAsync(command);
            }
            
            if (command.StartTrigger.HasFlag(Models.StartTrigger.Scheduled))
            {
                schedulerService.ScheduleCommand(command);
            }
        }
        
        schedulerService.Start();
        
        // Keep service running
        var tcs = new TaskCompletionSource();
        tcs.Task.Wait();
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        // Release the mutex
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
        
        base.OnExit(e);
    }
}
