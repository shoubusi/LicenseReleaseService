using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines the contract for license management operations
    /// </summary>
    public interface ILicenseManager
    {
        /// <summary>
        /// Gets the status of a license server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status information</returns>
        Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases a license from a specific user
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name to release license from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License release result</returns>
        Task<LicenseReleaseResult> ReleaseLicenseAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about a specific license feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License feature information</returns>
        Task<LicenseFeatureInfo> GetFeatureInfoAsync(string server, int port, string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available features from the license server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of feature names and their information</returns>
        Task<Dictionary<string, LicenseFeatureInfo>> GetAllFeaturesAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all users currently using licenses
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of user names and their license usage</returns>
        Task<Dictionary<string, LicenseUserUsage>> GetUsersAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a license server is available and responsive
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the server is available</returns>
        Task<bool> IsServerAvailableAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets license usage statistics
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License usage statistics</returns>
        Task<LicenseUsageStatistics> GetUsageStatisticsAsync(string server, int port, CancellationToken cancellationToken = default);
    }
}