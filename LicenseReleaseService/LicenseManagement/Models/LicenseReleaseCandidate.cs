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
        public int SessionId { get; set; }

        public LicenseReleaseCandidate()
        {
            SessionStartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            IsIdle = false;
            SessionId = 0;
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
            SessionId = 0;
        }
    }
}