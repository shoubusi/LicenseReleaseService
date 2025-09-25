using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService
{
    public enum ServiceStateStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Paused,
        Pausing,
        Continuing,
        Error
    }

    public class ServiceState
    {
        private readonly object _lock = new object();
        private ServiceStateStatus _status;
        private DateTime _lastStateChange;
        private DateTime _startTime;
        private string _lastError;
        private int _errorCount;
        private readonly Queue<ServiceStateTransition> _transitionHistory;
        private readonly int _maxHistorySize = 100;

        // Configuration state tracking
        private ConfigurationHealthStatus _configurationHealthStatus = ConfigurationHealthStatus.Unknown;
        private DateTime _lastConfigurationChange;
        private int _configurationReloadCount;
        private readonly List<string> _configurationIssues = new List<string>();
        private DateTime _lastConfigurationHealthCheck;

        public ServiceState()
        {
            _status = ServiceStateStatus.Stopped;
            _lastStateChange = DateTime.UtcNow;
            _startTime = DateTime.UtcNow;
            _lastConfigurationChange = DateTime.UtcNow;
            _lastConfigurationHealthCheck = DateTime.UtcNow;
            _transitionHistory = new Queue<ServiceStateTransition>();
            RecordTransition(ServiceStateStatus.Stopped, "Service initialized");
        }

        public ServiceStateStatus Status
        {
            get
            {
                lock (_lock)
                {
                    return _status;
                }
            }
        }

        public DateTime LastStateChange
        {
            get
            {
                lock (_lock)
                {
                    return _lastStateChange;
                }
            }
        }

        public DateTime StartTime
        {
            get
            {
                lock (_lock)
                {
                    return _startTime;
                }
            }
        }

        public string LastError
        {
            get
            {
                lock (_lock)
                {
                    return _lastError;
                }
            }
        }

        public int ErrorCount
        {
            get
            {
                lock (_lock)
                {
                    return _errorCount;
                }
            }
        }

        public TimeSpan CurrentStateDuration
        {
            get
            {
                lock (_lock)
                {
                    return DateTime.UtcNow - _lastStateChange;
                }
            }
        }

        public TimeSpan TotalRunTime
        {
            get
            {
                lock (_lock)
                {
                    if (_status == ServiceStateStatus.Stopped)
                        return TimeSpan.Zero;

                    return DateTime.UtcNow - _startTime;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _status == ServiceStateStatus.Running;
                }
            }
        }

        public bool CanStop
        {
            get
            {
                lock (_lock)
                {
                    return _status == ServiceStateStatus.Running || _status == ServiceStateStatus.Paused;
                }
            }
        }

        public bool CanPause
        {
            get
            {
                lock (_lock)
                {
                    return _status == ServiceStateStatus.Running;
                }
            }
        }

        public bool CanContinue
        {
            get
            {
                lock (_lock)
                {
                    return _status == ServiceStateStatus.Paused;
                }
            }
        }

        // Configuration state properties
        public ConfigurationHealthStatus ConfigurationHealthStatus
        {
            get
            {
                lock (_lock)
                {
                    return _configurationHealthStatus;
                }
            }
        }

        public DateTime LastConfigurationChange
        {
            get
            {
                lock (_lock)
                {
                    return _lastConfigurationChange;
                }
            }
        }

        public int ConfigurationReloadCount
        {
            get
            {
                lock (_lock)
                {
                    return _configurationReloadCount;
                }
            }
        }

        public IReadOnlyList<string> ConfigurationIssues
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_configurationIssues);
                }
            }
        }

        public bool IsConfigurationHealthy
        {
            get
            {
                lock (_lock)
                {
                    return _configurationHealthStatus == ConfigurationHealthStatus.Healthy;
                }
            }
        }

        public void ChangeState(ServiceStateStatus newState, string reason = null)
        {
            lock (_lock)
            {
                if (_status == newState)
                    return;

                var oldState = _status;
                _status = newState;
                _lastStateChange = DateTime.UtcNow;

                if (newState == ServiceStateStatus.Starting)
                {
                    _startTime = DateTime.UtcNow;
                }
                else if (newState == ServiceStateStatus.Error)
                {
                    _errorCount++;
                    if (!string.IsNullOrEmpty(reason))
                    {
                        _lastError = reason;
                    }
                }

                RecordTransition(oldState, newState, reason);
            }
        }

        public void RecordError(string errorMessage, Exception exception = null)
        {
            lock (_lock)
            {
                _errorCount++;
                _lastError = errorMessage;

                var errorDetails = exception != null
                    ? $"{errorMessage}: {exception.Message}"
                    : errorMessage;

                RecordTransition(_status, ServiceStateStatus.Error, errorDetails);
            }
        }

        public void ResetError()
        {
            lock (_lock)
            {
                if (_status == ServiceStateStatus.Error)
                {
                    _status = ServiceStateStatus.Stopped;
                    _lastStateChange = DateTime.UtcNow;
                    RecordTransition(ServiceStateStatus.Error, ServiceStateStatus.Stopped, "Error state reset");
                }
            }
        }

        // Configuration state methods
        public void UpdateConfigurationHealth(ConfigurationHealthStatus healthStatus, List<string> issues = null)
        {
            lock (_lock)
            {
                _configurationHealthStatus = healthStatus;
                _lastConfigurationHealthCheck = DateTime.UtcNow;

                // Update configuration issues
                _configurationIssues.Clear();
                if (issues != null)
                {
                    _configurationIssues.AddRange(issues);
                }

                // Record the configuration health change
                RecordTransition(_status, _status, $"Configuration health changed to {healthStatus}");
            }
        }

        public void RecordConfigurationReload(string reason = null)
        {
            lock (_lock)
            {
                _configurationReloadCount++;
                _lastConfigurationChange = DateTime.UtcNow;

                // Record the configuration reload
                RecordTransition(_status, _status, $"Configuration reloaded: {reason ?? "Unknown reason"}");
            }
        }

        public void AddConfigurationIssue(string issue)
        {
            lock (_lock)
            {
                if (!_configurationIssues.Contains(issue))
                {
                    _configurationIssues.Add(issue);
                }

                // Update health status if issues are added
                if (_configurationIssues.Count > 0 && _configurationHealthStatus == ConfigurationHealthStatus.Healthy)
                {
                    _configurationHealthStatus = ConfigurationHealthStatus.Warning;
                }
            }
        }

        public void ClearConfigurationIssues()
        {
            lock (_lock)
            {
                _configurationIssues.Clear();
                _configurationHealthStatus = ConfigurationHealthStatus.Healthy;
                _lastConfigurationHealthCheck = DateTime.UtcNow;
            }
        }

        public List<ServiceStateTransition> GetTransitionHistory(int count = 10)
        {
            lock (_lock)
            {
                return new List<ServiceStateTransition>(_transitionHistory.ToArray()).Take(count).ToList();
            }
        }

        public ServiceMetrics GetMetrics()
        {
            lock (_lock)
            {
                return new ServiceMetrics
                {
                    CurrentState = _status,
                    CurrentStateDuration = CurrentStateDuration,
                    TotalRunTime = TotalRunTime,
                    ErrorCount = _errorCount,
                    LastError = _lastError,
                    LastStateChange = _lastStateChange,
                    StartTime = _startTime,
                    TransitionCount = _transitionHistory.Count,

                    // Configuration metrics
                    ConfigurationHealthStatus = _configurationHealthStatus,
                    LastConfigurationChange = _lastConfigurationChange,
                    ConfigurationReloadCount = _configurationReloadCount,
                    ConfigurationIssues = new List<string>(_configurationIssues),
                    IsConfigurationHealthy = IsConfigurationHealthy
                };
            }
        }

        private void RecordTransition(ServiceStateStatus fromState, ServiceStateStatus toState, string reason = null)
        {
            var transition = new ServiceStateTransition
            {
                FromState = fromState,
                ToState = toState,
                Timestamp = DateTime.UtcNow,
                Reason = reason ?? $"State change from {fromState} to {toState}"
            };

            _transitionHistory.Enqueue(transition);

            while (_transitionHistory.Count > _maxHistorySize)
            {
                _transitionHistory.Dequeue();
            }
        }

        private void RecordTransition(ServiceStateStatus state, string reason)
        {
            RecordTransition(state, state, reason);
        }
    }

    public class ServiceStateTransition
    {
        public ServiceStateStatus FromState { get; set; }
        public ServiceStateStatus ToState { get; set; }
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; }
    }

    public class ServiceMetrics
    {
        public ServiceStateStatus CurrentState { get; set; }
        public TimeSpan CurrentStateDuration { get; set; }
        public TimeSpan TotalRunTime { get; set; }
        public int ErrorCount { get; set; }
        public string LastError { get; set; }
        public DateTime LastStateChange { get; set; }
        public DateTime StartTime { get; set; }
        public int TransitionCount { get; set; }

        // Configuration metrics
        public ConfigurationHealthStatus ConfigurationHealthStatus { get; set; }
        public DateTime LastConfigurationChange { get; set; }
        public int ConfigurationReloadCount { get; set; }
        public List<string> ConfigurationIssues { get; set; }
        public bool IsConfigurationHealthy { get; set; }
    }

    public static class ServiceStateExtensions
    {
        public static string ToFriendlyString(this ServiceStateStatus status)
        {
            return status switch
            {
                ServiceStateStatus.Stopped => "Stopped",
                ServiceStateStatus.Starting => "Starting",
                ServiceStateStatus.Running => "Running",
                ServiceStateStatus.Stopping => "Stopping",
                ServiceStateStatus.Paused => "Paused",
                ServiceStateStatus.Pausing => "Pausing",
                ServiceStateStatus.Continuing => "Continuing",
                ServiceStateStatus.Error => "Error",
                _ => "Unknown"
            };
        }

        public static bool IsTerminalState(this ServiceStateStatus status)
        {
            return status == ServiceStateStatus.Stopped || status == ServiceStateStatus.Error;
        }

        public static bool IsTransitionState(this ServiceStateStatus status)
        {
            return status == ServiceStateStatus.Starting ||
                   status == ServiceStateStatus.Stopping ||
                   status == ServiceStateStatus.Pausing ||
                   status == ServiceStateStatus.Continuing;
        }
    }
}