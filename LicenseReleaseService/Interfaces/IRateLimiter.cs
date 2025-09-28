using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService
{
    /// <summary>
    /// Defines the contract for rate limiting license release operations
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Checks if a license release operation is allowed based on rate limits
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name requesting the release</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Rate limit check result with wait time if limited</returns>
        Task<RateLimitResult> CanReleaseLicenseAsync(string server, string feature, string user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a successful license release operation for rate limiting
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name who released the license</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task RecordReleaseAsync(string server, string feature, string user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a failed license release operation for rate limiting
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name who attempted the release</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task RecordFailureAsync(string server, string feature, string user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current rate limit status for a specific server and feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current rate limit status</returns>
        Task<RateLimitStatus> GetRateLimitStatusAsync(string server, string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets rate limits for a specific server and feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task ResetRateLimitAsync(string server, string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates rate limit configuration
        /// </summary>
        /// <param name="configuration">New rate limit configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task UpdateConfigurationAsync(RateLimitConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current rate limit configuration
        /// </summary>
        /// <returns>Current rate limit configuration</returns>
        RateLimitConfiguration GetConfiguration();

        /// <summary>
        /// Cleans up expired rate limit entries
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task CleanupExpiredEntriesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of a rate limit check
    /// </summary>
    public class RateLimitResult
    {
        /// <summary>
        /// Gets whether the operation is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Gets the time to wait before the next allowed operation (if limited)
        /// </summary>
        public TimeSpan RetryAfter { get; set; }

        /// <summary>
        /// Gets the reason for the rate limit decision
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets the current rate limit status
        /// </summary>
        public RateLimitStatus Status { get; set; }

        /// <summary>
        /// Creates a successful rate limit result
        /// </summary>
        /// <param name="status">Current rate limit status</param>
        /// <returns>Rate limit result indicating success</returns>
        public static RateLimitResult Success(RateLimitStatus status)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RetryAfter = TimeSpan.Zero,
                Reason = "Within rate limits",
                Status = status
            };
        }

        /// <summary>
        /// Creates a failed rate limit result
        /// </summary>
        /// <param name="retryAfter">Time to wait before retry</param>
        /// <param name="reason">Reason for the limit</param>
        /// <param name="status">Current rate limit status</param>
        /// <returns>Rate limit result indicating failure</returns>
        public static RateLimitResult Limited(TimeSpan retryAfter, string reason, RateLimitStatus status)
        {
            return new RateLimitResult
            {
                IsAllowed = false,
                RetryAfter = retryAfter,
                Reason = reason,
                Status = status
            };
        }
    }

    /// <summary>
    /// Represents the current rate limit status
    /// </summary>
    public class RateLimitStatus
    {
        /// <summary>
        /// Gets the number of requests remaining in the current window
        /// </summary>
        public int RemainingRequests { get; set; }

        /// <summary>
        /// Gets the maximum number of requests allowed in the window
        /// </summary>
        public int MaxRequests { get; set; }

        /// <summary>
        /// Gets the time when the current rate limit window resets
        /// </summary>
        public DateTime ResetTime { get; set; }

        /// <summary>
        /// Gets the number of requests made in the current window
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets the current cooldown time remaining (if any)
        /// </summary>
        public TimeSpan CooldownRemaining { get; set; }

        /// <summary>
        /// Gets whether the rate limiter is currently in cooldown
        /// </summary>
        public bool IsInCooldown => CooldownRemaining > TimeSpan.Zero;
    }
}