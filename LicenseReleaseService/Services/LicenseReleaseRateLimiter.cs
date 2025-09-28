using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService
{
    /// <summary>
    /// Implements rate limiting for license release operations
    /// </summary>
    public class LicenseReleaseRateLimiter : IRateLimiter
    {
        private readonly RateLimitConfiguration _configuration;
        private readonly RateLimitStateManager _stateManager;
        private readonly object _configurationLock = new object();

        /// <summary>
        /// Initializes a new instance of the LicenseReleaseRateLimiter
        /// </summary>
        /// <param name="configuration">Rate limit configuration</param>
        public LicenseReleaseRateLimiter(RateLimitConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _stateManager = new RateLimitStateManager();
        }

        /// <summary>
        /// Initializes a new instance of the LicenseReleaseRateLimiter with default configuration
        /// </summary>
        public LicenseReleaseRateLimiter() : this(RateLimitConfiguration.Default())
        {
        }

        /// <inheritdoc/>
        public async Task<RateLimitResult> CanReleaseLicenseAsync(string server, string feature, string user, CancellationToken cancellationToken = default)
        {
            if (!_configuration.IsEnabled)
            {
                return RateLimitResult.Success(new RateLimitStatus
                {
                    RemainingRequests = int.MaxValue,
                    MaxRequests = int.MaxValue,
                    ResetTime = DateTime.UtcNow,
                    RequestCount = 0,
                    CooldownRemaining = TimeSpan.Zero
                });
            }

            var key = _configuration.GetRateLimitKey(server, feature, user);
            var currentTime = DateTime.UtcNow;
            var state = _stateManager.GetOrCreateState(key, currentTime, _configuration.BurstSize);

            // Check if state is in failure cooldown
            if (state.IsInFailureCooldown)
            {
                var failureCooldownRemaining = state.GetCooldownRemaining(currentTime, _configuration.FailureCooldownPeriod);
                if (failureCooldownRemaining > TimeSpan.Zero)
                {
                    return RateLimitResult.Limited(
                        failureCooldownRemaining,
                        $"Rate limited due to consecutive failures. Retry after {failureCooldownRemaining.TotalSeconds:F1} seconds.",
                        CreateRateLimitStatus(state, currentTime));
                }
                else
                {
                    // Failure cooldown has expired
                    state.IsInFailureCooldown = false;
                    state.ConsecutiveFailures = 0;
                    state.CooldownStart = null;
                }
            }

            // Check if current window has expired
            if (state.IsWindowExpired(currentTime, _configuration.WindowSize))
            {
                state.ResetWindow(currentTime);
                state.RefillBurstTokens(currentTime, _configuration.BurstSize, _configuration.WindowSize);
            }

            // Check if in cooldown
            if (state.IsInCooldown(currentTime, _configuration.CooldownPeriod))
            {
                var cooldownRemaining = state.GetCooldownRemaining(currentTime, _configuration.CooldownPeriod);
                return RateLimitResult.Limited(
                    cooldownRemaining,
                    $"Rate limited. Retry after {cooldownRemaining.TotalSeconds:F1} seconds.",
                    CreateRateLimitStatus(state, currentTime));
            }

            // Check regular rate limit
            if (state.RequestCount >= _configuration.MaxReleasesPerWindow)
            {
                // Try to use burst token
                if (!state.UseBurstToken(currentTime))
                {
                    var timeToReset = state.GetTimeToWindowReset(currentTime, _configuration.WindowSize);
                    return RateLimitResult.Limited(
                        timeToReset,
                        $"Rate limit exceeded. Maximum {_configuration.MaxReleasesPerWindow} releases per {_configuration.WindowSize.TotalMinutes:F1} minute(s). Retry after {timeToReset.TotalSeconds:F1} seconds.",
                        CreateRateLimitStatus(state, currentTime));
                }
            }

            // Check if we can use burst token
            if (state.RequestCount >= _configuration.MaxReleasesPerWindow && state.BurstTokens == 0)
            {
                var timeToReset = state.GetTimeToWindowReset(currentTime, _configuration.WindowSize);
                return RateLimitResult.Limited(
                    timeToReset,
                    $"Burst capacity exhausted. Retry after {timeToReset.TotalSeconds:F1} seconds.",
                    CreateRateLimitStatus(state, currentTime));
            }

            // All checks passed
            return RateLimitResult.Success(CreateRateLimitStatus(state, currentTime));
        }

        /// <inheritdoc/>
        public async Task RecordReleaseAsync(string server, string feature, string user, CancellationToken cancellationToken = default)
        {
            if (!_configuration.IsEnabled)
                return;

            var key = _configuration.GetRateLimitKey(server, feature, user);
            var currentTime = DateTime.UtcNow;
            var state = _stateManager.GetOrCreateState(key, currentTime, _configuration.BurstSize);

            // Reset window if expired
            if (state.IsWindowExpired(currentTime, _configuration.WindowSize))
            {
                state.ResetWindow(currentTime);
                state.RefillBurstTokens(currentTime, _configuration.BurstSize, _configuration.WindowSize);
            }

            state.RecordSuccess(currentTime);

            // Check if we hit the rate limit and should start cooldown
            if (state.RequestCount >= _configuration.MaxReleasesPerWindow)
            {
                state.StartCooldown(currentTime);
            }
        }

        /// <inheritdoc/>
        public async Task RecordFailureAsync(string server, string feature, string user, CancellationToken cancellationToken = default)
        {
            if (!_configuration.IsEnabled)
                return;

            var key = _configuration.GetRateLimitKey(server, feature, user);
            var currentTime = DateTime.UtcNow;
            var state = _stateManager.GetOrCreateState(key, currentTime, _configuration.BurstSize);

            // Reset window if expired
            if (state.IsWindowExpired(currentTime, _configuration.WindowSize))
            {
                state.ResetWindow(currentTime);
                state.RefillBurstTokens(currentTime, _configuration.BurstSize, _configuration.WindowSize);
            }

            state.RecordFailure(currentTime, _configuration.MaxConsecutiveFailures);
        }

        /// <inheritdoc/>
        public async Task<RateLimitStatus> GetRateLimitStatusAsync(string server, string feature, CancellationToken cancellationToken = default)
        {
            if (!_configuration.IsEnabled)
            {
                return new RateLimitStatus
                {
                    RemainingRequests = int.MaxValue,
                    MaxRequests = int.MaxValue,
                    ResetTime = DateTime.UtcNow,
                    RequestCount = 0,
                    CooldownRemaining = TimeSpan.Zero
                };
            }

            var key = _configuration.GetRateLimitKey(server, feature, string.Empty); // User-agnostic status
            var currentTime = DateTime.UtcNow;
            var state = _stateManager.GetState(key);

            if (state == null)
            {
                return new RateLimitStatus
                {
                    RemainingRequests = _configuration.MaxReleasesPerWindow,
                    MaxRequests = _configuration.MaxReleasesPerWindow,
                    ResetTime = currentTime.Add(_configuration.WindowSize),
                    RequestCount = 0,
                    CooldownRemaining = TimeSpan.Zero
                };
            }

            return CreateRateLimitStatus(state, currentTime);
        }

        /// <inheritdoc/>
        public async Task ResetRateLimitAsync(string server, string feature, CancellationToken cancellationToken = default)
        {
            var key = _configuration.GetRateLimitKey(server, feature, string.Empty);
            _stateManager.RemoveState(key);
        }

        /// <inheritdoc/>
        public async Task UpdateConfigurationAsync(RateLimitConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            lock (_configurationLock)
            {
                _configuration.MaxReleasesPerWindow = configuration.MaxReleasesPerWindow;
                _configuration.WindowSize = configuration.WindowSize;
                _configuration.CooldownPeriod = configuration.CooldownPeriod;
                _configuration.BurstSize = configuration.BurstSize;
                _configuration.MaxConsecutiveFailures = configuration.MaxConsecutiveFailures;
                _configuration.FailureCooldownPeriod = configuration.FailureCooldownPeriod;
                _configuration.IsEnabled = configuration.IsEnabled;
                _configuration.EnablePerUserLimiting = configuration.EnablePerUserLimiting;
                _configuration.EnablePerFeatureLimiting = configuration.EnablePerFeatureLimiting;
                _configuration.EnablePerServerLimiting = configuration.EnablePerServerLimiting;
            }
        }

        /// <inheritdoc/>
        public RateLimitConfiguration GetConfiguration()
        {
            lock (_configurationLock)
            {
                return new RateLimitConfiguration
                {
                    MaxReleasesPerWindow = _configuration.MaxReleasesPerWindow,
                    WindowSize = _configuration.WindowSize,
                    CooldownPeriod = _configuration.CooldownPeriod,
                    BurstSize = _configuration.BurstSize,
                    MaxConsecutiveFailures = _configuration.MaxConsecutiveFailures,
                    FailureCooldownPeriod = _configuration.FailureCooldownPeriod,
                    IsEnabled = _configuration.IsEnabled,
                    EnablePerUserLimiting = _configuration.EnablePerUserLimiting,
                    EnablePerFeatureLimiting = _configuration.EnablePerFeatureLimiting,
                    EnablePerServerLimiting = _configuration.EnablePerServerLimiting
                };
            }
        }

        /// <inheritdoc/>
        public async Task CleanupExpiredEntriesAsync(CancellationToken cancellationToken = default)
        {
            var currentTime = DateTime.UtcNow;
            var removedCount = _stateManager.RemoveExpiredStates(currentTime, _configuration.MaxEntryAge);
        }

        /// <summary>
        /// Gets statistics about the current rate limiting state
        /// </summary>
        /// <returns>Rate limiting statistics</returns>
        public RateLimitStatistics GetStatistics()
        {
            var states = _stateManager.GetAllStates();
            var currentTime = DateTime.UtcNow;

            var activeStates = 0;
            var cooldownStates = 0;
            var failureCooldownStates = 0;
            var totalRequests = 0;

            foreach (var state in states.Values)
            {
                if (currentTime - state.LastRequestTime <= _configuration.MaxEntryAge)
                {
                    activeStates++;
                    totalRequests += state.RequestCount;

                    if (state.IsInCooldown(currentTime, _configuration.CooldownPeriod))
                        cooldownStates++;

                    if (state.IsInFailureCooldown)
                        failureCooldownStates++;
                }
            }

            return new RateLimitStatistics
            {
                TotalStates = states.Count,
                ActiveStates = activeStates,
                CooldownStates = cooldownStates,
                FailureCooldownStates = failureCooldownStates,
                TotalRequests = totalRequests,
                Configuration = GetConfiguration()
            };
        }

        private RateLimitStatus CreateRateLimitStatus(RateLimitState state, DateTime currentTime)
        {
            var remaining = Math.Max(0, _configuration.MaxReleasesPerWindow - state.RequestCount);
            var resetTime = state.WindowStart.Add(_configuration.WindowSize);
            var cooldownRemaining = state.GetCooldownRemaining(currentTime, _configuration.CooldownPeriod);

            return new RateLimitStatus
            {
                RemainingRequests = remaining + state.BurstTokens,
                MaxRequests = _configuration.MaxReleasesPerWindow + _configuration.BurstSize,
                ResetTime = resetTime,
                RequestCount = state.RequestCount,
                CooldownRemaining = cooldownRemaining
            };
        }
    }

    /// <summary>
    /// Statistics about rate limiting state
    /// </summary>
    public class RateLimitStatistics
    {
        /// <summary>
        /// Gets the total number of rate limit states
        /// </summary>
        public int TotalStates { get; set; }

        /// <summary>
        /// Gets the number of active (non-expired) rate limit states
        /// </summary>
        public int ActiveStates { get; set; }

        /// <summary>
        /// Gets the number of states currently in cooldown
        /// </summary>
        public int CooldownStates { get; set; }

        /// <summary>
        /// Gets the number of states currently in failure cooldown
        /// </summary>
        public int FailureCooldownStates { get; set; }

        /// <summary>
        /// Gets the total number of requests across all states
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets the current rate limit configuration
        /// </summary>
        public RateLimitConfiguration Configuration { get; set; }
    }
}