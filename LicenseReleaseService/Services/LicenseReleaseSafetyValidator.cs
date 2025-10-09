using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.Models;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Services
{
    /// <summary>
    /// Implements safety validation for license release operations
    /// </summary>
    public class LicenseReleaseSafetyValidator : ISafetyValidator
    {
        private readonly ILogger<LicenseReleaseSafetyValidator> _logger;
        private readonly IProcessExecutor _processExecutor;
        private SafetyValidationConfiguration _configuration;
        private bool _isInitialized;
        private bool _isMonitoring;
        private CancellationTokenSource _monitoringCts;
        private readonly Dictionary<string, Func<ValidationContext, Task<SafetyCheckResult>>> _customValidationRules;
        private readonly SafetyValidationStatistics _statistics;
        private readonly object _lock = new object();
        private bool _isDisposed;

        // Windows API for user activity detection
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        /// <summary>
        /// Event raised when safety validation is completed
        /// </summary>
        public event EventHandler<SafetyValidationCompletedEventArgs> SafetyValidationCompleted;

        /// <summary>
        /// Event raised when a safety check fails
        /// </summary>
        public event EventHandler<SafetyCheckFailedEventArgs> SafetyCheckFailed;

        /// <summary>
        /// Event raised when user activity is detected
        /// </summary>
        public event EventHandler<UserActivityEventArgs> UserActivityDetected;

        /// <summary>
        /// Gets the safety validation configuration
        /// </summary>
        public SafetyValidationConfiguration Configuration
        {
            get => _configuration;
            private set => _configuration = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets a value indicating whether the validator is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes a new instance of the LicenseReleaseSafetyValidator class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="processExecutor">The process executor instance</param>
        public LicenseReleaseSafetyValidator(
            ILogger<LicenseReleaseSafetyValidator> logger,
            IProcessExecutor processExecutor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _customValidationRules = new Dictionary<string, Func<ValidationContext, Task<SafetyCheckResult>>>();
            _statistics = new SafetyValidationStatistics();
            _monitoringCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Initializes the safety validator
        /// </summary>
        /// <param name="configuration">Safety validation configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Initialization result</returns>
        public async Task<InitializationResult> InitializeAsync(SafetyValidationConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing LicenseReleaseSafetyValidator");

                var validationResult = configuration.Validate();
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors);
                    _logger.LogError("Configuration validation failed: {Errors}", errors);
                    return new InitializationResult
                    {
                        Success = false,
                        Message = "Configuration validation failed",
                        Errors = validationResult.Errors
                    };
                }

                Configuration = configuration;
                _isInitialized = true;

                _logger.LogInformation("LicenseReleaseSafetyValidator initialized successfully");
                return new InitializationResult
                {
                    Success = true,
                    Message = "Safety validator initialized successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize safety validator");
                return new InitializationResult
                {
                    Success = false,
                    Message = "Initialization failed",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// Validates the safety of a license release operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Safety validation result</returns>
        public async Task<SafetyValidationResult> ValidateLicenseReleaseSafetyAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Safety validator is not initialized");
            }

            var startTime = DateTime.UtcNow;
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogInformation("Validating license release safety for {Feature} from user {User} on {Server}:{Port}", feature, user, server, port);

                var context = new ValidationContext
                {
                    OperationType = "LicenseRelease",
                    Server = server,
                    Port = port,
                    Feature = feature,
                    UserName = user
                };

                // Perform comprehensive validation
                result = await ValidateComprehensiveSafetyAsync(context, cancellationToken);

                // Update statistics
                UpdateStatistics(result, DateTime.UtcNow - startTime);

                // Raise completion event
                SafetyValidationCompleted?.Invoke(this, new SafetyValidationCompletedEventArgs(result, context, DateTime.UtcNow - startTime));

                _logger.LogInformation("License release safety validation completed: {Status}, {Action}", result.Status, result.RecommendedAction);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during license release safety validation");
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"Validation error: {ex.Message}");
                result.FailureReason = ex.Message;

                return result;
            }
        }

        /// <summary>
        /// Validates process accessibility for license release
        /// </summary>
        /// <param name="processName">Process name to validate</param>
        /// <param name="userName">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process accessibility validation result</returns>
        public async Task<SafetyValidationResult> ValidateProcessAccessibilityAsync(string processName, string userName, CancellationToken cancellationToken = default)
        {
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogDebug("Validating process accessibility for {ProcessName} by user {UserName}", processName, userName);

                // Check if process is protected
                if (Configuration.ProtectedProcesses.Contains(processName.ToLower()))
                {
                    result.AddValidationCheck("ProtectedProcessCheck", false, $"Process {processName} is protected and cannot be interrupted", null);
                    result.Status = SafetyStatus.Unsafe;
                    result.RecommendedAction = SafetyValidationAction.Block;
                    result.FailureReason = $"Protected process: {processName}";
                    return result;
                }

                // Check if process is currently running
                var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    result.AddValidationCheck("ProcessRunningCheck", false, $"Process {processName} is not running", null);
                    result.Status = SafetyStatus.Warning;
                    result.RecommendedAction = SafetyValidationAction.Warn;
                    result.AddWarning($"Process {processName} is not currently running");
                    return result;
                }

                // Check process accessibility (permissions)
                var isAccessible = await CheckProcessAccessibilityAsync(processes[0], userName, cancellationToken);
                result.AddValidationCheck("ProcessAccessibilityCheck", isAccessible,
                    isAccessible ? "Process is accessible" : "Process access denied", null);

                // Check if process is critical application
                var isCritical = Configuration.CriticalApplications.Contains(processName.ToLower());
                if (isCritical)
                {
                    result.AddValidationCheck("CriticalApplicationCheck", true, $"Process {processName} is a critical application", null);
                    result.Status = SafetyStatus.Warning;
                    result.RecommendedAction = SafetyValidationAction.RequireConfirmation;
                    result.AddWarning($"Process {processName} is a critical application and requires confirmation");
                }

                result.UpdateOverallStatus();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating process accessibility for {ProcessName}", processName);
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"Process accessibility validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates user activity for safe license release
        /// </summary>
        /// <param name="userName">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User activity validation result</returns>
        public async Task<SafetyValidationResult> ValidateUserActivityAsync(string userName, CancellationToken cancellationToken = default)
        {
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogDebug("Validating user activity for user {UserName}", userName);

                var userActivityInfo = await GetUserActivityInfoAsync(userName, cancellationToken);
                result.UserActivity = userActivityInfo;

                // Check if user is idle for minimum required time
                var isIdleLongEnough = userActivityInfo.IdleDuration >= TimeSpan.FromMinutes(Configuration.MinimumIdleTimeMinutes);
                result.AddValidationCheck("UserIdleTimeCheck", isIdleLongEnough,
                    isIdleLongEnough ? "User idle time meets requirements" :
                    $"User idle time ({userActivityInfo.IdleDuration.TotalMinutes:F1} min) is less than required ({Configuration.MinimumIdleTimeMinutes} min)",
                    userActivityInfo.IdleDuration);

                // Check if user has any critical processes running
                var hasCriticalProcesses = userActivityInfo.ActiveProcesses?.Any(p =>
                    Configuration.CriticalApplications.Contains(p.ToLower())) ?? false;

                if (hasCriticalProcesses)
                {
                    result.AddValidationCheck("CriticalProcessesCheck", false, "User has critical processes running", userActivityInfo.ActiveProcesses);
                    result.Status = SafetyStatus.Warning;
                    result.RecommendedAction = SafetyValidationAction.RequireConfirmation;
                    result.AddWarning("User has critical processes running");
                }

                // Check user input activity
                if (userActivityInfo.InputActivity != null)
                {
                    var recentActivity = userActivityInfo.InputActivity.KeyboardActivity || userActivityInfo.InputActivity.MouseActivity;
                    result.AddValidationCheck("UserInputActivityCheck", !recentActivity,
                        recentActivity ? "Recent user input activity detected" : "No recent user input activity detected",
                        userActivityInfo.InputActivity);
                }

                result.UpdateOverallStatus();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user activity for user {UserName}", userName);
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"User activity validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates system resources for safe operation
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>System resource validation result</returns>
        public async Task<SafetyValidationResult> ValidateSystemResourcesAsync(CancellationToken cancellationToken = default)
        {
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogDebug("Validating system resources");

                var systemResources = await GetSystemResourceUsageAsync(cancellationToken);
                result.SystemResources = systemResources;

                // Check CPU usage
                var cpuUsageOk = systemResources.CpuUsagePercent <= Configuration.MaxCpuUsagePercent;
                result.AddValidationCheck("CpuUsageCheck", cpuUsageOk,
                    cpuUsageOk ? "CPU usage is within limits" :
                    $"CPU usage ({systemResources.CpuUsagePercent:F1}%) exceeds limit ({Configuration.MaxCpuUsagePercent}%)",
                    systemResources.CpuUsagePercent);

                // Check memory usage
                var memoryUsageOk = systemResources.MemoryUsagePercent <= Configuration.MaxMemoryUsagePercent;
                result.AddValidationCheck("MemoryUsageCheck", memoryUsageOk,
                    memoryUsageOk ? "Memory usage is within limits" :
                    $"Memory usage ({systemResources.MemoryUsagePercent:F1}%) exceeds limit ({Configuration.MaxMemoryUsagePercent}%)",
                    systemResources.MemoryUsagePercent);

                // Check available memory
                var availableMemoryGB = systemResources.AvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
                var availableMemoryOk = availableMemoryGB >= 1.0; // At least 1GB available
                result.AddValidationCheck("AvailableMemoryCheck", availableMemoryOk,
                    availableMemoryOk ? "Sufficient available memory" : $"Low available memory: {availableMemoryGB:F1}GB",
                    availableMemoryGB);

                // Check if system is busy
                var systemIsBusy = systemResources.CpuUsagePercent > 90 || systemResources.MemoryUsagePercent > 90;
                if (systemIsBusy)
                {
                    result.Status = SafetyStatus.Warning;
                    result.RecommendedAction = SafetyValidationAction.Warn;
                    result.AddWarning("System is busy and may not handle license release optimally");
                }

                result.UpdateOverallStatus();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating system resources");
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"System resource validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates network connectivity for license server communication
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Network connectivity validation result</returns>
        public async Task<SafetyValidationResult> ValidateNetworkConnectivityAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogDebug("Validating network connectivity to {Server}:{Port}", server, port);

                // Check if server is in allowed list
                if (Configuration.AllowedLicenseServers.Count > 0 && !Configuration.AllowedLicenseServers.Contains(server))
                {
                    result.AddValidationCheck("AllowedServerCheck", false, $"Server {server} is not in allowed servers list", null);
                    result.Status = SafetyStatus.Unsafe;
                    result.RecommendedAction = SafetyValidationAction.Block;
                    result.FailureReason = $"Server not allowed: {server}";
                    return result;
                }

                // Test network connectivity
                var pingResult = await TestNetworkConnectivityAsync(server, Configuration.NetworkConnectivityTimeoutSeconds * 1000, cancellationToken);
                result.AddValidationCheck("NetworkPingCheck", pingResult.Success,
                    pingResult.Success ? $"Network connectivity to {server} successful" : $"Network connectivity to {server} failed",
                    pingResult);

                // Test port connectivity
                var portResult = await TestPortConnectivityAsync(server, port, cancellationToken);
                result.AddValidationCheck("PortConnectivityCheck", portResult.Success,
                    portResult.Success ? $"Port {port} connectivity successful" : $"Port {port} connectivity failed",
                    portResult);

                result.UpdateOverallStatus();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating network connectivity to {Server}:{Port}", server, port);
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"Network connectivity validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates critical operations protection
        /// </summary>
        /// <param name="operation">Operation type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Critical operations validation result</returns>
        public async Task<SafetyValidationResult> ValidateCriticalOperationsAsync(string operation, CancellationToken cancellationToken = default)
        {
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogDebug("Validating critical operations protection for operation {Operation}", operation);

                // Check if operation is critical
                var isCriticalOperation = operation.Equals("LicenseRelease", StringComparison.OrdinalIgnoreCase) ||
                                        operation.Equals("ForceRelease", StringComparison.OrdinalIgnoreCase);

                if (isCriticalOperation)
                {
                    result.AddValidationCheck("CriticalOperationCheck", true, "Operation is marked as critical", null);

                    // Check if user confirmation is required
                    if (Configuration.RequireUserConfirmation)
                    {
                        result.AddValidationCheck("UserConfirmationCheck", false, "User confirmation required for critical operations", null);
                        result.Status = SafetyStatus.Warning;
                        result.RecommendedAction = SafetyValidationAction.RequireConfirmation;
                        result.AddWarning("Critical operation requires user confirmation");
                    }
                }

                // Check consecutive failures
                if (_statistics.FailedValidations >= Configuration.MaxConsecutiveFailures)
                {
                    result.AddValidationCheck("ConsecutiveFailuresCheck", false,
                        $"Too many consecutive failures ({_statistics.FailedValidations})", null);
                    result.Status = SafetyStatus.Unsafe;
                    result.RecommendedAction = SafetyValidationAction.Block;
                    result.FailureReason = $"Safety lockout: {Configuration.MaxConsecutiveFailures} consecutive failures";
                    return result;
                }

                result.UpdateOverallStatus();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating critical operations for {Operation}", operation);
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"Critical operations validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Performs a comprehensive safety validation
        /// </summary>
        /// <param name="context">Validation context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Comprehensive safety validation result</returns>
        public async Task<SafetyValidationResult> ValidateComprehensiveSafetyAsync(ValidationContext context, CancellationToken cancellationToken = default)
        {
            var result = new SafetyValidationResult();

            try
            {
                _logger.LogDebug("Performing comprehensive safety validation");

                // Validate user activity
                var userActivityResult = await ValidateUserActivityAsync(context.UserName, cancellationToken);
                MergeValidationResult(result, userActivityResult);

                // Validate system resources
                var systemResourceResult = await ValidateSystemResourcesAsync(cancellationToken);
                MergeValidationResult(result, systemResourceResult);

                // Validate network connectivity
                var networkResult = await ValidateNetworkConnectivityAsync(context.Server, context.Port, cancellationToken);
                MergeValidationResult(result, networkResult);

                // Validate critical operations
                var criticalOperationsResult = await ValidateCriticalOperationsAsync(context.OperationType, cancellationToken);
                MergeValidationResult(result, criticalOperationsResult);

                // Run custom validation rules
                foreach (var rule in _customValidationRules)
                {
                    try
                    {
                        var ruleResult = await rule.Value(context);
                        result.AddValidationCheck(rule.Key, ruleResult.Passed, ruleResult.Message, ruleResult.Details);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing custom validation rule {RuleName}", rule.Key);
                        result.AddValidationCheck(rule.Key, false, $"Custom validation rule failed: {ex.Message}", null);
                    }
                }

                // Update overall status
                result.UpdateOverallStatus();

                // Raise safety check failed event if there are critical failures
                if (result.Status == SafetyStatus.Unsafe && result.Errors.Count > 0)
                {
                    SafetyCheckFailed?.Invoke(this, new SafetyCheckFailedEventArgs(
                        "ComprehensiveValidation",
                        result.Errors.FirstOrDefault() ?? "Comprehensive validation failed",
                        result.RecommendedAction));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during comprehensive safety validation");
                result.IsValid = false;
                result.Status = SafetyStatus.Unsafe;
                result.RecommendedAction = SafetyValidationAction.Block;
                result.AddError($"Comprehensive validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Starts monitoring user activity
        /// </summary>
        /// <param name="userName">User name to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Monitoring result</returns>
        public async Task<MonitoringResult> StartUserActivityMonitoringAsync(string userName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting user activity monitoring for user {UserName}", userName);

                if (_isMonitoring)
                {
                    return new MonitoringResult
                    {
                        Success = false,
                        Message = "User activity monitoring is already active"
                    };
                }

                _isMonitoring = true;
                _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Start monitoring task
                _ = Task.Run(async () =>
                {
                    while (!_monitoringCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var activityInfo = await GetUserActivityInfoAsync(userName, _monitoringCts.Token);

                            // Raise event if activity detected
                            if (activityInfo.IsActive)
                            {
                                UserActivityDetected?.Invoke(this, new UserActivityEventArgs(userName, activityInfo));
                            }

                            await Task.Delay(TimeSpan.FromSeconds(Configuration.UserActivityCheckIntervalSeconds), _monitoringCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Monitoring stopped
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during user activity monitoring");
                            await Task.Delay(TimeSpan.FromSeconds(Configuration.UserActivityCheckIntervalSeconds), _monitoringCts.Token);
                        }
                    }
                }, _monitoringCts.Token);

                _logger.LogInformation("User activity monitoring started successfully");
                return new MonitoringResult
                {
                    Success = true,
                    Message = "User activity monitoring started",
                    MonitoringId = Guid.NewGuid().ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start user activity monitoring");
                return new MonitoringResult
                {
                    Success = false,
                    Message = $"Failed to start monitoring: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Stops monitoring user activity
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stop result</returns>
        public async Task<MonitoringResult> StopUserActivityMonitoringAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping user activity monitoring");

                if (!_isMonitoring)
                {
                    return new MonitoringResult
                    {
                        Success = false,
                        Message = "User activity monitoring is not active"
                    };
                }

                _monitoringCts.Cancel();
                _isMonitoring = false;

                _logger.LogInformation("User activity monitoring stopped successfully");
                return new MonitoringResult
                {
                    Success = true,
                    Message = "User activity monitoring stopped"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop user activity monitoring");
                return new MonitoringResult
                {
                    Success = false,
                    Message = $"Failed to stop monitoring: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets current user activity information
        /// </summary>
        /// <param name="userName">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User activity information</returns>
        public async Task<UserActivityInfo> GetUserActivityInfoAsync(string userName, CancellationToken cancellationToken = default)
        {
            try
            {
                var activityInfo = new UserActivityInfo
                {
                    ActiveProcesses = new List<string>(),
                    InputActivity = new InputDeviceActivity()
                };

                // Get last input time
                var lastInputInfo = new LASTINPUTINFO();
                lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
                if (GetLastInputInfo(ref lastInputInfo))
                {
                    var lastInputTime = DateTime.Now.AddMilliseconds(-(Environment.TickCount - lastInputInfo.dwTime));
                    activityInfo.InputActivity.LastKeyboardInput = lastInputTime;
                    activityInfo.InputActivity.LastMouseInput = lastInputTime;
                    activityInfo.LastActivityTime = lastInputTime;
                    activityInfo.IdleDuration = DateTime.Now - lastInputTime;

                    var idleThreshold = TimeSpan.FromMinutes(1); // 1 minute idle threshold
                    activityInfo.IsActive = activityInfo.IdleDuration < idleThreshold;

                    activityInfo.InputActivity.KeyboardActivity = activityInfo.IdleDuration < idleThreshold;
                    activityInfo.InputActivity.MouseActivity = activityInfo.IdleDuration < idleThreshold;
                }

                // Get running processes for the user
                try
                {
                    var processes = System.Diagnostics.Process.GetProcesses();
                    var userProcesses = processes.Where(p =>
                    {
                        try
                        {
                            return !string.IsNullOrEmpty(p.ProcessName) &&
                                   p.MainModule != null;
                        }
                        catch
                        {
                            return false;
                        }
                    }).ToList();

                    foreach (var process in userProcesses)
                    {
                        try
                        {
                            activityInfo.ActiveProcesses.Add(process.ProcessName.ToLower());
                        }
                        catch
                        {
                            // Skip processes we can't access
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting process information for user activity monitoring");
                }

                return activityInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activity information for user {UserName}", userName);
                throw;
            }
        }

        /// <summary>
        /// Gets the list of protected processes
        /// </summary>
        /// <returns>List of protected process names</returns>
        public Task<List<string>> GetProtectedProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Configuration.ProtectedProcesses);
        }

        /// <summary>
        /// Gets the list of critical applications
        /// </summary>
        /// <returns>List of critical application names</returns>
        public Task<List<string>> GetCriticalApplicationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Configuration.CriticalApplications);
        }

        /// <summary>
        /// Updates the safety validation configuration
        /// </summary>
        /// <param name="configuration">New configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Update result</returns>
        public async Task<UpdateResult> UpdateConfigurationAsync(SafetyValidationConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating safety validation configuration");

                var validationResult = configuration.Validate();
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors);
                    _logger.LogError("Configuration validation failed: {Errors}", errors);
                    return new UpdateResult
                    {
                        Success = false,
                        Message = $"Configuration validation failed: {errors}"
                    };
                }

                Configuration = configuration;
                _logger.LogInformation("Safety validation configuration updated successfully");
                return new UpdateResult
                {
                    Success = true,
                    Message = "Configuration updated successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update safety validation configuration");
                return new UpdateResult
                {
                    Success = false,
                    Message = $"Update failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets the safety validation statistics
        /// </summary>
        /// <returns>Safety validation statistics</returns>
        public Task<SafetyValidationStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_statistics);
        }

        /// <summary>
        /// Resets the safety validation statistics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Reset result</returns>
        public Task<ResetResult> ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    _statistics.TotalValidations = 0;
                    _statistics.SuccessfulValidations = 0;
                    _statistics.FailedValidations = 0;
                    _statistics.Warnings = 0;
                    _statistics.AverageValidationTimeMs = 0;
                    _statistics.LastValidationTime = DateTime.MinValue;
                    _statistics.SuccessRatePercentage = 0;
                    _statistics.MostCommonFailureReason = null;
                }

                _logger.LogInformation("Safety validation statistics reset successfully");
                return Task.FromResult(new ResetResult
                {
                    Success = true,
                    Message = "Statistics reset successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset safety validation statistics");
                return Task.FromResult(new ResetResult
                {
                    Success = false,
                    Message = $"Reset failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Adds a custom safety validation rule
        /// </summary>
        /// <param name="ruleName">Rule name</param>
        /// <param name="validationFunc">Validation function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Add result</returns>
        public Task<AddRuleResult> AddCustomValidationRuleAsync(string ruleName, Func<ValidationContext, Task<SafetyCheckResult>> validationFunc, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(ruleName))
                {
                    return Task.FromResult(new AddRuleResult
                    {
                        Success = false,
                        Message = "Rule name cannot be empty"
                    });
                }

                if (validationFunc == null)
                {
                    return Task.FromResult(new AddRuleResult
                    {
                        Success = false,
                        Message = "Validation function cannot be null"
                    });
                }

                _customValidationRules[ruleName] = validationFunc;
                _logger.LogInformation("Custom validation rule '{RuleName}' added successfully", ruleName);

                return Task.FromResult(new AddRuleResult
                {
                    Success = true,
                    Message = "Custom validation rule added successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add custom validation rule '{RuleName}'", ruleName);
                return Task.FromResult(new AddRuleResult
                {
                    Success = false,
                    Message = $"Failed to add rule: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Removes a custom safety validation rule
        /// </summary>
        /// <param name="ruleName">Rule name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Remove result</returns>
        public Task<RemoveRuleResult> RemoveCustomValidationRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_customValidationRules.Remove(ruleName))
                {
                    _logger.LogInformation("Custom validation rule '{RuleName}' removed successfully", ruleName);
                    return Task.FromResult(new RemoveRuleResult
                    {
                        Success = true,
                        Message = "Custom validation rule removed successfully"
                    });
                }
                else
                {
                    _logger.LogWarning("Custom validation rule '{RuleName}' not found", ruleName);
                    return Task.FromResult(new RemoveRuleResult
                    {
                        Success = false,
                        Message = "Custom validation rule not found"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove custom validation rule '{RuleName}'", ruleName);
                return Task.FromResult(new RemoveRuleResult
                {
                    Success = false,
                    Message = $"Failed to remove rule: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Disposes the safety validator
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the safety validator
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _monitoringCts?.Cancel();
                    _monitoringCts?.Dispose();
                }
                _isDisposed = true;
            }
        }

        #region Private Helper Methods

        private async Task<bool> CheckProcessAccessibilityAsync(System.Diagnostics.Process process, string userName, CancellationToken cancellationToken)
        {
            try
            {
                // Try to access process information
                var processName = process.ProcessName;
                var processId = process.Id;
                var startTime = process.StartTime;

                // Try to get process modules (requires appropriate permissions)
                try
                {
                    var modules = process.Modules;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Cannot access process modules for process {ProcessName} (ID: {ProcessId})", processName, processId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking process accessibility for user {UserName}", userName);
                return false;
            }
        }

        private async Task<SystemResourceUsage> GetSystemResourceUsageAsync(CancellationToken cancellationToken)
        {
            var usage = new SystemResourceUsage();

            try
            {
                // Get CPU usage
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call always returns 0
                await Task.Delay(1000, cancellationToken); // Wait for accurate reading
                usage.CpuUsagePercent = cpuCounter.NextValue();

                // Get memory usage
                var memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                usage.MemoryUsagePercent = memCounter.NextValue();

                // Get available memory
                var availableMemCounter = new PerformanceCounter("Memory", "Available MBytes");
                usage.AvailableMemoryBytes = (long)(availableMemCounter.NextValue() * 1024 * 1024);

                // Get total memory
                var totalMemCounter = new PerformanceCounter("Memory", "Committed Bytes");
                usage.TotalMemoryBytes = (long)totalMemCounter.NextValue();

                usage.NetworkActivity = new NetworkActivity
                {
                    IsConnected = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(),
                    ActiveConnections = 0 // Would need more complex logic to get actual connection count
                };

                return usage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system resource usage");
                throw;
            }
        }

        private async Task<NetworkConnectivityResult> TestNetworkConnectivityAsync(string server, int timeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(server, timeoutMs);
                    return new NetworkConnectivityResult
                    {
                        Success = reply.Status == IPStatus.Success,
                        LatencyMs = reply.RoundtripTime,
                        Status = reply.Status.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Network connectivity test failed for server {Server}", server);
                return new NetworkConnectivityResult
                {
                    Success = false,
                    Status = ex.Message
                };
            }
        }

        private async Task<PortConnectivityResult> TestPortConnectivityAsync(string server, int port, CancellationToken cancellationToken)
        {
            try
            {
                using (var tcpClient = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(server, port);
                    var timeoutTask = Task.Delay(Configuration.NetworkConnectivityTimeoutSeconds * 1000, cancellationToken);

                    // Wait for connection with timeout using WaitAsync
                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(Configuration.NetworkConnectivityTimeoutSeconds * 1000);
                        await connectTask.WaitAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout occurred
                        return new PortConnectivityResult
                        {
                            Success = false,
                            Status = "Timeout"
                        };
                    }

                    if (connectTask.IsFaulted)
                    {
                        return new PortConnectivityResult
                        {
                            Success = false,
                            Status = connectTask.Exception?.InnerException?.Message ?? "Connection failed"
                        };
                    }

                    return new PortConnectivityResult
                    {
                        Success = true,
                        Status = "Connected"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Port connectivity test failed for {Server}:{Port}", server, port);
                return new PortConnectivityResult
                {
                    Success = false,
                    Status = ex.Message
                };
            }
        }

        private void MergeValidationResult(SafetyValidationResult target, SafetyValidationResult source)
        {
            foreach (var check in source.ValidationChecks)
            {
                target.ValidationChecks[check.Key] = check.Value;
            }

            foreach (var warning in source.Warnings)
            {
                target.AddWarning(warning);
            }

            foreach (var error in source.Errors)
            {
                target.AddError(error);
            }

            if (source.UserActivity != null)
            {
                target.UserActivity = source.UserActivity;
            }

            if (source.SystemResources != null)
            {
                target.SystemResources = source.SystemResources;
            }
        }

        private void UpdateStatistics(SafetyValidationResult result, TimeSpan duration)
        {
            lock (_lock)
            {
                _statistics.TotalValidations++;
                _statistics.LastValidationTime = DateTime.UtcNow;

                if (result.IsValid)
                {
                    _statistics.SuccessfulValidations++;
                }
                else
                {
                    _statistics.FailedValidations++;
                    if (!string.IsNullOrEmpty(result.FailureReason))
                    {
                        _statistics.MostCommonFailureReason = result.FailureReason;
                    }
                }

                if (result.Status == SafetyStatus.Warning)
                {
                    _statistics.Warnings++;
                }

                // Update average validation time
                var totalTime = _statistics.AverageValidationTimeMs * (_statistics.TotalValidations - 1) + duration.TotalMilliseconds;
                _statistics.AverageValidationTimeMs = totalTime / _statistics.TotalValidations;

                // Update success rate
                _statistics.SuccessRatePercentage = (_statistics.SuccessfulValidations / (double)_statistics.TotalValidations) * 100;
            }
        }

        #endregion

        #region Helper Classes

        private class NetworkConnectivityResult
        {
            public bool Success { get; set; }
            public long LatencyMs { get; set; }
            public string Status { get; set; }
        }

        private class PortConnectivityResult
        {
            public bool Success { get; set; }
            public string Status { get; set; }
        }

        #endregion
    }
}