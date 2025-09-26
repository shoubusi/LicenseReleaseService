using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LicenseReleaseService.VersionManagement.Monitoring
{
    /// <summary>
    /// Represents an alert
    /// </summary>
    public class VersionAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public AlertCategory Category { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime? ResolvedTimestamp { get; set; }
        public bool IsResolved { get; set; }
        public string? ResolvedBy { get; set; }
        public string? ResolutionNotes { get; set; }
        public Dictionary<string, string> Context { get; set; } = new();
        public int OccurrenceCount { get; set; }
        public DateTime? LastOccurrence { get; set; }
        public TimeSpan EscalationTimeout { get; set; }
        public AlertStatus Status { get; set; }
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Informational,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Alert categories
    /// </summary>
    public enum AlertCategory
    {
        Deployment,
        Health,
        Performance,
        Security,
        License,
        System,
        Custom
    }

    /// <summary>
    /// Alert status
    /// </summary>
    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Escalated,
        Resolved,
        Suppressed
    }

    /// <summary>
    /// Represents alert management options
    /// </summary>
    public class VersionAlertManagerOptions
    {
        public bool Enabled { get; set; } = true;
        public int MaxActiveAlerts { get; set; } = 100;
        public int MaxAlertHistory { get; set; } = 10000;
        public TimeSpan DefaultEscalationTimeout { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan AlertSuppressionDuration { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableAutoEscalation { get; set; } = true;
        public bool EnableAutoResolution { get; set; } = true;
        public bool EnableAlertDeduplication { get; set; } = true;
        public Dictionary<AlertSeverity, int> SeverityThresholds { get; set; } = new();
        public Dictionary<string, TimeSpan> CategorySuppressionTimes { get; set; } = new();
        public List<string> NotificationChannels { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for alert events
    /// </summary>
    public class VersionAlertEventArgs : EventArgs
    {
        public VersionAlert Alert { get; set; } = new();
        public DateTime EventTime { get; set; }
        public string? TriggeredBy { get; set; }
    }

    /// <summary>
    /// Represents an alert rule
    /// </summary>
    public class AlertRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public AlertCategory Category { get; set; }
        public string Condition { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public TimeSpan SuppressionDuration { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
    }

    /// <summary>
    /// Represents an escalation policy
    /// </summary>
    public class EscalationPolicy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public List<EscalationLevel> Levels { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public TimeSpan Timeout { get; set; }
    }

    /// <summary>
    /// Represents an escalation level
    /// </summary>
    public class EscalationLevel
    {
        public int Level { get; set; }
        public TimeSpan Delay { get; set; }
        public List<string> NotificationTargets { get; set; } = new();
        public string NotificationMethod { get; set; } = string.Empty;
        public bool RepeatNotifications { get; set; } = true;
    }

    /// <summary>
    /// Handles alert management for version monitoring
    /// </summary>
    public class VersionAlertManager : IDisposable
    {
        private readonly ILogger<VersionAlertManager> _logger;
        private readonly VersionAlertManagerOptions _options;
        private readonly ConcurrentDictionary<string, VersionAlert> _activeAlerts;
        private readonly ConcurrentDictionary<string, VersionAlert> _alertHistory;
        private readonly ConcurrentDictionary<string, AlertRule> _alertRules;
        private readonly ConcurrentDictionary<string, EscalationPolicy> _escalationPolicies;
        private readonly ConcurrentDictionary<string, DateTime> _suppressedAlerts;
        private readonly Timer _escalationTimer;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _alertSemaphore;
        private readonly object _syncLock = new();
        private bool _isDisposed;
        private bool _isRunning;

        public event EventHandler<VersionAlertEventArgs>? AlertRaised;
        public event EventHandler<VersionAlertEventArgs>? AlertAcknowledged;
        public event EventHandler<VersionAlertEventArgs>? AlertEscalated;
        public event EventHandler<VersionAlertEventArgs>? AlertResolved;
        public event EventHandler<VersionAlertEventArgs>? AlertSuppressed;
        public event EventHandler<EventArgs>? AlertManagerStarted;
        public event EventHandler<EventArgs>? AlertManagerStopped;
        public event EventHandler<Exception>? AlertError;

        public VersionAlertManager(
            ILogger<VersionAlertManager> logger,
            IOptions<VersionAlertManagerOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _activeAlerts = new ConcurrentDictionary<string, VersionAlert>();
            _alertHistory = new ConcurrentDictionary<string, VersionAlert>();
            _alertRules = new ConcurrentDictionary<string, AlertRule>();
            _escalationPolicies = new ConcurrentDictionary<string, EscalationPolicy>();
            _suppressedAlerts = new ConcurrentDictionary<string, DateTime>();
            _alertSemaphore = new SemaphoreSlim(1, 1);
            _escalationTimer = new Timer(OnEscalationTimer, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new Timer(OnCleanupTimer, null, Timeout.Infinite, Timeout.Infinite);

            InitializeDefaultRules();
            InitializeDefaultPolicies();
            InitializeDefaultThresholds();
        }

        public bool IsRunning => _isRunning;

        public IEnumerable<VersionAlert> ActiveAlerts => _activeAlerts.Values;

        public IEnumerable<VersionAlert> AlertHistory => _alertHistory.Values.OrderByDescending(a => a.Timestamp);

        public IEnumerable<AlertRule> AlertRules => _alertRules.Values;

        public IEnumerable<EscalationPolicy> EscalationPolicies => _escalationPolicies.Values;

        public async Task<VersionAlertManagerStartResult> StartAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (_isRunning)
                return new VersionAlertManagerStartResult { Success = true, Message = "Alert manager already running" };

            try
            {
                await _alertSemaphore.WaitAsync().ConfigureAwait(false);

                if (_isRunning)
                    return new VersionAlertManagerStartResult { Success = true, Message = "Alert manager already running" };

                _isRunning = true;
                _escalationTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                _cleanupTimer.Change(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

                AlertManagerStarted?.Invoke(this, EventArgs.Empty);

                _logger.LogInformation("Alert manager started");

                return new VersionAlertManagerStartResult
                {
                    Success = true,
                    Message = "Alert manager started successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting alert manager");
                AlertError?.Invoke(this, ex);
                return new VersionAlertManagerStartResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _alertSemaphore.Release();
            }
        }

        public async Task<VersionAlertManagerStopResult> StopAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (!_isRunning)
                return new VersionAlertManagerStopResult { Success = true, Message = "Alert manager not running" };

            try
            {
                await _alertSemaphore.WaitAsync().ConfigureAwait(false);

                if (!_isRunning)
                    return new VersionAlertManagerStopResult { Success = true, Message = "Alert manager not running" };

                _isRunning = false;
                _escalationTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);

                AlertManagerStopped?.Invoke(this, EventArgs.Empty);

                _logger.LogInformation("Alert manager stopped");

                return new VersionAlertManagerStopResult
                {
                    Success = true,
                    Message = "Alert manager stopped successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping alert manager");
                AlertError?.Invoke(this, ex);
                return new VersionAlertManagerStopResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _alertSemaphore.Release();
            }
        }

        public async Task<VersionAlertRaiseResult> RaiseAlertAsync(
            string version,
            string title,
            string description,
            AlertSeverity severity,
            AlertCategory category,
            Dictionary<string, string>? context = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));

            try
            {
                // Check if alert is suppressed
                if (IsAlertSuppressed(version, title))
                {
                    _logger.LogDebug("Alert suppressed for version {Version}: {Title}", version, title);
                    return new VersionAlertRaiseResult
                    {
                        Success = true,
                        Message = "Alert suppressed",
                        IsSuppressed = true
                    };
                }

                // Check for duplicate alerts if deduplication is enabled
                if (_options.EnableAlertDeduplication)
                {
                    var existingAlert = FindDuplicateAlert(version, title, severity, category);
                    if (existingAlert != null)
                    {
                        existingAlert.OccurrenceCount++;
                        existingAlert.LastOccurrence = DateTime.UtcNow;
                        return new VersionAlertRaiseResult
                        {
                            Success = true,
                            Message = "Alert deduplicated",
                            Alert = existingAlert,
                            IsDuplicate = true
                        };
                    }
                }

                // Create new alert
                var alert = new VersionAlert
                {
                    Version = version,
                    Title = title,
                    Description = description,
                    Severity = severity,
                    Category = category,
                    Timestamp = DateTime.UtcNow,
                    Status = AlertStatus.Active,
                    OccurrenceCount = 1,
                    LastOccurrence = DateTime.UtcNow,
                    EscalationTimeout = _options.DefaultEscalationTimeout,
                    Context = context ?? new Dictionary<string, string>()
                };

                // Add to active alerts
                if (!_activeAlerts.TryAdd(alert.Id, alert))
                {
                    return new VersionAlertRaiseResult
                    {
                        Success = false,
                        Error = "Failed to add alert to active alerts"
                    };
                }

                // Check if we exceeded max active alerts
                if (_activeAlerts.Count > _options.MaxActiveAlerts)
                {
                    var oldestAlert = _activeAlerts.Values.OrderBy(a => a.Timestamp).First();
                    await ResolveAlertAsync(oldestAlert.Id, "Auto-resolved due to max active alerts limit").ConfigureAwait(false);
                }

                // Raise event
                AlertRaised?.Invoke(this, new VersionAlertEventArgs
                {
                    Alert = alert,
                    EventTime = DateTime.UtcNow,
                    TriggeredBy = "System"
                });

                _logger.LogWarning(
                    "Alert raised for version {Version}: {Title} ({Severity}, {Category})",
                    version, title, severity, category);

                return new VersionAlertRaiseResult
                {
                    Success = true,
                    Message = "Alert raised successfully",
                    Alert = alert
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising alert for version {Version}", version);
                AlertError?.Invoke(this, ex);
                return new VersionAlertRaiseResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<VersionAlertAcknowledgeResult> AcknowledgeAlertAsync(string alertId, string acknowledgedBy)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (string.IsNullOrWhiteSpace(alertId))
                throw new ArgumentException("Alert ID cannot be null or empty", nameof(alertId));
            if (string.IsNullOrWhiteSpace(acknowledgedBy))
                throw new ArgumentException("Acknowledged by cannot be null or empty", nameof(acknowledgedBy));

            try
            {
                if (!_activeAlerts.TryGetValue(alertId, out var alert))
                {
                    return new VersionAlertAcknowledgeResult
                    {
                        Success = false,
                        Error = "Alert not found or not active"
                    };
                }

                alert.Status = AlertStatus.Acknowledged;

                AlertAcknowledged?.Invoke(this, new VersionAlertEventArgs
                {
                    Alert = alert,
                    EventTime = DateTime.UtcNow,
                    TriggeredBy = acknowledgedBy
                });

                _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);

                return new VersionAlertAcknowledgeResult
                {
                    Success = true,
                    Message = "Alert acknowledged successfully",
                    Alert = alert
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acknowledging alert {AlertId}", alertId);
                AlertError?.Invoke(this, ex);
                return new VersionAlertAcknowledgeResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<VersionAlertResolveResult> ResolveAlertAsync(string alertId, string resolutionNotes, string? resolvedBy = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (string.IsNullOrWhiteSpace(alertId))
                throw new ArgumentException("Alert ID cannot be null or empty", nameof(alertId));
            if (string.IsNullOrWhiteSpace(resolutionNotes))
                throw new ArgumentException("Resolution notes cannot be null or empty", nameof(resolutionNotes));

            try
            {
                if (!_activeAlerts.TryRemove(alertId, out var alert))
                {
                    return new VersionAlertResolveResult
                    {
                        Success = false,
                        Error = "Alert not found or not active"
                    };
                }

                alert.IsResolved = true;
                alert.ResolvedTimestamp = DateTime.UtcNow;
                alert.ResolvedBy = resolvedBy;
                alert.ResolutionNotes = resolutionNotes;
                alert.Status = AlertStatus.Resolved;

                // Add to history
                _alertHistory.TryAdd(alertId, alert);

                // Remove from suppression if present
                _suppressedAlerts.TryRemove(GetSuppressionKey(alert.Version, alert.Title), out _);

                AlertResolved?.Invoke(this, new VersionAlertEventArgs
                {
                    Alert = alert,
                    EventTime = DateTime.UtcNow,
                    TriggeredBy = resolvedBy ?? "System"
                });

                _logger.LogInformation("Alert {AlertId} resolved by {User}: {Notes}", alertId, resolvedBy ?? "System", resolutionNotes);

                return new VersionAlertResolveResult
                {
                    Success = true,
                    Message = "Alert resolved successfully",
                    Alert = alert
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
                AlertError?.Invoke(this, ex);
                return new VersionAlertResolveResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<VersionAlertSuppressResult> SuppressAlertAsync(string version, string title, TimeSpan? duration = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));

            try
            {
                var suppressionKey = GetSuppressionKey(version, title);
                var suppressionDuration = duration ?? _options.AlertSuppressionDuration;
                var suppressionTime = DateTime.UtcNow.Add(suppressionDuration);

                _suppressedAlerts[suppressionKey] = suppressionTime;

                // Find and suppress active alerts
                var suppressedCount = 0;
                foreach (var alert in _activeAlerts.Values.Where(a => a.Version == version && a.Title == title))
                {
                    alert.Status = AlertStatus.Suppressed;
                    suppressedCount++;

                    AlertSuppressed?.Invoke(this, new VersionAlertEventArgs
                    {
                        Alert = alert,
                        EventTime = DateTime.UtcNow,
                        TriggeredBy = "System"
                    });
                }

                _logger.LogInformation("Suppressed {Count} alerts for version {Version}: {Title}", suppressedCount, version, title);

                return new VersionAlertSuppressResult
                {
                    Success = true,
                    Message = $"Suppressed {suppressedCount} alerts successfully",
                    SuppressedCount = suppressedCount,
                    SuppressionDuration = suppressionDuration
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suppressing alerts for version {Version}", version);
                AlertError?.Invoke(this, ex);
                return new VersionAlertSuppressResult { Success = false, Error = ex.Message };
            }
        }

        public VersionAlertGetActiveResult GetActiveAlerts(string? version = null, AlertSeverity? severity = null, AlertCategory? category = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            try
            {
                var alerts = _activeAlerts.Values.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(version))
                {
                    alerts = alerts.Where(a => a.Version == version);
                }

                if (severity.HasValue)
                {
                    alerts = alerts.Where(a => a.Severity == severity.Value);
                }

                if (category.HasValue)
                {
                    alerts = alerts.Where(a => a.Category == category.Value);
                }

                return new VersionAlertGetActiveResult
                {
                    Success = true,
                    Alerts = alerts.OrderByDescending(a => a.Timestamp).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active alerts");
                AlertError?.Invoke(this, ex);
                return new VersionAlertGetActiveResult { Success = false, Error = ex.Message };
            }
        }

        public VersionAlertGetHistoryResult GetAlertHistory(string? version = null, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            try
            {
                var alerts = _alertHistory.Values.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(version))
                {
                    alerts = alerts.Where(a => a.Version == version);
                }

                if (startTime.HasValue)
                {
                    alerts = alerts.Where(a => a.Timestamp >= startTime.Value);
                }

                if (endTime.HasValue)
                {
                    alerts = alerts.Where(a => a.Timestamp <= endTime.Value);
                }

                return new VersionAlertGetHistoryResult
                {
                    Success = true,
                    Alerts = alerts.OrderByDescending(a => a.Timestamp).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alert history");
                AlertError?.Invoke(this, ex);
                return new VersionAlertGetHistoryResult { Success = false, Error = ex.Message };
            }
        }

        public VersionAlertGetSummaryResult GetAlertSummary()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            try
            {
                var summary = new Dictionary<string, int>();

                // Count by severity
                foreach (var severity in Enum.GetValues<AlertSeverity>())
                {
                    summary[$"{severity}_active"] = _activeAlerts.Values.Count(a => a.Severity == severity);
                    summary[$"{severity}_total"] = _alertHistory.Values.Count(a => a.Severity == severity);
                }

                // Count by category
                foreach (var category in Enum.GetValues<AlertCategory>())
                {
                    summary[$"{category}_active"] = _activeAlerts.Values.Count(a => a.Category == category);
                    summary[$"{category}_total"] = _alertHistory.Values.Count(a => a.Category == category);
                }

                // Overall counts
                summary["total_active"] = _activeAlerts.Count;
                summary["total_resolved"] = _alertHistory.Values.Count(a => a.IsResolved);
                summary["total_suppressed"] = _activeAlerts.Values.Count(a => a.Status == AlertStatus.Suppressed);

                return new VersionAlertGetSummaryResult
                {
                    Success = true,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alert summary");
                AlertError?.Invoke(this, ex);
                return new VersionAlertGetSummaryResult { Success = false, Error = ex.Message };
            }
        }

        public VersionAlertAddRuleResult AddAlertRule(AlertRule rule)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            try
            {
                if (string.IsNullOrWhiteSpace(rule.Name))
                    throw new ArgumentException("Rule name cannot be null or empty", nameof(rule));

                rule.CreatedAt = DateTime.UtcNow;
                if (!_alertRules.TryAdd(rule.Id, rule))
                {
                    return new VersionAlertAddRuleResult
                    {
                        Success = false,
                        Error = "Failed to add alert rule"
                    };
                }

                _logger.LogInformation("Added alert rule: {Name}", rule.Name);

                return new VersionAlertAddRuleResult
                {
                    Success = true,
                    Message = "Alert rule added successfully",
                    Rule = rule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding alert rule");
                AlertError?.Invoke(this, ex);
                return new VersionAlertAddRuleResult { Success = false, Error = ex.Message };
            }
        }

        public VersionAlertRemoveRuleResult RemoveAlertRule(string ruleId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionAlertManager));

            if (string.IsNullOrWhiteSpace(ruleId))
                throw new ArgumentException("Rule ID cannot be null or empty", nameof(ruleId));

            try
            {
                if (!_alertRules.TryRemove(ruleId, out _))
                {
                    return new VersionAlertRemoveRuleResult
                    {
                        Success = false,
                        Error = "Rule not found"
                    };
                }

                _logger.LogInformation("Removed alert rule: {RuleId}", ruleId);

                return new VersionAlertRemoveRuleResult
                {
                    Success = true,
                    Message = "Alert rule removed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing alert rule");
                AlertError?.Invoke(this, ex);
                return new VersionAlertRemoveRuleResult { Success = false, Error = ex.Message };
            }
        }

        private async void OnEscalationTimer(object? state)
        {
            if (!_isRunning)
                return;

            try
            {
                var now = DateTime.UtcNow;
                var alertsToEscalate = _activeAlerts.Values
                    .Where(a => a.Status == AlertStatus.Active &&
                               now - a.Timestamp > a.EscalationTimeout)
                    .ToList();

                foreach (var alert in alertsToEscalate)
                {
                    await EscalateAlertAsync(alert).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in escalation timer");
                AlertError?.Invoke(this, ex);
            }
        }

        private async void OnCleanupTimer(object? state)
        {
            if (!_isRunning)
                return;

            try
            {
                var now = DateTime.UtcNow;

                // Clean up suppressed alerts
                var expiredSuppressions = _suppressedAlerts
                    .Where(s => s.Value <= now)
                    .ToList();

                foreach (var suppression in expiredSuppressions)
                {
                    _suppressedAlerts.TryRemove(suppression.Key, out _);
                }

                // Clean up alert history if too large
                if (_alertHistory.Count > _options.MaxAlertHistory)
                {
                    var oldestAlerts = _alertHistory.Values
                        .OrderBy(a => a.Timestamp)
                        .Take(_alertHistory.Count - _options.MaxAlertHistory)
                        .ToList();

                    foreach (var alert in oldestAlerts)
                    {
                        _alertHistory.TryRemove(alert.Id, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup timer");
                AlertError?.Invoke(this, ex);
            }
        }

        private async Task EscalateAlertAsync(VersionAlert alert)
        {
            try
            {
                alert.Status = AlertStatus.Escalated;

                AlertEscalated?.Invoke(this, new VersionAlertEventArgs
                {
                    Alert = alert,
                    EventTime = DateTime.UtcNow,
                    TriggeredBy = "System"
                });

                _logger.LogWarning("Alert escalated: {AlertId} - {Title}", alert.Id, alert.Title);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error escalating alert {AlertId}", alert.Id);
                AlertError?.Invoke(this, ex);
            }
        }

        private bool IsAlertSuppressed(string version, string title)
        {
            var suppressionKey = GetSuppressionKey(version, title);
            return _suppressedAlerts.TryGetValue(suppressionKey, out var suppressionTime) &&
                   suppressionTime > DateTime.UtcNow;
        }

        private VersionAlert? FindDuplicateAlert(string version, string title, AlertSeverity severity, AlertCategory category)
        {
            return _activeAlerts.Values.FirstOrDefault(a =>
                a.Version == version &&
                a.Title == title &&
                a.Severity == severity &&
                a.Category == category &&
                !a.IsResolved);
        }

        private string GetSuppressionKey(string version, string title) => $"{version}:{title}";

        private void InitializeDefaultRules()
        {
            // Add default alert rules
            var defaultRules = new[]
            {
                new AlertRule
                {
                    Name = "High License Utilization",
                    Description = "License utilization exceeds threshold",
                    Severity = AlertSeverity.Warning,
                    Category = AlertCategory.License,
                    Condition = "license_utilization > 0.9",
                    SuppressionDuration = TimeSpan.FromMinutes(30)
                },
                new AlertRule
                {
                    Name = "Deployment Failure",
                    Description = "Version deployment failed",
                    Severity = AlertSeverity.Error,
                    Category = AlertCategory.Deployment,
                    Condition = "deployment_status == 'failed'",
                    SuppressionDuration = TimeSpan.FromMinutes(15)
                },
                new AlertRule
                {
                    Name = "Health Check Failure",
                    Description = "Version health check failed",
                    Severity = AlertSeverity.Critical,
                    Category = AlertCategory.Health,
                    Condition = "health_status == 'unhealthy'",
                    SuppressionDuration = TimeSpan.FromMinutes(5)
                }
            };

            foreach (var rule in defaultRules)
            {
                _alertRules.TryAdd(rule.Id, rule);
            }
        }

        private void InitializeDefaultPolicies()
        {
            // Add default escalation policies
            var defaultPolicy = new EscalationPolicy
            {
                Name = "Default Escalation Policy",
                Timeout = TimeSpan.FromHours(1),
                Levels = new List<EscalationLevel>
                {
                    new EscalationLevel
                    {
                        Level = 1,
                        Delay = TimeSpan.FromMinutes(30),
                        NotificationTargets = new List<string> { "team-lead" },
                        NotificationMethod = "email"
                    },
                    new EscalationLevel
                    {
                        Level = 2,
                        Delay = TimeSpan.FromMinutes(60),
                        NotificationTargets = new List<string> { "manager" },
                        NotificationMethod = "sms"
                    }
                }
            };

            _escalationPolicies.TryAdd(defaultPolicy.Id, defaultPolicy);
        }

        private void InitializeDefaultThresholds()
        {
            if (!_options.SeverityThresholds.Any())
            {
                _options.SeverityThresholds[AlertSeverity.Critical] = 1;
                _options.SeverityThresholds[AlertSeverity.Error] = 5;
                _options.SeverityThresholds[AlertSeverity.Warning] = 10;
                _options.SeverityThresholds[AlertSeverity.Informational] = 20;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _escalationTimer?.Dispose();
                    _cleanupTimer?.Dispose();
                    _alertSemaphore?.Dispose();
                }
                _isDisposed = true;
            }
        }
    }

    // Result classes
    public class VersionAlertManagerStartResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class VersionAlertManagerStopResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class VersionAlertRaiseResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public VersionAlert? Alert { get; set; }
        public bool IsSuppressed { get; set; }
        public bool IsDuplicate { get; set; }
    }

    public class VersionAlertAcknowledgeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public VersionAlert? Alert { get; set; }
    }

    public class VersionAlertResolveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public VersionAlert? Alert { get; set; }
    }

    public class VersionAlertSuppressResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int SuppressedCount { get; set; }
        public TimeSpan SuppressionDuration { get; set; }
    }

    public class VersionAlertGetActiveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public List<VersionAlert> Alerts { get; set; } = new();
    }

    public class VersionAlertGetHistoryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public List<VersionAlert> Alerts { get; set; } = new();
    }

    public class VersionAlertGetSummaryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public Dictionary<string, int> Summary { get; set; } = new();
    }

    public class VersionAlertAddRuleResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public AlertRule? Rule { get; set; }
    }

    public class VersionAlertRemoveRuleResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}