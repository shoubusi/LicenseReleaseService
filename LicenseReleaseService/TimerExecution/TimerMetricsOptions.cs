using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for metrics collection
    /// </summary>
    public class TimerMetricsOptions
    {
        private int _collectionIntervalMs = 5000;
        private int _maxSnapshotsToKeep = 100;
        private double _cpuUsageThreshold = 80.0;
        private double _memoryUsageThreshold = 85.0;
        private int _threadCountThreshold = 100;
        private bool _enablePerformanceCounters = true;
        private bool _enableDetailedMetrics = true;
        private bool _enableTrendAnalysis = true;

        /// <summary>
        /// Gets or sets the interval for metrics collection in milliseconds
        /// </summary>
        public int CollectionIntervalMs
        {
            get => _collectionIntervalMs;
            set
            {
                if (value < 100)
                    throw new ArgumentException("Collection interval must be at least 100ms", nameof(value));
                _collectionIntervalMs = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of snapshots to keep in memory
        /// </summary>
        public int MaxSnapshotsToKeep
        {
            get => _maxSnapshotsToKeep;
            set
            {
                if (value < 10)
                    throw new ArgumentException("Max snapshots must be at least 10", nameof(value));
                _maxSnapshotsToKeep = value;
            }
        }

        /// <summary>
        /// Gets or sets the CPU usage threshold percentage
        /// </summary>
        public double CpuUsageThreshold
        {
            get => _cpuUsageThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("CPU usage threshold must be between 0 and 100", nameof(value));
                _cpuUsageThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the memory usage threshold percentage
        /// </summary>
        public double MemoryUsageThreshold
        {
            get => _memoryUsageThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Memory usage threshold must be between 0 and 100", nameof(value));
                _memoryUsageThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the thread count threshold
        /// </summary>
        public int ThreadCountThreshold
        {
            get => _threadCountThreshold;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Thread count threshold must be at least 1", nameof(value));
                _threadCountThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable performance counters
        /// </summary>
        public bool EnablePerformanceCounters
        {
            get => _enablePerformanceCounters;
            set => _enablePerformanceCounters = value;
        }

        /// <summary>
        /// Gets or sets whether to enable detailed metrics collection
        /// </summary>
        public bool EnableDetailedMetrics
        {
            get => _enableDetailedMetrics;
            set => _enableDetailedMetrics = value;
        }

        /// <summary>
        /// Gets or sets whether to enable trend analysis
        /// </summary>
        public bool EnableTrendAnalysis
        {
            get => _enableTrendAnalysis;
            set => _enableTrendAnalysis = value;
        }

        /// <summary>
        /// Initializes a new instance of the TimerMetricsOptions class
        /// </summary>
        public TimerMetricsOptions()
        {
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public TimerMetricsOptions Clone()
        {
            return new TimerMetricsOptions
            {
                CollectionIntervalMs = CollectionIntervalMs,
                MaxSnapshotsToKeep = MaxSnapshotsToKeep,
                CpuUsageThreshold = CpuUsageThreshold,
                MemoryUsageThreshold = MemoryUsageThreshold,
                ThreadCountThreshold = ThreadCountThreshold,
                EnablePerformanceCounters = EnablePerformanceCounters,
                EnableDetailedMetrics = EnableDetailedMetrics,
                EnableTrendAnalysis = EnableTrendAnalysis
            };
        }

        /// <summary>
        /// Gets default options for server environments
        /// </summary>
        /// <returns>Default server options</returns>
        public static TimerMetricsOptions ServerDefaults()
        {
            return new TimerMetricsOptions
            {
                CollectionIntervalMs = 10000,
                MaxSnapshotsToKeep = 200,
                CpuUsageThreshold = 75.0,
                MemoryUsageThreshold = 80.0,
                ThreadCountThreshold = 150,
                EnablePerformanceCounters = true,
                EnableDetailedMetrics = true,
                EnableTrendAnalysis = true
            };
        }

        /// <summary>
        /// Gets options for development environments
        /// </summary>
        /// <returns>Development options</returns>
        public static TimerMetricsOptions DevelopmentDefaults()
        {
            return new TimerMetricsOptions
            {
                CollectionIntervalMs = 2000,
                MaxSnapshotsToKeep = 50,
                CpuUsageThreshold = 90.0,
                MemoryUsageThreshold = 95.0,
                ThreadCountThreshold = 50,
                EnablePerformanceCounters = true,
                EnableDetailedMetrics = false,
                EnableTrendAnalysis = false
            };
        }

        /// <summary>
        /// Gets options for high-performance environments
        /// </summary>
        /// <returns>High-performance options</returns>
        public static TimerMetricsOptions HighPerformanceDefaults()
        {
            return new TimerMetricsOptions
            {
                CollectionIntervalMs = 1000,
                MaxSnapshotsToKeep = 500,
                CpuUsageThreshold = 60.0,
                MemoryUsageThreshold = 70.0,
                ThreadCountThreshold = 200,
                EnablePerformanceCounters = true,
                EnableDetailedMetrics = true,
                EnableTrendAnalysis = true
            };
        }
    }
}