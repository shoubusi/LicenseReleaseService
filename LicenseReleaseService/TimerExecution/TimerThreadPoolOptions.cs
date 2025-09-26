using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for thread pool management
    /// </summary>
    public class TimerThreadPoolOptions
    {
        private int _monitorIntervalMs = 10000;
        private int _minThreadPoolSize = 2;
        private int _maxThreadPoolSize = 100;
        private TimerThreadPoolWorkload _expectedWorkload = TimerThreadPoolWorkload.Medium;
        private int _maxQueueSize = 1000;
        private int _workItemTimeoutMs = 30000;
        private bool _enablePriorityScheduling = true;
        private bool _enableLoadBalancing = true;
        private bool _enableAdaptiveSizing = true;
        private double _highCpuThreshold = 85.0;
        private double _highMemoryThreshold = 80.0;
        private double _highQueueThreshold = 75.0;

        /// <summary>
        /// Gets or sets the interval for thread pool monitoring in milliseconds
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
        /// Gets or sets the minimum thread pool size
        /// </summary>
        public int MinThreadPoolSize
        {
            get => _minThreadPoolSize;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Min thread pool size must be at least 1", nameof(value));
                _minThreadPoolSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum thread pool size
        /// </summary>
        public int MaxThreadPoolSize
        {
            get => _maxThreadPoolSize;
            set
            {
                if (value < MinThreadPoolSize)
                    throw new ArgumentException("Max thread pool size must be greater than min size", nameof(value));
                _maxThreadPoolSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the expected workload type
        /// </summary>
        public TimerThreadPoolWorkload ExpectedWorkload
        {
            get => _expectedWorkload;
            set => _expectedWorkload = value;
        }

        /// <summary>
        /// Gets or sets the maximum queue size for work items
        /// </summary>
        public int MaxQueueSize
        {
            get => _maxQueueSize;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max queue size must be at least 1", nameof(value));
                _maxQueueSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout for work items in milliseconds
        /// </summary>
        public int WorkItemTimeoutMs
        {
            get => _workItemTimeoutMs;
            set
            {
                if (value < 1000)
                    throw new ArgumentException("Work item timeout must be at least 1000ms", nameof(value));
                _workItemTimeoutMs = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable priority scheduling
        /// </summary>
        public bool EnablePriorityScheduling
        {
            get => _enablePriorityScheduling;
            set => _enablePriorityScheduling = value;
        }

        /// <summary>
        /// Gets or sets whether to enable load balancing
        /// </summary>
        public bool EnableLoadBalancing
        {
            get => _enableLoadBalancing;
            set => _enableLoadBalancing = value;
        }

        /// <summary>
        /// Gets or sets whether to enable adaptive sizing
        /// </summary>
        public bool EnableAdaptiveSizing
        {
            get => _enableAdaptiveSizing;
            set => _enableAdaptiveSizing = value;
        }

        /// <summary>
        /// Gets or sets the high CPU usage threshold percentage
        /// </summary>
        public double HighCpuThreshold
        {
            get => _highCpuThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("High CPU threshold must be between 0 and 100", nameof(value));
                _highCpuThreshold = value;
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
        /// Gets or sets the high queue threshold percentage
        /// </summary>
        public double HighQueueThreshold
        {
            get => _highQueueThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("High queue threshold must be between 0 and 100", nameof(value));
                _highQueueThreshold = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the TimerThreadPoolOptions class
        /// </summary>
        public TimerThreadPoolOptions()
        {
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public TimerThreadPoolOptions Clone()
        {
            return new TimerThreadPoolOptions
            {
                MonitorIntervalMs = MonitorIntervalMs,
                MinThreadPoolSize = MinThreadPoolSize,
                MaxThreadPoolSize = MaxThreadPoolSize,
                ExpectedWorkload = ExpectedWorkload,
                MaxQueueSize = MaxQueueSize,
                WorkItemTimeoutMs = WorkItemTimeoutMs,
                EnablePriorityScheduling = EnablePriorityScheduling,
                EnableLoadBalancing = EnableLoadBalancing,
                EnableAdaptiveSizing = EnableAdaptiveSizing,
                HighCpuThreshold = HighCpuThreshold,
                HighMemoryThreshold = HighMemoryThreshold,
                HighQueueThreshold = HighQueueThreshold
            };
        }

        /// <summary>
        /// Gets default options for server environments
        /// </summary>
        /// <returns>Default server options</returns>
        public static TimerThreadPoolOptions ServerDefaults()
        {
            return new TimerThreadPoolOptions
            {
                MonitorIntervalMs = 15000,
                MinThreadPoolSize = 4,
                MaxThreadPoolSize = 200,
                ExpectedWorkload = TimerThreadPoolWorkload.Heavy,
                MaxQueueSize = 2000,
                WorkItemTimeoutMs = 60000,
                EnablePriorityScheduling = true,
                EnableLoadBalancing = true,
                EnableAdaptiveSizing = true,
                HighCpuThreshold = 80.0,
                HighMemoryThreshold = 75.0,
                HighQueueThreshold = 70.0
            };
        }

        /// <summary>
        /// Gets options for development environments
        /// </summary>
        /// <returns>Development options</returns>
        public static TimerThreadPoolOptions DevelopmentDefaults()
        {
            return new TimerThreadPoolOptions
            {
                MonitorIntervalMs = 5000,
                MinThreadPoolSize = 2,
                MaxThreadPoolSize = 50,
                ExpectedWorkload = TimerThreadPoolWorkload.Light,
                MaxQueueSize = 100,
                WorkItemTimeoutMs = 30000,
                EnablePriorityScheduling = true,
                EnableLoadBalancing = false,
                EnableAdaptiveSizing = true,
                HighCpuThreshold = 90.0,
                HighMemoryThreshold = 85.0,
                HighQueueThreshold = 80.0
            };
        }

        /// <summary>
        /// Gets options for high-performance environments
        /// </summary>
        /// <returns>High-performance options</returns>
        public static TimerThreadPoolOptions HighPerformanceDefaults()
        {
            return new TimerThreadPoolOptions
            {
                MonitorIntervalMs = 3000,
                MinThreadPoolSize = 8,
                MaxThreadPoolSize = 500,
                ExpectedWorkload = TimerThreadPoolWorkload.Heavy,
                MaxQueueSize = 5000,
                WorkItemTimeoutMs = 120000,
                EnablePriorityScheduling = true,
                EnableLoadBalancing = true,
                EnableAdaptiveSizing = true,
                HighCpuThreshold = 70.0,
                HighMemoryThreshold = 65.0,
                HighQueueThreshold = 60.0
            };
        }
    }

    /// <summary>
    /// Expected workload types for thread pool sizing
    /// </summary>
    public enum TimerThreadPoolWorkload
    {
        /// <summary>
        /// Light workload with few concurrent operations
        /// </summary>
        Light,

        /// <summary>
        /// Medium workload with moderate concurrency
        /// </summary>
        Medium,

        /// <summary>
        /// Heavy workload with high concurrency
        /// </summary>
        Heavy,

        /// <summary>
        /// Dynamic workload that varies over time
        /// </summary>
        Dynamic
    }

    /// <summary>
    /// Priority levels for work items
    /// </summary>
    public enum TimerWorkItemPriority
    {
        /// <summary>
        /// Low priority work items
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority work items
        /// </summary>
        Normal,

        /// <summary>
        /// High priority work items
        /// </summary>
        High
    }
}