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
    /// Defines fallback strategy types
    /// </summary>
    public enum FallbackStrategyType
    {
        /// <summary>
        /// No fallback strategy
        /// </summary>
        None,

        /// <summary>
        /// Retry with different parameters
        /// </summary>
        RetryWithDifferentParameters,

        /// <summary>
        /// Use alternative endpoint or server
        /// </summary>
        AlternativeEndpoint,

        /// <summary>
        /// Use cached data
        /// </summary>
        CachedData,

        /// <summary>
        /// Use simplified operation
        /// </summary>
        SimplifiedOperation,

        /// <summary>
        /// Return default value
        /// </summary>
        DefaultValue,

        /// <summary>
        /// Custom fallback logic
        /// </summary>
        Custom,

        /// <summary>
        /// Chain multiple fallback strategies
        /// </summary>
        Chained
    }

    /// <summary>
    /// Defines fallback execution order
    /// </summary>
    public enum FallbackExecutionOrder
    {
        /// <summary>
        /// Execute in parallel, return first successful result
        /// </summary>
        Parallel,

        /// <summary>
        /// Execute sequentially until one succeeds
        /// </summary>
        Sequential,

        /// <summary>
        /// Execute all and combine results
        /// </summary>
        All,

        /// <summary>
        /// Execute based on priority order
        /// </summary>
        Priority
    }

    /// <summary>
    /// Represents a fallback strategy configuration
    /// </summary>
    public class FallbackStrategy
    {
        /// <summary>
        /// Gets or sets the strategy name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the strategy type
        /// </summary>
        public FallbackStrategyType Type { get; set; }

        /// <summary>
        /// Gets or sets the operation name this strategy applies to
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the priority of this strategy (lower number = higher priority)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the execution order
        /// </summary>
        public FallbackExecutionOrder ExecutionOrder { get; set; }

        /// <summary>
        /// Gets or sets the timeout for this strategy
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets whether this strategy is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the condition to use this strategy
        /// </summary>
        public Func<Exception, bool> ShouldUse { get; set; }

        /// <summary>
        /// Gets or sets the fallback operation to execute
        /// </summary>
        public Func<Task<object>> Operation { get; set; }

        /// <summary>
        /// Gets or sets the strategy-specific configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum number of retries for this strategy
        /// </summary>
        public int MaxRetries { get; set; } = 1;

        /// <summary>
        /// Gets or sets the retry delay for this strategy
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the child strategies for chained strategies
        /// </summary>
        public List<FallbackStrategy> ChildStrategies { get; set; } = new();

        /// <summary>
        /// Gets or sets the success callback
        /// </summary>
        public Action<object> OnSuccess { get; set; }

        /// <summary>
        /// Gets or sets the failure callback
        /// </summary>
        public Action<Exception> OnFailure { get; set; }
    }

    /// <summary>
    /// Represents the result of a fallback strategy execution
    /// </summary>
    public class FallbackResult
    {
        /// <summary>
        /// Gets or sets the strategy name
        /// </summary>
        public string StrategyName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the fallback was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the result value
        /// </summary>
        public object Result { get; set; }

        /// <summary>
        /// Gets or sets the execution time
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the exception if failed
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the attempt number
        /// </summary>
        public int AttemptNumber { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Manages fallback strategies for service operations
    /// </summary>
    public class FallbackStrategyManager : IDisposable
    {
        private readonly ILogger<FallbackStrategyManager> _logger;
        private readonly ConcurrentDictionary<string, List<FallbackStrategy>> _strategies;
        private readonly ConcurrentDictionary<string, DateTime> _lastStrategyUsage;
        private readonly ConcurrentDictionary<string, int> _strategySuccessCounts;
        private readonly ConcurrentDictionary<string, int> _strategyFailureCounts;
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Event raised when a fallback strategy is executed
        /// </summary>
        public event EventHandler<FallbackResult> FallbackExecuted;

        /// <summary>
        /// Initializes a new instance of the FallbackStrategyManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public FallbackStrategyManager(ILogger<FallbackStrategyManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _strategies = new ConcurrentDictionary<string, List<FallbackStrategy>>();
            _lastStrategyUsage = new ConcurrentDictionary<string, DateTime>();
            _strategySuccessCounts = new ConcurrentDictionary<string, int>();
            _strategyFailureCounts = new ConcurrentDictionary<string, int>();

            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            // Register default strategies
            RegisterDefaultStrategies();

            _logger.LogInformation("Fallback strategy manager initialized");
        }

        /// <summary>
        /// Executes an operation with fallback strategies
        /// </summary>
        /// <param name="operationName">The operation name</param>
        /// <param name="primaryOperation">The primary operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The operation result</returns>
        public async Task<T> ExecuteWithFallbackAsync<T>(
            string operationName,
            Func<Task<T>> primaryOperation,
            CancellationToken cancellationToken = default)
        {
            if (primaryOperation == null)
                throw new ArgumentNullException(nameof(primaryOperation));

            try
            {
                // Try primary operation first
                return await primaryOperation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Primary operation {Operation} failed, attempting fallback strategies", operationName);
                var fallbackResult = await ExecuteFallbackStrategiesAsync<T>(operationName, ex, cancellationToken).ConfigureAwait(false);

                if (fallbackResult.Success)
                {
                    return (T)fallbackResult.Result;
                }

                // All fallback strategies failed, rethrow the original exception
                throw new AggregateException("Primary operation and all fallback strategies failed", ex);
            }
        }

        /// <summary>
        /// Registers a fallback strategy
        /// </summary>
        /// <param name="strategy">The fallback strategy to register</param>
        public void RegisterStrategy(FallbackStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            if (string.IsNullOrEmpty(strategy.Name))
                throw new ArgumentException("Strategy name cannot be null or empty", nameof(strategy));

            if (string.IsNullOrEmpty(strategy.OperationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(strategy));

            var strategies = _strategies.GetOrAdd(strategy.OperationName, _ => new List<FallbackStrategy>());
            lock (strategies)
            {
                strategies.Add(strategy);
                strategies.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }

            _logger.LogInformation("Registered fallback strategy {Strategy} for operation {Operation}",
                strategy.Name, strategy.OperationName);
        }

        /// <summary>
        /// Removes a fallback strategy
        /// </summary>
        /// <param name="strategyName">The strategy name</param>
        /// <param name="operationName">The operation name</param>
        public void RemoveStrategy(string strategyName, string operationName)
        {
            if (_strategies.TryGetValue(operationName, out var strategies))
            {
                lock (strategies)
                {
                    var strategy = strategies.FirstOrDefault(s => s.Name == strategyName);
                    if (strategy != null)
                    {
                        strategies.Remove(strategy);
                        _logger.LogInformation("Removed fallback strategy {Strategy} for operation {Operation}",
                            strategyName, operationName);
                    }
                }
            }
        }

        /// <summary>
        /// Gets fallback strategies for an operation
        /// </summary>
        /// <param name="operationName">The operation name</param>
        /// <returns>List of fallback strategies</returns>
        public List<FallbackStrategy> GetStrategiesForOperation(string operationName)
        {
            return _strategies.TryGetValue(operationName, out var strategies)
                ? new List<FallbackStrategy>(strategies.Where(s => s.IsEnabled))
                : new List<FallbackStrategy>();
        }

        /// <summary>
        /// Gets strategy statistics
        /// </summary>
        /// <returns>Dictionary of strategy statistics</returns>
        public Dictionary<string, object> GetStrategyStatistics()
        {
            var stats = new Dictionary<string, object>();

            foreach (var strategyName in _strategySuccessCounts.Keys.Concat(_strategyFailureCounts.Keys).Distinct())
            {
                var successCount = _strategySuccessCounts.TryGetValue(strategyName, out var sc) ? sc : 0;
                var failureCount = _strategyFailureCounts.TryGetValue(strategyName, out var fc) ? fc : 0;
                var total = successCount + failureCount;

                stats[strategyName] = new
                {
                    SuccessCount = successCount,
                    FailureCount = failureCount,
                    TotalAttempts = total,
                    SuccessRate = total > 0 ? (double)successCount / total : 0,
                    LastUsed = _lastStrategyUsage.TryGetValue(strategyName, out var lastUsed) ? lastUsed : (DateTime?)null
                };
            }

            return stats;
        }

        private async Task<FallbackResult> ExecuteFallbackStrategiesAsync<T>(
            string operationName,
            Exception originalException,
            CancellationToken cancellationToken)
        {
            var strategies = GetStrategiesForOperation(operationName);
            if (!strategies.Any())
            {
                _logger.LogWarning("No fallback strategies available for operation {Operation}", operationName);
                return new FallbackResult
                {
                    StrategyName = "none",
                    Success = false,
                    Exception = originalException
                };
            }

            // Filter strategies that should be used for this exception
            var applicableStrategies = strategies
                .Where(s => s.ShouldUse?.Invoke(originalException) != false)
                .ToList();

            if (!applicableStrategies.Any())
            {
                _logger.LogDebug("No applicable fallback strategies for operation {Operation} with exception {ExceptionType}",
                    operationName, originalException.GetType().Name);
                return new FallbackResult
                {
                    StrategyName = "none",
                    Success = false,
                    Exception = originalException
                };
            }

            // Execute strategies based on execution order
            switch (applicableStrategies.First().ExecutionOrder)
            {
                case FallbackExecutionOrder.Parallel:
                    return await ExecuteStrategiesInParallelAsync<T>(applicableStrategies, cancellationToken).ConfigureAwait(false);

                case FallbackExecutionOrder.Sequential:
                    return await ExecuteStrategiesSequentiallyAsync<T>(applicableStrategies, cancellationToken).ConfigureAwait(false);

                case FallbackExecutionOrder.Priority:
                    return await ExecuteStrategiesByPriorityAsync<T>(applicableStrategies, cancellationToken).ConfigureAwait(false);

                case FallbackExecutionOrder.All:
                    return await ExecuteAllStrategiesAsync<T>(applicableStrategies, cancellationToken).ConfigureAwait(false);

                default:
                    return await ExecuteStrategiesSequentiallyAsync<T>(applicableStrategies, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<FallbackResult> ExecuteStrategiesSequentiallyAsync<T>(
            List<FallbackStrategy> strategies,
            CancellationToken cancellationToken)
        {
            foreach (var strategy in strategies)
            {
                var result = await ExecuteSingleStrategyAsync<T>(strategy, cancellationToken).ConfigureAwait(false);
                if (result.Success)
                {
                    return result;
                }
            }

            return new FallbackResult
            {
                StrategyName = "sequential",
                Success = false,
                Exception = new InvalidOperationException("All sequential fallback strategies failed")
            };
        }

        private async Task<FallbackResult> ExecuteStrategiesInParallelAsync<T>(
            List<FallbackStrategy> strategies,
            CancellationToken cancellationToken)
        {
            var tasks = strategies.Select(s => ExecuteSingleStrategyAsync<T>(s, cancellationToken)).ToList();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Return first successful result
            var firstSuccess = results.FirstOrDefault(r => r.Success);
            if (firstSuccess != null)
            {
                return firstSuccess;
            }

            return new FallbackResult
            {
                StrategyName = "parallel",
                Success = false,
                Exception = new InvalidOperationException("All parallel fallback strategies failed")
            };
        }

        private async Task<FallbackResult> ExecuteStrategiesByPriorityAsync<T>(
            List<FallbackStrategy> strategies,
            CancellationToken cancellationToken)
        {
            // Already sorted by priority from GetStrategiesForOperation
            return await ExecuteStrategiesSequentiallyAsync<T>(strategies, cancellationToken).ConfigureAwait(false);
        }

        private async Task<FallbackResult> ExecuteAllStrategiesAsync<T>(
            List<FallbackStrategy> strategies,
            CancellationToken cancellationToken)
        {
            var results = new List<FallbackResult>();
            var successCount = 0;

            foreach (var strategy in strategies)
            {
                var result = await ExecuteSingleStrategyAsync<T>(strategy, cancellationToken).ConfigureAwait(false);
                results.Add(result);

                if (result.Success)
                {
                    successCount++;
                }
            }

            return new FallbackResult
            {
                StrategyName = "all",
                Success = successCount > 0,
                Result = successCount > 0 ? results.FirstOrDefault(r => r.Success)?.Result : null,
                Metadata = new Dictionary<string, object>
                {
                    ["TotalStrategies"] = strategies.Count,
                    ["SuccessfulStrategies"] = successCount,
                    ["Results"] = results
                }
            };
        }

        private async Task<FallbackResult> ExecuteSingleStrategyAsync<T>(
            FallbackStrategy strategy,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            var attempt = 0;
            var maxAttempts = Math.Max(1, strategy.MaxRetries);

            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    _logger.LogDebug("Executing fallback strategy {Strategy} (attempt {Attempt}/{MaxAttempts})",
                        strategy.Name, attempt, maxAttempts);

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    linkedCts.CancelAfter(strategy.Timeout);

                    var result = await strategy.Operation().ConfigureAwait(false);

                    // Success
                    var fallbackResult = new FallbackResult
                    {
                        StrategyName = strategy.Name,
                        Success = true,
                        Result = result,
                        ExecutionTime = DateTime.Now - startTime,
                        AttemptNumber = attempt
                    };

                    // Record success metrics
                    _strategySuccessCounts.AddOrUpdate(strategy.Name, 1, (key, count) => count + 1);
                    _lastStrategyUsage.AddOrUpdate(strategy.Name, DateTime.Now, (key, oldValue) => DateTime.Now);

                    // Invoke success callback
                    strategy.OnSuccess?.Invoke(result);

                    // Raise event
                    OnFallbackExecuted(fallbackResult);

                    _logger.LogInformation("Fallback strategy {Strategy} succeeded on attempt {Attempt}",
                        strategy.Name, attempt);

                    return fallbackResult;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _logger.LogDebug(ex, "Fallback strategy {Strategy} failed on attempt {Attempt}, retrying in {Delay}",
                        strategy.Name, attempt, strategy.RetryDelay);

                    if (strategy.RetryDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(strategy.RetryDelay, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // All attempts failed
                    var fallbackResult = new FallbackResult
                    {
                        StrategyName = strategy.Name,
                        Success = false,
                        Exception = ex,
                        ExecutionTime = DateTime.Now - startTime,
                        AttemptNumber = attempt
                    };

                    // Record failure metrics
                    _strategyFailureCounts.AddOrUpdate(strategy.Name, 1, (key, count) => count + 1);
                    _lastStrategyUsage.AddOrUpdate(strategy.Name, DateTime.Now, (key, oldValue) => DateTime.Now);

                    // Invoke failure callback
                    strategy.OnFailure?.Invoke(ex);

                    // Raise event
                    OnFallbackExecuted(fallbackResult);

                    _logger.LogWarning(ex, "Fallback strategy {Strategy} failed after {Attempt} attempts",
                        strategy.Name, attempt);

                    return fallbackResult;
                }
            }

            // Should never reach here
            return new FallbackResult
            {
                StrategyName = strategy.Name,
                Success = false,
                Exception = new InvalidOperationException("Unexpected error in fallback execution"),
                ExecutionTime = DateTime.Now - startTime,
                AttemptNumber = attempt
            };
        }

        private void RegisterDefaultStrategies()
        {
            // License server status fallback strategies
            RegisterStrategy(new FallbackStrategy
            {
                Name = "CachedLicenseStatus",
                OperationName = "GetServerStatusAsync",
                Type = FallbackStrategyType.CachedData,
                Priority = 1,
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                ShouldUse = ex => ex is TimeoutException || ex is System.Net.Sockets.SocketException,
                Configuration = { ["CacheTimeoutMinutes"] = 30 }
            });

            RegisterStrategy(new FallbackStrategy
            {
                Name = "AlternativeLicenseServer",
                OperationName = "GetServerStatusAsync",
                Type = FallbackStrategyType.AlternativeEndpoint,
                Priority = 2,
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(10),
                ShouldUse = ex => ex is System.Net.Sockets.SocketException,
                Configuration = { ["BackupServer"] = "backup-server:27000" }
            });

            // License release fallback strategies
            RegisterStrategy(new FallbackStrategy
            {
                Name = "DelayedRelease",
                OperationName = "ReleaseLicenseAsync",
                Type = FallbackStrategyType.RetryWithDifferentParameters,
                Priority = 1,
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromMinutes(2),
                MaxRetries = 2,
                RetryDelay = TimeSpan.FromSeconds(30),
                ShouldUse = ex => ex is TimeoutException || ex is ProcessExecutionException,
                Configuration = { ["IncreasedTimeout"] = true }
            });

            RegisterStrategy(new FallbackStrategy
            {
                Name = "ReleaseQueueFallback",
                OperationName = "ReleaseLicenseAsync",
                Type = FallbackStrategyType.SimplifiedOperation,
                Priority = 2,
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(30),
                ShouldUse = ex => ex is ProcessExecutionException,
                Configuration = { ["QueueForLater"] = true }
            });
        }

        private void OnFallbackExecuted(FallbackResult result)
        {
            FallbackExecuted?.Invoke(this, result);
        }

        private void CleanupOldMetrics(object state)
        {
            var cutoff = DateTime.Now.AddHours(-24);

            // Clean up old usage metrics
            foreach (var key in _lastStrategyUsage.Keys.ToList())
            {
                if (_lastStrategyUsage.TryGetValue(key, out var lastUsed) && lastUsed < cutoff)
                {
                    _lastStrategyUsage.TryRemove(key, out _);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}