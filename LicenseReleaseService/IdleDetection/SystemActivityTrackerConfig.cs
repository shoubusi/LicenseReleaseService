using System;
using System.ComponentModel.DataAnnotations;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Configuration for system activity tracking
    /// </summary>
    public class SystemActivityTrackerConfig
    {
        /// <summary>
        /// Gets or sets the tracking interval
        /// </summary>
        [Required]
        [Range(100, 60000)]
        public int TrackingInterval { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the event retention period
        /// </summary>
        [Required]
        public TimeSpan EventRetentionPeriod { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the process record retention period
        /// </summary>
        [Required]
        public TimeSpan ProcessRecordRetentionPeriod { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets a value indicating whether input tracking is enabled
        /// </summary>
        public bool EnableInputTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether keyboard tracking is enabled
        /// </summary>
        public bool EnableKeyboardTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether mouse tracking is enabled
        /// </summary>
        public bool EnableMouseTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether mouse position tracking is enabled
        /// </summary>
        public bool EnableMousePositionTracking { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether system resource tracking is enabled
        /// </summary>
        public bool EnableSystemResourceTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether process monitoring is enabled
        /// </summary>
        public bool EnableProcessMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether window tracking is enabled
        /// </summary>
        public bool EnableWindowTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets the CPU activity threshold percentage
        /// </summary>
        [Range(0, 100)]
        public double CpuActivityThreshold { get; set; } = 10.0;

        /// <summary>
        /// Gets or sets the memory activity threshold percentage
        /// </summary>
        [Range(0, 100)]
        public double MemoryActivityThreshold { get; set; } = 50.0;

        /// <summary>
        /// Gets or sets the disk activity threshold percentage
        /// </summary>
        [Range(0, 100)]
        public double DiskActivityThreshold { get; set; } = 5.0;

        /// <summary>
        /// Gets or sets the network activity threshold in KB/s
        /// </summary>
        [Range(0, 10000)]
        public double NetworkActivityThreshold { get; set; } = 100.0;

        /// <summary>
        /// Gets or sets the maximum number of events to retain
        /// </summary>
        [Range(100, 10000)]
        public int MaxEventsToRetain { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum number of process records to retain
        /// </summary>
        [Range(10, 1000)]
        public int MaxProcessRecordsToRetain { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether detailed logging is enabled
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public void Validate()
        {
            if (TrackingInterval <= 0)
                throw new ArgumentException("Tracking interval must be positive", nameof(TrackingInterval));

            if (EventRetentionPeriod <= TimeSpan.Zero)
                throw new ArgumentException("Event retention period must be positive", nameof(EventRetentionPeriod));

            if (ProcessRecordRetentionPeriod <= TimeSpan.Zero)
                throw new ArgumentException("Process record retention period must be positive", nameof(ProcessRecordRetentionPeriod));

            if (CpuActivityThreshold < 0 || CpuActivityThreshold > 100)
                throw new ArgumentException("CPU activity threshold must be between 0 and 100", nameof(CpuActivityThreshold));

            if (MemoryActivityThreshold < 0 || MemoryActivityThreshold > 100)
                throw new ArgumentException("Memory activity threshold must be between 0 and 100", nameof(MemoryActivityThreshold));

            if (DiskActivityThreshold < 0 || DiskActivityThreshold > 100)
                throw new ArgumentException("Disk activity threshold must be between 0 and 100", nameof(DiskActivityThreshold));

            if (NetworkActivityThreshold < 0)
                throw new ArgumentException("Network activity threshold must be non-negative", nameof(NetworkActivityThreshold));
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"TrackingInterval: {TrackingInterval}ms, " +
                   $"EventRetention: {EventRetentionPeriod}, " +
                   $"ProcessRecordRetention: {ProcessRecordRetentionPeriod}, " +
                   $"InputTracking: {EnableInputTracking}, " +
                   $"SystemResourceTracking: {EnableSystemResourceTracking}, " +
                   $"ProcessMonitoring: {EnableProcessMonitoring}, " +
                   $"WindowTracking: {EnableWindowTracking}, " +
                   $"CpuThreshold: {CpuActivityThreshold}%, " +
                   $"MemoryThreshold: {MemoryActivityThreshold}%, " +
                   $"DiskThreshold: {DiskActivityThreshold}%, " +
                   $"NetworkThreshold: {NetworkActivityThreshold}KB/s";
        }
    }
}