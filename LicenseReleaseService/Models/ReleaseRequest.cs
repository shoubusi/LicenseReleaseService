using System;
using System.Collections.Generic;

namespace LicenseReleaseService.Models
{
    /// <summary>
    /// Represents a request to release a license
    /// </summary>
    public class ReleaseRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for the request
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the license server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the license feature name
        /// </summary>
        public string Feature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name to release license from
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the host name where the license is being used
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the user
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request priority
        /// </summary>
        public ReleaseRequestPriority Priority { get; set; } = ReleaseRequestPriority.Normal;

        /// <summary>
        /// Gets or sets the release strategy to use
        /// </summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timeout for the release operation
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the request source (e.g., system, manual, scheduled)
        /// </summary>
        public string Source { get; set; } = "system";

        /// <summary>
        /// Gets or sets the request initiator
        /// </summary>
        public string Initiator { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the requested execution time
        /// </summary>
        public DateTime? ScheduledTime { get; set; }

        /// <summary>
        /// Gets or sets the release reason
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the additional metadata for the request
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the validation flags for the request
        /// </summary>
        public ReleaseValidationFlags ValidationFlags { get; set; } = ReleaseValidationFlags.Default;

        /// <summary>
        /// Gets or sets the force release flag (bypasses safety checks)
        /// </summary>
        public bool ForceRelease { get; set; } = false;

        /// <summary>
        /// Gets or sets the dry run flag (validates but doesn't execute)
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Gets or sets the callback information for async notifications
        /// </summary>
        public ReleaseCallbackInfo Callback { get; set; } = new ReleaseCallbackInfo();

        /// <summary>
        /// Gets a value indicating whether the request is valid
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Server) &&
                               Port > 0 &&
                               !string.IsNullOrWhiteSpace(Feature) &&
                               !string.IsNullOrWhiteSpace(User);

        /// <summary>
        /// Gets a value indicating whether the request is scheduled for future execution
        /// </summary>
        public bool IsScheduled => ScheduledTime.HasValue && ScheduledTime.Value > DateTime.Now;

        /// <summary>
        /// Returns a string representation of the release request
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"ReleaseRequest[Id={RequestId}, Server={Server}:{Port}, Feature={Feature}, User={User}, " +
                   $"Priority={Priority}, Source={Source}, Force={ForceRelease}, DryRun={DryRun}]";
        }

        /// <summary>
        /// Creates a basic release request
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <returns>Basic release request</returns>
        public static ReleaseRequest Create(string server, int port, string feature, string user)
        {
            return new ReleaseRequest
            {
                Server = server,
                Port = port,
                Feature = feature,
                User = user
            };
        }

        /// <summary>
        /// Creates a forced release request
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="reason">Release reason</param>
        /// <returns>Forced release request</returns>
        public static ReleaseRequest CreateForced(string server, int port, string feature, string user, string reason)
        {
            return new ReleaseRequest
            {
                Server = server,
                Port = port,
                Feature = feature,
                User = user,
                ForceRelease = true,
                Reason = reason
            };
        }

        /// <summary>
        /// Creates a scheduled release request
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="scheduledTime">Scheduled execution time</param>
        /// <returns>Scheduled release request</returns>
        public static ReleaseRequest CreateScheduled(string server, int port, string feature, string user, DateTime scheduledTime)
        {
            return new ReleaseRequest
            {
                Server = server,
                Port = port,
                Feature = feature,
                User = user,
                ScheduledTime = scheduledTime
            };
        }
    }

    /// <summary>
    /// Defines the priority levels for release requests
    /// </summary>
    public enum ReleaseRequestPriority
    {
        /// <summary>
        /// Low priority request
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority request
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority request
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority request
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Defines validation flags for release requests
    /// </summary>
    [Flags]
    public enum ReleaseValidationFlags
    {
        /// <summary>
        /// No validation
        /// </summary>
        None = 0,

        /// <summary>
        /// Validate server connectivity
        /// </summary>
        ValidateServer = 1,

        /// <summary>
        /// Validate feature existence
        /// </summary>
        ValidateFeature = 2,

        /// <summary>
        /// Validate user existence
        /// </summary>
        ValidateUser = 4,

        /// <summary>
        /// Validate safety constraints
        /// </summary>
        ValidateSafety = 8,

        /// <summary>
        /// Default validation flags
        /// </summary>
        Default = ValidateServer | ValidateFeature | ValidateUser | ValidateSafety
    }

    /// <summary>
    /// Represents callback information for release requests
    /// </summary>
    public class ReleaseCallbackInfo
    {
        /// <summary>
        /// Gets or sets the callback URL
        /// </summary>
        public string CallbackUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the callback method (GET, POST, etc.)
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// Gets or sets the callback headers
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the callback timeout
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets a value indicating whether to retry failed callbacks
        /// </summary>
        public bool RetryOnFailure { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of callback retry attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
    }
}