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
    /// Represents a single error event
    /// </summary>
    public class ErrorEvent
    {
        /// <summary>
        /// Gets or sets the unique event ID
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the error type
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// Gets or sets the error category
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the error severity
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the error code
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the source operation
        /// </summary>
        public string SourceOperation { get; set; }

        /// <summary>
        /// Gets or sets the recovery strategy
        /// </summary>
        public RecoveryStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether recovery was successful
        /// </summary>
        public bool RecoverySuccessful { get; set; }

        /// <summary>
        /// Gets or sets the recovery time
        /// </summary>
        public TimeSpan? RecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets the retry count
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets additional context data
        /// </summary>
        public Dictionary<string, object> Context { get; set; }

        /// <summary>
        /// Gets or sets the stack trace
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Gets or sets the user session ID
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the machine name
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        public int ThreadId { get; set; }
    }

    /// <summary>
    /// Represents error analytics for a specific time period
    /// </summary>
    public class ErrorAnalytics
    {
        /// <summary>
        /// Gets or sets the time period start
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Gets or sets the time period end
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Gets or sets the total number of errors
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Gets or sets the errors by category
        /// </summary>
        public Dictionary<ErrorCategory, int> ErrorsByCategory { get; set; }

        /// <summary>
        /// Gets or sets the errors by severity
        /// </summary>
        public Dictionary<ErrorSeverity, int> ErrorsBySeverity { get; set; }

        /// <summary>
        /// Gets or sets the errors by type
        /// </summary>
        public Dictionary<string, int> ErrorsByType { get; set; }

        /// <summary>
        /// Gets or sets the errors by operation
        /// </summary>
        public Dictionary<string, int> ErrorsByOperation { get; set; }

        /// <summary>
        /// Gets or sets the recovery statistics
        /// </summary>
        public RecoveryStatistics RecoveryStats { get; set; }

        /// <summary>
        /// Gets or sets the top error codes
        /// </summary>
        public List<ErrorCodeCount> TopErrorCodes { get; set; }

        /// <summary>
        /// Gets or sets the error rate per minute
        /// </summary>
        public double ErrorRatePerMinute { get; set; }

        /// <summary>
        /// Gets or sets the average recovery time
        /// </summary>
        public TimeSpan AverageRecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the retry statistics
        /// </summary>
        public RetryStatistics RetryStats { get; set; }
    }

    /// <summary>
    /// Recovery statistics
    /// </summary>
    public class RecoveryStatistics
    {
        /// <summary>
        /// Gets or sets the total recovery attempts
        /// </summary>
        public int TotalAttempts { get; set; }

        /// <summary>
        /// Gets or sets the successful recoveries
        /// </summary>
        public int SuccessfulRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the failed recoveries
        /// </summary>
        public int FailedRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the recovery success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the average recovery time
        /// </summary>
        public TimeSpan AverageRecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets the recovery success by category
        /// </summary>
        public Dictionary<ErrorCategory, double> SuccessByCategory { get; set; }
    }

    /// <summary>
    /// Error code with count
    /// </summary>
    public class ErrorCodeCount
    {
        /// <summary>
        /// Gets or sets the error code
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the count
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the percentage
        /// </summary>
        public double Percentage { get; set; }
    }

    /// <summary>
    /// Retry statistics
    /// </summary>
    public class RetryStatistics
    {
        /// <summary>
        /// Gets or sets the total retry attempts
        /// </summary>
        public int TotalAttempts { get; set; }

        /// <summary>
        /// Gets or sets the successful retries
        /// </summary>
        public int SuccessfulRetries { get; set; }

        /// <summary>
        /// Gets or sets the failed retries
        /// </summary>
        public int FailedRetries { get; set; }

        /// <summary>
        /// Gets or sets the average retry count
        /// </summary>
        public double AverageRetryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum retry count
        /// </summary>
        public int MaximumRetryCount { get; set; }
    }

    /// <summary>
    /// Handles error analytics and metrics collection
    /// </summary>
    public class ErrorAnalyticsManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<ErrorEvent> _errorEvents;
        private readonly ConcurrentDictionary<string, ErrorEvent> _recentErrors;
        private readonly Dictionary<DateTime, List<ErrorEvent>> _historicalErrors;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly Task _analyticsTask;
        private readonly TimeSpan _retentionPeriod;
        private readonly TimeSpan _analyticsInterval;
        private readonly int _maxRecentErrors;
        private bool _disposed;

        /// <summary>
        /// Gets the current analytics
        /// </summary>
        public ErrorAnalytics CurrentAnalytics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ErrorAnalyticsManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="retentionPeriod">Data retention period</param>
        /// <param name="analyticsInterval">Analytics calculation interval</param>
        /// <param name="maxRecentErrors">Maximum number of recent errors to keep</param>
        public ErrorAnalyticsManager(
            ILogger logger,
            TimeSpan retentionPeriod,
            TimeSpan analyticsInterval,
            int maxRecentErrors = 1000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retentionPeriod = retentionPeriod;
            _analyticsInterval = analyticsInterval;
            _maxRecentErrors = maxRecentErrors;

            _errorEvents = new ConcurrentQueue<ErrorEvent>();
            _recentErrors = new ConcurrentDictionary<string, ErrorEvent>();
            _historicalErrors = new Dictionary<DateTime, List<ErrorEvent>>();
            _shutdownCts = new CancellationTokenSource();

            CurrentAnalytics = new ErrorAnalytics
            {
                PeriodStart = DateTime.Now,
                PeriodEnd = DateTime.Now.Add(analyticsInterval),
                ErrorsByCategory = new Dictionary<ErrorCategory, int>(),
                ErrorsBySeverity = new Dictionary<ErrorSeverity, int>(),
                ErrorsByType = new Dictionary<string, int>(),
                ErrorsByOperation = new Dictionary<string, int>(),
                RecoveryStats = new RecoveryStatistics(),
                TopErrorCodes = new List<ErrorCodeCount>(),
                RetryStats = new RetryStatistics(),
                SuccessByCategory = new Dictionary<ErrorCategory, double>()
            };

            _analyticsTask = Task.Run(ProcessAnalyticsAsync);
        }

        /// <summary>
        /// Records an error event
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="classification">Error classification</param>
        /// <param name="context">Additional context</param>
        /// <param name="recoveryResult">Optional recovery result</param>
        public void RecordError(
            Exception exception,
            ErrorClassification classification,
            object context = null,
            RecoveryResult recoveryResult = null)
        {
            if (exception == null || classification == null)
                return;

            var errorEvent = new ErrorEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.Now,
                ErrorType = exception.GetType().Name,
                Category = classification.Category,
                Severity = classification.Severity,
                Message = classification.TechnicalMessage,
                ErrorCode = classification.ErrorCode,
                SourceOperation = classification.SourceOperation,
                Strategy = classification.Strategy,
                Context = context != null ? new Dictionary<string, object> { ["Context"] = context } : new Dictionary<string, object>(),
                StackTrace = exception.StackTrace,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            if (recoveryResult != null)
            {
                errorEvent.RecoverySuccessful = recoveryResult.Success;
                errorEvent.RecoveryTime = recoveryResult.RecoveryTime;
            }

            _errorEvents.Enqueue(errorEvent);

            // Keep only recent errors
            while (_recentErrors.Count > _maxRecentErrors)
            {
                var oldestKey = _recentErrors.Keys.OrderBy(k => k).FirstOrDefault();
                if (oldestKey != null)
                {
                    _recentErrors.TryRemove(oldestKey, out _);
                }
            }

            _recentErrors[errorEvent.EventId] = errorEvent;

            _logger.LogDebug("Recorded error event {EventId} for {ErrorType} - {Category}",
                errorEvent.EventId, errorEvent.ErrorType, errorEvent.Category);
        }

        /// <summary>
        /// Gets analytics for a specific time period
        /// </summary>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        /// <returns>Analytics for the specified period</returns>
        public ErrorAnalytics GetAnalyticsForPeriod(DateTime startTime, DateTime endTime)
        {
            var allEvents = GetAllEventsInRange(startTime, endTime);
            return CalculateAnalytics(allEvents, startTime, endTime);
        }

        /// <summary>
        /// Gets the current error trends
        /// </summary>
        /// <param name="timeWindow">Time window for trend analysis</param>
        /// <returns>Error trends</returns>
        public Dictionary<DateTime, ErrorAnalytics> GetErrorTrends(TimeSpan timeWindow)
        {
            var trends = new Dictionary<DateTime, ErrorAnalytics>();
            var endTime = DateTime.Now;
            var startTime = endTime - timeWindow;

            var eventsInRange = GetAllEventsInRange(startTime, endTime);

            // Group by hour
            var hourlyGroups = eventsInRange
                .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
                .OrderBy(g => g.Key);

            foreach (var group in hourlyGroups)
            {
                var periodStart = group.Key;
                var periodEnd = periodStart.AddHours(1);
                var hourEvents = group.ToList();

                trends[periodStart] = CalculateAnalytics(hourEvents, periodStart, periodEnd);
            }

            return trends;
        }

        /// <summary>
        /// Gets error patterns and anomalies
        /// </summary>
        /// <returns>Error patterns and anomalies</returns>
        public ErrorPatterns GetErrorPatterns()
        {
            var recentEvents = GetAllEventsInRange(DateTime.Now.AddHours(-24), DateTime.Now);
            var olderEvents = GetAllEventsInRange(DateTime.Now.AddHours(-48), DateTime.Now.AddHours(-24));

            return new ErrorPatterns
            {
                RecentErrors = recentEvents.Count,
                PreviousPeriodErrors = olderEvents.Count,
                ErrorGrowthRate = olderEvents.Count > 0 ? (double)(recentEvents.Count - olderEvents.Count) / olderEvents.Count : 0,
                TopErrorTypes = recentEvents
                    .GroupBy(e => e.ErrorType)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => new ErrorTypeCount { ErrorType = g.Key, Count = g.Count() })
                    .ToList(),
                TopOperations = recentEvents
                    .GroupBy(e => e.SourceOperation)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => new OperationErrorCount { Operation = g.Key, Count = g.Count() })
                    .ToList(),
                CriticalErrors = recentEvents.Count(e => e.Severity == ErrorSeverity.Critical),
                ErrorRate = CalculateErrorRate(recentEvents, TimeSpan.FromHours(24)),
                RecoveryRate = CalculateRecoveryRate(recentEvents)
            };
        }

        /// <summary>
        /// Processes analytics asynchronously
        /// </summary>
        private async Task ProcessAnalyticsAsync()
        {
            while (!_shutdownCts.Token.IsCancellationRequested)
            {
                try
                {
                    await ProcessErrorEventsAsync().ConfigureAwait(false);
                    await UpdateCurrentAnalyticsAsync().ConfigureAwait(false);
                    await CleanupOldDataAsync().ConfigureAwait(false);

                    await Task.Delay(_analyticsInterval, _shutdownCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing analytics");
                    await Task.Delay(TimeSpan.FromSeconds(10), _shutdownCts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Processes queued error events
        /// </summary>
        private async Task ProcessErrorEventsAsync()
        {
            while (_errorEvents.TryDequeue(out var errorEvent))
            {
                try
                {
                    // Add to historical data
                    var hourKey = new DateTime(errorEvent.Timestamp.Year, errorEvent.Timestamp.Month, errorEvent.Timestamp.Day, errorEvent.Timestamp.Hour, 0, 0);
                    lock (_historicalErrors)
                    {
                        if (!_historicalErrors.ContainsKey(hourKey))
                        {
                            _historicalErrors[hourKey] = new List<ErrorEvent>();
                        }
                        _historicalErrors[hourKey].Add(errorEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing error event {EventId}", errorEvent.EventId);
                }
            }
        }

        /// <summary>
        /// Updates current analytics
        /// </summary>
        private async Task UpdateCurrentAnalyticsAsync()
        {
            try
            {
                var now = DateTime.Now;
                var periodStart = now - _analyticsInterval;
                var periodEnd = now;

                var eventsInRange = GetAllEventsInRange(periodStart, periodEnd);
                CurrentAnalytics = CalculateAnalytics(eventsInRange, periodStart, periodEnd);

                _logger.LogDebug("Updated analytics: {TotalErrors} errors in the last {Interval} minutes",
                    CurrentAnalytics.TotalErrors, _analyticsInterval.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating current analytics");
            }
        }

        /// <summary>
        /// Cleans up old data
        /// </summary>
        private async Task CleanupOldDataAsync()
        {
            try
            {
                var cutoffTime = DateTime.Now - _retentionPeriod;
                var keysToRemove = new List<DateTime>();

                lock (_historicalErrors)
                {
                    foreach (var key in _historicalErrors.Keys)
                    {
                        if (key < cutoffTime)
                        {
                            keysToRemove.Add(key);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        _historicalErrors.Remove(key);
                    }
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old analytics entries", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old analytics data");
            }
        }

        /// <summary>
        /// Gets all error events in a time range
        /// </summary>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        /// <returns>List of error events</returns>
        private List<ErrorEvent> GetAllEventsInRange(DateTime startTime, DateTime endTime)
        {
            var events = new List<ErrorEvent>();

            lock (_historicalErrors)
            {
                foreach (var kvp in _historicalErrors)
                {
                    if (kvp.Key >= startTime && kvp.Key <= endTime.AddHours(1))
                    {
                        events.AddRange(kvp.Value.Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime));
                    }
                }
            }

            return events;
        }

        /// <summary>
        /// Calculates analytics from error events
        /// </summary>
        /// <param name="events">Error events</param>
        /// <param name="periodStart">Period start</param>
        /// <param name="periodEnd">Period end</param>
        /// <returns>Calculated analytics</returns>
        private ErrorAnalytics CalculateAnalytics(List<ErrorEvent> events, DateTime periodStart, DateTime periodEnd)
        {
            var analytics = new ErrorAnalytics
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalErrors = events.Count,
                ErrorsByCategory = events.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count()),
                ErrorsBySeverity = events.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count()),
                ErrorsByType = events.GroupBy(e => e.ErrorType).ToDictionary(g => g.Key, g => g.Count()),
                ErrorsByOperation = events.Where(e => !string.IsNullOrEmpty(e.SourceOperation))
                    .GroupBy(e => e.SourceOperation).ToDictionary(g => g.Key, g => g.Count()),
                RecoveryStats = CalculateRecoveryStats(events),
                TopErrorCodes = CalculateTopErrorCodes(events),
                ErrorRatePerMinute = CalculateErrorRate(events, periodEnd - periodStart),
                AverageRecoveryTime = CalculateAverageRecoveryTime(events),
                SuccessRate = CalculateSuccessRate(events),
                RetryStats = CalculateRetryStats(events)
            };

            return analytics;
        }

        /// <summary>
        /// Calculates recovery statistics
        /// </summary>
        /// <param name="events">Error events</param>
        /// <returns>Recovery statistics</returns>
        private RecoveryStatistics CalculateRecoveryStats(List<ErrorEvent> events)
        {
            var recoveryEvents = events.Where(e => e.RecoveryTime.HasValue).ToList();

            if (!recoveryEvents.Any())
            {
                return new RecoveryStatistics();
            }

            var successful = recoveryEvents.Count(e => e.RecoverySuccessful);
            var totalRecoveryTime = recoveryEvents.Sum(e => e.RecoveryTime.Value.TotalMilliseconds);

            return new RecoveryStatistics
            {
                TotalAttempts = recoveryEvents.Count,
                SuccessfulRecoveries = successful,
                FailedRecoveries = recoveryEvents.Count - successful,
                SuccessRate = recoveryEvents.Count > 0 ? (double)successful / recoveryEvents.Count : 0,
                AverageRecoveryTime = TimeSpan.FromMilliseconds(recoveryEvents.Count > 0 ? totalRecoveryTime / recoveryEvents.Count : 0),
                SuccessByCategory = recoveryEvents
                    .GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Any() ? (double)g.Count(e => e.RecoverySuccessful) / g.Count() : 0)
            };
        }

        /// <summary>
        /// Calculates top error codes
        /// </summary>
        /// <param name="events">Error events</param>
        /// <returns>Top error codes</returns>
        private List<ErrorCodeCount> CalculateTopErrorCodes(List<ErrorEvent> events)
        {
            var errorCodes = events
                .Where(e => !string.IsNullOrEmpty(e.ErrorCode))
                .GroupBy(e => e.ErrorCode)
                .Select(g => new ErrorCodeCount
                {
                    ErrorCode = g.Key,
                    Count = g.Count(),
                    Percentage = events.Count > 0 ? (double)g.Count() / events.Count * 100 : 0
                })
                .OrderByDescending(c => c.Count)
                .Take(10)
                .ToList();

            return errorCodes;
        }

        /// <summary>
        /// Calculates error rate
        /// </summary>
        /// <param name="events">Error events</param>
        /// <param name="timePeriod">Time period</param>
        /// <returns>Error rate per minute</returns>
        private double CalculateErrorRate(List<ErrorEvent> events, TimeSpan timePeriod)
        {
            var minutes = timePeriod.TotalMinutes;
            return minutes > 0 ? events.Count / minutes : 0;
        }

        /// <summary>
        /// Calculates average recovery time
        /// </summary>
        /// <param name="events">Error events</param>
        /// <returns>Average recovery time</returns>
        private TimeSpan CalculateAverageRecoveryTime(List<ErrorEvent> events)
        {
            var recoveryEvents = events.Where(e => e.RecoveryTime.HasValue).ToList();
            if (!recoveryEvents.Any())
                return TimeSpan.Zero;

            var totalMs = recoveryEvents.Sum(e => e.RecoveryTime.Value.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(totalMs / recoveryEvents.Count);
        }

        /// <summary>
        /// Calculates success rate
        /// </summary>
        /// <param name="events">Error events</param>
        /// <returns>Success rate</returns>
        private double CalculateSuccessRate(List<ErrorEvent> events)
        {
            var recoveryEvents = events.Where(e => e.RecoveryTime.HasValue).ToList();
            if (!recoveryEvents.Any())
                return 0;

            return (double)recoveryEvents.Count(e => e.RecoverySuccessful) / recoveryEvents.Count;
        }

        /// <summary>
        /// Calculates retry statistics
        /// </summary>
        /// <param name="events">Error events</param>
        /// <returns>Retry statistics</returns>
        private RetryStatistics CalculateRetryStats(List<ErrorEvent> events)
        {
            return new RetryStatistics
            {
                TotalAttempts = events.Sum(e => e.RetryCount),
                SuccessfulRetries = events.Count(e => e.RecoverySuccessful && e.RetryCount > 0),
                FailedRetries = events.Count(e => !e.RecoverySuccessful && e.RetryCount > 0),
                AverageRetryCount = events.Any() ? events.Average(e => e.RetryCount) : 0,
                MaximumRetryCount = events.Any() ? events.Max(e => e.RetryCount) : 0
            };
        }

        /// <summary>
        /// Calculates recovery rate
        /// </summary>
        /// <param name="events">Error events</param>
        /// <returns>Recovery rate</returns>
        private double CalculateRecoveryRate(List<ErrorEvent> events)
        {
            var recoveryEvents = events.Where(e => e.RecoveryTime.HasValue).ToList();
            if (!recoveryEvents.Any())
                return 0;

            return (double)recoveryEvents.Count(e => e.RecoverySuccessful) / recoveryEvents.Count;
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _shutdownCts.Cancel();
                _analyticsTask?.Wait(TimeSpan.FromSeconds(5));
                _shutdownCts?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Error patterns and anomalies
    /// </summary>
    public class ErrorPatterns
    {
        /// <summary>
        /// Gets or sets the number of recent errors
        /// </summary>
        public int RecentErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of errors in the previous period
        /// </summary>
        public int PreviousPeriodErrors { get; set; }

        /// <summary>
        /// Gets or sets the error growth rate
        /// </summary>
        public double ErrorGrowthRate { get; set; }

        /// <summary>
        /// Gets or sets the top error types
        /// </summary>
        public List<ErrorTypeCount> TopErrorTypes { get; set; }

        /// <summary>
        /// Gets or sets the top operations with errors
        /// </summary>
        public List<OperationErrorCount> TopOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of critical errors
        /// </summary>
        public int CriticalErrors { get; set; }

        /// <summary>
        /// Gets or sets the current error rate
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the recovery rate
        /// </summary>
        public double RecoveryRate { get; set; }
    }

    /// <summary>
    /// Error type with count
    /// </summary>
    public class ErrorTypeCount
    {
        /// <summary>
        /// Gets or sets the error type
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// Gets or sets the count
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Operation error count
    /// </summary>
    public class OperationErrorCount
    {
        /// <summary>
        /// Gets or sets the operation name
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets or sets the error count
        /// </summary>
        public int Count { get; set; }
    }
}