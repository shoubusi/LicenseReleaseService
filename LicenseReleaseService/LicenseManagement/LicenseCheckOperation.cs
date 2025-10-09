using System;
using System.Collections.Generic;
using System.Threading;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents a license check operation with its parameters and configuration
    /// </summary>
    public class LicenseCheckOperation
    {
        /// <summary>
        /// Gets or sets the unique identifier for this operation
        /// </summary>
        public Guid OperationId { get; set; }

        /// <summary>
        /// Gets or sets the type of license check operation
        /// </summary>
        public LicenseCheckOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the license server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the feature name (for feature-specific operations)
        /// </summary>
        public string Feature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name (for user-specific operations)
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public LicenseCheckOperationPriority Priority { get; set; } = LicenseCheckOperationPriority.Normal;

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retry attempts
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets additional operation parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the operation description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation tags for categorization
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the operation creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the operation scheduled execution timestamp
        /// </summary>
        public DateTime? ScheduledFor { get; set; }

        /// <summary>
        /// Gets or sets the operation execution timestamp
        /// </summary>
        public DateTime? ExecutedAt { get; set; }

        /// <summary>
        /// Gets or sets the operation completion timestamp
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the operation completion timestamp (alias for CompletedAt)
        /// </summary>
        public DateTime? CompletedTime
        {
            get => CompletedAt;
            set => CompletedAt = value;
        }

        /// <summary>
        /// Gets or sets the operation status
        /// </summary>
        public LicenseCheckOperationStatus Status { get; set; } = LicenseCheckOperationStatus.Pending;

        /// <summary>
        /// Gets or sets the cancellation token for this operation
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets the retry attempt count
        /// </summary>
        public int RetryAttempt { get; set; }

        /// <summary>
        /// Gets or sets the last error message
        /// </summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation execution context
        /// </summary>
        public object ExecutionContext { get; set; }

        /// <summary>
        /// Gets or sets whether this operation is recurring
        /// </summary>
        public bool IsRecurring { get; set; }

        /// <summary>
        /// Gets or sets the recurring interval for this operation
        /// </summary>
        public TimeSpan? RecurringInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of recurring executions
        /// </summary>
        public int? MaxRecurringExecutions { get; set; }

        /// <summary>
        /// Gets or sets the current recurring execution count
        /// </summary>
        public int RecurringExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the operation dependencies (must complete before this operation)
        /// </summary>
        public List<Guid> Dependencies { get; set; } = new List<Guid>();

        /// <summary>
        /// Gets or sets whether to cache the operation result
        /// </summary>
        public bool CacheResult { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache duration for the operation result
        /// </summary>
        public TimeSpan? CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to enable detailed logging for this operation
        /// </summary>
        public bool EnableDetailedLogging { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation is ready to execute
        /// </summary>
        public bool IsReadyToExecute => Status == LicenseCheckOperationStatus.Pending &&
                                      (ScheduledFor == null || ScheduledFor <= DateTime.Now);

        /// <summary>
        /// Gets a value indicating whether the operation is currently executing
        /// </summary>
        public bool IsExecuting => Status == LicenseCheckOperationStatus.Executing;

        /// <summary>
        /// Gets a value indicating whether the operation has completed
        /// </summary>
        public bool IsCompleted => Status == LicenseCheckOperationStatus.Completed ||
                                   Status == LicenseCheckOperationStatus.Failed ||
                                   Status == LicenseCheckOperationStatus.Cancelled;

        /// <summary>
        /// Gets a value indicating whether the operation can be retried
        /// </summary>
        public bool CanRetry => Status == LicenseCheckOperationStatus.Failed &&
                              RetryAttempt < MaxRetries &&
                              MaxRetries > 0;

        /// <summary>
        /// Gets a value indicating whether the operation has exceeded its timeout
        /// </summary>
        public bool IsTimedOut => ExecutedAt.HasValue &&
                                DateTime.Now - ExecutedAt.Value > Timeout;

        /// <summary>
        /// Gets the operation duration
        /// </summary>
        public TimeSpan? Duration => ExecutedAt.HasValue && CompletedAt.HasValue
                                    ? CompletedAt.Value - ExecutedAt.Value
                                    : null;

        /// <summary>
        /// Gets the operation age
        /// </summary>
        public TimeSpan Age => DateTime.Now - CreatedAt;

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperation class
        /// </summary>
        public LicenseCheckOperation()
        {
            OperationId = Guid.NewGuid();
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperation class with specified parameters
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        public LicenseCheckOperation(LicenseCheckOperationType operationType, string server, int port) : this()
        {
            OperationType = operationType;
            Server = server;
            Port = port;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperation class with all parameters
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="priority">Operation priority</param>
        /// <param name="timeout">Operation timeout</param>
        /// <param name="maxRetries">Maximum retry attempts</param>
        /// <param name="retryDelay">Delay between retries</param>
        public LicenseCheckOperation(LicenseCheckOperationType operationType, string server, int port,
                                  string feature, string user, LicenseCheckOperationPriority priority,
                                  TimeSpan timeout, int maxRetries, TimeSpan retryDelay) : this()
        {
            OperationType = operationType;
            Server = server;
            Port = port;
            Feature = feature;
            User = user;
            Priority = priority;
            Timeout = timeout;
            MaxRetries = maxRetries;
            RetryDelay = retryDelay;
        }

        /// <summary>
        /// Creates a status check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Status check operation</returns>
        public static LicenseCheckOperation CreateStatusCheck(string server, int port)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.ServerStatusCheck, server, port)
            {
                Description = $"Status check for {server}:{port}",
                Timeout = TimeSpan.FromMinutes(1)
            };
        }

        /// <summary>
        /// Creates a feature check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">Feature name</param>
        /// <returns>Feature check operation</returns>
        public static LicenseCheckOperation CreateFeatureCheck(string server, int port, string feature)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.FeatureStatusCheck, server, port)
            {
                Feature = feature,
                Description = $"Feature check for {feature} on {server}:{port}",
                Timeout = TimeSpan.FromMinutes(2)
            };
        }

        /// <summary>
        /// Creates a user check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="user">User name</param>
        /// <returns>User check operation</returns>
        public static LicenseCheckOperation CreateUserCheck(string server, int port, string user)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.UserStatusCheck, server, port)
            {
                User = user,
                Description = $"User check for {user} on {server}:{port}",
                Timeout = TimeSpan.FromMinutes(2)
            };
        }

        /// <summary>
        /// Creates an idle license check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="idleThresholdMinutes">Idle threshold in minutes</param>
        /// <returns>Idle license check operation</returns>
        public static LicenseCheckOperation CreateIdleLicenseCheck(string server, int port, int idleThresholdMinutes = 30)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.IdleLicenseCheck, server, port)
            {
                Description = $"Idle license check for {server}:{port} (threshold: {idleThresholdMinutes}min)",
                Timeout = TimeSpan.FromMinutes(3),
                Parameters = { ["IdleThresholdMinutes"] = idleThresholdMinutes }
            };
        }

        /// <summary>
        /// Creates a health check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Health check operation</returns>
        public static LicenseCheckOperation CreateHealthCheck(string server, int port)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.ServerHealthCheck, server, port)
            {
                Description = $"Health check for {server}:{port}",
                Timeout = TimeSpan.FromMinutes(1),
                Priority = LicenseCheckOperationPriority.High
            };
        }

        /// <summary>
        /// Creates a comprehensive license check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Comprehensive license check operation</returns>
        public static LicenseCheckOperation CreateComprehensiveCheck(string server, int port)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.ComprehensiveCheck, server, port)
            {
                Description = $"Comprehensive license check for {server}:{port}",
                Timeout = TimeSpan.FromMinutes(5),
                Priority = LicenseCheckOperationPriority.Normal
            };
        }

        /// <summary>
        /// Creates a recurring status check operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="interval">Recurring interval</param>
        /// <param name="maxExecutions">Maximum number of executions (optional)</param>
        /// <returns>Recurring status check operation</returns>
        public static LicenseCheckOperation CreateRecurringStatusCheck(string server, int port, TimeSpan interval, int? maxExecutions = null)
        {
            return new LicenseCheckOperation(LicenseCheckOperationType.ServerStatusCheck, server, port)
            {
                Description = $"Recurring status check for {server}:{port}",
                Timeout = TimeSpan.FromMinutes(1),
                IsRecurring = true,
                RecurringInterval = interval,
                MaxRecurringExecutions = maxExecutions
            };
        }

        /// <summary>
        /// Adds a tag to the operation
        /// </summary>
        /// <param name="tag">Tag to add</param>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !Tags.Contains(tag))
            {
                Tags.Add(tag);
            }
        }

        /// <summary>
        /// Adds multiple tags to the operation
        /// </summary>
        /// <param name="tags">Tags to add</param>
        public void AddTags(IEnumerable<string> tags)
        {
            foreach (var tag in tags)
            {
                AddTag(tag);
            }
        }

        /// <summary>
        /// Removes a tag from the operation
        /// </summary>
        /// <param name="tag">Tag to remove</param>
        public void RemoveTag(string tag)
        {
            Tags.Remove(tag);
        }

        /// <summary>
        /// Checks if the operation has a specific tag
        /// </summary>
        /// <param name="tag">Tag to check</param>
        /// <returns>True if the operation has the tag</returns>
        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        /// <summary>
        /// Adds a parameter to the operation
        /// </summary>
        /// <param name="key">Parameter key</param>
        /// <param name="value">Parameter value</param>
        public void AddParameter(string key, object value)
        {
            Parameters[key] = value;
        }

        /// <summary>
        /// Gets a parameter value from the operation
        /// </summary>
        /// <param name="key">Parameter key</param>
        /// <param name="defaultValue">Default value if parameter not found</param>
        /// <returns>Parameter value or default</returns>
        public T GetParameter<T>(string key, T defaultValue = default)
        {
            if (Parameters.TryGetValue(key, out var value) && value is T result)
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Schedules the operation for execution at a specific time
        /// </summary>
        /// <param name="scheduledFor">When to execute the operation</param>
        public void ScheduleFor(DateTime scheduledFor)
        {
            ScheduledFor = scheduledFor;
        }

        /// <summary>
        /// Schedules the operation for execution after a delay
        /// </summary>
        /// <param name="delay">Delay before execution</param>
        public void ScheduleAfter(TimeSpan delay)
        {
            ScheduledFor = DateTime.Now + delay;
        }

        /// <summary>
        /// Marks the operation as executing
        /// </summary>
        public void MarkAsExecuting()
        {
            Status = LicenseCheckOperationStatus.Executing;
            ExecutedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the operation as completed successfully
        /// </summary>
        public void MarkAsCompleted()
        {
            Status = LicenseCheckOperationStatus.Completed;
            CompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the operation as failed
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public void MarkAsFailed(string errorMessage)
        {
            Status = LicenseCheckOperationStatus.Failed;
            CompletedAt = DateTime.Now;
            LastError = errorMessage;
        }

        /// <summary>
        /// Marks the operation as cancelled
        /// </summary>
        public void MarkAsCancelled()
        {
            Status = LicenseCheckOperationStatus.Cancelled;
            CompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Increments the retry attempt count
        /// </summary>
        public void IncrementRetryAttempt()
        {
            RetryAttempt++;
            Status = LicenseCheckOperationStatus.Pending;
            LastError = string.Empty;
        }

        /// <summary>
        /// Resets the operation for retry
        /// </summary>
        public void ResetForRetry()
        {
            Status = LicenseCheckOperationStatus.Pending;
            ExecutedAt = null;
            CompletedAt = null;
            LastError = string.Empty;
        }

        /// <summary>
        /// Creates a copy of the operation
        /// </summary>
        /// <returns>Copy of the operation</returns>
        public LicenseCheckOperation Clone()
        {
            return new LicenseCheckOperation
            {
                OperationId = Guid.NewGuid(),
                OperationType = OperationType,
                Server = Server,
                Port = Port,
                Feature = Feature,
                User = User,
                Priority = Priority,
                Timeout = Timeout,
                MaxRetries = MaxRetries,
                RetryDelay = RetryDelay,
                Parameters = new Dictionary<string, object>(Parameters),
                Description = Description,
                Tags = new List<string>(Tags),
                CreatedAt = DateTime.Now,
                ScheduledFor = ScheduledFor,
                Status = LicenseCheckOperationStatus.Pending,
                IsRecurring = IsRecurring,
                RecurringInterval = RecurringInterval,
                MaxRecurringExecutions = MaxRecurringExecutions,
                Dependencies = new List<Guid>(Dependencies),
                CacheResult = CacheResult,
                CacheDuration = CacheDuration,
                EnableDetailedLogging = EnableDetailedLogging
            };
        }

        /// <summary>
        /// Returns a string representation of the operation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckOperation[Id={OperationId:N}, Type={OperationType}, " +
                   $"Server={Server}:{Port}, Status={Status}, Priority={Priority}, " +
                   $"Created={CreatedAt:yyyy-MM-dd HH:mm:ss}, " +
                   $"Executed={ExecutedAt:yyyy-MM-dd HH:mm:ss}, " +
                   $"Completed={CompletedAt:yyyy-MM-dd HH:mm:ss}]";
        }
    }
}