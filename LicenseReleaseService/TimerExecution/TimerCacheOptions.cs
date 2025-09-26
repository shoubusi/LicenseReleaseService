using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for cache management
    /// </summary>
    public class TimerCacheOptions
    {
        private int _monitorIntervalMs = 30000;
        private int _cleanupIntervalMs = 60000;
        private int _systemMemoryMB;
        private double _highMemoryThreshold = 80.0;
        private double _criticalMemoryThreshold = 90.0;
        private bool _enableAutoOptimization = true;
        private bool _enableMemoryPressureMonitoring = true;
        private int _maxCacheSize = 10000;
        private TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the interval for cache monitoring in milliseconds
        /// </summary>
        public int MonitorIntervalMs
        {
            get => _monitorIntervalMs;
            set
            {
                if (value < 1000)
                    throw new ArgumentException("Monitor interval must be at least 1000ms", nameof(value));
                _monitorIntervalMs = value;
            }
        }

        /// <summary>
        /// Gets or sets the interval for cache cleanup in milliseconds
        /// </summary>
        public int CleanupIntervalMs
        {
            get => _cleanupIntervalMs;
            set
            {
                if (value < 1000)
                    throw new ArgumentException("Cleanup interval must be at least 1000ms", nameof(value));
                _cleanupIntervalMs = value;
            }
        }

        /// <summary>
        /// Gets or sets the total system memory in MB (auto-detected if 0)
        /// </summary>
        public int SystemMemoryMB
        {
            get => _systemMemoryMB;
            set
            {
                if (value < 0)
                    throw new ArgumentException("System memory cannot be negative", nameof(value));
                _systemMemoryMB = value;
            }
        }

        /// <summary>
        /// Gets or sets the high memory usage threshold percentage
        /// </summary>
        public double HighMemoryThreshold
        {
            get => _highMemoryThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("High memory threshold must be between 0 and 100", nameof(value));
                _highMemoryThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the critical memory usage threshold percentage
        /// </summary>
        public double CriticalMemoryThreshold
        {
            get => _criticalMemoryThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Critical memory threshold must be between 0 and 100", nameof(value));
                if (value <= _highMemoryThreshold)
                    throw new ArgumentException("Critical threshold must be greater than high threshold", nameof(value));
                _criticalMemoryThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable automatic cache optimization
        /// </summary>
        public bool EnableAutoOptimization
        {
            get => _enableAutoOptimization;
            set => _enableAutoOptimization = value;
        }

        /// <summary>
        /// Gets or sets whether to enable memory pressure monitoring
        /// </summary>
        public bool EnableMemoryPressureMonitoring
        {
            get => _enableMemoryPressureMonitoring;
            set => _enableMemoryPressureMonitoring = value;
        }

        /// <summary>
        /// Gets or sets the maximum cache size
        /// </summary>
        public int MaxCacheSize
        {
            get => _maxCacheSize;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max cache size must be at least 1", nameof(value));
                _maxCacheSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the default expiration time for cache items
        /// </summary>
        public TimeSpan DefaultExpiration
        {
            get => _defaultExpiration;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Default expiration must be positive", nameof(value));
                _defaultExpiration = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the TimerCacheOptions class
        /// </summary>
        public TimerCacheOptions()
        {
            // Auto-detect system memory if not specified
            if (_systemMemoryMB == 0)
            {
                try
                {
                    using (var process = new System.Diagnostics.Process())
                    {
                        process.StartInfo.FileName = "wmic";
                        process.StartInfo.Arguments = "ComputerSystem get TotalPhysicalMemory";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        var lines = output.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("TotalPhysicalMemory"))
                            {
                                var value = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                                if (long.TryParse(value, out var bytes))
                                {
                                    _systemMemoryMB = (int)(bytes / (1024 * 1024));
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    _systemMemoryMB = 8192; // Default to 8GB
                }
            }
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public TimerCacheOptions Clone()
        {
            return new TimerCacheOptions
            {
                MonitorIntervalMs = MonitorIntervalMs,
                CleanupIntervalMs = CleanupIntervalMs,
                SystemMemoryMB = SystemMemoryMB,
                HighMemoryThreshold = HighMemoryThreshold,
                CriticalMemoryThreshold = CriticalMemoryThreshold,
                EnableAutoOptimization = EnableAutoOptimization,
                EnableMemoryPressureMonitoring = EnableMemoryPressureMonitoring,
                MaxCacheSize = MaxCacheSize,
                DefaultExpiration = DefaultExpiration
            };
        }

        /// <summary>
        /// Gets default options for server environments
        /// </summary>
        /// <returns>Default server options</returns>
        public static TimerCacheOptions ServerDefaults()
        {
            return new TimerCacheOptions
            {
                MonitorIntervalMs = 60000,
                CleanupIntervalMs = 120000,
                HighMemoryThreshold = 75.0,
                CriticalMemoryThreshold = 85.0,
                EnableAutoOptimization = true,
                EnableMemoryPressureMonitoring = true,
                MaxCacheSize = 50000,
                DefaultExpiration = TimeSpan.FromMinutes(10)
            };
        }

        /// <summary>
        /// Gets options for development environments
        /// </summary>
        /// <returns>Development options</returns>
        public static TimerCacheOptions DevelopmentDefaults()
        {
            return new TimerCacheOptions
            {
                MonitorIntervalMs = 15000,
                CleanupIntervalMs = 30000,
                HighMemoryThreshold = 85.0,
                CriticalMemoryThreshold = 95.0,
                EnableAutoOptimization = true,
                EnableMemoryPressureMonitoring = true,
                MaxCacheSize = 1000,
                DefaultExpiration = TimeSpan.FromMinutes(5)
            };
        }

        /// <summary>
        /// Gets options for high-performance environments
        /// </summary>
        /// <returns>High-performance options</returns>
        public static TimerCacheOptions HighPerformanceDefaults()
        {
            return new TimerCacheOptions
            {
                MonitorIntervalMs = 10000,
                CleanupIntervalMs = 20000,
                HighMemoryThreshold = 70.0,
                CriticalMemoryThreshold = 80.0,
                EnableAutoOptimization = true,
                EnableMemoryPressureMonitoring = true,
                MaxCacheSize = 100000,
                DefaultExpiration = TimeSpan.FromMinutes(2)
            };
        }
    }

    /// <summary>
    /// Expiration policy for cache items
    /// </summary>
    public class TimerCacheExpirationPolicy
    {
        private TimeSpan _defaultExpiration;

        /// <summary>
        /// Gets or sets the default expiration time for cache items
        /// </summary>
        public TimeSpan DefaultExpiration
        {
            get => _defaultExpiration;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Default expiration must be positive", nameof(value));
                _defaultExpiration = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable sliding expiration
        /// </summary>
        public bool EnableSlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the sliding expiration time
        /// </summary>
        public TimeSpan SlidingExpiration { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerCacheExpirationPolicy class
        /// </summary>
        public TimerCacheExpirationPolicy()
        {
            _defaultExpiration = TimeSpan.FromMinutes(5);
            EnableSlidingExpiration = false;
            SlidingExpiration = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Initializes a new instance of the TimerCacheExpirationPolicy class
        /// </summary>
        /// <param name="defaultExpiration">Default expiration time</param>
        public TimerCacheExpirationPolicy(TimeSpan defaultExpiration)
        {
            _defaultExpiration = defaultExpiration > TimeSpan.Zero ? defaultExpiration : throw new ArgumentException("Default expiration must be positive", nameof(defaultExpiration));
            EnableSlidingExpiration = false;
            SlidingExpiration = defaultExpiration;
        }

        /// <summary>
        /// Creates a copy of the current policy
        /// </summary>
        /// <returns>Copy of the policy</returns>
        public TimerCacheExpirationPolicy Clone()
        {
            return new TimerCacheExpirationPolicy
            {
                DefaultExpiration = DefaultExpiration,
                EnableSlidingExpiration = EnableSlidingExpiration,
                SlidingExpiration = SlidingExpiration
            };
        }
    }

    /// <summary>
    /// Eviction policy for cache items
    /// </summary>
    public class TimerCacheEvictionPolicy
    {
        /// <summary>
        /// Gets or sets the eviction type
        /// </summary>
        public TimerCacheEvictionType Type { get; set; }

        /// <summary>
        /// Gets or sets the eviction threshold (percentage)
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerCacheEvictionPolicy class
        /// </summary>
        public TimerCacheEvictionPolicy()
        {
            Type = TimerCacheEvictionType.LRU;
            Threshold = 90.0;
        }

        /// <summary>
        /// Initializes a new instance of the TimerCacheEvictionPolicy class
        /// </summary>
        /// <param name="type">Eviction type</param>
        public TimerCacheEvictionPolicy(TimerCacheEvictionType type)
        {
            Type = type;
            Threshold = 90.0;
        }

        /// <summary>
        /// Creates a copy of the current policy
        /// </summary>
        /// <returns>Copy of the policy</returns>
        public TimerCacheEvictionPolicy Clone()
        {
            return new TimerCacheEvictionPolicy
            {
                Type = Type,
                Threshold = Threshold
            };
        }
    }

    /// <summary>
    /// Cache eviction types
    /// </summary>
    public enum TimerCacheEvictionType
    {
        /// <summary>
        /// Least Recently Used eviction
        /// </summary>
        LRU,

        /// <summary>
        /// Least Frequently Used eviction
        /// </summary>
        LFU,

        /// <summary>
        /// First In First Out eviction
        /// </summary>
        FIFO,

        /// <summary>
        /// Random eviction
        /// </summary>
        Random
    }

    /// <summary>
    /// Cache memory pressure levels
    /// </summary>
    public enum TimerCacheMemoryPressureLevel
    {
        /// <summary>
        /// Low memory pressure
        /// </summary>
        Low,

        /// <summary>
        /// Medium memory pressure
        /// </summary>
        Medium,

        /// <summary>
        /// High memory pressure
        /// </summary>
        High,

        /// <summary>
        /// Critical memory pressure
        /// </summary>
        Critical
    }
}