using System;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents the status of the recovery queue
    /// </summary>
    public class RecoveryQueueStatus
    {
        /// <summary>
        /// Gets or sets the current number of items in the queue
        /// </summary>
        public int QueueSize { get; set; }

        /// <summary>
        /// Gets or sets the number of active recovery operations
        /// </summary>
        public int ActiveRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the maximum queue size
        /// </summary>
        public int MaxQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum concurrent recoveries
        /// </summary>
        public int MaxConcurrentRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the highest priority pending request
        /// </summary>
        public RecoveryPriority HighestPriorityPending { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the oldest pending request
        /// </summary>
        public DateTime? OldestPendingRequest { get; set; }

        /// <summary>
        /// Gets or sets the processing interval
        /// </summary>
        public TimeSpan ProcessingInterval { get; set; }

        /// <summary>
        /// Gets or sets the recovery statistics
        /// </summary>
        public RecoveryEngineStatistics Statistics { get; set; }

        /// <summary>
        /// Gets a value indicating whether the queue is full
        /// </summary>
        public bool IsQueueFull => QueueSize >= MaxQueueSize;

        /// <summary>
        /// Gets a value indicating whether the system is at maximum capacity
        /// </summary>
        public bool IsAtMaxCapacity => ActiveRecoveries >= MaxConcurrentRecoveries;

        /// <summary>
        /// Gets a value indicating whether there are pending requests
        /// </summary>
        public bool HasPendingRequests => QueueSize > 0;

        /// <summary>
        /// Gets the queue utilization percentage
        /// </summary>
        public double QueueUtilization => MaxQueueSize > 0 ? (double)QueueSize / MaxQueueSize * 100 : 0;

        /// <summary>
        /// Gets the capacity utilization percentage
        /// </summary>
        public double CapacityUtilization => MaxConcurrentRecoveries > 0 ? (double)ActiveRecoveries / MaxConcurrentRecoveries * 100 : 0;

        /// <summary>
        /// Returns a string representation of the queue status
        /// </summary>
        public override string ToString()
        {
            return $"RecoveryQueueStatus[Queue={QueueSize}/{MaxQueueSize} ({QueueUtilization:F1}%), " +
                   $"Active={ActiveRecoveries}/{MaxConcurrentRecoveries} ({CapacityUtilization:F1}%), " +
                   $"HighestPriority={HighestPriorityPending}, " +
                   $"Oldest={OldestPendingRequest:yyyy-MM-dd HH:mm:ss}, " +
                   $"ProcessingInterval={ProcessingInterval.TotalSeconds:F1}s, " +
                   $"SuccessRate={Statistics.SuccessRate:P2}]";
        }
    }
}