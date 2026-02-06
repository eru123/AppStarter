using System.Text.RegularExpressions;
using System.Timers;
using AppStarter.Models;
using Timer = System.Timers.Timer;

namespace AppStarter.Services;

/// <summary>
/// Simple cron-like scheduler for running commands on a schedule
/// Supports basic cron expressions: minute hour day month dayOfWeek
/// </summary>
public class SchedulerService : IDisposable
{
    private readonly ProcessManager _processManager;
    private readonly LogService _logService;
    private readonly List<ScheduledJob> _jobs = new();
    private readonly Timer _timer;
    private readonly object _lock = new();
    private bool _disposed;
    
    public SchedulerService(ProcessManager processManager, LogService logService)
    {
        _processManager = processManager;
        _logService = logService;
        
        // Check every minute
        _timer = new Timer(60000);
        _timer.Elapsed += OnTimerElapsed;
    }
    
    public void Start()
    {
        _timer.Start();
        _logService.LogSystem("Scheduler started");
    }
    
    public void Stop()
    {
        _timer.Stop();
        _logService.LogSystem("Scheduler stopped");
    }
    
    public void ScheduleCommand(CommandConfig command)
    {
        if (string.IsNullOrWhiteSpace(command.CronExpression))
        {
            return;
        }
        
        lock (_lock)
        {
            // Remove existing schedule for this command
            _jobs.RemoveAll(j => j.Command.Id == command.Id);
            
            try
            {
                var schedule = ParseCronExpression(command.CronExpression);
                _jobs.Add(new ScheduledJob(command, schedule));
                _logService.LogInfo(command.Id, command.Name, $"Scheduled with cron: {command.CronExpression}");
            }
            catch (Exception ex)
            {
                _logService.LogError(command.Id, command.Name, $"Invalid cron expression: {ex.Message}");
            }
        }
    }
    
    public void UnscheduleCommand(string commandId)
    {
        lock (_lock)
        {
            _jobs.RemoveAll(j => j.Command.Id == commandId);
        }
    }
    
    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var now = DateTime.Now;
        
        lock (_lock)
        {
            foreach (var job in _jobs.Where(j => j.Command.Enabled))
            {
                if (ShouldRun(job.Schedule, now))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logService.LogInfo(job.Command.Id, job.Command.Name, "Triggered by schedule");
                            await _processManager.StartAsync(job.Command);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(job.Command.Id, job.Command.Name, $"Scheduled execution failed: {ex.Message}");
                        }
                    });
                }
            }
        }
    }
    
    private bool ShouldRun(CronSchedule schedule, DateTime time)
    {
        return schedule.Minutes.Contains(time.Minute) &&
               schedule.Hours.Contains(time.Hour) &&
               schedule.DaysOfMonth.Contains(time.Day) &&
               schedule.Months.Contains(time.Month) &&
               schedule.DaysOfWeek.Contains((int)time.DayOfWeek);
    }
    
    /// <summary>
    /// Parse a cron expression (minute hour dayOfMonth month dayOfWeek)
    /// Supports: *, specific values, ranges (1-5), steps (*/5), and lists (1,3,5)
    /// </summary>
    private CronSchedule ParseCronExpression(string expression)
    {
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length != 5)
        {
            throw new ArgumentException("Cron expression must have 5 parts: minute hour dayOfMonth month dayOfWeek");
        }
        
        return new CronSchedule
        {
            Minutes = ParseCronField(parts[0], 0, 59),
            Hours = ParseCronField(parts[1], 0, 23),
            DaysOfMonth = ParseCronField(parts[2], 1, 31),
            Months = ParseCronField(parts[3], 1, 12),
            DaysOfWeek = ParseCronField(parts[4], 0, 6)
        };
    }
    
    private HashSet<int> ParseCronField(string field, int min, int max)
    {
        var values = new HashSet<int>();
        
        foreach (var part in field.Split(','))
        {
            if (part == "*")
            {
                for (int i = min; i <= max; i++)
                    values.Add(i);
            }
            else if (part.Contains('/'))
            {
                var stepParts = part.Split('/');
                var step = int.Parse(stepParts[1]);
                var start = stepParts[0] == "*" ? min : int.Parse(stepParts[0]);
                
                for (int i = start; i <= max; i += step)
                    values.Add(i);
            }
            else if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                var rangeStart = int.Parse(rangeParts[0]);
                var rangeEnd = int.Parse(rangeParts[1]);
                
                for (int i = rangeStart; i <= rangeEnd; i++)
                    values.Add(i);
            }
            else
            {
                values.Add(int.Parse(part));
            }
        }
        
        return values;
    }
    
    public DateTime? GetNextRunTime(CommandConfig command)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Command.Id == command.Id);
            if (job == null) return null;
            
            var now = DateTime.Now;
            var next = now.AddMinutes(1);
            next = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);
            
            // Look ahead up to 1 year
            for (int i = 0; i < 525600; i++) // minutes in a year
            {
                if (ShouldRun(job.Schedule, next))
                {
                    return next;
                }
                next = next.AddMinutes(1);
            }
            
            return null;
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _timer.Stop();
        _timer.Dispose();
    }
    
    private class ScheduledJob
    {
        public CommandConfig Command { get; }
        public CronSchedule Schedule { get; }
        
        public ScheduledJob(CommandConfig command, CronSchedule schedule)
        {
            Command = command;
            Schedule = schedule;
        }
    }
    
    private class CronSchedule
    {
        public HashSet<int> Minutes { get; set; } = new();
        public HashSet<int> Hours { get; set; } = new();
        public HashSet<int> DaysOfMonth { get; set; } = new();
        public HashSet<int> Months { get; set; } = new();
        public HashSet<int> DaysOfWeek { get; set; } = new();
    }
}
