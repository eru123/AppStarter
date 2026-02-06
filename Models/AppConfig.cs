using Newtonsoft.Json;

namespace AppStarter.Models;

/// <summary>
/// Application configuration containing all command definitions
/// </summary>
public class AppConfig
{
    public string Version { get; set; } = "1.0.0";
    
    public List<CommandConfig> Commands { get; set; } = new();
    
    public ServiceSettings ServiceSettings { get; set; } = new();
    
    public LoggingSettings LoggingSettings { get; set; } = new();

    public DatabaseConfig Database { get; set; } = new();
}

public class DatabaseConfig
{
    public string DatabaseName { get; set; } = "AppStarter.db";
    
    public string GetConnectionString()
    {
        // For SQLite, the connection string is just the data source file
        return $"Data Source={DatabaseName}";
    }
}

/// <summary>
/// Windows Service configuration
/// </summary>
public class ServiceSettings
{
    public string ServiceName { get; set; } = "AppStarterService";
    
    public string DisplayName { get; set; } = "AppStarter Service";
    
    public string Description { get; set; } = "Manages background processes and scheduled tasks";
    
    public bool StartWithWindows { get; set; } = true;
}

/// <summary>
/// Logging configuration
/// </summary>
public class LoggingSettings
{
    public string LogDirectory { get; set; } = "logs";
    
    public int MaxLogFileSizeMB { get; set; } = 10;
    
    public int MaxLogFiles { get; set; } = 10;
    
    public bool LogToConsole { get; set; } = true;
}
