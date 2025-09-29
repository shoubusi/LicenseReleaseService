using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Ping-based idle detector that actively probes processes for responsiveness
    /// </summary>
    public class PingBasedIdleDetector : IIdleDetector
    {
        private readonly ILogger<PingBasedIdleDetector> _logger;
        private readonly ProcessPingService _processPingService;
        private readonly DocumentActivityMonitor _documentMonitor;
        private readonly NetworkActivityMonitor _networkMonitor;
        private readonly PingBasedDetectionConfig _config;
        private readonly object _lock = new object();
        private readonly Dictionary<int, ProcessPingState> _processStates = new Dictionary<int, ProcessPingState>();
        private readonly Timer _detectionTimer;
        private bool _isDisposed;
        private CancellationTokenSource _cancellationTokenSource;

        #region IIdleDetector Properties

        public string Name => "PingBasedIdleDetector";
        public string Description => "Active probing-based idle detection with process responsiveness checking";
        public Version Version => new Version(1, 0, 0, 0);
        public bool IsEnabled { get; private set; }
        public bool IsInitialized { get; private set; }
        public int Priority => 20; // Higher priority than time-based detection
        public IReadOnlyList<string> SupportedMethods => new List<string> { "ProcessPing", "DocumentActivity", "NetworkActivity", "WindowsAPI" }.AsReadOnly();
        public DetectorStatus Status { get; private set; }
        public IdleDetectorConfiguration Configuration { get; private set; }

        #endregion

        #region IIdleDetector Events

        public event EventHandler<IdleDetectionEventArgs> IdleDetected;
        public event EventHandler<IdleDetectionEventArgs> ActivityDetected;
        public event EventHandler<DetectorStatusChangedEventArgs> StatusChanged;
        public event EventHandler<DetectorErrorEventArgs> ErrorOccurred;

        #endregion

        /// <summary>
        /// Initializes a new instance of the PingBasedIdleDetector class
        /// </summary>
        public PingBasedIdleDetector(
            ILogger<PingBasedIdleDetector> logger,
            ProcessPingService processPingService,
            DocumentActivityMonitor documentMonitor,
            NetworkActivityMonitor networkMonitor,
            PingBasedDetectionConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processPingService = processPingService ?? throw new ArgumentNullException(nameof(processPingService));
            _documentMonitor = documentMonitor ?? throw new ArgumentNullException(nameof(documentMonitor));
            _networkMonitor = networkMonitor ?? throw new ArgumentNullException(nameof(networkMonitor));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Status = DetectorStatus.NotInitialized;
            IsEnabled = _config.ToIdleDetectorConfiguration().IsEnabled;

            // Initialize detection timer
            _detectionTimer = new Timer(DetectionTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Wire up monitor events
            _processPingService.ProcessResponded += OnProcessResponded;
            _processPingService.ProcessUnresponsive += OnProcessUnresponsive;
            _documentMonitor.DocumentActivityDetected += OnDocumentActivityDetected;
            _networkMonitor.NetworkActivityDetected += OnNetworkActivityDetected;

            _logger.LogInformation("PingBasedIdleDetector initialized with configuration: {Config}", _config);
        }

        #region IIdleDetector Initialization and Lifecycle

        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing PingBasedIdleDetector");

                lock (_lock)
                {
                    if (IsInitialized)
                    {
                        _logger.LogWarning("PingBasedIdleDetector is already initialized");
                        return;
                    }

                    Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Validate configuration
                var validationResult = ValidateConfiguration(configuration);
                if (!validationResult.IsValid)
                {
                    var error = string.Join(", ", validationResult.Errors);
                    throw new InvalidOperationException($"Invalid configuration: {error}");
                }

                // Initialize all services
                await _processPingService.InitializeAsync(configuration, cancellationToken);
                await _documentMonitor.InitializeAsync(configuration, cancellationToken);
                await _networkMonitor.InitializeAsync(configuration, cancellationToken);

                // Update status
                UpdateStatus(DetectorStatus.Initialized, "Initialization completed");

                IsInitialized = true;
                _logger.LogInformation("PingBasedIdleDetector initialized successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("InitializeAsync", ex, ErrorSeverity.Critical);
                throw;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting PingBasedIdleDetector");

                lock (_lock)
                {
                    if (!IsInitialized)
                    {
                        throw new InvalidOperationException("Detector must be initialized before starting");
                    }

                    if (Status == DetectorStatus.Running)
                    {
                        _logger.LogWarning("PingBasedIdleDetector is already running");
                        return;
                    }
                }

                // Start all services
                await _processPingService.StartAsync(cancellationToken);
                await _documentMonitor.StartAsync(cancellationToken);
                await _networkMonitor.StartAsync(cancellationToken);

                // Start detection timer
                var dueTime = TimeSpan.FromSeconds(_config.DetectionIntervalSeconds);
                var period = TimeSpan.FromSeconds(_config.DetectionIntervalSeconds);
                _detectionTimer.Change(dueTime, period);

                // Update status
                UpdateStatus(DetectorStatus.Running, "Detector started");

                _logger.LogInformation("PingBasedIdleDetector started successfully with detection interval: {Interval}s",
                    _config.DetectionIntervalSeconds);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("StartAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping PingBasedIdleDetector");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Running)
                    {
                        _logger.LogWarning("PingBasedIdleDetector is not running");
                        return;
                    }
                }

                // Stop detection timer
                _detectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Stop all services
                await _processPingService.StopAsync(cancellationToken);
                await _documentMonitor.StopAsync(cancellationToken);
                await _networkMonitor.StopAsync(cancellationToken);

                // Cancel any pending operations
                _cancellationTokenSource?.Cancel();

                // Update status
                UpdateStatus(DetectorStatus.Stopped, "Detector stopped");

                _logger.LogInformation("PingBasedIdleDetector stopped successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("StopAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing PingBasedIdleDetector");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Running)
                    {
                        _logger.LogWarning("PingBasedIdleDetector is not running");
                        return;
                    }
                }

                // Stop detection timer
                _detectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Pause all services
                await _processPingService.PauseAsync(cancellationToken);
                await _documentMonitor.PauseAsync(cancellationToken);
                await _networkMonitor.PauseAsync(cancellationToken);

                // Update status
                UpdateStatus(DetectorStatus.Paused, "Detector paused");

                _logger.LogInformation("PingBasedIdleDetector paused successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("PauseAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming PingBasedIdleDetector");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Paused)
                    {
                        _logger.LogWarning("PingBasedIdleDetector is not paused");
                        return;
                    }
                }

                // Resume all services
                await _processPingService.ResumeAsync(cancellationToken);
                await _documentMonitor.ResumeAsync(cancellationToken);
                await _networkMonitor.ResumeAsync(cancellationToken);

                // Restart detection timer
                var dueTime = TimeSpan.FromSeconds(_config.DetectionIntervalSeconds);
                var period = TimeSpan.FromSeconds(_config.DetectionIntervalSeconds);
                _detectionTimer.Change(dueTime, period);

                // Update status
                UpdateStatus(DetectorStatus.Running, "Detector resumed");

                _logger.LogInformation("PingBasedIdleDetector resumed successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("ResumeAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        #endregion

        #region IIdleDetector Detection Methods

        public async Task<IdleDetectionResult> DetectIdleAsync(int processId, string userName, string computerName, CancellationToken cancellationToken = default)
        {
            try
            {
                var sessionId = Guid.NewGuid().ToString("N")[..8];
                var startTime = DateTime.UtcNow;

                _logger.LogDebug("Starting ping-based idle detection for process {ProcessId}, user {UserName}, computer {ComputerName}",
                    processId, userName, computerName);

                // Get or create process state
                var processState = GetOrCreateProcessState(processId, userName, computerName);

                // Perform multi-factor ping detection
                var pingResults = await PerformMultiFactorPingDetectionAsync(processId, userName, computerName, cancellationToken);

                // Analyze results and determine idle state
                var analysisResult = AnalyzePingResults(pingResults, processState);

                // Create result
                var result = new IdleDetectionResult
                {
                    SessionId = sessionId,
                    ProcessId = processId,
                    UserName = userName,
                    ComputerName = computerName,
                    IsIdle = analysisResult.IsIdle,
                    IdleTime = analysisResult.EstimatedIdleTime,
                    Confidence = analysisResult.Confidence,
                    DetectionMethod = analysisResult.PrimaryDetectionMethod,
                    Timestamp = DateTime.UtcNow,
                    DetectorName = Name,
                    Reason = analysisResult.Reason
                };

                // Add detailed metadata
                result.Metadata["PingResults"] = pingResults;
                result.Metadata["ResponseTime"] = analysisResult.AverageResponseTime;
                result.Metadata["SuccessRate"] = analysisResult.SuccessRate;
                result.Metadata["DocumentActivityCount"] = pingResults.DocumentActivities?.Count ?? 0;
                result.Metadata["NetworkActivityCount"] = pingResults.NetworkActivities?.Count ?? 0;
                result.Metadata["WindowsAPIAccessible"] = pingResults.WindowsAPIAccessible;
                result.Metadata["LastSuccessfulPing"] = processState.LastSuccessfulPing;

                // Update process state
                UpdateProcessState(processState, result, analysisResult);

                // Raise events
                if (result.IsIdle && !processState.WasIdle)
                {
                    OnIdleDetected(result);
                }
                else if (!result.IsIdle && processState.WasIdle)
                {
                    OnActivityDetected(result);
                }

                var detectionTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Ping-based idle detection completed for process {ProcessId}: IsIdle={IsIdle}, " +
                    "Confidence={Confidence:F2}, Time={Time}ms, Method={Method}",
                    processId, result.IsIdle, result.Confidence, detectionTime.TotalMilliseconds, result.DetectionMethod);

                return result;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("DetectIdleAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        public async Task<IList<IdleDetectionResult>> DetectIdleAsync(IList<ProcessInfo> processes, CancellationToken cancellationToken = default)
        {
            try
            {
                var results = new List<IdleDetectionResult>();
                var startTime = DateTime.UtcNow;

                _logger.LogDebug("Starting batch ping-based idle detection for {Count} processes", processes.Count);

                // Process in parallel with configurable concurrency
                var semaphore = new SemaphoreSlim(_config.MaxConcurrentPings);
                var tasks = processes.Select(async process =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await DetectIdleAsync(process.ProcessId, process.UserName, process.ComputerName, cancellationToken);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error detecting idle for process {ProcessId}", process.ProcessId);
                        return new IdleDetectionResult
                        {
                            ProcessId = process.ProcessId,
                            UserName = process.UserName,
                            ComputerName = process.ComputerName,
                            IsIdle = false,
                            Confidence = 0.0,
                            DetectionMethod = "Error",
                            Reason = ex.Message
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults);

                var totalTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Batch ping-based idle detection completed: {Count} processes in {Time}ms",
                    results.Count, totalTime.TotalMilliseconds);

                return results;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("DetectIdleAsync (batch)", ex, ErrorSeverity.High);
                throw;
            }
        }

        public async Task<TimeSpan> GetIdleTimeAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    if (_processStates.TryGetValue(processId, out var state))
                    {
                        return state.EstimatedIdleTime;
                    }
                }

                // If no state exists, perform a quick ping check
                var pingResult = await _processPingService.PingProcessAsync(processId, cancellationToken);
                return pingResult.IsResponsive ? TimeSpan.Zero : TimeSpan.FromMinutes(_config.DefaultIdleThresholdMinutes);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetIdleTimeAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<bool> IsIdleAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await DetectIdleAsync(processId, "unknown", "unknown", cancellationToken);
                return result.IsIdle;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("IsIdleAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<IdleInfo> GetIdleInfoAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get comprehensive ping information
                var pingResults = await PerformMultiFactorPingDetectionAsync(processId, "unknown", "unknown", cancellationToken);

                // Get recent activities from monitors
                var documentActivities = await _documentMonitor.GetRecentActivitiesAsync(processId, TimeSpan.FromMinutes(30), cancellationToken);
                var networkActivities = await _networkMonitor.GetRecentActivitiesAsync(processId, TimeSpan.FromMinutes(30), cancellationToken);

                // Calculate overall idle state
                var analysisResult = AnalyzePingResults(pingResults, null);

                return new IdleInfo
                {
                    ProcessId = processId,
                    IdleTime = analysisResult.EstimatedIdleTime,
                    LastActivity = GetLastActivityTime(pingResults, documentActivities, networkActivities),
                    State = analysisResult.IsIdle ? SessionState.Inactive : SessionState.Active,
                    Confidence = analysisResult.Confidence,
                    DetectionMethods = new List<string> { "ProcessPing", "DocumentActivity", "NetworkActivity" },
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["PingSuccessRate"] = analysisResult.SuccessRate,
                        ["AverageResponseTime"] = analysisResult.AverageResponseTime,
                        ["DocumentActivityCount"] = documentActivities.Count,
                        ["NetworkActivityCount"] = networkActivities.Count,
                        ["WindowsAPIAccessible"] = pingResults.WindowsAPIAccessible,
                        ["LastSuccessfulPing"] = pingResults.LastSuccessfulPing,
                        ["FailedPingCount"] = pingResults.FailedPingCount,
                        ["PrimaryDetectionMethod"] = analysisResult.PrimaryDetectionMethod
                    }
                };
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetIdleInfoAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        #endregion

        #region IIdleDetector Configuration and Management

        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating PingBasedIdleDetector configuration");

                lock (_lock)
                {
                    var validationResult = ValidateConfiguration(configuration);
                    if (!validationResult.IsValid)
                    {
                        var error = string.Join(", ", validationResult.Errors);
                        throw new InvalidOperationException($"Invalid configuration: {error}");
                    }

                    Configuration = configuration;
                }

                // Update service configurations
                await _processPingService.UpdateConfigurationAsync(configuration, cancellationToken);
                await _documentMonitor.UpdateConfigurationAsync(configuration, cancellationToken);
                await _networkMonitor.UpdateConfigurationAsync(configuration, cancellationToken);

                _logger.LogInformation("PingBasedIdleDetector configuration updated successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("UpdateConfigurationAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<DetectorHealth> GetHealthAsync()
        {
            try
            {
                var health = new DetectorHealth
                {
                    DetectorName = Name,
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                // Check component health
                var isHealthy = true;
                var issues = new List<string>();

                // Check process ping service
                var pingHealth = await _processPingService.GetHealthAsync();
                if (!pingHealth.IsHealthy)
                {
                    isHealthy = false;
                    issues.Add($"Process ping service unhealthy: {pingHealth.StatusMessage}");
                }

                // Check document monitor
                var documentHealth = await _documentMonitor.GetHealthAsync();
                if (!documentHealth.IsHealthy)
                {
                    isHealthy = false;
                    issues.Add($"Document monitor unhealthy: {documentHealth.StatusMessage}");
                }

                // Check network monitor
                var networkHealth = await _networkMonitor.GetHealthAsync();
                if (!networkHealth.IsHealthy)
                {
                    isHealthy = false;
                    issues.Add($"Network monitor unhealthy: {networkHealth.StatusMessage}");
                }

                // Check process states
                lock (_lock)
                {
                    if (_processStates.Count > 500)
                    {
                        issues.Add($"High process state count: {_processStates.Count}");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["ProcessStateCount"] = _processStates.Count;
                health.AdditionalInfo["PingServiceHealthy"] = pingHealth.IsHealthy;
                health.AdditionalInfo["DocumentMonitorHealthy"] = documentHealth.IsHealthy;
                health.AdditionalInfo["NetworkMonitorHealthy"] = networkHealth.IsHealthy;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detector health");
                return new DetectorHealth
                {
                    DetectorName = Name,
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        public async Task<DetectorStatistics> GetStatisticsAsync()
        {
            try
            {
                var pingStats = await _processPingService.GetStatisticsAsync();
                var documentStats = await _documentMonitor.GetStatisticsAsync();
                var networkStats = await _networkMonitor.GetStatisticsAsync();

                lock (_lock)
                {
                    var stats = new DetectorStatistics
                    {
                        DetectorName = Name,
                        TotalDetections = pingStats.TotalPings + documentStats.TotalActivitiesMonitored + networkStats.TotalActivitiesMonitored,
                        SuccessfulDetections = pingStats.SuccessfulPings + documentStats.SuccessfulDetections + networkStats.SuccessfulDetections,
                        FailedDetections = pingStats.FailedPings + documentStats.FailedDetections + networkStats.FailedDetections,
                        IdleStatesDetected = _processStates.Values.Count(ps => ps.WasIdle),
                        ActiveStatesDetected = _processStates.Values.Count(ps => !ps.WasIdle),
                        AverageDetectionTimeMs = pingStats.AverageResponseTimeMs,
                        Uptime = DateTime.UtcNow - (Configuration?.CustomParameters?["StartTime"] as DateTime? ?? DateTime.UtcNow),
                        ErrorCount = pingStats.ErrorCount + documentStats.ErrorCount + networkStats.ErrorCount,
                        StatisticsStartTime = Configuration?.CustomParameters?["StatisticsStartTime"] as DateTime? ?? DateTime.UtcNow,
                        LastDetectionTimestamp = _processStates.Values
                            .Select(ps => ps.LastDetectionTime)
                            .OrderByDescending(t => t)
                            .FirstOrDefault()
                    };

                    return stats;
                }
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetStatisticsAsync", ex, ErrorSeverity.Low);
                throw;
            }
        }

        public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    _processStates.Clear();
                }

                await _processPingService.ResetStatisticsAsync(cancellationToken);
                await _documentMonitor.ResetStatisticsAsync(cancellationToken);
                await _networkMonitor.ResetStatisticsAsync(cancellationToken);

                _logger.LogInformation("Ping-based detector statistics reset successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("ResetStatisticsAsync", ex, ErrorSeverity.Low);
                throw;
            }
        }

        public ValidationResult ValidateConfiguration(IdleDetectorConfiguration configuration)
        {
            var result = new ValidationResult();

            try
            {
                if (configuration == null)
                {
                    result.AddError("Configuration cannot be null");
                    return result;
                }

                // Validate required settings
                if (configuration.DetectionIntervalSeconds < 5)
                {
                    result.AddError("Detection interval must be at least 5 seconds for ping-based detection");
                }

                if (configuration.IdleThresholdSeconds < 30)
                {
                    result.AddError("Idle threshold must be at least 30 seconds for ping-based detection");
                }

                if (configuration.ConfidenceThreshold < 0.5 || configuration.ConfidenceThreshold > 1.0)
                {
                    result.AddError("Confidence threshold must be between 0.5 and 1.0 for ping-based detection");
                }

                // Validate ping-specific parameters
                if (configuration.CustomParameters != null)
                {
                    if (configuration.CustomParameters.ContainsKey("PingTimeoutMs"))
                    {
                        var pingTimeout = Convert.ToInt32(configuration.CustomParameters["PingTimeoutMs"]);
                        if (pingTimeout < 100 || pingTimeout > 10000)
                        {
                            result.AddError("Ping timeout must be between 100 and 10000 milliseconds");
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("MaxConcurrentPings"))
                    {
                        var maxConcurrent = Convert.ToInt32(configuration.CustomParameters["MaxConcurrentPings"]);
                        if (maxConcurrent < 1 || maxConcurrent > 50)
                        {
                            result.AddError("Max concurrent pings must be between 1 and 50");
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("RequiredSuccessRate"))
                    {
                        var successRate = Convert.ToDouble(configuration.CustomParameters["RequiredSuccessRate"]);
                        if (successRate < 0.0 || successRate > 1.0)
                        {
                            result.AddError("Required success rate must be between 0.0 and 1.0");
                        }
                    }
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.AddError($"Configuration validation error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Private Methods

        private async Task<PingDetectionResults> PerformMultiFactorPingDetectionAsync(int processId, string userName, string computerName, CancellationToken cancellationToken)
        {
            var results = new PingDetectionResults();

            try
            {
                // Process ping detection
                results.ProcessPingResult = await _processPingService.PingProcessAsync(processId, cancellationToken);

                // Document activity detection
                results.DocumentActivities = await _documentMonitor.GetRecentActivitiesAsync(processId, TimeSpan.FromMinutes(15), cancellationToken);

                // Network activity detection
                results.NetworkActivities = await _networkMonitor.GetRecentActivitiesAsync(processId, TimeSpan.FromMinutes(15), cancellationToken);

                // Windows API accessibility check
                results.WindowsAPIAccessible = await CheckWindowsAPIAccessibilityAsync(processId, cancellationToken);

                // Calculate summary metrics
                results.SuccessRate = CalculateOverallSuccessRate(results);
                results.LastSuccessfulPing = results.ProcessPingResult?.LastSuccessfulPing;
                results.FailedPingCount = results.ProcessPingResult?.FailedPingCount ?? 0;

                _logger.LogDebug("Multi-factor ping detection completed for process {ProcessId}: SuccessRate={SuccessRate:F2}, WindowsAPI={WindowsAPI}",
                    processId, results.SuccessRate, results.WindowsAPIAccessible);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing multi-factor ping detection for process {ProcessId}", processId);
                results.Error = ex.Message;
            }

            return results;
        }

        private PingAnalysisResult AnalyzePingResults(PingDetectionResults results, ProcessPingState processState)
        {
            var analysis = new PingAnalysisResult();

            try
            {
                // Calculate success rate
                analysis.SuccessRate = results.SuccessRate;
                analysis.AverageResponseTime = results.ProcessPingResult?.AverageResponseTimeMs ?? 0;

                // Determine primary detection method
                analysis.PrimaryDetectionMethod = DeterminePrimaryDetectionMethod(results);

                // Calculate confidence based on multiple factors
                analysis.Confidence = CalculatePingConfidence(results, processState);

                // Determine if process is idle
                var isUnresponsive = analysis.SuccessRate < _config.RequiredSuccessRate;
                var hasRecentActivity = HasRecentActivity(results);
                var isWindowsAPIAccessible = results.WindowsAPIAccessible;

                analysis.IsIdle = isUnresponsive && !hasRecentActivity && !isWindowsAPIAccessible;

                // Estimate idle time
                analysis.EstimatedIdleTime = EstimateIdleTime(results, processState, analysis.IsIdle);

                // Generate reason
                analysis.Reason = GenerateIdleReason(analysis, results);

                _logger.LogDebug("Ping analysis completed: IsIdle={IsIdle}, Confidence={Confidence:F2}, Method={Method}",
                    analysis.IsIdle, analysis.Confidence, analysis.PrimaryDetectionMethod);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing ping results");
                analysis.IsIdle = false;
                analysis.Confidence = 0.0;
                analysis.Reason = $"Analysis error: {ex.Message}";
            }

            return analysis;
        }

        private double CalculateOverallSuccessRate(PingDetectionResults results)
        {
            if (results.ProcessPingResult == null)
                return 0.0;

            var totalAttempts = results.ProcessPingResult.TotalPingAttempts;
            if (totalAttempts == 0)
                return 0.0;

            var successfulPings = results.ProcessPingResult.SuccessfulPingCount;
            return (double)successfulPings / totalAttempts;
        }

        private string DeterminePrimaryDetectionMethod(PingDetectionResults results)
        {
            if (results.ProcessPingResult?.IsResponsive == false)
                return "ProcessPing";

            if (results.DocumentActivities?.Any() == true)
                return "DocumentActivity";

            if (results.NetworkActivities?.Any() == true)
                return "NetworkActivity";

            if (results.WindowsAPIAccessible)
                return "WindowsAPI";

            return "Unknown";
        }

        private double CalculatePingConfidence(PingDetectionResults results, ProcessPingState processState)
        {
            var baseConfidence = 0.0;

            // Factor 1: Success rate (inverse - lower success rate = higher idle confidence)
            var successRateFactor = 1.0 - results.SuccessRate;

            // Factor 2: Response time (higher response time = higher idle confidence)
            var responseTimeFactor = Math.Min(results.ProcessPingResult?.AverageResponseTimeMs / 1000.0 ?? 0.0, 1.0);

            // Factor 3: Recent activity (no activity = higher idle confidence)
            var activityFactor = HasRecentActivity(results) ? 0.2 : 0.8;

            // Factor 4: Windows API accessibility (not accessible = higher idle confidence)
            var apiFactor = results.WindowsAPIAccessible ? 0.3 : 0.7;

            // Factor 5: Historical consistency
            var consistencyFactor = processState?.TotalDetections > 0 ?
                (double)processState.IdleDetections / processState.TotalDetections : 0.5;

            // Weighted combination
            baseConfidence = (successRateFactor * 0.3) + (responseTimeFactor * 0.2) +
                           (activityFactor * 0.2) + (apiFactor * 0.2) + (consistencyFactor * 0.1);

            return Math.Max(0.0, Math.Min(1.0, baseConfidence));
        }

        private bool HasRecentActivity(PingDetectionResults results)
        {
            var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(_config.ActivityThresholdMinutes);

            // Check document activities
            if (results.DocumentActivities?.Any(a => a.Timestamp > cutoffTime) == true)
                return true;

            // Check network activities
            if (results.NetworkActivities?.Any(a => a.Timestamp > cutoffTime) == true)
                return true;

            return false;
        }

        private TimeSpan EstimateIdleTime(PingDetectionResults results, ProcessPingState processState, bool isIdle)
        {
            if (!isIdle)
                return TimeSpan.Zero;

            // Use last successful ping as baseline
            if (results.LastSuccessfulPing.HasValue)
            {
                return DateTime.UtcNow - results.LastSuccessfulPing.Value;
            }

            // Use process state estimation
            if (processState?.LastSuccessfulPing.HasValue == true)
            {
                return DateTime.UtcNow - processState.LastSuccessfulPing.Value;
            }

            // Fallback to configured threshold
            return TimeSpan.FromMinutes(_config.DefaultIdleThresholdMinutes);
        }

        private string GenerateIdleReason(PingAnalysisResult analysis, PingDetectionResults results)
        {
            if (!analysis.IsIdle)
            {
                if (analysis.SuccessRate >= _config.RequiredSuccessRate)
                    return $"Process responsive (success rate: {analysis.SuccessRate:P1})";

                if (HasRecentActivity(results))
                    return "Recent activity detected";

                if (results.WindowsAPIAccessible)
                    return "Windows API accessible - process appears active";

                return "Process appears active";
            }

            return $"Process unresponsive (success rate: {analysis.SuccessRate:P1}, threshold: {_config.RequiredSuccessRate:P1})";
        }

        private async Task<bool> CheckWindowsAPIAccessibilityAsync(int processId, CancellationToken cancellationToken)
        {
            try
            {
                // Try to access the process using Windows API
                using var process = System.Diagnostics.Process.GetProcessById(processId);

                // Check basic process properties
                _ = process.ProcessName;
                _ = process.MainWindowTitle;
                _ = process.Responding;

                // If we get here, the process is accessible
                return true;
            }
            catch (ArgumentException)
            {
                // Process not found
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied or other Windows API error
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Windows API accessibility check failed for process {ProcessId}", processId);
                return false;
            }
        }

        private DateTime GetLastActivityTime(PingDetectionResults results, IList<DocumentActivityData> documentActivities, IList<NetworkActivityData> networkActivities)
        {
            var times = new List<DateTime?>();

            if (results.LastSuccessfulPing.HasValue)
                times.Add(results.LastSuccessfulPing);

            if (documentActivities?.Any() == true)
                times.Add(documentActivities.Max(a => a.Timestamp));

            if (networkActivities?.Any() == true)
                times.Add(networkActivities.Max(a => a.Timestamp));

            return times.Max() ?? DateTime.UtcNow;
        }

        private void DetectionTimerCallback(object state)
        {
            try
            {
                if (Status != DetectorStatus.Running)
                    return;

                // Perform periodic detection for all tracked processes
                lock (_lock)
                {
                    var processesToCheck = _processStates.Values.ToList();
                    foreach (var processState in processesToCheck)
                    {
                        try
                        {
                            // Fire and forget detection
                            _ = Task.Run(() => DetectIdleAsync(
                                processState.ProcessId,
                                processState.UserName,
                                processState.ComputerName));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in detection timer callback for process {ProcessId}",
                                processState.ProcessId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in detection timer callback");
            }
        }

        private ProcessPingState GetOrCreateProcessState(int processId, string userName, string computerName)
        {
            lock (_lock)
            {
                if (!_processStates.TryGetValue(processId, out var state))
                {
                    state = new ProcessPingState
                    {
                        ProcessId = processId,
                        UserName = userName,
                        ComputerName = computerName,
                        CreatedTime = DateTime.UtcNow,
                        LastDetectionTime = DateTime.UtcNow,
                        EstimatedIdleTime = TimeSpan.Zero,
                        WasIdle = false
                    };

                    _processStates[processId] = state;
                    _logger.LogDebug("Created new process ping state for process {ProcessId}", processId);
                }

                return state;
            }
        }

        private void UpdateProcessState(ProcessPingState state, IdleDetectionResult result, PingAnalysisResult analysis)
        {
            lock (_lock)
            {
                state.LastDetectionTime = DateTime.UtcNow;
                state.EstimatedIdleTime = result.IdleTime;
                state.WasIdle = result.IsIdle;
                state.TotalDetections++;
                state.LastSuccessfulPing = result.Metadata.ContainsKey("LastSuccessfulPing")
                    ? result.Metadata["LastSuccessfulPing"] as DateTime?
                    : null;

                if (result.IsIdle)
                {
                    state.IdleDetections++;
                    state.TotalIdleTime += result.IdleTime;
                }
                else
                {
                    state.ActiveDetections++;
                }
            }
        }

        private void UpdateStatus(DetectorStatus newStatus, string reason)
        {
            var oldStatus = Status;
            Status = newStatus;

            try
            {
                StatusChanged?.Invoke(this, new DetectorStatusChangedEventArgs(Name, oldStatus, newStatus, reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising StatusChanged event");
            }
        }

        private void OnIdleDetected(IdleDetectionResult result)
        {
            try
            {
                IdleDetected?.Invoke(this, new IdleDetectionEventArgs(
                    result.SessionId,
                    result.ProcessId,
                    result.UserName,
                    result.ComputerName,
                    result.Confidence,
                    result.IdleTime,
                    result.DetectionMethod,
                    result.Reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising IdleDetected event");
            }
        }

        private void OnActivityDetected(IdleDetectionResult result)
        {
            try
            {
                ActivityDetected?.Invoke(this, new IdleDetectionEventArgs(
                    result.SessionId,
                    result.ProcessId,
                    result.UserName,
                    result.ComputerName,
                    result.Confidence,
                    result.IdleTime,
                    result.DetectionMethod,
                    result.Reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising ActivityDetected event");
            }
        }

        private void OnProcessResponded(object sender, ProcessPingResult result)
        {
            _logger.LogDebug("Process responded to ping: Process={ProcessId}, ResponseTime={Time}ms",
                result.ProcessId, result.ResponseTimeMs);
        }

        private void OnProcessUnresponsive(object sender, ProcessPingResult result)
        {
            _logger.LogDebug("Process unresponsive to ping: Process={ProcessId}, Attempts={Attempts}",
                result.ProcessId, result.TotalPingAttempts);
        }

        private void OnDocumentActivityDetected(object sender, DocumentActivityData activity)
        {
            _logger.LogDebug("Document activity detected: Process={ProcessId}, Activity={Activity}",
                activity.ProcessId, activity.ActivityType);
        }

        private void OnNetworkActivityDetected(object sender, NetworkActivityData activity)
        {
            _logger.LogDebug("Network activity detected: Process={ProcessId}, Activity={Activity}",
                activity.ProcessId, activity.ActivityType);
        }

        private async Task HandleErrorAsync(string operation, Exception ex, ErrorSeverity severity)
        {
            _logger.LogError(ex, "Error in {Operation}", operation);

            try
            {
                ErrorOccurred?.Invoke(this, new DetectorErrorEventArgs(Name, ex, severity, operation));
            }
            catch (Exception eventEx)
            {
                _logger.LogError(eventEx, "Error raising ErrorOccurred event");
            }

            if (severity == ErrorSeverity.Critical)
            {
                UpdateStatus(DetectorStatus.Error, $"Critical error in {operation}: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _logger.LogInformation("Disposing PingBasedIdleDetector");

                    // Stop detection
                    StopAsync().Wait(TimeSpan.FromSeconds(5));

                    // Stop services
                    _processPingService?.Dispose();
                    _documentMonitor?.Dispose();
                    _networkMonitor?.Dispose();

                    // Dispose components
                    _detectionTimer?.Dispose();
                    _cancellationTokenSource?.Dispose();

                    // Clear process states
                    lock (_lock)
                    {
                        _processStates.Clear();
                    }

                    UpdateStatus(DetectorStatus.Disposing, "Detector disposed");
                }

                _isDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Configuration specific to ping-based detection
    /// </summary>
    public class PingBasedDetectionConfig
    {
        /// <summary>
        /// Gets or sets the detection interval in seconds
        /// </summary>
        public int DetectionIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the ping timeout in milliseconds
        /// </summary>
        public int PingTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum number of concurrent pings
        /// </summary>
        public int MaxConcurrentPings { get; set; } = 10;

        /// <summary>
        /// Gets or sets the required success rate (0.0 to 1.0)
        /// </summary>
        public double RequiredSuccessRate { get; set; } = 0.3;

        /// <summary>
        /// Gets or sets the activity threshold in minutes
        /// </summary>
        public int ActivityThresholdMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the default idle threshold in minutes
        /// </summary>
        public int DefaultIdleThresholdMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets the maximum number of ping attempts
        /// </summary>
        public int MaxPingAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the ping retry delay in milliseconds
        /// </summary>
        public int PingRetryDelayMs { get; set; } = 500;

        /// <summary>
        /// Gets or sets a value indicating whether Windows API checking is enabled
        /// </summary>
        public bool EnableWindowsAPIChecking { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether document activity monitoring is enabled
        /// </summary>
        public bool EnableDocumentActivityMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether network activity monitoring is enabled
        /// </summary>
        public bool EnableNetworkActivityMonitoring { get; set; } = true;

        /// <summary>
        /// Converts to a standard IdleDetectorConfiguration
        /// </summary>
        public IdleDetectorConfiguration ToIdleDetectorConfiguration()
        {
            return new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = DetectionIntervalSeconds,
                IdleThresholdSeconds = DefaultIdleThresholdMinutes * 60,
                ConfidenceThreshold = RequiredSuccessRate,
                IsEnabled = true,
                Priority = 20,
                MaxDetectionTimeMs = PingTimeoutMs * MaxPingAttempts,
                TimeoutSeconds = PingTimeoutMs / 1000,
                MaxConcurrentOperations = MaxConcurrentPings,
                CustomParameters = new Dictionary<string, object>
                {
                    ["PingTimeoutMs"] = PingTimeoutMs,
                    ["MaxConcurrentPings"] = MaxConcurrentPings,
                    ["RequiredSuccessRate"] = RequiredSuccessRate,
                    ["ActivityThresholdMinutes"] = ActivityThresholdMinutes,
                    ["MaxPingAttempts"] = MaxPingAttempts,
                    ["PingRetryDelayMs"] = PingRetryDelayMs,
                    ["EnableWindowsAPIChecking"] = EnableWindowsAPIChecking,
                    ["EnableDocumentActivityMonitoring"] = EnableDocumentActivityMonitoring,
                    ["EnableNetworkActivityMonitoring"] = EnableNetworkActivityMonitoring
                }
            };
        }
    }

    /// <summary>
    /// Represents the state of a process for ping detection
    /// </summary>
    public class ProcessPingState
    {
        public int ProcessId { get; set; }
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastDetectionTime { get; set; }
        public TimeSpan EstimatedIdleTime { get; set; }
        public bool WasIdle { get; set; }
        public DateTime? LastSuccessfulPing { get; set; }
        public long TotalDetections { get; set; }
        public long IdleDetections { get; set; }
        public long ActiveDetections { get; set; }
        public TimeSpan TotalIdleTime { get; set; }

        public ProcessPingState()
        {
            CreatedTime = DateTime.UtcNow;
            LastDetectionTime = DateTime.UtcNow;
            EstimatedIdleTime = TimeSpan.Zero;
            WasIdle = false;
            TotalDetections = 0;
            IdleDetections = 0;
            ActiveDetections = 0;
            TotalIdleTime = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Results from multi-factor ping detection
    /// </summary>
    public class PingDetectionResults
    {
        public ProcessPingResult ProcessPingResult { get; set; }
        public IList<DocumentActivityData> DocumentActivities { get; set; }
        public IList<NetworkActivityData> NetworkActivities { get; set; }
        public bool WindowsAPIAccessible { get; set; }
        public double SuccessRate { get; set; }
        public DateTime? LastSuccessfulPing { get; set; }
        public int FailedPingCount { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Analysis result from ping detection
    /// </summary>
    public class PingAnalysisResult
    {
        public bool IsIdle { get; set; }
        public TimeSpan EstimatedIdleTime { get; set; }
        public double Confidence { get; set; }
        public string PrimaryDetectionMethod { get; set; }
        public double SuccessRate { get; set; }
        public double AverageResponseTime { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Result from a single process ping
    /// </summary>
    public class ProcessPingResult
    {
        public int ProcessId { get; set; }
        public bool IsResponsive { get; set; }
        public int ResponseTimeMs { get; set; }
        public int TotalPingAttempts { get; set; }
        public int SuccessfulPingCount { get; set; }
        public int FailedPingCount { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public DateTime? LastSuccessfulPing { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Document activity data
    /// </summary>
    public class DocumentActivityData
    {
        public int ProcessId { get; set; }
        public string ActivityType { get; set; }
        public string DocumentPath { get; set; }
        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Network activity data
    /// </summary>
    public class NetworkActivityData
    {
        public int ProcessId { get; set; }
        public string ActivityType { get; set; }
        public string RemoteEndpoint { get; set; }
        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }
    }
}