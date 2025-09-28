using System;
using System.Collections.Concurrent;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Statistics for system activity tracking
    /// </summary>
    public class SystemActivityTrackerStats
    {
        /// <summary>
        /// Gets the total number of events processed
        /// </summary>
        public long TotalEventsProcessed { get; set; }

        /// <summary>
        /// Gets the number of events notified
        /// </summary>
        public long EventsNotified { get; set; }

        /// <summary>
        /// Gets the number of process events notified
        /// </summary>
        public long ProcessEventsNotified { get; set; }

        /// <summary>
        /// Gets the number of errors encountered
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Gets the number of events by activity type
        /// </summary>
        public ConcurrentDictionary<SystemActivityType, long> EventsByType { get; } = new ConcurrentDictionary<SystemActivityType, long>();

        /// <summary>
        /// Gets the average events per minute
        /// </summary>
        public double AverageEventsPerMinute { get; set; }

        /// <summary>
        /// Gets the peak events per minute
        /// </summary>
        public double PeakEventsPerMinute { get; set; }

        /// <summary>
        /// Gets the start time
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the last activity time
        /// </summary>
        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the uptime
        /// </summary>
        public TimeSpan Uptime => DateTime.UtcNow - StartTime;

        /// <summary>
        /// Gets the time since last activity
        /// </summary>
        public TimeSpan TimeSinceLastActivity => DateTime.UtcNow - LastActivityTime;

        /// <summary>
        /// Increments the count for a specific activity type
        /// </summary>
        public void IncrementActivityType(SystemActivityType activityType)
        {
            EventsByType.AddOrUpdate(activityType, 1, (key, oldValue) => oldValue + 1);
        }

        /// <summary>
        /// Gets the count for a specific activity type
        /// </summary>
        public long GetActivityTypeCount(SystemActivityType activityType)
        {
            return EventsByType.TryGetValue(activityType, out var count) ? count : 0;
        }

        /// <summary>
        /// Resets all statistics
        /// </summary>
        public void Reset()
        {
            TotalEventsProcessed = 0;
            EventsNotified = 0;
            ProcessEventsNotified = 0;
            ErrorCount = 0;
            AverageEventsPerMinute = 0;
            PeakEventsPerMinute = 0;
            StartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            EventsByType.Clear();
        }

        /// <summary>
        /// Updates activity timing statistics
        /// </summary>
        public void UpdateActivityTiming()
        {
            LastActivityTime = DateTime.UtcNow;

            var uptimeMinutes = Uptime.TotalMinutes;
            if (uptimeMinutes > 0)
            {
                AverageEventsPerMinute = TotalEventsProcessed / uptimeMinutes;
                PeakEventsPerMinute = Math.Max(PeakEventsPerMinute, AverageEventsPerMinute);
            }
        }

        /// <summary>
        /// Returns a string representation of the statistics
        /// </summary>
        public override string ToString()
        {
            return $"Uptime: {Uptime}, " +
                   $"TotalEvents: {TotalEventsProcessed}, " +
                   $"EventsNotified: {EventsNotified}, " +
                   $"ProcessEvents: {ProcessEventsNotified}, " +
                   $"Errors: {ErrorCount}, " +
                   $"AvgEvents/Min: {AverageEventsPerMinute:F2}, " +
                   $"PeakEvents/Min: {PeakEventsPerMinute:F2}, " +
                   $"LastActivity: {TimeSinceLastActivity.TotalSeconds:F1}s ago";
        }
    }
}