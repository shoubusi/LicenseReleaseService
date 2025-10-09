using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.IdleDetection.Models
{
    public class IdleDetectionTask
    {
        public Guid TaskId { get; set; }
        public string TaskName { get; set; }
        public IdleDetectionTaskType TaskType { get; set; }
        public Func<CancellationToken, Task> ExecuteAsync { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? ExecutionDuration { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFaulted { get; set; }
        public Exception Exception { get; set; }
        public object Result { get; set; }
        public TaskPriority Priority { get; set; }

        // Missing properties
        public string DetectorName { get; set; }
        public string SessionId { get; set; }
        public Func<CancellationToken, Task> TaskFunc { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public TimeSpan? Timeout { get; set; }

        public IdleDetectionTask()
        {
            TaskId = Guid.NewGuid();
            TaskName = string.Empty;
            DetectorName = string.Empty;
            SessionId = string.Empty;
            CreatedTime = DateTime.UtcNow;
            CreatedAt = CreatedTime;
            Priority = TaskPriority.Normal;
        }

        public IdleDetectionTask(string taskName, IdleDetectionTaskType taskType, Func<CancellationToken, Task> executeAsync, TaskPriority priority = TaskPriority.Normal)
        {
            TaskId = Guid.NewGuid();
            TaskName = taskName ?? throw new ArgumentNullException(nameof(taskName));
            TaskType = taskType;
            ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            TaskFunc = executeAsync; // Initialize TaskFunc with same function
            DetectorName = string.Empty;
            SessionId = string.Empty;
            CreatedTime = DateTime.UtcNow;
            CreatedAt = CreatedTime;
            Priority = priority;
        }
    }

    public enum IdleDetectionTaskType
    {
        TimerCheck,
        ProcessMonitor,
        FileSystemMonitor,
        NetworkMonitor,
        ConfigurationUpdate,
        LicenseRelease,
        SystemActivityCheck,
        HealthCheck,
        CustomTask,
        PeriodicDetection,
        DetectorSpecific
    }

    public enum TaskPriority
    {
        Low = 1,
        Normal = 5,
        High = 8,
        Critical = 10
    }
}