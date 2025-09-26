using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Core license query engine that provides comprehensive license querying capabilities
    /// </summary>
    public class LicenseQueryEngine : ILicenseQueryEngine
    {
        private readonly object _metricsLock = new object();
        private readonly ICacheManager _cacheManager;
        private readonly IProcessExecutor _processExecutor;
        private readonly LmstatOutputParser _outputParser;
        private LicenseQueryMetrics _metrics;

        /// <summary>
        /// Initializes a new instance of the LicenseQueryEngine class
        /// </summary>
        /// <param name="cacheManager">Cache manager for license data</param>
        /// <param name="processExecutor">Process executor for running lmstat commands</param>
        /// <param name="outputParser">Parser for lmstat output</param>
        public LicenseQueryEngine(ICacheManager cacheManager, IProcessExecutor processExecutor, LmstatOutputParser outputParser)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _outputParser = outputParser ?? throw new ArgumentNullException(nameof(outputParser));

            Options = LicenseQueryOptions.DefaultSolidWorksOptions();
            _metrics = new LicenseQueryMetrics();
        }

        /// <summary>
        /// Gets or sets the query options for the license engine
        /// </summary>
        public LicenseQueryOptions Options { get; set; }

        /// <summary>
        /// Gets the cache manager for license data caching
        /// </summary>
        public ICacheManager CacheManager => _cacheManager;

        /// <summary>
        /// Gets the process executor for running lmstat commands
        /// </summary>
        public IProcessExecutor ProcessExecutor => _processExecutor;

        /// <summary>
        /// Queries the license status for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status information</returns>
        public async Task<LicenseServerStatus> QueryLicenseStatusAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryLicenseStatusAsync",
                server,
                port,
                async () =>
                {
                    var cacheKey = _cacheManager.GenerateServerStatusKey(server, port);

                    if (Options.EnableCaching)
                    {
                        var cachedStatus = await _cacheManager.GetServerStatusAsync(server, port, cancellationToken);
                        if (cachedStatus != null && !IsCacheExpired(cachedStatus.LastChecked))
                        {
                            Interlocked.Increment(ref _metrics.CachedQueries);
                            return cachedStatus;
                        }
                    }

                    var result = await ExecuteLmstatQueryAsync(server, port, cancellationToken);
                    var status = _outputParser.ParseLmstatOutput(result.Output, $"{server}@{port}");

                    status.ResponseTimeMs = (long)result.ExecutionTime.TotalMilliseconds;
                    status.IsHealthy = status.IsServerUp && string.IsNullOrEmpty(status.ErrorMessage);

                    if (Options.EnableCaching)
                    {
                        await _cacheManager.SetServerStatusAsync(server, port, status, Options.CacheExpiration, cancellationToken);
                    }

                    return status;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries all available license features for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of license features</returns>
        public async Task<Dictionary<string, LicenseFeature>> QueryFeaturesAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryFeaturesAsync",
                server,
                port,
                async () =>
                {
                    var status = await QueryLicenseStatusAsync(server, port, cancellationToken);
                    return status.FeatureDetails;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries all active users for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of active users by feature</returns>
        public async Task<Dictionary<string, List<LicenseInfo>>> QueryActiveUsersAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryActiveUsersAsync",
                server,
                port,
                async () =>
                {
                    var features = await QueryFeaturesAsync(server, port, cancellationToken);
                    var activeUsers = new Dictionary<string, List<LicenseInfo>>();

                    foreach (var feature in features.Values)
                    {
                        var users = feature.GetAllUsers()
                            .Where(u => u.Status == Models.LicenseStatus.Active)
                            .ToList();

                        if (users.Count > 0)
                        {
                            activeUsers[feature.Name] = users;
                        }
                    }

                    return activeUsers;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries detailed information for a specific license feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">Feature name to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed license feature information</returns>
        public async Task<LicenseFeature> QueryFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryFeatureAsync",
                server,
                port,
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(feature))
                    {
                        throw new ArgumentException("Feature name cannot be null or whitespace", nameof(feature));
                    }

                    if (Options.EnableCaching)
                    {
                        var cachedFeature = await _cacheManager.GetLicenseFeatureAsync(server, port, feature, cancellationToken);
                        if (cachedFeature != null && !IsCacheExpired(cachedFeature.LastUpdated))
                        {
                            Interlocked.Increment(ref _metrics.CachedQueries);
                            return cachedFeature;
                        }
                    }

                    var arguments = $"-f {feature} -c {server}@{port}";
                    var result = await ExecuteLmstatQueryAsync(server, port, arguments, cancellationToken);
                    var licenseFeature = _outputParser.ParseFeatureOutput(result.Output, $"{server}@{port}", feature);

                    if (Options.EnableCaching)
                    {
                        await _cacheManager.SetLicenseFeatureAsync(server, port, feature, licenseFeature, Options.CacheExpiration, cancellationToken);
                    }

                    return licenseFeature;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries license status with verbose output for additional details
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status with verbose information</returns>
        public async Task<LicenseServerStatus> QueryLicenseStatusVerboseAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryLicenseStatusVerboseAsync",
                server,
                port,
                async () =>
                {
                    var arguments = $"-v -c {server}@{port}";
                    var result = await ExecuteLmstatQueryAsync(server, port, arguments, cancellationToken);
                    var status = _outputParser.ParseLmstatVerboseOutput(result.Output, $"{server}@{port}");

                    status.ResponseTimeMs = (long)result.ExecutionTime.TotalMilliseconds;
                    status.IsHealthy = status.IsServerUp && string.IsNullOrEmpty(status.ErrorMessage);

                    if (Options.EnableCaching)
                    {
                        await _cacheManager.SetServerStatusAsync(server, port, status, Options.CacheExpiration, cancellationToken);
                    }

                    return status;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries license information for specific users on a server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="userNames">List of user names to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of user license information</returns>
        public async Task<Dictionary<string, List<LicenseInfo>>> QueryUsersAsync(string server, int port, IEnumerable<string> userNames, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryUsersAsync",
                server,
                port,
                async () =>
                {
                    var features = await QueryFeaturesAsync(server, port, cancellationToken);
                    var users = new Dictionary<string, List<LicenseInfo>>();
                    var userNameSet = new HashSet<string>(userNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

                    foreach (var feature in features.Values)
                    {
                        foreach (var user in feature.GetAllUsers())
                        {
                            if (userNameSet.Contains(user.UserHost))
                            {
                                if (!users.TryGetValue(user.UserHost, out var userLicenses))
                                {
                                    userLicenses = new List<LicenseInfo>();
                                    users[user.UserHost] = userLicenses;
                                }
                                userLicenses.Add(user);
                            }
                        }
                    }

                    return users;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries borrowed licenses for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of borrowed license information</returns>
        public async Task<List<LicenseInfo>> QueryBorrowedLicensesAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryBorrowedLicensesAsync",
                server,
                port,
                async () =>
                {
                    var features = await QueryFeaturesAsync(server, port, cancellationToken);
                    var borrowedLicenses = new List<LicenseInfo>();

                    foreach (var feature in features.Values)
                    {
                        borrowedLicenses.AddRange(feature.GetAllUsers().Where(u => u.Status == Models.LicenseStatus.Borrowed));
                    }

                    return borrowedLicenses;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries idle licenses for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of idle license information</returns>
        public async Task<List<LicenseInfo>> QueryIdleLicensesAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryIdleLicensesAsync",
                server,
                port,
                async () =>
                {
                    var features = await QueryFeaturesAsync(server, port, cancellationToken);
                    var idleLicenses = new List<LicenseInfo>();

                    foreach (var feature in features.Values)
                    {
                        idleLicenses.AddRange(feature.GetAllUsers().Where(u => u.IsIdle));
                    }

                    return idleLicenses;
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries license usage statistics for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License usage statistics</returns>
        public async Task<LicenseUsageStatistics> QueryUsageStatisticsAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryUsageStatisticsAsync",
                server,
                port,
                async () =>
                {
                    var status = await QueryLicenseStatusAsync(server, port, cancellationToken);

                    return new LicenseUsageStatistics
                    {
                        Server = server,
                        Port = port,
                        TotalLicenses = status.TotalLicenses,
                        LicensesInUse = status.LicensesInUse,
                        AvailableLicenses = status.AvailableLicenses,
                        ActiveUsers = status.TotalActiveUsers,
                        IdleUsers = status.TotalIdleUsers,
                        BorrowedUsers = status.TotalBorrowedUsers,
                        UtilizationPercentage = status.UtilizationPercentage,
                        AvailabilityPercentage = status.AvailabilityPercentage,
                        IdlePercentage = status.TotalUsers > 0 ? (status.TotalIdleUsers * 100.0 / status.TotalUsers) : 0,
                        Timestamp = DateTime.Now
                    };
                },
                cancellationToken);
        }

        /// <summary>
        /// Queries multiple servers simultaneously for license status
        /// </summary>
        /// <param name="servers">List of server addresses</param>
        /// <param name="ports">List of server ports (must match servers count)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of server statuses by server address</returns>
        public async Task<Dictionary<string, LicenseServerStatus>> QueryMultipleServersAsync(IEnumerable<string> servers, IEnumerable<int> ports, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "QueryMultipleServersAsync",
                "",
                0,
                async () =>
                {
                    var serverList = servers?.ToList() ?? new List<string>();
                    var portList = ports?.ToList() ?? new List<int>();

                    if (serverList.Count != portList.Count)
                    {
                        throw new ArgumentException("Number of servers must match number of ports", nameof(ports));
                    }

                    var tasks = new List<Task<LicenseServerStatus>>();
                    var results = new Dictionary<string, LicenseServerStatus>();

                    for (int i = 0; i < serverList.Count; i++)
                    {
                        var server = serverList[i];
                        var port = portList[i];

                        tasks.Add(QueryLicenseStatusAsync(server, port, cancellationToken));
                    }

                    var statuses = await Task.WhenAll(tasks);

                    for (int i = 0; i < serverList.Count; i++)
                    {
                        var serverKey = $"{serverList[i]}:{portList[i]}";
                        results[serverKey] = statuses[i];
                    }

                    return results;
                },
                cancellationToken);
        }

        /// <summary>
        /// Performs a health check on a license server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<LicenseServerHealth> CheckServerHealthAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryWithMetricsAsync(
                "CheckServerHealthAsync",
                server,
                port,
                async () =>
                {
                    var stopwatch = Stopwatch.StartNew();

                    try
                    {
                        var status = await QueryLicenseStatusAsync(server, port, cancellationToken);
                        stopwatch.Stop();

                        return new LicenseServerHealth
                        {
                            Server = server,
                            Port = port,
                            IsHealthy = status.IsAvailable,
                            Message = status.StatusMessage,
                            ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                            Timestamp = DateTime.Now,
                            ErrorMessage = status.ErrorMessage
                        };
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();

                        return new LicenseServerHealth
                        {
                            Server = server,
                            Port = port,
                            IsHealthy = false,
                            Message = "Health check failed",
                            ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                            Timestamp = DateTime.Now,
                            ErrorMessage = ex.Message
                        };
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Invalidates cached data for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task InvalidateServerCacheAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            await _cacheManager.InvalidateServerAsync(server, port, cancellationToken);
        }

        /// <summary>
        /// Invalidates cached data for a specific feature on a server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task InvalidateFeatureCacheAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            await _cacheManager.InvalidateFeatureAsync(server, port, feature, cancellationToken);
        }

        /// <summary>
        /// Clears all cached license data
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task ClearAllCacheAsync(CancellationToken cancellationToken = default)
        {
            await _cacheManager.ClearAsync(cancellationToken);
        }

        /// <summary>
        /// Gets performance metrics for the query engine
        /// </summary>
        /// <returns>Performance metrics</returns>
        public LicenseQueryMetrics GetPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                var metrics = new LicenseQueryMetrics
                {
                    TotalQueries = _metrics.TotalQueries,
                    SuccessfulQueries = _metrics.SuccessfulQueries,
                    FailedQueries = _metrics.FailedQueries,
                    CachedQueries = _metrics.CachedQueries,
                    AverageQueryTimeMs = _metrics.AverageQueryTimeMs,
                    MinQueryTimeMs = _metrics.MinQueryTimeMs,
                    MaxQueryTimeMs = _metrics.MaxQueryTimeMs,
                    CacheHitRatio = _metrics.CacheHitRatio,
                    StartTime = _metrics.StartTime,
                    EndTime = DateTime.Now
                };

                return metrics;
            }
        }

        /// <summary>
        /// Resets performance metrics
        /// </summary>
        public void ResetPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                _metrics = new LicenseQueryMetrics
                {
                    StartTime = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();

            if (_cacheManager == null)
            {
                errors.Add("Cache manager is not configured");
            }

            if (_processExecutor == null)
            {
                errors.Add("Process executor is not configured");
            }

            if (_outputParser == null)
            {
                errors.Add("Output parser is not configured");
            }

            if (Options == null)
            {
                errors.Add("Query options are not configured");
            }
            else
            {
                var optionErrors = Options.Validate();
                errors.AddRange(optionErrors);
            }

            return errors;
        }

        /// <summary>
        /// Executes a query with retry logic and metrics collection
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="queryType">Type of query being executed</param>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="queryFunction">Query function to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Query result</returns>
        private async Task<T> ExecuteQueryWithMetricsAsync<T>(
            string queryType,
            string server,
            int port,
            Func<Task<T>> queryFunction,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var attempt = 0;
            Exception lastException = null;

            while (attempt <= Options.MaxRetries)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await queryFunction();

                    stopwatch.Stop();
                    UpdateMetrics(true, stopwatch.ElapsedMilliseconds);

                    return result;
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    UpdateMetrics(false, stopwatch.ElapsedMilliseconds);
                    throw new LicenseQueryException(
                        "Query was cancelled",
                        server,
                        port,
                        queryType,
                        LicenseQueryErrorCode.OperationCancelled,
                        true);
                }
                catch (LicenseQueryException ex) when (ex.IsTransient && attempt < Options.MaxRetries)
                {
                    lastException = ex;
                    attempt++;

                    if (Options.EnableErrorRecovery)
                    {
                        await Task.Delay(Options.RetryDelay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    UpdateMetrics(false, stopwatch.ElapsedMilliseconds);

                    var isTransient = IsTransientError(ex);
                    var errorCode = DetermineErrorCode(ex, queryType);

                    throw LicenseQueryException.FromException(
                        $"Query failed: {ex.Message}",
                        server,
                        port,
                        queryType,
                        errorCode,
                        ex);
                }
            }

            UpdateMetrics(false, stopwatch.ElapsedMilliseconds);
            throw LicenseQueryException.FromException(
                $"Query failed after {Options.MaxRetries} retries: {lastException?.Message}",
                server,
                port,
                queryType,
                LicenseQueryErrorCode.TemporaryFailure,
                lastException);
        }

        /// <summary>
        /// Executes lmstat query with retry logic
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        private async Task<ProcessExecutionResult> ExecuteLmstatQueryAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var arguments = $"-c {server}@{port}";
            return await ExecuteLmstatQueryAsync(server, port, arguments, cancellationToken);
        }

        /// <summary>
        /// Executes lmstat query with custom arguments
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="arguments">Command arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        private async Task<ProcessExecutionResult> ExecuteLmstatQueryAsync(string server, int port, string arguments, CancellationToken cancellationToken = default)
        {
            // This would typically read from configuration, but for now we'll use a default path
            var lmutilPath = "lmutil.exe";

            try
            {
                var result = await _processExecutor.ExecuteAsync(
                    lmutilPath,
                    arguments,
                    Options.QueryTimeout,
                    cancellationToken);

                if (!result.Success)
                {
                    throw new LicenseQueryException(
                        $"lmstat command failed with exit code {result.ExitCode}: {result.Error}",
                        server,
                        port,
                        "lmstat",
                        LicenseQueryErrorCode.ProcessExecutionError,
                        false);
                }

                return result;
            }
            catch (Exception ex) when (!(ex is LicenseQueryException))
            {
                throw new LicenseQueryException(
                    $"Failed to execute lmstat: {ex.Message}",
                    server,
                    port,
                    "lmstat",
                    LicenseQueryErrorCode.ProcessExecutionError,
                    ex);
            }
        }

        /// <summary>
        /// Updates performance metrics
        /// </summary>
        /// <param name="success">Whether the query was successful</param>
        /// <param name="elapsedMs">Elapsed time in milliseconds</param>
        private void UpdateMetrics(bool success, long elapsedMs)
        {
            lock (_metricsLock)
            {
                Interlocked.Increment(ref _metrics.TotalQueries);

                if (success)
                {
                    Interlocked.Increment(ref _metrics.SuccessfulQueries);
                }
                else
                {
                    Interlocked.Increment(ref _metrics.FailedQueries);
                }

                // Update timing metrics
                if (_metrics.TotalQueries == 1)
                {
                    _metrics.MinQueryTimeMs = elapsedMs;
                    _metrics.MaxQueryTimeMs = elapsedMs;
                    _metrics.AverageQueryTimeMs = elapsedMs;
                }
                else
                {
                    _metrics.MinQueryTimeMs = Math.Min(_metrics.MinQueryTimeMs, elapsedMs);
                    _metrics.MaxQueryTimeMs = Math.Max(_metrics.MaxQueryTimeMs, elapsedMs);

                    var total = _metrics.AverageQueryTimeMs * (_metrics.TotalQueries - 1) + elapsedMs;
                    _metrics.AverageQueryTimeMs = total / _metrics.TotalQueries;
                }

                // Update cache hit ratio
                _metrics.CacheHitRatio = _metrics.TotalQueries > 0 ?
                    (double)_metrics.CachedQueries / _metrics.TotalQueries : 0;

                _metrics.EndTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Determines if cache entry has expired
        /// </summary>
        /// <param name="lastUpdated">Last update timestamp</param>
        /// <returns>True if cache has expired</returns>
        private bool IsCacheExpired(DateTime lastUpdated)
        {
            return (DateTime.Now - lastUpdated) > Options.CacheExpiration;
        }

        /// <summary>
        /// Determines if an error is transient (retryable)
        /// </summary>
        /// <param name="exception">Exception to check</param>
        /// <returns>True if the error is transient</returns>
        private bool IsTransientError(Exception exception)
        {
            return exception switch
            {
                System.IO.IOException or
                System.Net.Sockets.SocketException or
                System.TimeoutException or
                System.OperationCanceledException => true,
                LicenseQueryException lex when lex.IsTransient => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines the error code for an exception
        /// </summary>
        /// <param name="exception">Exception to analyze</param>
        /// <param name="queryType">Type of query that failed</param>
        /// <returns>Error code</returns>
        private LicenseQueryErrorCode DetermineErrorCode(Exception exception, string queryType)
        {
            return exception switch
            {
                System.TimeoutException => LicenseQueryErrorCode.NetworkTimeout,
                System.Net.Sockets.SocketException => LicenseQueryErrorCode.ConnectionRefused,
                System.OperationCanceledException => LicenseQueryErrorCode.OperationCancelled,
                System.IO.IOException => LicenseQueryErrorCode.NetworkError,
                LicenseQueryException lex => lex.ErrorCode,
                _ => LicenseQueryErrorCode.Unknown
            };
        }
    }
}