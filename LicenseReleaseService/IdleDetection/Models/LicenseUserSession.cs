using System;
using System.Collections.Generic;

namespace LicenseReleaseService.IdleDetection.Models
{
    public class LicenseUserSession
    {
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public string Feature { get; set; }
        public DateTime SessionStartTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public DateTime LastCheckTime { get; set; }
        public TimeSpan IdleDuration { get; set; }
        public bool IsIdle { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> SessionData { get; set; }
        public List<string> ActiveProcesses { get; set; }
        public List<string> ActiveDocuments { get; set; }
        public Dictionary<string, DateTime> LastFileAccessTimes { get; set; }

        public LicenseUserSession()
        {
            SessionStartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            LastCheckTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            IsIdle = false;
            IsActive = true;
            SessionData = new Dictionary<string, object>();
            ActiveProcesses = new List<string>();
            ActiveDocuments = new List<string>();
            LastFileAccessTimes = new Dictionary<string, DateTime>();
        }

        public LicenseUserSession(string userName, string computerName, string feature)
        {
            UserName = userName ?? throw new ArgumentNullException(nameof(userName));
            ComputerName = computerName ?? throw new ArgumentNullException(nameof(computerName));
            Feature = feature ?? throw new ArgumentNullException(nameof(feature));
            SessionStartTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            LastCheckTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            IsIdle = false;
            IsActive = true;
            SessionData = new Dictionary<string, object>();
            ActiveProcesses = new List<string>();
            ActiveDocuments = new List<string>();
            LastFileAccessTimes = new Dictionary<string, DateTime>();
        }

        public void UpdateActivity()
        {
            LastActivityTime = DateTime.UtcNow;
            IdleDuration = TimeSpan.Zero;
            IsIdle = false;
        }

        public void UpdateIdleStatus(TimeSpan idleDuration)
        {
            IdleDuration = idleDuration;
            IsIdle = idleDuration > TimeSpan.Zero;
        }

        public void AddProcess(string processName)
        {
            if (!string.IsNullOrWhiteSpace(processName) && !ActiveProcesses.Contains(processName))
            {
                ActiveProcesses.Add(processName);
                UpdateActivity();
            }
        }

        public void RemoveProcess(string processName)
        {
            ActiveProcesses.Remove(processName);
        }

        public void AddDocument(string documentPath)
        {
            if (!string.IsNullOrWhiteSpace(documentPath) && !ActiveDocuments.Contains(documentPath))
            {
                ActiveDocuments.Add(documentPath);
                UpdateActivity();
                LastFileAccessTimes[documentPath] = DateTime.UtcNow;
            }
        }

        public void UpdateDocumentAccess(string documentPath)
        {
            if (!string.IsNullOrWhiteSpace(documentPath))
            {
                LastFileAccessTimes[documentPath] = DateTime.UtcNow;
                UpdateActivity();
            }
        }

        public void RemoveDocument(string documentPath)
        {
            ActiveDocuments.Remove(documentPath);
            LastFileAccessTimes.Remove(documentPath);
        }

        public TimeSpan GetTotalIdleDuration()
        {
            return DateTime.UtcNow - LastActivityTime;
        }
    }
}