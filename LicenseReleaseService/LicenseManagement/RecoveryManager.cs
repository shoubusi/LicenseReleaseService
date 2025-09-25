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
    /// Defines the severity levels for recovery scenarios
    /// </summary>
    public enum RecoverySeverity
    {
        /// <summary>
        /// Low severity issue, can be handled automatically
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity issue, may require user intervention
        /// </summary>
        Medium,

        /// <summary>
        /// High severity issue, requires immediate attention
        /// </summary>
        High,

        /// <summary>
        /// Critical severity issue, system may be unstable
        /// </summary>
        Critical
    }

    /// <summary>
    /// Defines the recovery action types
    /// </summary>
    public enum RecoveryActionType
    {
        /// <summary>
        /// Retry the operation
        /// </summary>
        Retry,

        /// <summary>
        /// Reset the component state
        /// </summary>
        Reset,

        /// <summary>
        /// Restart the affected service
        /// </summary>
        Restart,

        /// <summary>
        /// Fail over to backup system
        /// </summary>
        Failover,

        /// <summary>
        /// Notify administrator
        /// </summary>
        Notify,

        /// <summary>
        /// Custom recovery action
        /// </summary>
        Custom
    }

    /// <summary>
    /// Represents a recovery scenario
    /// </summary>
    public class RecoveryScenario
    {
        /// <summary>
        /// Gets or sets the unique identifier for the scenario
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the scenario
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the scenario
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the severity level
        /// </summary>
        public RecoverySeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the exception types that trigger this scenario
        /// </summary>
        public HashSet<Type> ExceptionTypes { get; set; }

        /// <summary>
        /// Gets or sets the condition predicate to determine if this scenario applies
        /// </summary>
        public Func<Exception, bool> Condition { get; set; }

        /// <summary>
        /// Gets or sets the recovery actions to execute
        /// </summary>
        public List<RecoveryAction> Actions { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of recovery attempts
        /// </summary>
        public int MaxRecoveryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the cooldown period between recovery attempts
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this scenario is enabled
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Represents a recovery action to be executed
    /// </summary>
    public class RecoveryAction
    {
        /// <summary>
        /// Gets or sets the type of recovery action
        /// </summary>
        public RecoveryActionType Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the action
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the action
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the action to execute
        /// </summary>
        public Func<Task<bool>> Action { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the action
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this action is critical
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets or sets the rollback action if this action fails
        /// </summary>
        public Func<Task> RollbackAction { get; set; }
    }

    /// <summary>
    /// Represents the result of a recovery operation
    /// </summary>
    public class RecoveryResult
    {
        /// <summary>
        /// Gets or sets the scenario ID
        /// </summary>
        public string ScenarioId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether recovery was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the exception that was recovered from
        /// </summary>
        public Exception OriginalException { get; set; }

        /// <summary>
        /// Gets or sets the recovery actions executed
        /// </summary>
        public List<RecoveryActionResult> Actions { get; set; }

        /// <summary>
        /// Gets or sets the total time taken for recovery
        /// </summary>
        public TimeSpan RecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets any error message if recovery failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when recovery was completed
        /// </summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// Represents the result of a single recovery action
    /// </summary>
    public class RecoveryActionResult
    {
        /// <summary>
        /// Gets or sets the action name
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the action was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the time taken to execute the action
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets any error message if the action failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Manages coordinated error recovery across components
    /// </summary>
    public class RecoveryManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, RecoveryScenario> _scenarios;
        private readonly ConcurrentDictionary<string, DateTime> _lastRecoveryAttempts;
        private readonly SemaphoreSlim _recoveryLock;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the RecoveryManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public RecoveryManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scenarios = new ConcurrentDictionary<string, RecoveryScenario>();
            _lastRecoveryAttempts = new ConcurrentDictionary<string, DateTime>();
            _recoveryLock = new SemaphoreSlim(1, 1);

            RegisterDefaultScenarios();
        }

        /// <summary>
        /// Registers a recovery scenario
        /// </summary>
        /// <param name="scenario">The scenario to register</param>
        public void RegisterScenario(RecoveryScenario scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            if (string.IsNullOrEmpty(scenario.Id))
                throw new ArgumentException("Scenario ID cannot be null or empty", nameof(scenario));

            _scenarios[scenario.Id] = scenario;
            _logger.LogInformation("Registered recovery scenario: {ScenarioName} ({ScenarioId})", scenario.Name, scenario.Id);
        }

        /// <summary>
        /// Attempts to recover from an exception
        /// </summary>
        /// <param name="exception">The exception to recover from</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The recovery result</returns>
        public async Task<RecoveryResult> RecoverAsync(Exception exception, object context = null, CancellationToken cancellationToken = default)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var scenario = FindMatchingScenario(exception);
            if (scenario == null)
            {
                _logger.LogWarning(exception, "No recovery scenario found for exception type: {ExceptionType}", exception.GetType().Name);
                return new RecoveryResult
                {
                    Success = false,
                    OriginalException = exception,
                    ErrorMessage = "No recovery scenario found for this exception type",
                    CompletedAt = DateTime.Now
                };
            }

            return await ExecuteRecoveryAsync(scenario, exception, context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds the matching recovery scenario for an exception
        /// </summary>
        /// <param name="exception">The exception to match</param>
        /// <returns>The matching scenario, or null if none found</returns>
        private RecoveryScenario FindMatchingScenario(Exception exception)
        {
            var matchingScenarios = _scenarios.Values
                .Where(s => s.IsEnabled && MatchesScenario(s, exception))
                .OrderBy(s => s.Severity)
                .ToList();

            return matchingScenarios.FirstOrDefault();
        }

        /// <summary>
        /// Determines if a scenario matches the given exception
        /// </summary>
        /// <param name="scenario">The scenario to check</param>
        /// <param name="exception">The exception to match</param>
        /// <returns>True if the scenario matches</returns>
        private bool MatchesScenario(RecoveryScenario scenario, Exception exception)
        {
            // Check exception types
            if (scenario.ExceptionTypes.Any(t => t.IsInstanceOfType(exception)))
            {
                // If no custom condition, or if condition matches
                return scenario.Condition == null || scenario.Condition(exception);
            }

            return false;
        }

        /// <summary>
        /// Executes a recovery scenario
        /// </summary>
        /// <param name="scenario">The scenario to execute</param>
        /// <param name="exception">The exception being recovered from</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The recovery result</returns>
        private async Task<RecoveryResult> ExecuteRecoveryAsync(RecoveryScenario scenario, Exception exception, object context, CancellationToken cancellationToken)
        {
            await _recoveryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Check cooldown period
                if (_lastRecoveryAttempts.TryGetValue(scenario.Id, out var lastAttempt))
                {
                    var timeSinceLastAttempt = DateTime.Now - lastAttempt;
                    if (timeSinceLastAttempt < scenario.CooldownPeriod)
                    {
                        _logger.LogWarning("Recovery scenario {ScenarioId} is in cooldown period. Last attempt: {LastAttempt}",
                            scenario.Id, lastAttempt);
                        return new RecoveryResult
                        {
                            Success = false,
                            ScenarioId = scenario.Id,
                            OriginalException = exception,
                            ErrorMessage = $"Recovery scenario is in cooldown period until {lastAttempt.Add(scenario.CooldownPeriod)}",
                            CompletedAt = DateTime.Now
                        };
                    }
                }

                _lastRecoveryAttempts[scenario.Id] = DateTime.Now;

                var startTime = DateTime.Now;
                var actions = new List<RecoveryActionResult>();
                bool success = true;
                string errorMessage = null;

                _logger.LogInformation("Starting recovery for scenario {ScenarioName} ({ScenarioId}) with severity {Severity}",
                    scenario.Name, scenario.Id, scenario.Severity);

                foreach (var action in scenario.Actions)
                {
                    var actionStartTime = DateTime.Now;
                    var actionResult = new RecoveryActionResult
                    {
                        ActionName = action.Name
                    };

                    try
                    {
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        linkedCts.CancelAfter(action.Timeout);

                        var actionSuccess = await action.Action().ConfigureAwait(false);
                        actionResult.Success = actionSuccess;
                        actionResult.ExecutionTime = DateTime.Now - actionStartTime;

                        if (!actionSuccess)
                        {
                            _logger.LogError("Recovery action {ActionName} failed", action.Name);
                            if (action.IsCritical)
                            {
                                success = false;
                                errorMessage = $"Critical recovery action {action.Name} failed";
                                break;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Recovery action {ActionName} completed successfully", action.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        actionResult.Success = false;
                        actionResult.ExecutionTime = DateTime.Now - actionStartTime;
                        actionResult.ErrorMessage = ex.Message;

                        _logger.LogError(ex, "Recovery action {ActionName} failed with exception", action.Name);

                        if (action.IsCritical)
                        {
                            success = false;
                            errorMessage = $"Critical recovery action {action.Name} failed: {ex.Message}";
                            break;
                        }
                    }

                    actions.Add(actionResult);
                }

                var result = new RecoveryResult
                {
                    ScenarioId = scenario.Id,
                    Success = success,
                    OriginalException = exception,
                    Actions = actions,
                    RecoveryTime = DateTime.Now - startTime,
                    ErrorMessage = errorMessage,
                    CompletedAt = DateTime.Now
                };

                if (success)
                {
                    _logger.LogInformation("Recovery scenario {ScenarioName} completed successfully in {Time:F2}ms",
                        scenario.Name, result.RecoveryTime.TotalMilliseconds);
                }
                else
                {
                    _logger.LogError("Recovery scenario {ScenarioName} failed: {ErrorMessage}", scenario.Name, errorMessage);
                }

                return result;
            }
            finally
            {
                _recoveryLock.Release();
            }
        }

        /// <summary>
        /// Registers default recovery scenarios
        /// </summary>
        private void RegisterDefaultScenarios()
        {
            // Network connectivity issues
            RegisterScenario(new RecoveryScenario
            {
                Id = "network-connectivity",
                Name = "Network Connectivity Recovery",
                Description = "Handles network connectivity issues",
                Severity = RecoverySeverity.Medium,
                ExceptionTypes = new HashSet<Type>
                {
                    typeof(System.Net.Sockets.SocketException),
                    typeof(System.Net.Http.HttpRequestException)
                },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "RetryOperation",
                        Description = "Retry the operation with exponential backoff",
                        Timeout = TimeSpan.FromSeconds(30),
                        IsCritical = false
                    }
                },
                MaxRecoveryAttempts = 3,
                CooldownPeriod = TimeSpan.FromMinutes(1),
                IsEnabled = true
            });

            // Process execution failures
            RegisterScenario(new RecoveryScenario
            {
                Id = "process-execution",
                Name = "Process Execution Recovery",
                Description = "Handles process execution failures",
                Severity = RecoverySeverity.High,
                ExceptionTypes = new HashSet<Type>
                {
                    typeof(ProcessExecutionException)
                },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "RetryProcess",
                        Description = "Retry process execution",
                        Timeout = TimeSpan.FromSeconds(30),
                        IsCritical = false
                    },
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Reset,
                        Name = "ResetProcessState",
                        Description = "Reset process execution state",
                        Timeout = TimeSpan.FromSeconds(10),
                        IsCritical = true
                    }
                },
                MaxRecoveryAttempts = 2,
                CooldownPeriod = TimeSpan.FromMinutes(2),
                IsEnabled = true
            });

            // Timeout scenarios
            RegisterScenario(new RecoveryScenario
            {
                Id = "timeout-recovery",
                Name = "Timeout Recovery",
                Description = "Handles operation timeouts",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type>
                {
                    typeof(TimeoutException)
                },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "RetryWithIncreasedTimeout",
                        Description = "Retry operation with increased timeout",
                        Timeout = TimeSpan.FromMinutes(1),
                        IsCritical = false
                    }
                },
                MaxRecoveryAttempts = 2,
                CooldownPeriod = TimeSpan.FromSeconds(30),
                IsEnabled = true
            });
        }

        /// <summary>
        /// Gets all registered recovery scenarios
        /// </summary>
        /// <returns>List of registered scenarios</returns>
        public IEnumerable<RecoveryScenario> GetRegisteredScenarios()
        {
            return _scenarios.Values.ToList();
        }

        /// <summary>
        /// Gets recovery statistics
        /// </summary>
        /// <returns>Recovery statistics</returns>
        public RecoveryStatistics GetStatistics()
        {
            // This would normally track actual recovery attempts and results
            // For now, returning basic information
            return new RecoveryStatistics
            {
                TotalScenarios = _scenarios.Count,
                EnabledScenarios = _scenarios.Values.Count(s => s.IsEnabled),
                RecentRecoveryAttempts = _lastRecoveryAttempts.Count,
                LastRecoveryAttempt = _lastRecoveryAttempts.Values.Any()
                    ? _lastRecoveryAttempts.Values.Max()
                    : (DateTime?)null
            };
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _recoveryLock?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents recovery statistics
    /// </summary>
    public class RecoveryStatistics
    {
        /// <summary>
        /// Gets or sets the total number of registered scenarios
        /// </summary>
        public int TotalScenarios { get; set; }

        /// <summary>
        /// Gets or sets the number of enabled scenarios
        /// </summary>
        public int EnabledScenarios { get; set; }

        /// <summary>
        /// Gets or sets the number of recent recovery attempts
        /// </summary>
        public int RecentRecoveryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last recovery attempt
        /// </summary>
        public DateTime? LastRecoveryAttempt { get; set; }
    }
}