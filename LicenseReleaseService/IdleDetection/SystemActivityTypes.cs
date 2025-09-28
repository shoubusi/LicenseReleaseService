using System;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Types of system activities that can be tracked
    /// </summary>
    public enum SystemActivityType
    {
        /// <summary>
        /// Keyboard input activity
        /// </summary>
        Keyboard = 1,

        /// <summary>
        /// Mouse input activity
        /// </summary>
        Mouse = 2,

        /// <summary>
        /// CPU usage activity
        /// </summary>
        CpuUsage = 3,

        /// <summary>
        /// Memory usage activity
        /// </summary>
        MemoryUsage = 4,

        /// <summary>
        /// Disk usage activity
        /// </summary>
        DiskUsage = 5,

        /// <summary>
        /// Network usage activity
        /// </summary>
        NetworkUsage = 6,

        /// <summary>
        /// Process start event
        /// </summary>
        ProcessStart = 7,

        /// <summary>
        /// Process activity monitoring
        /// </summary>
        ProcessActivity = 8,

        /// <summary>
        /// Window focus change
        /// </summary>
        WindowFocus = 9,

        /// <summary>
        /// Power state change
        /// </summary>
        PowerState = 10,

        /// <summary>
        /// System idle state
        /// </summary>
        SystemIdle = 11,

        /// <summary>
        /// User session activity
        /// </summary>
        UserSession = 12,

        /// <summary>
        /// System service activity
        /// </summary>
        ServiceActivity = 13
    }

    /// <summary>
    /// Represents a system activity event
    /// </summary>
    public class SystemActivityEvent
    {
        /// <summary>
        /// Gets or sets the type of activity
        /// </summary>
        public SystemActivityType ActivityType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the activity occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the confidence level (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the process ID associated with the activity
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the process name associated with the activity
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets or sets the window title associated with the activity
        /// </summary>
        public string WindowTitle { get; set; }

        /// <summary>
        /// Gets or sets the idle time associated with the activity
        /// </summary>
        public TimeSpan? IdleTime { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double? CpuUsage { get; set; }

        /// <summary>
        /// Gets or sets the memory usage percentage
        /// </summary>
        public double? MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the disk usage percentage
        /// </summary>
        public double? DiskUsage { get; set; }

        /// <summary>
        /// Gets or sets the network usage (KB/s)
        /// </summary>
        public double? NetworkUsage { get; set; }

        /// <summary>
        /// Gets or sets additional metadata for the activity
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; } = new System.Collections.Generic.Dictionary<string, object>();

        /// <summary>
        /// Returns a string representation of the activity event
        /// </summary>
        public override string ToString()
        {
            return $"{ActivityType} at {Timestamp:yyyy-MM-dd HH:mm:ss.fff} " +
                   $"(Confidence: {Confidence:F2}, " +
                   $"Process: {ProcessName ?? "N/A"} ({ProcessId?.ToString() ?? "N/A"})";
        }
    }

    /// <summary>
    /// Represents a process activity record
    /// </summary>
    public class ProcessActivityRecord
    {
        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets or sets the start time of the process
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the last activity time
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the working set size in bytes
        /// </summary>
        public long WorkingSetSize { get; set; }

        /// <summary>
        /// Gets or sets the private memory size in bytes
        /// </summary>
        public long PrivateMemorySize { get; set; }

        /// <summary>
        /// Gets or sets the thread count
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the CPU time in milliseconds
        /// </summary>
        public double CpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is responding
        /// </summary>
        public bool IsResponding { get; set; }

        /// <summary>
        /// Gets or sets the file handles count
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Gets or sets the number of activity events detected for this process
        /// </summary>
        public int ActivityEventCount { get; set; }

        /// <summary>
        /// Returns a string representation of the process record
        /// </summary>
        public override string ToString()
        {
            return $"{ProcessName} (ID: {ProcessId}) - " +
                   $"Active: {IsActive}, " +
                   $"Responding: {IsResponding}, " +
                   $"CPU: {CpuTimeMs:F0}ms, " +
                   $"Memory: {WorkingSetSize / 1024 / 1024:F1}MB, " +
                   $"Threads: {ThreadCount}";
        }
    }

    /// <summary>
    /// Represents system power information
    /// </summary>
    public class SystemPowerInfo
    {
        /// <summary>
        /// Gets or sets the maximum idleness allowed
        /// </summary>
        public uint MaxIdlenessAllowed { get; set; }

        /// <summary>
        /// Gets or sets the current idleness level
        /// </summary>
        public uint CurrentIdleness { get; set; }

        /// <summary>
        /// Gets or sets the time remaining until power state change
        /// </summary>
        public TimeSpan TimeRemaining { get; set; }

        /// <summary>
        /// Gets or sets the cooling mode
        /// </summary>
        public byte CoolingMode { get; set; }

        /// <summary>
        /// Returns a string representation of the power information
        /// </summary>
        public override string ToString()
        {
            return $"Idleness: {CurrentIdleness}/{MaxIdlenessAllowed}, " +
                   $"TimeRemaining: {TimeRemaining}, " +
                   $"CoolingMode: {CoolingMode}";
        }
    }

    /// <summary>
    /// Represents foreground window information
    /// </summary>
    public class WindowInfo
    {
        /// <summary>
        /// Gets or sets the window handle
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets or sets the window title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Returns a string representation of the window information
        /// </summary>
        public override string ToString()
        {
            return $"Window: '{Title}' by {ProcessName} (ID: {ProcessId})";
        }
    }

    /// <summary>
    /// Represents mouse position information
    /// </summary>
    public class MousePositionInfo
    {
        /// <summary>
        /// Gets or sets the X coordinate
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Returns a string representation of the mouse position
        /// </summary>
        public override string ToString()
        {
            return $"Mouse position: ({X}, {Y}) at {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
}