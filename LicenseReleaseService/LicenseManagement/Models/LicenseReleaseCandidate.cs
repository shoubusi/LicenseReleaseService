using System;
using System.Collections.Generic;

namespace LicenseReleaseService.LicenseManagement.Models
{
    public class LicenseReleaseCandidate
    {
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public string Feature { get; set; }
        public DateTime SessionStartTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public TimeSpan IdleDuration { get; set; }
        public bool IsIdle { get; set; }
        public string LicenseServer { get; set; }
        public string SessionId { get; set; }
        public string Server { get; set; }
        public int Port { get; set; }
        public TimeSpan IdleTime { get; set; }
        public string Reason { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> DetectionMethods { get; set; }
        public DateTime LastActivity { get; set; }

        public LicenseReleaseCandidate()
        {
            SessionStartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            IsIdle = false;
            SessionId = string.Empty;
            Server = string.Empty;
            Port = 0;
            IdleTime = TimeSpan.Zero;
            Reason = string.Empty;
            ConfidenceScore = 0.0;
            DetectionMethods = new List<string>();
            LastActivity = DateTime.UtcNow;
        }

        public LicenseReleaseCandidate(string userName, string computerName, string feature, string licenseServer)
        {
            UserName = userName;
            ComputerName = computerName;
            Feature = feature;
            LicenseServer = licenseServer;
            SessionStartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            IsIdle = false;
            SessionId = string.Empty;
            Server = string.Empty;
            Port = 0;
            IdleTime = TimeSpan.Zero;
            Reason = string.Empty;
            ConfidenceScore = 0.0;
            DetectionMethods = new List<string>();
            LastActivity = DateTime.UtcNow;
        }
    }
}