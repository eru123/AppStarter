using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace AppStarter.Services;

public class BackupService
{
    private readonly string _configPath;
    private readonly string _dbPath;

    public BackupService()
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AppStarter",
            "config.json"
        );
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppStarter.db");
    }

    public void Export(string destinationPath)
    {
        // Use ZipArchive with SmallestSize for optimal compression and reliability
        using var fs = new FileStream(destinationPath, FileMode.Create);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // Export Config
        if (File.Exists(_configPath))
        {
            archive.CreateEntryFromFile(_configPath, "config.json", CompressionLevel.SmallestSize);
        }
        
        // Export Database
        if (File.Exists(_dbPath))
        {
            // Create a temp copy to avoid source lock
            string tempDb = Path.Combine(Path.GetTempPath(), $"AppStarter_Export_{Guid.NewGuid()}.db");
            try
            {
                File.Copy(_dbPath, tempDb, true);
                archive.CreateEntryFromFile(tempDb, "AppStarter.db", CompressionLevel.SmallestSize);
            }
            finally
            {
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
        }
    }

    public void Import(string sourcePath)
    {
        // 1. Prepare for replacement
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        using var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Equals("config.json", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                entry.ExtractToFile(_configPath, overwrite: true);
            }
            else if (entry.Name.Equals("AppStarter.db", StringComparison.OrdinalIgnoreCase))
            {
                // Aggressive lock-breaking for DB replacement
                bool success = false;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        SqliteConnection.ClearAllPools();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        entry.ExtractToFile(_dbPath, overwrite: true);
                        success = true;
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(200 * (i + 1));
                    }
                }

                if (!success)
                {
                    // Last resort: Attempt to move the file before extracting
                    try
                    {
                        string oldDb = _dbPath + ".old";
                        if (File.Exists(_dbPath)) File.Move(_dbPath, oldDb, true);
                        entry.ExtractToFile(_dbPath, overwrite: true);
                        if (File.Exists(oldDb)) File.Delete(oldDb);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Could not replace database file because it is locked by another process. Please ensure all instances of AppStarter are closed.", ex);
                    }
                }
            }
        }
    }
}
