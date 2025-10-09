using System;
using System.Collections.Generic;

namespace LicenseReleaseService.IdleDetection.Models
{
    public class TimerExecutionStatistics
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long TotalExecutions { get; set; }
        public long SuccessfulExecutions { get; set; }
        public long FailedExecutions { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan MinExecutionTime { get; set; }
        public TimeSpan MaxExecutionTime { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public Dictionary<string, long> TaskTypeCounts { get; set; }
        public Dictionary<string, long> ErrorCounts { get; set; }

        // Missing properties
        public bool IsEnabled { get; set; }
        public bool IsRunning { get; set; }
        public string TimerState { get; set; }
        public int PendingTaskCount { get; set; }
        public long TotalTimerRegistrations { get; set; }
        public long EnabledTimerRegistrations { get; set; }
        public long TotalErrors { get; set; }
        public DateTime? LastExecutionTime { get; set; }
        public Dictionary<string, object> TimerMetrics { get; set; }
        public string ErrorMessage { get; set; }

        public TimerExecutionStatistics()
        {
            StartTime = DateTime.UtcNow;
            TotalExecutions = 0;
            SuccessfulExecutions = 0;
            FailedExecutions = 0;
            AverageExecutionTime = TimeSpan.Zero;
            MinExecutionTime = TimeSpan.Zero;
            MaxExecutionTime = TimeSpan.Zero;
            TotalExecutionTime = TimeSpan.Zero;
            TaskTypeCounts = new Dictionary<string, long>();
            ErrorCounts = new Dictionary<string, long>();

            // Initialize missing properties
            IsEnabled = false;
            IsRunning = false;
            TimerState = "Stopped";
            PendingTaskCount = 0;
            TotalTimerRegistrations = 0;
            EnabledTimerRegistrations = 0;
            TotalErrors = 0;
            LastExecutionTime = null;
            TimerMetrics = new Dictionary<string, object>();
            ErrorMessage = null;
        }

        public void RecordExecution(TimeSpan executionTime, bool success, string taskType, string errorType = null)
        {
            TotalExecutions++;
            TotalExecutionTime += executionTime;

            if (success)
            {
                SuccessfulExecutions++;
            }
            else
            {
                FailedExecutions++;
                if (!string.IsNullOrWhiteSpace(errorType))
                {
                    ErrorCounts.TryGetValue(errorType, out var count);
                    ErrorCounts[errorType] = count + 1;
                }
            }

            // Update execution time statistics
            if (TotalExecutions == 1)
            {
                MinExecutionTime = executionTime;
                MaxExecutionTime = executionTime;
                AverageExecutionTime = executionTime;
            }
            else
            {
                MinExecutionTime = executionTime < MinExecutionTime ? executionTime : MinExecutionTime;
                MaxExecutionTime = executionTime > MaxExecutionTime ? executionTime : MaxExecutionTime;
                AverageExecutionTime = TimeSpan.FromTicks(TotalExecutionTime.Ticks / TotalExecutions);
            }

            // Update task type counts
            if (!string.IsNullOrWhiteSpace(taskType))
            {
                TaskTypeCounts.TryGetValue(taskType, out var count);
                TaskTypeCounts[taskType] = count + 1;
            }
        }

        public void Reset()
        {
            StartTime = DateTime.UtcNow;
            EndTime = null;
            TotalExecutions = 0;
            SuccessfulExecutions = 0;
            FailedExecutions = 0;
            AverageExecutionTime = TimeSpan.Zero;
            MinExecutionTime = TimeSpan.Zero;
            MaxExecutionTime = TimeSpan.Zero;
            TotalExecutionTime = TimeSpan.Zero;
            TaskTypeCounts.Clear();
            ErrorCounts.Clear();
        }
    }
}