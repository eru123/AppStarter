using System.IO;
using Newtonsoft.Json;
using AppStarter.Models;

namespace AppStarter.Services;

/// <summary>
/// Handles loading and saving application configuration
/// </summary>
public class ConfigService
{
    private readonly string _configPath;
    private readonly object _lock = new();
    private DatabaseService? _dbService;
    
    public ConfigService(DatabaseService? dbService = null) : this()
    {
        _dbService = dbService;
    }

    public ConfigService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AppStarter"
        );
        
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, "config.json");
    }
    
    public ConfigService(string configPath, DatabaseService? dbService = null)
    {
        _dbService = dbService;
        _configPath = configPath;
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    public string ConfigPath => _configPath;
    
    public AppConfig Load()
    {
        lock (_lock)
        {
            AppConfig config;
            if (!File.Exists(_configPath))
            {
                config = new AppConfig();
                SaveInternal(config); // Create file with defaults
            }
            else
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
                    config = new AppConfig();
                }
            }

            // Initialize DB and load commands if available
            if (_dbService != null)
            {
                _dbService.Initialize(config.Database);
                var dbCommands = _dbService.LoadCommands();
                // If DB has commands, use them. If DB is empty but we have local commands (first migration?)
                // Maybe we should save local commands to DB?
                // For now, let's prefer DB commands if we are using DB.
                // But we must preserve the list instance if possible or just replace it.
                if (dbCommands.Count > 0)
                {
                    config.Commands = dbCommands;
                }
                else if (config.Commands.Count > 0)
                {
                    // Migration: Save existing commands to DB
                    foreach (var cmd in config.Commands)
                    {
                        _dbService.SaveCommand(cmd);
                    }
                }
            }
            
            return config;
        }
    }
    
    public void Save(AppConfig config)
    {
        lock (_lock)
        {
            SaveInternal(config);
            
            if (_dbService != null)
            {
                foreach (var cmd in config.Commands)
                {
                    _dbService.SaveCommand(cmd);
                }
            }
        }
    }

    private void SaveInternal(AppConfig config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            throw;
        }
    }
    
    public void AddCommand(CommandConfig command)
    {
        if (_dbService != null)
        {
            _dbService.SaveCommand(command);
            // Also update local cache if needed, but the caller usually reloads
            // For consistency we should update local JSON too? 
            // The request says "data should be save in a mysql database".
            // So we don't strictly need to update JSON commands list anymore, but keeping it synced is safer for fallback.
        }
        
        var config = Load(); // This reloads from DB anyway
        if (!config.Commands.Any(c => c.Id == command.Id))
        {
             config.Commands.Add(command);
             SaveInternal(config); // Update local cache
        }
    }
    
    public void UpdateCommand(CommandConfig command)
    {
        if (_dbService != null)
        {
            _dbService.SaveCommand(command);
        }

        var config = Load();
        var index = config.Commands.FindIndex(c => c.Id == command.Id);
        if (index >= 0)
        {
            config.Commands[index] = command;
            SaveInternal(config);
        }
    }
    
    public void RemoveCommand(string commandId)
    {
        if (_dbService != null)
        {
            _dbService.DeleteCommand(commandId);
        }

        var config = Load();
        config.Commands.RemoveAll(c => c.Id == commandId);
        SaveInternal(config);
    }
}
