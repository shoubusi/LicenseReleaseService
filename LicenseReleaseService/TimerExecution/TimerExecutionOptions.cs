using System;
using System.ComponentModel;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for timer execution behavior
    /// </summary>
    public class TimerExecutionOptions
    {
        private TimeSpan _defaultInterval = TimeSpan.FromMinutes(1);
        private TimeSpan _executionTimeout = TimeSpan.FromMinutes(5);
        private TimeSpan _circuitBreakerCooldown = TimeSpan.FromMinutes(5);
        private int _maxConsecutiveErrors = 5;
        private int _maxConcurrentExecutions = 1;
        private bool _enableAutoRestart = true;
        private bool _enableExecutionTimeout = true;
        private bool _enableMetrics = true;
        private bool _enableDetailedLogging = true;
        private bool _enableCircuitBreaker = true;
        private int _maxExecutionHistory = 100;

        /// <summary>
        /// Gets or sets the default timer interval
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:01:00")]
        public TimeSpan DefaultInterval
        {
            get => _defaultInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Default interval must be greater than zero", nameof(value));
                }
                _defaultInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of consecutive errors before triggering circuit breaker
        /// </summary>
        [DefaultValue(5)]
        public int MaxConsecutiveErrors
        {
            get => _maxConsecutiveErrors;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Max consecutive errors must be at least 1", nameof(value));
                }
                _maxConsecutiveErrors = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent executions allowed
        /// </summary>
        [DefaultValue(1)]
        public int MaxConcurrentExecutions
        {
            get => _maxConcurrentExecutions;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Max concurrent executions must be at least 1", nameof(value));
                }
                _maxConcurrentExecutions = value;
            }
        }

        /// <summary>
        /// Gets or sets the cooldown period for circuit breaker
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan CircuitBreakerCooldown
        {
            get => _circuitBreakerCooldown;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Circuit breaker cooldown must be greater than zero", nameof(value));
                }
                _circuitBreakerCooldown = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable automatic restart after failures
        /// </summary>
        [DefaultValue(true)]
        public bool EnableAutoRestart
        {
            get => _enableAutoRestart;
            set => _enableAutoRestart = value;
        }

        /// <summary>
        /// Gets or sets whether to enable execution timeout
        /// </summary>
        [DefaultValue(true)]
        public bool EnableExecutionTimeout
        {
            get => _enableExecutionTimeout;
            set => _enableExecutionTimeout = value;
        }

        /// <summary>
        /// Gets or sets the execution timeout for individual operations
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan ExecutionTimeout
        {
            get => _executionTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Execution timeout must be greater than zero", nameof(value));
                }
                _executionTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable performance metrics collection
        /// </summary>
        [DefaultValue(true)]
        public bool EnableMetrics
        {
            get => _enableMetrics;
            set => _enableMetrics = value;
        }

        /// <summary>
        /// Gets or sets whether to enable detailed logging
        /// </summary>
        [DefaultValue(true)]
        public bool EnableDetailedLogging
        {
            get => _enableDetailedLogging;
            set => _enableDetailedLogging = value;
        }

        /// <summary>
        /// Gets or sets whether to enable circuit breaker functionality
        /// </summary>
        [DefaultValue(true)]
        public bool EnableCircuitBreaker
        {
            get => _enableCircuitBreaker;
            set => _enableCircuitBreaker = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of execution history items to keep
        /// </summary>
        [DefaultValue(100)]
        public int MaxExecutionHistory
        {
            get => _maxExecutionHistory;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Max execution history cannot be negative", nameof(value));
                }
                _maxExecutionHistory = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum interval allowed
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:01")]
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum interval allowed
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "1.00:00:00")]
        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Gets or sets the synchronization timeout for thread-safe operations
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:30")]
        public TimeSpan SyncTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to stop timer on unhandled exceptions
        /// </summary>
        [DefaultValue(false)]
        public bool StopOnUnhandledException { get; set; } = false;

        /// <summary>
        /// Gets or sets the grace period for timer disposal
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:05")]
        public TimeSpan DisposalGracePeriod { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets whether to enable execution overlap prevention
        /// </summary>
        [DefaultValue(true)]
        public bool PreventExecutionOverlap { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the TimerExecutionOptions class
        /// </summary>
        public TimerExecutionOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionOptions class with default interval
        /// </summary>
        /// <param name="defaultInterval">The default timer interval</param>
        public TimerExecutionOptions(TimeSpan defaultInterval) : this()
        {
            DefaultInterval = defaultInterval;
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionOptions class with specified parameters
        /// </summary>
        /// <param name="defaultInterval">The default timer interval</param>
        /// <param name="maxConsecutiveErrors">The maximum consecutive errors</param>
        /// <param name="circuitBreakerCooldown">The circuit breaker cooldown period</param>
        public TimerExecutionOptions(TimeSpan defaultInterval, int maxConsecutiveErrors, TimeSpan circuitBreakerCooldown)
            : this(defaultInterval)
        {
            MaxConsecutiveErrors = maxConsecutiveErrors;
            CircuitBreakerCooldown = circuitBreakerCooldown;
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public TimerExecutionOptions Clone()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = DefaultInterval,
                MaxConsecutiveErrors = MaxConsecutiveErrors,
                MaxConcurrentExecutions = MaxConcurrentExecutions,
                CircuitBreakerCooldown = CircuitBreakerCooldown,
                EnableAutoRestart = EnableAutoRestart,
                EnableExecutionTimeout = EnableExecutionTimeout,
                ExecutionTimeout = ExecutionTimeout,
                EnableMetrics = EnableMetrics,
                EnableDetailedLogging = EnableDetailedLogging,
                EnableCircuitBreaker = EnableCircuitBreaker,
                MaxExecutionHistory = MaxExecutionHistory,
                MinInterval = MinInterval,
                MaxInterval = MaxInterval,
                SyncTimeout = SyncTimeout,
                StopOnUnhandledException = StopOnUnhandledException,
                DisposalGracePeriod = DisposalGracePeriod,
                PreventExecutionOverlap = PreventExecutionOverlap
            };
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (DefaultInterval <= TimeSpan.Zero)
            {
                errors.Add("Default interval must be greater than zero");
            }

            if (ExecutionTimeout <= TimeSpan.Zero)
            {
                errors.Add("Execution timeout must be greater than zero");
            }

            if (CircuitBreakerCooldown <= TimeSpan.Zero)
            {
                errors.Add("Circuit breaker cooldown must be greater than zero");
            }

            if (MaxConsecutiveErrors < 1)
            {
                errors.Add("Max consecutive errors must be at least 1");
            }

            if (MaxConcurrentExecutions < 1)
            {
                errors.Add("Max concurrent executions must be at least 1");
            }

            if (MaxExecutionHistory < 0)
            {
                errors.Add("Max execution history cannot be negative");
            }

            if (MinInterval <= TimeSpan.Zero)
            {
                errors.Add("Min interval must be greater than zero");
            }

            if (MaxInterval <= TimeSpan.Zero)
            {
                errors.Add("Max interval must be greater than zero");
            }

            if (MinInterval > MaxInterval)
            {
                errors.Add("Min interval cannot be greater than max interval");
            }

            if (SyncTimeout <= TimeSpan.Zero)
            {
                errors.Add("Sync timeout must be greater than zero");
            }

            if (DisposalGracePeriod <= TimeSpan.Zero)
            {
                errors.Add("Disposal grace period must be greater than zero");
            }

            if (DefaultInterval < MinInterval)
            {
                errors.Add("Default interval cannot be less than min interval");
            }

            if (DefaultInterval > MaxInterval)
            {
                errors.Add("Default interval cannot be greater than max interval");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the timer execution options
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Execution Options:");
            builder.AppendLine($"  Default Interval: {DefaultInterval.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Max Consecutive Errors: {MaxConsecutiveErrors}");
            builder.AppendLine($"  Max Concurrent Executions: {MaxConcurrentExecutions}");
            builder.AppendLine($"  Circuit Breaker Cooldown: {CircuitBreakerCooldown.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Enable Auto Restart: {EnableAutoRestart}");
            builder.AppendLine($"  Enable Execution Timeout: {EnableExecutionTimeout}");
            builder.AppendLine($"  Execution Timeout: {ExecutionTimeout.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Enable Metrics: {EnableMetrics}");
            builder.AppendLine($"  Enable Detailed Logging: {EnableDetailedLogging}");
            builder.AppendLine($"  Enable Circuit Breaker: {EnableCircuitBreaker}");
            builder.AppendLine($"  Max Execution History: {MaxExecutionHistory}");
            builder.AppendLine($"  Min Interval: {MinInterval.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Max Interval: {MaxInterval.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Sync Timeout: {SyncTimeout.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Stop On Unhandled Exception: {StopOnUnhandledException}");
            builder.AppendLine($"  Disposal Grace Period: {DisposalGracePeriod.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Prevent Execution Overlap: {PreventExecutionOverlap}");

            return builder.ToString();
        }

        /// <summary>
        /// Gets default options for license monitoring operations
        /// </summary>
        /// <returns>Default options for license monitoring</returns>
        public static TimerExecutionOptions DefaultLicenseMonitoringOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMinutes(5),
                MaxConsecutiveErrors = 3,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(10),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(2),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 50,
                MinInterval = TimeSpan.FromSeconds(30),
                MaxInterval = TimeSpan.FromHours(1),
                PreventExecutionOverlap = true
            };
        }

        /// <summary>
        /// Gets options for high-frequency monitoring operations
        /// </summary>
        /// <returns>Options for high-frequency monitoring</returns>
        public static TimerExecutionOptions HighFrequencyOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(30),
                MaxConsecutiveErrors = 5,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(2),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromSeconds(15),
                EnableMetrics = true,
                EnableDetailedLogging = false,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 200,
                MinInterval = TimeSpan.FromSeconds(5),
                MaxInterval = TimeSpan.FromMinutes(10),
                PreventExecutionOverlap = true
            };
        }

        /// <summary>
        /// Gets options for low-frequency background operations
        /// </summary>
        /// <returns>Options for low-frequency operations</returns>
        public static TimerExecutionOptions LowFrequencyOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromHours(1),
                MaxConsecutiveErrors = 2,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromHours(1),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(10),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 25,
                MinInterval = TimeSpan.FromMinutes(5),
                MaxInterval = TimeSpan.FromDays(7),
                PreventExecutionOverlap = false
            };
        }

        /// <summary>
        /// Gets options for testing scenarios
        /// </summary>
        /// <returns>Options for testing</returns>
        public static TimerExecutionOptions TestOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(1),
                MaxConsecutiveErrors = 2,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromSeconds(5),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromSeconds(5),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 10,
                MinInterval = TimeSpan.FromMilliseconds(100),
                MaxInterval = TimeSpan.FromSeconds(10),
                StopOnUnhandledException = true,
                PreventExecutionOverlap = true
            };
        }
    }
}