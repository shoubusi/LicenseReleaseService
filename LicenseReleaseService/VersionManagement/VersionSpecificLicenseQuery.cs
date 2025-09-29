using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Process;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Provides version-specific license query operations with lmutil integration and caching
    /// </summary>
    public class VersionSpecificLicenseQuery : IDisposable
    {
        private readonly ILogger<VersionSpecificLicenseQuery> _logger;
        private readonly SolidWorksVersionInfo _versionInfo;
        private readonly IProcessExecutor _processExecutor;
        private readonly VersionQueryConfiguration _configuration;
        private readonly Dictionary<string, LicenseQueryResult> _queryCache;
        private readonly object _cacheLock = new object();
        private readonly Timer _cacheCleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Gets the version information for this query service
        /// </summary>
        public SolidWorksVersionInfo VersionInfo => _versionInfo;

        /// <summary>
        /// Gets the current health status of this version
        /// </summary>
        public global::LicenseReleaseService.Configuration.VersionHealthStatus HealthStatus { get; private set; }

        /// <summary>
        /// Gets the last time this version was successfully queried
        /// </summary>
        public DateTime LastSuccessfulQuery { get; private set; }

        /// <summary>
        /// Initializes a new instance of the VersionSpecificLicenseQuery class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="versionInfo">Version information</param>
        /// <param name="processExecutor">Process execution service</param>
        /// <param name="configuration">Query configuration</param>
        public VersionSpecificLicenseQuery(
            ILogger<VersionSpecificLicenseQuery> logger,
            SolidWorksVersionInfo versionInfo,
            IProcessExecutor processExecutor,
            VersionQueryConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _queryCache = new Dictionary<string, LicenseQueryResult>();
            HealthStatus = global::LicenseReleaseService.Configuration.VersionHealthStatus.Unknown;

            // Initialize cache cleanup timer
            _cacheCleanupTimer = new Timer(CleanupCache, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Queries licenses for this specific version
        /// </summary>
        /// <param name="queryOptions">Query options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Version-specific query result</returns>
        public async Task<VersionSpecificQueryResult> QueryLicensesAsync(
            LicenseQueryOptions queryOptions,
            CancellationToken cancellationToken = default)
        {
            if (queryOptions == null)
                throw new ArgumentNullException(nameof(queryOptions));

            try
            {
                _logger.LogDebug("Querying licenses for version {Version}", _versionInfo.Version);

                var result = new VersionSpecificQueryResult
                {
                    Version = _versionInfo.Version,
                    QueryTimestamp = DateTime.UtcNow
                };

                // Check cache first if enabled
                if (_configuration.EnableCaching)
                {
                    var cacheKey = GenerateCacheKey(queryOptions);
                    if (TryGetFromCache(cacheKey, out var cachedResult))
                    {
                        result.LicenseCount = cachedResult.Licenses.Count;
                        result.AvailableLicenses = cachedResult.AvailableLicenses;
                        result.UsedLicenses = cachedResult.UsedLicenses;
                        result.TotalLicenses = cachedResult.TotalLicenses;
                        result.Users = cachedResult.Users.Select(u => u.Username).ToList();
                        result.Cached = true;
                        result.Success = true;
                        return result;
                    }
                }

                // Validate version health before querying
                var healthCheck = await CheckVersionHealthAsync(cancellationToken);
                if (!healthCheck.IsHealthy)
                {
                    result.Success = false;
                    result.Errors.Add($"Version health check failed: {healthCheck.Status}");
                    return result;
                }

                // Build and execute lmutil command
                var command = BuildLmutilCommand(queryOptions);
                var executionResult = await ExecuteLmutilCommandAsync(command, cancellationToken);

                if (!executionResult.Success)
                {
                    result.Success = false;
                    result.Errors.AddRange(executionResult.Errors);
                    result.Warnings.AddRange(executionResult.Warnings);
                    return result;
                }

                // Parse the lmutil output
                var parseResult = ParseLmutilOutput(executionResult.Output, executionResult.ErrorOutput);
                if (!parseResult.Success)
                {
                    result.Success = false;
                    result.Errors.AddRange(parseResult.Errors);
                    return result;
                }

                // Populate result with parsed data
                result.LicenseCount = parseResult.Licenses.Count;
                result.AvailableLicenses = parseResult.AvailableLicenses;
                result.UsedLicenses = parseResult.UsedLicenses;
                result.TotalLicenses = parseResult.TotalLicenses;
                result.Users = parseResult.Users.Select(u => u.Username).Distinct().ToList();
                result.Success = true;

                // Update health status and last query time
                HealthStatus = VersionHealthStatus.Healthy;
                LastSuccessfulQuery = DateTime.UtcNow;

                // Cache the result if enabled
                if (_configuration.EnableCaching)
                {
                    CacheResult(GenerateCacheKey(queryOptions), parseResult);
                }

                _logger.LogDebug("Successfully queried {LicenseCount} licenses for version {Version}",
                    result.LicenseCount, _versionInfo.Version);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying licenses for version {Version}", _versionInfo.Version);
                return new VersionSpecificQueryResult
                {
                    Version = _versionInfo.Version,
                    Success = false,
                    Errors = { $"Query failed: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Tries to allocate licenses from this version
        /// </summary>
        /// <param name="request">Allocation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Allocation result</returns>
        public async Task<VersionAllocationResult> TryAllocateAsync(
            MultiVersionAllocationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                _logger.LogDebug("Trying to allocate {FeatureName} licenses from version {Version}",
                    request.FeatureName, _versionInfo.Version);

                // Query current license status
                var queryOptions = new LicenseQueryOptions
                {
                    QueryTimeout = _configuration.QueryTimeout,
                    EnableCaching = false // Get fresh data for allocation
                };

                var queryResult = await QueryLicensesAsync(queryOptions, cancellationToken);
                if (!queryResult.Success)
                {
                    return new VersionAllocationResult
                    {
                        Success = false,
                        ErrorMessage = $"License query failed: {string.Join(", ", queryResult.Errors)}"
                    };
                }

                // Check if feature exists and has available licenses
                var featureLicenses = queryResult.AvailableLicenses
                    .Where(l => l.FeatureName.Equals(request.FeatureName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (featureLicenses.Count == 0)
                {
                    return new VersionAllocationResult
                    {
                        Success = false,
                        ErrorMessage = $"Feature {request.FeatureName} not found in version {_versionInfo.Version}"
                    };
                }

                var availableCount = featureLicenses.Sum(l => l.AvailableLicenses);
                if (availableCount < request.RequiredLicenses)
                {
                    return new VersionAllocationResult
                    {
                        Success = false,
                        ErrorMessage = $"Insufficient licenses for {request.FeatureName}: {availableCount} available, {request.RequiredLicenses} required"
                    };
                }

                // Simulate allocation (in real implementation, this would interact with license server)
                var allocationResult = await SimulateLicenseAllocationAsync(request, queryResult, cancellationToken);

                _logger.LogDebug("Allocation {Success} for {FeatureName} from version {Version}",
                    allocationResult.Success ? "successful" : "failed", request.FeatureName, _versionInfo.Version);

                return allocationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating licenses from version {Version}", _versionInfo.Version);
                return new VersionAllocationResult
                {
                    Success = false,
                    ErrorMessage = $"Allocation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Releases licenses for this version
        /// </summary>
        /// <param name="request">Release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Release result</returns>
        public async Task<VersionReleaseResult> ReleaseLicensesAsync(
            MultiVersionReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                _logger.LogDebug("Releasing {FeatureName} licenses for user {User} from version {Version}",
                    request.FeatureName, request.User, _versionInfo.Version);

                // Validate release request
                var validationResult = await ValidateReleaseRequestAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    return new VersionReleaseResult
                    {
                        Success = false,
                        Errors = validationResult.Errors.ToList()
                    };
                }

                // Simulate license release (in real implementation, this would interact with license server)
                var releaseResult = await SimulateLicenseReleaseAsync(request, cancellationToken);

                if (releaseResult.Success)
                {
                    // Clear cache to ensure fresh data on next query
                    ClearCache();
                }

                _logger.LogDebug("Release {Success} for {FeatureName} from version {Version}",
                    releaseResult.Success ? "successful" : "failed", request.FeatureName, _versionInfo.Version);

                return releaseResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing licenses from version {Version}", _versionInfo.Version);
                return new VersionReleaseResult
                {
                    Success = false,
                    Errors = { $"Release error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Gets the health status of this version
        /// </summary>
        /// <returns>Health status result</returns>
        public async Task<VersionHealthResult> GetHealthStatusAsync()
        {
            try
            {
                _logger.LogDebug("Checking health status for version {Version}", _versionInfo.Version);

                var result = new VersionHealthResult
                {
                    Version = _versionInfo.Version,
                    LastCheck = DateTime.UtcNow
                };

                // Check basic version accessibility
                if (!VersionInfo.IsAvailable)
                {
                    result.IsHealthy = false;
                    result.Status = "Version not available";
                    return result;
                }

                // Check executable accessibility
                if (!File.Exists(_versionInfo.ExecutablePath))
                {
                    result.IsHealthy = false;
                    result.Status = "Executable not found";
                    return result;
                }

                // Check license manager accessibility
                if (string.IsNullOrWhiteSpace(_versionInfo.LmutilPath) || !File.Exists(_versionInfo.LmutilPath))
                {
                    result.IsHealthy = false;
                    result.Status = "License manager not accessible";
                    return result;
                }

                // Test lmutil functionality
                var testResult = await TestLmutilFunctionalityAsync();
                if (!testResult.Success)
                {
                    result.IsHealthy = false;
                    result.Status = testResult.ErrorMessage ?? "License manager test failed";
                    return result;
                }

                // Check license server connectivity
                var connectivityResult = await CheckLicenseServerConnectivityAsync();
                if (!connectivityResult.IsConnected)
                {
                    result.IsHealthy = false;
                    result.Status = $"License server not reachable: {connectivityResult.ErrorMessage}";
                    return result;
                }

                // All checks passed
                result.IsHealthy = true;
                result.Status = "Healthy";
                result.Metrics = new Dictionary<string, object>
                {
                    ["ResponseTime"] = connectivityResult.ResponseTime.TotalMilliseconds,
                    ["ServerStatus"] = connectivityResult.ServerStatus,
                    ["LastSuccessfulQuery"] = LastSuccessfulQuery
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health status for version {Version}", _versionInfo.Version);
                return new VersionHealthResult
                {
                    Version = _versionInfo.Version,
                    IsHealthy = false,
                    Status = $"Health check failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Updates the version information (used when version detection is refreshed)
        /// </summary>
        /// <param name="newVersionInfo">Updated version information</param>
        public void UpdateVersionInfo(SolidWorksVersionInfo newVersionInfo)
        {
            if (newVersionInfo == null)
                throw new ArgumentNullException(nameof(newVersionInfo));

            if (newVersionInfo.Version != _versionInfo.Version)
            {
                throw new ArgumentException("Version mismatch during update", nameof(newVersionInfo));
            }

            // Update internal version info
            _versionInfo.InstallationPath = newVersionInfo.InstallationPath;
            _versionInfo.ExecutablePath = newVersionInfo.ExecutablePath;
            _versionInfo.LmutilPath = newVersionInfo.LmutilPath;
            _versionInfo.FullVersion = newVersionInfo.FullVersion;
            _versionInfo.ServicePack = newVersionInfo.ServicePack;
            _versionInfo.BuildNumber = newVersionInfo.BuildNumber;
            _versionInfo.Health = newVersionInfo.Health;
            _versionInfo.LastDetected = newVersionInfo.LastDetected;

            _logger.LogDebug("Updated version info for {Version}", _versionInfo.Version);
        }

        /// <summary>
        /// Disposes the version-specific license query service
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the version-specific license query service
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cacheCleanupTimer?.Dispose();
                    ClearCache();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Generates a cache key for query options
        /// </summary>
        /// <param name="queryOptions">Query options</param>
        /// <returns>Cache key</returns>
        private string GenerateCacheKey(LicenseQueryOptions queryOptions)
        {
            return $"{_versionInfo.Version}_{queryOptions.QueryTimeout.TotalMilliseconds}_{DateTime.UtcNow:yyyyMMddHHmm}";
        }

        /// <summary>
        /// Tries to get a result from cache
        /// </summary>
        /// <param name="cacheKey">Cache key</param>
        /// <param name="result">Cached result</param>
        /// <returns>True if found and valid, false otherwise</returns>
        private bool TryGetFromCache(string cacheKey, out LicenseQueryResult result)
        {
            lock (_cacheLock)
            {
                if (_queryCache.TryGetValue(cacheKey, out result))
                {
                    // Check if cache entry is still valid
                    if (DateTime.UtcNow - result.QueryTimestamp <= _configuration.CacheExpiration)
                    {
                        return true;
                    }
                    else
                    {
                        // Remove expired entry
                        _queryCache.Remove(cacheKey);
                    }
                }
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Caches a query result
        /// </summary>
        /// <param name="cacheKey">Cache key</param>
        /// <param name="result">Result to cache</param>
        private void CacheResult(string cacheKey, LicenseQueryResult result)
        {
            lock (_cacheLock)
            {
                // Remove oldest entries if cache is full
                while (_queryCache.Count >= _configuration.MaxCacheSize)
                {
                    var oldestKey = _queryCache.OrderBy(kvp => kvp.Value.QueryTimestamp).First().Key;
                    _queryCache.Remove(oldestKey);
                }

                _queryCache[cacheKey] = result;
            }
        }

        /// <summary>
        /// Cleans up expired cache entries
        /// </summary>
        /// <param name="state">Timer state</param>
        private void CleanupCache(object state)
        {
            try
            {
                lock (_cacheLock)
                {
                    var expiredKeys = _queryCache
                        .Where(kvp => DateTime.UtcNow - kvp.Value.QueryTimestamp > _configuration.CacheExpiration)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredKeys)
                    {
                        _queryCache.Remove(key);
                    }

                    _logger.LogDebug("Cleaned up {Count} expired cache entries for version {Version}",
                        expiredKeys.Count, _versionInfo.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up cache for version {Version}", _versionInfo.Version);
            }
        }

        /// <summary>
        /// Clears the entire cache
        /// </summary>
        private void ClearCache()
        {
            lock (_cacheLock)
            {
                _queryCache.Clear();
                _logger.LogDebug("Cleared cache for version {Version}", _versionInfo.Version);
            }
        }

        /// <summary>
        /// Builds the lmutil command for the given query options
        /// </summary>
        /// <param name="queryOptions">Query options</param>
        /// <returns>Lmutil command</returns>
        private LmutilCommand BuildLmutilCommand(LicenseQueryOptions queryOptions)
        {
            var command = new LmutilCommand
            {
                ExecutablePath = _versionInfo.LmutilPath,
                Timeout = _configuration.QueryTimeout,
                Arguments = new List<string>()
            };

            // Build base command
            command.Arguments.Add("lmstat");
            command.Arguments.Add("-a"); // Show all features
            command.Arguments.Add("-c"); // License file path (will be added dynamically)

            // Add version-specific parameters if needed
            if (_versionInfo.Version != "2025") // Assume 2025 is the default
            {
                command.Arguments.Add("-f");
                command.Arguments.Add(GetVersionSpecificFeatureFilter(_versionInfo.Version));
            }

            // Add additional options based on query parameters
            if (queryOptions.IncludedFeatures.Count > 0)
            {
                command.Arguments.Add("-i");
                command.Arguments.Add(string.Join(",", queryOptions.IncludedFeatures));
            }

            return command;
        }

        /// <summary>
        /// Executes the lmutil command
        /// </summary>
        /// <param name="command">Command to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Execution result</returns>
        private async Task<LmutilExecutionResult> ExecuteLmutilCommandAsync(
            LmutilCommand command,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Executing lmutil command for version {Version}", _versionInfo.Version);

                // Prepare command line
                var arguments = string.Join(" ", command.Arguments);
                var executionResult = await _processExecutor.ExecuteAsync(
                    command.ExecutablePath,
                    arguments,
                    command.Timeout,
                    cancellationToken);

                return new LmutilExecutionResult
                {
                    Success = executionResult.ExitCode == 0,
                    Output = executionResult.StandardOutput,
                    ErrorOutput = executionResult.StandardError,
                    ExitCode = executionResult.ExitCode,
                    ExecutionTime = executionResult.ExecutionTime,
                    Errors = executionResult.ExitCode != 0 ?
                        new List<string> { $"lmutil exited with code {executionResult.ExitCode}" } :
                        new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing lmutil command for version {Version}", _versionInfo.Version);
                return new LmutilExecutionResult
                {
                    Success = false,
                    Errors = { $"Command execution failed: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Parses lmutil output into license information
        /// </summary>
        /// <param name="output">Standard output</param>
        /// <param name="errorOutput">Error output</param>
        /// <returns>Parsed result</returns>
        private LicenseQueryResult ParseLmutilOutput(string output, string errorOutput)
        {
            try
            {
                _logger.LogDebug("Parsing lmutil output for version {Version}", _versionInfo.Version);

                var result = new LicenseQueryResult
                {
                    QueryTimestamp = DateTime.UtcNow,
                    Success = true
                };

                if (string.IsNullOrWhiteSpace(output))
                {
                    result.Success = false;
                    result.Errors.Add("No output from lmutil command");
                    return result;
                }

                // Parse license features
                var featureRegex = new Regex(@"Users of (\w+):\s+\(Total of (\d+) licenses issued; Total of (\d+) licenses in use\)");
                var userRegex = new Regex(@"(\w+)\s+(\S+)\s+\(([^)]+)\)\s+(.+?)(?:\s+\(v(\d+\.\d+)\))?");
                var borrowRegex = new Regex(@"(\w+)\s+(\S+)\s+\(borrowed:\s+(\d{4}/\d{2}/\d{2})\)");

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    // Parse feature lines
                    var featureMatch = featureRegex.Match(line);
                    if (featureMatch.Success)
                    {
                        var feature = new LicenseFeature
                        {
                            FeatureName = featureMatch.Groups[1].Value,
                            TotalLicenses = int.Parse(featureMatch.Groups[2].Value),
                            UsedLicenses = int.Parse(featureMatch.Groups[3].Value),
                            AvailableLicenses = int.Parse(featureMatch.Groups[2].Value) - int.Parse(featureMatch.Groups[3].Value),
                            Version = _versionInfo.Version
                        };
                        result.Licenses.Add(feature);
                    }

                    // Parse user lines
                    var userMatch = userRegex.Match(line);
                    if (userMatch.Success)
                    {
                        var user = new LicenseUser
                        {
                            Username = userMatch.Groups[1].Value,
                            Host = userMatch.Groups[2].Value,
                            Display = userMatch.Groups[3].Value,
                            Version = userMatch.Groups[5].Success ? userMatch.Groups[5].Value : "unknown",
                            CheckoutTime = DateTime.UtcNow, // Would need to parse from display field
                            Feature = GetLastFeature(result.Licenses) // Last processed feature
                        };
                        result.Users.Add(user);
                    }

                    // Parse borrow lines
                    var borrowMatch = borrowRegex.Match(line);
                    if (borrowMatch.Success)
                    {
                        var borrowedLicense = new BorrowedLicense
                        {
                            Username = borrowMatch.Groups[1].Value,
                            Host = borrowMatch.Groups[2].Value,
                            BorrowDate = DateTime.ParseExact(borrowMatch.Groups[3].Value, "yyyy/MM/dd", null),
                            Feature = GetLastFeature(result.Licenses)
                        };
                        result.BorrowedLicenses.Add(borrowedLicense);
                    }
                }

                // Calculate totals
                result.TotalLicenses = result.Licenses.Sum(l => l.TotalLicenses);
                result.AvailableLicenses = result.Licenses.Sum(l => l.AvailableLicenses);
                result.UsedLicenses = result.Licenses.Sum(l => l.UsedLicenses);

                if (result.Licenses.Count == 0)
                {
                    result.Warnings.Add("No license features found in lmutil output");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing lmutil output for version {Version}", _versionInfo.Version);
                return new LicenseQueryResult
                {
                    Success = false,
                    Errors = { $"Parse error: {ex.Message}" },
                    RawOutput = output,
                    ErrorOutput = errorOutput
                };
            }
        }

        /// <summary>
        /// Checks the version health
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        private async Task<VersionHealthResult> CheckVersionHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check basic accessibility
                if (!VersionInfo.IsAvailable)
                {
                    return new VersionHealthResult
                    {
                        Version = _versionInfo.Version,
                        IsHealthy = false,
                        Status = "Version not available"
                    };
                }

                // Test lmutil functionality
                var testResult = await TestLmutilFunctionalityAsync(cancellationToken);
                if (!testResult.Success)
                {
                    return new VersionHealthResult
                    {
                        Version = _versionInfo.Version,
                        IsHealthy = false,
                        Status = testResult.ErrorMessage
                    };
                }

                return new VersionHealthResult
                {
                    Version = _versionInfo.Version,
                    IsHealthy = true,
                    Status = "Healthy"
                };
            }
            catch (Exception ex)
            {
                return new VersionHealthResult
                {
                    Version = _versionInfo.Version,
                    IsHealthy = false,
                    Status = $"Health check error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Tests lmutil functionality
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Test result</returns>
        private async Task<LmutilTestResult> TestLmutilFunctionalityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Test with -help parameter
                var result = await _processExecutor.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "-help",
                    TimeSpan.FromSeconds(10),
                    cancellationToken);

                return new LmutilTestResult
                {
                    Success = result.ExitCode == 0,
                    ErrorMessage = result.ExitCode != 0 ? result.StandardError : null
                };
            }
            catch (Exception ex)
            {
                return new LmutilTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Checks license server connectivity
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Connectivity result</returns>
        private async Task<ServerConnectivityResult> CheckLicenseServerConnectivityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Try to connect to license server
                var result = await _processExecutor.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "lmstat -c",
                    TimeSpan.FromSeconds(15),
                    cancellationToken);

                stopwatch.Stop();

                return new ServerConnectivityResult
                {
                    IsConnected = result.ExitCode == 0,
                    ResponseTime = stopwatch.Elapsed,
                    ServerStatus = result.ExitCode == 0 ? "Online" : "Offline",
                    ErrorMessage = result.ExitCode != 0 ? result.StandardError : null
                };
            }
            catch (Exception ex)
            {
                return new ServerConnectivityResult
                {
                    IsConnected = false,
                    ResponseTime = TimeSpan.Zero,
                    ServerStatus = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Simulates license allocation
        /// </summary>
        /// <param name="request">Allocation request</param>
        /// <param name="queryResult">Current license status</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Allocation result</returns>
        private async Task<VersionAllocationResult> SimulateLicenseAllocationAsync(
            MultiVersionAllocationRequest request,
            VersionSpecificQueryResult queryResult,
            CancellationToken cancellationToken = default)
        {
            // In a real implementation, this would interact with the license server
            // For now, we'll simulate successful allocation
            await Task.Delay(100, cancellationToken); // Simulate server interaction time

            return new VersionAllocationResult
            {
                Success = true,
                AllocatedLicenses = request.RequiredLicenses,
                RemainingLicenses = queryResult.AvailableLicenses - request.RequiredLicenses,
                AllocationId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddHours(1) // Default expiration
            };
        }

        /// <summary>
        /// Validates a release request
        /// </summary>
        /// <param name="request">Release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        private async Task<ReleaseValidationResult> ValidateReleaseRequestAsync(
            MultiVersionReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = new ReleaseValidationResult();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.FeatureName))
            {
                result.Errors.Add("Feature name is required");
            }

            if (string.IsNullOrWhiteSpace(request.User))
            {
                result.Errors.Add("User is required");
            }

            if (request.LicenseCount <= 0)
            {
                result.Errors.Add("License count must be greater than 0");
            }

            // Query current status to validate release
            var queryOptions = new LicenseQueryOptions
            {
                QueryTimeout = _configuration.QueryTimeout,
                EnableCaching = false
            };

            var queryResult = await QueryLicensesAsync(queryOptions, cancellationToken);
            if (queryResult.Success)
            {
                // Check if user has the feature checked out
                var userFeatureUsage = queryResult.Users
                    .Where(u => u.Equals(request.User, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (userFeatureUsage.Count == 0)
                {
                    result.Warnings.Add($"User {request.User} has no active licenses");
                }
            }

            return result;
        }

        /// <summary>
        /// Simulates license release
        /// </summary>
        /// <param name="request">Release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Release result</returns>
        private async Task<VersionReleaseResult> SimulateLicenseReleaseAsync(
            MultiVersionReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            // In a real implementation, this would interact with the license server
            await Task.Delay(100, cancellationToken); // Simulate server interaction time

            return new VersionReleaseResult
            {
                Success = true,
                ReleasedLicenses = new List<ReleasedLicense>
                {
                    new ReleasedLicense
                    {
                        FeatureName = request.FeatureName,
                        User = request.User,
                        ReleasedCount = request.LicenseCount,
                        ReleasedAt = DateTime.UtcNow
                    }
                }
            };
        }

        /// <summary>
        /// Gets version-specific feature filter
        /// </summary>
        /// <param name="version">Version string</param>
        /// <returns>Feature filter string</returns>
        private string GetVersionSpecificFeatureFilter(string version)
        {
            // Return version-specific feature patterns
            return version switch
            {
                "2025" => "sldworks.*2025",
                "2024" => "sldworks.*2024",
                "2023" => "sldworks.*2023",
                "2022" => "sldworks.*2022",
                "2021" => "sldworks.*2021",
                "2020" => "sldworks.*2020",
                _ => "sldworks"
            };
        }

        /// <summary>
        /// Gets the last processed license feature
        /// </summary>
        /// <param name="licenses">List of licenses</param>
        /// <returns>Last feature or null</returns>
        private LicenseFeature GetLastFeature(List<LicenseFeature> licenses)
        {
            return licenses.Count > 0 ? licenses.Last() : null;
        }
    }

    
    /// <summary>
    /// Command structure for lmutil execution
    /// </summary>
    public class LmutilCommand
    {
        public string ExecutablePath { get; set; }
        public TimeSpan Timeout { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of lmutil execution
    /// </summary>
    public class LmutilExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string ErrorOutput { get; set; }
        public int ExitCode { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of lmutil functionality test
    /// </summary>
    public class LmutilTestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of server connectivity check
    /// </summary>
    public class ServerConnectivityResult
    {
        public bool IsConnected { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string ServerStatus { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of license query parsing
    /// </summary>
    public class LicenseQueryResult
    {
        public bool Success { get; set; }
        public DateTime QueryTimestamp { get; set; }
        public List<LicenseFeature> Licenses { get; set; } = new List<LicenseFeature>();
        public List<LicenseUser> Users { get; set; } = new List<LicenseUser>();
        public List<BorrowedLicense> BorrowedLicenses { get; set; } = new List<BorrowedLicense>();
        public int TotalLicenses { get; set; }
        public int AvailableLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string RawOutput { get; set; }
        public string ErrorOutput { get; set; }
    }

    /// <summary>
    /// Represents a license feature
    /// </summary>
    public class LicenseFeature
    {
        public string FeatureName { get; set; }
        public string Version { get; set; }
        public int TotalLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public int AvailableLicenses { get; set; }
    }

    /// <summary>
    /// Represents a license user
    /// </summary>
    public class LicenseUser
    {
        public string Username { get; set; }
        public string Host { get; set; }
        public string Display { get; set; }
        public string Version { get; set; }
        public DateTime CheckoutTime { get; set; }
        public string Feature { get; set; }
    }

    /// <summary>
    /// Represents a borrowed license
    /// </summary>
    public class BorrowedLicense
    {
        public string Username { get; set; }
        public string Host { get; set; }
        public DateTime BorrowDate { get; set; }
        public string Feature { get; set; }
    }

    /// <summary>
    /// Result of release request validation
    /// </summary>
    public class ReleaseValidationResult
    {
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>
    /// Represents a released license
    /// </summary>
    public class ReleasedLicense
    {
        public string FeatureName { get; set; }
        public string User { get; set; }
        public int ReleasedCount { get; set; }
        public DateTime ReleasedAt { get; set; }
    }
}