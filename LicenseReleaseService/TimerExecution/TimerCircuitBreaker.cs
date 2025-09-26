using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Implements a circuit breaker pattern specifically for timer execution operations
    /// </summary>
    public class TimerCircuitBreaker : IDisposable
    {
        private readonly ILogger<TimerCircuitBreaker> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly SemaphoreSlim _stateLock;
        private readonly object _eventLock;
        private bool _disposed;

        private CircuitBreakerState _state;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private DateTime _stateChangeTime;
        private TimeSpan _currentTimeout;
        private readonly Queue<DateTime> _failureHistory;

        #region Events

        /// <summary>
        /// Occurs when the circuit breaker state changes
        /// </summary>
        public event EventHandler<TimerCircuitBreakerEventArgs> StateChanged;

        /// <summary>
        /// Occurs when the circuit breaker trips (opens)
        /// </summary>
        public event EventHandler<TimerCircuitBreakerEventArgs> Tripped;

        /// <summary>
        /// Occurs when the circuit breaker resets (closes)
        /// </summary>
        public event EventHandler<TimerCircuitBreakerEventArgs> Reset;

        /// <summary>
        /// Occurs when the circuit breaker attempts to test if it should close
        /// </summary>
        public event EventHandler<TimerCircuitBreakerEventArgs> AttemptReset;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current circuit breaker state
        /// </summary>
        public CircuitBreakerState State => _state;

        /// <summary>
        /// Gets the current failure count
        /// </summary>
        public int FailureCount => _failureCount;

        /// <summary>
        /// Gets the last failure time
        /// </summary>
        public DateTime LastFailureTime => _lastFailureTime;

        /// <summary>
        /// Gets the time when the state last changed
        /// </summary>
        public DateTime StateChangeTime => _stateChangeTime;

        /// <summary>
        /// Gets the current timeout period
        /// </summary>
        public TimeSpan CurrentTimeout => _currentTimeout;

        /// <summary>
        /// Gets the failure threshold
        /// </summary>
        public int FailureThreshold => _options.MaxConsecutiveErrors;

        /// <summary>
        /// Gets the base cooldown period
        /// </summary>
        public TimeSpan CooldownPeriod => _options.CircuitBreakerCooldown;

        /// <summary>
        /// Gets a value indicating whether the circuit is allowing operations
        /// </summary>
        public bool IsAllowingOperations => _state != CircuitBreakerState.Open;

        /// <summary>
        /// Gets a value indicating whether the circuit is in failure state
        /// </summary>
        public bool IsInFailureState => _state == CircuitBreakerState.Open;

        /// <summary>
        /// Gets the estimated time when the circuit breaker will attempt to reset
        /// </summary>
        public DateTime? EstimatedResetTime =>
            _state == CircuitBreakerState.Open ? _lastFailureTime.Add(_currentTimeout) : (DateTime?)null;

        /// <summary>
        /// Gets the time remaining until the next reset attempt
        /// </summary>
        public TimeSpan? TimeUntilReset =>
            EstimatedResetTime.HasValue ? EstimatedResetTime.Value - DateTime.UtcNow : (TimeSpan?)null;

        #endregion

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreaker class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The timer execution options</param>
        public TimerCircuitBreaker(ILogger<TimerCircuitBreaker> logger, TimerExecutionOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _stateLock = new SemaphoreSlim(1, 1);
            _eventLock = new object();
            _failureHistory = new Queue<DateTime>();

            ResetState();
        }

        /// <summary>
        /// Records a successful operation
        /// </summary>
        /// <param name="operationId">Optional operation identifier</param>
        public async Task RecordSuccessAsync(Guid? operationId = null)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var previousState = _state;

                // Reset failure count on success
                _failureCount = 0;
                _failureHistory.Clear();

                // If we're in half-open state and succeed, close the circuit
                if (_state == CircuitBreakerState.HalfOpen)
                {
                    await ChangeStateAsync(CircuitBreakerState.Closed, "Operation succeeded in half-open state", operationId).ConfigureAwait(false);
                    _currentTimeout = _options.CircuitBreakerCooldown; // Reset timeout
                }

                _logger.LogDebug("Recorded successful operation. State: {State}, Failures: {Count}",
                    _state, _failureCount);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Records a failed operation
        /// </summary>
        /// <param name="exception">The exception that caused the failure</param>
        /// <param name="operationId">Optional operation identifier</param>
        public async Task RecordFailureAsync(Exception exception, Guid? operationId = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var previousState = _state;

                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;
                _failureHistory.Enqueue(_lastFailureTime);

                // Keep only recent failures (within the last hour)
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                while (_failureHistory.Count > 0 && _failureHistory.Peek() < cutoffTime)
                {
                    _failureHistory.Dequeue();
                }

                _logger.LogWarning(exception, "Recorded operation failure. State: {State}, Failures: {Count}/{Threshold}",
                    _state, _failureCount, _options.MaxConsecutiveErrors);

                // Check if we should trip the circuit breaker
                if (_failureCount >= _options.MaxConsecutiveErrors)
                {
                    await TripCircuitAsync(exception, operationId).ConfigureAwait(false);
                }
                else if (_state == CircuitBreakerState.HalfOpen)
                {
                    // If we fail in half-open state, open the circuit again
                    await ChangeStateAsync(CircuitBreakerState.Open, "Operation failed in half-open state", operationId).ConfigureAwait(false);
                    IncreaseTimeout();
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Attempts to execute an operation within circuit breaker protection
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationId">Optional operation identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the operation</returns>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Guid? operationId = null, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Check if we should attempt to reset from open state
                if (_state == CircuitBreakerState.Open)
                {
                    if (ShouldAttemptReset())
                    {
                        await AttemptResetAsync(operationId).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new TimerCircuitBreakerOpenException("Circuit breaker is open", _failureCount, _options.MaxConsecutiveErrors, EstimatedResetTime);
                    }
                }

                // Execute the operation
                try
                {
                    var result = await operation().ConfigureAwait(false);
                    await RecordSuccessAsync(operationId).ConfigureAwait(false);
                    return result;
                }
                catch (Exception ex)
                {
                    await RecordFailureAsync(ex, operationId).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Attempts to execute an operation within circuit breaker protection (void return)
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationId">Optional operation identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ExecuteAsync(Func<Task> operation, Guid? operationId = null, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await ExecuteAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            }, operationId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Forces the circuit breaker into the open state
        /// </summary>
        /// <param name="reason">The reason for forcing the circuit open</param>
        /// <param name="operationId">Optional operation identifier</param>
        public async Task ForceOpenAsync(string reason, Guid? operationId = null)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != CircuitBreakerState.Open)
                {
                    _failureCount = _options.MaxConsecutiveErrors;
                    _lastFailureTime = DateTime.UtcNow;
                    await ChangeStateAsync(CircuitBreakerState.Open, reason, operationId).ConfigureAwait(false);
                    _currentTimeout = _options.CircuitBreakerCooldown;
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Resets the circuit breaker to closed state
        /// </summary>
        /// <param name="reason">The reason for resetting</param>
        /// <param name="operationId">Optional operation identifier</param>
        public async Task ResetAsync(string reason = "Manual reset", Guid? operationId = null)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != CircuitBreakerState.Closed)
                {
                    await ChangeStateAsync(CircuitBreakerState.Closed, reason, operationId).ConfigureAwait(false);
                    ResetState();
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Gets the current status of the circuit breaker
        /// </summary>
        /// <returns>Circuit breaker status</returns>
        public async Task<TimerCircuitBreakerStatus> GetStatusAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return new TimerCircuitBreakerStatus
                {
                    State = _state,
                    FailureCount = _failureCount,
                    FailureThreshold = _options.MaxConsecutiveErrors,
                    LastFailureTime = _lastFailureTime,
                    StateChangeTime = _stateChangeTime,
                    CurrentTimeout = _currentTimeout,
                    CooldownPeriod = _options.CircuitBreakerCooldown,
                    EstimatedResetTime = EstimatedResetTime,
                    TimeUntilReset = TimeUntilReset,
                    IsAllowingOperations = IsAllowingOperations,
                    IsInFailureState = IsInFailureState,
                    RecentFailures = _failureHistory.ToList()
                };
            }
            finally
            {
                _stateLock.Release();
            }
        }

        #region Private Methods

        /// <summary>
        /// Trips the circuit breaker (opens it)
        /// </summary>
        private async Task TripCircuitAsync(Exception exception, Guid? operationId)
        {
            var previousState = _state;
            var reason = $"Circuit breaker tripped after {_failureCount} consecutive failures: {exception.Message}";

            await ChangeStateAsync(CircuitBreakerState.Open, reason, operationId).ConfigureAwait(false);
            IncreaseTimeout();

            _logger.LogWarning("Circuit breaker tripped: {Reason}", reason);
        }

        /// <summary>
        /// Increases the timeout period (exponential backoff)
        /// </summary>
        private void IncreaseTimeout()
        {
            // Exponential backoff with a maximum of 1 hour
            _currentTimeout = TimeSpan.FromTicks(Math.Min(
                _currentTimeout.Ticks * 2,
                TimeSpan.FromHours(1).Ticks
            ));

            _logger.LogDebug("Increased circuit breaker timeout to {Timeout}", _currentTimeout);
        }

        /// <summary>
        /// Determines if the circuit breaker should attempt to reset
        /// </summary>
        private bool ShouldAttemptReset()
        {
            return DateTime.UtcNow - _lastFailureTime >= _currentTimeout;
        }

        /// <summary>
        /// Attempts to reset the circuit breaker from open to half-open state
        /// </summary>
        private async Task AttemptResetAsync(Guid? operationId)
        {
            await ChangeStateAsync(CircuitBreakerState.HalfOpen, "Attempting to reset circuit breaker", operationId).ConfigureAwait(false);

            _logger.LogInformation("Circuit breaker attempting reset from open to half-open state");

            // Notify about the reset attempt
            OnAttemptReset(new TimerCircuitBreakerEventArgs(
                CircuitBreakerState.Open,
                CircuitBreakerState.HalfOpen,
                "Attempting reset after cooldown period",
                _failureCount,
                _options.MaxConsecutiveErrors
            ));
        }

        /// <summary>
        /// Changes the circuit breaker state and notifies listeners
        /// </summary>
        private async Task ChangeStateAsync(CircuitBreakerState newState, string reason, Guid? operationId)
        {
            if (_state == newState)
                return;

            var previousState = _state;
            _state = newState;
            _stateChangeTime = DateTime.UtcNow;

            var eventArgs = new TimerCircuitBreakerEventArgs(
                previousState,
                newState,
                reason,
                _failureCount,
                _options.MaxConsecutiveErrors
            );

            // Set additional properties
            eventArgs.CooldownPeriod = _options.CircuitBreakerCooldown;
            eventArgs.EstimatedResetTime = EstimatedResetTime;

            // Notify state change
            await NotifyStateChangedAsync(eventArgs).ConfigureAwait(false);

            // Notify specific events
            if (previousState != CircuitBreakerState.Open && newState == CircuitBreakerState.Open)
            {
                await NotifyTrippedAsync(eventArgs).ConfigureAwait(false);
            }
            else if (previousState != CircuitBreakerState.Closed && newState == CircuitBreakerState.Closed)
            {
                await NotifyResetAsync(eventArgs).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resets the circuit breaker state to initial values
        /// </summary>
        private void ResetState()
        {
            _state = CircuitBreakerState.Closed;
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
            _stateChangeTime = DateTime.UtcNow;
            _currentTimeout = _options.CircuitBreakerCooldown;
            _failureHistory.Clear();
        }

        /// <summary>
        /// Notifies listeners of state changes
        /// </summary>
        private async Task NotifyStateChangedAsync(TimerCircuitBreakerEventArgs eventArgs)
        {
            // Create a copy of the event to avoid race conditions
            var handler = StateChanged;
            if (handler != null)
            {
                await Task.Run(() =>
                {
                    lock (_eventLock)
                    {
                        handler.Invoke(this, eventArgs);
                    }
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Notifies listeners when the circuit breaker trips
        /// </summary>
        private async Task NotifyTrippedAsync(TimerCircuitBreakerEventArgs eventArgs)
        {
            var handler = Tripped;
            if (handler != null)
            {
                await Task.Run(() =>
                {
                    lock (_eventLock)
                    {
                        handler.Invoke(this, eventArgs);
                    }
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Notifies listeners when the circuit breaker resets
        /// </summary>
        private async Task NotifyResetAsync(TimerCircuitBreakerEventArgs eventArgs)
        {
            var handler = Reset;
            if (handler != null)
            {
                await Task.Run(() =>
                {
                    lock (_eventLock)
                    {
                        handler.Invoke(this, eventArgs);
                    }
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Notifies listeners of reset attempts
        /// </summary>
        private void OnAttemptReset(TimerCircuitBreakerEventArgs eventArgs)
        {
            var handler = AttemptReset;
            if (handler != null)
            {
                lock (_eventLock)
                {
                    handler.Invoke(this, eventArgs);
                }
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the circuit breaker
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _stateLock?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerCircuitBreaker()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Represents the status of the timer circuit breaker
    /// </summary>
    public class TimerCircuitBreakerStatus
    {
        /// <summary>
        /// Gets or sets the current circuit state
        /// </summary>
        public CircuitBreakerState State { get; set; }

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
        /// Gets or sets the time when the state last changed
        /// </summary>
        public DateTime StateChangeTime { get; set; }

        /// <summary>
        /// Gets or sets the current timeout period
        /// </summary>
        public TimeSpan CurrentTimeout { get; set; }

        /// <summary>
        /// Gets or sets the cooldown period
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; }

        /// <summary>
        /// Gets or sets the estimated reset time
        /// </summary>
        public DateTime? EstimatedResetTime { get; set; }

        /// <summary>
        /// Gets or sets the time remaining until reset
        /// </summary>
        public TimeSpan? TimeUntilReset { get; set; }

        /// <summary>
        /// Gets or sets whether operations are allowed
        /// </summary>
        public bool IsAllowingOperations { get; set; }

        /// <summary>
        /// Gets or sets whether the circuit is in failure state
        /// </summary>
        public bool IsInFailureState { get; set; }

        /// <summary>
        /// Gets or sets the recent failure history
        /// </summary>
        public List<DateTime> RecentFailures { get; set; }

        /// <summary>
        /// Gets a value indicating whether the circuit breaker is healthy
        /// </summary>
        public bool IsHealthy => State == CircuitBreakerState.Closed && FailureCount == 0;

        /// <summary>
        /// Gets a value indicating whether the circuit breaker is degraded
        /// </summary>
        public bool IsDegraded => State != CircuitBreakerState.Closed || FailureCount > 0;

        /// <summary>
        /// Gets the health score (0-100)
        /// </summary>
        public int HealthScore
        {
            get
            {
                if (IsHealthy)
                    return 100;

                if (IsInFailureState)
                    return Math.Max(0, 100 - (FailureCount * 20));

                // Degraded but not failed
                return Math.Max(50, 100 - (FailureCount * 10));
            }
        }

        /// <summary>
        /// Returns a string representation of the circuit breaker status
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"TimerCircuitBreakerStatus[State={State}, Failures={FailureCount}/{FailureThreshold}, " +
                   $"Timeout={CurrentTimeout.TotalSeconds:F1}s, Health={HealthScore}, " +
                   $"ResetTime={EstimatedResetTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}]";
        }
    }

    /// <summary>
    /// Exception thrown when the timer circuit breaker is open
    /// </summary>
    public class TimerCircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Gets the current failure count
        /// </summary>
        public int FailureCount { get; }

        /// <summary>
        /// Gets the failure threshold
        /// </summary>
        public int FailureThreshold { get; }

        /// <summary>
        /// Gets the estimated reset time
        /// </summary>
        public DateTime? EstimatedResetTime { get; }

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreakerOpenException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="failureCount">Current failure count</param>
        /// <param name="failureThreshold">Failure threshold</param>
        /// <param name="estimatedResetTime">Estimated reset time</param>
        public TimerCircuitBreakerOpenException(string message, int failureCount, int failureThreshold, DateTime? estimatedResetTime)
            : base(message)
        {
            FailureCount = failureCount;
            FailureThreshold = failureThreshold;
            EstimatedResetTime = estimatedResetTime;
        }

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreakerOpenException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        /// <param name="failureCount">Current failure count</param>
        /// <param name="failureThreshold">Failure threshold</param>
        /// <param name="estimatedResetTime">Estimated reset time</param>
        public TimerCircuitBreakerOpenException(string message, Exception innerException, int failureCount, int failureThreshold, DateTime? estimatedResetTime)
            : base(message, innerException)
        {
            FailureCount = failureCount;
            FailureThreshold = failureThreshold;
            EstimatedResetTime = estimatedResetTime;
        }
    }
}