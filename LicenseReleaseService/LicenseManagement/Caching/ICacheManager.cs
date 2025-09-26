using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.LicenseManagement.Caching
{
    /// <summary>
    /// Defines the contract for cache management operations
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// Gets or sets the cache options
        /// </summary>
        CacheOptions Options { get; set; }

        /// <summary>
        /// Gets the current cache statistics
        /// </summary>
        CacheStatistics Statistics { get; }

        /// <summary>
        /// Gets a value from the cache
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or default if not found</returns>
        T Get<T>(string key);

        /// <summary>
        /// Gets a value from the cache asynchronously
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached value or default if not found</returns>
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache with default expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// Sets a value in the cache with specified expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Expiration time</param>
        void Set<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Sets a value in the cache asynchronously with default expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache asynchronously with specified expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Expiration time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if the value was removed, false if it didn't exist</returns>
        bool Remove(string key);

        /// <summary>
        /// Removes a value from the cache asynchronously
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the value was removed, false if it didn't exist</returns>
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if the key exists, false otherwise</returns>
        bool Contains(string key);

        /// <summary>
        /// Checks if a key exists in the cache asynchronously
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the key exists, false otherwise</returns>
        Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value from the cache or creates it using the factory function if not found
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create the value if not found</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <returns>Cached or newly created value</returns>
        T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiration = null);

        /// <summary>
        /// Gets a value from the cache or creates it using the factory function if not found (async)
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create the value if not found</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached or newly created value</returns>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all license information for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Dictionary of all license information</returns>
        Dictionary<string, LicenseInfo> GetLicenseInfo(string server, int port);

        /// <summary>
        /// Gets all license information for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of all license information</returns>
        Task<Dictionary<string, LicenseInfo>> GetLicenseInfoAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets license information for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="licenseInfo">Dictionary of license information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        void SetLicenseInfo(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null);

        /// <summary>
        /// Sets license information for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="licenseInfo">Dictionary of license information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetLicenseInfoAsync(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets license feature information for a specific server and feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <returns>License feature information</returns>
        LicenseFeature GetLicenseFeature(string server, int port, string feature);

        /// <summary>
        /// Gets license feature information for a specific server and feature asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License feature information</returns>
        Task<LicenseFeature> GetLicenseFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets license feature information for a specific server and feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="licenseFeature">License feature information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        void SetLicenseFeature(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null);

        /// <summary>
        /// Sets license feature information for a specific server and feature asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="licenseFeature">License feature information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetLicenseFeatureAsync(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets license server status for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>License server status</returns>
        LicenseServerStatus GetServerStatus(string server, int port);

        /// <summary>
        /// Gets license server status for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status</returns>
        Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets license server status for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="serverStatus">License server status</param>
        /// <param name="expiration">Expiration time (optional)</param>
        void SetServerStatus(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null);

        /// <summary>
        /// Sets license server status for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="serverStatus">License server status</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetServerStatusAsync(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        void Clear();

        /// <summary>
        /// Clears all items from the cache asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes expired items from the cache
        /// </summary>
        /// <returns>Number of items removed</returns>
        int RemoveExpired();

        /// <summary>
        /// Removes expired items from the cache asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of items removed</returns>
        Task<int> RemoveExpiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a cache key for license information
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Cache key</returns>
        string GenerateLicenseInfoKey(string server, int port);

        /// <summary>
        /// Generates a cache key for license feature information
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <returns>Cache key</returns>
        string GenerateLicenseFeatureKey(string server, int port, string feature);

        /// <summary>
        /// Generates a cache key for server status information
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Cache key</returns>
        string GenerateServerStatusKey(string server, int port);

        /// <summary>
        /// Invalidates all cache entries for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        void InvalidateServer(string server, int port);

        /// <summary>
        /// Invalidates all cache entries for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InvalidateServerAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache entries for a specific feature on a server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        void InvalidateFeature(string server, int port, string feature);

        /// <summary>
        /// Invalidates cache entries for a specific feature on a server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InvalidateFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default);
    }
}