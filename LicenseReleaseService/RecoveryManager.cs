using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService
{
    public enum RecoveryAction
    {
        None,
        RestartService,
        RestartComponents,
        ClearCache,
        LogAndContinue,
        GracefulShutdown
    }

    public class RecoveryManager
    {
        private readonly object _lock = new object();
        private readonly ServiceState _serviceState;
        private readonly ILogger _logger;
        private readonly Dictionary<Type, RecoveryStrategy> _recoveryStrategies;
        private readonly Queue<RecoveryEvent> _recoveryHistory;
        private readonly int _maxHistorySize = 100;
        private readonly Timer _recoveryTimer;
        private bool _recoveryInProgress;
        private int _consecutiveErrors;
        private DateTime _lastErrorTime;
        private readonly int _maxConsecutiveErrors = 5;
        private readonly TimeSpan _errorWindow = TimeSpan.FromMinutes(5);

        public RecoveryManager(ServiceState serviceState, ILogger logger)
        {
            _serviceState = serviceState;
            _logger = logger;
            _recoveryStrategies = new Dictionary<Type, RecoveryStrategy>();
            _recoveryHistory = new Queue<RecoveryEvent>();
            _recoveryTimer = new Timer(CheckRecoveryConditions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            InitializeDefaultStrategies();
        }

        public bool RecoveryInProgress
        {
            get
            {
                lock (_lock)
                {
                    return _recoveryInProgress;
                }
            }
        }

        public int ConsecutiveErrors
        {
            get
            {
                lock (_lock)
                {
                    return _consecutiveErrors;
                }
            }
        }

        public List<RecoveryEvent> GetRecoveryHistory(int count = 10)
        {
            lock (_lock)
            {
                return new List<RecoveryEvent>(_recoveryHistory.ToArray()).Take(count).ToList();
            }
        }

        public void RegisterRecoveryStrategy(Type exceptionType, RecoveryStrategy strategy)
        {
            lock (_lock)
            {
                _recoveryStrategies[exceptionType] = strategy;
            }
        }

        public async Task<RecoveryResult> HandleExceptionAsync(Exception exception, string context = null)
        {
            lock (_lock)
            {
                if (_recoveryInProgress)
                {
                    return new RecoveryResult
                    {
                        Success = false,
                        Action = RecoveryAction.None,
                        Message = "Recovery already in progress"
                    };
                }

                _recoveryInProgress = true;
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
            }

            try
            {
                _logger.LogError($"Exception in {context}: {exception.Message}", exception);

                var strategy = GetRecoveryStrategy(exception);
                var result = await ExecuteRecoveryActionAsync(strategy, exception, context);

                RecordRecoveryEvent(exception, context, result);

                return result;
            }
            catch (Exception recoveryEx)
            {
                _logger.LogError($"Recovery failed: {recoveryEx.Message}", recoveryEx);

                var failureResult = new RecoveryResult
                {
                    Success = false,
                    Action = RecoveryAction.None,
                    Message = $"Recovery failed: {recoveryEx.Message}"
                };

                RecordRecoveryEvent(exception, context, failureResult);

                return failureResult;
            }
            finally
            {
                lock (_lock)
                {
                    _recoveryInProgress = false;
                }
            }
        }

        public RecoveryStrategy GetRecoveryStrategy(Exception exception)
        {
            var exceptionType = exception.GetType();

            // Look for specific strategy first
            if (_recoveryStrategies.TryGetValue(exceptionType, out var strategy))
            {
                return strategy;
            }

            // Look for base exception types
            foreach (var kvp in _recoveryStrategies)
            {
                if (kvp.Key.IsAssignableFrom(exceptionType))
                {
                    return kvp.Value;
                }
            }

            // Use default strategy based on exception type
            return GetDefaultStrategy(exception);
        }

        private RecoveryStrategy GetDefaultStrategy(Exception exception)
        {
            if (exception is OutOfMemoryException)
            {
                return new RecoveryStrategy
                {
                    Action = RecoveryAction.GracefulShutdown,
                    MaxAttempts = 1,
                    DelayBetweenAttempts = TimeSpan.Zero,
                    Description = "Out of memory - requiring graceful shutdown"
                };
            }

            if (exception is StackOverflowException)
            {
                return new RecoveryStrategy
                {
                    Action = RecoveryAction.GracefulShutdown,
                    MaxAttempts = 1,
                    DelayBetweenAttempts = TimeSpan.Zero,
                    Description = "Stack overflow - requiring graceful shutdown"
                };
            }

            if (exception is SystemException)
            {
                return new RecoveryStrategy
                {
                    Action = RecoveryAction.RestartService,
                    MaxAttempts = 3,
                    DelayBetweenAttempts = TimeSpan.FromSeconds(30),
                    Description = "System exception - attempting service restart"
                };
            }

            if (exception is InvalidOperationException)
            {
                return new RecoveryStrategy
                {
                    Action = RecoveryAction.RestartComponents,
                    MaxAttempts = 2,
                    DelayBetweenAttempts = TimeSpan.FromSeconds(10),
                    Description = "Invalid operation - attempting component restart"
                };
            }

            if (exception is TimeoutException)
            {
                return new RecoveryStrategy
                {
                    Action = RecoveryAction.ClearCache,
                    MaxAttempts = 3,
                    DelayBetweenAttempts = TimeSpan.FromSeconds(5),
                    Description = "Timeout - attempting cache clear"
                };
            }

            return new RecoveryStrategy
            {
                Action = RecoveryAction.LogAndContinue,
                MaxAttempts = 1,
                DelayBetweenAttempts = TimeSpan.Zero,
                Description = "Unknown exception - logging and continuing"
            };
        }

        private async Task<RecoveryResult> ExecuteRecoveryActionAsync(RecoveryStrategy strategy, Exception exception, string context)
        {
            var attempt = 1;
            var lastException = exception;

            while (attempt <= strategy.MaxAttempts)
            {
                try
                {
                    _logger.LogInformation($"Attempting recovery action {strategy.Action} (attempt {attempt}/{strategy.MaxAttempts}) for {context}");

                    var result = await ExecuteSingleRecoveryAsync(strategy.Action, exception, context);

                    if (result.Success)
                    {
                        _logger.LogInformation($"Recovery successful for {context} using {strategy.Action}");
                        return result;
                    }

                    if (attempt < strategy.MaxAttempts)
                    {
                        _logger.LogInformation($"Recovery attempt {attempt} failed, waiting {strategy.DelayBetweenAttempts.TotalSeconds}s before retry");
                        await Task.Delay(strategy.DelayBetweenAttempts);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"Recovery attempt {attempt} failed with exception: {ex.Message}", ex);
                }

                attempt++;
            }

            return new RecoveryResult
            {
                Success = false,
                Action = strategy.Action,
                Message = $"Recovery failed after {strategy.MaxAttempts} attempts. Last error: {lastException.Message}",
                Attempts = strategy.MaxAttempts
            };
        }

        private async Task<RecoveryResult> ExecuteSingleRecoveryAsync(RecoveryAction action, Exception exception, string context)
        {
            return action switch
            {
                RecoveryAction.RestartService => await RestartServiceAsync(context),
                RecoveryAction.RestartComponents => await RestartComponentsAsync(context),
                RecoveryAction.ClearCache => await ClearCacheAsync(context),
                RecoveryAction.LogAndContinue => LogAndContinue(exception, context),
                RecoveryAction.GracefulShutdown => await GracefulShutdownAsync(context),
                RecoveryAction.None => new RecoveryResult { Success = true, Action = RecoveryAction.None, Message = "No action required" },
                _ => new RecoveryResult { Success = false, Action = action, Message = "Unknown recovery action" }
            };
        }

        private async Task<RecoveryResult> RestartServiceAsync(string context)
        {
            try
            {
                _logger.LogInformation($"Attempting service restart for {context}");

                // Stop the service
                _serviceState.ChangeState(ServiceStateStatus.Stopping, "Service restart due to error recovery");

                // Wait a moment
                await Task.Delay(TimeSpan.FromSeconds(2));

                // Start the service
                _serviceState.ChangeState(ServiceStateStatus.Starting, "Service restart after error recovery");

                // Reset error count
                lock (_lock)
                {
                    _consecutiveErrors = 0;
                }

                _serviceState.ChangeState(ServiceStateStatus.Running, "Service restart completed");

                return new RecoveryResult
                {
                    Success = true,
                    Action = RecoveryAction.RestartService,
                    Message = "Service restarted successfully"
                };
            }
            catch (Exception ex)
            {
                return new RecoveryResult
                {
                    Success = false,
                    Action = RecoveryAction.RestartService,
                    Message = $"Service restart failed: {ex.Message}"
                };
            }
        }

        private async Task<RecoveryResult> RestartComponentsAsync(string context)
        {
            try
            {
                _logger.LogInformation($"Attempting component restart for {context}");

                // Simulate component restart
                await Task.Delay(TimeSpan.FromSeconds(1));

                _logger.LogInformation("Components restarted successfully");

                return new RecoveryResult
                {
                    Success = true,
                    Action = RecoveryAction.RestartComponents,
                    Message = "Components restarted successfully"
                };
            }
            catch (Exception ex)
            {
                return new RecoveryResult
                {
                    Success = false,
                    Action = RecoveryAction.RestartComponents,
                    Message = $"Component restart failed: {ex.Message}"
                };
            }
        }

        private async Task<RecoveryResult> ClearCacheAsync(string context)
        {
            try
            {
                _logger.LogInformation($"Attempting cache clear for {context}");

                // Simulate cache clear
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                _logger.LogInformation("Cache cleared successfully");

                return new RecoveryResult
                {
                    Success = true,
                    Action = RecoveryAction.ClearCache,
                    Message = "Cache cleared successfully"
                };
            }
            catch (Exception ex)
            {
                return new RecoveryResult
                {
                    Success = false,
                    Action = RecoveryAction.ClearCache,
                    Message = $"Cache clear failed: {ex.Message}"
                };
            }
        }

        private RecoveryResult LogAndContinue(Exception exception, string context)
        {
            _logger.LogInformation($"Logged exception for {context} and continuing operation");

            return new RecoveryResult
            {
                Success = true,
                Action = RecoveryAction.LogAndContinue,
                Message = "Exception logged and continuing"
            };
        }

        private async Task<RecoveryResult> GracefulShutdownAsync(string context)
        {
            try
            {
                _logger.LogInformation($"Attempting graceful shutdown for {context}");

                _serviceState.ChangeState(ServiceStateStatus.Stopping, "Graceful shutdown due to critical error");

                // In a real service, this would trigger the actual service shutdown
                await Task.Delay(TimeSpan.FromSeconds(1));

                _serviceState.ChangeState(ServiceStateStatus.Stopped, "Graceful shutdown completed");

                return new RecoveryResult
                {
                    Success = true,
                    Action = RecoveryAction.GracefulShutdown,
                    Message = "Graceful shutdown completed"
                };
            }
            catch (Exception ex)
            {
                return new RecoveryResult
                {
                    Success = false,
                    Action = RecoveryAction.GracefulShutdown,
                    Message = $"Graceful shutdown failed: {ex.Message}"
                };
            }
        }

        private void RecordRecoveryEvent(Exception exception, string context, RecoveryResult result)
        {
            var recoveryEvent = new RecoveryEvent
            {
                Timestamp = DateTime.UtcNow,
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                Context = context,
                RecoveryAction = result.Action,
                Success = result.Success,
                Attempts = result.Attempts,
                Message = result.Message
            };

            lock (_lock)
            {
                _recoveryHistory.Enqueue(recoveryEvent);

                while (_recoveryHistory.Count > _maxHistorySize)
                {
                    _recoveryHistory.Dequeue();
                }
            }
        }

        private void CheckRecoveryConditions(object state)
        {
            lock (_lock)
            {
                // Reset consecutive errors if error window has passed
                if (_consecutiveErrors > 0 && DateTime.UtcNow - _lastErrorTime > _errorWindow)
                {
                    _consecutiveErrors = 0;
                    _logger.LogInformation("Reset consecutive error count");
                }

                // Check if we need to force recovery due to too many consecutive errors
                if (_consecutiveErrors >= _maxConsecutiveErrors && !_recoveryInProgress)
                {
                    _logger.LogWarning($"Too many consecutive errors ({_consecutiveErrors}), forcing recovery action");

                    // Force a service restart
                    Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteRecoveryActionAsync(new RecoveryStrategy
                            {
                                Action = RecoveryAction.RestartService,
                                MaxAttempts = 1,
                                DelayBetweenAttempts = TimeSpan.Zero,
                                Description = "Forced recovery due to consecutive errors"
                            }, new Exception("Forced recovery"), "automatic recovery");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Automatic recovery failed: {ex.Message}", ex);
                        }
                    });
                }
            }
        }

        private void InitializeDefaultStrategies()
        {
            // Configuration-specific recovery strategies
            RegisterRecoveryStrategy(typeof(ConfigurationErrorsException), new RecoveryStrategy
            {
                Action = RecoveryAction.LogAndContinue,
                MaxAttempts = 3,
                DelayBetweenAttempts = TimeSpan.FromSeconds(10),
                Description = "Configuration error - logging and continuing with previous configuration"
            });

            RegisterRecoveryStrategy(typeof(ConfigurationValidationException), new RecoveryStrategy
            {
                Action = RecoveryAction.RestartComponents,
                MaxAttempts = 2,
                DelayBetweenAttempts = TimeSpan.FromSeconds(5),
                Description = "Configuration validation error - attempting to restart components"
            });

            RegisterRecoveryStrategy(typeof(System.Configuration.ConfigurationException), new RecoveryStrategy
            {
                Action = RecoveryAction.ClearCache,
                MaxAttempts = 1,
                DelayBetweenAttempts = TimeSpan.Zero,
                Description = "System configuration exception - clearing cache and continuing"
            });

            // Add any custom recovery strategies here
            // For example, specific handling for database exceptions, network exceptions, etc.
        }
    }

    public class RecoveryStrategy
    {
        public RecoveryAction Action { get; set; }
        public int MaxAttempts { get; set; }
        public TimeSpan DelayBetweenAttempts { get; set; }
        public string Description { get; set; }
    }

    public class RecoveryResult
    {
        public bool Success { get; set; }
        public RecoveryAction Action { get; set; }
        public string Message { get; set; }
        public int Attempts { get; set; } = 1;
    }

    public class RecoveryEvent
    {
        public DateTime Timestamp { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string Context { get; set; }
        public RecoveryAction RecoveryAction { get; set; }
        public bool Success { get; set; }
        public int Attempts { get; set; }
        public string Message { get; set; }
    }

    public static class RecoveryActionExtensions
    {
        public static string ToFriendlyString(this RecoveryAction action)
        {
            return action switch
            {
                RecoveryAction.None => "None",
                RecoveryAction.RestartService => "Restart Service",
                RecoveryAction.RestartComponents => "Restart Components",
                RecoveryAction.ClearCache => "Clear Cache",
                RecoveryAction.LogAndContinue => "Log and Continue",
                RecoveryAction.GracefulShutdown => "Graceful Shutdown",
                _ => "Unknown"
            };
        }
    }
}