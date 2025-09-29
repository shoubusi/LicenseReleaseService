using System;
using System.Collections.Generic;
using System.Linq;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents the status of a license server
    /// </summary>
    public class LicenseServerStatus
    {
        /// <summary>
        /// Gets or sets the license server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server name
        /// </summary>
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server is up and running
        /// </summary>
        public bool IsServerUp { get; set; }

        /// <summary>
        /// Gets or sets the last time the server status was checked
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the server response time in milliseconds
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the license server version
        /// </summary>
        public string ServerVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server vendor
        /// </summary>
        public string Vendor { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the dictionary of license features and their status
        /// </summary>
        public Dictionary<string, LicenseFeatureStatus> Features { get; set; } = new Dictionary<string, LicenseFeatureStatus>();

        /// <summary>
        /// Gets or sets the dictionary of license features using the new model
        /// </summary>
        public Dictionary<string, LicenseFeature> FeatureDetails { get; set; } = new Dictionary<string, LicenseFeature>();

        /// <summary>
        /// Gets or sets the list of server messages
        /// </summary>
        public List<string> ServerMessages { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the total number of available licenses
        /// </summary>
        public int TotalLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses in use
        /// </summary>
        public int LicensesInUse { get; set; }

        /// <summary>
        /// Gets or sets the number of available licenses
        /// </summary>
        public int AvailableLicenses { get; set; }

        /// <summary>
        /// Gets or sets the license server status message
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message if the status check failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the server is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the number of connected users
        /// </summary>
        public int ConnectedUsers { get; set; }

        /// <summary>
        /// Gets or sets the server uptime in seconds
        /// </summary>
        public long UptimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the server start time
        /// </summary>
        public DateTime? ServerStartTime { get; set; }

        /// <summary>
        /// Gets or sets the license server daemon status
        /// </summary>
        public string DaemonStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server platform information
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server architecture
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the server is available for operations
        /// </summary>
        public bool IsAvailable => IsServerUp && IsHealthy && string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets the license utilization percentage
        /// </summary>
        public double UtilizationPercentage => TotalLicenses > 0 ? (LicensesInUse * 100.0 / TotalLicenses) : 0;

        /// <summary>
        /// Gets the availability percentage
        /// </summary>
        public double AvailabilityPercentage => TotalLicenses > 0 ? (AvailableLicenses * 100.0 / TotalLicenses) : 0;

        /// <summary>
        /// Returns a string representation of the license server status
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseServerStatus[Server={Server}:{Port}, Up={IsServerUp}, Healthy={IsHealthy}, " +
                   $"Features={Features.Count}, Total={TotalLicenses}, InUse={LicensesInUse}, " +
                   $"Available={AvailableLicenses}, Utilization={UtilizationPercentage:F1}%]";
        }

        /// <summary>
        /// Creates a successful license server status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="responseTimeMs">Response time in milliseconds</param>
        /// <returns>Successful status</returns>
        public static LicenseServerStatus CreateSuccess(string server, int port, long responseTimeMs)
        {
            return new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                LastChecked = DateTime.Now,
                ResponseTimeMs = responseTimeMs,
                StatusMessage = "License server is running normally"
            };
        }

        /// <summary>
        /// Creates a failed license server status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Failed status</returns>
        public static LicenseServerStatus CreateFailure(string server, int port, string errorMessage)
        {
            return new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = false,
                IsHealthy = false,
                LastChecked = DateTime.Now,
                ErrorMessage = errorMessage,
                StatusMessage = "License server is not responding"
            };
        }

        /// <summary>
        /// Adds or updates a feature status
        /// </summary>
        /// <param name="featureName">Feature name</param>
        /// <param name="featureStatus">Feature status</param>
        public void AddOrUpdateFeature(string featureName, LicenseFeatureStatus featureStatus)
        {
            Features[featureName] = featureStatus;
            UpdateAggregateCounts();
        }

        /// <summary>
        /// Removes a feature status
        /// </summary>
        /// <param name="featureName">Feature name</param>
        /// <returns>True if the feature was removed</returns>
        public bool RemoveFeature(string featureName)
        {
            var removed = Features.Remove(featureName);
            if (removed)
            {
                UpdateAggregateCounts();
            }
            return removed;
        }

        /// <summary>
        /// Updates aggregate counts from feature statuses
        /// </summary>
        private void UpdateAggregateCounts()
        {
            TotalLicenses = 0;
            LicensesInUse = 0;
            AvailableLicenses = 0;

            foreach (var feature in Features.Values)
            {
                TotalLicenses += feature.TotalLicenses;
                LicensesInUse += feature.LicensesInUse;
                AvailableLicenses += feature.AvailableLicenses;
            }
        }

        /// <summary>
        /// Adds or updates a feature detail using the new model
        /// </summary>
        /// <param name="featureName">Feature name</param>
        /// <param name="featureDetail">Feature detail</param>
        public void AddOrUpdateFeatureDetail(string featureName, LicenseFeature featureDetail)
        {
            FeatureDetails[featureName] = featureDetail;
            UpdateAggregateCountsFromDetails();
        }

        /// <summary>
        /// Removes a feature detail
        /// </summary>
        /// <param name="featureName">Feature name</param>
        /// <returns>True if the feature was removed</returns>
        public bool RemoveFeatureDetail(string featureName)
        {
            var removed = FeatureDetails.Remove(featureName);
            if (removed)
            {
                UpdateAggregateCountsFromDetails();
            }
            return removed;
        }

        /// <summary>
        /// Updates aggregate counts from feature details
        /// </summary>
        private void UpdateAggregateCountsFromDetails()
        {
            TotalLicenses = 0;
            LicensesInUse = 0;
            AvailableLicenses = 0;

            foreach (var feature in FeatureDetails.Values)
            {
                TotalLicenses += feature.TotalLicenses;
                LicensesInUse += feature.UsedLicenses;
                AvailableLicenses += feature.AvailableLicenses;
            }
        }

        /// <summary>
        /// Adds a server message
        /// </summary>
        /// <param name="message">Message to add</param>
        public void AddServerMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                ServerMessages.Add(message);
            }
        }

        /// <summary>
        /// Clears all server messages
        /// </summary>
        public void ClearServerMessages()
        {
            ServerMessages.Clear();
        }

        /// <summary>
        /// Gets the total number of users across all features
        /// </summary>
        public int TotalUsers => FeatureDetails.Values.Sum(f => f.TotalUsers);

        /// <summary>
        /// Gets the total number of active users across all features
        /// </summary>
        public int TotalActiveUsers => FeatureDetails.Values.Sum(f => f.ActiveUsers);

        /// <summary>
        /// Gets the total number of idle users across all features
        /// </summary>
        public int TotalIdleUsers => FeatureDetails.Values.Sum(f => f.IdleUsers);

        /// <summary>
        /// Gets the total number of borrowed users across all features
        /// </summary>
        public int TotalBorrowedUsers => FeatureDetails.Values.Sum(f => f.BorrowedUsers);
    }

    /// <summary>
    /// Represents the status of a specific license feature
    /// </summary>
    public class LicenseFeatureStatus
    {
        /// <summary>
        /// Gets or sets the feature name
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the feature version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of licenses for this feature
        /// </summary>
        public int TotalLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses in use for this feature
        /// </summary>
        public int LicensesInUse { get; set; }

        /// <summary>
        /// Gets or sets the number of available licenses for this feature
        /// </summary>
        public int AvailableLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of reserved licenses for this feature
        /// </summary>
        public int ReservedLicenses { get; set; }

        /// <summary>
        /// Gets or sets the license expiration date
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the feature status (e.g., "ACTIVE", "EXPIRED", "DISABLED")
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the feature description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the vendor of this feature
        /// </summary>
        public string Vendor { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the dictionary of users currently using this feature
        /// </summary>
        public Dictionary<string, LicenseUserUsage> Users { get; set; } = new Dictionary<string, LicenseUserUsage>();

        /// <summary>
        /// Gets a value indicating whether the feature is available
        /// </summary>
        public bool IsAvailable => Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) &&
                                 AvailableLicenses > 0 &&
                                 (!ExpirationDate.HasValue || ExpirationDate.Value > DateTime.Now);

        /// <summary>
        /// Gets the utilization percentage for this feature
        /// </summary>
        public double UtilizationPercentage => TotalLicenses > 0 ? (LicensesInUse * 100.0 / TotalLicenses) : 0;

        /// <summary>
        /// Returns a string representation of the license feature status
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseFeatureStatus[Feature={FeatureName}, Version={Version}, " +
                   $"Total={TotalLicenses}, InUse={LicensesInUse}, Available={AvailableLicenses}, " +
                   $"Status={Status}, Utilization={UtilizationPercentage:F1}%]";
        }
    }

    /// <summary>
    /// Represents license usage information for a specific user
    /// </summary>
    public class LicenseUserUsage
    {
        /// <summary>
        /// Gets or sets the user name
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's host name
        /// </summary>
        public string HostName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the checkout time
        /// </summary>
        public DateTime CheckoutTime { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses checked out by this user
        /// </summary>
        public int LicensesCheckedOut { get; set; }

        /// <summary>
        /// Gets or sets the user's session ID
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's IP address
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process ID using the license
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the process name using the license
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration the license has been checked out
        /// </summary>
        public TimeSpan CheckoutDuration => DateTime.Now - CheckoutTime;

        /// <summary>
        /// Returns a string representation of the license user usage
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseUserUsage[User={UserName}, Host={HostName}, " +
                   $"Licenses={LicensesCheckedOut}, Duration={CheckoutDuration.TotalMinutes:F1}min, " +
                   $"Process={ProcessName}({ProcessId})]";
        }
    }
}