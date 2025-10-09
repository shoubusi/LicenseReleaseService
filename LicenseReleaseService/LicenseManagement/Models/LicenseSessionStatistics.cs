using System;
using System.Collections.Generic;

namespace LicenseReleaseService.LicenseManagement.Models
{
    public class LicenseSessionStatistics
    {
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int IdleSessions { get; set; }
        public int ReleasedSessions { get; set; }
        public TimeSpan AverageIdleDuration { get; set; }
        public TimeSpan MaxIdleDuration { get; set; }
        public DateTime LastActivityTime { get; set; }
        public Dictionary<string, int> FeatureUsage { get; set; }
        public Dictionary<string, int> UserUsage { get; set; }
        public List<LicenseSessionInfo> MonitoredSessions { get; set; }

        // Additional properties for LicenseManagementIntegration
        public int MonitoredSessionsCount { get; set; }
        public TimeSpan TotalIdleTime { get; set; }
        public TimeSpan AverageIdleTime { get; set; }
        public TimeSpan LongestIdleTime { get; set; }
        public double ConfidenceThreshold { get; set; }
        public double AverageConfidenceScore { get; set; }
        public double MinConfidenceScore { get; set; }
        public double MaxConfidenceScore { get; set; }

        public LicenseSessionStatistics()
        {
            TotalSessions = 0;
            ActiveSessions = 0;
            IdleSessions = 0;
            ReleasedSessions = 0;
            AverageIdleDuration = TimeSpan.Zero;
            MaxIdleDuration = TimeSpan.Zero;
            LastActivityTime = DateTime.UtcNow;
            FeatureUsage = new Dictionary<string, int>();
            UserUsage = new Dictionary<string, int>();
            MonitoredSessions = new List<LicenseSessionInfo>();

            // Initialize additional properties
            MonitoredSessionsCount = 0;
            TotalIdleTime = TimeSpan.Zero;
            AverageIdleTime = TimeSpan.Zero;
            LongestIdleTime = TimeSpan.Zero;
            ConfidenceThreshold = 0.7;
            AverageConfidenceScore = 0.0;
            MinConfidenceScore = 0.0;
            MaxConfidenceScore = 0.0;
        }
    }

    /// <summary>
    /// Represents information about a monitored license session
    /// </summary>
    public class LicenseSessionInfo
    {
        public string SessionId { get; set; }
        public int ProcessId { get; set; }
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public string Feature { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public TimeSpan IdleDuration { get; set; }
        public SessionState State { get; set; }
        public bool IsMonitored { get; set; }

        public LicenseSessionInfo()
        {
            SessionId = string.Empty;
            UserName = string.Empty;
            ComputerName = string.Empty;
            Feature = string.Empty;
            StartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            State = SessionState.Unknown;
            IsMonitored = false;
        }
    }

    /// <summary>
    /// Represents session states
    /// </summary>
    public enum SessionState
    {
        Unknown,
        Active,
        Idle,
        Suspended,
        Terminated
    }
}