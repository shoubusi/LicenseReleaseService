using System;
using LicenseReleaseService.IdleDetection.Models;

namespace LicenseReleaseService.IdleDetection.Events
{
    public class IdleDetectionTaskEventArgs : EventArgs
    {
        public IdleDetectionTask Task { get; set; }
        public DateTime ExecutionTime { get; set; }
        public TimeSpan ExecutionDuration { get; set; }
        public string Message { get; set; }

        public IdleDetectionTaskEventArgs()
        {
            ExecutionTime = DateTime.UtcNow;
            ExecutionDuration = TimeSpan.Zero;
            Message = string.Empty;
        }

        public IdleDetectionTaskEventArgs(IdleDetectionTask task, DateTime executionTime, TimeSpan executionDuration, string message = null)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            ExecutionTime = executionTime;
            ExecutionDuration = executionDuration;
            Message = message ?? string.Empty;
        }
    }
}