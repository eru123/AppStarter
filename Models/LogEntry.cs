namespace AppStarter.Models;

/// <summary>
/// Represents a single log entry for a command's output
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    public string CommandId { get; set; } = string.Empty;
    
    public string CommandName { get; set; } = string.Empty;
    
    public LogLevel Level { get; set; } = LogLevel.Info;
    
    public string Message { get; set; } = string.Empty;
    
    public bool IsError { get; set; }
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}
