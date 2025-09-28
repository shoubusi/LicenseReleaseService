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
        }
    }
}