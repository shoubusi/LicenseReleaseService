using System;
using System.ComponentModel;
using System.Configuration;

namespace LicenseReleaseService.LicenseManagement.Caching
{
    /// <summary>
    /// Configuration options for the cache manager
    /// </summary>
    public class CacheOptions
    {
        private TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);
        private TimeSpan _serverStatusExpiration = TimeSpan.FromMinutes(2);
        private TimeSpan _licenseInfoExpiration = TimeSpan.FromMinutes(3);
        private TimeSpan _licenseFeatureExpiration = TimeSpan.FromMinutes(4);
        private int _maximumItems = 1000;
        private bool _enableSlidingExpiration = true;
        private TimeSpan _slidingExpirationWindow = TimeSpan.FromMinutes(1);
        private bool _enableCompression = false;
        private bool _enableStatistics = true;
        private TimeSpan _statisticsUpdateInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the default expiration time for cache items
        /// </summary>
        [Description("Default expiration time for cache items")]
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
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
        /// Gets or sets the expiration time for server status cache items
        /// </summary>
        [Description("Expiration time for server status cache items")]
        [DefaultValue(typeof(TimeSpan), "00:02:00")]
        public TimeSpan ServerStatusExpiration
        {
            get => _serverStatusExpiration;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Server status expiration must be positive", nameof(value));
                _serverStatusExpiration = value;
            }
        }

        /// <summary>
        /// Gets or sets the expiration time for license information cache items
        /// </summary>
        [Description("Expiration time for license information cache items")]
        [DefaultValue(typeof(TimeSpan), "00:03:00")]
        public TimeSpan LicenseInfoExpiration
        {
            get => _licenseInfoExpiration;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("License info expiration must be positive", nameof(value));
                _licenseInfoExpiration = value;
            }
        }

        /// <summary>
        /// Gets or sets the expiration time for license feature cache items
        /// </summary>
        [Description("Expiration time for license feature cache items")]
        [DefaultValue(typeof(TimeSpan), "00:04:00")]
        public TimeSpan LicenseFeatureExpiration
        {
            get => _licenseFeatureExpiration;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("License feature expiration must be positive", nameof(value));
                _licenseFeatureExpiration = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of items in the cache
        /// </summary>
        [Description("Maximum number of items in the cache")]
        [DefaultValue(1000)]
        public int MaximumItems
        {
            get => _maximumItems;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Maximum items must be positive", nameof(value));
                _maximumItems = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable sliding expiration
        /// </summary>
        [Description("Enable sliding expiration for cache items")]
        [DefaultValue(true)]
        public bool EnableSlidingExpiration
        {
            get => _enableSlidingExpiration;
            set => _enableSlidingExpiration = value;
        }

        /// <summary>
        /// Gets or sets the sliding expiration window
        /// </summary>
        [Description("Sliding expiration window for cache items")]
        [DefaultValue(typeof(TimeSpan), "00:01:00")]
        public TimeSpan SlidingExpirationWindow
        {
            get => _slidingExpirationWindow;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Sliding expiration window must be positive", nameof(value));
                _slidingExpirationWindow = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable compression for cached items
        /// </summary>
        [Description("Enable compression for cached items")]
        [DefaultValue(false)]
        public bool EnableCompression
        {
            get => _enableCompression;
            set => _enableCompression = value;
        }

        /// <summary>
        /// Gets or sets whether to enable statistics collection
        /// </summary>
        [Description("Enable statistics collection")]
        [DefaultValue(true)]
        public bool EnableStatistics
        {
            get => _enableStatistics;
            set => _enableStatistics = value;
        }

        /// <summary>
        /// Gets or sets the interval for updating statistics
        /// </summary>
        [Description("Interval for updating statistics")]
        [DefaultValue(typeof(TimeSpan), "00:01:00")]
        public TimeSpan StatisticsUpdateInterval
        {
            get => _statisticsUpdateInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Statistics update interval must be positive", nameof(value));
                _statisticsUpdateInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the cache key prefix
        /// </summary>
        [Description("Prefix for cache keys")]
        [DefaultValue("LicenseCache_")]
        public string KeyPrefix { get; set; } = "LicenseCache_";

        /// <summary>
        /// Gets or sets whether to enable detailed logging
        /// </summary>
        [Description("Enable detailed logging of cache operations")]
        [DefaultValue(true)]
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the memory pressure threshold percentage
        /// </summary>
        [Description("Memory pressure threshold percentage for cache cleanup")]
        [DefaultValue(80)]
        public int MemoryPressureThreshold
        {
            get => _memoryPressureThreshold;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Memory pressure threshold must be between 0 and 100", nameof(value));
                _memoryPressureThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the percentage of items to remove when memory pressure is high
        /// </summary>
        [Description("Percentage of items to remove when memory pressure is high")]
        [DefaultValue(20)]
        public int MemoryPressureCleanupPercentage
        {
            get => _memoryPressureCleanupPercentage;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentException("Memory pressure cleanup percentage must be between 0 and 100", nameof(value));
                _memoryPressureCleanupPercentage = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable automatic memory pressure monitoring
        /// </summary>
        [Description("Enable automatic memory pressure monitoring")]
        [DefaultValue(true)]
        public bool EnableMemoryPressureMonitoring { get; set; } = true;

        private int _memoryPressureThreshold = 80;
        private int _memoryPressureCleanupPercentage = 20;

        /// <summary>
        /// Gets or sets the cache region name
        /// </summary>
        [Description("Cache region name for organization")]
        [DefaultValue("LicenseManagement")]
        public string CacheRegion { get; set; } = "LicenseManagement";

        /// <summary>
        /// Gets or sets the priority for cache eviction
        /// </summary>
        [Description("Priority for cache eviction")]
        [DefaultValue(CacheEvictionPriority.Normal)]
        public CacheEvictionPriority EvictionPriority { get; set; } = CacheEvictionPriority.Normal;

        /// <summary>
        /// Gets or sets whether to enable background cleanup
        /// </summary>
        [Description("Enable background cleanup of expired items")]
        [DefaultValue(true)]
        public bool EnableBackgroundCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets the background cleanup interval
        /// </summary>
        [Description("Background cleanup interval")]
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan BackgroundCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Creates a new CacheOptions instance with default values
        /// </summary>
        public CacheOptions()
        {
        }

        /// <summary>
        /// Creates a new CacheOptions instance with custom values
        /// </summary>
        /// <param name="defaultExpiration">Default expiration time</param>
        /// <param name="maximumItems">Maximum number of items</param>
        /// <param name="enableSlidingExpiration">Enable sliding expiration</param>
        public CacheOptions(TimeSpan defaultExpiration, int maximumItems, bool enableSlidingExpiration)
        {
            DefaultExpiration = defaultExpiration;
            MaximumItems = maximumItems;
            EnableSlidingExpiration = enableSlidingExpiration;
        }

        /// <summary>
        /// Validates the cache options
        /// </summary>
        /// <returns>List of validation errors</returns>
        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (DefaultExpiration <= TimeSpan.Zero)
                errors.Add("Default expiration must be positive");

            if (ServerStatusExpiration <= TimeSpan.Zero)
                errors.Add("Server status expiration must be positive");

            if (LicenseInfoExpiration <= TimeSpan.Zero)
                errors.Add("License info expiration must be positive");

            if (LicenseFeatureExpiration <= TimeSpan.Zero)
                errors.Add("License feature expiration must be positive");

            if (MaximumItems <= 0)
                errors.Add("Maximum items must be positive");

            if (SlidingExpirationWindow <= TimeSpan.Zero)
                errors.Add("Sliding expiration window must be positive");

            if (StatisticsUpdateInterval <= TimeSpan.Zero)
                errors.Add("Statistics update interval must be positive");

            if (string.IsNullOrWhiteSpace(KeyPrefix))
                errors.Add("Key prefix cannot be empty");

            if (MemoryPressureThreshold < 0 || MemoryPressureThreshold > 100)
                errors.Add("Memory pressure threshold must be between 0 and 100");

            if (MemoryPressureCleanupPercentage < 0 || MemoryPressureCleanupPercentage > 100)
                errors.Add("Memory pressure cleanup percentage must be between 0 and 100");

            if (BackgroundCleanupInterval <= TimeSpan.Zero)
                errors.Add("Background cleanup interval must be positive");

            return errors;
        }

        /// <summary>
        /// Creates a copy of the current cache options
        /// </summary>
        /// <returns>New CacheOptions instance with the same values</returns>
        public CacheOptions Clone()
        {
            return new CacheOptions
            {
                DefaultExpiration = DefaultExpiration,
                ServerStatusExpiration = ServerStatusExpiration,
                LicenseInfoExpiration = LicenseInfoExpiration,
                LicenseFeatureExpiration = LicenseFeatureExpiration,
                MaximumItems = MaximumItems,
                EnableSlidingExpiration = EnableSlidingExpiration,
                SlidingExpirationWindow = SlidingExpirationWindow,
                EnableCompression = EnableCompression,
                EnableStatistics = EnableStatistics,
                StatisticsUpdateInterval = StatisticsUpdateInterval,
                KeyPrefix = KeyPrefix,
                EnableDetailedLogging = EnableDetailedLogging,
                MemoryPressureThreshold = MemoryPressureThreshold,
                MemoryPressureCleanupPercentage = MemoryPressureCleanupPercentage,
                EnableMemoryPressureMonitoring = EnableMemoryPressureMonitoring,
                CacheRegion = CacheRegion,
                EvictionPriority = EvictionPriority,
                EnableBackgroundCleanup = EnableBackgroundCleanup,
                BackgroundCleanupInterval = BackgroundCleanupInterval
            };
        }

        /// <summary>
        /// Returns a string representation of the cache options
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"CacheOptions[DefaultExpiration={DefaultExpiration}, " +
                   $"MaxItems={MaximumItems}, SlidingExpiration={EnableSlidingExpiration}, " +
                   $"Statistics={EnableStatistics}, Compression={EnableCompression}, " +
                   $"KeyPrefix={KeyPrefix}]";
        }
    }

    /// <summary>
    /// Defines the priority for cache eviction
    /// </summary>
    public enum CacheEvictionPriority
    {
        /// <summary>
        /// Low priority items are evicted first
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority items
        /// </summary>
        Normal,

        /// <summary>
        /// High priority items are evicted last
        /// </summary>
        High
    }
}