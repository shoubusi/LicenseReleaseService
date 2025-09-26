using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines the contract for the license query engine that provides comprehensive license querying capabilities
    /// </summary>
    public interface ILicenseQueryEngine
    {
        /// <summary>
        /// Gets or sets the query options for the license engine
        /// </summary>
        LicenseQueryOptions Options { get; set; }

        /// <summary>
        /// Gets the cache manager for license data caching
        /// </summary>
        ICacheManager CacheManager { get; }

        /// <summary>
        /// Gets the process executor for running lmstat commands
        /// </summary>
        IProcessExecutor ProcessExecutor { get; }

        /// <summary>
        /// Queries the license status for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status information</returns>
        Task<LicenseServerStatus> QueryLicenseStatusAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries all available license features for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of license features</returns>
        Task<Dictionary<string, LicenseFeature>> QueryFeaturesAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries all active users for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of active users by feature</returns>
        Task<Dictionary<string, List<LicenseInfo>>> QueryActiveUsersAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries detailed information for a specific license feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">Feature name to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed license feature information</returns>
        Task<LicenseFeature> QueryFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries license status with verbose output for additional details
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status with verbose information</returns>
        Task<LicenseServerStatus> QueryLicenseStatusVerboseAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries license information for specific users on a server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="userNames">List of user names to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of user license information</returns>
        Task<Dictionary<string, List<LicenseInfo>>> QueryUsersAsync(string server, int port, IEnumerable<string> userNames, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries borrowed licenses for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of borrowed license information</returns>
        Task<List<LicenseInfo>> QueryBorrowedLicensesAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries idle licenses for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of idle license information</returns>
        Task<List<LicenseInfo>> QueryIdleLicensesAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries license usage statistics for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License usage statistics</returns>
        Task<LicenseUsageStatistics> QueryUsageStatisticsAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries multiple servers simultaneously for license status
        /// </summary>
        /// <param name="servers">List of server addresses</param>
        /// <param name="ports">List of server ports (must match servers count)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of server statuses by server address</returns>
        Task<Dictionary<string, LicenseServerStatus>> QueryMultipleServersAsync(IEnumerable<string> servers, IEnumerable<int> ports, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a health check on a license server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        Task<LicenseServerHealth> CheckServerHealthAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cached data for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        Task InvalidateServerCacheAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cached data for a specific feature on a server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        Task InvalidateFeatureCacheAsync(string server, int port, string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached license data
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        Task ClearAllCacheAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets performance metrics for the query engine
        /// </summary>
        /// <returns>Performance metrics</returns>
        LicenseQueryMetrics GetPerformanceMetrics();

        /// <summary>
        /// Resets performance metrics
        /// </summary>
        void ResetPerformanceMetrics();

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        List<string> ValidateConfiguration();
    }

    /// <summary>
    /// Represents license usage statistics
    /// </summary>
    public class LicenseUsageStatistics
    {
        /// <summary>
        /// Gets or sets the total number of licenses
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
        /// Gets or sets the number of active users
        /// </summary>
        public int ActiveUsers { get; set; }

        /// <summary>
        /// Gets or sets the number of idle users
        /// </summary>
        public int IdleUsers { get; set; }

        /// <summary>
        /// Gets or sets the number of borrowed users
        /// </summary>
        public int BorrowedUsers { get; set; }

        /// <summary>
        /// Gets or sets the utilization percentage
        /// </summary>
        public double UtilizationPercentage { get; set; }

        /// <summary>
        /// Gets or sets the availability percentage
        /// </summary>
        public double AvailabilityPercentage { get; set; }

        /// <summary>
        /// Gets or sets the idle percentage
        /// </summary>
        public double IdlePercentage { get; set; }

        /// <summary>
        /// Gets or sets the statistics timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Returns a string representation of the license usage statistics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseUsageStatistics[Server={Server}:{Port}, Total={TotalLicenses}, " +
                   $"InUse={LicensesInUse}, Available={AvailableLicenses}, Users={ActiveUsers}, " +
                   $"Utilization={UtilizationPercentage:F1}%, Availability={AvailabilityPercentage:F1}%, " +
                   $"Idle={IdlePercentage:F1}%, Timestamp={Timestamp:yyyy-MM-dd HH:mm:ss}]";
        }
    }

    /// <summary>
    /// Represents license server health information
    /// </summary>
    public class LicenseServerHealth
    {
        /// <summary>
        /// Gets or sets the server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the health check result message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response time in milliseconds
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the health check timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the error message if the health check failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of the license server health
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseServerHealth[Server={Server}:{Port}, Healthy={IsHealthy}, " +
                   $"ResponseTime={ResponseTimeMs}ms, Message={Message}, " +
                   $"Timestamp={Timestamp:yyyy-MM-dd HH:mm:ss}]";
        }
    }

    /// <summary>
    /// Represents performance metrics for the license query engine
    /// </summary>
    public class LicenseQueryMetrics
    {
        /// <summary>
        /// Gets or sets the total number of queries executed
        /// </summary>
        public long TotalQueries { get; set; }

        /// <summary>
        /// Gets or sets the number of successful queries
        /// </summary>
        public long SuccessfulQueries { get; set; }

        /// <summary>
        /// Gets or sets the number of failed queries
        /// </summary>
        public long FailedQueries { get; set; }

        /// <summary>
        /// Gets or sets the number of cached queries
        /// </summary>
        public long CachedQueries { get; set; }

        /// <summary>
        /// Gets or sets the average query time in milliseconds
        /// </summary>
        public double AverageQueryTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum query time in milliseconds
        /// </summary>
        public double MinQueryTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum query time in milliseconds
        /// </summary>
        public double MaxQueryTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the cache hit ratio (0.0 to 1.0)
        /// </summary>
        public double CacheHitRatio { get; set; }

        /// <summary>
        /// Gets or sets the metrics start timestamp
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the metrics end timestamp
        /// </summary>
        public DateTime EndTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets the success rate (0.0 to 1.0)
        /// </summary>
        public double SuccessRate => TotalQueries > 0 ? (double)SuccessfulQueries / TotalQueries : 0;

        /// <summary>
        /// Gets the failure rate (0.0 to 1.0)
        /// </summary>
        public double FailureRate => TotalQueries > 0 ? (double)FailedQueries / TotalQueries : 0;

        /// <summary>
        /// Returns a string representation of the license query metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseQueryMetrics[Total={TotalQueries}, Success={SuccessfulQueries}, " +
                   $"Failed={FailedQueries}, Cached={CachedQueries}, SuccessRate={SuccessRate:P2}, " +
                   $"CacheHitRatio={CacheHitRatio:P2}, AvgTime={AverageQueryTimeMs:F2}ms, " +
                   $"MinTime={MinQueryTimeMs:F2}ms, MaxTime={MaxQueryTimeMs:F2}ms]";
        }
    }
}