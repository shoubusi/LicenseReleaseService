using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{

    /// <summary>
    /// Represents a recovery action for timer execution errors
    /// </summary>
    public class TimerRecoveryActionInstance
    {
        /// <summary>
        /// Gets or sets the type of recovery action
        /// </summary>
        public TimerRecoveryAction Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the action
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the action
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the action
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets whether the action is critical
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets or sets the action to execute
        /// </summary>
        public Func<TimerErrorEventArgs, object, CancellationToken, Task<bool>> Action { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryActionInstance class
        /// </summary>
        public TimerRecoveryActionInstance()
        {
            Timeout = TimeSpan.FromSeconds(30);
            IsCritical = false;
        }
    }

    /// <summary>
    /// Manages recovery operations for timer execution errors
    /// </summary>
    public class TimerRecoveryManager : IDisposable
    {
        private readonly ILogger<TimerRecoveryManager> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly ITimerExecutionService _timerService;
        private readonly TimerCircuitBreaker _circuitBreaker;
        private readonly TimerErrorClassifier _errorClassifier;
        private readonly SemaphoreSlim _recoveryLock;
        private readonly object _scenarioLock;
        private readonly Dictionary<string, TimerRecoveryScenario> _scenarios;
        private readonly Queue<TimerRecoveryAttempt> _recoveryHistory;
        private readonly Dictionary<Guid, CancellationTokenSource> _activeRecoveries;

        private bool _isDisposed;
        private int _totalRecoveryAttempts;
        private int _successfulRecoveries;
        private int _failedRecoveries;

        #region Events

        /// <summary>
        /// Occurs when a recovery operation starts
        /// </summary>
        public event EventHandler<TimerRecoveryEventArgs> RecoveryStarted;

        /// <summary>
        /// Occurs when a recovery operation completes
        /// </summary>
        public event EventHandler<TimerRecoveryEventArgs> RecoveryCompleted;

        /// <summary>
        /// Occurs when a recovery scenario is registered
        /// </summary>
        public event EventHandler<TimerRecoveryScenarioEventArgs> ScenarioRegistered;

        /// <summary>
        /// Occurs when automatic recovery is triggered
        /// </summary>
        public event EventHandler<TimerAutoRecoveryEventArgs> AutoRecoveryTriggered;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the total number of recovery attempts
        /// </summary>
        public int TotalRecoveryAttempts => _totalRecoveryAttempts;

        /// <summary>
        /// Gets the number of successful recoveries
        /// </summary>
        public int SuccessfulRecoveries => _successfulRecoveries;

        /// <summary>
        /// Gets the number of failed recoveries
        /// </summary>
        public int FailedRecoveries => _failedRecoveries;

        /// <summary>
        /// Gets the recovery success rate
        /// </summary>
        public double RecoverySuccessRate => _totalRecoveryAttempts > 0 ?
            (double)_successfulRecoveries / _totalRecoveryAttempts : 0;

        /// <summary>
        /// Gets the registered recovery scenarios
        /// </summary>
        public IReadOnlyDictionary<string, TimerRecoveryScenario> RegisteredScenarios => new Dictionary<string, TimerRecoveryScenario>(_scenarios);

        /// <summary>
        /// Gets the recovery history
        /// </summary>
        public IReadOnlyList<TimerRecoveryAttempt> RecoveryHistory => _recoveryHistory.ToList();

        #endregion

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryManager class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The timer execution options</param>
        /// <param name="timerService">The timer execution service</param>
        /// <param name="circuitBreaker">The circuit breaker instance</param>
        /// <param name="errorClassifier">The error classifier instance</param>
        public TimerRecoveryManager(ILogger<TimerRecoveryManager> logger, TimerExecutionOptions options,
            ITimerExecutionService timerService, TimerCircuitBreaker circuitBreaker, TimerErrorClassifier errorClassifier)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _errorClassifier = errorClassifier ?? throw new ArgumentNullException(nameof(errorClassifier));

            _recoveryLock = new SemaphoreSlim(1, 1);
            _scenarioLock = new object();
            _scenarios = new Dictionary<string, TimerRecoveryScenario>();
            _recoveryHistory = new Queue<TimerRecoveryAttempt>();
            _activeRecoveries = new Dictionary<Guid, CancellationTokenSource>();

            RegisterDefaultScenarios();
        }

        /// <summary>
        /// Registers a recovery scenario
        /// </summary>
        /// <param name="scenario">The scenario to register</param>
        public void RegisterScenario(TimerRecoveryScenario scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            if (string.IsNullOrEmpty(scenario.Id))
                throw new ArgumentException("Scenario ID cannot be null or empty", nameof(scenario));

            lock (_scenarioLock)
            {
                _scenarios[scenario.Id] = scenario;
            }

            _logger.LogInformation("Registered recovery scenario: {ScenarioName} ({ScenarioId})", scenario.Name, scenario.Id);

            OnScenarioRegistered(new TimerRecoveryScenarioEventArgs(scenario));
        }

        /// <summary>
        /// Attempts to recover from a timer error
        /// </summary>
        /// <param name="errorEventArgs">The error event arguments</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The recovery result</returns>
        public async Task<TimerRecoveryResult> RecoverAsync(TimerErrorEventArgs errorEventArgs, object context = null, CancellationToken cancellationToken = default)
        {
            if (errorEventArgs == null)
                throw new ArgumentNullException(nameof(errorEventArgs));

            var recoveryId = Guid.NewGuid();
            var result = new TimerRecoveryResult
            {
                RecoveryId = recoveryId,
                ErrorEvent = errorEventArgs,
                Timestamp = DateTime.UtcNow,
                Actions = new List<TimerRecoveryActionResult>()
            };

            await _recoveryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Classify the error
                var classification = _errorClassifier.ClassifyError(errorEventArgs.Exception, context);

                // Find matching scenario
                var scenario = FindMatchingScenario(classification, errorEventArgs);
                if (scenario == null)
                {
                    _logger.LogWarning("No recovery scenario found for error: {ErrorCategory}/{ErrorSeverity}",
                        classification.Category, classification.Severity);

                    result.Success = false;
                    result.ErrorMessage = "No recovery scenario found for this error type";
                    result.CompletedAt = DateTime.UtcNow;

                    return result;
                }

                // Check cooldown period
                if (IsInCooldown(scenario))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Recovery scenario {scenario.Id} is in cooldown period";
                    result.CompletedAt = DateTime.UtcNow;

                    return result;
                }

                // Execute recovery
                return await ExecuteRecoveryAsync(scenario, errorEventArgs, classification, context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _recoveryLock.Release();
            }
        }

        /// <summary>
        /// Attempts automatic recovery for a timer error
        /// </summary>
        /// <param name="errorEventArgs">The error event arguments</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The recovery result</returns>
        public async Task<TimerRecoveryResult> AutoRecoverAsync(TimerErrorEventArgs errorEventArgs, object context = null, CancellationToken cancellationToken = default)
        {
            var result = await RecoverAsync(errorEventArgs, context, cancellationToken).ConfigureAwait(false);

            // Notify about automatic recovery
            OnAutoRecoveryTriggered(new TimerAutoRecoveryEventArgs(result, errorEventArgs));

            return result;
        }

        /// <summary>
        /// Executes a specific recovery action
        /// </summary>
        /// <param name="action">The recovery action to execute</param>
        /// <param name="errorEventArgs">The error event arguments</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The action result</returns>
        public async Task<TimerRecoveryActionResult> ExecuteActionAsync(TimerRecoveryActionInstance action, TimerErrorEventArgs errorEventArgs, object context = null, CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var actionResult = new TimerRecoveryActionResult
            {
                ActionName = action.Name,
                StartTime = DateTime.UtcNow
            };

            var recoveryEventArgs = new TimerRecoveryEventArgs(errorEventArgs, action.Type);

            try
            {
                // Notify recovery started
                OnRecoveryStarted(recoveryEventArgs);

                // Create linked cancellation token with timeout
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(action.Timeout);

                // Execute the action
                var success = await action.Action(errorEventArgs, context, linkedCts.Token).ConfigureAwait(false);

                actionResult.Success = success;
                actionResult.Duration = DateTime.UtcNow - actionResult.StartTime;

                if (success)
                {
                    _logger.LogInformation("Recovery action {ActionName} completed successfully in {Duration}ms",
                        action.Name, actionResult.Duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Recovery action {ActionName} failed", action.Name);
                }

                // Update recovery event args
                recoveryEventArgs.Complete(success, success ? "Action completed successfully" : "Action failed");

                return actionResult;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                actionResult.Success = false;
                actionResult.Duration = DateTime.UtcNow - actionResult.StartTime;
                actionResult.ErrorMessage = "Action timed out";

                _logger.LogWarning("Recovery action {ActionName} timed out after {Timeout}ms",
                    action.Name, action.Timeout.TotalMilliseconds);

                recoveryEventArgs.Complete(false, "Action timed out");

                return actionResult;
            }
            catch (Exception ex)
            {
                actionResult.Success = false;
                actionResult.Duration = DateTime.UtcNow - actionResult.StartTime;
                actionResult.ErrorMessage = ex.Message;

                _logger.LogError(ex, "Recovery action {ActionName} failed with exception", action.Name);

                recoveryEventArgs.Complete(false, $"Action failed: {ex.Message}");
                recoveryEventArgs.RecoveryException = ex;

                return actionResult;
            }
            finally
            {
                // Notify recovery completed
                OnRecoveryCompleted(recoveryEventArgs);
            }
        }

        /// <summary>
        /// Gets recovery statistics
        /// </summary>
        /// <returns>Recovery statistics</returns>
        public TimerRecoveryStatistics GetStatistics()
        {
            lock (_scenarioLock)
            {
                return new TimerRecoveryStatistics
                {
                    TotalRecoveryAttempts = _totalRecoveryAttempts,
                    SuccessfulRecoveries = _successfulRecoveries,
                    FailedRecoveries = _failedRecoveries,
                    RecoverySuccessRate = RecoverySuccessRate,
                    TotalScenarios = _scenarios.Count,
                    EnabledScenarios = _scenarios.Values.Count(s => s.IsEnabled),
                    ActiveRecoveries = _activeRecoveries.Count,
                    RecentRecoveryAttempts = _recoveryHistory.Count,
                    AverageRecoveryTime = _recoveryHistory.Any() ?
                        TimeSpan.FromTicks((long)_recoveryHistory.Average(r => r.Duration.Ticks)) : TimeSpan.Zero
                };
            }
        }

        /// <summary>
        /// Gets recovery history for a specific time period
        /// </summary>
        /// <param name="since">The start time for the history</param>
        /// <returns>List of recovery attempts</returns>
        public List<TimerRecoveryAttempt> GetRecoveryHistory(DateTime since)
        {
            return _recoveryHistory.Where(r => r.StartTime >= since).ToList();
        }

        /// <summary>
        /// Cancels all active recovery operations
        /// </summary>
        public async Task CancelAllActiveRecoveriesAsync()
        {
            var cancellationSources = _activeRecoveries.Values.ToList();
            _activeRecoveries.Clear();

            foreach (var cts in cancellationSources)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cancelling recovery operation");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears recovery history
        /// </summary>
        public void ClearHistory()
        {
            lock (_recoveryLock)
            {
                _recoveryHistory.Clear();
            }
        }

        #region Private Methods

        /// <summary>
        /// Finds a matching recovery scenario for the given error
        /// </summary>
        private TimerRecoveryScenario FindMatchingScenario(TimerErrorClassification classification, TimerErrorEventArgs errorEventArgs)
        {
            lock (_scenarioLock)
            {
                var matchingScenarios = _scenarios.Values
                    .Where(s => s.IsEnabled && MatchesScenario(s, classification, errorEventArgs))
                    .OrderBy(s => s.Priority)
                    .ToList();

                return matchingScenarios.FirstOrDefault();
            }
        }

        /// <summary>
        /// Determines if a scenario matches the given error
        /// </summary>
        private bool MatchesScenario(TimerRecoveryScenario scenario, TimerErrorClassification classification, TimerErrorEventArgs errorEventArgs)
        {
            // Check error categories
            if (scenario.ErrorCategories.Count > 0 &&
                !scenario.ErrorCategories.Contains(classification.Category))
            {
                return false;
            }

            // Check error severities
            if (scenario.ErrorSeverities.Count > 0 &&
                !scenario.ErrorSeverities.Contains(classification.Severity))
            {
                return false;
            }

            // Check exception types
            if (scenario.ExceptionTypes.Count > 0)
            {
                var exceptionType = errorEventArgs.Exception.GetType();
                if (!scenario.ExceptionTypes.Any(t => t.IsAssignableFrom(exceptionType)))
                {
                    return false;
                }
            }

            // Check custom condition
            if (scenario.Condition != null)
            {
                return scenario.Condition(classification, errorEventArgs);
            }

            return true;
        }

        /// <summary>
        /// Checks if a scenario is in cooldown period
        /// </summary>
        private bool IsInCooldown(TimerRecoveryScenario scenario)
        {
            var lastAttempt = _recoveryHistory
                .Where(r => r.ScenarioId == scenario.Id)
                .OrderByDescending(r => r.StartTime)
                .FirstOrDefault();

            if (lastAttempt == null)
                return false;

            return DateTime.UtcNow - lastAttempt.StartTime < scenario.CooldownPeriod;
        }

        /// <summary>
        /// Executes a recovery scenario
        /// </summary>
        private async Task<TimerRecoveryResult> ExecuteRecoveryAsync(TimerRecoveryScenario scenario, TimerErrorEventArgs errorEventArgs,
            TimerErrorClassification classification, object context, CancellationToken cancellationToken)
        {
            var recoveryId = Guid.NewGuid();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeRecoveries[recoveryId] = cts;

            var result = new TimerRecoveryResult
            {
                RecoveryId = recoveryId,
                ErrorEvent = errorEventArgs,
                ScenarioId = scenario.Id,
                ScenarioName = scenario.Name,
                Timestamp = DateTime.UtcNow,
                Actions = new List<TimerRecoveryActionResult>()
            };

            var recoveryAttempt = new TimerRecoveryAttempt
            {
                RecoveryId = recoveryId,
                ScenarioId = scenario.Id,
                StartTime = DateTime.UtcNow,
                ErrorClassification = classification,
                ErrorEvent = errorEventArgs
            };

            try
            {
                _logger.LogInformation("Starting recovery for scenario {ScenarioName} ({ScenarioId})",
                    scenario.Name, scenario.Id);

                Interlocked.Increment(ref _totalRecoveryAttempts);

                var success = true;
                string errorMessage = null;

                // Execute each action in the scenario
                foreach (var action in scenario.Actions)
                {
                    var actionResult = await ExecuteActionAsync(action, errorEventArgs, context, cts.Token).ConfigureAwait(false);
                    result.Actions.Add(actionResult);

                    if (!actionResult.Success)
                    {
                        success = false;
                        errorMessage = $"Critical action {action.Name} failed";

                        if (action.IsCritical)
                        {
                            break;
                        }
                    }
                }

                result.Success = success;
                result.ErrorMessage = errorMessage;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.Timestamp;

                // Update statistics
                if (success)
                {
                    Interlocked.Increment(ref _successfulRecoveries);
                    _logger.LogInformation("Recovery scenario {ScenarioName} completed successfully in {Duration}ms",
                        scenario.Name, result.Duration.TotalMilliseconds);
                }
                else
                {
                    Interlocked.Increment(ref _failedRecoveries);
                    _logger.LogError("Recovery scenario {ScenarioName} failed: {ErrorMessage}",
                        scenario.Name, errorMessage);
                }

                // Complete the recovery attempt
                recoveryAttempt.Success = success;
                recoveryAttempt.Duration = result.Duration;
                recoveryAttempt.ErrorMessage = errorMessage;
                recoveryAttempt.Actions = result.Actions;

                // Add to history
                lock (_recoveryLock)
                {
                    _recoveryHistory.Enqueue(recoveryAttempt);
                    while (_recoveryHistory.Count > 100) // Keep last 100 attempts
                    {
                        _recoveryHistory.Dequeue();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.Timestamp;

                recoveryAttempt.Success = false;
                recoveryAttempt.Duration = result.Duration;
                recoveryAttempt.ErrorMessage = ex.Message;

                Interlocked.Increment(ref _failedRecoveries);

                _logger.LogError(ex, "Recovery scenario {ScenarioName} failed with exception", scenario.Name);

                return result;
            }
            finally
            {
                // Clean up
                _activeRecoveries.Remove(recoveryId);
                cts.Dispose();
            }
        }

        /// <summary>
        /// Registers default recovery scenarios
        /// </summary>
        private void RegisterDefaultScenarios()
        {
            // Network connectivity issues
            RegisterScenario(new TimerRecoveryScenario
            {
                Id = "network-connectivity",
                Name = "Network Connectivity Recovery",
                Description = "Handles network connectivity issues with retry logic",
                Priority = 10,
                ErrorCategories = new List<TimerErrorCategory> { TimerErrorCategory.Network },
                ExceptionTypes = new List<Type>
                {
                    typeof(System.Net.Sockets.SocketException),
                    typeof(System.Net.Http.HttpRequestException)
                },
                Actions = new List<TimerRecoveryActionInstance>
                {
                    new TimerRecoveryActionInstance
                    {
                        Type = TimerRecoveryAction.Retry,
                        Name = "RetryWithBackoff",
                        Description = "Retry operation with exponential backoff",
                        Timeout = TimeSpan.FromSeconds(30),
                        IsCritical = false,
                        Action = async (error, context, token) =>
                        {
                            // Simulate retry with backoff
                            await Task.Delay(1000, token);
                            return true;
                        }
                    }
                },
                MaxRecoveryAttempts = 3,
                CooldownPeriod = TimeSpan.FromMinutes(1),
                IsEnabled = true
            });

            // Timeout issues
            RegisterScenario(new TimerRecoveryScenario
            {
                Id = "timeout-recovery",
                Name = "Timeout Recovery",
                Description = "Handles operation timeouts with retry and increased timeout",
                Priority = 20,
                ErrorCategories = new List<TimerErrorCategory> { TimerErrorCategory.Timeout },
                ExceptionTypes = new List<Type> { typeof(TimeoutException) },
                Actions = new List<TimerRecoveryActionInstance>
                {
                    new TimerRecoveryActionInstance
                    {
                        Type = TimerRecoveryAction.Retry,
                        Name = "RetryWithIncreasedTimeout",
                        Description = "Retry operation with increased timeout",
                        Timeout = TimeSpan.FromMinutes(1),
                        IsCritical = false,
                        Action = async (error, context, token) =>
                        {
                            // Simulate retry with increased timeout
                            await Task.Delay(100, token);
                            return true;
                        }
                    }
                },
                MaxRecoveryAttempts = 2,
                CooldownPeriod = TimeSpan.FromSeconds(30),
                IsEnabled = true
            });

            // Resource exhaustion
            RegisterScenario(new TimerRecoveryScenario
            {
                Id = "resource-exhaustion",
                Name = "Resource Exhaustion Recovery",
                Description = "Handles resource exhaustion by increasing interval and cleaning up",
                Priority = 5,
                ErrorCategories = new List<TimerErrorCategory> { TimerErrorCategory.Resource },
                ExceptionTypes = new List<Type>
                {
                    typeof(OutOfMemoryException),
                    typeof(InsufficientMemoryException)
                },
                Actions = new List<TimerRecoveryActionInstance>
                {
                    new TimerRecoveryActionInstance
                    {
                        Type = TimerRecoveryAction.IncreaseInterval,
                        Name = "IncreaseTimerInterval",
                        Description = "Increase timer interval to reduce resource usage",
                        Timeout = TimeSpan.FromSeconds(10),
                        IsCritical = true,
                        Action = async (error, context, token) =>
                        {
                            try
                            {
                                if (_timerService.IsRunning)
                                {
                                    var newInterval = _timerService.CurrentInterval.Add(TimeSpan.FromMinutes(5));
                                    _timerService.UpdateInterval(newInterval);
                                    return true;
                                }
                                return false;
                            }
                            catch
                            {
                                return false;
                            }
                        }
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromMinutes(10),
                IsEnabled = true
            });

            // License server issues
            RegisterScenario(new TimerRecoveryScenario
            {
                Id = "license-server-recovery",
                Name = "License Server Recovery",
                Description = "Handles license server connectivity issues",
                Priority = 15,
                ErrorCategories = new List<TimerErrorCategory> { TimerErrorCategory.LicenseServer },
                Actions = new List<TimerRecoveryActionInstance>
                {
                    new TimerRecoveryActionInstance
                    {
                        Type = TimerRecoveryAction.EnableCircuitBreaker,
                        Name = "EnableCircuitBreaker",
                        Description = "Enable circuit breaker to prevent cascading failures",
                        Timeout = TimeSpan.FromSeconds(5),
                        IsCritical = true,
                        Action = async (error, context, token) =>
                        {
                            try
                            {
                                await _circuitBreaker.ForceOpenAsync("License server connectivity issues", error.ExecutionId);
                                return true;
                            }
                            catch
                            {
                                return false;
                            }
                        }
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromMinutes(5),
                IsEnabled = true
            });

            // Timer execution failures
            RegisterScenario(new TimerRecoveryScenario
            {
                Id = "timer-execution-recovery",
                Name = "Timer Execution Recovery",
                Description = "Handles timer execution failures with restart capability",
                Priority = 25,
                ErrorCategories = new List<TimerErrorCategory> { TimerErrorCategory.Execution },
                Actions = new List<TimerRecoveryActionInstance>
                {
                    new TimerRecoveryActionInstance
                    {
                        Type = TimerRecoveryAction.RestartService,
                        Name = "RestartTimerService",
                        Description = "Restart the timer service",
                        Timeout = TimeSpan.FromSeconds(30),
                        IsCritical = true,
                        Action = async (error, context, token) =>
                        {
                            try
                            {
                                if (_timerService.IsRunning)
                                {
                                    _timerService.Stop();
                                    await Task.Delay(1000, token);
                                    _timerService.Start(_timerService.CurrentInterval);
                                    return true;
                                }
                                return false;
                            }
                            catch
                            {
                                return false;
                            }
                        }
                    }
                },
                MaxRecoveryAttempts = 2,
                CooldownPeriod = TimeSpan.FromMinutes(2),
                IsEnabled = true
            });
        }

        /// <summary>
        /// Notifies listeners of recovery started events
        /// </summary>
        private void OnRecoveryStarted(TimerRecoveryEventArgs args)
        {
            RecoveryStarted?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of recovery completed events
        /// </summary>
        private void OnRecoveryCompleted(TimerRecoveryEventArgs args)
        {
            RecoveryCompleted?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of scenario registered events
        /// </summary>
        private void OnScenarioRegistered(TimerRecoveryScenarioEventArgs args)
        {
            ScenarioRegistered?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of auto recovery triggered events
        /// </summary>
        private void OnAutoRecoveryTriggered(TimerAutoRecoveryEventArgs args)
        {
            AutoRecoveryTriggered?.Invoke(this, args);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the recovery manager
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
            if (!_isDisposed)
            {
                if (disposing)
                {
                    CancelAllActiveRecoveriesAsync().Wait();
                    _recoveryLock?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerRecoveryManager()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Represents a recovery scenario for timer execution errors
    /// </summary>
    public class TimerRecoveryScenario
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
        /// Gets or sets the priority (lower numbers = higher priority)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the error categories this scenario handles
        /// </summary>
        public List<TimerErrorCategory> ErrorCategories { get; set; }

        /// <summary>
        /// Gets or sets the error severities this scenario handles
        /// </summary>
        public List<TimerErrorSeverity> ErrorSeverities { get; set; }

        /// <summary>
        /// Gets or sets the exception types this scenario handles
        /// </summary>
        public List<Type> ExceptionTypes { get; set; }

        /// <summary>
        /// Gets or sets the custom condition for this scenario
        /// </summary>
        public Func<TimerErrorClassification, TimerErrorEventArgs, bool> Condition { get; set; }

        /// <summary>
        /// Gets or sets the recovery actions to execute
        /// </summary>
        public List<TimerRecoveryActionInstance> Actions { get; set; }

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

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryScenario class
        /// </summary>
        public TimerRecoveryScenario()
        {
            ErrorCategories = new List<TimerErrorCategory>();
            ErrorSeverities = new List<TimerErrorSeverity>();
            ExceptionTypes = new List<Type>();
            Actions = new List<TimerRecoveryActionInstance>();
            Priority = 50;
            MaxRecoveryAttempts = 3;
            CooldownPeriod = TimeSpan.FromMinutes(1);
            IsEnabled = true;
        }
    }

    /// <summary>
    /// Represents a recovery action for timer execution errors
    /// </summary>
    public class TimerRecoveryActionDefinition
    {
        /// <summary>
        /// Gets or sets the type of recovery action
        /// </summary>
        public TimerRecoveryAction Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the action
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the action
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the action
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this action is critical
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets or sets the action to execute
        /// </summary>
        public Func<TimerErrorEventArgs, object, CancellationToken, Task<bool>> Action { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryActionDefinition class
        /// </summary>
        public TimerRecoveryActionDefinition()
        {
            Timeout = TimeSpan.FromSeconds(30);
            IsCritical = false;
        }
    }

    /// <summary>
    /// Represents the result of a recovery operation
    /// </summary>
    public class TimerRecoveryResult
    {
        /// <summary>
        /// Gets or sets the recovery operation ID
        /// </summary>
        public Guid RecoveryId { get; set; }

        /// <summary>
        /// Gets or sets the associated error event
        /// </summary>
        public TimerErrorEventArgs ErrorEvent { get; set; }

        /// <summary>
        /// Gets or sets the scenario ID
        /// </summary>
        public string ScenarioId { get; set; }

        /// <summary>
        /// Gets or sets the scenario name
        /// </summary>
        public string ScenarioName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when recovery started
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when recovery completed
        /// </summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the duration of the recovery
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets whether recovery was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if recovery failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the recovery actions executed
        /// </summary>
        public List<TimerRecoveryActionResult> Actions { get; set; }
    }

    /// <summary>
    /// Represents the result of a single recovery action
    /// </summary>
    public class TimerRecoveryActionResult
    {
        /// <summary>
        /// Gets or sets the action name
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the action started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the duration of the action
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets whether the action was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the action failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents a recovery attempt in the history
    /// </summary>
    public class TimerRecoveryAttempt
    {
        /// <summary>
        /// Gets or sets the recovery operation ID
        /// </summary>
        public Guid RecoveryId { get; set; }

        /// <summary>
        /// Gets or sets the scenario ID
        /// </summary>
        public string ScenarioId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the recovery started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the duration of the recovery
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets whether the recovery was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if recovery failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the error classification
        /// </summary>
        public TimerErrorClassification ErrorClassification { get; set; }

        /// <summary>
        /// Gets or sets the associated error event
        /// </summary>
        public TimerErrorEventArgs ErrorEvent { get; set; }

        /// <summary>
        /// Gets or sets the recovery actions executed
        /// </summary>
        public List<TimerRecoveryActionResult> Actions { get; set; }
    }

    /// <summary>
    /// Represents recovery statistics
    /// </summary>
    public class TimerRecoveryStatistics
    {
        /// <summary>
        /// Gets or sets the total number of recovery attempts
        /// </summary>
        public int TotalRecoveryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the number of successful recoveries
        /// </summary>
        public int SuccessfulRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of failed recoveries
        /// </summary>
        public int FailedRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the recovery success rate
        /// </summary>
        public double RecoverySuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the total number of scenarios
        /// </summary>
        public int TotalScenarios { get; set; }

        /// <summary>
        /// Gets or sets the number of enabled scenarios
        /// </summary>
        public int EnabledScenarios { get; set; }

        /// <summary>
        /// Gets or sets the number of active recoveries
        /// </summary>
        public int ActiveRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of recent recovery attempts
        /// </summary>
        public int RecentRecoveryAttempts { get; set; }

        /// <summary>
        /// Gets or sets the average recovery time
        /// </summary>
        public TimeSpan AverageRecoveryTime { get; set; }
    }

    /// <summary>
    /// Event arguments for scenario registration
    /// </summary>
    public class TimerRecoveryScenarioEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the registered scenario
        /// </summary>
        public TimerRecoveryScenario Scenario { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryScenarioEventArgs class
        /// </summary>
        public TimerRecoveryScenarioEventArgs(TimerRecoveryScenario scenario)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        }
    }

    /// <summary>
    /// Event arguments for auto recovery triggered events
    /// </summary>
    public class TimerAutoRecoveryEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the recovery result
        /// </summary>
        public TimerRecoveryResult RecoveryResult { get; set; }

        /// <summary>
        /// Gets or sets the error event that triggered the recovery
        /// </summary>
        public TimerErrorEventArgs ErrorEvent { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerAutoRecoveryEventArgs class
        /// </summary>
        public TimerAutoRecoveryEventArgs(TimerRecoveryResult recoveryResult, TimerErrorEventArgs errorEvent)
        {
            RecoveryResult = recoveryResult ?? throw new ArgumentNullException(nameof(recoveryResult));
            ErrorEvent = errorEvent ?? throw new ArgumentNullException(nameof(errorEvent));
        }
    }
}