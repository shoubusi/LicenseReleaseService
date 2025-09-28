using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.Interfaces
{
    /// <summary>
    /// Defines the contract for license release strategies
    /// </summary>
    public interface ILicenseReleaseStrategy
    {
        /// <summary>
        /// Gets the name of the strategy
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the strategy
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the priority of the strategy (higher values = higher priority)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Determines whether this strategy can handle the given release request
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>True if this strategy can handle the request</returns>
        bool CanHandle(ReleaseRequest request);

        /// <summary>
        /// Executes the license release strategy
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License release result</returns>
        Task<LicenseReleaseResult> ExecuteAsync(ReleaseRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the release request before execution
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>Validation result indicating whether the request is valid</returns>
        StrategyValidationResult Validate(ReleaseRequest request);

        /// <summary>
        /// Gets the health status of the strategy
        /// </summary>
        /// <returns>Health status information</returns>
        StrategyHealthStatus GetHealthStatus();
    }

    /// <summary>
    /// Represents the result of a strategy validation
    /// </summary>
    public class StrategyValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the validation was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the validation details
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <returns>Successful validation result</returns>
        public static StrategyValidationResult Success() => new StrategyValidationResult { IsValid = true };

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="details">Validation details</param>
        /// <returns>Failed validation result</returns>
        public static StrategyValidationResult Failure(string errorMessage, string details = "")
        {
            return new StrategyValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                Details = details
            };
        }
    }

    /// <summary>
    /// Represents the health status of a strategy
    /// </summary>
    public class StrategyHealthStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether the strategy is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the health status message
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last health check timestamp
        /// </summary>
        public DateTime LastCheck { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the number of consecutive failures
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// Gets or sets the last error message
        /// </summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>
        /// Creates a healthy status
        /// </summary>
        /// <param name="message">Status message</param>
        /// <returns>Healthy status</returns>
        public static StrategyHealthStatus Healthy(string message = "Strategy is healthy")
        {
            return new StrategyHealthStatus
            {
                IsHealthy = true,
                StatusMessage = message,
                ConsecutiveFailures = 0
            };
        }

        /// <summary>
        /// Creates an unhealthy status
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="consecutiveFailures">Number of consecutive failures</param>
        /// <returns>Unhealthy status</returns>
        public static StrategyHealthStatus Unhealthy(string errorMessage, int consecutiveFailures = 1)
        {
            return new StrategyHealthStatus
            {
                IsHealthy = false,
                StatusMessage = errorMessage,
                LastError = errorMessage,
                ConsecutiveFailures = consecutiveFailures
            };
        }
    }
}