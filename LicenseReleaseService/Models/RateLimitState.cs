using System;
using System.Collections.Generic;
using System.Linq;

namespace LicenseReleaseService
{
    /// <summary>
    /// Represents the state of rate limiting for a specific key
    /// </summary>
    public class RateLimitState
    {
        /// <summary>
        /// Gets or sets the rate limit key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the current request count
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the window start time
        /// </summary>
        public DateTime WindowStart { get; set; }

        /// <summary>
        /// Gets or sets the last request timestamp
        /// </summary>
        public DateTime LastRequestTime { get; set; }

        /// <summary>
        /// Gets or sets the cooldown start time (if in cooldown)
        /// </summary>
        public DateTime? CooldownStart { get; set; }

        /// <summary>
        /// Gets or sets the consecutive failure count
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// Gets or sets the burst tokens available
        /// </summary>
        public int BurstTokens { get; set; }

        /// <summary>
        /// Gets or sets the last burst token refill time
        /// </summary>
        public DateTime LastBurstRefill { get; set; }

        /// <summary>
        /// Gets or sets whether the state is currently in failure cooldown
        /// </summary>
        public bool IsInFailureCooldown { get; set; }

        /// <summary>
        /// Gets or sets the last cleanup time
        /// </summary>
        public DateTime LastCleanupTime { get; set; }

        /// <summary>
        /// Gets whether the current window has expired
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="windowSize">Window size</param>
        /// <returns>True if window has expired</returns>
        public bool IsWindowExpired(DateTime currentTime, TimeSpan windowSize)
        {
            return currentTime >= WindowStart.Add(windowSize);
        }

        /// <summary>
        /// Gets whether the state is currently in cooldown
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="cooldownPeriod">Cooldown period</param>
        /// <returns>True if in cooldown</returns>
        public bool IsInCooldown(DateTime currentTime, TimeSpan cooldownPeriod)
        {
            if (!CooldownStart.HasValue)
                return false;

            return currentTime < CooldownStart.Value.Add(cooldownPeriod);
        }

        /// <summary>
        /// Gets the remaining cooldown time
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="cooldownPeriod">Cooldown period</param>
        /// <returns>Remaining cooldown time</returns>
        public TimeSpan GetCooldownRemaining(DateTime currentTime, TimeSpan cooldownPeriod)
        {
            if (!CooldownStart.HasValue)
                return TimeSpan.Zero;

            var endTime = CooldownStart.Value.Add(cooldownPeriod);
            return currentTime < endTime ? endTime - currentTime : TimeSpan.Zero;
        }

        /// <summary>
        /// Resets the current window
        /// </summary>
        /// <param name="currentTime">Current time</param>
        public void ResetWindow(DateTime currentTime)
        {
            WindowStart = currentTime;
            RequestCount = 0;
        }

        /// <summary>
        /// Starts cooldown period
        /// </summary>
        /// <param name="currentTime">Current time</param>
        public void StartCooldown(DateTime currentTime)
        {
            CooldownStart = currentTime;
        }

        /// <summary>
        /// Records a successful request
        /// </summary>
        /// <param name="currentTime">Current time</param>
        public void RecordSuccess(DateTime currentTime)
        {
            RequestCount++;
            LastRequestTime = currentTime;
            ConsecutiveFailures = 0;
            IsInFailureCooldown = false;
        }

