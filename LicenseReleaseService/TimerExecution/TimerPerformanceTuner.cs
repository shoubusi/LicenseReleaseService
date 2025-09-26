using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Provides dynamic performance tuning for the timer execution system
    /// </summary>
    public class TimerPerformanceTuner : IDisposable
    {
        private readonly ILogger<TimerPerformanceTuner> _logger;
        private readonly TimerPerformanceTunerOptions _options;
        private readonly TimerMetricsCollector _metricsCollector;
        private readonly TimerPerformanceOptimizer _optimizer;
        private readonly Timer _tuningTimer;
        private readonly Dictionary<string, TimerTuningRule> _tuningRules;
        private readonly object _lock = new object();
        private readonly Queue<TimerTuningAction> _tuningHistory;
        private DateTime _startTime;
        private bool _isDisposed;
        private bool _isTuning;

        /// <summary>
        /// Occurs when performance tuning is applied
        /// </summary>
        public event EventHandler<TimerTuningAppliedEventArgs>? TuningApplied;

        /// <summary>
        /// Occurs when tuning recommendations are generated
        /// </summary>
        public event EventHandler<TimerTuningRecommendationEventArgs>? TuningRecommended;

        /// <summary>
        /// Gets the tuner uptime
        /// </summary>
        public TimeSpan Uptime => DateTime.UtcNow - _startTime;

        /// <summary>
        /// Gets the current tuning configuration
        /// </summary>
        public TimerTuningConfiguration CurrentConfiguration { get; private set; }

        /// <summary>
        /// Gets the tuning statistics
        /// </summary>
        public TimerTuningStatistics TuningStatistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceTuner class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="options">The tuner options</param>
        /// <param name="metricsCollector">The metrics collector</param>
        /// <param name="optimizer">The performance optimizer</param>
        public TimerPerformanceTuner(ILogger<TimerPerformanceTuner> logger, TimerPerformanceTunerOptions options,
            TimerMetricsCollector metricsCollector, TimerPerformanceOptimizer optimizer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));

            _startTime = DateTime.UtcNow;
            _tuningRules = new Dictionary<string, TimerTuningRule>();
            _tuningHistory = new Queue<TimerTuningAction>();
            CurrentConfiguration = new TimerTuningConfiguration();
            TuningStatistics = new TimerTuningStatistics();

            _tuningTimer = new Timer(TuningCycleAsync, null, _options.TuningIntervalMs, _options.TuningIntervalMs);

            InitializeTuningRules();
            SubscribeToEvents();
        }

        /// <summary>
        /// Starts the performance tuner
        /// </summary>
        public async Task StartAsync()
        {
            if (_isTuning)
                return;

            _isTuning = true;
            _startTime = DateTime.UtcNow;

            await TuningCycleAsync(null);
            _logger.LogInformation("Performance tuner started with interval {Interval}ms", _options.TuningIntervalMs);
        }

        /// <summary>
        /// Stops the performance tuner
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isTuning)
                return;

            _isTuning = false;
            _tuningTimer.Change(Timeout.Infinite, Timeout.Infinite);

            await TuningCycleAsync(null);
            _logger.LogInformation("Performance tuner stopped after {Uptime}", Uptime);
        }

        /// <summary>
        /// Adds a custom tuning rule
        /// </summary>
        /// <param name="rule">The tuning rule to add</param>
        public void AddTuningRule(TimerTuningRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            lock (_lock)
            {
                _tuningRules[rule.Name] = rule;
            }

            _logger.LogInformation("Added tuning rule: {RuleName}", rule.Name);
        }

        /// <summary>
        /// Removes a tuning rule
        /// </summary>
        /// <param name="ruleName">The name of the rule to remove</param>
        public void RemoveTuningRule(string ruleName)
        {
            lock (_lock)
            {
                if (_tuningRules.Remove(ruleName))
                {
                    _logger.LogInformation("Removed tuning rule: {RuleName}", ruleName);
                }
            }
        }

        /// <summary>
        /// Gets tuning recommendations
        /// </summary>
        /// <returns>Collection of tuning recommendations</returns>
        public IEnumerable<TimerTuningRecommendation> GetTuningRecommendations()
        {
            var currentSnapshot = _metricsCollector.CurrentSnapshot;
            var recentSnapshots = _metricsCollector.GetRecentSnapshots(10).ToList();

            var recommendations = new List<TimerTuningRecommendation>();

            foreach (var rule in _tuningRules.Values)
            {
                if (rule.ShouldApply(currentSnapshot, recentSnapshots))
                {
                    recommendations.Add(new TimerTuningRecommendation
                    {
                        RuleName = rule.Name,
                        Action = rule.Action,
                        Priority = rule.Priority,
                        Reason = rule.GetReason(currentSnapshot, recentSnapshots),
                        EstimatedImpact = rule.EstimatedImpact,
                        Confidence = rule.GetConfidence(currentSnapshot, recentSnapshots)
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.Priority).ThenByDescending(r => r.Confidence);
        }

        /// <summary>
        /// Applies a tuning configuration
        /// </summary>
        /// <param name="configuration">The configuration to apply</param>
        /// <param name="reason">The reason for applying the configuration</param>
        public async Task ApplyConfigurationAsync(TimerTuningConfiguration configuration, string reason)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var oldConfiguration = CurrentConfiguration;
            CurrentConfiguration = configuration;

            // Apply configuration changes
            await ApplyConfigurationChangesAsync(oldConfiguration, configuration);

            // Record tuning action
            var tuningAction = new TimerTuningAction
            {
                Timestamp = DateTime.UtcNow,
                Action = TimerTuningActionType.ConfigurationUpdate,
                Reason = reason,
                OldConfiguration = oldConfiguration,
                NewConfiguration = configuration,
                Success = true
            };

            RecordTuningAction(tuningAction);

            // Raise event
            TuningApplied?.Invoke(this, new TimerTuningAppliedEventArgs(tuningAction));

            _logger.LogInformation("Applied tuning configuration: {Reason}", reason);
        }

        /// <summary>
        /// Gets tuning history
        /// </summary>
        /// <param name="count">The number of recent actions to retrieve</param>
        /// <returns>Collection of tuning actions</returns>
        public IEnumerable<TimerTuningAction> GetTuningHistory(int count = 20)
        {
            lock (_lock)
            {
                return _tuningHistory.OrderByDescending(a => a.Timestamp).Take(count).ToList();
            }
        }

        /// <summary>
        /// Resets the tuner to default configuration
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            var defaultConfig = new TimerTuningConfiguration();
            await ApplyConfigurationAsync(defaultConfig, "Reset to defaults");
        }

        /// <summary>
        /// Forces immediate tuning cycle
        /// </summary>
        public async Task ForceTuningCycleAsync()
        {
            await TuningCycleAsync(null);
        }

        private async void TuningCycleAsync(object? state)
        {
            if (!_isTuning)
                return;

            try
            {
                var recommendations = GetTuningRecommendations().ToList();

                if (recommendations.Any())
                {
                    // Raise recommendation event
                    TuningRecommended?.Invoke(this, new TimerTuningRecommendationEventArgs(recommendations));

                    // Apply top recommendations based on settings
                    var recommendationsToApply = recommendations
                        .Take(_options.MaxRecommendationsPerCycle)
                        .Where(r => r.Confidence >= _options.MinimumConfidenceThreshold)
                        .ToList();

                    foreach (var recommendation in recommendationsToApply)
                    {
                        if (await ApplyRecommendationAsync(recommendation))
                        {
                            break; // Apply one recommendation per cycle
                        }
                    }
                }

                // Update statistics
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tuning cycle");
            }
        }

        private async Task<bool> ApplyRecommendationAsync(TimerTuningRecommendation recommendation)
        {
            try
            {
                var tuningAction = new TimerTuningAction
                {
                    Timestamp = DateTime.UtcNow,
                    Action = recommendation.Action,
                    Reason = recommendation.Reason,
                    Recommendation = recommendation
                };

                switch (recommendation.Action)
                {
                    case TimerTuningActionType.IncreaseThreadPool:
                        await IncreaseThreadPoolAsync(recommendation);
                        break;
                    case TimerTuningActionType.DecreaseThreadPool:
                        await DecreaseThreadPoolAsync(recommendation);
                        break;
                    case TimerTuningActionType.AdjustCacheSize:
                        await AdjustCacheSizeAsync(recommendation);
                        break;
                    case TimerTuningActionType.ForceGarbageCollection:
                        await ForceGarbageCollectionAsync(recommendation);
                        break;
                    case TimerTuningActionType.AdjustMemoryPressure:
                        await AdjustMemoryPressureAsync(recommendation);
                        break;
                    case TimerTuningActionType.ConfigurationUpdate:
                        await ApplyConfigurationUpdateAsync(recommendation);
                        break;
                    default:
                        _logger.LogWarning("Unknown tuning action: {Action}", recommendation.Action);
                        return false;
                }

                tuningAction.Success = true;
                RecordTuningAction(tuningAction);
                TuningApplied?.Invoke(this, new TimerTuningAppliedEventArgs(tuningAction));

                _logger.LogInformation("Applied tuning recommendation: {Action} - {Reason}",
                    recommendation.Action, recommendation.Reason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying tuning recommendation: {Action}", recommendation.Action);
                return false;
            }
        }

        private async Task IncreaseThreadPoolAsync(TimerTuningRecommendation recommendation)
        {
            // This would integrate with the TimerThreadPoolManager
            // For now, we'll simulate the action
            _logger.LogInformation("Increasing thread pool size based on recommendation: {Reason}", recommendation.Reason);
        }

        private async Task DecreaseThreadPoolAsync(TimerTuningRecommendation recommendation)
        {
            // This would integrate with the TimerThreadPoolManager
            _logger.LogInformation("Decreasing thread pool size based on recommendation: {Reason}", recommendation.Reason);
        }

        private async Task AdjustCacheSizeAsync(TimerTuningRecommendation recommendation)
        {
            // This would integrate with the TimerCacheManager
            _logger.LogInformation("Adjusting cache size based on recommendation: {Reason}", recommendation.Reason);
        }

        private async Task ForceGarbageCollectionAsync(TimerTuningRecommendation recommendation)
        {
            // This would integrate with the TimerMemoryManager
            _logger.LogInformation("Forcing garbage collection based on recommendation: {Reason}", recommendation.Reason);
        }

        private async Task AdjustMemoryPressureAsync(TimerTuningRecommendation recommendation)
        {
            // This would integrate with the TimerMemoryManager
            _logger.LogInformation("Adjusting memory pressure based on recommendation: {Reason}", recommendation.Reason);
        }

        private async Task ApplyConfigurationUpdateAsync(TimerTuningRecommendation recommendation)
        {
            // Apply configuration changes based on the recommendation
            var newConfig = CurrentConfiguration.Clone();
            // Modify the configuration based on the recommendation
            await ApplyConfigurationAsync(newConfig, recommendation.Reason);
        }

        private async Task ApplyConfigurationChangesAsync(TimerTuningConfiguration oldConfig, TimerTuningConfiguration newConfig)
        {
            // Apply changes to the performance optimizer
            if (oldConfig.OptimizationIntervalMs != newConfig.OptimizationIntervalMs)
            {
                await _optimizer.UpdateOptimizationIntervalAsync(newConfig.OptimizationIntervalMs);
            }

            // Apply other configuration changes as needed
        }

        private void RecordTuningAction(TimerTuningAction action)
        {
            lock (_lock)
            {
                _tuningHistory.Enqueue(action);

                // Keep only recent history
                while (_tuningHistory.Count > _options.MaxTuningHistorySize)
                {
                    _tuningHistory.Dequeue();
                }
            }
        }

        private void UpdateStatistics()
        {
            lock (_lock)
            {
                TuningStatistics.TotalTuningCycles++;
                TuningStatistics.Uptime = Uptime;
                TuningStatistics.LastTuningTime = DateTime.UtcNow;

                var successfulActions = _tuningHistory.Count(a => a.Success);
                var failedActions = _tuningHistory.Count(a => !a.Success);
                var totalActions = _tuningHistory.Count;

                TuningStatistics.SuccessfulTunings = successfulActions;
                TuningStatistics.FailedTunings = failedActions;
                TuningStatistics.SuccessRate = totalActions > 0 ? (successfulActions * 100.0 / totalActions) : 0;
            }
        }

        private void InitializeTuningRules()
        {
            // High CPU usage rule
            _tuningRules["HighCpuUsage"] = new TimerTuningRule
            {
                Name = "HighCpuUsage",
                Action = TimerTuningActionType.DecreaseThreadPool,
                Priority = TimerTuningPriority.High,
                EstimatedImpact = TimerTuningImpact.Medium,
                Condition = (snapshot, recent) => snapshot.CpuUsagePercent > 85,
                ReasonGenerator = (snapshot, recent) => $"High CPU usage detected: {snapshot.CpuUsagePercent:F1}%",
                ConfidenceCalculator = (snapshot, recent) => Math.Min(100, snapshot.CpuUsagePercent)
            };

            // High memory usage rule
            _tuningRules["HighMemoryUsage"] = new TimerTuningRule
            {
                Name = "HighMemoryUsage",
                Action = TimerTuningActionType.ForceGarbageCollection,
                Priority = TimerTuningPriority.High,
                EstimatedImpact = TimerTuningImpact.Medium,
                Condition = (snapshot, recent) => snapshot.MemoryUsagePercent > 90,
                ReasonGenerator = (snapshot, recent) => $"High memory usage detected: {snapshot.MemoryUsagePercent:F1}%",
                ConfidenceCalculator = (snapshot, recent) => Math.Min(100, snapshot.MemoryUsagePercent)
            };

            // Low CPU usage rule
            _tuningRules["LowCpuUsage"] = new TimerTuningRule
            {
                Name = "LowCpuUsage",
                Action = TimerTuningActionType.IncreaseThreadPool,
                Priority = TimerTuningPriority.Medium,
                EstimatedImpact = TimerTuningImpact.Low,
                Condition = (snapshot, recent) => snapshot.CpuUsagePercent < 30 && snapshot.ThreadCount < 50,
                ReasonGenerator = (snapshot, recent) => $"Low CPU usage with available capacity: {snapshot.CpuUsagePercent:F1}%",
                ConfidenceCalculator = (snapshot, recent) => Math.Max(0, 100 - snapshot.CpuUsagePercent)
            };

            // Memory pressure rule
            _tuningRules["MemoryPressure"] = new TimerTuningRule
            {
                Name = "MemoryPressure",
                Action = TimerTuningActionType.AdjustMemoryPressure,
                Priority = TimerTuningPriority.High,
                EstimatedImpact = TimerTuningImpact.High,
                Condition = (snapshot, recent) => snapshot.AvailableMemoryMB < 1000,
                ReasonGenerator = (snapshot, recent) => $"Low available memory: {snapshot.AvailableMemoryMB}MB",
                ConfidenceCalculator = (snapshot, recent) => Math.Max(0, 100 - (snapshot.AvailableMemoryMB / 10))
            };
        }

        private void SubscribeToEvents()
        {
            _metricsCollector.ThresholdExceeded += OnThresholdExceeded;
        }

        private void OnThresholdExceeded(object? sender, TimerPerformanceThresholdExceededEventArgs e)
        {
            // React to threshold exceeded events
            _logger.LogWarning("Performance threshold exceeded: {Thresholds}",
                string.Join(", ", e.ExceededThresholds.Select(t => t.ToString())));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isTuning = false;
            _tuningTimer?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}