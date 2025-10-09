using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines resilience policy types
    /// </summary>
    public enum ResiliencePolicyType
    {
        /// <summary>
        /// Retry policy
        /// </summary>
        Retry,

        /// <summary>
        /// Circuit breaker policy
        /// </summary>
        CircuitBreaker,

        /// <summary>
        /// Timeout policy
        /// </summary>
        Timeout,

        /// <summary>
        /// Fallback policy
        /// </summary>
        Fallback,

        /// <summary>
        /// Bulkhead isolation policy
        /// </summary>
        Bulkhead,

        /// <summary>
        /// Cache policy
        /// </summary>
        Cache,

        /// <summary>
        /// Combined policy (multiple policies)
        /// </summary>
        Combined
    }

    /// <summary>
    /// Represents a resilience policy configuration
    /// </summary>
    public class ResiliencePolicy
    {
        /// <summary>
        /// Gets or sets the policy name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the policy type
        /// </summary>
        public ResiliencePolicyType Type { get; set; }

        /// <summary>
        /// Gets or sets the operation name this policy applies to
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets whether the policy is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the policy priority
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the policy configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Gets or sets the policy execution order
        /// </summary>
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Gets or sets the condition to apply this policy
        /// </summary>
        public Func<Exception, bool> ShouldApply { get; set; }

        /// <summary>
        /// Gets or sets the child policies for combined policies
        /// </summary>
        public List<ResiliencePolicy> ChildPolicies { get; set; } = new();
    }

    /// <summary>
    /// Represents resilience metrics
    /// </summary>
    public class ResilienceMetrics
    {
        /// <summary>
        /// Gets or sets the total number of operations
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of successful operations
        /// </summary>
        public long SuccessfulOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of failed operations
        /// </summary>
        public long FailedOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of retried operations
        /// </summary>
        public long RetriedOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of circuit breaker trips
        /// </summary>
        public long CircuitBreakerTrips { get; set; }

        /// <summary>
        /// Gets or sets the number of fallback operations
        /// </summary>
        public long FallbackOperations { get; set; }

        /// <summary>
        /// Gets or sets the average operation time
        /// </summary>
        public TimeSpan AverageOperationTime { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0;

        /// <summary>
        /// Gets or sets the failure rate
        /// </summary>
        public double FailureRate => TotalOperations > 0 ? (double)FailedOperations / TotalOperations : 0;
    }

    /// <summary>
    /// Coordinates all resilience mechanisms for service operations
    /// </summary>
    public class ServiceResilienceCoordinator : IDisposable
    {
        private readonly ILogger<ServiceResilienceCoordinator> _logger;
        private readonly List<ResiliencePolicy> _policies;
        private readonly ConcurrentDictionary<string, ResilienceMetrics> _operationMetrics;
        private readonly RetryPolicy _retryPolicy;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly GracefulDegradationManager _degradationManager;
        private readonly FallbackStrategyManager _fallbackManager;
        private readonly Timer _metricsCleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Event raised when resilience policy is applied
        /// </summary>
        public event EventHandler<ResiliencePolicy> PolicyApplied;

        /// <summary>
        /// Event raised when resilience metrics are updated
        /// </summary>
        public event EventHandler<ResilienceMetrics> MetricsUpdated;

        /// <summary>
        /// Initializes a new instance of the ServiceResilienceCoordinator class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="retryPolicy">Retry policy</param>
        /// <param name="circuitBreaker">Circuit breaker</param>
        /// <param name="degradationManager">Graceful degradation manager</param>
        /// <param name="fallbackManager">Fallback strategy manager</param>
        public ServiceResilienceCoordinator(
            ILogger<ServiceResilienceCoordinator> logger,
            RetryPolicy retryPolicy,
            CircuitBreaker circuitBreaker,
            GracefulDegradationManager degradationManager,
            FallbackStrategyManager fallbackManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _degradationManager = degradationManager ?? throw new ArgumentNullException(nameof(degradationManager));
            _fallbackManager = fallbackManager ?? throw new ArgumentNullException(nameof(fallbackManager));

            _policies = new List<ResiliencePolicy>();
            _operationMetrics = new ConcurrentDictionary<string, ResilienceMetrics>();

            // Start metrics cleanup timer
            _metricsCleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            // Register default policies
            RegisterDefaultPolicies();

            _logger.LogInformation("Service resilience coordinator initialized");
        }

        /// <summary>
        /// Executes an operation with resilience policies
        /// </summary>
        /// <param name="operationName">The operation name</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="context">Additional context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The operation result</returns>
        public async Task<T> ExecuteWithResilienceAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            object context = null,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var startTime = DateTime.Now;
            var metrics = GetOrCreateMetrics(operationName);
            var appliedPolicies = new List<ResiliencePolicy>();

            try
            {
                // Get applicable policies for this operation
                var applicablePolicies = GetApplicablePolicies(operationName)
                    .Where(p => p.IsEnabled)
                    .OrderBy(p => p.ExecutionOrder)
                    .ToList();

                // Execute with resilience policies
                var result = await ExecuteWithPoliciesAsync<T>(
                    operationName, operation, applicablePolicies, appliedPolicies, context, cancellationToken).ConfigureAwait(false);

                // Record success metrics
                UpdateMetrics(metrics, startTime, true, appliedPolicies);

                return result;
            }
            catch (Exception ex)
            {
                // Record failure metrics
                UpdateMetrics(metrics, startTime, false, appliedPolicies, ex);

                // Check if we should apply graceful degradation
                await HandleOperationFailureAsync(operationName, ex, context).ConfigureAwait(false);

                throw;
            }
        }

        /// <summary>
        /// Registers a resilience policy
        /// </summary>
        /// <param name="policy">The policy to register</param>
        public void RegisterPolicy(ResiliencePolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            if (string.IsNullOrEmpty(policy.Name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(policy));

            if (string.IsNullOrEmpty(policy.OperationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(policy));

            _policies.Add(policy);
            _logger.LogInformation("Registered resilience policy {Policy} for operation {Operation}",
                policy.Name, policy.OperationName);
        }

        /// <summary>
        /// Removes a resilience policy
        /// </summary>
        /// <param name="policyName">The policy name</param>
        /// <param name="operationName">The operation name</param>
        public void RemovePolicy(string policyName, string operationName)
        {
            var policy = _policies.FirstOrDefault(p => p.Name == policyName && p.OperationName == operationName);
            if (policy != null)
            {
                _policies.Remove(policy);
                _logger.LogInformation("Removed resilience policy {Policy} for operation {Operation}",
                    policyName, operationName);
            }
        }

        /// <summary>
        /// Gets policies for an operation
        /// </summary>
        /// <param name="operationName">The operation name</param>
        /// <returns>List of resilience policies</returns>
        public List<ResiliencePolicy> GetPoliciesForOperation(string operationName)
        {
            return _policies.Where(p => p.OperationName == operationName).ToList();
        }

        /// <summary>
        /// Gets resilience metrics for an operation
        /// </summary>
        /// <param name="operationName">The operation name</param>
        /// <returns>Resilience metrics</returns>
        public ResilienceMetrics GetMetricsForOperation(string operationName)
        {
            return _operationMetrics.TryGetValue(operationName, out var metrics) ? metrics : new ResilienceMetrics();
        }

        /// <summary>
        /// Gets overall resilience metrics
        /// </summary>
        /// <returns>Combined resilience metrics</returns>
        public ResilienceMetrics GetOverallMetrics()
        {
            var allMetrics = _operationMetrics.Values.ToList();
            if (!allMetrics.Any())
            {
                return new ResilienceMetrics();
            }

            return new ResilienceMetrics
            {
                TotalOperations = allMetrics.Sum(m => m.TotalOperations),
                SuccessfulOperations = allMetrics.Sum(m => m.SuccessfulOperations),
                FailedOperations = allMetrics.Sum(m => m.FailedOperations),
                RetriedOperations = allMetrics.Sum(m => m.RetriedOperations),
                CircuitBreakerTrips = allMetrics.Sum(m => m.CircuitBreakerTrips),
                FallbackOperations = allMetrics.Sum(m => m.FallbackOperations),
                AverageOperationTime = allMetrics.Any() ?
                    TimeSpan.FromMilliseconds(allMetrics.Average(m => m.AverageOperationTime.TotalMilliseconds)) :
                    TimeSpan.Zero
            };
        }

        private async Task<T> ExecuteWithPoliciesAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            List<ResiliencePolicy> policies,
            List<ResiliencePolicy> appliedPolicies,
            object context,
            CancellationToken cancellationToken)
        {
            var currentOperation = operation;
            Exception lastException = null;

            foreach (var policy in policies)
            {
                try
                {
                    var result = await ApplyPolicyAsync<T>(
                        operationName, currentOperation, policy, context, cancellationToken).ConfigureAwait(false);

                    if (result != null)
                    {
                        appliedPolicies.Add(policy);
                        OnPolicyApplied(policy);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogDebug(ex, "Policy {Policy} failed for operation {Operation}", policy.Name, operationName);
                }
            }

            // If all policies failed, try the original operation
            if (currentOperation == operation)
            {
                return await currentOperation().ConfigureAwait(false);
            }

            throw lastException ?? new InvalidOperationException("All resilience policies failed");
        }

        private async Task<T> ApplyPolicyAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            ResiliencePolicy policy,
            object context,
            CancellationToken cancellationToken)
        {
            switch (policy.Type)
            {
                case ResiliencePolicyType.Retry:
                    return await ApplyRetryPolicyAsync<T>(operation, policy, cancellationToken).ConfigureAwait(false);

                case ResiliencePolicyType.CircuitBreaker:
                    return await ApplyCircuitBreakerPolicyAsync<T>(operation, cancellationToken).ConfigureAwait(false);

                case ResiliencePolicyType.Timeout:
                    return await ApplyTimeoutPolicyAsync<T>(operation, policy, cancellationToken).ConfigureAwait(false);

                case ResiliencePolicyType.Fallback:
                    return await ApplyFallbackPolicyAsync<T>(operationName, operation, policy, context, cancellationToken).ConfigureAwait(false);

                case ResiliencePolicyType.Combined:
                    return await ApplyCombinedPolicyAsync<T>(operationName, operation, policy, context, cancellationToken).ConfigureAwait(false);

                default:
                    return await operation().ConfigureAwait(false);
            }
        }

        private async Task<T> ApplyRetryPolicyAsync<T>(
            Func<Task<T>> operation,
            ResiliencePolicy policy,
            CancellationToken cancellationToken)
        {
            var maxRetries = policy.Configuration.TryGetValue("MaxRetries", out var retriesObj) ? Convert.ToInt32(retriesObj) : 3;
            var delay = policy.Configuration.TryGetValue("RetryDelay", out var delayObj) ?
                TimeSpan.FromMilliseconds(Convert.ToInt64(delayObj)) : TimeSpan.FromSeconds(1);

            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
            {
                try
                {
                    var result = await operation().ConfigureAwait(false);
                    if (attempt > 1)
                    {
                        var metrics = GetOrCreateMetrics("global");
                        metrics.RetriedOperations++;
                    }
                    return result;
                }
                catch (Exception ex) when (attempt <= maxRetries)
                {
                    lastException = ex;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            throw lastException ?? new InvalidOperationException("Retry policy failed");
        }

        private async Task<T> ApplyCircuitBreakerPolicyAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken)
        {
            return await _circuitBreaker.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        private async Task<T> ApplyTimeoutPolicyAsync<T>(
            Func<Task<T>> operation,
            ResiliencePolicy policy,
            CancellationToken cancellationToken)
        {
            var timeout = policy.Configuration.TryGetValue("Timeout", out var timeoutObj) ?
                TimeSpan.FromMilliseconds(Convert.ToInt64(timeoutObj)) : TimeSpan.FromSeconds(30);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds", ex);
            }
        }

        private async Task<T> ApplyFallbackPolicyAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            ResiliencePolicy policy,
            object context,
            CancellationToken cancellationToken)
        {
            return await _fallbackManager.ExecuteWithFallbackAsync(operationName, operation, cancellationToken).ConfigureAwait(false);
        }

        private async Task<T> ApplyCombinedPolicyAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            ResiliencePolicy policy,
            object context,
            CancellationToken cancellationToken)
        {
            var childOperations = new List<Func<Task<T>>>();
            var childPolicies = policy.ChildPolicies.OrderBy(p => p.ExecutionOrder).ToList();

            foreach (var childPolicy in childPolicies)
            {
                var capturedPolicy = childPolicy;
                childOperations.Add(async () =>
                {
                    return await ApplyPolicyAsync<T>(operationName, operation, capturedPolicy, context, cancellationToken).ConfigureAwait(false);
                });
            }

            // Try each child policy in sequence
            foreach (var childOperation in childOperations)
            {
                try
                {
                    return await childOperation().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Child policy failed in combined policy");
                    continue;
                }
            }

            throw new InvalidOperationException("All child policies in combined policy failed");
        }

        private async Task HandleOperationFailureAsync(string operationName, Exception exception, object context)
        {
            // Trigger graceful degradation
            await _degradationManager.TriggerDegradationAsync(operationName, exception).ConfigureAwait(false);

            // Log the failure for resilience metrics
            _logger.LogWarning(exception, "Operation {Operation} failed after resilience policies were applied", operationName);
        }

        private List<ResiliencePolicy> GetApplicablePolicies(string operationName)
        {
            return _policies
                .Where(p => p.OperationName == operationName || p.OperationName == "*")
                .Where(p => p.ShouldApply == null || p.ShouldApply(new Exception("Dummy exception for policy check")))
                .ToList();
        }

        private ResilienceMetrics GetOrCreateMetrics(string operationName)
        {
            return _operationMetrics.GetOrAdd(operationName, _ => new ResilienceMetrics());
        }

        private void UpdateMetrics(
            ResilienceMetrics metrics,
            DateTime startTime,
            bool success,
            List<ResiliencePolicy> appliedPolicies,
            Exception exception = null)
        {
            metrics.TotalOperations++;
            if (success)
            {
                metrics.SuccessfulOperations++;
            }
            else
            {
                metrics.FailedOperations++;
            }

            // Update average operation time
            var operationTime = DateTime.Now - startTime;
            var currentAverage = metrics.AverageOperationTime.TotalMilliseconds;
            var newAverage = (currentAverage * (metrics.TotalOperations - 1) + operationTime.TotalMilliseconds) / metrics.TotalOperations;
            metrics.AverageOperationTime = TimeSpan.FromMilliseconds(newAverage);

            // Count specific policy applications
            foreach (var policy in appliedPolicies)
            {
                switch (policy.Type)
                {
                    case ResiliencePolicyType.Retry:
                        metrics.RetriedOperations++;
                        break;
                    case ResiliencePolicyType.Fallback:
                        metrics.FallbackOperations++;
                        break;
                    case ResiliencePolicyType.CircuitBreaker:
                        if (!success)
                        {
                            metrics.CircuitBreakerTrips++;
                        }
                        break;
                }
            }

            // Notify listeners
            OnMetricsUpdated(metrics);
        }

        private void OnPolicyApplied(ResiliencePolicy policy)
        {
            PolicyApplied?.Invoke(this, policy);
        }

        private void OnMetricsUpdated(ResilienceMetrics metrics)
        {
            MetricsUpdated?.Invoke(this, metrics);
        }

        private void RegisterDefaultPolicies()
        {
            // License server status policies
            RegisterPolicy(new ResiliencePolicy
            {
                Name = "LicenseServerRetry",
                OperationName = "GetServerStatusAsync",
                Type = ResiliencePolicyType.Retry,
                Priority = 1,
                ExecutionOrder = 1,
                Configuration = { ["MaxRetries"] = 3, ["RetryDelay"] = 1000 }
            });

            RegisterPolicy(new ResiliencePolicy
            {
                Name = "LicenseServerCircuitBreaker",
                OperationName = "GetServerStatusAsync",
                Type = ResiliencePolicyType.CircuitBreaker,
                Priority = 2,
                ExecutionOrder = 2
            });

            // License release policies
            RegisterPolicy(new ResiliencePolicy
            {
                Name = "LicenseReleaseRetry",
                OperationName = "ReleaseLicenseAsync",
                Type = ResiliencePolicyType.Retry,
                Priority = 1,
                ExecutionOrder = 1,
                Configuration = { ["MaxRetries"] = 2, ["RetryDelay"] = 2000 }
            });

            RegisterPolicy(new ResiliencePolicy
            {
                Name = "LicenseReleaseTimeout",
                OperationName = "ReleaseLicenseAsync",
                Type = ResiliencePolicyType.Timeout,
                Priority = 2,
                ExecutionOrder = 2,
                Configuration = { ["Timeout"] = 60000 }
            });

            RegisterPolicy(new ResiliencePolicy
            {
                Name = "LicenseReleaseFallback",
                OperationName = "ReleaseLicenseAsync",
                Type = ResiliencePolicyType.Fallback,
                Priority = 3,
                ExecutionOrder = 3
            });
        }

        private void CleanupOldMetrics(object state)
        {
            // Clean up old metrics data
            // This is a placeholder for more sophisticated cleanup logic
            _logger.LogDebug("Cleaning up old resilience metrics");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _metricsCleanupTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}