        /// <summary>
        /// Records a failed request
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="maxConsecutiveFailures">Maximum consecutive failures before cooldown</param>
        /// <returns>True if failure cooldown was triggered</returns>
        public bool RecordFailure(DateTime currentTime, int maxConsecutiveFailures)
        {
            ConsecutiveFailures++;
            LastRequestTime = currentTime;

            if (ConsecutiveFailures >= maxConsecutiveFailures)
            {
                IsInFailureCooldown = true;
                StartCooldown(currentTime);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Uses a burst token if available
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <returns>True if burst token was used</returns>
        public bool UseBurstToken(DateTime currentTime)
        {
            if (BurstTokens > 0)
            {
                BurstTokens--;
                LastRequestTime = currentTime;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Refills burst tokens based on elapsed time
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="burstSize">Maximum burst size</param>
        /// <param name="windowSize">Time window for burst refill</param>
        public void RefillBurstTokens(DateTime currentTime, int burstSize, TimeSpan windowSize)
        {
            var timeSinceRefill = currentTime - LastBurstRefill;

            // Refill burst tokens proportionally to elapsed time
            var refillAmount = (int)(timeSinceRefill.TotalSeconds / windowSize.TotalSeconds * burstSize);

            if (refillAmount > 0)
            {
                BurstTokens = Math.Min(burstSize, BurstTokens + refillAmount);
                LastBurstRefill = currentTime;
            }
        }

        /// <summary>
        /// Gets the time until next window reset
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="windowSize">Window size</param>
        /// <returns>Time until window reset</returns>
        public TimeSpan GetTimeToWindowReset(DateTime currentTime, TimeSpan windowSize)
        {
            var resetTime = WindowStart.Add(windowSize);
            return currentTime < resetTime ? resetTime - currentTime : TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a new rate limit state
        /// </summary>
        /// <param name="key">Rate limit key</param>
        /// <param name="currentTime">Current time</param>
        /// <param name="burstSize">Initial burst size</param>
        /// <returns>New rate limit state</returns>
        public static RateLimitState Create(string key, DateTime currentTime, int burstSize)
        {
            return new RateLimitState
            {
                Key = key,
                RequestCount = 0,
                WindowStart = currentTime,
                LastRequestTime = currentTime,
                ConsecutiveFailures = 0,
                BurstTokens = burstSize,
                LastBurstRefill = currentTime,
                LastCleanupTime = currentTime
            };
        }
    }

    /// <summary>
    /// Manages multiple rate limit states
    /// </summary>
    public class RateLimitStateManager
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, RateLimitState> _states = new Dictionary<string, RateLimitState>();

        /// <summary>
        /// Gets or creates a rate limit state for a key
        /// </summary>
        /// <param name="key">Rate limit key</param>
        /// <param name="currentTime">Current time</param>
        /// <param name="burstSize">Burst size for new states</param>
        /// <returns>Rate limit state</returns>
        public RateLimitState GetOrCreateState(string key, DateTime currentTime, int burstSize)
        {
            lock (_lock)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    state = RateLimitState.Create(key, currentTime, burstSize);
                    _states[key] = state;
                }
                return state;
            }
        }

        /// <summary>
        /// Gets a rate limit state if it exists
        /// </summary>
        /// <param name="key">Rate limit key</param>
        /// <returns>Rate limit state or null</returns>
        public RateLimitState GetState(string key)
        {
            lock (_lock)
            {
                _states.TryGetValue(key, out var state);
                return state;
            }
        }

        /// <summary>
        /// Removes a rate limit state
        /// </summary>
        /// <param name="key">Rate limit key</param>
        public void RemoveState(string key)
        {
            lock (_lock)
            {
                _states.Remove(key);
            }
        }

        /// <summary>
        /// Gets all rate limit states
        /// </summary>
        /// <returns>All rate limit states</returns>
        public IReadOnlyDictionary<string, RateLimitState> GetAllStates()
        {
            lock (_lock)
            {
                return new Dictionary<string, RateLimitState>(_states);
            }
        }

        /// <summary>
        /// Removes expired states
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="maxAge">Maximum age for states</param>
        /// <returns>Number of states removed</returns>
        public int RemoveExpiredStates(DateTime currentTime, TimeSpan maxAge)
        {
            lock (_lock)
            {
                var expiredKeys = _states
                    .Where(kvp => currentTime - kvp.Value.LastRequestTime > maxAge)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _states.Remove(key);
                }

                return expiredKeys.Count;
            }
        }

        /// <summary>
        /// Clears all rate limit states
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _states.Clear();
            }
        }

        /// <summary>
        /// Gets the number of active rate limit states
        /// </summary>
        public int StateCount
        {
            get
            {
                lock (_lock)
                {
                    return _states.Count;
                }
            }
        }
    }
}