using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines the retry strategy types
    /// </summary>
    public enum RetryStrategyType
    {
        /// <summary>
        /// Fixed delay between retries
        /// </summary>
        Fixed,

        /// <summary>
        /// Linear increasing delay between retries
        /// </summary>
        Linear,

        /// <summary>
        /// Exponential backoff delay between retries
        /// </summary>
        Exponential,

        /// <summary>
        /// Exponential backoff with jitter to avoid thundering herd
        /// </summary>
        ExponentialWithJitter
    }

    /// <summary>
    /// Configuration for retry behavior
    /// </summary>
    public class RetryPolicyOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial delay between retries
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum delay between retries
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the retry strategy type
        /// </summary>
        public RetryStrategyType Strategy { get; set; } = RetryStrategyType.Exponential;

        /// <summary>
        /// Gets or sets the backoff multiplier for linear/exponential strategies
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the jitter factor for exponential with jitter strategy (0.0 to 1.0)
        /// </summary>
        public double JitterFactor { get; set; } = 0.1;

        /// <summary>
        /// Gets or sets the exception types that should be retried
        /// </summary>
        public HashSet<Type> RetryableExceptionTypes { get; set; } = new HashSet<Type>();

        /// <summary>
        /// Gets or sets a value indicating whether to retry on timeout
        /// </summary>
        public bool RetryOnTimeout { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to retry on transient errors
        /// </summary>
        public bool RetryOnTransientErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets a custom predicate to determine if an exception should be retried
        /// </summary>
        public Func<Exception, int, bool> ShouldRetry { get; set; }
    }

    /// <summary>
    /// Implements configurable retry policies for resilient operations
    /// </summary>
    public class RetryPolicy
    {
        private readonly ILogger _logger;
        private readonly RetryPolicyOptions _options;
        private readonly Random _random;

        /// <summary>
        /// Initializes a new instance of the RetryPolicy class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Retry policy configuration</param>
        public RetryPolicy(ILogger logger, RetryPolicyOptions options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new RetryPolicyOptions();
            _random = new Random();
        }

        /// <summary>
        /// Executes an operation with retry policy
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the operation</returns>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            Exception lastException = null;

            for (int attempt = 1; attempt <= _options.MaxRetries + 1; attempt++)
            {
                try
                {
                    var result = await operation().ConfigureAwait(false);

                    if (attempt > 1)
                    {
                        _logger.LogInformation("Operation succeeded after {AttemptCount} attempts", attempt);
                    }

                    return result;
                }
                catch (Exception ex) when (ShouldRetry(ex, attempt))
                {
                    lastException = ex;

                    if (attempt <= _options.MaxRetries)
                    {
                        var delay = CalculateDelay(attempt);
                        _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms. Error: {ErrorMessage}",
                            attempt, _options.MaxRetries + 1, delay.TotalMilliseconds, ex.Message);

                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogError(ex, "Operation failed after {AttemptCount} attempts. Giving up.", attempt);
                    }
                }
            }

            throw new RetryPolicyException($"Operation failed after {_options.MaxRetries + 1} attempts", lastException);
        }

        /// <summary>
        /// Executes an operation with retry policy (non-generic version)
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await ExecuteAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if an operation should be retried
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="attempt">The current attempt number</param>
        /// <returns>True if the operation should be retried</returns>
        private bool ShouldRetry(Exception exception, int attempt)
        {
            // Don't retry on the last attempt
            if (attempt > _options.MaxRetries)
                return false;

            // Check custom retry predicate first
            if (_options.ShouldRetry != null)
            {
                return _options.ShouldRetry(exception, attempt);
            }

            // Check if this is an operation canceled exception
            if (exception is OperationCanceledException && !IsTransientException(exception))
            {
                return false;
            }

            // Check if this is a timeout exception
            if (exception is TimeoutException)
            {
                return _options.RetryOnTimeout;
            }

            // Check specific exception types
            if (_options.RetryableExceptionTypes.Any(t => t.IsInstanceOfType(exception)))
            {
                return true;
            }

            // Check for transient errors if enabled
            if (_options.RetryOnTransientErrors)
            {
                return IsTransientException(exception);
            }

            return false;
        }

        /// <summary>
        /// Determines if an exception represents a transient error
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception is transient</returns>
        private bool IsTransientException(Exception exception)
        {
            // Network-related exceptions
            if (exception is System.Net.Sockets.SocketException ||
                exception is System.Net.Http.HttpRequestException ||
                exception is System.IO.IOException)
            {
                return true;
            }

            // Timeout exceptions
            if (exception is TimeoutException ||
                (exception is OperationCanceledException && exception.Message.Contains("timeout")))
            {
                return true;
            }

            // Process execution exceptions
            if (exception is ProcessExecutionException)
            {
                return true;
            }

            // Check inner exception
            if (exception.InnerException != null)
            {
                return IsTransientException(exception.InnerException);
            }

            return false;
        }

        /// <summary>
        /// Calculates the delay for the next retry attempt
        /// </summary>
        /// <param name="attempt">The current attempt number</param>
        /// <returns>The delay to wait before the next attempt</returns>
        private TimeSpan CalculateDelay(int attempt)
        {
            var baseDelay = _options.InitialDelay;

            switch (_options.Strategy)
            {
                case RetryStrategyType.Fixed:
                    return baseDelay;

                case RetryStrategyType.Linear:
                    var linearDelay = baseDelay.TotalMilliseconds + (baseDelay.TotalMilliseconds * (attempt - 1) * _options.BackoffMultiplier);
                    return TimeSpan.FromMilliseconds(Math.Min(linearDelay, _options.MaxDelay.TotalMilliseconds));

                case RetryStrategyType.Exponential:
                    var exponentialDelay = baseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attempt - 1);
                    return TimeSpan.FromMilliseconds(Math.Min(exponentialDelay, _options.MaxDelay.TotalMilliseconds));

                case RetryStrategyType.ExponentialWithJitter:
                    var expDelay = baseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attempt - 1);
                    var jitter = expDelay * _options.JitterFactor * (_random.NextDouble() * 2 - 1);
                    var finalDelay = Math.Max(0, expDelay + jitter);
                    return TimeSpan.FromMilliseconds(Math.Min(finalDelay, _options.MaxDelay.TotalMilliseconds));

                default:
                    return baseDelay;
            }
        }

        /// <summary>
        /// Creates a default retry policy for license management operations
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>A configured retry policy</returns>
        public static RetryPolicy CreateDefaultLicensePolicy(ILogger logger)
        {
            var options = new RetryPolicyOptions
            {
                MaxRetries = 3,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                Strategy = RetryStrategyType.ExponentialWithJitter,
                BackoffMultiplier = 2.0,
                JitterFactor = 0.1,
                RetryOnTimeout = true,
                RetryOnTransientErrors = true,
                RetryableExceptionTypes = new HashSet<Type>
                {
                    typeof(ProcessExecutionException),
                    typeof(System.Net.Sockets.SocketException),
                    typeof(System.IO.IOException)
                }
            };

            return new RetryPolicy(logger, options);
        }

        /// <summary>
        /// Creates an aggressive retry policy for critical operations
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>A configured retry policy</returns>
        public static RetryPolicy CreateAggressivePolicy(ILogger logger)
        {
            var options = new RetryPolicyOptions
            {
                MaxRetries = 5,
                InitialDelay = TimeSpan.FromMilliseconds(500),
                MaxDelay = TimeSpan.FromSeconds(10),
                Strategy = RetryStrategyType.ExponentialWithJitter,
                BackoffMultiplier = 1.5,
                JitterFactor = 0.2,
                RetryOnTimeout = true,
                RetryOnTransientErrors = true
            };

            return new RetryPolicy(logger, options);
        }

        /// <summary>
        /// Creates a conservative retry policy for non-critical operations
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>A configured retry policy</returns>
        public static RetryPolicy CreateConservativePolicy(ILogger logger)
        {
            var options = new RetryPolicyOptions
            {
                MaxRetries = 2,
                InitialDelay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(10),
                Strategy = RetryStrategyType.Linear,
                BackoffMultiplier = 1.0,
                RetryOnTimeout = false,
                RetryOnTransientErrors = true
            };

            return new RetryPolicy(logger, options);
        }
    }

    /// <summary>
    /// Exception thrown when retry policy is exhausted
    /// </summary>
    public class RetryPolicyException : Exception
    {
        /// <summary>
        /// Gets the total number of attempts made
        /// </summary>
        public int AttemptCount { get; }

        /// <summary>
        /// Initializes a new instance of the RetryPolicyException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public RetryPolicyException(string message, Exception innerException) : base(message, innerException)
        {
            AttemptCount = 1;
        }

        /// <summary>
        /// Initializes a new instance of the RetryPolicyException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        /// <param name="attemptCount">Total number of attempts</param>
        public RetryPolicyException(string message, Exception innerException, int attemptCount)
            : base(message, innerException)
        {
            AttemptCount = attemptCount;
        }
    }
}