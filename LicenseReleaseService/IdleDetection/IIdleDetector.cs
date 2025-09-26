using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Defines the contract for idle detection strategies
    /// </summary>
    public interface IIdleDetector : IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets the name of the detector
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the detector
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the version of the detector
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets a value indicating whether the detector is enabled
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the detector is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets the detector priority (higher values = higher priority)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the supported detection methods
        /// </summary>
        IReadOnlyList<string> SupportedMethods { get; }

        /// <summary>
        /// Gets the current detector status
        /// </summary>
        DetectorStatus Status { get; }

        /// <summary>
        /// Gets the detector configuration
        /// </summary>
        IdleDetectorConfiguration Configuration { get; }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when idle state is detected
        /// </summary>
        event EventHandler<IdleDetectionEventArgs> IdleDetected;

        /// <summary>
        /// Event raised when activity is detected after idle state
        /// </summary>
        event EventHandler<IdleDetectionEventArgs> ActivityDetected;

        /// <summary>
        /// Event raised when detector status changes
        /// </summary>
        event EventHandler<DetectorStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Event raised when detector encounters an error
        /// </summary>
        event EventHandler<DetectorErrorEventArgs> ErrorOccurred;

        #endregion

        #region Initialization and Lifecycle

        /// <summary>
        /// Initializes the detector with configuration
        /// </summary>
        /// <param name="configuration">The detector configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the initialization operation</returns>
        Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the detector
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the detector
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses the detector
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the pause operation</returns>
        Task PauseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes the detector
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the resume operation</returns>
        Task ResumeAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Detection Methods

        /// <summary>
        /// Detects idle state for a specific process
        /// </summary>
        /// <param name="processId">The process identifier</param>
        /// <param name="userName">The user name associated with the process</param>
        /// <param name="computerName">The computer name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Idle detection result</returns>
        Task<IdleDetectionResult> DetectIdleAsync(int processId, string userName, string computerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Detects idle state for multiple processes
        /// </summary>
        /// <param name="processes">The processes to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of idle detection results</returns>
        Task<IList<IdleDetectionResult>> DetectIdleAsync(IList<ProcessInfo> processes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current idle time for a process
        /// </summary>
        /// <param name="processId">The process identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Idle time duration</returns>
        Task<TimeSpan> GetIdleTimeAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a process is currently idle
        /// </summary>
        /// <param name="processId">The process identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the process is idle</returns>
        Task<bool> IsIdleAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed idle information for a process
        /// </summary>
        /// <param name="processId">The process identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed idle information</returns>
        Task<IdleInfo> GetIdleInfoAsync(int processId, CancellationToken cancellationToken = default);

        #endregion

        #region Configuration and Management

        /// <summary>
        /// Updates the detector configuration
        /// </summary>
        /// <param name="configuration">The new configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the update operation</returns>
        Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the detector health status
        /// </summary>
        /// <returns>Health status information</returns>
        Task<DetectorHealth> GetHealthAsync();

        /// <summary>
        /// Gets detector statistics and metrics
        /// </summary>
        /// <returns>Detector statistics</returns>
        Task<DetectorStatistics> GetStatisticsAsync();

        /// <summary>
        /// Resets the detector statistics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the reset operation</returns>
        Task ResetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the detector configuration
        /// </summary>
        /// <param name="configuration">The configuration to validate</param>
        /// <returns>Validation result</returns>
        ValidationResult ValidateConfiguration(IdleDetectorConfiguration configuration);

        #endregion
    }

    /// <summary>
    /// Represents an idle detection result
    /// </summary>
    public class IdleDetectionResult
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets a value indicating whether the process is idle
        /// </summary>
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets the idle time duration
        /// </summary>
        public TimeSpan IdleTime { get; set; }

        /// <summary>
        /// Gets the confidence level (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets the detection method used
        /// </summary>
        public string DetectionMethod { get; set; }

        /// <summary>
        /// Gets the detection timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the detector name
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Gets the reason for the detection result
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Initializes a new instance of the IdleDetectionResult class
        /// </summary>
        public IdleDetectionResult()
        {
            SessionId = Guid.NewGuid().ToString("N")[..8];
            Timestamp = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            Confidence = 0.0;
            IdleTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Returns a string representation of the idle detection result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"IdleDetectionResult: Session={SessionId}, Process={ProcessId}, " +
                   $"IsIdle={IsIdle}, IdleTime={IdleTime.TotalMinutes:F1}m, " +
                   $"Confidence={Confidence:F2}, Method={DetectionMethod}, " +
                   $"Detector={DetectorName}, Reason={Reason}";
        }
    }

    /// <summary>
    /// Represents detailed idle information
    /// </summary>
    public class IdleInfo
    {
        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the current idle time
        /// </summary>
        public TimeSpan IdleTime { get; set; }

        /// <summary>
        /// Gets the last activity timestamp
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets the session state
        /// </summary>
        public SessionState State { get; set; }

        /// <summary>
        /// Gets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets the detection methods used
        /// </summary>
        public List<string> DetectionMethods { get; set; }

        /// <summary>
        /// Gets additional information
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the IdleInfo class
        /// </summary>
        public IdleInfo()
        {
            DetectionMethods = new List<string>();
            AdditionalInfo = new Dictionary<string, object>();
            Confidence = 0.0;
            State = SessionState.Unknown;
            LastActivity = DateTime.UtcNow;
            IdleTime = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Represents process information for idle detection
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the process name
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets the process start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets additional process information
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the ProcessInfo class
        /// </summary>
        public ProcessInfo()
        {
            AdditionalInfo = new Dictionary<string, object>();
            StartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the ProcessInfo class
        /// </summary>
        /// <param name="processId">The process identifier</param>
        /// <param name="processName">The process name</param>
        /// <param name="userName">The user name</param>
        /// <param name="computerName">The computer name</param>
        public ProcessInfo(int processId, string processName, string userName, string computerName)
        {
            ProcessId = processId;
            ProcessName = processName;
            UserName = userName;
            ComputerName = computerName;
            AdditionalInfo = new Dictionary<string, object>();
            StartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Defines the detector status
    /// </summary>
    public enum DetectorStatus
    {
        /// <summary>
        /// Detector is not initialized
        /// </summary>
        NotInitialized,

        /// <summary>
        /// Detector is initialized but not running
        /// </summary>
        Initialized,

        /// <summary>
        /// Detector is running normally
        /// </summary>
        Running,

        /// <summary>
        /// Detector is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Detector is stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// Detector is in error state
        /// </summary>
        Error,

        /// <summary>
        /// Detector is disposing
        /// </summary>
        Disposing
    }

    /// <summary>
    /// Event arguments for detector status changes
    /// </summary>
    public class DetectorStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the detector name
        /// </summary>
        public string DetectorName { get; }

        /// <summary>
        /// Gets the previous status
        /// </summary>
        public DetectorStatus PreviousStatus { get; }

        /// <summary>
        /// Gets the new status
        /// </summary>
        public DetectorStatus NewStatus { get; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the reason for the status change
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the DetectorStatusChangedEventArgs class
        /// </summary>
        /// <param name="detectorName">The detector name</param>
        /// <param name="previousStatus">The previous status</param>
        /// <param name="newStatus">The new status</param>
        /// <param name="reason">The reason for the change</param>
        public DetectorStatusChangedEventArgs(string detectorName, DetectorStatus previousStatus, DetectorStatus newStatus, string reason)
        {
            DetectorName = detectorName ?? throw new ArgumentNullException(nameof(detectorName));
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the status change event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"DetectorStatusChanged: {DetectorName}, {PreviousStatus} -> {NewStatus}, Reason: {Reason}";
        }
    }

    /// <summary>
    /// Event arguments for detector errors
    /// </summary>
    public class DetectorErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the detector name
        /// </summary>
        public string DetectorName { get; }

        /// <summary>
        /// Gets the error exception
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the error severity
        /// </summary>
        public ErrorSeverity Severity { get; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the operation that failed
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets additional context
        /// </summary>
        public Dictionary<string, object> Context { get; }

        /// <summary>
        /// Initializes a new instance of the DetectorErrorEventArgs class
        /// </summary>
        /// <param name="detectorName">The detector name</param>
        /// <param name="error">The error exception</param>
        /// <param name="severity">The error severity</param>
        /// <param name="operation">The operation that failed</param>
        public DetectorErrorEventArgs(string detectorName, Exception error, ErrorSeverity severity, string operation)
        {
            DetectorName = detectorName ?? throw new ArgumentNullException(nameof(detectorName));
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Severity = severity;
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// Adds context information
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="value">The context value</param>
        public void AddContext(string key, object value)
        {
            Context[key] = value;
        }

        /// <summary>
        /// Returns a string representation of the error event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"DetectorError: {DetectorName}, {Severity}, Operation: {Operation}, Error: {Error.Message}";
        }
    }

    /// <summary>
    /// Defines error severity levels
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Low severity error
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity error
        /// </summary>
        Medium,

        /// <summary>
        /// High severity error
        /// </summary>
        High,

        /// <summary>
        /// Critical severity error
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents detector health information
    /// </summary>
    public class DetectorHealth
    {
        /// <summary>
        /// Gets the detector name
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets a value indicating whether the detector is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets the health check timestamp
        /// </summary>
        public DateTime CheckTimestamp { get; set; }

        /// <summary>
        /// Gets the health status message
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Gets the last error, if any
        /// </summary>
        public Exception LastError { get; set; }

        /// <summary>
        /// Gets additional health information
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the DetectorHealth class
        /// </summary>
        public DetectorHealth()
        {
            AdditionalInfo = new Dictionary<string, object>();
            CheckTimestamp = DateTime.UtcNow;
            IsHealthy = true;
            StatusMessage = "Healthy";
        }
    }

    /// <summary>
    /// Represents detector statistics
    /// </summary>
    public class DetectorStatistics
    {
        /// <summary>
        /// Gets the detector name
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets the total number of detections performed
        /// </summary>
        public long TotalDetections { get; set; }

        /// <summary>
        /// Gets the number of successful detections
        /// </summary>
        public long SuccessfulDetections { get; set; }

        /// <summary>
        /// Gets the number of failed detections
        /// </summary>
        public long FailedDetections { get; set; }

        /// <summary>
        /// Gets the number of idle states detected
        /// </summary>
        public long IdleStatesDetected { get; set; }

        /// <summary>
        /// Gets the number of active states detected
        /// </summary>
        public long ActiveStatesDetected { get; set; }

        /// <summary>
        /// Gets the average detection time in milliseconds
        /// </summary>
        public double AverageDetectionTimeMs { get; set; }

        /// <summary>
        /// Gets the uptime duration
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the last detection timestamp
        /// </summary>
        public DateTime? LastDetectionTimestamp { get; set; }

        /// <summary>
        /// Gets the error count
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Gets the statistics start timestamp
        /// </summary>
        public DateTime StatisticsStartTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the DetectorStatistics class
        /// </summary>
        public DetectorStatistics()
        {
            StatisticsStartTime = DateTime.UtcNow;
            TotalDetections = 0;
            SuccessfulDetections = 0;
            FailedDetections = 0;
            IdleStatesDetected = 0;
            ActiveStatesDetected = 0;
            AverageDetectionTimeMs = 0.0;
            Uptime = TimeSpan.Zero;
            ErrorCount = 0;
        }
    }

    /// <summary>
    /// Represents configuration for idle detectors
    /// </summary>
    public class IdleDetectorConfiguration
    {
        /// <summary>
        /// Gets or sets the detection interval in seconds
        /// </summary>
        public int DetectionIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the idle threshold in seconds
        /// </summary>
        public int IdleThresholdSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets the confidence threshold (0.0 to 1.0)
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets a value indicating whether the detector is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the detector priority
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum detection time in milliseconds
        /// </summary>
        public int MaxDetectionTimeMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the timeout for operations in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the retry count for failed operations
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the retry delay in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum concurrent operations
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = 5;

        /// <summary>
        /// Gets or sets custom configuration parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; }

        /// <summary>
        /// Initializes a new instance of the IdleDetectorConfiguration class
        /// </summary>
        public IdleDetectorConfiguration()
        {
            CustomParameters = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents a validation result
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation is successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the validation errors
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Gets the validation warnings
        /// </summary>
        public List<string> Warnings { get; set; }

        /// <summary>
        /// Initializes a new instance of the ValidationResult class
        /// </summary>
        public ValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            IsValid = true;
        }

        /// <summary>
        /// Adds a validation error
        /// </summary>
        /// <param name="error">The error message</param>
        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// Adds a validation warning
        /// </summary>
        /// <param name="warning">The warning message</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }
}