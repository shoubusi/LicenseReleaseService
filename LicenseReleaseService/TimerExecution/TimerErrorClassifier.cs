using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Handles intelligent classification of timer execution errors
    /// </summary>
    public class TimerErrorClassifier
    {
        private readonly ILogger<TimerErrorClassifier> _logger;
        private readonly Dictionary<Type, TimerErrorCategory> _exceptionMappings;
        private readonly Dictionary<Type, TimerErrorSeverity> _severityMappings;
        private readonly Dictionary<TimerErrorCategory, TimerRecoveryAction> _recoveryMappings;

        /// <summary>
        /// Initializes a new instance of the TimerErrorClassifier class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public TimerErrorClassifier(ILogger<TimerErrorClassifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _exceptionMappings = new Dictionary<Type, TimerErrorCategory>();
            _severityMappings = new Dictionary<Type, TimerErrorSeverity>();
            _recoveryMappings = new Dictionary<TimerErrorCategory, TimerRecoveryAction>();

            InitializeDefaultMappings();
        }

        /// <summary>
        /// Classifies an exception and returns detailed error information
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <param name="context">Additional context information</param>
        /// <returns>Classification result</returns>
        public TimerErrorClassification ClassifyError(Exception exception, object context = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var classification = new TimerErrorClassification
            {
                Exception = exception,
                Timestamp = DateTime.UtcNow,
                Context = context
            };

            try
            {
                // Determine category
                classification.Category = DetermineErrorCategory(exception, context);

                // Determine severity
                classification.Severity = DetermineErrorSeverity(exception, classification.Category, context);

                // Determine recovery action
                classification.RecoveryAction = DetermineRecoveryAction(classification.Category, classification.Severity, exception);

                // Add analysis details
                classification.AnalysisDetails = GetAnalysisDetails(exception, classification.Category, classification.Severity);

                // Determine if error is transient
                classification.IsTransient = IsTransientError(exception, classification.Category);

                // Determine if error is recoverable
                classification.IsRecoverable = IsRecoverableError(exception, classification.Category, classification.Severity);

                _logger.LogDebug("Classified error: {ExceptionType} as {Category}/{Severity} -> {Action}",
                    exception.GetType().Name, classification.Category, classification.Severity, classification.RecoveryAction);

                return classification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during error classification for {ExceptionType}", exception.GetType().Name);

                // Fallback classification
                classification.Category = TimerErrorCategory.Unknown;
                classification.Severity = TimerErrorSeverity.Medium;
                classification.RecoveryAction = TimerRecoveryAction.LogAndContinue;
                classification.IsTransient = false;
                classification.IsRecoverable = true;
                classification.AnalysisDetails = new Dictionary<string, object>
                {
                    ["ClassificationError"] = ex.Message
                };

                return classification;
            }
        }

        /// <summary>
        /// Determines the error category for an exception
        /// </summary>
        private TimerErrorCategory DetermineErrorCategory(Exception exception, object context)
        {
            // Check specific exception type mappings first
            var exceptionType = exception.GetType();

            // Check exact type matches
            if (_exceptionMappings.TryGetValue(exceptionType, out var category))
                return category;

            // Check inheritance hierarchy
            foreach (var mapping in _exceptionMappings)
            {
                if (mapping.Key.IsAssignableFrom(exceptionType))
                    return mapping.Value;
            }

            // Special cases based on exception properties
            if (exception is AggregateException aggregateEx)
            {
                return ClassifyAggregateException(aggregateEx);
            }

            // Check for specific patterns in exception messages
            return CategorizeByExceptionMessage(exception);
        }

        /// <summary>
        /// Classifies aggregate exceptions by analyzing inner exceptions
        /// </summary>
        private TimerErrorCategory ClassifyAggregateException(AggregateException aggregateEx)
        {
            var innerCategories = aggregateEx.InnerExceptions
                .Select(innerEx => DetermineErrorCategory(innerEx, null))
                .ToList();

            // If all inner exceptions are the same category, use that
            if (innerCategories.Distinct().Count() == 1)
            {
                return innerCategories.First();
            }

            // If there's a critical error among them, classify as critical
            if (innerCategories.Contains(TimerErrorCategory.Resource) ||
                innerCategories.Contains(TimerErrorCategory.LicenseServer))
            {
                return TimerErrorCategory.Resource;
            }

            // Default to execution error for mixed aggregate exceptions
            return TimerErrorCategory.Execution;
        }

        /// <summary>
        /// Categorizes errors based on exception message patterns
        /// </summary>
        private TimerErrorCategory CategorizeByExceptionMessage(Exception exception)
        {
            var message = exception.Message?.ToLower() ?? string.Empty;

            if (message.Contains("timeout") || message.Contains("timed out"))
                return TimerErrorCategory.Timeout;

            if (message.Contains("network") || message.Contains("connection") ||
                message.Contains("socket") || message.Contains("endpoint"))
                return TimerErrorCategory.Network;

            if (message.Contains("memory") || message.Contains("out of memory") ||
                message.Contains("resource") || message.Contains("thread"))
                return TimerErrorCategory.Resource;

            if (message.Contains("license") || message.Contains("lmutil") ||
                message.Contains("flexlm") || message.Contains("flexnet"))
                return TimerErrorCategory.LicenseServer;

            if (message.Contains("process") || message.Contains("executable") ||
                message.Contains("file not found") || message.Contains("access denied"))
                return TimerErrorCategory.Process;

            if (message.Contains("configuration") || message.Contains("config") ||
                message.Contains("setting") || message.Contains("option"))
                return TimerErrorCategory.Configuration;

            // Default to unknown
            return TimerErrorCategory.Unknown;
        }

        /// <summary>
        /// Determines the severity of an error
        /// </summary>
        private TimerErrorSeverity DetermineErrorSeverity(Exception exception, TimerErrorCategory category, object context)
        {
            var exceptionType = exception.GetType();

            // Check specific severity mappings
            if (_severityMappings.TryGetValue(exceptionType, out var severity))
                return severity;

            // Check inheritance hierarchy
            foreach (var mapping in _severityMappings)
            {
                if (mapping.Key.IsAssignableFrom(exceptionType))
                    return mapping.Value;
            }

            // Determine severity based on category
            return GetDefaultSeverityForCategory(category);
        }

        /// <summary>
        /// Gets the default severity for a given category
        /// </summary>
        private TimerErrorSeverity GetDefaultSeverityForCategory(TimerErrorCategory category)
        {
            switch (category)
            {
                case TimerErrorCategory.Configuration:
                    return TimerErrorSeverity.High;

                case TimerErrorCategory.Resource:
                    return TimerErrorSeverity.Critical;

                case TimerErrorCategory.LicenseServer:
                    return TimerErrorSeverity.High;

                case TimerErrorCategory.Network:
                    return TimerErrorSeverity.Medium;

                case TimerErrorCategory.Timeout:
                    return TimerErrorSeverity.Low;

                case TimerErrorCategory.Process:
                    return TimerErrorSeverity.Medium;

                case TimerErrorCategory.Execution:
                    return TimerErrorSeverity.Medium;

                case TimerErrorCategory.Unknown:
                default:
                    return TimerErrorSeverity.Medium;
            }
        }

        /// <summary>
        /// Determines the recommended recovery action
        /// </summary>
        private TimerRecoveryAction DetermineRecoveryAction(TimerErrorCategory category, TimerErrorSeverity severity, Exception exception)
        {
            // Check category-specific recovery mappings
            if (_recoveryMappings.TryGetValue(category, out var action))
                return action;

            // Determine action based on category and severity
            switch (category)
            {
                case TimerErrorCategory.Configuration:
                    return TimerRecoveryAction.DisableTimer;

                case TimerErrorCategory.Resource:
                    return severity >= TimerErrorSeverity.Critical
                        ? TimerRecoveryAction.RestartService
                        : TimerRecoveryAction.IncreaseInterval;

                case TimerErrorCategory.LicenseServer:
                    return TimerRecoveryAction.EnableCircuitBreaker;

                case TimerErrorCategory.Network:
                    return TimerRecoveryAction.Retry;

                case TimerErrorCategory.Timeout:
                    return TimerRecoveryAction.Retry;

                case TimerErrorCategory.Process:
                    return TimerRecoveryAction.Retry;

                case TimerErrorCategory.Execution:
                    return TimerRecoveryAction.ResetTimer;

                case TimerErrorCategory.Unknown:
                default:
                    return TimerRecoveryAction.LogAndContinue;
            }
        }

        /// <summary>
        /// Determines if an error is transient
        /// </summary>
        private bool IsTransientError(Exception exception, TimerErrorCategory category)
        {
            switch (category)
            {
                case TimerErrorCategory.Network:
                case TimerErrorCategory.Timeout:
                    return true;

                case TimerErrorCategory.LicenseServer:
                    // Some license server errors might be transient
                    var message = exception.Message?.ToLower() ?? string.Empty;
                    return message.Contains("timeout") || message.Contains("connection") ||
                           message.Contains("temporary") || message.Contains("unavailable");

                case TimerErrorCategory.Process:
                    // Process errors might be transient if they're timeout-related
                    return exception is TimeoutException ||
                           (exception.Message?.ToLower().Contains("timeout") ?? false);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if an error is recoverable
        /// </summary>
        private bool IsRecoverableError(Exception exception, TimerErrorCategory category, TimerErrorSeverity severity)
        {
            // Critical severity errors might not be recoverable
            if (severity == TimerErrorSeverity.Critical)
                return false;

            // Configuration errors are generally not recoverable without manual intervention
            if (category == TimerErrorCategory.Configuration)
                return false;

            // Most other errors are recoverable
            return true;
        }

        /// <summary>
        /// Gets detailed analysis information for the error
        /// </summary>
        private Dictionary<string, object> GetAnalysisDetails(Exception exception, TimerErrorCategory category, TimerErrorSeverity severity)
        {
            var details = new Dictionary<string, object>
            {
                ["ExceptionType"] = exception.GetType().FullName,
                ["ExceptionMessage"] = exception.Message,
                ["Category"] = category.ToString(),
                ["Severity"] = severity.ToString(),
                ["StackTrace"] = exception.StackTrace,
                ["InnerException"] = exception.InnerException?.Message
            };

            // Add category-specific details
            switch (category)
            {
                case TimerErrorCategory.Network:
                    AddNetworkErrorDetails(exception, details);
                    break;

                case TimerErrorCategory.Timeout:
                    AddTimeoutErrorDetails(exception, details);
                    break;

                case TimerErrorCategory.Resource:
                    AddResourceErrorDetails(exception, details);
                    break;
            }

            return details;
        }

        /// <summary>
        /// Adds network-specific error details
        /// </summary>
        private void AddNetworkErrorDetails(Exception exception, Dictionary<string, object> details)
        {
            if (exception is SocketException socketEx)
            {
                details["ErrorCode"] = socketEx.ErrorCode;
                details["SocketErrorCode"] = socketEx.SocketErrorCode;
            }

            if (exception is WebException webEx)
            {
                details["StatusCode"] = webEx.Status;
                details["Response"] = webEx.Response?.GetType().Name;
            }
        }

        /// <summary>
        /// Adds timeout-specific error details
        /// </summary>
        private void AddTimeoutErrorDetails(Exception exception, Dictionary<string, object> details)
        {
            details["TimeoutType"] = exception.GetType().Name;

            if (exception is TimeoutException timeoutEx)
            {
                details["TimeoutMessage"] = timeoutEx.Message;
            }
        }

        /// <summary>
        /// Adds resource-specific error details
        /// </summary>
        private void AddResourceErrorDetails(Exception exception, Dictionary<string, object> details)
        {
            if (exception is OutOfMemoryException)
            {
                details["ResourceType"] = "Memory";
            }

            if (exception is InsufficientMemoryException)
            {
                details["ResourceType"] = "Memory";
            }

            if (exception is ThreadStateException)
            {
                details["ResourceType"] = "Thread";
            }
        }

        /// <summary>
        /// Initializes the default exception mappings
        /// </summary>
        private void InitializeDefaultMappings()
        {
            // Configuration errors
            _exceptionMappings[typeof(ArgumentException)] = TimerErrorCategory.Configuration;
            _exceptionMappings[typeof(ArgumentNullException)] = TimerErrorCategory.Configuration;
            _exceptionMappings[typeof(InvalidOperationException)] = TimerErrorCategory.Configuration;
            _exceptionMappings[typeof(NotSupportedException)] = TimerErrorCategory.Configuration;
            _exceptionMappings[typeof(ObjectDisposedException)] = TimerErrorCategory.Configuration;

            // Network errors
            _exceptionMappings[typeof(SocketException)] = TimerErrorCategory.Network;
            _exceptionMappings[typeof(WebException)] = TimerErrorCategory.Network;
            _exceptionMappings[typeof(System.Net.Http.HttpRequestException)] = TimerErrorCategory.Network;

            // Timeout errors
            _exceptionMappings[typeof(TimeoutException)] = TimerErrorCategory.Timeout;
            _exceptionMappings[typeof(OperationCanceledException)] = TimerErrorCategory.Timeout;

            // Resource errors
            _exceptionMappings[typeof(OutOfMemoryException)] = TimerErrorCategory.Resource;
            _exceptionMappings[typeof(InsufficientMemoryException)] = TimerErrorCategory.Resource;
            _exceptionMappings[typeof(ThreadStateException)] = TimerErrorCategory.Resource;
            _exceptionMappings[typeof(System.Threading.SemaphoreFullException)] = TimerErrorCategory.Resource;

            // Process errors
            _exceptionMappings[typeof(System.ComponentModel.Win32Exception)] = TimerErrorCategory.Process;
            _exceptionMappings[typeof(System.UnauthorizedAccessException)] = TimerErrorCategory.Process;
            _exceptionMappings[typeof(System.IO.FileNotFoundException)] = TimerErrorCategory.Process;
            _exceptionMappings[typeof(System.InvalidOperationException)] = TimerErrorCategory.Process;

            // Severity mappings
            _severityMappings[typeof(OutOfMemoryException)] = TimerErrorSeverity.Critical;
            _severityMappings[typeof(InsufficientMemoryException)] = TimerErrorSeverity.Critical;
            _severityMappings[typeof(StackOverflowException)] = TimerErrorSeverity.Critical;
            _severityMappings[typeof(AccessViolationException)] = TimerErrorSeverity.Critical;
            _severityMappings[typeof(ArgumentException)] = TimerErrorSeverity.High;
            _severityMappings[typeof(ArgumentNullException)] = TimerErrorSeverity.High;
            _severityMappings[typeof(InvalidOperationException)] = TimerErrorSeverity.High;
            _severityMappings[typeof(NotSupportedException)] = TimerErrorSeverity.High;
            _severityMappings[typeof(ObjectDisposedException)] = TimerErrorSeverity.High;

            // Recovery action mappings
            _recoveryMappings[TimerErrorCategory.Configuration] = TimerRecoveryAction.DisableTimer;
            _recoveryMappings[TimerErrorCategory.Resource] = TimerRecoveryAction.IncreaseInterval;
            _recoveryMappings[TimerErrorCategory.LicenseServer] = TimerRecoveryAction.EnableCircuitBreaker;
            _recoveryMappings[TimerErrorCategory.Network] = TimerRecoveryAction.Retry;
            _recoveryMappings[TimerErrorCategory.Timeout] = TimerRecoveryAction.Retry;
            _recoveryMappings[TimerErrorCategory.Process] = TimerRecoveryAction.Retry;
            _recoveryMappings[TimerErrorCategory.Execution] = TimerRecoveryAction.ResetTimer;
        }

        /// <summary>
        /// Registers a custom exception mapping
        /// </summary>
        /// <param name="exceptionType">The exception type</param>
        /// <param name="category">The error category</param>
        public void RegisterExceptionMapping(Type exceptionType, TimerErrorCategory category)
        {
            if (exceptionType == null)
                throw new ArgumentNullException(nameof(exceptionType));

            if (!typeof(Exception).IsAssignableFrom(exceptionType))
                throw new ArgumentException("Type must be an exception type", nameof(exceptionType));

            _exceptionMappings[exceptionType] = category;
            _logger.LogInformation("Registered exception mapping: {ExceptionType} -> {Category}", exceptionType.Name, category);
        }

        /// <summary>
        /// Registers a custom severity mapping
        /// </summary>
        /// <param name="exceptionType">The exception type</param>
        /// <param name="severity">The error severity</param>
        public void RegisterSeverityMapping(Type exceptionType, TimerErrorSeverity severity)
        {
            if (exceptionType == null)
                throw new ArgumentNullException(nameof(exceptionType));

            if (!typeof(Exception).IsAssignableFrom(exceptionType))
                throw new ArgumentException("Type must be an exception type", nameof(exceptionType));

            _severityMappings[exceptionType] = severity;
            _logger.LogInformation("Registered severity mapping: {ExceptionType} -> {Severity}", exceptionType.Name, severity);
        }

        /// <summary>
        /// Registers a custom recovery action mapping
        /// </summary>
        /// <param name="category">The error category</param>
        /// <param name="action">The recovery action</param>
        public void RegisterRecoveryMapping(TimerErrorCategory category, TimerRecoveryAction action)
        {
            _recoveryMappings[category] = action;
            _logger.LogInformation("Registered recovery mapping: {Category} -> {Action}", category, action);
        }

        /// <summary>
        /// Gets all registered exception mappings
        /// </summary>
        /// <returns>Dictionary of exception mappings</returns>
        public IReadOnlyDictionary<Type, TimerErrorCategory> GetExceptionMappings()
        {
            return new Dictionary<Type, TimerErrorCategory>(_exceptionMappings);
        }

        /// <summary>
        /// Gets all registered severity mappings
        /// </summary>
        /// <returns>Dictionary of severity mappings</returns>
        public IReadOnlyDictionary<Type, TimerErrorSeverity> GetSeverityMappings()
        {
            return new Dictionary<Type, TimerErrorSeverity>(_severityMappings);
        }

        /// <summary>
        /// Gets all registered recovery mappings
        /// </summary>
        /// <returns>Dictionary of recovery mappings</returns>
        public IReadOnlyDictionary<TimerErrorCategory, TimerRecoveryAction> GetRecoveryMappings()
        {
            return new Dictionary<TimerErrorCategory, TimerRecoveryAction>(_recoveryMappings);
        }
    }

    /// <summary>
    /// Represents the result of error classification
    /// </summary>
    public class TimerErrorClassification
    {
        /// <summary>
        /// Gets or sets the original exception
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the determined error category
        /// </summary>
        public TimerErrorCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the determined error severity
        /// </summary>
        public TimerErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the recommended recovery action
        /// </summary>
        public TimerRecoveryAction RecoveryAction { get; set; }

        /// <summary>
        /// Gets or sets the classification timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets additional context information
        /// </summary>
        public object Context { get; set; }

        /// <summary>
        /// Gets or sets detailed analysis information
        /// </summary>
        public Dictionary<string, object> AnalysisDetails { get; set; }

        /// <summary>
        /// Gets or sets whether the error is transient
        /// </summary>
        public bool IsTransient { get; set; }

        /// <summary>
        /// Gets or sets whether the error is recoverable
        /// </summary>
        public bool IsRecoverable { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerErrorClassification class
        /// </summary>
        public TimerErrorClassification()
        {
            AnalysisDetails = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets a value indicating whether the error should trigger circuit breaker
        /// </summary>
        public bool ShouldTriggerCircuitBreaker =>
            Severity >= TimerErrorSeverity.High ||
            Category == TimerErrorCategory.Resource ||
            Category == TimerErrorCategory.LicenseServer;

        /// <summary>
        /// Gets a value indicating whether the error should stop the timer
        /// </summary>
        public bool ShouldStopTimer =>
            Severity == TimerErrorSeverity.Critical ||
            Category == TimerErrorCategory.Configuration;

        /// <summary>
        /// Gets a value indicating whether the error allows retry
        /// </summary>
        public bool AllowsRetry =>
            IsTransient && IsRecoverable && Severity < TimerErrorSeverity.High;

        /// <summary>
        /// Returns a string representation of the classification
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"TimerErrorClassification[Category={Category}, " +
                   $"Severity={Severity}, Action={RecoveryAction}, " +
                   $"Transient={IsTransient}, Recoverable={IsRecoverable}, " +
                   $"Exception={Exception?.GetType().Name}]";
        }
    }
}