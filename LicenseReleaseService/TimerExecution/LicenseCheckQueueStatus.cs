using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Status information for the license check queue
    /// </summary>
    public class LicenseCheckQueueStatus
    {
        /// <summary>
        /// Gets a value indicating whether the queue is empty
        /// </summary>
        public bool IsEmpty { get; set; }

        /// <summary>
        /// Gets a value indicating whether the queue is full
        /// </summary>
        public bool IsFull { get; set; }

        /// <summary>
        /// Gets a value indicating whether the queue is processing
        /// </summary>
        public bool IsProcessing { get; set; }

        /// <summary>
        /// Gets the current number of operations in the queue
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets the maximum queue capacity
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// Gets the queue utilization percentage
        /// </summary>
        public double UtilizationPercentage { get; set; }

        /// <summary>
        /// Gets the queue statistics
        /// </summary>
        public LicenseCheckQueueStatistics Statistics { get; set; }

        /// <summary>
        /// Gets the timestamp when this status was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckQueueStatus class
        /// </summary>
        public LicenseCheckQueueStatus()
        {
            GeneratedAt = DateTime.UtcNow;
        }
    }
}