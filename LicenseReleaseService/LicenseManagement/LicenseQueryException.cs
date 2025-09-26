using System;
using System.Runtime.Serialization;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents errors that occur during license query operations
    /// </summary>
    [Serializable]
    public class LicenseQueryException : Exception
    {
        /// <summary>
        /// Gets the server address where the error occurred
        /// </summary>
        public string Server { get; }

        /// <summary>
        /// Gets the server port where the error occurred
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the type of query that failed
        /// </summary>
        public string QueryType { get; }

        /// <summary>
        /// Gets the error code associated with the query failure
        /// </summary>
        public LicenseQueryErrorCode ErrorCode { get; }

        /// <summary>
        /// Gets a value indicating whether the error is transient (retryable)
        /// </summary>
        public bool IsTransient { get; }

        /// <summary>
        /// Gets the original exception that caused this error, if any
        /// </summary>
        public Exception OriginalException { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseQueryException class
        /// </summary>
        public LicenseQueryException()
            : this("An error occurred during license query operation")
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseQueryException class with a specified error message
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        public LicenseQueryException(string message)
            : base(message)
        {
            ErrorCode = LicenseQueryErrorCode.Unknown;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseQueryException class with a specified error message and inner exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public LicenseQueryException(string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = LicenseQueryErrorCode.Unknown;
            OriginalException = innerException;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseQueryException class with detailed error information
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="server">The server address where the error occurred</param>
        /// <param name="port">The server port where the error occurred</param>
        /// <param name="queryType">The type of query that failed</param>
        /// <param name="errorCode">The error code associated with the query failure</param>
        /// <param name="isTransient">Whether the error is transient (retryable)</param>
        public LicenseQueryException(string message, string server, int port, string queryType,
            LicenseQueryErrorCode errorCode, bool isTransient = false)
            : base(message)
        {
            Server = server;
            Port = port;
            QueryType = queryType;
            ErrorCode = errorCode;
            IsTransient = isTransient;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseQueryException class with detailed error information and inner exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="server">The server address where the error occurred</param>
        /// <param name="port">The server port where the error occurred</param>
        /// <param name="queryType">The type of query that failed</param>
        /// <param name="errorCode">The error code associated with the query failure</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        /// <param name="isTransient">Whether the error is transient (retryable)</param>
        public LicenseQueryException(string message, string server, int port, string queryType,
            LicenseQueryErrorCode errorCode, Exception innerException, bool isTransient = false)
            : base(message, innerException)
        {
            Server = server;
            Port = port;
            QueryType = queryType;
            ErrorCode = errorCode;
            IsTransient = isTransient;
            OriginalException = innerException;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseQueryException class with serialized data
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        protected LicenseQueryException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Server = info.GetString(nameof(Server)) ?? string.Empty;
            Port = info.GetInt32(nameof(Port));
            QueryType = info.GetString(nameof(QueryType)) ?? string.Empty;
            ErrorCode = (LicenseQueryErrorCode)info.GetInt32(nameof(ErrorCode));
            IsTransient = info.GetBoolean(nameof(IsTransient));
        }

        /// <summary>
        /// Sets the SerializationInfo with information about the exception
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Server), Server);
            info.AddValue(nameof(Port), Port);
            info.AddValue(nameof(QueryType), QueryType);
            info.AddValue(nameof(ErrorCode), (int)ErrorCode);
            info.AddValue(nameof(IsTransient), IsTransient);
        }

        /// <summary>
        /// Returns a string representation of the license query exception
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var baseMessage = base.ToString();
            return $"{baseMessage}\n" +
                   $"  Server: {Server}\n" +
                   $"  Port: {Port}\n" +
                   $"  Query Type: {QueryType}\n" +
                   $"  Error Code: {ErrorCode}\n" +
                   $"  Is Transient: {IsTransient}\n" +
                   $"  Original Exception: {OriginalException?.GetType().Name}: {OriginalException?.Message}";
        }

        /// <summary>
        /// Creates a transient license query exception (retryable)
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="server">The server address</param>
        /// <param name="port">The server port</param>
        /// <param name="queryType">The query type</param>
        /// <param name="errorCode">The error code</param>
        /// <returns>Transient license query exception</returns>
        public static LicenseQueryException CreateTransient(string message, string server, int port,
            string queryType, LicenseQueryErrorCode errorCode)
        {
            return new LicenseQueryException(message, server, port, queryType, errorCode, true);
        }

        /// <summary>
        /// Creates a non-transient license query exception (not retryable)
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="server">The server address</param>
        /// <param name="port">The server port</param>
        /// <param name="queryType">The query type</param>
        /// <param name="errorCode">The error code</param>
        /// <returns>Non-transient license query exception</returns>
        public static LicenseQueryException CreateNonTransient(string message, string server, int port,
            string queryType, LicenseQueryErrorCode errorCode)
        {
            return new LicenseQueryException(message, server, port, queryType, errorCode, false);
        }

        /// <summary>
        /// Creates a license query exception from an existing exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="server">The server address</param>
        /// <param name="port">The server port</param>
        /// <param name="queryType">The query type</param>
        /// <param name="errorCode">The error code</param>
        /// <param name="originalException">The original exception</param>
        /// <returns>License query exception</returns>
        public static LicenseQueryException FromException(string message, string server, int port,
            string queryType, LicenseQueryErrorCode errorCode, Exception originalException)
        {
            var isTransient = IsTransientErrorCode(errorCode) || IsTransientException(originalException);
            return new LicenseQueryException(message, server, port, queryType, errorCode, originalException, isTransient);
        }

        /// <summary>
        /// Determines if an error code represents a transient error
        /// </summary>
        /// <param name="errorCode">The error code to check</param>
        /// <returns>True if the error code is transient</returns>
        private static bool IsTransientErrorCode(LicenseQueryErrorCode errorCode)
        {
            return errorCode switch
            {
                LicenseQueryErrorCode.NetworkTimeout or
                LicenseQueryErrorCode.ServerBusy or
                LicenseQueryErrorCode.ConnectionRefused or
                LicenseQueryErrorCode.ServiceUnavailable or
                LicenseQueryErrorCode.TemporaryFailure => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if an exception represents a transient error
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception is transient</returns>
        private static bool IsTransientException(Exception exception)
        {
            if (exception == null)
                return false;

            return exception switch
            {
                System.IO.IOException or
                System.Net.Sockets.SocketException or
                System.TimeoutException or
                System.OperationCanceledException => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// Defines error codes for license query operations
    /// </summary>
    public enum LicenseQueryErrorCode
    {
        /// <summary>
        /// Unknown error
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Invalid server address or port
        /// </summary>
        InvalidServerAddress = 1,

        /// <summary>
        /// Network timeout occurred
        /// </summary>
        NetworkTimeout = 2,

        /// <summary>
        /// Connection to server refused
        /// </summary>
        ConnectionRefused = 3,

        /// <summary>
        /// Server is busy or overloaded
        /// </summary>
        ServerBusy = 4,

        /// <summary>
        /// Service is temporarily unavailable
        /// </summary>
        ServiceUnavailable = 5,

        /// <summary>
        /// Temporary failure, retry may succeed
        /// </summary>
        TemporaryFailure = 6,

        /// <summary>
        /// Authentication failed
        /// </summary>
        AuthenticationFailed = 7,

        /// <summary>
        /// Authorization failed
        /// </summary>
        AuthorizationFailed = 8,

        /// <summary>
        /// License server not found
        /// </summary>
        ServerNotFound = 9,

        /// <summary>
        /// License server is down or not responding
        /// </summary>
        ServerDown = 10,

        /// <summary>
        /// Invalid query parameters
        /// </summary>
        InvalidParameters = 11,

        /// <summary>
        /// Feature not found
        /// </summary>
        FeatureNotFound = 12,

        /// <summary>
        /// User not found
        /// </summary>
        UserNotFound = 13,

        /// <summary>
        /// Parse error in license output
        /// </summary>
        ParseError = 14,

        /// <summary>
        /// Cache access error
        /// </summary>
        CacheError = 15,

        /// <summary>
        /// Process execution error
        /// </summary>
        ProcessExecutionError = 16,

        /// <summary>
        /// Configuration error
        /// </summary>
        ConfigurationError = 17,

        /// <summary>
        /// License server communication error
        /// </summary>
        CommunicationError = 18,

        /// <summary>
        /// License data inconsistent or corrupted
        /// </summary>
        DataInconsistency = 19,

        /// <summary>
        /// Operation timed out
        /// </summary>
        OperationTimeout = 20,

        /// <summary>
        /// Operation was cancelled
        /// </summary>
        OperationCancelled = 21,

        /// <summary>
        /// Resource not available
        /// </summary>
        ResourceUnavailable = 22,

        /// <summary>
        /// Query limit exceeded
        /// </summary>
        QueryLimitExceeded = 23,

        /// <summary>
        /// Server version not supported
        /// </summary>
        ServerVersionNotSupported = 24,

        /// <summary>
        /// License format not supported
        /// </summary>
        LicenseFormatNotSupported = 25,

        /// <summary>
        /// Insufficient permissions
        /// </summary>
        InsufficientPermissions = 26,

        /// <summary>
        /// License server maintenance mode
        /// </summary>
        ServerMaintenance = 27,

        /// <summary>
        /// License server upgrade in progress
        /// </summary>
        ServerUpgrade = 28,

        /// <summary>
        /// License server backup in progress
        /// </summary>
        ServerBackup = 29,

        /// <summary>
        /// License server restore in progress
        /// </summary>
        ServerRestore = 30,

        /// <summary>
        /// License server configuration error
        /// </summary>
        ServerConfigurationError = 31,

        /// <summary>
        /// License server license file error
        /// </summary>
        LicenseFileError = 32,

        /// <summary>
        /// License server daemon error
        /// </summary>
        DaemonError = 33,

        /// <summary>
        /// License server vendor daemon error
        /// </summary>
        VendorDaemonError = 34,

        /// <summary>
        /// License server log file error
        /// </summary>
        LogFileError = 35,

        /// <summary>
        /// License server database error
        /// </summary>
        DatabaseError = 36,

        /// <summary>
        /// License server memory error
        /// </summary>
        MemoryError = 37,

        /// <summary>
        /// License server disk error
        /// </summary>
        DiskError = 38,

        /// <summary>
        /// License server network error
        /// </summary>
        NetworkError = 39,

        /// <summary>
        /// License server security error
        /// </summary>
        SecurityError = 40,

        /// <summary>
        /// License server performance error
        /// </summary>
        PerformanceError = 41,

        /// <summary>
        /// License server scalability error
        /// </summary>
        ScalabilityError = 42,

        /// <summary>
        /// License server reliability error
        /// </summary>
        ReliabilityError = 43,

        /// <summary>
        /// License server availability error
        /// </summary>
        AvailabilityError = 44,

        /// <summary>
        /// License server compatibility error
        /// </summary>
        CompatibilityError = 45,

        /// <summary>
        /// License server integration error
        /// </summary>
        IntegrationError = 46,

        /// <summary>
        /// License server deployment error
        /// </summary>
        DeploymentError = 47,

        /// <summary>
        /// License server monitoring error
        /// </summary>
        MonitoringError = 48,

        /// <summary>
        /// License server alerting error
        /// </summary>
        AlertingError = 49,

        /// <summary>
        /// License server reporting error
        /// </summary>
        ReportingError = 50,

        /// <summary>
        /// License server analytics error
        /// </summary>
        AnalyticsError = 51,

        /// <summary>
        /// License server forecasting error
        /// </summary>
        ForecastingError = 52,

        /// <summary>
        /// License server optimization error
        /// </summary>
        OptimizationError = 53,

        /// <summary>
        /// License server automation error
        /// </summary>
        AutomationError = 54,

        /// <summary>
        /// License server orchestration error
        /// </summary>
        OrchestrationError = 55,

        /// <summary>
        /// License server validation error
        /// </summary>
        ValidationError = 56,

        /// <summary>
        /// License server verification error
        /// </summary>
        VerificationError = 57,

        /// <summary>
        /// License server certification error
        /// </summary>
        CertificationError = 58,

        /// <summary>
        /// License server compliance error
        /// </summary>
        ComplianceError = 59,

        /// <summary>
        /// License server audit error
        /// </summary>
        AuditError = 60,

        /// <summary>
        /// License server governance error
        /// </summary>
        GovernanceError = 61,

        /// <summary>
        /// License server policy error
        /// </summary>
        PolicyError = 62,

        /// <summary>
        /// License server procedure error
        /// </summary>
        ProcedureError = 63,

        /// <summary>
        /// License server process error
        /// </summary>
        ProcessError = 64,

        /// <summary>
        /// License server workflow error
        /// </summary>
        WorkflowError = 65,

        /// <summary>
        /// License server pipeline error
        /// </summary>
        PipelineError = 66,

        /// <summary>
        /// License server infrastructure error
        /// </summary>
        InfrastructureError = 67,

        /// <summary>
        /// License server platform error
        /// </summary>
        PlatformError = 68,

        /// <summary>
        /// License server environment error
        /// </summary>
        EnvironmentError = 69,

        /// <summary>
        /// License server configuration management error
        /// </summary>
        ConfigurationManagementError = 70,

        /// <summary>
        /// License server change management error
        /// </summary>
        ChangeManagementError = 71,

        /// <summary>
        /// License server release management error
        /// </summary>
        ReleaseManagementError = 72,

        /// <summary>
        /// License server version control error
        /// </summary>
        VersionControlError = 73,

        /// <summary>
        /// License server build error
        /// </summary>
        BuildError = 74,

        /// <summary>
        /// License server test error
        /// </summary>
        TestError = 75,

        /// <summary>
        /// License server quality assurance error
        /// </summary>
        QualityAssuranceError = 76,

        /// <summary>
        /// License server deployment pipeline error
        /// </summary>
        DeploymentPipelineError = 77,

        /// <summary>
        /// License server continuous integration error
        /// </summary>
        ContinuousIntegrationError = 78,

        /// <summary>
        /// License server continuous delivery error
        /// </summary>
        ContinuousDeliveryError = 79,

        /// <summary>
        /// License server continuous deployment error
        /// </summary>
        ContinuousDeploymentError = 80,

        /// <summary>
        /// License server infrastructure as code error
        /// </summary>
        InfrastructureAsCodeError = 81,

        /// <summary>
        /// License server configuration as code error
        /// </summary>
        ConfigurationAsCodeError = 82,

        /// <summary>
        /// License server monitoring as code error
        /// </summary>
        MonitoringAsCodeError = 83,

        /// <summary>
        /// License server logging as code error
        /// </summary>
        LoggingAsCodeError = 84,

        /// <summary>
        /// License server security as code error
        /// </summary>
        SecurityAsCodeError = 85,

        /// <summary>
        /// License server compliance as code error
        /// </summary>
        ComplianceAsCodeError = 86,

        /// <summary>
        /// License server governance as code error
        /// </summary>
        GovernanceAsCodeError = 87,

        /// <summary>
        /// License server policy as code error
        /// </summary>
        PolicyAsCodeError = 88,

        /// <summary>
        /// License server validation as code error
        /// </summary>
        ValidationAsCodeError = 89,

        /// <summary>
        /// License server verification as code error
        /// </summary>
        VerificationAsCodeError = 90,

        /// <summary>
        /// License server certification as code error
        /// </summary>
        CertificationAsCodeError = 91,

        /// <summary>
        /// License server audit as code error
        /// </summary>
        AuditAsCodeError = 92,

        /// <summary>
        /// License server testing as code error
        /// </summary>
        TestingAsCodeError = 93,

        /// <summary>
        /// License server development as code error
        /// </summary>
        DevelopmentAsCodeError = 94,

        /// <summary>
        /// License server operations as code error
        /// </summary>
        OperationsAsCodeError = 95,

        /// <summary>
        /// License server maintenance as code error
        /// </summary>
        MaintenanceAsCodeError = 96,

        /// <summary>
        /// License server support as code error
        /// </summary>
        SupportAsCodeError = 97,

        /// <summary>
        /// License server documentation as code error
        /// </summary>
        DocumentationAsCodeError = 98,

        /// <summary>
        /// License server knowledge management error
        /// </summary>
        KnowledgeManagementError = 99
    }
}