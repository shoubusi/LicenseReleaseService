using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Configuration options for license query engine behavior
    /// </summary>
    public class LicenseQueryOptions
    {
        private TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private TimeSpan _queryTimeout = TimeSpan.FromSeconds(30);
        private TimeSpan _parsingTimeout = TimeSpan.FromSeconds(15);
        private TimeSpan _serverResponseTimeout = TimeSpan.FromSeconds(20);
        private TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
        private int _maxRetries = 3;
        private int _maxCacheSize = 1000;
        private int _maxMemoryUsage = 52428800; // 50MB
        private int _maxConcurrentQueries = 10;
        private int _maxOutputSize = 10485760; // 10MB
        private int _statisticsRetentionDays = 30;
        private int _maxStatisticsEntries = 10000;
        private bool _enableCaching = true;
        private bool _enableStatistics = true;
        private bool _enableVerboseOutput = false;
        private bool _enableDetailedParsing = true;
        private bool _enableErrorRecovery = true;
        private bool _enableIncrementalUpdates = true;
        private bool _enableFeatureBatching = true;
        private bool _enableUserActivityTracking = true;
        private bool _enableBorrowingTracking = true;
        private bool _enableHealthMonitoring = true;
        private bool _enablePerformanceMetrics = true;
        private bool _enableAlerting = true;
        private bool _failFastOnInvalidData = false;
        private bool _enableAutoCleanup = true;
        private bool _enableCompactOutput = false;
        private string _defaultOutputFormat = "Standard";
        private string _preferredLanguage = "en-US";
        private int _alertThreshold = 90;
        private int _cleanupInterval = 3600; // 1 hour
        private int _healthCheckInterval = 60; // 1 minute
        private int _performanceMetricsInterval = 30; // 30 seconds

        /// <summary>
        /// Gets or sets the cache expiration time
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan CacheExpiration
        {
            get => _cacheExpiration;
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw new ArgumentException("Cache expiration cannot be negative", nameof(value));
                }
                _cacheExpiration = value;
            }
        }

        /// <summary>
        /// Gets or sets the query timeout for license server operations
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:30")]
        public TimeSpan QueryTimeout
        {
            get => _queryTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Query timeout must be greater than zero", nameof(value));
                }
                _queryTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the parsing timeout for lmstat output processing
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:15")]
        public TimeSpan ParsingTimeout
        {
            get => _parsingTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Parsing timeout must be greater than zero", nameof(value));
                }
                _parsingTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the server response timeout
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:20")]
        public TimeSpan ServerResponseTimeout
        {
            get => _serverResponseTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Server response timeout must be greater than zero", nameof(value));
                }
                _serverResponseTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the delay between retry attempts
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:02")]
        public TimeSpan RetryDelay
        {
            get => _retryDelay;
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw new ArgumentException("Retry delay cannot be negative", nameof(value));
                }
                _retryDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts
        /// </summary>
        [DefaultValue(3)]
        public int MaxRetries
        {
            get => _maxRetries;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Max retries cannot be negative", nameof(value));
                }
                _maxRetries = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum cache size in entries
        /// </summary>
        [DefaultValue(1000)]
        public int MaxCacheSize
        {
            get => _maxCacheSize;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Max cache size must be greater than zero", nameof(value));
                }
                _maxCacheSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum memory usage in bytes
        /// </summary>
        [DefaultValue(52428800)]
        public int MaxMemoryUsage
        {
            get => _maxMemoryUsage;
            set
            {
                if (value < 1048576) // 1MB minimum
                {
                    throw new ArgumentException("Max memory usage must be at least 1MB", nameof(value));
                }
                _maxMemoryUsage = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent queries
        /// </summary>
        [DefaultValue(10)]
        public int MaxConcurrentQueries
        {
            get => _maxConcurrentQueries;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Max concurrent queries must be greater than zero", nameof(value));
                }
                _maxConcurrentQueries = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum output size in bytes (0 = unlimited)
        /// </summary>
        [DefaultValue(10485760)]
        public int MaxOutputSize
        {
            get => _maxOutputSize;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Max output size cannot be negative", nameof(value));
                }
                _maxOutputSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of days to retain statistics
        /// </summary>
        [DefaultValue(30)]
        public int StatisticsRetentionDays
        {
            get => _statisticsRetentionDays;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Statistics retention days must be greater than zero", nameof(value));
                }
                _statisticsRetentionDays = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of statistics entries to retain
        /// </summary>
        [DefaultValue(10000)]
        public int MaxStatisticsEntries
        {
            get => _maxStatisticsEntries;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Max statistics entries must be greater than zero", nameof(value));
                }
                _maxStatisticsEntries = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable caching
        /// </summary>
        [DefaultValue(true)]
        public bool EnableCaching
        {
            get => _enableCaching;
            set => _enableCaching = value;
        }

        /// <summary>
        /// Gets or sets whether to enable statistics collection
        /// </summary>
        [DefaultValue(true)]
        public bool EnableStatistics
        {
            get => _enableStatistics;
            set => _enableStatistics = value;
        }

        /// <summary>
        /// Gets or sets whether to enable verbose output
        /// </summary>
        [DefaultValue(false)]
        public bool EnableVerboseOutput
        {
            get => _enableVerboseOutput;
            set => _enableVerboseOutput = value;
        }

        /// <summary>
        /// Gets or sets whether to enable detailed parsing
        /// </summary>
        [DefaultValue(true)]
        public bool EnableDetailedParsing
        {
            get => _enableDetailedParsing;
            set => _enableDetailedParsing = value;
        }

        /// <summary>
        /// Gets or sets whether to enable error recovery
        /// </summary>
        [DefaultValue(true)]
        public bool EnableErrorRecovery
        {
            get => _enableErrorRecovery;
            set => _enableErrorRecovery = value;
        }

        /// <summary>
        /// Gets or sets whether to enable incremental updates
        /// </summary>
        [DefaultValue(true)]
        public bool EnableIncrementalUpdates
        {
            get => _enableIncrementalUpdates;
            set => _enableIncrementalUpdates = value;
        }

        /// <summary>
        /// Gets or sets whether to enable feature batching
        /// </summary>
        [DefaultValue(true)]
        public bool EnableFeatureBatching
        {
            get => _enableFeatureBatching;
            set => _enableFeatureBatching = value;
        }

        /// <summary>
        /// Gets or sets whether to enable user activity tracking
        /// </summary>
        [DefaultValue(true)]
        public bool EnableUserActivityTracking
        {
            get => _enableUserActivityTracking;
            set => _enableUserActivityTracking = value;
        }

        /// <summary>
        /// Gets or sets whether to enable borrowing tracking
        /// </summary>
        [DefaultValue(true)]
        public bool EnableBorrowingTracking
        {
            get => _enableBorrowingTracking;
            set => _enableBorrowingTracking = value;
        }

        /// <summary>
        /// Gets or sets whether to enable health monitoring
        /// </summary>
        [DefaultValue(true)]
        public bool EnableHealthMonitoring
        {
            get => _enableHealthMonitoring;
            set => _enableHealthMonitoring = value;
        }

        /// <summary>
        /// Gets or sets whether to enable performance metrics
        /// </summary>
        [DefaultValue(true)]
        public bool EnablePerformanceMetrics
        {
            get => _enablePerformanceMetrics;
            set => _enablePerformanceMetrics = value;
        }

        /// <summary>
        /// Gets or sets whether to enable alerting
        /// </summary>
        [DefaultValue(true)]
        public bool EnableAlerting
        {
            get => _enableAlerting;
            set => _enableAlerting = value;
        }

        /// <summary>
        /// Gets or sets whether to fail fast on invalid data
        /// </summary>
        [DefaultValue(false)]
        public bool FailFastOnInvalidData
        {
            get => _failFastOnInvalidData;
            set => _failFastOnInvalidData = value;
        }

        /// <summary>
        /// Gets or sets whether to enable automatic cleanup
        /// </summary>
        [DefaultValue(true)]
        public bool EnableAutoCleanup
        {
            get => _enableAutoCleanup;
            set => _enableAutoCleanup = value;
        }

        /// <summary>
        /// Gets or sets whether to enable compact output format
        /// </summary>
        [DefaultValue(false)]
        public bool EnableCompactOutput
        {
            get => _enableCompactOutput;
            set => _enableCompactOutput = value;
        }

        /// <summary>
        /// Gets or sets the default output format
        /// </summary>
        [DefaultValue("Standard")]
        public string DefaultOutputFormat
        {
            get => _defaultOutputFormat;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Default output format cannot be null or empty", nameof(value));
                }
                _defaultOutputFormat = value;
            }
        }

        /// <summary>
        /// Gets or sets the preferred language for output
        /// </summary>
        [DefaultValue("en-US")]
        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Preferred language cannot be null or empty", nameof(value));
                }
                _preferredLanguage = value;
            }
        }

        /// <summary>
        /// Gets or sets the alert threshold percentage
        /// </summary>
        [DefaultValue(90)]
        public int AlertThreshold
        {
            get => _alertThreshold;
            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentException("Alert threshold must be between 0 and 100", nameof(value));
                }
                _alertThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the cleanup interval in seconds
        /// </summary>
        [DefaultValue(3600)]
        public int CleanupInterval
        {
            get => _cleanupInterval;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Cleanup interval must be greater than zero", nameof(value));
                }
                _cleanupInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the health check interval in seconds
        /// </summary>
        [DefaultValue(60)]
        public int HealthCheckInterval
        {
            get => _healthCheckInterval;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Health check interval must be greater than zero", nameof(value));
                }
                _healthCheckInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the performance metrics interval in seconds
        /// </summary>
        [DefaultValue(30)]
        public int PerformanceMetricsInterval
        {
            get => _performanceMetricsInterval;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Performance metrics interval must be greater than zero", nameof(value));
                }
                _performanceMetricsInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the regex patterns for license parsing
        /// </summary>
        public Dictionary<string, string> RegexPatterns { get; set; } = new Dictionary<string, string>
        {
            { "FeatureLine", @"Users of (\w+):\s+\(Total of (\d+) licenses issued; Total of (\d+) licenses in use\)" },
            { "UserLine", @"(\w+)\s+(\S+)\s+\(([^)]+)\)\s+(.+?)(?:\s+\(v(\d+\.\d+)\))?" },
            { "BorrowLine", @"(\w+)\s+(\S+)\s+\(borrowed:\s+(\d{4}/\d{2}/\d{2})\)" },
            { "ServerLine", @"License server status: (\S+)" },
            { "Timestamp", @"(\d{4}/\d{2}/\d{2}\s+\d{2}:\d{2}:\d{2})" }
        };

        /// <summary>
        /// Gets or sets the feature filtering options
        /// </summary>
        public List<string> IncludedFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the features to exclude from queries
        /// </summary>
        public List<string> ExcludedFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the user filtering options
        /// </summary>
        public List<string> IncludedUsers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the users to exclude from queries
        /// </summary>
        public List<string> ExcludedUsers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the host filtering options
        /// </summary>
        public List<string> IncludedHosts { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the hosts to exclude from queries
        /// </summary>
        public List<string> ExcludedHosts { get; set; } = new List<string>();

        /// <summary>
        /// Initializes a new instance of the LicenseQueryOptions class
        /// </summary>
        public LicenseQueryOptions()
        {
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public LicenseQueryOptions Clone()
        {
            var clone = new LicenseQueryOptions
            {
                CacheExpiration = CacheExpiration,
                QueryTimeout = QueryTimeout,
                ParsingTimeout = ParsingTimeout,
                ServerResponseTimeout = ServerResponseTimeout,
                RetryDelay = RetryDelay,
                MaxRetries = MaxRetries,
                MaxCacheSize = MaxCacheSize,
                MaxMemoryUsage = MaxMemoryUsage,
                MaxConcurrentQueries = MaxConcurrentQueries,
                MaxOutputSize = MaxOutputSize,
                StatisticsRetentionDays = StatisticsRetentionDays,
                MaxStatisticsEntries = MaxStatisticsEntries,
                EnableCaching = EnableCaching,
                EnableStatistics = EnableStatistics,
                EnableVerboseOutput = EnableVerboseOutput,
                EnableDetailedParsing = EnableDetailedParsing,
                EnableErrorRecovery = EnableErrorRecovery,
                EnableIncrementalUpdates = EnableIncrementalUpdates,
                EnableFeatureBatching = EnableFeatureBatching,
                EnableUserActivityTracking = EnableUserActivityTracking,
                EnableBorrowingTracking = EnableBorrowingTracking,
                EnableHealthMonitoring = EnableHealthMonitoring,
                EnablePerformanceMetrics = EnablePerformanceMetrics,
                EnableAlerting = EnableAlerting,
                FailFastOnInvalidData = FailFastOnInvalidData,
                EnableAutoCleanup = EnableAutoCleanup,
                EnableCompactOutput = EnableCompactOutput,
                DefaultOutputFormat = DefaultOutputFormat,
                PreferredLanguage = PreferredLanguage,
                AlertThreshold = AlertThreshold,
                CleanupInterval = CleanupInterval,
                HealthCheckInterval = HealthCheckInterval,
                PerformanceMetricsInterval = PerformanceMetricsInterval
            };

            // Copy collections
            foreach (var kvp in RegexPatterns)
            {
                clone.RegexPatterns[kvp.Key] = kvp.Value;
            }

            clone.IncludedFeatures.AddRange(IncludedFeatures);
            clone.ExcludedFeatures.AddRange(ExcludedFeatures);
            clone.IncludedUsers.AddRange(IncludedUsers);
            clone.ExcludedUsers.AddRange(ExcludedUsers);
            clone.IncludedHosts.AddRange(IncludedHosts);
            clone.ExcludedHosts.AddRange(ExcludedHosts);

            return clone;
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // Validate timeout relationships
            if (QueryTimeout < ParsingTimeout)
            {
                errors.Add("Query timeout should be greater than or equal to parsing timeout");
            }

            if (ServerResponseTimeout > QueryTimeout)
            {
                errors.Add("Server response timeout should be less than or equal to query timeout");
            }

            // Validate retry logic
            if (MaxRetries > 0 && RetryDelay.TotalSeconds == 0)
            {
                errors.Add("Retry delay should be greater than zero when max retries is greater than zero");
            }

            // Validate memory limits
            if (MaxMemoryUsage > 1073741824) // 1GB
            {
                errors.Add("Max memory usage exceeds recommended limit of 1GB");
            }

            // Validate concurrent queries
            if (MaxConcurrentQueries > 100)
            {
                errors.Add("Max concurrent queries exceeds recommended limit of 100");
            }

            // Validate output size
            if (MaxOutputSize > 104857600) // 100MB
            {
                errors.Add("Max output size exceeds recommended limit of 100MB");
            }

            // Validate statistics settings
            if (StatisticsRetentionDays > 365)
            {
                errors.Add("Statistics retention days exceeds recommended limit of 365");
            }

            if (MaxStatisticsEntries > 100000)
            {
                errors.Add("Max statistics entries exceeds recommended limit of 100000");
            }

            // Validate interval relationships
            if (PerformanceMetricsInterval > HealthCheckInterval)
            {
                errors.Add("Performance metrics interval should be less than or equal to health check interval");
            }

            if (CleanupInterval < HealthCheckInterval)
            {
                errors.Add("Cleanup interval should be greater than or equal to health check interval");
            }

            // Validate alert threshold
            if (AlertThreshold < 50 && EnableAlerting)
            {
                errors.Add("Alert threshold less than 50% may generate excessive notifications");
            }

            // Validate output format
            var validFormats = new[] { "Standard", "Verbose", "Compact", "JSON", "XML" };
            if (!Array.Exists(validFormats, format => format.Equals(DefaultOutputFormat, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Invalid default output format: {DefaultOutputFormat}. Valid formats are: {string.Join(", ", validFormats)}");
            }

            // Validate language format
            if (!System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .Any(c => c.Name.Equals(PreferredLanguage, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Invalid preferred language: {PreferredLanguage}");
            }

            // Validate regex patterns
            if (RegexPatterns.Count == 0)
            {
                errors.Add("At least one regex pattern must be specified");
            }

            foreach (var pattern in RegexPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern.Value))
                {
                    errors.Add($"Regex pattern for '{pattern.Key}' cannot be empty");
                }
                else
                {
                    try
                    {
                        // Test if the regex pattern is valid
                        new System.Text.RegularExpressions.Regex(pattern.Value);
                    }
                    catch (ArgumentException ex)
                    {
                        errors.Add($"Invalid regex pattern for '{pattern.Key}': {ex.Message}");
                    }
                }
            }

            // Validate filter lists
            if (IncludedFeatures.Count > 100)
            {
                errors.Add("Included features list exceeds recommended limit of 100");
            }

            if (ExcludedFeatures.Count > 100)
            {
                errors.Add("Excluded features list exceeds recommended limit of 100");
            }

            if (IncludedUsers.Count > 1000)
            {
                errors.Add("Included users list exceeds recommended limit of 1000");
            }

            if (ExcludedUsers.Count > 1000)
            {
                errors.Add("Excluded users list exceeds recommended limit of 1000");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the license query options
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("License Query Options:");
            builder.AppendLine($"  Cache Expiration: {CacheExpiration.TotalSeconds:F1}s");
            builder.AppendLine($"  Query Timeout: {QueryTimeout.TotalSeconds:F1}s");
            builder.AppendLine($"  Parsing Timeout: {ParsingTimeout.TotalSeconds:F1}s");
            builder.AppendLine($"  Server Response Timeout: {ServerResponseTimeout.TotalSeconds:F1}s");
            builder.AppendLine($"  Max Retries: {MaxRetries}");
            builder.AppendLine($"  Retry Delay: {RetryDelay.TotalSeconds:F1}s");
            builder.AppendLine($"  Max Cache Size: {MaxCacheSize} entries");
            builder.AppendLine($"  Max Memory Usage: {MaxMemoryUsage / (1024 * 1024):F1}MB");
            builder.AppendLine($"  Max Concurrent Queries: {MaxConcurrentQueries}");
            builder.AppendLine($"  Max Output Size: {MaxOutputSize / (1024 * 1024):F1}MB");
            builder.AppendLine($"  Statistics Retention: {StatisticsRetentionDays} days");
            builder.AppendLine($"  Enable Caching: {EnableCaching}");
            builder.AppendLine($"  Enable Statistics: {EnableStatistics}");
            builder.AppendLine($"  Enable Verbose Output: {EnableVerboseOutput}");
            builder.AppendLine($"  Enable Detailed Parsing: {EnableDetailedParsing}");
            builder.AppendLine($"  Enable Error Recovery: {EnableErrorRecovery}");
            builder.AppendLine($"  Enable Incremental Updates: {EnableIncrementalUpdates}");
            builder.AppendLine($"  Enable Feature Batching: {EnableFeatureBatching}");
            builder.AppendLine($"  Enable User Activity Tracking: {EnableUserActivityTracking}");
            builder.AppendLine($"  Enable Borrowing Tracking: {EnableBorrowingTracking}");
            builder.AppendLine($"  Enable Health Monitoring: {EnableHealthMonitoring}");
            builder.AppendLine($"  Enable Performance Metrics: {EnablePerformanceMetrics}");
            builder.AppendLine($"  Enable Alerting: {EnableAlerting}");
            builder.AppendLine($"  Fail Fast On Invalid Data: {FailFastOnInvalidData}");
            builder.AppendLine($"  Enable Auto Cleanup: {EnableAutoCleanup}");
            builder.AppendLine($"  Enable Compact Output: {EnableCompactOutput}");
            builder.AppendLine($"  Default Output Format: {DefaultOutputFormat}");
            builder.AppendLine($"  Preferred Language: {PreferredLanguage}");
            builder.AppendLine($"  Alert Threshold: {AlertThreshold}%");
            builder.AppendLine($"  Cleanup Interval: {CleanupInterval}s");
            builder.AppendLine($"  Health Check Interval: {HealthCheckInterval}s");
            builder.AppendLine($"  Performance Metrics Interval: {PerformanceMetricsInterval}s");
            builder.AppendLine($"  Regex Patterns: {RegexPatterns.Count} patterns");

            if (IncludedFeatures.Count > 0)
            {
                builder.AppendLine($"  Included Features: {IncludedFeatures.Count} features");
            }

            if (ExcludedFeatures.Count > 0)
            {
                builder.AppendLine($"  Excluded Features: {ExcludedFeatures.Count} features");
            }

            if (IncludedUsers.Count > 0)
            {
                builder.AppendLine($"  Included Users: {IncludedUsers.Count} users");
            }

            if (ExcludedUsers.Count > 0)
            {
                builder.AppendLine($"  Excluded Users: {ExcludedUsers.Count} users");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets default options for typical SolidWorks license scenarios
        /// </summary>
        /// <returns>Default options</returns>
        public static LicenseQueryOptions DefaultSolidWorksOptions()
        {
            return new LicenseQueryOptions
            {
                CacheExpiration = TimeSpan.FromMinutes(5),
                QueryTimeout = TimeSpan.FromSeconds(30),
                ParsingTimeout = TimeSpan.FromSeconds(15),
                ServerResponseTimeout = TimeSpan.FromSeconds(20),
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(2),
                MaxCacheSize = 1000,
                MaxMemoryUsage = 52428800, // 50MB
                MaxConcurrentQueries = 10,
                MaxOutputSize = 10485760, // 10MB
                StatisticsRetentionDays = 30,
                MaxStatisticsEntries = 10000,
                EnableCaching = true,
                EnableStatistics = true,
                EnableVerboseOutput = false,
                EnableDetailedParsing = true,
                EnableErrorRecovery = true,
                EnableIncrementalUpdates = true,
                EnableFeatureBatching = true,
                EnableUserActivityTracking = true,
                EnableBorrowingTracking = true,
                EnableHealthMonitoring = true,
                EnablePerformanceMetrics = true,
                EnableAlerting = true,
                FailFastOnInvalidData = false,
                EnableAutoCleanup = true,
                EnableCompactOutput = false,
                DefaultOutputFormat = "Standard",
                PreferredLanguage = "en-US",
                AlertThreshold = 90,
                CleanupInterval = 3600,
                HealthCheckInterval = 60,
                PerformanceMetricsInterval = 30
            };
        }

        /// <summary>
        /// Gets options for high-performance scenarios with minimal caching
        /// </summary>
        /// <returns>High-performance options</returns>
        public static LicenseQueryOptions HighPerformanceOptions()
        {
            return new LicenseQueryOptions
            {
                CacheExpiration = TimeSpan.FromMinutes(1),
                QueryTimeout = TimeSpan.FromSeconds(10),
                ParsingTimeout = TimeSpan.FromSeconds(5),
                ServerResponseTimeout = TimeSpan.FromSeconds(8),
                MaxRetries = 1,
                RetryDelay = TimeSpan.FromSeconds(1),
                MaxCacheSize = 100,
                MaxMemoryUsage = 20971520, // 20MB
                MaxConcurrentQueries = 50,
                MaxOutputSize = 5242880, // 5MB
                StatisticsRetentionDays = 7,
                MaxStatisticsEntries = 1000,
                EnableCaching = true,
                EnableStatistics = false,
                EnableVerboseOutput = false,
                EnableDetailedParsing = false,
                EnableErrorRecovery = false,
                EnableIncrementalUpdates = false,
                EnableFeatureBatching = true,
                EnableUserActivityTracking = false,
                EnableBorrowingTracking = false,
                EnableHealthMonitoring = true,
                EnablePerformanceMetrics = true,
                EnableAlerting = false,
                FailFastOnInvalidData = true,
                EnableAutoCleanup = true,
                EnableCompactOutput = true,
                DefaultOutputFormat = "Compact",
                PreferredLanguage = "en-US",
                AlertThreshold = 95,
                CleanupInterval = 1800,
                HealthCheckInterval = 30,
                PerformanceMetricsInterval = 15
            };
        }

        /// <summary>
        /// Gets options for debugging scenarios with maximum output
        /// </summary>
        /// <returns>Debug options</returns>
        public static LicenseQueryOptions DebugOptions()
        {
            return new LicenseQueryOptions
            {
                CacheExpiration = TimeSpan.FromSeconds(30),
                QueryTimeout = TimeSpan.FromMinutes(2),
                ParsingTimeout = TimeSpan.FromMinutes(1),
                ServerResponseTimeout = TimeSpan.FromMinutes(1),
                MaxRetries = 5,
                RetryDelay = TimeSpan.FromSeconds(5),
                MaxCacheSize = 50,
                MaxMemoryUsage = 104857600, // 100MB
                MaxConcurrentQueries = 1,
                MaxOutputSize = 104857600, // 100MB
                StatisticsRetentionDays = 1,
                MaxStatisticsEntries = 100,
                EnableCaching = false,
                EnableStatistics = true,
                EnableVerboseOutput = true,
                EnableDetailedParsing = true,
                EnableErrorRecovery = true,
                EnableIncrementalUpdates = false,
                EnableFeatureBatching = false,
                EnableUserActivityTracking = true,
                EnableBorrowingTracking = true,
                EnableHealthMonitoring = true,
                EnablePerformanceMetrics = true,
                EnableAlerting = false,
                FailFastOnInvalidData = false,
                EnableAutoCleanup = false,
                EnableCompactOutput = false,
                DefaultOutputFormat = "Verbose",
                PreferredLanguage = "en-US",
                AlertThreshold = 100,
                CleanupInterval = 86400,
                HealthCheckInterval = 10,
                PerformanceMetricsInterval = 5
            };
        }
    }
}