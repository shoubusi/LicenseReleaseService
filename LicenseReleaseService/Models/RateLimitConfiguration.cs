using System;
using System.ComponentModel;

namespace LicenseReleaseService
{
    /// <summary>
    /// Configuration for rate limiting license release operations
    /// </summary>
    public class RateLimitConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of license releases allowed per time window
        /// </summary>
        [DefaultValue(10)]
        public int MaxReleasesPerWindow { get; set; } = 10;

        /// <summary>
        /// Gets or sets the time window size for rate limiting
        /// </summary>
        [DefaultValue("00:01:00")]
        public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the cooldown period after reaching rate limits
        /// </summary>
        [DefaultValue("00:05:00")]
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the burst size allowed for immediate releases
        /// </summary>
        [DefaultValue(3)]
        public int BurstSize { get; set; } = 3;

        /// <summary>
        /// Gets or sets the maximum number of consecutive failures before triggering extended cooldown
        /// </summary>
        [DefaultValue(5)]
        public int MaxConsecutiveFailures { get; set; } = 5;

        /// <summary>
        /// Gets or sets the extended cooldown period after consecutive failures
        /// </summary>
        [DefaultValue("00:15:00")]
        public TimeSpan FailureCooldownPeriod { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets whether rate limiting is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the cleanup interval for expired rate limit entries
        /// </summary>
        [DefaultValue("01:00:00")]
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the maximum age for rate limit entries before cleanup
        /// </summary>
        [DefaultValue("24:00:00")]
        public TimeSpan MaxEntryAge { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Gets or sets whether to enable per-user rate limiting
        /// </summary>
        [DefaultValue(true)]
        public bool EnablePerUserLimiting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable per-feature rate limiting
        /// </summary>
        [DefaultValue(true)]
        public bool EnablePerFeatureLimiting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable per-server rate limiting
        /// </summary>
        [DefaultValue(true)]
        public bool EnablePerServerLimiting { get; set; } = true;

        /// <summary>
        /// Creates a default rate limit configuration
        /// </summary>
        /// <returns>Default configuration</returns>
        public static RateLimitConfiguration Default()
        {
            return new RateLimitConfiguration();
        }

        /// <summary>
        /// Creates a strict configuration for production environments
        /// </summary>
        /// <returns>Strict configuration</returns>
        public static RateLimitConfiguration Strict()
        {
            return new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 5,
                WindowSize = TimeSpan.FromMinutes(2),
                CooldownPeriod = TimeSpan.FromMinutes(10),
                BurstSize = 1,
                MaxConsecutiveFailures = 3,
                FailureCooldownPeriod = TimeSpan.FromMinutes(30),
                IsEnabled = true
            };
        }

        /// <summary>
        /// Creates a lenient configuration for development environments
        /// </summary>
        /// <returns>Lenient configuration</returns>
        public static RateLimitConfiguration Lenient()
        {
            return new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 100,
                WindowSize = TimeSpan.FromMinutes(1),
                CooldownPeriod = TimeSpan.FromSeconds(30),
                BurstSize = 10,
                MaxConsecutiveFailures = 20,
                FailureCooldownPeriod = TimeSpan.FromMinutes(2),
                IsEnabled = true
            };
        }

        /// <summary>
        /// Validates the configuration settings
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
        public void Validate()
        {
            if (MaxReleasesPerWindow <= 0)
                throw new InvalidOperationException("MaxReleasesPerWindow must be greater than 0");

            if (WindowSize <= TimeSpan.Zero)
                throw new InvalidOperationException("WindowSize must be greater than 0");

            if (CooldownPeriod <= TimeSpan.Zero)
                throw new InvalidOperationException("CooldownPeriod must be greater than 0");

            if (BurstSize <= 0)
                throw new InvalidOperationException("BurstSize must be greater than 0");

            if (MaxConsecutiveFailures <= 0)
                throw new InvalidOperationException("MaxConsecutiveFailures must be greater than 0");

            if (FailureCooldownPeriod <= TimeSpan.Zero)
                throw new InvalidOperationException("FailureCooldownPeriod must be greater than 0");

            if (CleanupInterval <= TimeSpan.Zero)
                throw new InvalidOperationException("CleanupInterval must be greater than 0");

            if (MaxEntryAge <= TimeSpan.Zero)
                throw new InvalidOperationException("MaxEntryAge must be greater than 0");

            if (BurstSize > MaxReleasesPerWindow)
                throw new InvalidOperationException("BurstSize cannot exceed MaxReleasesPerWindow");
        }

        /// <summary>
        /// Gets the effective rate limit key for a specific server, feature, and user
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name</param>
        /// <returns>Rate limit key</returns>
        public string GetRateLimitKey(string server, string feature, string user)
        {
            var keyParts = new System.Collections.Generic.List<string>();

            if (EnablePerServerLimiting && !string.IsNullOrEmpty(server))
                keyParts.Add($"server:{server}");

            if (EnablePerFeatureLimiting && !string.IsNullOrEmpty(feature))
                keyParts.Add($"feature:{feature}");

            if (EnablePerUserLimiting && !string.IsNullOrEmpty(user))
                keyParts.Add($"user:{user}");

            return keyParts.Count > 0 ? string.Join("|", keyParts) : "global";
        }

        /// <summary>
        /// Gets the reset time for the current time window
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <returns>Time when the current window resets</returns>
        public DateTime GetWindowResetTime(DateTime currentTime)
        {
            var windowStart = new DateTime(
                currentTime.Ticks / WindowSize.Ticks * WindowSize.Ticks,
                currentTime.Kind);
            return windowStart.Add(WindowSize);
        }
    }
}