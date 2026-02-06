using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using AppStarter.Models;

namespace AppStarter.Services;

/// <summary>
/// Manages Windows service installation and control
/// </summary>
public class WindowsServiceManager
{
    private readonly LogService _logService;
    private readonly string _serviceName;
    private readonly string _displayName;
    private readonly string _description;
    
    public WindowsServiceManager(LogService logService, ServiceSettings? settings = null)
    {
        _logService = logService;
        settings ??= new ServiceSettings();
        
        _serviceName = settings.ServiceName;
        _displayName = settings.DisplayName;
        _description = settings.Description;
    }
    
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    
    public bool IsServiceInstalled()
    {
        try
        {
            using var sc = new ServiceController(_serviceName);
            var status = sc.Status; // This will throw if service doesn't exist
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public ServiceControllerStatus? GetServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(_serviceName);
            return sc.Status;
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<bool> InstallServiceAsync()
    {
        if (!IsAdministrator())
        {
            _logService.LogSystem("Administrator privileges required to install service", true);
            return false;
        }
        
        if (IsServiceInstalled())
        {
            _logService.LogSystem("Service is already installed");
            return true;
        }
        
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                _logService.LogSystem("Could not determine application path", true);
                return false;
            }
            
            // Use sc.exe to create the service
            var createArgs = $"create \"{_serviceName}\" binPath= \"\\\"{exePath}\\\" --service\" start= auto DisplayName= \"{_displayName}\"";
            
            var result = await RunScCommandAsync(createArgs);
            if (!result)
            {
                return false;
            }
            
            // Set description
            var descArgs = $"description \"{_serviceName}\" \"{_description}\"";
            await RunScCommandAsync(descArgs);
            
            _logService.LogSystem($"Service '{_displayName}' installed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Failed to install service: {ex.Message}", true);
            return false;
        }
    }
    
    public async Task<bool> UninstallServiceAsync()
    {
        if (!IsAdministrator())
        {
            _logService.LogSystem("Administrator privileges required to uninstall service", true);
            return false;
        }
        
        if (!IsServiceInstalled())
        {
            _logService.LogSystem("Service is not installed");
            return true;
        }
        
        try
        {
            // Stop the service first
            await StopServiceAsync();
            
            // Delete the service
            var result = await RunScCommandAsync($"delete \"{_serviceName}\"");
            
            if (result)
            {
                _logService.LogSystem($"Service '{_displayName}' uninstalled successfully");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Failed to uninstall service: {ex.Message}", true);
            return false;
        }
    }
    
    public async Task<bool> StartServiceAsync()
    {
        if (!IsServiceInstalled())
        {
            _logService.LogSystem("Service is not installed", true);
            return false;
        }
        
        try
        {
            using var sc = new ServiceController(_serviceName);
            
            if (sc.Status == ServiceControllerStatus.Running)
            {
                _logService.LogSystem("Service is already running");
                return true;
            }
            
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            
            _logService.LogSystem("Service started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Failed to start service: {ex.Message}", true);
            return false;
        }
    }
    
    public async Task<bool> StopServiceAsync()
    {
        if (!IsServiceInstalled())
        {
            return true;
        }
        
        try
        {
            using var sc = new ServiceController(_serviceName);
            
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                return true;
            }
            
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            
            _logService.LogSystem("Service stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Failed to stop service: {ex.Message}", true);
            return false;
        }
    }
    
    private async Task<bool> RunScCommandAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null) return false;
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            _logService.LogSystem($"sc.exe failed: {error}", true);
            return false;
        }
        
        return true;
    }
}
