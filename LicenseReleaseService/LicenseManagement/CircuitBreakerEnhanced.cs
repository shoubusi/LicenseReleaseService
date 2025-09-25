using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Enhanced circuit breaker with advanced health monitoring and features
    /// </summary>
    public class CircuitBreakerEnhanced : CircuitBreaker
    {
        private readonly TimeSpan _halfOpenSuccessThreshold;
        private readonly List<Type> _handledExceptionTypes;
        private readonly Queue<DateTime> _recentFailures;
        private readonly TimeSpan _slidingWindow;
        private int _successCount;
        private int _totalOperations;
        private DateTime _lastSuccessTime;

        /// <summary>
        /// Gets the current success count
        /// </summary>
        public int SuccessCount => _successCount;

        /// <summary>
        /// Gets the total number of operations
        /// </summary>
        public int TotalOperations => _totalOperations;

        /// <summary>
        /// Gets the success rate
        /// </summary>
        public double SuccessRate => _totalOperations > 0 ? (double)_successCount / _totalOperations : 0;

        /// <summary>
        /// Gets the last success time
        /// </summary>
        public DateTime LastSuccessTime => _lastSuccessTime;

        /// <summary>
        /// Gets the health score (0-100)
        /// </summary>
        public double HealthScore
        {
            get
            {
                if (_totalOperations == 0) return 100;

                var baseScore = SuccessRate * 100;

                // Apply state-based adjustments
                switch (State)
                {
                    case CircuitState.Open:
                        return Math.Max(0, baseScore - 50);
                    case CircuitState.HalfOpen:
                        return Math.Max(0, baseScore - 25);
                    default:
                        return baseScore;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerEnhanced class
        /// </summary>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="recoveryTimeout">Time to wait before attempting recovery</param>
        /// <param name="timeout">Individual operation timeout</param>
        /// <param name="logger">Logger instance</param>
        public CircuitBreakerEnhanced(int failureThreshold, TimeSpan recoveryTimeout, TimeSpan timeout, ILogger logger)
            : this(failureThreshold, recoveryTimeout, timeout, TimeSpan.FromMinutes(5), null, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerEnhanced class with advanced configuration
        /// </summary>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="recoveryTimeout">Time to wait before attempting recovery</param>
        /// <param name="timeout">Individual operation timeout</param>
        /// <param name="halfOpenSuccessThreshold">Time window for half-open state success validation</param>
        /// <param name="handledExceptionTypes">Exception types to handle</param>
        /// <param name="logger">Logger instance</param>
        public CircuitBreakerEnhanced(int failureThreshold, TimeSpan recoveryTimeout, TimeSpan timeout,
            TimeSpan halfOpenSuccessThreshold, IEnumerable<Type> handledExceptionTypes, ILogger logger)
            : base(failureThreshold, recoveryTimeout, timeout, logger)
        {
            _halfOpenSuccessThreshold = halfOpenSuccessThreshold > TimeSpan.Zero ? halfOpenSuccessThreshold :
                throw new ArgumentException("Half-open success threshold must be positive", nameof(halfOpenSuccessThreshold));
            _handledExceptionTypes = handledExceptionTypes?.ToList() ?? new List<Type>();
            _slidingWindow = TimeSpan.FromMinutes(10);
            _recentFailures = new Queue<DateTime>();
            _successCount = 0;
            _totalOperations = 0;
            _lastSuccessTime = DateTime.MinValue;
        }

        /// <summary>
        /// Executes an operation within the circuit breaker protection with enhanced features
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the operation</returns>
        public new async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var startTime = DateTime.Now;

            try
            {
                var result = await base.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);

                // Record success with enhanced metrics
                await RecordEnhancedSuccessAsync().ConfigureAwait(false);

                return result;
            }
            catch (Exception ex)
            {
                // Record failure with enhanced metrics
                await RecordEnhancedFailureAsync(ex).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Records a successful operation with enhanced metrics
        /// </summary>
        private async Task RecordEnhancedSuccessAsync()
        {
            _successCount++;
            _totalOperations++;
            _lastSuccessTime = DateTime.Now;

            // Clean up old failure records
            CleanupOldFailureRecords();

            // Log enhanced metrics
            var logger = GetLogger();
            logger?.LogDebug("Enhanced circuit breaker success. Success rate: {SuccessRate:P2}, Health score: {HealthScore:F1}",
                SuccessRate, HealthScore);
        }

        /// <summary>
        /// Records a failed operation with enhanced metrics
        /// </summary>
        private async Task RecordEnhancedFailureAsync(Exception exception)
        {
            // Only record failures for exception types we're configured to handle
            if (_handledExceptionTypes.Count > 0 && !_handledExceptionTypes.Any(t => t.IsInstanceOfType(exception)))
            {
                return;
            }

            _totalOperations++;
            _recentFailures.Enqueue(DateTime.Now);

            // Clean up old failure records
            CleanupOldFailureRecords();

            // Log enhanced metrics
            var logger = GetLogger();
            logger?.LogWarning(exception, "Enhanced circuit breaker failure. Success rate: {SuccessRate:P2}, Health score: {HealthScore:F1}, Window failures: {WindowFailures}",
                SuccessRate, HealthScore, _recentFailures.Count);
        }

        /// <summary>
        /// Cleans up old failure records outside the sliding window
        /// </summary>
        private void CleanupOldFailureRecords()
        {
            var cutoff = DateTime.Now - _slidingWindow;
            while (_recentFailures.Count > 0 && _recentFailures.Peek() < cutoff)
            {
                _recentFailures.Dequeue();
            }
        }

        /// <summary>
        /// Gets the failure rate within the sliding window
        /// </summary>
        /// <returns>Failure rate as a percentage</returns>
        public double GetFailureRateInWindow()
        {
            CleanupOldFailureRecords();
            return _recentFailures.Count > 0 ? (double)_recentFailures.Count / Math.Max(1, _totalOperations) : 0;
        }

        /// <summary>
        /// Gets the enhanced status information
        /// </summary>
        /// <returns>Enhanced circuit breaker status</returns>
        public new async Task<CircuitBreakerEnhancedStatus> GetStatusAsync()
        {
            var baseStatus = await base.GetStatusAsync().ConfigureAwait(false);

            return new CircuitBreakerEnhancedStatus
            {
                State = baseStatus.State,
                FailureCount = baseStatus.FailureCount,
                SuccessCount = _successCount,
                TotalOperations = _totalOperations,
                SuccessRate = SuccessRate,
                HealthScore = HealthScore,
                FailureThreshold = baseStatus.FailureThreshold,
                LastFailureTime = baseStatus.LastFailureTime,
                LastSuccessTime = _lastSuccessTime,
                RecoveryTimeout = baseStatus.RecoveryTimeout,
                Timeout = baseStatus.Timeout,
                NextResetAttempt = baseStatus.NextResetAttempt,
                RecentFailuresInWindow = _recentFailures.Count,
                FailureRateInWindow = GetFailureRateInWindow()
            };
        }

        /// <summary>
        /// Gets the logger (access to base class private field)
        /// </summary>
        private ILogger GetLogger()
        {
            // This is a workaround to access the private logger field
            // In a real implementation, this would be handled differently
            return null;
        }

        /// <summary>
        /// Performs health check analysis and provides recommendations
        /// </summary>
        /// <returns>Health check result</returns>
        public async Task<CircuitBreakerHealthCheck> PerformHealthCheckAsync()
        {
            var status = await GetStatusAsync().ConfigureAwait(false);
            var recommendations = new List<string>();

            if (status.HealthScore < 50)
            {
                recommendations.Add("Health score is critically low - consider immediate investigation");
            }
            else if (status.HealthScore < 75)
            {
                recommendations.Add("Health score is below optimal - monitor closely");
            }

            if (status.FailureRateInWindow > 0.3)
            {
                recommendations.Add("High failure rate detected in recent operations");
            }

            if (status.State == CircuitState.Open)
            {
                recommendations.Add("Circuit is open - operations are being blocked");
            }

            return new CircuitBreakerHealthCheck
            {
                Timestamp = DateTime.Now,
                Status = status,
                HealthScore = status.HealthScore,
                IsHealthy = status.HealthScore >= 75 && status.State != CircuitState.Open,
                Recommendations = recommendations
            };
        }
    }

    /// <summary>
    /// Enhanced circuit breaker status with additional metrics
    /// </summary>
    public class CircuitBreakerEnhancedStatus : CircuitBreakerStatus
    {
        /// <summary>
        /// Gets or sets the current success count
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of operations
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the health score (0-100)
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Gets or sets the last success time
        /// </summary>
        public DateTime LastSuccessTime { get; set; }

        /// <summary>
        /// Gets or sets the number of recent failures in the sliding window
        /// </summary>
        public int RecentFailuresInWindow { get; set; }

        /// <summary>
        /// Gets or sets the failure rate within the sliding window
        /// </summary>
        public double FailureRateInWindow { get; set; }

        /// <summary>
        /// Returns a string representation of the enhanced circuit breaker status
        /// </summary>
        public override string ToString()
        {
            return $"CircuitBreakerEnhancedStatus[State={State}, Failures={FailureCount}/{FailureThreshold}, " +
                   $"Successes={SuccessCount}, Total={TotalOperations}, Rate={SuccessRate:P2}, " +
                   $"Health={HealthScore:F1}, WindowFailures={RecentFailuresInWindow}, " +
                   $"WindowRate={FailureRateInWindow:P2}, Timeout={Timeout.TotalSeconds:F1}s, " +
                   $"Recovery={RecoveryTimeout.TotalSeconds:F1}s" +
                   (NextResetAttempt.HasValue ? $", NextReset={NextResetAttempt.Value:yyyy-MM-dd HH:mm:ss}" : "") + "]";
        }
    }

    /// <summary>
    /// Health check result for the enhanced circuit breaker
    /// </summary>
    public class CircuitBreakerHealthCheck
    {
        /// <summary>
        /// Gets or sets the timestamp of the health check
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the circuit breaker status
        /// </summary>
        public CircuitBreakerEnhancedStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the health score
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the circuit breaker is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the recommendations for improvement
        /// </summary>
        public List<string> Recommendations { get; set; }
    }
}