using System;
using LicenseReleaseService.IdleDetection.Models;

namespace LicenseReleaseService.IdleDetection.Events
{
    public class IdleDetectionTaskErrorEventArgs : EventArgs
    {
        public IdleDetectionTask Task { get; set; }
        public Exception Exception { get; set; }
        public DateTime ErrorTime { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsRecoverable { get; set; }

        public IdleDetectionTaskErrorEventArgs()
        {
            ErrorTime = DateTime.UtcNow;
            ErrorMessage = string.Empty;
            IsRecoverable = false;
        }

        public IdleDetectionTaskErrorEventArgs(IdleDetectionTask task, Exception exception, DateTime errorTime, string errorMessage = null, bool isRecoverable = false)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            ErrorTime = errorTime;
            ErrorMessage = errorMessage ?? exception.Message;
            IsRecoverable = isRecoverable;
        }
    }
}