using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AppStarter.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace AppStarter.Services;

public class DatabaseService
{
    private readonly LogService _logService;
    private DatabaseConfig _config;
    private bool _isInitialized;
    private string _connectionString = "";

    public DatabaseService(LogService logService)
    {
        _logService = logService;
        _config = new DatabaseConfig();
    }

    public void Initialize(DatabaseConfig config)
    {
        _config = config;
        
        // Ensure DB file is in executable directory
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.DatabaseName);
        _connectionString = $"Data Source={dbPath}";
        
        try
        {
            EnsureTablesExist();
            _isInitialized = true;
            _logService.LogSystem("Database initialized successfully at " + dbPath);
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Failed to initialize database: {ex.Message}", true);
        }
    }

    private void EnsureTablesExist()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        
        // Commands table
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS commands (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                Command TEXT NOT NULL,
                Arguments TEXT,
                WorkingDirectory TEXT,
                RestartPolicy INTEGER,
                RestartDelaySeconds INTEGER,
                MaxRestartAttempts INTEGER,
                CronExpression TEXT,
                StartTrigger INTEGER,
                Enabled INTEGER,
                Priority INTEGER,
                RunAsAdmin INTEGER,
                HideWindow INTEGER,
                EnvironmentVariables TEXT,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );";
        cmd.ExecuteNonQuery();

        // Logs table
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS logs (
                LogId INTEGER PRIMARY KEY AUTOINCREMENT,
                CommandId TEXT,
                CommandName TEXT,
                Message TEXT,
                IsError INTEGER,
                Timestamp TEXT
            );";
        cmd.ExecuteNonQuery();
        
        // Indices
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_logs_command ON logs(CommandId);";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(Timestamp);";
        cmd.ExecuteNonQuery();
    }

    public List<CommandConfig> LoadCommands()
    {
        if (!_isInitialized) return new List<CommandConfig>();

        var commands = new List<CommandConfig>();

        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM commands";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var command = new CommandConfig
                {
                    Id = reader.GetString(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                    Command = reader.GetString(reader.GetOrdinal("Command")),
                    Arguments = reader.IsDBNull(reader.GetOrdinal("Arguments")) ? "" : reader.GetString(reader.GetOrdinal("Arguments")),
                    WorkingDirectory = reader.IsDBNull(reader.GetOrdinal("WorkingDirectory")) ? "" : reader.GetString(reader.GetOrdinal("WorkingDirectory")),
                    RestartPolicy = (RestartPolicy)reader.GetInt32(reader.GetOrdinal("RestartPolicy")),
                    RestartDelaySeconds = reader.GetInt32(reader.GetOrdinal("RestartDelaySeconds")),
                    MaxRestartAttempts = reader.GetInt32(reader.GetOrdinal("MaxRestartAttempts")),
                    CronExpression = reader.IsDBNull(reader.GetOrdinal("CronExpression")) ? string.Empty : reader.GetString(reader.GetOrdinal("CronExpression")),
                    StartTrigger = (StartTrigger)reader.GetInt32(reader.GetOrdinal("StartTrigger")),
                    Enabled = reader.GetBoolean(reader.GetOrdinal("Enabled")),
                    Priority = reader.IsDBNull(reader.GetOrdinal("Priority")) ? 0 : reader.GetInt32(reader.GetOrdinal("Priority")),
                    RunAsAdmin = reader.GetBoolean(reader.GetOrdinal("RunAsAdmin")),
                    HideWindow = reader.GetBoolean(reader.GetOrdinal("HideWindow")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                };

                if (!reader.IsDBNull(reader.GetOrdinal("EnvironmentVariables")))
                {
                    var envJson = reader.GetString(reader.GetOrdinal("EnvironmentVariables"));
                    command.EnvironmentVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(envJson) 
                        ?? new Dictionary<string, string>();
                }
                
                // Parse dates if needed, but strings are fine for viewing usually. 
                // Model uses DateTime, so SQLite returns string for DateTime columns usually?
                // Actually Sqlite parsing depends. Ideally we store as YYYY-MM-DD HH:MM:SS.
                // Let's assume defaults for CreatedAt.

                commands.Add(command);
            }
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Error loading commands from DB: {ex.Message}", true);
        }

        return commands;
    }

    public void SaveCommand(CommandConfig command)
    {
        if (!_isInitialized) return;

        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            // SQLite REPLACE INTO acts like DELETE + INSERT on PK conflict
            cmd.CommandText = @"
                INSERT OR REPLACE INTO commands (
                    Id, Name, Description, Command, Arguments, WorkingDirectory, 
                    RestartPolicy, RestartDelaySeconds, MaxRestartAttempts, CronExpression, 
                    StartTrigger, Enabled, Priority, RunAsAdmin, HideWindow, EnvironmentVariables,
                    CreatedAt, UpdatedAt
                ) VALUES (
                    @Id, @Name, @Description, @Command, @Arguments, @WorkingDirectory, 
                    @RestartPolicy, @RestartDelaySeconds, @MaxRestartAttempts, @CronExpression, 
                    @StartTrigger, @Enabled, @Priority, @RunAsAdmin, @HideWindow, @EnvironmentVariables,
                    @CreatedAt, CURRENT_TIMESTAMP
                );";

            cmd.Parameters.AddWithValue("@Id", command.Id);
            cmd.Parameters.AddWithValue("@Name", command.Name);
            cmd.Parameters.AddWithValue("@Description", command.Description);
            cmd.Parameters.AddWithValue("@Command", command.Command);
            cmd.Parameters.AddWithValue("@Arguments", command.Arguments);
            cmd.Parameters.AddWithValue("@WorkingDirectory", command.WorkingDirectory);
            cmd.Parameters.AddWithValue("@RestartPolicy", (int)command.RestartPolicy);
            cmd.Parameters.AddWithValue("@RestartDelaySeconds", command.RestartDelaySeconds);
            cmd.Parameters.AddWithValue("@MaxRestartAttempts", command.MaxRestartAttempts);
            cmd.Parameters.AddWithValue("@CronExpression", (object)command.CronExpression ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StartTrigger", (int)command.StartTrigger);
            cmd.Parameters.AddWithValue("@Enabled", command.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@Priority", command.Priority);
            cmd.Parameters.AddWithValue("@RunAsAdmin", command.RunAsAdmin ? 1 : 0);
            cmd.Parameters.AddWithValue("@HideWindow", command.HideWindow ? 1 : 0);
            cmd.Parameters.AddWithValue("@EnvironmentVariables", JsonConvert.SerializeObject(command.EnvironmentVariables));
            cmd.Parameters.AddWithValue("@CreatedAt", command.CreatedAt);

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Error saving command to DB: {ex.Message}", true);
        }
    }

    public void DeleteCommand(string id)
    {
        if (!_isInitialized) return;

        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM commands WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logService.LogSystem($"Error deleting command from DB: {ex.Message}", true);
        }
    }

    private readonly ConcurrentBag<Task> _pendingLogs = new();

    public void SaveLog(LogEntry entry)
    {
        if (!_isInitialized) return;

        // Fire and forget logging, but track it
        var task = Task.Run(() =>
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO logs (CommandId, CommandName, Message, IsError, Timestamp)
                    VALUES (@CommandId, @CommandName, @Message, @IsError, @Timestamp)";

                cmd.Parameters.AddWithValue("@CommandId", entry.CommandId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CommandName", entry.CommandName);
                cmd.Parameters.AddWithValue("@Message", entry.Message);
                cmd.Parameters.AddWithValue("@IsError", entry.IsError ? 1 : 0);
                cmd.Parameters.AddWithValue("@Timestamp", entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                cmd.ExecuteNonQuery();
            }
            catch 
            {
                // Silently fail logging to avoid recursion
            }
        });

        _pendingLogs.Add(task);
        
        // Cleanup completed tasks occasionally
        if (_pendingLogs.Count > 100)
        {
            var completed = _pendingLogs.Where(t => t.IsCompleted).ToList();
            foreach (var t in completed) _pendingLogs.TryTake(out _);
        }
    }

    public async Task WaitForPendingLogsAsync()
    {
        var tasks = _pendingLogs.ToList();
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
        _pendingLogs.Clear();
    }
}
