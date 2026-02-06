using System.Diagnostics;
using System.Collections.Concurrent;
using AppStarter.Models;

namespace AppStarter.Services;

/// <summary>
/// Manages process lifecycle - starting, stopping, monitoring, and restarting processes
/// </summary>
public class ProcessManager : IDisposable
{
    private readonly LogService _logService;
    private readonly ConcurrentDictionary<string, ManagedProcess> _processes = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _startLock = new();
    private bool _disposed;
    
    public event EventHandler<CommandConfig>? ProcessStarted;
    public event EventHandler<CommandConfig>? ProcessStopped;
    public event EventHandler<(CommandConfig Command, int ExitCode)>? ProcessExited;
    
    public ProcessManager(LogService logService)
    {
        _logService = logService;
    }
    
    public int GetRunningCount() => _processes.Count;
    
    public async Task<bool> StartAsync(CommandConfig command)
    {
        // Thread-safe check to prevent duplicate starts
        lock (_startLock)
        {
            if (_processes.ContainsKey(command.Id))
            {
                _logService.LogInfo(command.Id, command.Name, "Process is already running - skipping duplicate start");
                return false;
            }
            
            // Reserve the slot immediately to prevent race conditions
            if (!_processes.TryAdd(command.Id, null!))
            {
                _logService.LogInfo(command.Id, command.Name, "Process start already in progress");
                return false;
            }
        }
        
        try
        {
            command.Status = CommandStatus.Starting;
            _logService.LogInfo(command.Id, command.Name, $"Starting process: {command.Command} {command.Arguments}");
            
            var psi = new ProcessStartInfo
            {
                FileName = command.Command,
                Arguments = command.Arguments,
                WorkingDirectory = string.IsNullOrEmpty(command.WorkingDirectory) 
                    ? Environment.CurrentDirectory 
                    : command.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = command.HideWindow
            };
            
            // Add environment variables
            foreach (var envVar in command.EnvironmentVariables)
            {
                psi.Environment[envVar.Key] = envVar.Value;
            }
            
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var managedProcess = new ManagedProcess(command, process);
            
            // Handle output
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logService.LogInfo(command.Id, command.Name, e.Data);
                }
            };
            
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logService.LogError(command.Id, command.Name, e.Data);
                }
            };
            
            // Handle exit
            process.Exited += async (s, e) =>
            {
                var exitCode = process.ExitCode;
                _logService.LogInfo(command.Id, command.Name, $"Process exited with code: {exitCode}");
                
                command.Status = exitCode == 0 ? CommandStatus.Stopped : CommandStatus.Failed;
                command.ProcessId = null;
                command.StartedAt = null;
                
                _processes.TryRemove(command.Id, out _);
                
                ProcessExited?.Invoke(this, (command, exitCode));
                
                // Handle restart policy
                await HandleRestartPolicyAsync(command, exitCode, managedProcess);
            };
            
            if (!process.Start())
            {
                _logService.LogError(command.Id, command.Name, "Failed to start process");
                command.Status = CommandStatus.Failed;
                // Clean up the reserved slot
                _processes.TryRemove(command.Id, out _);
                return false;
            }
            
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            _processes[command.Id] = managedProcess;
            
            command.Status = CommandStatus.Running;
            command.ProcessId = process.Id;
            command.StartedAt = DateTime.Now;
            command.LastRunAt = DateTime.Now;
            command.RestartCount = 0;
            
            _logService.LogInfo(command.Id, command.Name, $"Process started with PID: {process.Id}");
            ProcessStarted?.Invoke(this, command);
            
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogError(command.Id, command.Name, $"Error starting process: {ex.Message}");
            command.Status = CommandStatus.Failed;
            // Clean up the reserved slot
            _processes.TryRemove(command.Id, out _);
            return false;
        }
    }
    
    public async Task<bool> StopAsync(CommandConfig command, int timeoutMs = 5000)
    {
        if (!_processes.TryGetValue(command.Id, out var managedProcess))
        {
            _logService.LogInfo(command.Id, command.Name, "Process is not running");
            return false;
        }
        
        try
        {
            command.Status = CommandStatus.Stopping;
            managedProcess.StopRequested = true;
            
            _logService.LogInfo(command.Id, command.Name, "Stopping process...");
            
            var process = managedProcess.Process;
            
            // Try graceful shutdown first
            if (!process.HasExited)
            {
                // Capture PID for fallback
                int pid = 0;
                try { pid = process.Id; } catch { }
                
                // Try CloseMainWindow for GUI apps - wrap in try/catch as properties can throw if process exits
                try
                {
                    if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                    {
                        process.CloseMainWindow();
                    }
                }
                catch { } // Ignore errors if process exited or handle invalid
                
                bool exited = false;
                try
                {
                    exited = await Task.Run(() => process.WaitForExit(timeoutMs));
                }
                catch { }

                if (!exited && pid > 0)
                {
                    _logService.LogInfo(command.Id, command.Name, "Process did not exit gracefully, killing process tree via taskkill...");
                    
                    try 
                    {
                        // Use taskkill to forcefully kill the process tree which is more robust
                        // than process.Kill(true) for things like cmd.exe wrappers
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /T /PID {pid}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        var p = Process.Start(startInfo);
                        p?.WaitForExit(2000);
                        
                        // Verify exit
                        if (!process.HasExited)
                        {
                            try { process.Kill(entireProcessTree: true); } catch { }
                        }
                    }
                    catch (Exception killEx)
                    {
                        _logService.LogError(command.Id, command.Name, $"Taskkill failed ({killEx.Message}), trying standard kill...");
                        try { process.Kill(entireProcessTree: true); } catch { }
                    }
                }
            }
            
            _processes.TryRemove(command.Id, out _);
            
            command.Status = CommandStatus.Stopped;
            command.ProcessId = null;
            command.StartedAt = null;
            
            _logService.LogInfo(command.Id, command.Name, "Process stopped");
            ProcessStopped?.Invoke(this, command);
            
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogError(command.Id, command.Name, $"Error stopping process: {ex.Message}");
            return false;
        }
    }
    
    public async Task RestartAsync(CommandConfig command)
    {
        await StopAsync(command);
        await Task.Delay(1000); // Brief pause between stop and start
        await StartAsync(command);
    }
    
    public bool IsRunning(string commandId)
    {
        return _processes.ContainsKey(commandId);
    }
    
    public CommandStatus GetStatus(string commandId)
    {
        if (_processes.TryGetValue(commandId, out var mp))
        {
            return mp.Command.Status;
        }
        return CommandStatus.Stopped;
    }
    
    private async Task HandleRestartPolicyAsync(CommandConfig command, int exitCode, ManagedProcess managedProcess)
    {
        if (managedProcess.StopRequested)
        {
            // Don't restart if explicitly stopped
            return;
        }
        
        var shouldRestart = command.RestartPolicy switch
        {
            RestartPolicy.Always => true,
            RestartPolicy.OnFailure => exitCode != 0,
            RestartPolicy.UnlessStopped => !managedProcess.StopRequested,
            _ => false
        };
        
        if (shouldRestart && command.RestartCount < command.MaxRestartAttempts)
        {
            command.RestartCount++;
            _logService.LogInfo(command.Id, command.Name, 
                $"Restarting in {command.RestartDelaySeconds} seconds (attempt {command.RestartCount}/{command.MaxRestartAttempts})...");
            
            await Task.Delay(command.RestartDelaySeconds * 1000);
            
            if (!_cts.Token.IsCancellationRequested)
            {
                await StartAsync(command);
            }
        }
        else if (shouldRestart)
        {
            _logService.LogError(command.Id, command.Name, 
                $"Max restart attempts ({command.MaxRestartAttempts}) reached. Process will not be restarted.");
            command.Status = CommandStatus.Failed;
        }
    }
    
    public async Task StopAllAsync()
    {
        var tasks = _processes.Values.Select(mp => StopAsync(mp.Command));
        await Task.WhenAll(tasks);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _cts.Cancel();
        
        foreach (var mp in _processes.Values)
        {
            try
            {
                if (!mp.Process.HasExited)
                {
                    mp.Process.Kill(entireProcessTree: true);
                }
                mp.Process.Dispose();
            }
            catch { }
        }
        
        _processes.Clear();
        _cts.Dispose();
    }
    
    private class ManagedProcess
    {
        public CommandConfig Command { get; }
        public Process Process { get; }
        public bool StopRequested { get; set; }
        
        public ManagedProcess(CommandConfig command, Process process)
        {
            Command = command;
            Process = process;
        }
    }
}
