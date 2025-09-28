using System;

namespace LicenseReleaseService.IdleDetection.Events
{
    public class IdleDetectionTimerEventArgs : EventArgs
    {
        public Guid TimerId { get; set; }
        public string TimerName { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Interval { get; set; }
        public TimerStatus Status { get; set; }
        public string Message { get; set; }

        public IdleDetectionTimerEventArgs()
        {
            TimerId = Guid.NewGuid();
            TimerName = string.Empty;
            StartTime = DateTime.UtcNow;
            Interval = TimeSpan.Zero;
            Status = TimerStatus.Stopped;
            Message = string.Empty;
        }

        public IdleDetectionTimerEventArgs(Guid timerId, string timerName, DateTime startTime, TimeSpan interval, TimerStatus status, string message = null)
        {
            TimerId = timerId;
            TimerName = timerName ?? throw new ArgumentNullException(nameof(timerName));
            StartTime = startTime;
            Interval = interval;
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    public enum TimerStatus
    {
        Stopped,
        Running,
        Paused,
        Faulted,
        Disposed
    }
}