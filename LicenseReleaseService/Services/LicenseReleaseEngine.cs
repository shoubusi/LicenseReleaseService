using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.Models;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Services
{
    /// <summary>
    /// Core engine for license release operations with multi-factor decision making
    /// </summary>
    public class LicenseReleaseEngine : IDisposable
    {
        private readonly ILogger<LicenseReleaseEngine> _logger;
        private readonly ILicenseManager _licenseManager;
        private readonly ServiceSettings _serviceSettings;
        private readonly IEnumerable<ILicenseReleaseStrategy> _strategies;
        private readonly object _syncLock = new object();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the LicenseReleaseEngine class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="licenseManager">The license manager instance</param>
        /// <param name="serviceSettings">The service settings</param>
        /// <param name="strategies">The collection of release strategies</param>
        public LicenseReleaseEngine(
            ILogger<LicenseReleaseEngine> logger,
            ILicenseManager licenseManager,
            ServiceSettings serviceSettings,
            IEnumerable<ILicenseReleaseStrategy> strategies)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _serviceSettings = serviceSettings ?? throw new ArgumentNullException(nameof(serviceSettings));
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));

            _logger.LogInformation("LicenseReleaseEngine initialized with {StrategyCount} strategies", _strategies.Count());
        }

        /// <summary>
        /// Executes a license release request using multi-factor decision making
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License release result</returns>
        public async Task<LicenseReleaseResult> ExecuteReleaseAsync(ReleaseRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var startTime = DateTime.Now;
            var operationId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                _logger.LogInformation("Starting license release operation {OperationId} for {Feature} by {User} on {Server}:{Port}",
                    operationId, request.Feature, request.User, request.Server, request.Port);

                // Stage 1: Request validation
                var validationResult = await ValidateRequestAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Request validation failed for {OperationId}: {ErrorMessage}",
                        operationId, validationResult.ErrorMessage);
                    return CreateFailureResult(request, validationResult.ErrorMessage, LicenseReleaseResultCode.InvalidParameters, startTime);
                }

                // Stage 2: Dry run check (if requested)
                if (request.DryRun)
                {
                    _logger.LogInformation("Performing dry run for operation {OperationId}", operationId);
                    var dryRunResult = await PerformDryRunAsync(request, cancellationToken);
                    return dryRunResult;
                }

                // Stage 3: Strategy selection and execution
                var strategy = SelectStrategy(request);
                if (strategy == null)
                {
                    _logger.LogWarning("No suitable strategy found for request {OperationId}", operationId);
                    return CreateFailureResult(request, "No suitable release strategy found", LicenseReleaseResultCode.InvalidParameters, startTime);
                }

                _logger.LogInformation("Selected strategy {StrategyName} for operation {OperationId}", strategy.Name, operationId);

                // Stage 4: Pre-release verification
                var verificationResult = await PerformPreReleaseVerificationAsync(request, strategy, cancellationToken);
                if (!verificationResult.IsSuccess)
                {
                    _logger.LogWarning("Pre-release verification failed for {OperationId}: {ErrorMessage}",
                        operationId, verificationResult.ErrorMessage);
                    return CreateFailureResult(request, verificationResult.ErrorMessage, verificationResult.ResultCode, startTime);
                }

                // Stage 5: Execute release with retries
                var releaseResult = await ExecuteReleaseWithRetriesAsync(request, strategy, cancellationToken);

                // Stage 6: Post-release verification
                if (releaseResult.Success)
                {
                    var postVerificationResult = await PerformPostReleaseVerificationAsync(request, releaseResult, cancellationToken);
                    if (!postVerificationResult.IsSuccess)
                    {
                        _logger.LogWarning("Post-release verification failed for {OperationId}: {ErrorMessage}",
                            operationId, postVerificationResult.ErrorMessage);
                        releaseResult.Success = false;
                        releaseResult.ErrorMessage = postVerificationResult.ErrorMessage;
                        releaseResult.ResultCode = postVerificationResult.ResultCode;
                    }
                }

                var executionTime = DateTime.Now - startTime;
                releaseResult.ExecutionTime = executionTime;

                _logger.LogInformation("License release operation {OperationId} completed in {ExecutionTime}ms with result: {Success}",
                    operationId, executionTime.TotalMilliseconds, releaseResult.Success);

                // Stage 7: Audit logging
                await LogAuditTrailAsync(request, releaseResult, operationId, executionTime);

                return releaseResult;
            }
            catch (Exception ex)
            {
                var executionTime = DateTime.Now - startTime;
                _logger.LogError(ex, "License release operation {OperationId} failed after {ExecutionTime}ms",
                    operationId, executionTime.TotalMilliseconds);

                return CreateFailureResult(request, ex.Message, LicenseReleaseResultCode.UnknownError, startTime);
            }
        }

        /// <summary>
        /// Validates a release request
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        private async Task<RequestValidationResult> ValidateRequestAsync(ReleaseRequest request, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            // Basic validation
            if (!request.IsValid)
            {
                errors.Add("Required fields (Server, Port, Feature, User) must be provided");
            }

            // Server connectivity validation
            if (request.ValidationFlags.HasFlag(ReleaseValidationFlags.ValidateServer))
            {
                try
                {
                    var isServerAvailable = await _licenseManager.IsServerAvailableAsync(request.Server, request.Port, cancellationToken);
                    if (!isServerAvailable)
                    {
                        errors.Add($"License server {request.Server}:{request.Port} is not available");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to validate server connectivity: {ex.Message}");
                }
            }

            // Feature validation
            if (request.ValidationFlags.HasFlag(ReleaseValidationFlags.ValidateFeature))
            {
                try
                {
                    var featureInfo = await _licenseManager.GetFeatureInfoAsync(request.Server, request.Port, request.Feature, cancellationToken);
                    if (featureInfo == null)
                    {
                        errors.Add($"Feature '{request.Feature}' not found on server {request.Server}:{request.Port}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to validate feature: {ex.Message}");
                }
            }

            // User validation
            if (request.ValidationFlags.HasFlag(ReleaseValidationFlags.ValidateUser))
            {
                try
                {
                    var users = await _licenseManager.GetUsersAsync(request.Server, request.Port, cancellationToken);
                    if (!users.ContainsKey(request.User))
                    {
                        errors.Add($"User '{request.User}' not found using licenses on server {request.Server}:{request.Port}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to validate user: {ex.Message}");
                }
            }

            return errors.Count == 0
                ? RequestValidationResult.Success()
                : RequestValidationResult.Failure(string.Join("; ", errors));
        }

        /// <summary>
        /// Performs a dry run of the release operation
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dry run result</returns>
        private async Task<LicenseReleaseResult> PerformDryRunAsync(ReleaseRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Dry run validation for {Feature} by {User} on {Server}:{Port}",
                request.Feature, request.User, request.Server, request.Port);

            var validationResult = await ValidateRequestAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return CreateFailureResult(request, validationResult.ErrorMessage, LicenseReleaseResultCode.InvalidParameters, DateTime.Now);
            }

            var strategy = SelectStrategy(request);
            if (strategy == null)
            {
                return CreateFailureResult(request, "No suitable release strategy found", LicenseReleaseResultCode.InvalidParameters, DateTime.Now);
            }

            var strategyValidation = strategy.Validate(request);
            if (!strategyValidation.IsValid)
            {
                return CreateFailureResult(request, strategyValidation.ErrorMessage, LicenseReleaseResultCode.InvalidParameters, DateTime.Now);
            }

            return LicenseReleaseResult.CreateSuccess(request.Server, request.Port, request.Feature, request.User, 0, TimeSpan.Zero);
        }

        /// <summary>
        /// Selects the best strategy for the given request
        /// </summary>
        /// <param name="request">The release request</param>
        /// <returns>Selected strategy or null if none suitable</returns>
        private ILicenseReleaseStrategy SelectStrategy(ReleaseRequest request)
        {
            if (!string.IsNullOrEmpty(request.Strategy))
            {
                var specificStrategy = _strategies.FirstOrDefault(s =>
                    s.Name.Equals(request.Strategy, StringComparison.OrdinalIgnoreCase));
                if (specificStrategy != null && specificStrategy.CanHandle(request))
                {
                    return specificStrategy;
                }
            }

            // Select based on priority and capability
            var suitableStrategies = _strategies
                .Where(s => s.CanHandle(request))
                .OrderByDescending(s => s.Priority)
                .ToList();

            return suitableStrategies.FirstOrDefault();
        }

        /// <summary>
        /// Performs pre-release verification
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="strategy">The selected strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Verification result</returns>
        private async Task<VerificationResult> PerformPreReleaseVerificationAsync(ReleaseRequest request, ILicenseReleaseStrategy strategy, CancellationToken cancellationToken)
        {
            // Check strategy health
            var strategyHealth = strategy.GetHealthStatus();
            if (!strategyHealth.IsHealthy)
            {
                return VerificationResult.Failure($"Strategy {strategy.Name} is not healthy: {strategyHealth.StatusMessage}");
            }

            // Validate strategy can handle the request
            var strategyValidation = strategy.Validate(request);
            if (!strategyValidation.IsValid)
            {
                return VerificationResult.Failure(strategyValidation.ErrorMessage);
            }

            // Safety validation (unless forced)
            if (!request.ForceRelease && request.ValidationFlags.HasFlag(ReleaseValidationFlags.ValidateSafety))
            {
                var safetyResult = await ValidateSafetyConstraintsAsync(request, cancellationToken);
                if (!safetyResult.IsSuccess)
                {
                    return safetyResult;
                }
            }

            return VerificationResult.Success();
        }

        /// <summary>
        /// Validates safety constraints for license release
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Safety validation result</returns>
        private async Task<VerificationResult> ValidateSafetyConstraintsAsync(ReleaseRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var featureInfo = await _licenseManager.GetFeatureInfoAsync(request.Server, request.Port, request.Feature, cancellationToken);
                if (featureInfo == null)
                {
                    return VerificationResult.Failure($"Feature '{request.Feature}' not found", LicenseReleaseResultCode.FeatureNotFound);
                }

                // Check if user is actually using the license
                var users = await _licenseManager.GetUsersAsync(request.Server, request.Port, cancellationToken);
                if (!users.ContainsKey(request.User))
                {
                    return VerificationResult.Failure($"User '{request.User}' is not using any licenses", LicenseReleaseResultCode.UserNotFound);
                }

                // Additional safety checks can be added here based on configuration
                var safetyConfig = _serviceSettings.SafetyValidation;
                if (safetyConfig?.PreventReleaseDuringHighUsage == true)
                {
                    var utilizationPercentage = featureInfo.UtilizationPercentage;
                    if (utilizationPercentage > safetyConfig.HighUsageThreshold)
                    {
                        return VerificationResult.Failure(
                            $"Cannot release license during high usage ({utilizationPercentage:F1}% > {safetyConfig.HighUsageThreshold}%)",
                            LicenseReleaseResultCode.PermissionDenied);
                    }
                }

                return VerificationResult.Success();
            }
            catch (Exception ex)
            {
                return VerificationResult.Failure($"Safety validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes license release with retry logic
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="strategy">The selected strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License release result</returns>
        private async Task<LicenseReleaseResult> ExecuteReleaseWithRetriesAsync(ReleaseRequest request, ILicenseReleaseStrategy strategy, CancellationToken cancellationToken)
        {
            var maxRetries = request.MaxRetries > 0 ? request.MaxRetries : 1;
            var baseDelay = TimeSpan.FromSeconds(1);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Executing release attempt {Attempt}/{MaxAttempts} for {Feature} by {User}",
                        attempt, maxRetries, request.Feature, request.User);

                    var result = await strategy.ExecuteAsync(request, cancellationToken);
                    result.RetryAttempt = attempt - 1;

                    if (result.Success || IsTransientError(result.ResultCode))
                    {
                        return result;
                    }

                    _logger.LogWarning("Release attempt {Attempt} failed for {Feature} by {User}: {ErrorMessage}",
                        attempt, request.Feature, request.User, result.ErrorMessage);

                    if (attempt < maxRetries)
                    {
                        var delay = baseDelay * Math.Pow(2, attempt - 1); // Exponential backoff
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Release attempt {Attempt} failed for {Feature} by {User}, retrying...",
                        attempt, request.Feature, request.User);

                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            return CreateFailureResult(request, "All release attempts failed", LicenseReleaseResultCode.UnknownError, DateTime.Now);
        }

        /// <summary>
        /// Performs post-release verification
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="releaseResult">The release result</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Verification result</returns>
        private async Task<VerificationResult> PerformPostReleaseVerificationAsync(ReleaseRequest request, LicenseReleaseResult releaseResult, CancellationToken cancellationToken)
        {
            try
            {
                // Verify the license was actually released
                var users = await _licenseManager.GetUsersAsync(request.Server, request.Port, cancellationToken);
                if (users.ContainsKey(request.User))
                {
                    // Check if the user is still using the specific feature
                    var userUsage = users[request.User];
                    if (userUsage.FeatureUsages?.Any(f => f.FeatureName.Equals(request.Feature, StringComparison.OrdinalIgnoreCase)) ?? false)
                    {
                        return VerificationResult.Failure("License was not successfully released - user still has the feature checked out");
                    }
                }

                return VerificationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Post-release verification failed for {Feature} by {User}: {ErrorMessage}",
                    request.Feature, request.User, ex.Message);
                return VerificationResult.Failure($"Post-release verification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs audit trail for the release operation
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="result">The release result</param>
        /// <param name="operationId">The operation identifier</param>
        /// <param name="executionTime">The execution time</param>
        private async Task LogAuditTrailAsync(ReleaseRequest request, LicenseReleaseResult result, string operationId, TimeSpan executionTime)
        {
            try
            {
                var auditEvent = new
                {
                    OperationId = operationId,
                    Timestamp = DateTime.Now,
                    ExecutionTimeMs = executionTime.TotalMilliseconds,
                    Request = new
                    {
                        request.RequestId,
                        request.Server,
                        request.Port,
                        request.Feature,
                        request.User,
                        request.Priority,
                        request.Source,
                        request.Initiator,
                        request.ForceRelease,
                        request.DryRun
                    },
                    Result = new
                    {
                        result.Success,
                        result.ErrorMessage,
                        result.ResultCode,
                        result.LicensesReleased,
                        result.RetryAttempt,
                        result.TimedOut,
                        result.Cancelled
                    }
                };

                _logger.LogInformation("License release audit: {AuditEvent}", System.Text.Json.JsonSerializer.Serialize(auditEvent));

                // Additional audit logging can be added here (e.g., to database, external monitoring, etc.)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit trail for operation {OperationId}", operationId);
            }
        }

        /// <summary>
        /// Determines if an error code represents a transient error
        /// </summary>
        /// <param name="resultCode">The result code</param>
        /// <returns>True if the error is transient</returns>
        private bool IsTransientError(LicenseReleaseResultCode resultCode)
        {
            return resultCode switch
            {
                LicenseReleaseResultCode.ServerUnavailable => true,
                LicenseReleaseResultCode.NetworkError => true,
                LicenseReleaseResultCode.Timeout => true,
                LicenseReleaseResultCode.UnknownError => true,
                _ => false
            };
        }

        /// <summary>
        /// Creates a failure result
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="resultCode">Result code</param>
        /// <param name="startTime">Start time</param>
        /// <returns>Failure result</returns>
        private LicenseReleaseResult CreateFailureResult(ReleaseRequest request, string errorMessage, LicenseReleaseResultCode resultCode, DateTime startTime)
        {
            var executionTime = DateTime.Now - startTime;
            return LicenseReleaseResult.CreateFailure(request.Server, request.Port, request.Feature, request.User,
                errorMessage, resultCode, executionTime);
        }

        /// <summary>
        /// Gets the health status of the release engine
        /// </summary>
        /// <returns>Health status information</returns>
        public EngineHealthStatus GetHealthStatus()
        {
            var strategyHealth = _strategies.Select(s => new
            {
                StrategyName = s.Name,
                Health = s.GetHealthStatus()
            }).ToList();

            return new EngineHealthStatus
            {
                IsHealthy = strategyHealth.All(sh => sh.Health.IsHealthy),
                TotalStrategies = _strategies.Count(),
                HealthyStrategies = strategyHealth.Count(sh => sh.Health.IsHealthy),
                StrategyHealth = strategyHealth.ToDictionary(sh => sh.StrategyName, sh => sh.Health),
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Disposes the engine and its resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_syncLock)
                {
                    if (!_disposed)
                    {
                        _logger.LogInformation("Disposing LicenseReleaseEngine");
                        (_licenseManager as IDisposable)?.Dispose();
                        _disposed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents the result of request validation
    /// </summary>
    public class RequestValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether validation was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <returns>Successful validation result</returns>
        public static RequestValidationResult Success() => new RequestValidationResult { IsValid = true };

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Failed validation result</returns>
        public static RequestValidationResult Failure(string errorMessage)
        {
            return new RequestValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Represents the result of verification
    /// </summary>
    public class VerificationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether verification was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the error message if verification failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the result code if verification failed
        /// </summary>
        public LicenseReleaseResultCode ResultCode { get; set; } = LicenseReleaseResultCode.UnknownError;

        /// <summary>
        /// Creates a successful verification result
        /// </summary>
        /// <returns>Successful verification result</returns>
        public static VerificationResult Success() => new VerificationResult { IsSuccess = true };

        /// <summary>
        /// Creates a failed verification result
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="resultCode">Result code</param>
        /// <returns>Failed verification result</returns>
        public static VerificationResult Failure(string errorMessage, LicenseReleaseResultCode resultCode = LicenseReleaseResultCode.UnknownError)
        {
            return new VerificationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ResultCode = resultCode
            };
        }
    }

    /// <summary>
    /// Represents the health status of the release engine
    /// </summary>
    public class EngineHealthStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether the engine is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the total number of strategies
        /// </summary>
        public int TotalStrategies { get; set; }

        /// <summary>
        /// Gets or sets the number of healthy strategies
        /// </summary>
        public int HealthyStrategies { get; set; }

        /// <summary>
        /// Gets or sets the health status of individual strategies
        /// </summary>
        public Dictionary<string, StrategyHealthStatus> StrategyHealth { get; set; } = new Dictionary<string, StrategyHealthStatus>();

        /// <summary>
        /// Gets or sets the timestamp when the health status was last updated
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}