using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Process;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Implements license management operations using lmutil.exe
    /// </summary>
    public class LmutilLicenseManager : ILicenseManager, IDisposable
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<LmutilLicenseManager> _logger;
        private readonly ServiceSettings _serviceSettings;
        private readonly ProcessExecutionOptions _processOptions;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly string _lmutilPath;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the LmutilLicenseManager class
        /// </summary>
        /// <param name="processExecutor">The process executor instance</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="serviceSettings">The service settings</param>
        /// <param name="processOptions">The process execution options</param>
        public LmutilLicenseManager(
            IProcessExecutor processExecutor,
            ILogger<LmutilLicenseManager> logger,
            ServiceSettings serviceSettings,
            ProcessExecutionOptions processOptions)
        {
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceSettings = serviceSettings ?? throw new ArgumentNullException(nameof(serviceSettings));
            _processOptions = processOptions?.Clone() ?? throw new ArgumentNullException(nameof(processOptions));

            _lmutilPath = _serviceSettings.LmutilPath;
            if (string.IsNullOrEmpty(_lmutilPath))
            {
                throw new ArgumentException("lmutil.exe path is not configured", nameof(serviceSettings));
            }

            // Initialize circuit breaker with license manager specific settings
            _circuitBreaker = new CircuitBreaker(
                failureThreshold: 3,
                recoveryTimeout: TimeSpan.FromMinutes(1),
                timeout: TimeSpan.FromSeconds(30),
                logger: _logger);
        }

        /// <summary>
        /// Gets the status of a license server
        /// </summary>
        public async Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Getting license server status for {Server}:{Port}", server, port);

                var arguments = $"lmstat -c {port}@{server}";
                var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

                if (result.ExitCode != 0)
                {
                    _logger.LogError("lmstat failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
                    throw new LicenseManagerException($"License server status check failed: {result.Error}", result.ExitCode);
                }

                var status = ParseServerStatus(result.Output, server, port);
                status.LastChecked = DateTime.Now;
                status.ResponseTimeMs = (long)result.ExecutionTime.TotalMilliseconds;

                _logger.LogInformation("License server status retrieved successfully for {Server}:{Port}", server, port);
                return status;
            }, cancellationToken);
        }

        /// <summary>
        /// Releases a license from a specific user
        /// </summary>
        public async Task<LicenseReleaseResult> ReleaseLicenseAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Releasing license for feature {Feature} from user {User} on {Server}:{Port}", feature, user, server, port);

                var startTime = DateTime.Now;
                var arguments = $"lmremove -c {port}@{server} {feature} {user}";
                var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

                var executionTime = DateTime.Now - startTime;
                var releaseResult = new LicenseReleaseResult
                {
                    Server = server,
                    Port = port,
                    Feature = feature,
                    User = user,
                    ExecutionTime = executionTime,
                    Timestamp = DateTime.Now,
                    Command = arguments,
                    CommandOutput = result.Output,
                    CommandError = result.Error,
                    ExitCode = result.ExitCode,
                    ProcessId = result.ProcessId
                };

                if (result.ExitCode == 0)
                {
                    releaseResult.Success = true;
                    releaseResult.ResultCode = LicenseReleaseResultCode.Success;
                    releaseResult.LicensesReleased = ParseReleasedLicensesCount(result.Output);
                    _logger.LogInformation("License released successfully for feature {Feature} from user {User} on {Server}:{Port}. Released {Count} licenses.",
                        feature, user, server, port, releaseResult.LicensesReleased);
                }
                else
                {
                    releaseResult.Success = false;
                    releaseResult.ResultCode = DetermineFailureResultCode(result.Error, result.ExitCode);
                    releaseResult.ErrorMessage = result.Error;
                    _logger.LogWarning("License release failed for feature {Feature} from user {User} on {Server}:{Port}: {Error}",
                        feature, user, server, port, result.Error);
                }

                return releaseResult;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets information about a specific license feature
        /// </summary>
        public async Task<LicenseFeatureInfo> GetFeatureInfoAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Getting feature info for {Feature} on {Server}:{Port}", feature, server, port);

                var arguments = $"lmstat -c {port}@{server} -f {feature}";
                var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

                if (result.ExitCode != 0)
                {
                    _logger.LogError("lmstat -f failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
                    throw new LicenseManagerException($"Feature info check failed: {result.Error}", result.ExitCode);
                }

                var featureInfo = ParseFeatureInfo(result.Output, feature);
                _logger.LogInformation("Feature info retrieved successfully for {Feature} on {Server}:{Port}", feature, server, port);
                return featureInfo;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets all available features from the license server
        /// </summary>
        public async Task<Dictionary<string, LicenseFeatureInfo>> GetAllFeaturesAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Getting all features from {Server}:{Port}", server, port);

                var arguments = $"lmstat -c {port}@{server}";
                var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

                if (result.ExitCode != 0)
                {
                    _logger.LogError("lmstat failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
                    throw new LicenseManagerException($"Feature list retrieval failed: {result.Error}", result.ExitCode);
                }

                var features = ParseAllFeatures(result.Output);
                _logger.LogInformation("Retrieved {Count} features from {Server}:{Port}", features.Count, server, port);
                return features;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets all users currently using licenses
        /// </summary>
        public async Task<Dictionary<string, LicenseUserUsage>> GetUsersAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Getting all users from {Server}:{Port}", server, port);

                var arguments = $"lmstat -c {port}@{server} -a";
                var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

                if (result.ExitCode != 0)
                {
                    _logger.LogError("lmstat -a failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
                    throw new LicenseManagerException($"User list retrieval failed: {result.Error}", result.ExitCode);
                }

                var users = ParseAllUsers(result.Output);
                _logger.LogInformation("Retrieved {Count} users from {Server}:{Port}", users.Count, server, port);
                return users;
            }, cancellationToken);
        }

        /// <summary>
        /// Checks if a license server is available and responsive
        /// </summary>
        public async Task<bool> IsServerAvailableAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            try
            {
                var status = await GetServerStatusAsync(server, port, cancellationToken);
                return status.IsAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "License server availability check failed for {Server}:{Port}", server, port);
                return false;
            }
        }

        /// <summary>
        /// Gets license usage statistics
        /// </summary>
        public async Task<LicenseUsageStatistics> GetUsageStatisticsAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Getting usage statistics from {Server}:{Port}", server, port);

                var arguments = $"lmstat -c {port}@{server}";
                var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

                if (result.ExitCode != 0)
                {
                    _logger.LogError("lmstat failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
                    throw new LicenseManagerException($"Usage statistics retrieval failed: {result.Error}", result.ExitCode);
                }

                var statistics = ParseUsageStatistics(result.Output, server, port);
                _logger.LogInformation("Usage statistics retrieved successfully from {Server}:{Port}", server, port);
                return statistics;
            }, cancellationToken);
        }

        /// <summary>
        /// Executes an lmutil command with retry logic
        /// </summary>
        private async Task<ProcessExecutionResult> ExecuteWithRetryAsync(string arguments, CancellationToken cancellationToken)
        {
            var maxRetries = _serviceSettings.LicenseManagerRetryCount;
            var retryDelay = TimeSpan.FromMilliseconds(_serviceSettings.RetryDelay);
            var retryCount = 0;

            while (retryCount <= maxRetries)
            {
                try
                {
                    var timeout = TimeSpan.FromSeconds(_serviceSettings.LicenseManagerTimeout);
                    var result = await _processExecutor.ExecuteAsync(_lmutilPath, arguments, timeout, cancellationToken);

                    if (result.ExitCode == 0 || retryCount == maxRetries)
                    {
                        result.RetryAttempt = retryCount;
                        return result;
                    }

                    _logger.LogWarning("lmutil command failed, retry {RetryCount}/{MaxRetries}: {Error}",
                        retryCount + 1, maxRetries, result.Error);

                    retryCount++;
                    if (retryCount <= maxRetries)
                    {
                        await Task.Delay(retryDelay, cancellationToken);
                    }
                }
                catch (Exception ex) when (retryCount < maxRetries)
                {
                    _logger.LogWarning(ex, "lmutil command failed, retry {RetryCount}/{MaxRetries}",
                        retryCount + 1, maxRetries);
                    retryCount++;
                    if (retryCount <= maxRetries)
                    {
                        await Task.Delay(retryDelay, cancellationToken);
                    }
                }
            }

            throw new LicenseManagerException($"lmutil command failed after {maxRetries} retries");
        }

        /// <summary>
        /// Parses lmutil lmstat output to create LicenseServerStatus
        /// </summary>
        private LicenseServerStatus ParseServerStatus(string output, string server, int port)
        {
            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = false,
                IsHealthy = false
            };

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Contains("license server UP"))
                {
                    status.IsServerUp = true;
                    status.IsHealthy = true;
                    status.StatusMessage = "License server is running normally";
                }
                else if (trimmedLine.Contains("license server DOWN"))
                {
                    status.IsServerUp = false;
                    status.IsHealthy = false;
                    status.StatusMessage = "License server is down";
                }
                else if (trimmedLine.Contains("license server"))
                {
                    // Parse server information
                    var serverMatch = Regex.Match(trimmedLine, @"license server\s+(\S+):(\d+)");
                    if (serverMatch.Success)
                    {
                        status.Server = serverMatch.Groups[1].Value;
                        status.Port = int.Parse(serverMatch.Groups[2].Value);
                    }
                }
                else if (trimmedLine.Contains("Vendor daemon"))
                {
                    var daemonMatch = Regex.Match(trimmedLine, @"Vendor daemon\s+(\S+)");
                    if (daemonMatch.Success)
                    {
                        status.Vendor = daemonMatch.Groups[1].Value;
                        status.DaemonStatus = "UP";
                    }
                }
            }

            // Parse features and update aggregate counts
            var features = ParseAllFeatures(output);
            foreach (var kvp in features)
            {
                status.AddOrUpdateFeature(kvp.Key, new LicenseFeatureStatus
                {
                    FeatureName = kvp.Value.FeatureName,
                    Version = kvp.Value.Version,
                    TotalLicenses = kvp.Value.TotalLicenses,
                    LicensesInUse = kvp.Value.LicensesInUse,
                    AvailableLicenses = kvp.Value.AvailableLicenses,
                    Status = kvp.Value.Status
                });
            }

            return status;
        }

        /// <summary>
        /// Parses the number of released licenses from lmremove output
        /// </summary>
        private int ParseReleasedLicensesCount(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("removed") || line.Contains("released"))
                {
                    var match = Regex.Match(line, @"(\d+)\s+(license|licenses)");
                    if (match.Success)
                    {
                        return int.Parse(match.Groups[1].Value);
                    }
                }
            }
            return 1; // Default to 1 if we can't parse the count
        }

        /// <summary>
        /// Parses feature information from lmstat output
        /// </summary>
        private LicenseFeatureInfo ParseFeatureInfo(string output, string featureName)
        {
            var featureInfo = new LicenseFeatureInfo { FeatureName = featureName };

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Contains(featureName))
                {
                    // Parse feature line
                    var featureMatch = Regex.Match(trimmedLine, $@"{featureName}\s+(\S+):");
                    if (featureMatch.Success)
                    {
                        featureInfo.Version = featureMatch.Groups[1].Value;
                    }

                    // Parse license counts
                    var countMatch = Regex.Match(trimmedLine, @"(\d+)\s+license\(s\) in use");
                    if (countMatch.Success)
                    {
                        featureInfo.LicensesInUse = int.Parse(countMatch.Groups[1].Value);
                    }

                    var totalMatch = Regex.Match(trimmedLine, @"(\d+)\s+total license\(s\)");
                    if (totalMatch.Success)
                    {
                        featureInfo.TotalLicenses = int.Parse(totalMatch.Groups[1].Value);
                    }
                }
            }

            featureInfo.AvailableLicenses = featureInfo.TotalLicenses - featureInfo.LicensesInUse;
            featureInfo.Status = featureInfo.AvailableLicenses > 0 ? "ACTIVE" : "EXHAUSTED";

            return featureInfo;
        }

        /// <summary>
        /// Parses all features from lmstat output
        /// </summary>
        private Dictionary<string, LicenseFeatureInfo> ParseAllFeatures(string output)
        {
            var features = new Dictionary<string, LicenseFeatureInfo>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Match feature lines
                var featureMatch = Regex.Match(trimmedLine, @"(\w+)\s+(\S+):\s+v=(\S+)");
                if (featureMatch.Success)
                {
                    var featureName = featureMatch.Groups[1].Value;
                    var vendor = featureMatch.Groups[2].Value;
                    var version = featureMatch.Groups[3].Value;

                    if (!features.ContainsKey(featureName))
                    {
                        features[featureName] = new LicenseFeatureInfo
                        {
                            FeatureName = featureName,
                            Vendor = vendor,
                            Version = version
                        };
                    }
                }
            }

            return features;
        }

        /// <summary>
        /// Parses all users from lmstat output
        /// </summary>
        private Dictionary<string, LicenseUserUsage> ParseAllUsers(string output)
        {
            var users = new Dictionary<string, LicenseUserUsage>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Match user lines: username hostname (display) start_time
                var userMatch = Regex.Match(trimmedLine, @"(\w+)\s+(\S+)\s+\(([^)]+)\)\s+(.+)");
                if (userMatch.Success)
                {
                    var userName = userMatch.Groups[1].Value;
                    var hostName = userMatch.Groups[2].Value;
                    var displayName = userMatch.Groups[3].Value;
                    var startTimeStr = userMatch.Groups[4].Value;

                    if (!users.ContainsKey(userName))
                    {
                        users[userName] = new LicenseUserUsage
                        {
                            UserName = userName,
                            HostName = hostName,
                            DisplayName = displayName,
                            LicensesCheckedOut = 1
                        };
                    }
                    else
                    {
                        users[userName].LicensesCheckedOut++;
                    }
                }
            }

            return users;
        }

        /// <summary>
        /// Parses usage statistics from lmstat output
        /// </summary>
        private LicenseUsageStatistics ParseUsageStatistics(string output, string server, int port)
        {
            var statistics = new LicenseUsageStatistics
            {
                Server = server,
                Port = port,
                Timestamp = DateTime.Now
            };

            var features = ParseAllFeatures(output);
            statistics.ActiveFeatures = features.Count;

            foreach (var feature in features.Values)
            {
                statistics.TotalLicenses += feature.TotalLicenses;
                statistics.TotalLicensesInUse += feature.LicensesInUse;
                statistics.TotalAvailableLicenses += feature.AvailableLicenses;
            }

            statistics.OverallUtilization = statistics.TotalLicenses > 0 ?
                (statistics.TotalLicensesInUse * 100.0 / statistics.TotalLicenses) : 0;

            var users = ParseAllUsers(output);
            statistics.UniqueUsers = users.Count;

            return statistics;
        }

        /// <summary>
        /// Determines the failure result code based on error message and exit code
        /// </summary>
        private LicenseReleaseResultCode DetermineFailureResultCode(string errorMessage, int exitCode)
        {
            if (errorMessage.Contains("cannot connect"))
                return LicenseReleaseResultCode.ServerUnavailable;
            if (errorMessage.Contains("feature") && errorMessage.Contains("not found"))
                return LicenseReleaseResultCode.FeatureNotFound;
            if (errorMessage.Contains("user") && errorMessage.Contains("not found"))
                return LicenseReleaseResultCode.UserNotFound;
            if (errorMessage.Contains("permission") || errorMessage.Contains("denied"))
                return LicenseReleaseResultCode.PermissionDenied;
            if (errorMessage.Contains("timeout"))
                return LicenseReleaseResultCode.Timeout;

            return exitCode switch
            {
                -1 => LicenseReleaseResultCode.Timeout,
                1 => LicenseReleaseResultCode.ServerError,
                2 => LicenseReleaseResultCode.InvalidParameters,
                _ => LicenseReleaseResultCode.UnknownError
            };
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _circuitBreaker?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Custom exception for license manager operations
    /// </summary>
    public class LicenseManagerException : Exception
    {
        /// <summary>
        /// Gets the error code associated with the exception
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseManagerException class
        /// </summary>
        /// <param name="message">Exception message</param>
        public LicenseManagerException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseManagerException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="errorCode">Error code</param>
        public LicenseManagerException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseManagerException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public LicenseManagerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseManagerException class
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="errorCode">Error code</param>
        /// <param name="innerException">Inner exception</param>
        public LicenseManagerException(string message, int errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}