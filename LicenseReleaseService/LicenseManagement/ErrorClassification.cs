using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines error categories for classification
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>
        /// Transient network-related errors
        /// </summary>
        Network,

        /// <summary>
        /// Process execution failures
        /// </summary>
        Process,

        /// <summary>
        /// Authentication and authorization errors
        /// </summary>
        Authentication,

        /// <summary>
        /// Configuration-related errors
        /// </summary>
        Configuration,

        /// <summary>
        /// Resource availability errors
        /// </summary>
        Resource,

        /// <summary>
        /// License server specific errors
        /// </summary>
        LicenseServer,

        /// <summary>
        /// Data parsing and validation errors
        /// </summary>
        Data,

        /// <summary>
        /// Timeout-related errors
        /// </summary>
        Timeout,

        /// <summary>
        /// Unknown or uncategorized errors
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Defines error severity levels
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Information,

        /// <summary>
        /// Warning that doesn't prevent operation
        /// </summary>
        Warning,

        /// <summary>
        /// Error that affects functionality but allows continuation
        /// </summary>
        Error,

        /// <summary>
        /// Critical error that requires immediate attention
        /// </summary>
        Critical
    }

    /// <summary>
    /// Defines recovery strategies for different error types
    /// </summary>
    public enum RecoveryStrategy
    {
        /// <summary>
        /// No recovery possible
        /// </summary>
        None,

        /// <summary>
        /// Retry the operation
        /// </summary>
        Retry,

        /// <summary>
        /// Use alternative method or endpoint
        /// </summary>
        Fallback,

        /// <summary>
        /// Reset connection or state
        /// </summary>
        Reset,

        /// <summary>
        /// Escalate to administrator
        /// </summary>
        Escalate,

        /// <summary>
        /// Graceful degradation
        /// </summary>
        Degrade
    }

    /// <summary>
    /// Represents classified error information
    /// </summary>
    public class ErrorClassification
    {
        /// <summary>
        /// Gets or sets the error category
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the error severity
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the recommended recovery strategy
        /// </summary>
        public RecoveryStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the error is retryable
        /// </summary>
        public bool IsRetryable { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets the retry delay
        /// </summary>
        public TimeSpan RetryDelay { get; set; }

        /// <summary>
        /// Gets or sets the user-friendly message
        /// </summary>
        public string UserMessage { get; set; }

        /// <summary>
        /// Gets or sets the technical message
        /// </summary>
        public string TechnicalMessage { get; set; }

        /// <summary>
        /// Gets or sets the error code
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets additional context data
        /// </summary>
        public Dictionary<string, object> Context { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the error
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the source operation
        /// </summary>
        public string SourceOperation { get; set; }

        /// <summary>
        /// Gets a value indicating whether immediate notification is required
        /// </summary>
        public bool RequiresImmediateNotification => Severity == ErrorSeverity.Critical;

        /// <summary>
        /// Gets a value indicating whether the error should be logged
        /// </summary>
        public bool ShouldLog => Severity != ErrorSeverity.Information;
    }

    /// <summary>
    /// Handles comprehensive error classification and management
    /// </summary>
    public class ErrorClassifier
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Type, ErrorClassification> _exceptionMappings;
        private readonly List<Func<Exception, ErrorClassification>> _customClassifiers;

        /// <summary>
        /// Initializes a new instance of the ErrorClassifier class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public ErrorClassifier(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exceptionMappings = new Dictionary<Type, ErrorClassification>();
            _customClassifiers = new List<Func<Exception, ErrorClassification>>();

            InitializeDefaultMappings();
        }

        /// <summary>
        /// Classifies an exception
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <param name="context">Additional context</param>
        /// <param name="sourceOperation">Source operation name</param>
        /// <returns>Classified error information</returns>
        public ErrorClassification ClassifyException(Exception exception, object context = null, string sourceOperation = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var classification = ClassifyByType(exception) ?? ClassifyByCustomRules(exception);

            if (classification == null)
            {
                classification = CreateDefaultClassification(exception);
            }

            // Add context information
            classification.Timestamp = DateTime.Now;
            classification.SourceOperation = sourceOperation;
            classification.Context = context != null ? new Dictionary<string, object> { ["Context"] = context } : new Dictionary<string, object>();

            // Log the classification
            LogClassification(exception, classification);

            return classification;
        }

        /// <summary>
        /// Classifies an exception by its type
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <returns>Classification or null if not found</returns>
        private ErrorClassification ClassifyByType(Exception exception)
        {
            var exceptionType = exception.GetType();

            // Check for exact match
            if (_exceptionMappings.TryGetValue(exceptionType, out var classification))
            {
                return classification.Clone();
            }

            // Check for inheritance hierarchy
            foreach (var mapping in _exceptionMappings)
            {
                if (mapping.Key.IsAssignableFrom(exceptionType))
                {
                    return mapping.Value.Clone();
                }
            }

            return null;
        }

        /// <summary>
        /// Classifies an exception using custom rules
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <returns>Classification or null if no custom rules match</returns>
        private ErrorClassification ClassifyByCustomRules(Exception exception)
        {
            foreach (var classifier in _customClassifiers)
            {
                try
                {
                    var classification = classifier(exception);
                    if (classification != null)
                    {
                        return classification.Clone();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error classifier failed for exception of type {ExceptionType}", exception.GetType().Name);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a default classification for unknown exceptions
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <returns>Default classification</returns>
        private ErrorClassification CreateDefaultClassification(Exception exception)
        {
            return new ErrorClassification
            {
                Category = ErrorCategory.Unknown,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.None,
                IsRetryable = false,
                MaxRetries = 0,
                RetryDelay = TimeSpan.Zero,
                UserMessage = "An unexpected error occurred. Please try again later.",
                TechnicalMessage = exception.Message,
                ErrorCode = "UNKNOWN_ERROR"
            };
        }

        /// <summary>
        /// Initializes default exception type mappings
        /// </summary>
        private void InitializeDefaultMappings()
        {
            // Network errors
            _exceptionMappings[typeof(SocketException)] = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(5),
                UserMessage = "Network connection error. Please check your network connection and try again.",
                TechnicalMessage = "Network connectivity issue detected",
                ErrorCode = "NETWORK_ERROR"
            };

            // Process execution errors
            _exceptionMappings[typeof(ProcessExecutionException)] = new ErrorClassification
            {
                Category = ErrorCategory.Process,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 2,
                RetryDelay = TimeSpan.FromSeconds(2),
                UserMessage = "Process execution failed. The system will retry automatically.",
                TechnicalMessage = "Process execution encountered an error",
                ErrorCode = "PROCESS_ERROR"
            };

            // Timeout errors
            _exceptionMappings[typeof(TimeoutException)] = new ErrorClassification
            {
                Category = ErrorCategory.Timeout,
                Severity = ErrorSeverity.Warning,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 2,
                RetryDelay = TimeSpan.FromSeconds(1),
                UserMessage = "Operation timed out. The system will retry with a longer timeout.",
                TechnicalMessage = "Operation exceeded timeout limit",
                ErrorCode = "TIMEOUT_ERROR"
            };

            // Operation cancelled
            _exceptionMappings[typeof(OperationCanceledException)] = new ErrorClassification
            {
                Category = ErrorCategory.Unknown,
                Severity = ErrorSeverity.Information,
                Strategy = RecoveryStrategy.None,
                IsRetryable = false,
                MaxRetries = 0,
                RetryDelay = TimeSpan.Zero,
                UserMessage = "Operation was cancelled.",
                TechnicalMessage = "Operation was cancelled by user or system",
                ErrorCode = "OPERATION_CANCELLED"
            };

            // Configuration errors
            _exceptionMappings[typeof(ArgumentException)] = new ErrorClassification
            {
                Category = ErrorCategory.Configuration,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.None,
                IsRetryable = false,
                MaxRetries = 0,
                RetryDelay = TimeSpan.Zero,
                UserMessage = "Invalid configuration detected. Please check system settings.",
                TechnicalMessage = "Invalid argument or configuration provided",
                ErrorCode = "CONFIGURATION_ERROR"
            };

            // License server specific errors
            _exceptionMappings[typeof(LicenseManagerException)] = new ErrorClassification
            {
                Category = ErrorCategory.LicenseServer,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(3),
                UserMessage = "License server communication error. The system will retry.",
                TechnicalMessage = "License server operation failed",
                ErrorCode = "LICENSE_SERVER_ERROR"
            };

            // Add custom classifiers for specific scenarios
            _customClassifiers.Add(ClassifyProcessExecutionException);
            _customClassifiers.Add(ClassifyNetworkExceptions);
            _customClassifiers.Add(ClassifyLicenseServerErrors);
        }

        /// <summary>
        /// Custom classifier for process execution exceptions
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <returns>Classification or null</returns>
        private ErrorClassification ClassifyProcessExecutionException(Exception exception)
        {
            if (exception is ProcessExecutionException processEx)
            {
                var classification = new ErrorClassification
                {
                    Category = ErrorCategory.Process,
                    ErrorCode = "PROCESS_ERROR"
                };

                if (processEx.TimedOut)
                {
                    classification.Severity = ErrorSeverity.Warning;
                    classification.Strategy = RecoveryStrategy.Retry;
                    classification.IsRetryable = true;
                    classification.MaxRetries = 2;
                    classification.RetryDelay = TimeSpan.FromSeconds(5);
                    classification.UserMessage = "Process execution timed out. The system will retry with increased timeout.";
                    classification.TechnicalMessage = "Process execution exceeded timeout limit";
                }
                else if (processEx.ExitCode == 2) // File not found
                {
                    classification.Severity = ErrorSeverity.Critical;
                    classification.Strategy = RecoveryStrategy.None;
                    classification.IsRetryable = false;
                    classification.UserMessage = "Required executable not found. Please check system configuration.";
                    classification.TechnicalMessage = "Process executable not found";
                }
                else if (processEx.Cancelled)
                {
                    classification.Severity = ErrorSeverity.Information;
                    classification.Strategy = RecoveryStrategy.None;
                    classification.IsRetryable = false;
                    classification.UserMessage = "Process execution was cancelled.";
                    classification.TechnicalMessage = "Process execution cancelled by user or system";
                }
                else
                {
                    classification.Severity = ErrorSeverity.Error;
                    classification.Strategy = RecoveryStrategy.Retry;
                    classification.IsRetryable = true;
                    classification.MaxRetries = 2;
                    classification.RetryDelay = TimeSpan.FromSeconds(2);
                    classification.UserMessage = "Process execution failed. The system will retry automatically.";
                    classification.TechnicalMessage = "Process execution failed";
                }

                return classification;
            }

            return null;
        }

        /// <summary>
        /// Custom classifier for network exceptions
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <returns>Classification or null</returns>
        private ErrorClassification ClassifyNetworkExceptions(Exception exception)
        {
            if (exception is SocketException socketEx)
            {
                var classification = new ErrorClassification
                {
                    Category = ErrorCategory.Network,
                    Severity = ErrorSeverity.Error,
                    Strategy = RecoveryStrategy.Retry,
                    IsRetryable = true,
                    MaxRetries = 3,
                    RetryDelay = TimeSpan.FromSeconds(5),
                    ErrorCode = "NETWORK_ERROR"
                };

                switch (socketEx.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused:
                        classification.UserMessage = "Connection to server was refused. Please check if the server is running.";
                        classification.TechnicalMessage = "Connection refused by target server";
                        break;
                    case SocketError.TimedOut:
                        classification.UserMessage = "Network operation timed out. Please check your network connection.";
                        classification.TechnicalMessage = "Network operation timed out";
                        break;
                    case SocketError.HostNotFound:
                        classification.Severity = ErrorSeverity.Critical;
                        classification.Strategy = RecoveryStrategy.None;
                        classification.IsRetryable = false;
                        classification.UserMessage = "Server not found. Please check the server address.";
                        classification.TechnicalMessage = "Host not found";
                        break;
                    default:
                        classification.UserMessage = "Network connection error. Please check your network connection.";
                        classification.TechnicalMessage = $"Network error: {socketEx.SocketErrorCode}";
                        break;
                }

                return classification;
            }

            return null;
        }

        /// <summary>
        /// Custom classifier for license server errors
        /// </summary>
        /// <param name="exception">The exception to classify</param>
        /// <returns>Classification or null</returns>
        private ErrorClassification ClassifyLicenseServerErrors(Exception exception)
        {
            if (exception is LicenseManagerException licenseEx)
            {
                var classification = new ErrorClassification
                {
                    Category = ErrorCategory.LicenseServer,
                    ErrorCode = "LICENSE_SERVER_ERROR"
                };

                // Check error message patterns
                var message = licenseEx.Message.ToLowerInvariant();
                if (message.Contains("server down") || message.Contains("not responding"))
                {
                    classification.Severity = ErrorSeverity.Critical;
                    classification.Strategy = RecoveryStrategy.Escalate;
                    classification.IsRetryable = false;
                    classification.UserMessage = "License server is not responding. Please contact system administrator.";
                    classification.TechnicalMessage = "License server appears to be down";
                }
                else if (message.Contains("invalid license") || message.Contains("license not found"))
                {
                    classification.Severity = ErrorSeverity.Error;
                    classification.Strategy = RecoveryStrategy.None;
                    classification.IsRetryable = false;
                    classification.UserMessage = "Invalid license configuration detected. Please check license settings.";
                    classification.TechnicalMessage = "License validation failed";
                }
                else
                {
                    classification.Severity = ErrorSeverity.Error;
                    classification.Strategy = RecoveryStrategy.Retry;
                    classification.IsRetryable = true;
                    classification.MaxRetries = 3;
                    classification.RetryDelay = TimeSpan.FromSeconds(3);
                    classification.UserMessage = "License server communication error. The system will retry.";
                    classification.TechnicalMessage = "License server operation failed";
                }

                return classification;
            }

            return null;
        }

        /// <summary>
        /// Logs the error classification
        /// </summary>
        /// <param name="exception">The original exception</param>
        /// <param name="classification">The classification result</param>
        private void LogClassification(Exception exception, ErrorClassification classification)
        {
            if (!classification.ShouldLog)
                return;

            var logLevel = classification.Severity switch
            {
                ErrorSeverity.Information => LogLevel.Information,
                ErrorSeverity.Warning => LogLevel.Warning,
                ErrorSeverity.Error => LogLevel.Error,
                ErrorSeverity.Critical => LogLevel.Critical,
                _ => LogLevel.Error
            };

            _logger.Log(logLevel, exception,
                "Error classified: {Category} - {Severity} - {Strategy} - {ErrorCode} - {UserMessage}",
                classification.Category, classification.Severity, classification.Strategy,
                classification.ErrorCode, classification.UserMessage);
        }

        /// <summary>
        /// Registers a custom exception mapping
        /// </summary>
        /// <param name="exceptionType">The exception type</param>
        /// <param name="classification">The classification</param>
        public void RegisterExceptionMapping(Type exceptionType, ErrorClassification classification)
        {
            if (exceptionType == null)
                throw new ArgumentNullException(nameof(exceptionType));
            if (classification == null)
                throw new ArgumentNullException(nameof(classification));

            _exceptionMappings[exceptionType] = classification;
        }

        /// <summary>
        /// Registers a custom classifier function
        /// </summary>
        /// <param name="classifier">The classifier function</param>
        public void RegisterCustomClassifier(Func<Exception, ErrorClassification> classifier)
        {
            if (classifier == null)
                throw new ArgumentNullException(nameof(classifier));

            _customClassifiers.Add(classifier);
        }

        /// <summary>
        /// Gets recommended retry policy for a classification
        /// </summary>
        /// <param name="classification">The error classification</param>
        /// <returns>Recommended retry policy options</returns>
        public RetryPolicyOptions GetRecommendedRetryPolicy(ErrorClassification classification)
        {
            if (!classification.IsRetryable)
            {
                return new RetryPolicyOptions { MaxRetries = 0 };
            }

            return new RetryPolicyOptions
            {
                MaxRetries = classification.MaxRetries,
                InitialDelay = classification.RetryDelay,
                Strategy = RetryStrategyType.Exponential,
                BackoffMultiplier = 2.0,
                JitterFactor = 0.1,
                RetryOnTimeout = true,
                RetryOnTransientErrors = true
            };
        }
    }

    /// <summary>
    /// Extension methods for ErrorClassification
    /// </summary>
    public static class ErrorClassificationExtensions
    {
        /// <summary>
        /// Creates a clone of the error classification
        /// </summary>
        /// <param name="classification">The classification to clone</param>
        /// <returns>A new instance with the same values</returns>
        public static ErrorClassification Clone(this ErrorClassification classification)
        {
            if (classification == null)
                return null;

            return new ErrorClassification
            {
                Category = classification.Category,
                Severity = classification.Severity,
                Strategy = classification.Strategy,
                IsRetryable = classification.IsRetryable,
                MaxRetries = classification.MaxRetries,
                RetryDelay = classification.RetryDelay,
                UserMessage = classification.UserMessage,
                TechnicalMessage = classification.TechnicalMessage,
                ErrorCode = classification.ErrorCode,
                Context = classification.Context != null ? new Dictionary<string, object>(classification.Context) : null,
                Timestamp = classification.Timestamp,
                SourceOperation = classification.SourceOperation
            };
        }
    }
}