using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Models;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Strategies
{
    /// <summary>
    /// License release strategy that releases licenses when users are idle for a configured duration
    /// </summary>
    public class IdleTimeReleaseStrategy : ILicenseReleaseStrategy
    {
        private readonly ILogger<IdleTimeReleaseStrategy> _logger;
        private readonly IIdleDetector _idleDetector;
        private readonly Lazy<ILicenseManager> _licenseManager;
        private readonly IdleTimeReleaseStrategyConfig _config;

        /// <summary>
        /// Gets the name of the strategy
        /// </summary>
        public string Name => "IdleTimeReleaseStrategy";

        /// <summary>
        /// Gets the description of the strategy
        /// </summary>
        public string Description => "Releases licenses when users are idle for a configured duration";

        /// <summary>
        /// Gets the priority of the strategy (higher values = higher priority)
        /// </summary>
        public int Priority => 10;

        /// <summary>
        /// Initializes a new instance of the IdleTimeReleaseStrategy class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="idleDetector">The idle detection service</param>
        /// <param name="licenseManager">The license manager (lazy-loaded to prevent circular dependencies)</param>
        /// <param name="config">The strategy configuration</param>
        public IdleTimeReleaseStrategy(
            ILogger<IdleTimeReleaseStrategy> logger,
            IIdleDetector idleDetector,
            Lazy<ILicenseManager> licenseManager,
            IdleTimeReleaseStrategyConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idleDetector = idleDetector ?? throw new ArgumentNullException(nameof(idleDetector));
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Determines whether this strategy can handle the given release request
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>True if this strategy can handle the request</returns>
        public bool CanHandle(ReleaseRequest request)
        {
            if (request == null)
                return false;

            // Check if strategy is enabled
            if (!_config.IsEnabled)
                return false;

            // Check if the request specifies this strategy or allows automatic strategy selection
            if (!string.IsNullOrEmpty(request.Strategy) &&
                !request.Strategy.Equals(Name, StringComparison.OrdinalIgnoreCase))
                return false;

            // Check if user is specified (required for idle detection)
            if (string.IsNullOrWhiteSpace(request.User))
                return false;

            // Check if host is specified (helps with idle detection)
            if (string.IsNullOrWhiteSpace(request.Host))
            {
                _logger.LogDebug("Cannot handle request {RequestId}: Host not specified for idle detection", request.RequestId);
                return false;
            }

            _logger.LogDebug("Strategy {StrategyName} can handle request {RequestId} for user {User}",
                Name, request.RequestId, request.User);

            return true;
        }

        /// <summary>
        /// Executes the license release strategy
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License release result</returns>
        public async Task<LicenseReleaseResult> ExecuteAsync(ReleaseRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Executing {StrategyName} for request {RequestId}, user {User}, feature {Feature}",
                Name, request.RequestId, request.User, request.Feature);

            var startTime = DateTime.UtcNow;

            try
            {
                // Step 1: Find processes associated with the user
                var userProcesses = await FindUserProcessesAsync(request, cancellationToken);
                if (userProcesses.Count == 0)
                {
                    _logger.LogWarning("No processes found for user {User} on host {Host}", request.User, request.Host);
                    return LicenseReleaseResult.CreateFailure(
                        request.Server, request.Port, request.Feature, request.User,
                        $"No processes found for user {request.User} on host {request.Host}",
                        LicenseReleaseResultCode.UserNotFound,
                        DateTime.UtcNow - startTime);
                }

                // Step 2: Check idle status for all user processes
                var idleResults = await CheckProcessesIdleStatusAsync(userProcesses, cancellationToken);
                var idleProcesses = idleResults.Where(r => r.IsIdle).ToList();

                if (idleProcesses.Count == 0)
                {
                    _logger.LogInformation("No idle processes found for user {User} on host {Host}", request.User, request.Host);
                    return LicenseReleaseResult.CreateFailure(
                        request.Server, request.Port, request.Feature, request.User,
                        $"User {request.User} is not idle on host {request.Host}",
                        LicenseReleaseResultCode.UserNotFound,
                        DateTime.UtcNow - startTime);
                }

                // Step 3: Check if idle duration exceeds threshold
                var longestIdleTime = idleProcesses.Max(p => p.IdleTime);
                if (longestIdleTime < TimeSpan.FromSeconds(_config.IdleThresholdSeconds))
                {
                    _logger.LogInformation("User {User} idle time {IdleTime} is below threshold {Threshold}",
                        request.User, longestIdleTime, TimeSpan.FromSeconds(_config.IdleThresholdSeconds));
                    return LicenseReleaseResult.CreateFailure(
                        request.Server, request.Port, request.Feature, request.User,
                        $"User {request.User} idle time {longestIdleTime.TotalMinutes:F1} minutes is below threshold {_config.IdleThresholdSeconds} seconds",
                        LicenseReleaseResultCode.UserNotFound,
                        DateTime.UtcNow - startTime);
                }

                // Step 4: Check confidence level
                var avgConfidence = idleProcesses.Average(p => p.Confidence);
                if (avgConfidence < _config.ConfidenceThreshold)
                {
                    _logger.LogWarning("Idle detection confidence {Confidence} is below threshold {Threshold}",
                        avgConfidence, _config.ConfidenceThreshold);
                    return LicenseReleaseResult.CreateFailure(
                        request.Server, request.Port, request.Feature, request.User,
                        $"Idle detection confidence {avgConfidence:F2} is below threshold {_config.ConfidenceThreshold}",
                        LicenseReleaseResultCode.UnknownError,
                        DateTime.UtcNow - startTime);
                }

                // Step 5: Perform safety validation if not forced
                if (!request.ForceRelease)
                {
                    var safetyResult = await PerformSafetyValidationAsync(request, idleProcesses, cancellationToken);
                    if (!safetyResult.IsValid)
                    {
                        _logger.LogWarning("Safety validation failed for request {RequestId}: {Reason}",
                            request.RequestId, safetyResult.ErrorMessage);
                        return LicenseReleaseResult.CreateFailure(
                            request.Server, request.Port, request.Feature, request.User,
                            safetyResult.ErrorMessage,
                            LicenseReleaseResultCode.PermissionDenied,
                            DateTime.UtcNow - startTime);
                    }
                }

                // Step 6: Execute license release
                _logger.LogInformation("Releasing license for user {User} who has been idle for {IdleTime}",
                    request.User, longestIdleTime);

                var releaseResult = await _licenseManager.Value.ReleaseLicenseAsync(
                    request.Server, request.Port, request.Feature, request.User, cancellationToken);

                if (releaseResult.Success)
                {
                    _logger.LogInformation("Successfully released license for user {User} after {IdleTime} of idle time",
                        request.User, longestIdleTime);
                }
                else
                {
                    _logger.LogError("Failed to release license for user {User}: {Error}",
                        request.User, releaseResult.ErrorMessage);
                }

                // Add strategy-specific metadata
                releaseResult.Metadata = new
                {
                    Strategy = Name,
                    IdleTime = longestIdleTime,
                    Confidence = avgConfidence,
                    IdleProcesses = idleProcesses.Count,
                    TotalProcesses = userProcesses.Count,
                    DetectionMethods = idleProcesses.SelectMany(p => p.DetectionMethod).Distinct().ToList()
                };

                return releaseResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing {StrategyName} for request {RequestId}", Name, request.RequestId);
                return LicenseReleaseResult.CreateFailure(
                    request.Server, request.Port, request.Feature, request.User,
                    $"Strategy execution failed: {ex.Message}",
                    LicenseReleaseResultCode.UnknownError,
                    DateTime.UtcNow - startTime);
            }
        }

        /// <summary>
        /// Validates the release request before execution
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>Validation result indicating whether the request is valid</returns>
        public StrategyValidationResult Validate(ReleaseRequest request)
        {
            if (request == null)
                return StrategyValidationResult.Failure("Request cannot be null");

            if (!_config.IsEnabled)
                return StrategyValidationResult.Failure("Idle time release strategy is disabled");

            if (string.IsNullOrWhiteSpace(request.User))
                return StrategyValidationResult.Failure("User name is required for idle time release strategy");

            if (string.IsNullOrWhiteSpace(request.Host))
                return StrategyValidationResult.Failure("Host name is required for idle time release strategy");

            if (_config.IdleThresholdSeconds <= 0)
                return StrategyValidationResult.Failure("Idle threshold must be greater than 0");

            if (_config.ConfidenceThreshold < 0 || _config.ConfidenceThreshold > 1)
                return StrategyValidationResult.Failure("Confidence threshold must be between 0 and 1");

            return StrategyValidationResult.Success();
        }

        /// <summary>
        /// Gets the health status of the strategy
        /// </summary>
        /// <returns>Health status information</returns>
        public StrategyHealthStatus GetHealthStatus()
        {
            try
            {
                // Check if idle detector is available and healthy
                if (_idleDetector == null)
                {
                    return StrategyHealthStatus.Unhealthy("Idle detector is not available");
                }

                // Check configuration
                if (!_config.IsEnabled)
                {
                    return StrategyHealthStatus.Unhealthy("Strategy is disabled");
                }

                // Check if license manager is available
                if (_licenseManager == null)
                {
                    return StrategyHealthStatus.Unhealthy("License manager is not available");
                }

                return StrategyHealthStatus.Healthy("Strategy is healthy and ready to process requests");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status for {StrategyName}", Name);
                return StrategyHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds processes associated with the user
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of user processes</returns>
        private async Task<List<ProcessInfo>> FindUserProcessesAsync(ReleaseRequest request, CancellationToken cancellationToken)
        {
            // In a real implementation, this would query the system for processes belonging to the user
            // For now, we'll create a mock process list based on the request information

            _logger.LogDebug("Finding processes for user {User} on host {Host}", request.User, request.Host);

            // This is a simplified implementation - in production, you'd use WMI or other system APIs
            var processes = new List<ProcessInfo>
            {
                new ProcessInfo(
                    GetMockProcessId(request.User),
                    "licenced_application.exe",
                    request.User,
                    request.Host
                )
            };

            _logger.LogDebug("Found {Count} processes for user {User}", processes.Count, request.User);
            return await Task.FromResult(processes);
        }

        /// <summary>
        /// Checks the idle status of processes
        /// </summary>
        /// <param name="processes">List of processes to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of idle detection results</returns>
        private async Task<List<IdleDetectionResult>> CheckProcessesIdleStatusAsync(List<ProcessInfo> processes, CancellationToken cancellationToken)
        {
            var results = new List<IdleDetectionResult>();

            foreach (var process in processes)
            {
                try
                {
                    var result = await _idleDetector.DetectIdleAsync(
                        process.ProcessId,
                        process.UserName,
                        process.ComputerName,
                        cancellationToken);

                    results.Add(result);
                    _logger.LogDebug("Process {ProcessId} idle status: {IsIdle}, idle time: {IdleTime}",
                        process.ProcessId, result.IsIdle, result.IdleTime);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check idle status for process {ProcessId}", process.ProcessId);

                    // Add a failed result
                    results.Add(new IdleDetectionResult
                    {
                        ProcessId = process.ProcessId,
                        UserName = process.UserName,
                        ComputerName = process.ComputerName,
                        IsIdle = false,
                        Confidence = 0.0,
                        DetectionMethod = "Error",
                        Reason = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Performs safety validation before releasing the license
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="idleProcesses">List of idle processes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Safety validation result</returns>
        private async Task<StrategyValidationResult> PerformSafetyValidationAsync(ReleaseRequest request, List<IdleDetectionResult> idleProcesses, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Performing safety validation for request {RequestId}", request.RequestId);

            // Check if the user has critical work that shouldn't be interrupted
            if (_config.PreventReleaseDuringCriticalWork)
            {
                var hasCriticalWork = await CheckForCriticalWorkAsync(request, cancellationToken);
                if (hasCriticalWork)
                {
                    return StrategyValidationResult.Failure("User has critical work in progress");
                }
            }

            // Check if it's within restricted hours
            if (_config.RestrictedHours.Any())
            {
                var currentTime = DateTime.Now.TimeOfDay;
                var isRestricted = _config.RestrictedHours.Any(range =>
                    currentTime >= range.Start && currentTime <= range.End);

                if (isRestricted)
                {
                    return StrategyValidationResult.Failure("License release is restricted during this time period");
                }
            }

            // Check minimum session duration
            if (_config.MinimumSessionDuration > TimeSpan.Zero)
            {
                // This would require tracking session start times
                // For now, we'll skip this check
            }

            _logger.LogDebug("Safety validation passed for request {RequestId}", request.RequestId);
            return StrategyValidationResult.Success();
        }

        /// <summary>
        /// Checks if the user has critical work in progress
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if critical work is detected</returns>
        private async Task<bool> CheckForCriticalWorkAsync(ReleaseRequest request, CancellationToken cancellationToken)
        {
            // In a real implementation, this would check for:
            // - Active file saves in progress
            // - Active network transfers
            // - Critical applications running
            // - User-defined "do not disturb" status

            // For now, return false (no critical work)
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Generates a mock process ID for testing purposes
        /// </summary>
        /// <param name="userName">The user name</param>
        /// <returns>Mock process ID</returns>
        private int GetMockProcessId(string userName)
        {
            // Simple hash-based mock process ID generation
            var hash = userName.GetHashCode();
            return Math.Abs(hash % 9000) + 1000; // Return between 1000 and 9999
        }
    }

    /// <summary>
    /// Configuration for the IdleTimeReleaseStrategy
    /// </summary>
    public class IdleTimeReleaseStrategyConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether the strategy is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the idle threshold in seconds
        /// </summary>
        public int IdleThresholdSeconds { get; set; } = 1800; // 30 minutes

        /// <summary>
        /// Gets or sets the confidence threshold (0.0 to 1.0)
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.8;

        /// <summary>
        /// Gets or sets a value indicating whether to prevent release during critical work
        /// </summary>
        public bool PreventReleaseDuringCriticalWork { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum session duration before release is allowed
        /// </summary>
        public TimeSpan MinimumSessionDuration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets or sets the restricted hours when license release is not allowed
        /// </summary>
        public List<TimeRange> RestrictedHours { get; set; } = new List<TimeRange>();

        /// <summary>
        /// Gets or sets the maximum number of concurrent release operations
        /// </summary>
        public int MaxConcurrentReleases { get; set; } = 3;

        /// <summary>
        /// Gets or sets the timeout for release operations in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets custom configuration parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a time range for restricted hours
    /// </summary>
    public class TimeRange
    {
        /// <summary>
        /// Gets or sets the start time of the range
        /// </summary>
        public TimeSpan Start { get; set; }

        /// <summary>
        /// Gets or sets the end time of the range
        /// </summary>
        public TimeSpan End { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimeRange class
        /// </summary>
        /// <param name="start">Start time</param>
        /// <param name="end">End time</param>
        public TimeRange(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
        }
    }
}