using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents the result of a license feature check
    /// </summary>
    public class LicenseFeatureResult
    {
        /// <summary>
        /// Gets the feature code
        /// </summary>
        public string FeatureCode { get; }

        /// <summary>
        /// Gets the feature name
        /// </summary>
        public string FeatureName { get; }

        /// <summary>
        /// Gets the license status for this feature
        /// </summary>
        public LicenseStatus Status { get; }

        /// <summary>
        /// Gets the total number of licenses available for this feature
        /// </summary>
        public int TotalLicenses { get; }

        /// <summary>
        /// Gets the number of licenses currently in use
        /// </summary>
        public int LicensesInUse { get; }

        /// <summary>
        /// Gets the number of available licenses
        /// </summary>
        public int AvailableLicenses { get; }

        /// <summary>
        /// Gets the list of users currently using this feature
        /// </summary>
        public IReadOnlyList<LicenseUserInfo> LicensedUsers { get; }

        /// <summary>
        /// Gets the license server version
        /// </summary>
        public string ServerVersion { get; }

        /// <summary>
        /// Gets the vendor daemon information
        /// </summary>
        public string VendorDaemon { get; }

        /// <summary>
        /// Gets the license file path
        /// </summary>
        public string LicenseFilePath { get; }

        /// <summary>
        /// Gets the expiration date of the license
        /// </summary>
        public DateTime? ExpirationDate { get; }

        /// <summary>
        /// Gets a value indicating whether the license is expiring soon
        /// </summary>
        public bool IsExpiringSoon { get; }

        /// <summary>
        /// Gets the days until expiration
        /// </summary>
        public int? DaysUntilExpiration { get; }

        /// <summary>
        /// Gets the feature-specific metrics
        /// </summary>
        public IReadOnlyDictionary<string, object> FeatureMetrics { get; }

        /// <summary>
        /// Gets the timestamp when this result was generated
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets a value indicating whether this feature is available
        /// </summary>
        public bool IsAvailable => AvailableLicenses > 0;

        /// <summary>
        /// Gets a value indicating whether this feature is at capacity
        /// </summary>
        public bool IsAtCapacity => AvailableLicenses == 0 && LicensesInUse > 0;

        /// <summary>
        /// Gets the usage percentage
        /// </summary>
        public double UsagePercentage
        {
            get
            {
                if (TotalLicenses == 0)
                    return 0;

                return (double)LicensesInUse / TotalLicenses * 100;
            }
        }

        /// <summary>
        /// Initializes a new instance of the LicenseFeatureResult class
        /// </summary>
        /// <param name="featureCode">The feature code</param>
        /// <param name="featureName">The feature name</param>
        /// <param name="status">The license status</param>
        /// <param name="totalLicenses">The total number of licenses</param>
        /// <param name="licensesInUse">The number of licenses in use</param>
        /// <param name="availableLicenses">The number of available licenses</param>
        /// <param name="licensedUsers">The list of licensed users</param>
        public LicenseFeatureResult(
            string featureCode,
            string featureName,
            LicenseStatus status,
            int totalLicenses,
            int licensesInUse,
            int availableLicenses,
            IEnumerable<LicenseUserInfo> licensedUsers = null)
            : this(featureCode, featureName, status, totalLicenses, licensesInUse, availableLicenses, licensedUsers, null, null, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseFeatureResult class with extended information
        /// </summary>
        /// <param name="featureCode">The feature code</param>
        /// <param name="featureName">The feature name</param>
        /// <param name="status">The license status</param>
        /// <param name="totalLicenses">The total number of licenses</param>
        /// <param name="licensesInUse">The number of licenses in use</param>
        /// <param name="availableLicenses">The number of available licenses</param>
        /// <param name="licensedUsers">The list of licensed users</param>
        /// <param name="serverVersion">The server version</param>
        /// <param name="vendorDaemon">The vendor daemon</param>
        /// <param name="licenseFilePath">The license file path</param>
        /// <param name="expirationDate">The expiration date</param>
        public LicenseFeatureResult(
            string featureCode,
            string featureName,
            LicenseStatus status,
            int totalLicenses,
            int licensesInUse,
            int availableLicenses,
            IEnumerable<LicenseUserInfo> licensedUsers = null,
            string serverVersion = null,
            string vendorDaemon = null,
            string licenseFilePath = null,
            DateTime? expirationDate = null)
        {
            FeatureCode = featureCode ?? throw new ArgumentNullException(nameof(featureCode));
            FeatureName = featureName ?? throw new ArgumentNullException(nameof(featureName));
            Status = status;
            TotalLicenses = totalLicenses;
            LicensesInUse = licensesInUse;
            AvailableLicenses = availableLicenses;
            LicensedUsers = licensedUsers != null ? new List<LicenseUserInfo>(licensedUsers).AsReadOnly() : new List<LicenseUserInfo>().AsReadOnly();
            ServerVersion = serverVersion;
            VendorDaemon = vendorDaemon;
            LicenseFilePath = licenseFilePath;
            ExpirationDate = expirationDate;
            Timestamp = DateTime.UtcNow;
            FeatureMetrics = new Dictionary<string, object>().AsReadOnly();

            // Calculate expiration information
            if (expirationDate.HasValue)
            {
                DaysUntilExpiration = (int)(expirationDate.Value - DateTime.UtcNow).TotalDays;
                IsExpiringSoon = DaysUntilExpiration <= 30; // 30 days threshold
            }
            else
            {
                DaysUntilExpiration = null;
                IsExpiringSoon = false;
            }
        }

        /// <summary>
        /// Returns a string representation of the license feature result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseFeatureResult [Feature={FeatureCode}, Status={Status}, Available={AvailableLicenses}/{TotalLicenses}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the feature result
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            var expirationText = ExpirationDate.HasValue ?
                $"\n  Expires: {ExpirationDate.Value:yyyy-MM-dd} ({DaysUntilExpiration} days)" :
                "";
            var serverText = !string.IsNullOrEmpty(ServerVersion) ?
                $"\n  Server: {ServerVersion}" :
                "";

            return $@"LicenseFeatureResult Details:
  Feature Code: {FeatureCode}
  Feature Name: {FeatureName}
  Status: {Status}
  Total Licenses: {TotalLicenses}
  Licenses In Use: {LicensesInUse}
  Available Licenses: {AvailableLicenses}
  Usage Percentage: {UsagePercentage:F1}%
  Licensed Users: {LicensedUsers.Count}
  Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss UTC}{serverText}{expirationText}
  Is Expiring Soon: {IsExpiringSoon}";
        }
    }

    /// <summary>
    /// License status enumeration
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>
        /// License is available and operational
        /// </summary>
        Available,

        /// <summary>
        /// License is in use
        /// </summary>
        InUse,

        /// <summary>
        /// License is expired
        /// </summary>
        Expired,

        /// <summary>
        /// License is invalid or corrupted
        /// </summary>
        Invalid,

        /// <summary>
        /// License server is unreachable
        /// </summary>
        ServerUnreachable,

        /// <summary>
        /// License feature not found
        /// </summary>
        NotFound,

        /// <summary>
        /// License is borrowed
        /// </summary>
        Borrowed,

        /// <summary>
        /// License is reserved
        /// </summary>
        Reserved,

        /// <summary>
        /// License is in maintenance mode
        /// </summary>
        Maintenance,

        /// <summary>
        /// License status is unknown
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Represents information about a licensed user
    /// </summary>
    public class LicenseUserInfo
    {
        /// <summary>
        /// Gets the username
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; }

        /// <summary>
        /// Gets the display name
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the user's department
        /// </summary>
        public string Department { get; }

        /// <summary>
        /// Gets the checkout time
        /// </summary>
        public DateTime? CheckoutTime { get; }

        /// <summary>
        /// Gets the checkout duration
        /// </summary>
        public TimeSpan? CheckoutDuration { get; }

        /// <summary>
        /// Gets the user's IP address
        /// </summary>
        public string IPAddress { get; }

        /// <summary>
        /// Gets the user's host name
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// Gets the process ID using the license
        /// </summary>
        public int? ProcessId { get; }

        /// <summary>
        /// Gets the user's session ID
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets a value indicating whether the user is currently active
        /// </summary>
        public bool IsActive { get; }

        /// <summary>
        /// Gets additional user information
        /// </summary>
        public IReadOnlyDictionary<string, object> AdditionalInfo { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseUserInfo class
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="computerName">The computer name</param>
        /// <param name="displayName">The display name</param>
        /// <param name="checkoutTime">The checkout time</param>
        /// <param name="processId">The process ID</param>
        public LicenseUserInfo(
            string username,
            string computerName,
            string displayName = null,
            DateTime? checkoutTime = null,
            int? processId = null)
            : this(username, computerName, displayName, null, checkoutTime, null, null, null, processId, true, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseUserInfo class with extended information
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="computerName">The computer name</param>
        /// <param name="displayName">The display name</param>
        /// <param name="department">The department</param>
        /// <param name="checkoutTime">The checkout time</param>
        /// <param name="iPAddress">The IP address</param>
        /// <param name="hostName">The host name</param>
        /// <param name="sessionId">The session ID</param>
        /// <param name="processId">The process ID</param>
        /// <param name="isActive">Whether the user is active</param>
        /// <param name="additionalInfo">Additional user information</param>
        public LicenseUserInfo(
            string username,
            string computerName,
            string displayName = null,
            string department = null,
            DateTime? checkoutTime = null,
            string iPAddress = null,
            string hostName = null,
            string sessionId = null,
            int? processId = null,
            bool isActive = true,
            IDictionary<string, object> additionalInfo = null)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            ComputerName = computerName ?? throw new ArgumentNullException(nameof(computerName));
            DisplayName = displayName ?? username;
            Department = department;
            CheckoutTime = checkoutTime ?? DateTime.UtcNow;
            CheckoutDuration = checkoutTime.HasValue ? DateTime.UtcNow - checkoutTime.Value : (TimeSpan?)null;
            IPAddress = iPAddress;
            HostName = hostName;
            SessionId = sessionId;
            ProcessId = processId;
            IsActive = isActive;
            AdditionalInfo = additionalInfo != null ? new Dictionary<string, object>(additionalInfo).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
        }

        /// <summary>
        /// Returns a string representation of the license user info
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseUserInfo [User={Username}, Computer={ComputerName}, Active={IsActive}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the user info
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            var checkoutText = CheckoutTime.HasValue ?
                $"\n  Checkout Time: {CheckoutTime.Value:yyyy-MM-dd HH:mm:ss UTC}" :
                "";
            var durationText = CheckoutDuration.HasValue ?
                $"\n  Checkout Duration: {CheckoutDuration.Value.TotalHours:F1} hours" :
                "";
            var processText = ProcessId.HasValue ?
                $"\n  Process ID: {ProcessId.Value}" :
                "";

            return $@"LicenseUserInfo Details:
  Username: {Username}
  Display Name: {DisplayName}
  Computer Name: {ComputerName}
  Department: {Department}
  IP Address: {IPAddress}
  Host Name: {HostName}
  Session ID: {SessionId}
  Is Active: {IsActive}{checkoutText}{durationText}{processText}";
        }
    }
}