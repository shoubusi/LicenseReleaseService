using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for performance tuning
    /// </summary>
    public class TimerPerformanceTunerOptions
    {
        private int _tuningIntervalMs = 30000;
        private int _maxTuningHistorySize = 100;
        private int _maxRecommendationsPerCycle = 3;
        private double _minimumConfidenceThreshold = 70.0;
        private bool _enableAutoTuning = true;
        private bool _enableTuningHistory = true;
        private bool _enableRecommendationLogging = true;

        /// <summary>
        /// Gets or sets the interval for tuning cycles in milliseconds
        /// </summary>
        public int TuningIntervalMs
        {
            get => _tuningIntervalMs;
            set
            {
                if (value < 1000)
                    throw new ArgumentException("Tuning interval must be at least 1000ms", nameof(value));
                _tuningIntervalMs = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of tuning actions to keep in history
        /// </summary>
        public int MaxTuningHistorySize
        {
            get => _maxTuningHistorySize;
            set
            {
                if (value < 10)
                    throw new ArgumentException("Max tuning history size must be at least 10", nameof(value));
                _maxTuningHistorySize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of recommendations to apply per tuning cycle
        /// </summary>
        public int MaxRecommendationsPerCycle
        {
            get => _maxRecommendationsPerCycle;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max recommendations per cycle must be at least 1", nameof(value));
                _maxRecommendationsPerCycle = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum confidence threshold for applying recommendations
        /// </summary>
        public double MinimumConfidenceThreshold
        {
            get => _minimumConfidenceThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Minimum confidence threshold must be between 0 and 100", nameof(value));
                _minimumConfidenceThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable automatic tuning
        /// </summary>
        public bool EnableAutoTuning
        {
            get => _enableAutoTuning;
            set => _enableAutoTuning = value;
        }

        /// <summary>
        /// Gets or sets whether to enable tuning history
        /// </summary>
        public bool EnableTuningHistory
        {
            get => _enableTuningHistory;
            set => _enableTuningHistory = value;
        }

        /// <summary>
        /// Gets or sets whether to enable recommendation logging
        /// </summary>
        public bool EnableRecommendationLogging
        {
            get => _enableRecommendationLogging;
            set => _enableRecommendationLogging = value;
        }

        /// <summary>
        /// Gets or sets the CPU usage threshold for tuning
        /// </summary>
        public double CpuUsageThreshold { get; set; }

        /// <summary>
        /// Gets or sets the memory usage threshold for tuning
        /// </summary>
        public double MemoryUsageThreshold { get; set; }

        /// <summary>
        /// Gets or sets the maximum thread count for tuning
        /// </summary>
        public int MaxThreadCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceTunerOptions class
        /// </summary>
        public TimerPerformanceTunerOptions()
        {
            CpuUsageThreshold = 80.0;
            MemoryUsageThreshold = 85.0;
            MaxThreadCount = 32;
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public TimerPerformanceTunerOptions Clone()
        {
            return new TimerPerformanceTunerOptions
            {
                TuningIntervalMs = TuningIntervalMs,
                MaxTuningHistorySize = MaxTuningHistorySize,
                MaxRecommendationsPerCycle = MaxRecommendationsPerCycle,
                MinimumConfidenceThreshold = MinimumConfidenceThreshold,
                EnableAutoTuning = EnableAutoTuning,
                EnableTuningHistory = EnableTuningHistory,
                EnableRecommendationLogging = EnableRecommendationLogging,
                CpuUsageThreshold = CpuUsageThreshold,
                MemoryUsageThreshold = MemoryUsageThreshold,
                MaxThreadCount = MaxThreadCount
            };
        }

        /// <summary>
        /// Gets default options for server environments
        /// </summary>
        /// <returns>Default server options</returns>
        public static TimerPerformanceTunerOptions ServerDefaults()
        {
            return new TimerPerformanceTunerOptions
            {
                TuningIntervalMs = 60000,
                MaxTuningHistorySize = 200,
                MaxRecommendationsPerCycle = 2,
                MinimumConfidenceThreshold = 80.0,
                EnableAutoTuning = true,
                EnableTuningHistory = true,
                EnableRecommendationLogging = true
            };
        }

        /// <summary>
        /// Gets options for development environments
        /// </summary>
        /// <returns>Development options</returns>
        public static TimerPerformanceTunerOptions DevelopmentDefaults()
        {
            return new TimerPerformanceTunerOptions
            {
                TuningIntervalMs = 15000,
                MaxTuningHistorySize = 50,
                MaxRecommendationsPerCycle = 5,
                MinimumConfidenceThreshold = 60.0,
                EnableAutoTuning = true,
                EnableTuningHistory = true,
                EnableRecommendationLogging = true
            };
        }

        /// <summary>
        /// Gets options for high-performance environments
        /// </summary>
        /// <returns>High-performance options</returns>
        public static TimerPerformanceTunerOptions HighPerformanceDefaults()
        {
            return new TimerPerformanceTunerOptions
            {
                TuningIntervalMs = 10000,
                MaxTuningHistorySize = 500,
                MaxRecommendationsPerCycle = 1,
                MinimumConfidenceThreshold = 90.0,
                EnableAutoTuning = true,
                EnableTuningHistory = true,
                EnableRecommendationLogging = false
            };
        }
    }
}