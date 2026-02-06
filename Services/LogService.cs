using System.IO;
using System.Collections.Concurrent;
using AppStarter.Models;

namespace AppStarter.Services;

/// <summary>
/// Service for managing command output logs
/// </summary>
public class LogService
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, List<LogEntry>> _memoryLogs = new();
    private readonly int _maxEntriesPerCommand = 1000;
    private readonly object _fileLock = new();
    private DatabaseService? _databaseService;
    
    public event EventHandler<LogEntry>? LogAdded;

    public void SetDatabaseService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }
    
    public LogService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AppStarter",
            "logs"
        );
        Directory.CreateDirectory(_logDirectory);
    }
    
    public LogService(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }
    
    public void Log(string commandId, string commandName, string message, bool isError = false)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            CommandId = commandId,
            CommandName = commandName,
            Message = message,
            IsError = isError,
            Level = isError ? Models.LogLevel.Error : Models.LogLevel.Info
        };
        
        // Add to memory log
        var logs = _memoryLogs.GetOrAdd(commandId, _ => new List<LogEntry>());
        lock (logs)
        {
            logs.Add(entry);
            
            // Trim if too many entries
            while (logs.Count > _maxEntriesPerCommand)
            {
                logs.RemoveAt(0);
            }
        }
        
        // Write to file
        WriteToFile(commandId, entry);

        // Write to database
        _databaseService?.SaveLog(entry);
        
        // Notify listeners
        LogAdded?.Invoke(this, entry);
    }
    
    public void LogInfo(string commandId, string commandName, string message)
        => Log(commandId, commandName, message, false);
    
    public void LogError(string commandId, string commandName, string message)
        => Log(commandId, commandName, message, true);
    
    public void LogSystem(string message, bool isError = false)
        => Log("system", "System", message, isError);
    
    public List<LogEntry> GetLogs(string commandId, int count = 100)
    {
        if (_memoryLogs.TryGetValue(commandId, out var logs))
        {
            lock (logs)
            {
                return logs.TakeLast(count).ToList();
            }
        }
        return new List<LogEntry>();
    }
    
    public List<LogEntry> GetAllLogs(int count = 500)
    {
        var allLogs = new List<LogEntry>();
        
        foreach (var logs in _memoryLogs.Values)
        {
            lock (logs)
            {
                allLogs.AddRange(logs);
            }
        }
        
        return allLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .Reverse()
            .ToList();
    }
    
    public void ClearLogs(string commandId)
    {
        if (_memoryLogs.TryGetValue(commandId, out var logs))
        {
            lock (logs)
            {
                logs.Clear();
            }
        }
    }
    
    private void WriteToFile(string commandId, LogEntry entry)
    {
        try
        {
            var safeFileName = string.Join("_", commandId.Split(Path.GetInvalidFileNameChars()));
            var logFile = Path.Combine(_logDirectory, $"{safeFileName}_{DateTime.Now:yyyy-MM-dd}.log");
            
            var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] {entry.Message}";
            
            lock (_fileLock)
            {
                File.AppendAllLines(logFile, new[] { logLine });
            }
        }
        catch
        {
            // Silently fail file logging to not interrupt process
        }
    }
    
    public string GetLogFilePath(string commandId)
    {
        var safeFileName = string.Join("_", commandId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_logDirectory, $"{safeFileName}_{DateTime.Now:yyyy-MM-dd}.log");
    }
}
