using System;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Event arguments for performance threshold events
    /// </summary>
    public class PerformanceThresholdEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the performance counter
        /// </summary>
        public string CounterName { get; set; }

        /// <summary>
        /// Gets the current value of the performance counter
        /// </summary>
        public float CurrentValue { get; set; }

        /// <summary>
        /// Gets the threshold value that was exceeded
        /// </summary>
        public float ThresholdValue { get; set; }

        /// <summary>
        /// Gets the timestamp when the threshold was exceeded
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets a value indicating whether this is an upper threshold breach
        /// </summary>
        public bool IsUpperThreshold { get; set; }

        /// <summary>
        /// Initializes a new instance of the PerformanceThresholdEventArgs class
        /// </summary>
        public PerformanceThresholdEventArgs()
        {
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the PerformanceThresholdEventArgs class
        /// </summary>
        /// <param name="counterName">Name of the performance counter</param>
        /// <param name="currentValue">Current value</param>
        /// <param name="thresholdValue">Threshold value</param>
        /// <param name="isUpperThreshold">Whether this is an upper threshold breach</param>
        public PerformanceThresholdEventArgs(string counterName, float currentValue, float thresholdValue, bool isUpperThreshold)
        {
            CounterName = counterName ?? throw new ArgumentNullException(nameof(counterName));
            CurrentValue = currentValue;
            ThresholdValue = thresholdValue;
            IsUpperThreshold = isUpperThreshold;
            Timestamp = DateTime.UtcNow;
        }
    }
}