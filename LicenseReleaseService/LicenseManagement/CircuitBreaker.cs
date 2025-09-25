using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Implements the circuit breaker pattern for improving system resilience
    /// </summary>
    public class CircuitBreaker : IDisposable
    {
        private readonly ILogger _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _recoveryTimeout;
        private readonly TimeSpan _timeout;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitState _state;
        private readonly SemaphoreSlim _stateLock;
        private bool _disposed;

        /// <summary>
        /// Defines the circuit breaker states
        /// </summary>
        public enum CircuitState
        {
            /// <summary>
            /// Circuit is closed, operations are allowed
            /// </summary>
            Closed,

            /// <summary>
            /// Circuit is open, operations are blocked
            /// </summary>
            Open,

            /// <summary>
            /// Circuit is half-open, testing if operations should be allowed
            /// </summary>
            HalfOpen
        }

        /// <summary>
        /// Gets the current circuit state
        /// </summary>
        public CircuitState State => _state;

        /// <summary>
        /// Gets the current failure count
        /// </summary>
        public int FailureCount => _failureCount;

        /// <summary>
        /// Initializes a new instance of the CircuitBreaker class
        /// </summary>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="recoveryTimeout">Time to wait before attempting recovery</param>
        /// <param name="timeout">Individual operation timeout</param>
        /// <param name="logger">Logger instance</param>
        public CircuitBreaker(int failureThreshold, TimeSpan recoveryTimeout, TimeSpan timeout, ILogger logger)
        {
            _failureThreshold = failureThreshold > 0 ? failureThreshold : throw new ArgumentException("Failure threshold must be positive", nameof(failureThreshold));
            _recoveryTimeout = recoveryTimeout > TimeSpan.Zero ? recoveryTimeout : throw new ArgumentException("Recovery timeout must be positive", nameof(recoveryTimeout));
            _timeout = timeout > TimeSpan.Zero ? timeout : throw new ArgumentException("Timeout must be positive", nameof(timeout));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _state = CircuitState.Closed;
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
            _stateLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Executes an operation within the circuit breaker protection
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the operation</returns>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                if (_state == CircuitState.Open)
                {
                    if (ShouldAttemptReset())
                    {
                        _logger.LogInformation("Circuit breaker transitioning from Open to HalfOpen state");
                        _state = CircuitState.HalfOpen;
                    }
                    else
                    {
                        _logger.LogWarning("Circuit breaker is open, blocking operation");
                        throw new CircuitBreakerOpenException("Circuit breaker is open");
                    }
                }

                try
                {
                    var result = await ExecuteWithTimeoutAsync(operation, cancellationToken);
                    RecordSuccess();
                    return result;
                }
                catch (Exception ex)
                {
                    await RecordFailureAsync(ex);
                    throw;
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Executes an operation with timeout protection
        /// </summary>
        private async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_timeout);

            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Operation timed out after {Timeout}", _timeout);
                throw new TimeoutException($"Operation timed out after {_timeout.TotalSeconds:F1} seconds", ex);
            }
        }

        /// <summary>
        /// Records a successful operation
        /// </summary>
        private void RecordSuccess()
        {
            _failureCount = 0;
            if (_state == CircuitState.HalfOpen)
            {
                _logger.LogInformation("Circuit breaker transitioning from HalfOpen to Closed state");
                _state = CircuitState.Closed;
            }
        }

        /// <summary>
        /// Records a failed operation
        /// </summary>
        private async Task RecordFailureAsync(Exception exception)
        {
            _failureCount++;
            _lastFailureTime = DateTime.Now;

            _logger.LogWarning(exception, "Operation failed. Failure count: {Count}, Threshold: {Threshold}",
                _failureCount, _failureThreshold);

            if (_failureCount >= _failureThreshold)
            {
                _logger.LogWarning("Circuit breaker transitioning to Open state after {Count} failures", _failureCount);
                _state = CircuitState.Open;
            }
        }

        /// <summary>
        /// Determines if the circuit should attempt to reset
        /// </summary>
        private bool ShouldAttemptReset()
        {
            return DateTime.Now - _lastFailureTime >= _recoveryTimeout;
        }

        /// <summary>
        /// Forces the circuit breaker into the open state
        /// </summary>
        public async Task ForceOpenAsync()
        {
            await _stateLock.WaitAsync();
            try
            {
                _state = CircuitState.Open;
                _failureCount = _failureThreshold;
                _lastFailureTime = DateTime.Now;
                _logger.LogWarning("Circuit breaker forced to Open state");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Resets the circuit breaker to closed state
        /// </summary>
        public async Task ResetAsync()
        {
            await _stateLock.WaitAsync();
            try
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                _lastFailureTime = DateTime.MinValue;
                _logger.LogInformation("Circuit breaker reset to Closed state");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Gets the current status information
        /// </summary>
        public async Task<CircuitBreakerStatus> GetStatusAsync()
        {
            await _stateLock.WaitAsync();
            try
            {
                return new CircuitBreakerStatus
                {
                    State = _state,
                    FailureCount = _failureCount,
                    FailureThreshold = _failureThreshold,
                    LastFailureTime = _lastFailureTime,
                    RecoveryTimeout = _recoveryTimeout,
                    Timeout = _timeout,
                    NextResetAttempt = _state == CircuitState.Open ? _lastFailureTime.Add(_recoveryTimeout) : (DateTime?)null
                };
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _stateLock?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the status of the circuit breaker
    /// </summary>
    public class CircuitBreakerStatus
    {
        /// <summary>
        /// Gets or sets the current circuit state
        /// </summary>
        public CircuitBreaker.CircuitState State { get; set; }

        /// <summary>
        /// Gets or sets the current failure count
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the failure threshold
        /// </summary>
        public int FailureThreshold { get; set; }

        /// <summary>
        /// Gets or sets the last failure time
        /// </summary>
        public DateTime LastFailureTime { get; set; }

        /// <summary>
        /// Gets or sets the recovery timeout
        /// </summary>
        public TimeSpan RecoveryTimeout { get; set; }

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets the next reset attempt time
        /// </summary>
        public DateTime? NextResetAttempt { get; set; }

        /// <summary>
        /// Gets a value indicating whether the circuit is allowing operations
        /// </summary>
        public bool IsAllowingOperations => State != CircuitBreaker.CircuitState.Open;

        /// <summary>
        /// Gets a value indicating whether the circuit is in failure state
        /// </summary>
        public bool IsInFailureState => State == CircuitBreaker.CircuitState.Open;

        /// <summary>
        /// Returns a string representation of the circuit breaker status
        /// </summary>
        public override string ToString()
        {
            return $"CircuitBreakerStatus[State={State}, Failures={FailureCount}/{FailureThreshold}, " +
                   $"Timeout={Timeout.TotalSeconds:F1}s, Recovery={RecoveryTimeout.TotalSeconds:F1}s" +
                   (NextResetAttempt.HasValue ? $", NextReset={NextResetAttempt.Value:yyyy-MM-dd HH:mm:ss}" : "") + "]";
        }
    }

    /// <summary>
    /// Exception thrown when the circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class
        /// </summary>
        public CircuitBreakerOpenException() : base("Circuit breaker is open")
        {
        }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class
        /// </summary>
        /// <param name="message">Exception message</param>
        public CircuitBreakerOpenException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}