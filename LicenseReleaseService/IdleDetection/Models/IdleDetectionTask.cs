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

        public IdleDetectionTask()
        {
            TaskId = Guid.NewGuid();
            TaskName = string.Empty;
            CreatedTime = DateTime.UtcNow;
            Priority = TaskPriority.Normal;
        }

        public IdleDetectionTask(string taskName, IdleDetectionTaskType taskType, Func<CancellationToken, Task> executeAsync, TaskPriority priority = TaskPriority.Normal)
        {
            TaskId = Guid.NewGuid();
            TaskName = taskName ?? throw new ArgumentNullException(nameof(taskName));
            TaskType = taskType;
            ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            CreatedTime = DateTime.UtcNow;
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
        CustomTask
    }

    public enum TaskPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}