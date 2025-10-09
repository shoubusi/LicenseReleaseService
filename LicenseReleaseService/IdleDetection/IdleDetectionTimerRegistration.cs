using System;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Represents a timer registration for idle detection
    /// </summary>
    public class IdleDetectionTimerRegistration : TimerRegistration
    {
        /// <summary>
        /// Gets or sets the name of the detector associated with this timer
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets or sets the detection interval
        /// </summary>
        public TimeSpan DetectionInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum detection time
        /// </summary>
        public TimeSpan MaxDetectionTime { get; set; }

        /// <summary>
        /// Gets or sets the last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets whether the timer is currently running
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Gets or sets the average execution time
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the idle detection configuration
        /// </summary>
        public object Configuration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the detector is active
        /// </summary>
        public bool IsDetectorActive { get; set; }

        /// <summary>
        /// Gets or sets the last detection result
        /// </summary>
        public object LastDetectionResult { get; set; }

        /// <summary>
        /// Initializes a new instance of the IdleDetectionTimerRegistration class
        /// </summary>
        public IdleDetectionTimerRegistration()
        {
            DetectionInterval = TimeSpan.FromSeconds(30);
            MaxDetectionTime = TimeSpan.FromMinutes(5);
            LastUpdated = DateTime.UtcNow;
            IsDetectorActive = true;
        }

        /// <summary>
        /// Creates a clone of this registration
        /// </summary>
        /// <returns>A new instance with the same values</returns>
        public new IdleDetectionTimerRegistration Clone()
        {
            return new IdleDetectionTimerRegistration
            {
                TimerId = this.TimerId,
                TimerName = this.TimerName,
                Interval = this.Interval,
                IsEnabled = this.IsEnabled,
                IsRunning = this.IsRunning,
                StartedAt = this.StartedAt,
                StoppedAt = this.StoppedAt,
                ExecutionCount = this.ExecutionCount,
                ErrorCount = this.ErrorCount,
                LastExecution = this.LastExecution,
                TotalExecutionTime = this.TotalExecutionTime,
                AverageExecutionTime = this.AverageExecutionTime,
                DetectorName = this.DetectorName,
                DetectionInterval = this.DetectionInterval,
                MaxDetectionTime = this.MaxDetectionTime,
                LastUpdated = this.LastUpdated,
                Configuration = this.Configuration,
                IsDetectorActive = this.IsDetectorActive,
                LastDetectionResult = this.LastDetectionResult
            };
        }

        /// <summary>
        /// Returns a string representation of the timer registration
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"IdleDetectionTimerRegistration[{TimerName}, Detector={DetectorName}, " +
                   $"Interval={DetectionInterval.TotalSeconds}s, Enabled={IsEnabled}, " +
                   $"Active={IsDetectorActive}, Executions={ExecutionCount}, Errors={ErrorCount}]";
        }
    }
}