namespace AppStarter.Models;

/// <summary>
/// Defines the restart policy for a command when it exits
/// </summary>
public enum RestartPolicy
{
    /// <summary>
    /// Do nothing when the command exits
    /// </summary>
    None,
    
    /// <summary>
    /// Always restart the command, regardless of exit code
    /// </summary>
    Always,
    
    /// <summary>
    /// Only restart if the command exits with a non-zero exit code (failure)
    /// </summary>
    OnFailure,
    
    /// <summary>
    /// Restart unless explicitly stopped by the user
    /// </summary>
    UnlessStopped
}

/// <summary>
/// Defines when a command should be triggered to start
/// </summary>
[Flags]
public enum StartTrigger
{
    None = 0,
    
    /// <summary>
    /// Start when the system boots (as a service)
    /// </summary>
    OnBoot = 1,
    
    /// <summary>
    /// Start when any user logs in
    /// </summary>
    OnUserLogin = 2,
    
    /// <summary>
    /// Start manually by user action
    /// </summary>
    Manual = 4,
    
    /// <summary>
    /// Start when AppStarter UI is launched
    /// </summary>
    OnAppStart = 8,
    
    /// <summary>
    /// Start based on cron schedule
    /// </summary>
    Scheduled = 16
}

/// <summary>
/// Current running status of a command
/// </summary>
public enum CommandStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Failed,
    Scheduled
}
