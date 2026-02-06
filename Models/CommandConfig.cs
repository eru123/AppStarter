using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppStarter.Models;

/// <summary>
/// Represents a command/process configuration managed by AppStarter
/// </summary>
public partial class CommandConfig : ObservableObject
{
    [ObservableProperty]
    private string id = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private string name = string.Empty;
    
    [ObservableProperty]
    private string description = string.Empty;
    
    [ObservableProperty]
    private string command = string.Empty;
    
    [ObservableProperty]
    private string arguments = string.Empty;
    
    [ObservableProperty]
    private string workingDirectory = string.Empty;
    
    [ObservableProperty]
    private RestartPolicy restartPolicy = RestartPolicy.None;
    
    [ObservableProperty]
    private int restartDelaySeconds = 5;
    
    [ObservableProperty]
    private int maxRestartAttempts = 3;
    
    [ObservableProperty]
    private StartTrigger startTrigger = StartTrigger.Manual;
    
    [ObservableProperty]
    private string cronExpression = string.Empty;
    
    [ObservableProperty]
    private bool enabled = true;
    
    [ObservableProperty]
    private int priority = 0;
    
    [ObservableProperty]
    private Dictionary<string, string> environmentVariables = new();
    
    [ObservableProperty]
    private bool runAsAdmin = false;
    
    [ObservableProperty]
    private bool hideWindow = true;
    
    [ObservableProperty]
    private DateTime createdAt = DateTime.Now;
    
    [ObservableProperty]
    private DateTime? lastRunAt;

    [JsonIgnore]
    public bool IsOnBootTrigger
    {
        get => StartTrigger.HasFlag(StartTrigger.OnBoot);
        set
        {
            if (value) StartTrigger |= StartTrigger.OnBoot;
            else StartTrigger &= ~StartTrigger.OnBoot;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsOnUserLoginTrigger
    {
        get => StartTrigger.HasFlag(StartTrigger.OnUserLogin);
        set
        {
            if (value) StartTrigger |= StartTrigger.OnUserLogin;
            else StartTrigger &= ~StartTrigger.OnUserLogin;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsManualTrigger
    {
        get => StartTrigger.HasFlag(StartTrigger.Manual);
        set
        {
            if (value) StartTrigger |= StartTrigger.Manual;
            else StartTrigger &= ~StartTrigger.Manual;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsOnAppStartTrigger
    {
        get => StartTrigger.HasFlag(StartTrigger.OnAppStart);
        set
        {
            if (value) StartTrigger |= StartTrigger.OnAppStart;
            else StartTrigger &= ~StartTrigger.OnAppStart;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsScheduledTrigger
    {
        get => StartTrigger.HasFlag(StartTrigger.Scheduled);
        set
        {
            if (value) StartTrigger |= StartTrigger.Scheduled;
            else StartTrigger &= ~StartTrigger.Scheduled;
            OnPropertyChanged();
        }
    }
    
    // Runtime properties (not persisted)
    [JsonIgnore]
    [ObservableProperty]
    private CommandStatus status = CommandStatus.Stopped;
    
    [JsonIgnore]
    [ObservableProperty]
    private int? processId;
    
    [JsonIgnore]
    [ObservableProperty]
    private int restartCount;
    
    [JsonIgnore]
    [ObservableProperty]
    private DateTime? startedAt;
    
    [JsonIgnore]
    public TimeSpan? Uptime => StartedAt.HasValue ? DateTime.Now - StartedAt : null;
}
