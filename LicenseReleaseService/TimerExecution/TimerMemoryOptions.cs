using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for memory management
    /// </summary>
    public class TimerMemoryOptions
    {
        private int _monitorIntervalMs = 5000;
        private int _systemMemoryMB;
        private double _lowPressureThreshold = 50.0;
        private double _mediumPressureThreshold = 70.0;
        private double _highPressureThreshold = 85.0;
        private double _criticalPressureThreshold = 95.0;
        private int _memoryLeakThresholdMB = 1024;
        private int _maxGenerationThreshold = 10;
        private int _largeObjectHeapThreshold = 10;
        private bool _enableMemoryLeakDetection = true;
        private bool _enableMemoryPools = true;
        private bool _enableAggressiveOptimization = false;
        private int _maxPoolSize = 1000;
        private int _poolCleanupIntervalMs = 30000;

        /// <summary>
        /// Gets or sets the interval for memory monitoring in milliseconds
        /// </summary>
        public int MonitorIntervalMs
        {
            get => _monitorIntervalMs;
            set
            {
                if (value < 100)
                    throw new ArgumentException("Monitor interval must be at least 100ms", nameof(value));
                _monitorIntervalMs = value;
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
        /// Gets or sets the memory usage threshold for low pressure (percentage)
        /// </summary>
        public double LowPressureThreshold
        {
            get => _lowPressureThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Low pressure threshold must be between 0 and 100", nameof(value));
                _lowPressureThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the memory usage threshold for medium pressure (percentage)
        /// </summary>
        public double MediumPressureThreshold
        {
            get => _mediumPressureThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Medium pressure threshold must be between 0 and 100", nameof(value));
                if (value <= _lowPressureThreshold)
                    throw new ArgumentException("Medium pressure threshold must be greater than low pressure threshold", nameof(value));
                _mediumPressureThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the memory usage threshold for high pressure (percentage)
        /// </summary>
        public double HighPressureThreshold
        {
            get => _highPressureThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("High pressure threshold must be between 0 and 100", nameof(value));
                if (value <= _mediumPressureThreshold)
                    throw new ArgumentException("High pressure threshold must be greater than medium pressure threshold", nameof(value));
                _highPressureThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the memory usage threshold for critical pressure (percentage)
        /// </summary>
        public double CriticalPressureThreshold
        {
            get => _criticalPressureThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Critical pressure threshold must be between 0 and 100", nameof(value));
                if (value <= _highPressureThreshold)
                    throw new ArgumentException("Critical pressure threshold must be greater than high pressure threshold", nameof(value));
                _criticalPressureThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the memory leak detection threshold in MB
        /// </summary>
        public int MemoryLeakThresholdMB
        {
            get => _memoryLeakThresholdMB;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Memory leak threshold cannot be negative", nameof(value));
                _memoryLeakThresholdMB = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum generation threshold for GC notifications
        /// </summary>
        public int MaxGenerationThreshold
        {
            get => _maxGenerationThreshold;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max generation threshold must be at least 1", nameof(value));
                _maxGenerationThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the large object heap threshold for GC notifications
        /// </summary>
        public int LargeObjectHeapThreshold
        {
            get => _largeObjectHeapThreshold;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Large object heap threshold must be at least 1", nameof(value));
                _largeObjectHeapThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable memory leak detection
        /// </summary>
        public bool EnableMemoryLeakDetection
        {
            get => _enableMemoryLeakDetection;
            set => _enableMemoryLeakDetection = value;
        }

        /// <summary>
        /// Gets or sets whether to enable memory pools
        /// </summary>
        public bool EnableMemoryPools
        {
            get => _enableMemoryPools;
            set => _enableMemoryPools = value;
        }

        /// <summary>
        /// Gets or sets whether to enable aggressive memory optimization
        /// </summary>
        public bool EnableAggressiveOptimization
        {
            get => _enableAggressiveOptimization;
            set => _enableAggressiveOptimization = value;
        }

        /// <summary>
        /// Gets or sets the maximum size for memory pools
        /// </summary>
        public int MaxPoolSize
        {
            get => _maxPoolSize;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max pool size must be at least 1", nameof(value));
                _maxPoolSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the interval for memory pool cleanup in milliseconds
        /// </summary>
        public int PoolCleanupIntervalMs
        {
            get => _poolCleanupIntervalMs;
            set
            {
                if (value < 1000)
                    throw new ArgumentException("Pool cleanup interval must be at least 1000ms", nameof(value));
                _poolCleanupIntervalMs = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryOptions class
        /// </summary>
        public TimerMemoryOptions()
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
        public TimerMemoryOptions Clone()
        {
            return new TimerMemoryOptions
            {
                MonitorIntervalMs = MonitorIntervalMs,
                SystemMemoryMB = SystemMemoryMB,
                LowPressureThreshold = LowPressureThreshold,
                MediumPressureThreshold = MediumPressureThreshold,
                HighPressureThreshold = HighPressureThreshold,
                CriticalPressureThreshold = CriticalPressureThreshold,
                MemoryLeakThresholdMB = MemoryLeakThresholdMB,
                MaxGenerationThreshold = MaxGenerationThreshold,
                LargeObjectHeapThreshold = LargeObjectHeapThreshold,
                EnableMemoryLeakDetection = EnableMemoryLeakDetection,
                EnableMemoryPools = EnableMemoryPools,
                EnableAggressiveOptimization = EnableAggressiveOptimization,
                MaxPoolSize = MaxPoolSize,
                PoolCleanupIntervalMs = PoolCleanupIntervalMs
            };
        }

        /// <summary>
        /// Gets default options for server environments
        /// </summary>
        /// <returns>Default server options</returns>
        public static TimerMemoryOptions ServerDefaults()
        {
            return new TimerMemoryOptions
            {
                MonitorIntervalMs = 30000,
                LowPressureThreshold = 60.0,
                MediumPressureThreshold = 75.0,
                HighPressureThreshold = 85.0,
                CriticalPressureThreshold = 90.0,
                MemoryLeakThresholdMB = 2048,
                EnableMemoryLeakDetection = true,
                EnableMemoryPools = true,
                EnableAggressiveOptimization = false,
                MaxPoolSize = 2000,
                PoolCleanupIntervalMs = 60000
            };
        }

        /// <summary>
        /// Gets options for development environments
        /// </summary>
        /// <returns>Development options</returns>
        public static TimerMemoryOptions DevelopmentDefaults()
        {
            return new TimerMemoryOptions
            {
                MonitorIntervalMs = 10000,
                LowPressureThreshold = 70.0,
                MediumPressureThreshold = 80.0,
                HighPressureThreshold = 90.0,
                CriticalPressureThreshold = 95.0,
                MemoryLeakThresholdMB = 512,
                EnableMemoryLeakDetection = true,
                EnableMemoryPools = true,
                EnableAggressiveOptimization = false,
                MaxPoolSize = 500,
                PoolCleanupIntervalMs = 30000
            };
        }

        /// <summary>
        /// Gets options for high-performance environments
        /// </summary>
        /// <returns>High-performance options</returns>
        public static TimerMemoryOptions HighPerformanceDefaults()
        {
            return new TimerMemoryOptions
            {
                MonitorIntervalMs = 5000,
                LowPressureThreshold = 40.0,
                MediumPressureThreshold = 60.0,
                HighPressureThreshold = 75.0,
                CriticalPressureThreshold = 85.0,
                MemoryLeakThresholdMB = 1024,
                EnableMemoryLeakDetection = true,
                EnableMemoryPools = true,
                EnableAggressiveOptimization = true,
                MaxPoolSize = 5000,
                PoolCleanupIntervalMs = 15000
            };
        }
    }

    /// <summary>
    /// Memory pressure levels
    /// </summary>
    public enum TimerMemoryPressureLevel
    {
        /// <summary>
        /// No memory pressure
        /// </summary>
        None,

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