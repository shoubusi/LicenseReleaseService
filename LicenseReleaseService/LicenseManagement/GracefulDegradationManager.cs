using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines degradation levels for service operations
    /// </summary>
    public enum DegradationLevel
    {
        /// <summary>
        /// Full functionality available
        /// </summary>
        Full,

        /// <summary>
        /// Minor functionality limitations
        /// </summary>
        Minimal,

        /// <summary>
        /// Significant functionality limitations
        /// </summary>
        Moderate,

        /// <summary>
        /// Critical functionality only
        /// </summary>
        Severe,

        /// <summary>
        /// Basic emergency operations only
        /// </summary>
        Emergency
    }

    /// <summary>
    /// Defines the priority of operations during degradation
    /// </summary>
    public enum OperationPriority
    {
        /// <summary>
        /// Critical operations that must always work
        /// </summary>
        Critical = 1,

        /// <summary>
        /// High priority operations
        /// </summary>
        High = 2,

        /// <summary>
        /// Normal priority operations
        /// </summary>
        Normal = 3,

        /// <summary>
        /// Low priority operations
        /// </summary>
        Low = 4,

        /// <summary>
        /// Optional operations that can be skipped
        /// </summary>
        Optional = 5
    }

    /// <summary>
    /// Represents a degradation rule for operations
    /// </summary>
    public class DegradationRule
    {
        /// <summary>
        /// Gets or sets the rule name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the operation name this rule applies to
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public OperationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the minimum degradation level required for this operation
        /// </summary>
        public DegradationLevel MinimumLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum degradation level at which this operation is allowed
        /// </summary>
        public DegradationLevel MaximumLevel { get; set; }

        /// <summary>
        /// Gets or sets the timeout multiplier for this operation during degradation
        /// </summary>
        public double TimeoutMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets whether this operation should be skipped during degradation
        /// </summary>
        public bool SkipDuringDegradation { get; set; }

        /// <summary>
        /// Gets or sets the fallback operation to use during degradation
        /// </summary>
        public Func<Task<object>> FallbackOperation { get; set; }

        /// <summary>
        /// Gets or sets the condition to check if degradation should apply
        /// </summary>
        public Func<Exception, bool> ShouldDegrade { get; set; }

        /// <summary>
        /// Gets or sets additional configuration for this rule
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Represents the current degradation state
    /// </summary>
    public class DegradationState
    {
        /// <summary>
        /// Gets or sets the current degradation level
        /// </summary>
        public DegradationLevel CurrentLevel { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the degradation started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the degradation was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the reason for degradation
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the affected components
        /// </summary>
        public List<string> AffectedComponents { get; set; } = new();

        /// <summary>
        /// Gets or sets the recovery actions being taken
        /// </summary>
        public List<string> RecoveryActions { get; set; } = new();

        /// <summary>
        /// Gets or sets the degradation score (0-100, higher is worse)
        /// </summary>
        public double DegradationScore { get; set; }

        /// <summary>
        /// Gets or sets the estimated time to recovery
        /// </summary>
        public TimeSpan? EstimatedRecoveryTime { get; set; }

        /// <summary>
        /// Gets a value indicating whether the system is currently degraded
        /// </summary>
        public bool IsDegraded => CurrentLevel != DegradationLevel.Full;

        /// <summary>
        /// Gets a value indicating whether the degradation is critical
        /// </summary>
        public bool IsCritical => CurrentLevel >= DegradationLevel.Severe;
    }

    /// <summary>
    /// Manages graceful degradation of service operations during failures
    /// </summary>
    public class GracefulDegradationManager : IDisposable
    {
        private readonly ILogger<GracefulDegradationManager> _logger;
        private readonly List<DegradationRule> _degradationRules;
        private readonly ConcurrentDictionary<string, DegradationState> _componentStates;
        private readonly DegradationState _globalState;
        private readonly Timer _recoveryTimer;
        private readonly object _stateLock;
        private readonly TimeSpan _recoveryCheckInterval;
        private bool _disposed;

        /// <summary>
        /// Event raised when degradation level changes
        /// </summary>
        public event EventHandler<DegradationState> DegradationLevelChanged;

        /// <summary>
        /// Gets the current global degradation state
        /// </summary>
        public DegradationState GlobalState => _globalState;

        /// <summary>
        /// Initializes a new instance of the GracefulDegradationManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="rules">Degradation rules</param>
        public GracefulDegradationManager(
            ILogger<GracefulDegradationManager> logger,
            IEnumerable<DegradationRule> rules = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _degradationRules = rules?.ToList() ?? GetDefaultRules();
            _componentStates = new ConcurrentDictionary<string, DegradationState>();
            _stateLock = new object();
            _recoveryCheckInterval = TimeSpan.FromMinutes(1);

            _globalState = new DegradationState
            {
                CurrentLevel = DegradationLevel.Full,
                StartedAt = DateTime.Now,
                LastUpdated = DateTime.Now,
                DegradationScore = 0
            };

            // Start recovery monitoring timer
            _recoveryTimer = new Timer(CheckRecoveryStatus, null, _recoveryCheckInterval, _recoveryCheckInterval);

            _logger.LogInformation("Graceful degradation manager initialized with {RuleCount} rules", _degradationRules.Count);
        }

        /// <summary>
        /// Executes an operation with graceful degradation
        /// </summary>
        /// <param name="operationName">The operation name</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="component">The component name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The operation result</returns>
        public async Task<T> ExecuteWithDegradationAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            string component = "default",
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var rule = GetDegradationRule(operationName, component);
            var currentDegradation = GetCurrentDegradationLevel(component);

            // Check if operation should be allowed at current degradation level
            if (rule != null && !ShouldAllowOperation(rule, currentDegradation))
            {
                if (rule.SkipDuringDegradation)
                {
                    _logger.LogInformation("Skipping operation {Operation} for component {Component} due to degradation level {Level}",
                        operationName, component, currentDegradation);
                    return default;
                }

                if (rule.FallbackOperation != null)
                {
                    _logger.LogInformation("Using fallback operation for {Operation} due to degradation level {Level}",
                        operationName, currentDegradation);
                    try
                    {
                        var fallbackResult = await rule.FallbackOperation().ConfigureAwait(false);
                        return (T)fallbackResult;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fallback operation failed for {Operation}", operationName);
                        return default;
                    }
                }
            }

            // Apply timeout multiplier if degraded
            var timeout = rule?.TimeoutMultiplier > 1.0 && currentDegradation != DegradationLevel.Full
                ? TimeSpan.FromMilliseconds((rule.TimeoutMultiplier - 1.0) * 1000)
                : (TimeSpan?)null;

            try
            {
                if (timeout.HasValue)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(timeout.Value);
                    return await operation().ConfigureAwait(false);
                }
                else
                {
                    return await operation().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Check if this exception should trigger degradation
                if (rule?.ShouldDegrade?.Invoke(ex) == true)
                {
                    await TriggerDegradationAsync(component, ex, rule.Priority).ConfigureAwait(false);
                }

                // If we have a fallback, try it
                if (rule?.FallbackOperation != null)
                {
                    _logger.LogInformation("Exception occurred, using fallback operation for {Operation}", operationName);
                    try
                    {
                        var fallbackResult = await rule.FallbackOperation().ConfigureAwait(false);
                        return (T)fallbackResult;
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Fallback operation failed for {Operation}", operationName);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Triggers degradation for a component
        /// </summary>
        /// <param name="component">The component name</param>
        /// <param name="exception">The exception that triggered degradation</param>
        /// <param name="priority">The operation priority</param>
        public async Task TriggerDegradationAsync(string component, Exception exception, OperationPriority priority = OperationPriority.Normal)
        {
            lock (_stateLock)
            {
                var currentState = _componentStates.GetOrAdd(component, key => new DegradationState
                {
                    CurrentLevel = DegradationLevel.Full,
                    StartedAt = DateTime.Now,
                    LastUpdated = DateTime.Now,
                    DegradationScore = 0
                });

                // Calculate new degradation level based on exception and priority
                var newLevel = CalculateDegradationLevel(exception, priority, currentState.CurrentLevel);

                if (newLevel > currentState.CurrentLevel)
                {
                    currentState.CurrentLevel = newLevel;
                    currentState.LastUpdated = DateTime.Now;
                    currentState.Reason = exception.Message;
                    currentState.AffectedComponents.Add(component);

                    if (currentState.StartedAt == DateTime.MinValue)
                    {
                        currentState.StartedAt = DateTime.Now;
                    }

                    _logger.LogWarning("Degradation level for component {Component} increased to {Level}: {Reason}",
                        component, newLevel, exception.Message);

                    // Update global state
                    UpdateGlobalDegradationState();
                }
            }

            // Notify listeners
            OnDegradationLevelChanged();
        }

        /// <summary>
        /// Recovers a component from degradation
        /// </summary>
        /// <param name="component">The component name</param>
        /// <param name="reason">The reason for recovery</param>
        public async Task RecoverComponentAsync(string component, string reason = "Manual recovery")
        {
            lock (_stateLock)
            {
                if (_componentStates.TryRemove(component, out var state))
                {
                    _logger.LogInformation("Component {Component} recovered from degradation: {Reason}", component, reason);
                }

                // Update global state
                UpdateGlobalDegradationState();
            }

            // Notify listeners
            OnDegradationLevelChanged();
        }

        /// <summary>
        /// Gets the current degradation level for a component
        /// </summary>
        /// <param name="component">The component name</param>
        /// <returns>Current degradation level</returns>
        public DegradationLevel GetCurrentDegradationLevel(string component = "global")
        {
            if (component == "global")
            {
                return _globalState.CurrentLevel;
            }

            return _componentStates.TryGetValue(component, out var state) ? state.CurrentLevel : DegradationLevel.Full;
        }

        /// <summary>
        /// Gets all component degradation states
        /// </summary>
        /// <returns>Dictionary of component states</returns>
        public Dictionary<string, DegradationState> GetComponentStates()
        {
            return new Dictionary<string, DegradationState>(_componentStates);
        }

        /// <summary>
        /// Adds a degradation rule
        /// </summary>
        /// <param name="rule">The degradation rule to add</param>
        public void AddDegradationRule(DegradationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            _degradationRules.Add(rule);
            _logger.LogDebug("Added degradation rule for operation {Operation}", rule.OperationName);
        }

        /// <summary>
        /// Removes a degradation rule
        /// </summary>
        /// <param name="operationName">The operation name</param>
        public void RemoveDegradationRule(string operationName)
        {
            var rule = _degradationRules.FirstOrDefault(r => r.OperationName == operationName);
            if (rule != null)
            {
                _degradationRules.Remove(rule);
                _logger.LogDebug("Removed degradation rule for operation {Operation}", operationName);
            }
        }

        private DegradationRule GetDegradationRule(string operationName, string component)
        {
            return _degradationRules.FirstOrDefault(r => r.OperationName == operationName);
        }

        private bool ShouldAllowOperation(DegradationRule rule, DegradationLevel currentLevel)
        {
            return currentLevel >= rule.MinimumLevel && currentLevel <= rule.MaximumLevel;
        }

        private DegradationLevel CalculateDegradationLevel(Exception exception, OperationPriority priority, DegradationLevel currentLevel)
        {
            // Base severity from exception type
            var severity = GetExceptionSeverity(exception);

            // Adjust based on priority
            var adjustment = priority switch
            {
                OperationPriority.Critical => 0,
                OperationPriority.High => 1,
                OperationPriority.Normal => 2,
                OperationPriority.Low => 3,
                OperationPriority.Optional => 4,
                _ => 2
            };

            var newLevel = (int)severity + adjustment;

            // Only increase degradation, never decrease automatically
            if (newLevel > (int)currentLevel)
            {
                return (DegradationLevel)Math.Min(newLevel, (int)DegradationLevel.Emergency);
            }

            return currentLevel;
        }

        private DegradationLevel GetExceptionSeverity(Exception exception)
        {
            if (exception is TimeoutException)
            {
                return DegradationLevel.Minimal;
            }

            if (exception is ProcessExecutionException pee && pee.Message.Contains("timeout"))
            {
                return DegradationLevel.Minimal;
            }

            if (exception is ProcessExecutionException)
            {
                return DegradationLevel.Moderate;
            }

            if (exception is SocketException)
            {
                return DegradationLevel.Severe;
            }

            if (exception is LicenseManagerException)
            {
                return DegradationLevel.Severe;
            }

            if (exception is OperationCanceledException)
            {
                return DegradationLevel.Full;
            }

            return DegradationLevel.Moderate;
        }

        private void UpdateGlobalDegradationState()
        {
            var previousLevel = _globalState.CurrentLevel;

            if (_componentStates.IsEmpty)
            {
                _globalState.CurrentLevel = DegradationLevel.Full;
                _globalState.DegradationScore = 0;
            }
            else
            {
                // Find the highest degradation level among all components
                var maxLevel = _componentStates.Values.Max(s => s.CurrentLevel);
                _globalState.CurrentLevel = maxLevel;

                // Calculate degradation score based on number and severity of degraded components
                var totalScore = _componentStates.Values.Sum(s => (int)s.CurrentLevel * 10);
                _globalState.DegradationScore = Math.Min(100, totalScore);
            }

            _globalState.LastUpdated = DateTime.Now;
            _globalState.AffectedComponents = _componentStates.Keys.ToList();

            if (previousLevel != _globalState.CurrentLevel)
            {
                _logger.LogWarning("Global degradation level changed from {Previous} to {Current}",
                    previousLevel, _globalState.CurrentLevel);
            }
        }

        private void CheckRecoveryStatus(object state)
        {
            try
            {
                lock (_stateLock)
                {
                    var componentsToRecover = new List<string>();

                    foreach (var kvp in _componentStates)
                    {
                        var component = kvp.Key;
                        var componentState = kvp.Value;

                        // Check if component has been degraded for more than 30 minutes
                        if (DateTime.Now - componentState.StartedAt > TimeSpan.FromMinutes(30))
                        {
                            // Auto-recover minimal degradations
                            if (componentState.CurrentLevel == DegradationLevel.Minimal)
                            {
                                componentsToRecover.Add(component);
                            }
                        }
                    }

                    // Recover eligible components
                    foreach (var component in componentsToRecover)
                    {
                        _logger.LogInformation("Auto-recovering component {Component} from minimal degradation", component);
                        _componentStates.TryRemove(component, out _);
                    }

                    if (componentsToRecover.Count > 0)
                    {
                        UpdateGlobalDegradationState();
                        OnDegradationLevelChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking recovery status");
            }
        }

        private void OnDegradationLevelChanged()
        {
            DegradationLevelChanged?.Invoke(this, _globalState);
        }

        private List<DegradationRule> GetDefaultRules()
        {
            return new List<DegradationRule>
            {
                new DegradationRule
                {
                    Name = "LicenseQueryDegradation",
                    OperationName = "GetServerStatusAsync",
                    Priority = OperationPriority.High,
                    MinimumLevel = DegradationLevel.Full,
                    MaximumLevel = DegradationLevel.Emergency,
                    TimeoutMultiplier = 2.0,
                    SkipDuringDegradation = false,
                    ShouldDegrade = ex => ex is TimeoutException or SocketException,
                    Configuration = { ["MaxRetries"] = 2 }
                },
                new DegradationRule
                {
                    Name = "LicenseReleaseDegradation",
                    OperationName = "ReleaseLicenseAsync",
                    Priority = OperationPriority.Critical,
                    MinimumLevel = DegradationLevel.Full,
                    MaximumLevel = DegradationLevel.Emergency,
                    TimeoutMultiplier = 3.0,
                    SkipDuringDegradation = false,
                    ShouldDegrade = ex => ex is TimeoutException or ProcessExecutionException,
                    Configuration = { ["MaxRetries"] = 3 }
                },
                new DegradationRule
                {
                    Name = "ProcessMetricsDegradation",
                    OperationName = "RecordMetrics",
                    Priority = OperationPriority.Low,
                    MinimumLevel = DegradationLevel.Full,
                    MaximumLevel = DegradationLevel.Moderate,
                    SkipDuringDegradation = true,
                    ShouldDegrade = ex => false
                }
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _recoveryTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}