using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Process;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Parsing;

namespace LicenseReleaseService
{
    public partial class LicenseReleaseService : ServiceBase
    {
        private readonly ServiceState _serviceState;
        private readonly HealthChecker _healthChecker;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly RecoveryManager _recoveryManager;
        private readonly ConfigurationManager _configurationManager;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _backgroundTask;
        private readonly PerformanceCounters _performanceCounters;
        private bool _configurationInitialized;

        // License management components
        private ServiceSettings _serviceSettings;
        private IProcessExecutor _processExecutor;
        private ProcessExecutionOptions _processOptions;
        private ProcessMetrics _processMetrics;
        private PerformanceMonitor _performanceMonitor;
        private Microsoft.Extensions.Logging.ILogger<LmutilLicenseManager> _licenseManagerLogger;
        private Microsoft.Extensions.Logging.ILogger<ProcessExecutor> _processExecutorLogger;
        private Microsoft.Extensions.Logging.ILogger<MonitoredLicenseManager> _monitoredLicenseManagerLogger;
        private ILicenseManager _licenseManager;
        private MonitoredLicenseManager _monitoredLicenseManager;
        private bool _licenseManagementInitialized;

        // License query engine components
        private readonly LicenseQueryOptions _licenseQueryOptions;
        private readonly Microsoft.Extensions.Logging.ILogger<LicenseQueryEngine> _queryEngineLogger;
        private readonly CacheOptions _cacheOptions;
        private ICacheManager _cacheManager;
        private readonly LmstatOutputParser _outputParser;
        private ILicenseQueryEngine _licenseQueryEngine;
        private bool _queryEngineInitialized;

        public LicenseReleaseService()
        {
            InitializeComponent();
            _serviceState = new ServiceState();
            _healthChecker = new HealthChecker();
            _logger = new EventLogLogger();
            _configurationManager = ConfigurationManager.Instance;
            _recoveryManager = new RecoveryManager(_serviceState, _logger);
            _performanceCounters = new PerformanceCounters();

            // Initialize license query engine components
            _licenseQueryOptions = CreateLicenseQueryOptionsFromSettings();
            _cacheOptions = CreateCacheOptionsFromSettings();
            _outputParser = new LmstatOutputParser();
            _queryEngineLogger = new Microsoft.Extensions.Logging.LoggerFactory()
                .CreateLogger<LicenseQueryEngine>();

            InitializeLicenseManagement();
            InitializeLicenseQueryEngine();
            InitializeService();
        }

        private LicenseQueryOptions CreateLicenseQueryOptionsFromSettings()
        {
            var options = LicenseQueryOptions.DefaultSolidWorksOptions();

            // Override with service settings
            if (_serviceSettings != null)
            {
                options.CacheExpiration = TimeSpan.FromSeconds(_serviceSettings.LicenseQueryCacheExpiration);
                options.QueryTimeout = TimeSpan.FromSeconds(_serviceSettings.LicenseQueryTimeout);
                options.MaxCacheSize = _serviceSettings.LicenseQueryMaxCacheSize;
                options.MaxConcurrentQueries = _serviceSettings.LicenseQueryMaxConcurrentQueries;
                options.EnableCaching = _serviceSettings.EnableLicenseQueryCaching;
                options.EnableStatistics = _serviceSettings.EnableLicenseQueryStatistics;
                options.EnableVerboseOutput = _serviceSettings.EnableLicenseQueryVerboseOutput;
                options.EnableHealthMonitoring = _serviceSettings.EnableLicenseQueryHealthMonitoring;
                options.EnablePerformanceMetrics = _serviceSettings.EnableLicenseQueryPerformanceMetrics;
                options.AlertThreshold = _serviceSettings.LicenseQueryAlertThreshold;
                options.HealthCheckInterval = _serviceSettings.LicenseQueryHealthCheckInterval;
                options.PerformanceMetricsInterval = _serviceSettings.LicenseQueryPerformanceMetricsInterval;
                options.CleanupInterval = _serviceSettings.LicenseQueryCleanupInterval;
            }

            return options;
        }

        private CacheOptions CreateCacheOptionsFromSettings()
        {
            var options = new CacheOptions();

            if (_serviceSettings != null)
            {
                options.DefaultExpiration = TimeSpan.FromSeconds(_serviceSettings.LicenseQueryCacheExpiration);
                options.EnableBackgroundCleanup = _serviceSettings.EnableLicenseQueryHealthMonitoring;
                options.EnableStatistics = _serviceSettings.EnableLicenseQueryStatistics;
                options.EnableDetailedLogging = _serviceSettings.EnableLicenseQueryVerboseOutput;
                options.BackgroundCleanupInterval = TimeSpan.FromSeconds(_serviceSettings.LicenseQueryCleanupInterval);
            }

            return options;
        }

        private void InitializeService()
        {
            ServiceName = "LicenseReleaseService";
            CanStop = true;
            CanPauseAndContinue = true;
            CanShutdown = true;
            CanHandlePowerEvent = true;
            CanHandleSessionChangeEvent = true;
            AutoLog = false;

            EventLog.Source = ServiceName;
            EventLog.Log = "Application";

            // Initialize configuration management
            InitializeConfiguration();
        }

        private void InitializeConfiguration()
        {
            try
            {
                _logger.LogInformation("Initializing configuration management");

                // Register configuration event handlers
                _configurationManager.ConfigurationChanged += OnConfigurationChanged;
                _configurationManager.ConfigurationFileChanged += OnConfigurationFileChanged;
                _configurationManager.ConfigurationHealthChanged += OnConfigurationHealthChanged;
                _configurationManager.ConfigurationBackupCompleted += OnConfigurationBackupCompleted;

                // Enable advanced features if configured
                var enableAdvancedFeatures = _configurationManager.Settings.EnableAdvancedFeatures;
                if (enableAdvancedFeatures)
                {
                    _configurationManager.EnableAdvancedFeatures();
                    _logger.LogInformation("Advanced configuration features enabled");
                }

                // Validate initial configuration
                var validationErrors = _configurationManager.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    var errorString = string.Join("; ", validationErrors);
                    _logger.LogWarning($"Initial configuration validation warnings: {errorString}");
                }

                _configurationInitialized = true;
                _logger.LogInformation("Configuration management initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize configuration management: {ex.Message}", ex);
                _configurationInitialized = false;
                throw;
            }
        }

        private void InitializeLicenseManagement()
        {
            try
            {
                _logger.LogInformation("Initializing license management components");

                // Initialize service settings
                _serviceSettings = new ServiceSettings(_configurationManager);

                // Initialize process execution options
                _processOptions = new ProcessExecutionOptions();

                // Initialize loggers for license management components
                _licenseManagerLogger = new Microsoft.Extensions.Logging.LoggerFactory()
                                        .CreateLogger<LmutilLicenseManager>();

                _processExecutorLogger = new Microsoft.Extensions.Logging.LoggerFactory()
                                        .CreateLogger<ProcessExecutor>();

                _monitoredLicenseManagerLogger = new Microsoft.Extensions.Logging.LoggerFactory()
                                        .CreateLogger<MonitoredLicenseManager>();

                // Initialize process executor
                _processExecutor = new ProcessExecutor(_processExecutorLogger, _processOptions);

                // Initialize process metrics
                _processMetrics = new ProcessMetrics(new Microsoft.Extensions.Logging.LoggerFactory()
                                        .CreateLogger<ProcessMetrics>(), 1000);

                // Initialize performance monitor
                _performanceMonitor = new LicenseManagement.PerformanceMonitor(new Microsoft.Extensions.Logging.LoggerFactory()
                                        .CreateLogger<LicenseManagement.PerformanceMonitor>(), _processMetrics);

                // Initialize license manager
                _licenseManager = new LmutilLicenseManager(
                    _processExecutor,
                    _licenseManagerLogger,
                    _serviceSettings,
                    _processOptions);

                // Initialize monitored license manager
                _monitoredLicenseManager = new MonitoredLicenseManager(
                    _licenseManager,
                    _monitoredLicenseManagerLogger,
                    _processMetrics,
                    _performanceMonitor,
                    new global::LicenseReleaseService.LicenseManagement.HealthChecker(
                    new Microsoft.Extensions.Logging.LoggerFactory()
                                                                        .CreateLogger<global::LicenseReleaseService.LicenseManagement.HealthChecker>(),
                    _licenseManager,
                    _processMetrics,
                    _performanceMonitor),
                    _serviceSettings);

                _licenseManagementInitialized = true;
                _logger.LogInformation("License management components initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize license management components: {ex.Message}", ex);
                _licenseManagementInitialized = false;
                throw;
            }
        }

        private void InitializeLicenseQueryEngine()
        {
            try
            {
                _logger.LogInformation("Initializing license query engine components");

                // Initialize cache manager
                _cacheManager = new MemoryCacheManager(_cacheOptions);

                // Initialize license query engine
                _licenseQueryEngine = new LicenseQueryEngine(
                    _cacheManager,
                    _processExecutor,
                    _outputParser);

                // Configure query engine options
                _licenseQueryEngine.Options = _licenseQueryOptions;

                // Validate query engine configuration
                var validationErrors = _licenseQueryEngine.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    var errorString = string.Join("; ", validationErrors);
                    _logger.LogWarning($"License query engine validation warnings: {errorString}");
                }

                _queryEngineInitialized = true;
                _logger.LogInformation("License query engine components initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize license query engine components: {ex.Message}", ex);
                _queryEngineInitialized = false;
                throw;
            }
        }

        protected override async void OnStart(string[] args)
        {
            try
            {
                _serviceState.ChangeState(ServiceStateStatus.Starting);
                _logger.LogInformation($"License Release Service starting with {args.Length} arguments");

                if (args.Length > 0)
                {
                    _logger.LogInformation($"Startup arguments: {string.Join(", ", args)}");
                }

                // Validate configuration before starting
                if (!ValidateConfigurationForStartup())
                {
                    throw new InvalidOperationException("Configuration validation failed. Service cannot start.");
                }

                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize service components
                InitializeServiceComponents();

                // Start health checker
                _healthChecker.StartPeriodicChecks();

                // Start performance counters
                _performanceCounters.Start();

                // Start background task for periodic operations
                _backgroundTask = Task.Run(() => BackgroundServiceLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                _serviceState.ChangeState(ServiceStateStatus.Running);
                _logger.LogInformation("License Release Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Service failed to start: {ex.Message}", ex);
                _serviceState.ChangeState(ServiceStateStatus.Error, ex.Message);
                _serviceState.RecordError($"Service failed to start: {ex.Message}", ex);

                // Attempt recovery
                var recoveryResult = await _recoveryManager.HandleExceptionAsync(ex, "Service startup");
                _logger.LogInformation($"Recovery result for startup failure: {recoveryResult.Message}");

                throw;
            }
        }

        protected override async void OnStop()
        {
            try
            {
                _serviceState.ChangeState(ServiceStateStatus.Stopping);
                _logger.LogInformation("License Release Service stopping");

                // Stop health checker
                _healthChecker.StopPeriodicChecks();

                // Stop performance counters
                _performanceCounters.Stop();

                // Signal cancellation to background task
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                }

                // Wait for background task to complete
                if (_backgroundTask != null)
                {
                    try
                    {
                        if (!_backgroundTask.Wait(TimeSpan.FromSeconds(30)))
                        {
                            _logger.LogWarning("Background task did not complete gracefully within timeout");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Background task was cancelled as expected");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error stopping background task: {ex.Message}", ex);
                    }
                }

                // Cleanup service components
                CleanupServiceComponents();

                // Cleanup configuration management
                CleanupConfiguration();

                _serviceState.ChangeState(ServiceStateStatus.Stopped);
                _logger.LogInformation("License Release Service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping service: {ex.Message}", ex);
                _serviceState.ChangeState(ServiceStateStatus.Error, ex.Message);
                _serviceState.RecordError($"Error stopping service: {ex.Message}", ex);

                // Attempt recovery
                var recoveryResult = await _recoveryManager.HandleExceptionAsync(ex, "Service shutdown");
                _logger.LogInformation($"Recovery result for shutdown failure: {recoveryResult.Message}");

                throw;
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _backgroundTask = null;
            }
        }

        protected override void OnPause()
        {
            try
            {
                _serviceState.ChangeState(ServiceStateStatus.Pausing);
                _logger.LogInformation("License Release Service pausing");

                // Pause specific service operations
                PauseServiceOperations();

                _serviceState.ChangeState(ServiceStateStatus.Paused);
                _logger.LogInformation("License Release Service paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error pausing service: {ex.Message}", ex);
                _serviceState.ChangeState(ServiceStateStatus.Error, ex.Message);
                _serviceState.RecordError($"Error pausing service: {ex.Message}", ex);
                throw;
            }
        }

        protected override void OnContinue()
        {
            try
            {
                _serviceState.ChangeState(ServiceStateStatus.Continuing);
                _logger.LogInformation("License Release Service continuing");

                // Resume service operations
                ResumeServiceOperations();

                _serviceState.ChangeState(ServiceStateStatus.Running);
                _logger.LogInformation("License Release Service resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error continuing service: {ex.Message}", ex);
                _serviceState.ChangeState(ServiceStateStatus.Error, ex.Message);
                _serviceState.RecordError($"Error continuing service: {ex.Message}", ex);
                throw;
            }
        }

        protected override void OnShutdown()
        {
            _logger.LogInformation("License Release Service shutdown requested");
            OnStop();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            _logger.LogInformation($"Power event received: {powerStatus}");
            return base.OnPowerEvent(powerStatus);
        }

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            _logger.LogInformation($"Session change event: {changeDescription.Reason} - Session ID: {changeDescription.SessionId}");
            base.OnSessionChange(changeDescription);
        }

        private async Task BackgroundServiceLoopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Background service loop started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Perform periodic health check
                        await PerformHealthCheckAsync();

                        // Process license releases (placeholder for actual implementation)
                        ProcessLicenseReleases(cancellationToken);

                        // Perform license query engine maintenance
                        await PerformLicenseQueryEngineMaintenanceAsync(cancellationToken);

                        // Log performance metrics periodically
                        var metrics = _performanceCounters.GetCurrentMetrics();
                        _logger.LogDebug($"Performance metrics - CPU: {metrics.Current.CpuUsage:F1}%, Memory: {metrics.Current.MemoryUsageMB:F1}MB");

                        // Wait for next iteration using configuration value
                        var config = _configurationManager?.CurrentConfiguration;
                        var interval = config?.HealthCheckIntervalSeconds > 0 ?
                            TimeSpan.FromSeconds(config.HealthCheckIntervalSeconds) :
                            TimeSpan.FromMinutes(1);
                        await Task.Delay(interval, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in background service loop: {ex.Message}", ex);

                        // Attempt recovery
                        var recoveryResult = await _recoveryManager.HandleExceptionAsync(ex, "Background service loop");
                        _logger.LogInformation($"Recovery result for background loop error: {recoveryResult.Message}");

                        // Continue running despite errors, unless recovery suggests shutdown
                        if (recoveryResult.Action == RecoveryAction.GracefulShutdown)
                        {
                            _logger.LogWarning("Recovery suggests graceful shutdown, stopping background loop");
                            break;
                        }
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Background service loop ended");
            }
        }

        private bool ValidateConfigurationForStartup()
        {
            try
            {
                if (!_configurationInitialized)
                {
                    _logger.LogError("Configuration management is not initialized");
                    throw new ConfigurationInitializationException("Configuration management is not initialized");
                }

                if (!_licenseManagementInitialized)
                {
                    _logger.LogError("License management is not initialized");
                    throw new InvalidOperationException("License management is not initialized");
                }

                if (!_queryEngineInitialized)
                {
                    _logger.LogError("License query engine is not initialized");
                    throw new InvalidOperationException("License query engine is not initialized");
                }

                var validationErrors = _configurationManager.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    var errorString = string.Join("; ", validationErrors);
                    _logger.LogError($"Configuration validation failed: {errorString}");
                    throw new ConfigurationValidationException($"Configuration validation failed: {errorString}");
                }

                // Validate query engine configuration
                var queryEngineErrors = _licenseQueryEngine.ValidateConfiguration();
                if (queryEngineErrors.Count > 0)
                {
                    var errorString = string.Join("; ", queryEngineErrors);
                    _logger.LogError($"Query engine configuration validation failed: {errorString}");
                    throw new InvalidOperationException($"Query engine configuration validation failed: {errorString}");
                }

                // Check if advanced features are enabled and healthy
                if (_configurationManager.AdvancedFeaturesEnabled)
                {
                    var configHealth = _configurationManager.HealthStatus;
                    if (configHealth == ConfigurationHealthStatus.Error)
                    {
                        _logger.LogError("Configuration health check failed - advanced features are in error state");
                        throw new ConfigurationHealthException("Configuration health check failed - advanced features are in error state");
                    }
                }

                _logger.LogInformation("Configuration validation passed for service startup");
                return true;
            }
            catch (ConfigurationInitializationException ex)
            {
                _logger.LogError($"Configuration initialization error: {ex.Message}", ex);
                throw;
            }
            catch (ConfigurationValidationException ex)
            {
                _logger.LogError($"Configuration validation error: {ex.Message}", ex);
                throw;
            }
            catch (ConfigurationHealthException ex)
            {
                _logger.LogError($"Configuration health error: {ex.Message}", ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Configuration validation exception: {ex.Message}", ex);
                throw new ConfigurationInitializationException($"Configuration validation exception: {ex.Message}", ex);
            }
        }

        private void InitializeServiceComponents()
        {
            _logger.LogInformation("Initializing service components");

            try
            {
                // Initialize license query engine components
                if (_queryEngineInitialized && _licenseQueryEngine != null)
                {
                    _logger.LogInformation("License query engine is already initialized");
                }
                else
                {
                    _logger.LogWarning("License query engine is not initialized, attempting re-initialization");
                    InitializeLicenseQueryEngine();
                }

                // Validate query engine is operational
                if (_licenseQueryEngine != null)
                {
                    var validationErrors = _licenseQueryEngine.ValidateConfiguration();
                    if (validationErrors.Count > 0)
                    {
                        var errorString = string.Join("; ", validationErrors);
                        _logger.LogWarning($"Query engine has validation warnings: {errorString}");
                    }
                }

                _logger.LogInformation("Service components initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize service components: {ex.Message}", ex);
                throw;
            }
        }

        private void CleanupServiceComponents()
        {
            _logger.LogInformation("Cleaning up service components");

            try
            {
                // Cleanup license query engine components
                if (_licenseQueryEngine != null)
                {
                    _logger.LogInformation("Cleaning up license query engine");

                    // Clear cache before shutdown
                    try
                    {
                        _licenseQueryEngine.ClearAllCacheAsync().Wait(TimeSpan.FromSeconds(5));
                        _logger.LogInformation("License query engine cache cleared");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to clear query engine cache: {ex.Message}");
                    }

                    // Reset performance metrics
                    try
                    {
                        _licenseQueryEngine.ResetPerformanceMetrics();
                        _logger.LogInformation("License query engine performance metrics reset");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to reset query engine metrics: {ex.Message}");
                    }
                }

                // Cleanup cache manager
                if (_cacheManager is IDisposable disposableCache)
                {
                    try
                    {
                        disposableCache.Dispose();
                        _logger.LogInformation("Cache manager disposed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to dispose cache manager: {ex.Message}");
                    }
                }

                _queryEngineInitialized = false;
                _logger.LogInformation("Service components cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning up service components: {ex.Message}", ex);
            }
        }

        private void CleanupConfiguration()
        {
            try
            {
                _logger.LogInformation("Cleaning up configuration management");

                // Unregister event handlers
                _configurationManager.ConfigurationChanged -= OnConfigurationChanged;
                _configurationManager.ConfigurationFileChanged -= OnConfigurationFileChanged;
                _configurationManager.ConfigurationHealthChanged -= OnConfigurationHealthChanged;
                _configurationManager.ConfigurationBackupCompleted -= OnConfigurationBackupCompleted;

                // Disable advanced features
                if (_configurationManager.AdvancedFeaturesEnabled)
                {
                    _configurationManager.DisableAdvancedFeatures();
                }

                _configurationInitialized = false;
                _logger.LogInformation("Configuration management cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning up configuration management: {ex.Message}", ex);
            }
        }

        private void PauseServiceOperations()
        {
            _logger.LogInformation("Pausing service operations");
            // Placeholder for pausing specific operations
        }

        private void ResumeServiceOperations()
        {
            _logger.LogInformation("Resuming service operations");
            // Placeholder for resuming specific operations
        }

        private async Task PerformHealthCheckAsync()
        {
            try
            {
                var healthReport = await _healthChecker.GetHealthReportAsync();
                _logger.LogInformation($"Health check completed - Status: {healthReport.OverallStatus.ToFriendlyString()}");

                if (healthReport.OverallStatus == HealthStatus.Unhealthy)
                {
                    _logger.LogWarning("Service health check indicates unhealthy state");
                }

                // Perform license query engine health check
                await PerformLicenseQueryEngineHealthCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Health check failed: {ex.Message}", ex);
            }
        }

        private void ProcessLicenseReleases(CancellationToken cancellationToken)
        {
            // Placeholder for license release processing
            // This will be implemented in future tasks
        }

        private async Task PerformLicenseQueryEngineMaintenanceAsync(CancellationToken cancellationToken)
        {
            if (!_queryEngineInitialized || _licenseQueryEngine == null)
            {
                return;
            }

            try
            {
                // Perform cache cleanup if needed
                if (_licenseQueryOptions.EnableAutoCleanup)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken); // Small delay to avoid contention
                        _logger.LogDebug("License query engine maintenance completed");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                    }
                }

                // Log query engine metrics periodically
                if (_licenseQueryOptions.EnablePerformanceMetrics)
                {
                    try
                    {
                        var metrics = _licenseQueryEngine.GetPerformanceMetrics();
                        if (metrics.TotalQueries > 0)
                        {
                            _logger.LogDebug($"Query engine metrics - Total: {metrics.TotalQueries}, " +
                                $"Success: {metrics.SuccessfulQueries}, Cached: {metrics.CachedQueries}, " +
                                $"AvgTime: {metrics.AverageQueryTimeMs:F2}ms, HitRatio: {metrics.CacheHitRatio:P2}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to get query engine metrics: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during license query engine maintenance: {ex.Message}");
            }
        }

        private async Task PerformLicenseQueryEngineHealthCheckAsync()
        {
            if (!_queryEngineInitialized || _licenseQueryEngine == null)
            {
                _logger.LogWarning("License query engine health check skipped - not initialized");
                return;
            }

            try
            {
                // Validate query engine configuration
                var validationErrors = _licenseQueryEngine.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    _logger.LogWarning($"License query engine has configuration issues: {string.Join("; ", validationErrors)}");
                }

                // Check cache manager health
                if (_cacheManager != null)
                {
                    try
                    {
                        // Test cache operations
                        var testKey = "health_check_test";
                        var testValue = DateTime.Now.ToString();

                        _cacheManager.Set(testKey, testValue, TimeSpan.FromSeconds(30));
                        var retrievedValue = _cacheManager.Get<string>(testKey);

                        if (retrievedValue != testValue)
                        {
                            _logger.LogWarning("Cache manager health check failed - value mismatch");
                        }
                        else
                        {
                            _cacheManager.Remove(testKey);
                            _logger.LogDebug("Cache manager health check passed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Cache manager health check failed: {ex.Message}");
                    }
                }

                // Check output parser health
                if (_outputParser != null)
                {
                    try
                    {
                        // Test parser with sample lmstat output
                        var sampleOutput = @"License server status: test-server@27000
Users of solidworks: (Total of 10 licenses issued; Total of 5 licenses in use)
""user1"" workstation1 (v2023.0) (start/hostname1)
""user2"" workstation2 (v2023.0) (start/hostname2)";

                        var parsedStatus = _outputParser.ParseLmstatOutput(sampleOutput, "test-server@27000");
                        if (parsedStatus != null && parsedStatus.FeatureDetails.Count > 0)
                        {
                            _logger.LogDebug("Output parser health check passed");
                        }
                        else
                        {
                            _logger.LogWarning("Output parser health check failed - no features parsed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Output parser health check failed: {ex.Message}");
                    }
                }

                // Check process executor health
                if (_processExecutor != null)
                {
                    try
                    {
                        // Test process executor with a simple command
                        var result = await _processExecutor.ExecuteAsync("cmd", "/c echo health_check", TimeSpan.FromSeconds(5));
                        if (result.Success && result.Output.Contains("health_check"))
                        {
                            _logger.LogDebug("Process executor health check passed");
                        }
                        else
                        {
                            _logger.LogWarning("Process executor health check failed - command execution failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Process executor health check failed: {ex.Message}");
                    }
                }

                _logger.LogDebug("License query engine health check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"License query engine health check failed: {ex.Message}", ex);
                _serviceState.RecordError($"License query engine health check failed: {ex.Message}", ex);
            }
        }

        // License Query Engine Public API Methods
        /// <summary>
        /// Gets the current license query engine instance
        /// </summary>
        /// <returns>License query engine instance or null if not initialized</returns>
        public ILicenseQueryEngine GetLicenseQueryEngine()
        {
            return _licenseQueryEngine;
        }

        /// <summary>
        /// Gets the current performance metrics for the license query engine
        /// </summary>
        /// <returns>Performance metrics or null if query engine is not available</returns>
        public LicenseQueryMetrics GetLicenseQueryEngineMetrics()
        {
            try
            {
                return _licenseQueryEngine?.GetPerformanceMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get license query engine metrics: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the current cache statistics for the license query engine
        /// </summary>
        /// <returns>Cache statistics or null if cache manager is not available</returns>
        public global::LicenseReleaseService.LicenseManagement.Caching.CacheStatistics GetCacheStatistics()
        {
            try
            {
                return _cacheManager?.Statistics;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get cache statistics: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears the license query engine cache
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> ClearLicenseQueryCacheAsync()
        {
            try
            {
                if (_licenseQueryEngine != null)
                {
                    await _licenseQueryEngine.ClearAllCacheAsync();
                    _logger.LogInformation("License query engine cache cleared successfully");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to clear license query engine cache: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Resets the license query engine performance metrics
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool ResetLicenseQueryMetrics()
        {
            try
            {
                _licenseQueryEngine?.ResetPerformanceMetrics();
                _logger.LogInformation("License query engine metrics reset successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to reset license query engine metrics: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Performs a comprehensive health check of the license query engine
        /// </summary>
        /// <returns>Health check result</returns>
        public async Task<Dictionary<string, object>> GetLicenseQueryEngineHealthAsync()
        {
            var healthResult = new Dictionary<string, object>();

            try
            {
                // Basic status
                healthResult["Initialized"] = _queryEngineInitialized;
                healthResult["EngineAvailable"] = _licenseQueryEngine != null;
                healthResult["CacheAvailable"] = _cacheManager != null;
                healthResult["ParserAvailable"] = _outputParser != null;

                if (_licenseQueryEngine != null)
                {
                    // Configuration validation
                    var validationErrors = _licenseQueryEngine.ValidateConfiguration();
                    healthResult["ConfigurationValid"] = validationErrors.Count == 0;
                    healthResult["ConfigurationErrors"] = validationErrors;

                    // Performance metrics
                    var metrics = _licenseQueryEngine.GetPerformanceMetrics();
                    healthResult["TotalQueries"] = metrics.TotalQueries;
                    healthResult["SuccessfulQueries"] = metrics.SuccessfulQueries;
                    healthResult["FailedQueries"] = metrics.FailedQueries;
                    healthResult["CachedQueries"] = metrics.CachedQueries;
                    healthResult["SuccessRate"] = metrics.SuccessRate;
                    healthResult["CacheHitRatio"] = metrics.CacheHitRatio;
                    healthResult["AverageQueryTimeMs"] = metrics.AverageQueryTimeMs;
                }

                if (_cacheManager != null)
                {
                    // Cache statistics
                    var cacheStats = _cacheManager.Statistics;
                    healthResult["CacheItemCount"] = cacheStats.CurrentItemCount;
                    healthResult["CacheHitCount"] = cacheStats.CacheHits;
                    healthResult["CacheMissCount"] = cacheStats.CacheMisses;
                    healthResult["CacheHitRate"] = cacheStats.HitRatio;
                    healthResult["CacheTotalSize"] = cacheStats.TotalMemoryBytesUsed;
                }

                healthResult["LastChecked"] = DateTime.Now;
                healthResult["Healthy"] = _queryEngineInitialized && _licenseQueryEngine != null;

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get license query engine health: {ex.Message}", ex);
                healthResult["Error"] = ex.Message;
                healthResult["Healthy"] = false;
                return healthResult;
            }
        }

        // Configuration event handlers
        private void OnConfigurationChanged(object sender, ConfigurationReloadEventArgs e)
        {
            try
            {
                _logger.LogInformation($"Configuration reloaded - Success: {e.IsSuccess}");

                if (e.IsSuccess)
                {
                    // Update service behavior based on new configuration
                    UpdateServiceBehaviorForConfiguration(e.NewConfiguration);

                    // Log configuration changes
                    LogConfigurationChanges(e.OldConfiguration, e.NewConfiguration);
                }
                else
                {
                    _logger.LogWarning($"Configuration reload failed: {string.Join(", ", e.ValidationErrors)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling configuration change: {ex.Message}", ex);
            }
        }

        private void OnConfigurationFileChanged(object sender, ConfigurationFileChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation($"Configuration file changed: {e.ChangeType} - {e.FilePath}");

                // Update service state to reflect file change
                _serviceState.RecordError($"Configuration file {e.ChangeType}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling configuration file change: {ex.Message}", ex);
            }
        }

        private void OnConfigurationHealthChanged(object sender, ConfigurationHealthEventArgs e)
        {
            try
            {
                _logger.LogInformation($"Configuration health changed: {e.HealthStatus}");

                // Update service health based on configuration health
                if (e.HealthStatus == ConfigurationHealthStatus.Error)
                {
                    _serviceState.RecordError($"Configuration health error: {string.Join(", ", e.HealthIssues)}", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling configuration health change: {ex.Message}", ex);
            }
        }

        private void OnConfigurationBackupCompleted(object sender, ConfigurationBackupEventArgs e)
        {
            try
            {
                _logger.LogInformation($"Configuration backup completed: {e.OperationType} - Success: {e.IsSuccess}");

                if (!e.IsSuccess)
                {
                    _logger.LogWarning($"Configuration backup failed: {e.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling configuration backup completion: {ex.Message}", ex);
            }
        }

        private void UpdateServiceBehaviorForConfiguration(LicenseReleaseServiceSection newConfig)
        {
            try
            {
                // Update service intervals and settings based on configuration
                var healthCheckInterval = newConfig.HealthCheckIntervalSeconds;
                var logLevel = newConfig.Logging.LogLevel;
                var enableAdvancedFeatures = newConfig.AdvancedFeatures.EnableAdvancedMonitoring;
                var enableDiagnostics = newConfig.AdvancedFeatures.EnableDiagnostics;
                var backupEnabled = newConfig.AdvancedFeatures.EnableAutomaticBackups;

                // Update health checker interval if it's different
                if (healthCheckInterval > 0 && healthCheckInterval != 60) // Default is 60 seconds
                {
                    _logger.LogInformation($"Health check interval updated to {healthCheckInterval}s");
                }

                // Handle advanced features configuration
                if (enableAdvancedFeatures && !_configurationManager.AdvancedFeaturesEnabled)
                {
                    _configurationManager.EnableAdvancedFeatures();
                    _logger.LogInformation("Advanced features enabled via configuration");
                }
                else if (!enableAdvancedFeatures && _configurationManager.AdvancedFeaturesEnabled)
                {
                    _configurationManager.DisableAdvancedFeatures();
                    _logger.LogInformation("Advanced features disabled via configuration");
                }

                // Update logging behavior based on configuration
                UpdateLoggingBehavior(logLevel);

                // Update diagnostics settings
                if (enableDiagnostics)
                {
                    _logger.LogInformation("Diagnostics enabled via configuration");
                }

                // Update backup settings
                if (backupEnabled)
                {
                    _logger.LogInformation("Automatic backups enabled via configuration");
                }

                _logger.LogInformation($"Service behavior updated - Health check interval: {healthCheckInterval}s, Log level: {logLevel}, Advanced features: {enableAdvancedFeatures}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating service behavior for configuration: {ex.Message}", ex);
            }
        }

        private void UpdateLoggingBehavior(string logLevel)
        {
            try
            {
                // Update logging behavior based on configuration
                _logger.LogInformation($"Log level updated to: {logLevel}");

                // In a real implementation, this would update the actual logging framework
                // For now, we just log the change
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating logging behavior: {ex.Message}", ex);
            }
        }

        private void LogConfigurationChanges(LicenseReleaseServiceSection oldConfig, LicenseReleaseServiceSection newConfig)
        {
            try
            {
                // Log specific configuration changes
                if (oldConfig.HealthCheckIntervalSeconds != newConfig.HealthCheckIntervalSeconds)
                {
                    _logger.LogInformation($"Health check interval changed from {oldConfig.HealthCheckIntervalSeconds}s to {newConfig.HealthCheckIntervalSeconds}s");
                }

                if (oldConfig.Logging.LogLevel != newConfig.Logging.LogLevel)
                {
                    _logger.LogInformation($"Log level changed from {oldConfig.Logging.LogLevel} to {newConfig.Logging.LogLevel}");
                }

                if (oldConfig.AdvancedFeatures.EnableAdvancedMonitoring != newConfig.AdvancedFeatures.EnableAdvancedMonitoring)
                {
                    _logger.LogInformation($"Advanced monitoring changed from {oldConfig.AdvancedFeatures.EnableAdvancedMonitoring} to {newConfig.AdvancedFeatures.EnableAdvancedMonitoring}");
                }

                if (oldConfig.AdvancedFeatures.EnableDiagnostics != newConfig.AdvancedFeatures.EnableDiagnostics)
                {
                    _logger.LogInformation($"Diagnostics changed from {oldConfig.AdvancedFeatures.EnableDiagnostics} to {newConfig.AdvancedFeatures.EnableDiagnostics}");
                }

                if (oldConfig.AdvancedFeatures.EnableAutomaticBackups != newConfig.AdvancedFeatures.EnableAutomaticBackups)
                {
                    _logger.LogInformation($"Automatic backups changed from {oldConfig.AdvancedFeatures.EnableAutomaticBackups} to {newConfig.AdvancedFeatures.EnableAutomaticBackups}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error logging configuration changes: {ex.Message}", ex);
            }
        }

        public ServiceStateStatus CurrentState => _serviceState.Status;

        public bool IsHealthy => _healthChecker.OverallStatus.IsOperational();

        public ServiceMetrics GetServiceMetrics() => _serviceState.GetMetrics();

        public async Task<HealthReport> GetHealthReportAsync() => await _healthChecker.GetHealthReportAsync();

        public PerformanceMetrics GetPerformanceMetrics() => _performanceCounters?.GetCurrentMetrics() ?? new PerformanceMetrics();

        // Public methods for console mode
        public void StartConsoleMode(string[] args)
        {
            OnStart(args);
        }

        public void StopConsoleMode()
        {
            OnStop();
        }
    }

    public class EventLogLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly ServiceState _serviceState;
        private readonly HealthChecker _healthChecker;
        private readonly PerformanceMonitor _performanceCounters;
        private readonly RecoveryManager _recoveryManager;

        public EventLogLogger()
        {
            // Simple constructor for basic logging
            _serviceState = null;
            _healthChecker = null;
            _performanceCounters = null;
            _recoveryManager = null;
        }

        public EventLogLogger(ServiceState serviceState, HealthChecker healthChecker, PerformanceMonitor performanceCounters, RecoveryManager recoveryManager)
        {
            _serviceState = serviceState;
            _healthChecker = healthChecker;
            _performanceCounters = performanceCounters;
            _recoveryManager = recoveryManager;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null; // Simple implementation
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true; // Enable all logging
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter != null)
            {
                var message = formatter(state, exception);
                EventLogEntryType entryType = ConvertLogLevelToEventLogEntryType(logLevel);
                EventLog.WriteEntry("LicenseReleaseService", message, entryType);
            }
        }

        private EventLogEntryType ConvertLogLevelToEventLogEntryType(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    return EventLogEntryType.Error;
                case LogLevel.Warning:
                    return EventLogEntryType.Warning;
                case LogLevel.Information:
                case LogLevel.Debug:
                case LogLevel.Trace:
                default:
                    return EventLogEntryType.Information;
            }
        }

        
        // Enhanced methods for interactive console mode
        public string GetServiceStatus()
        {
            if (_serviceState == null)
                return "Service status unavailable - ServiceState not initialized";

            var state = _serviceState.Status;
            global::LicenseReleaseService.LicenseManagement.HealthStatus health;
            if (_healthChecker != null)
            {
                // Need to convert between different HealthStatus enums
                var sourceStatus = _healthChecker.OverallStatus;
                health = sourceStatus switch
                {
                    HealthStatus.Healthy => global::LicenseReleaseService.LicenseManagement.HealthStatus.Healthy,
                    HealthStatus.Degraded => global::LicenseReleaseService.LicenseManagement.HealthStatus.Warning,
                    HealthStatus.Unhealthy => global::LicenseReleaseService.LicenseManagement.HealthStatus.Unhealthy,
                    HealthStatus.Unknown => global::LicenseReleaseService.LicenseManagement.HealthStatus.Critical,
                    _ => global::LicenseReleaseService.LicenseManagement.HealthStatus.Healthy
                };
            }
            else
            {
                health = global::LicenseReleaseService.LicenseManagement.HealthStatus.Healthy;
            }
            var metrics = _serviceState.GetMetrics();

            return $"Service Status: {state.ToFriendlyString()}\n" +
                   $"Health Status: {health}\n" +
                   $"Uptime: {metrics.TotalRunTime:hh\\:mm\\:ss}\n" +
                   $"Current State Duration: {metrics.CurrentStateDuration:hh\\:mm\\:ss}\n" +
                   $"Error Count: {metrics.ErrorCount}";
        }

        public async Task<string> GetDetailedHealthReportAsync()
        {
            try
            {
                if (_healthChecker == null)
                    return "Health report unavailable - HealthChecker not initialized";

                var report = await _healthChecker.GetHealthReportAsync();
                var details = $"Overall Health: {report.OverallStatus.ToFriendlyString()}\n" +
                             $"Last Health Check: {report.LastHealthCheck:yyyy-MM-dd HH:mm:ss}\n" +
                             $"Health Checks: {report.HealthChecks.Count}\n" +
                             $"Metrics: {report.Metrics.Count}\n\n";

                foreach (var check in report.HealthChecks.Values)
                {
                    details += $"  {check.Name}: {check.Status.ToFriendlyString()}";
                    if (!string.IsNullOrEmpty(check.Description))
                    {
                        details += $" - {check.Description}";
                    }
                    details += "\n";
                }

                return details;
            }
            catch (Exception ex)
            {
                return $"Error getting health report: {ex.Message}";
            }
        }

        public string GetPerformanceSummary()
        {
            try
            {
                var metrics = _performanceCounters.GetSystemPerformanceCounters();
                var current = metrics;

                return $"Performance Summary:\n" +
                       $"  CPU Usage: {current.CpuUsage:F1}%\n" +
                       $"  Memory Usage: {current.MemoryUsage:F1}%\n" +
                       $"  Available Memory: {(current.AvailableMemory / 1024 / 1024):F1} MB\n" +
                       $"  Total Memory: {(current.TotalMemory / 1024 / 1024):F1} MB\n" +
                       $"  Active Processes: {current.ActiveProcesses}\n" +
                       $"  Active Threads: {current.ActiveThreads}\n" +
                       $"  Handle Count: {current.HandleCount}\n" +
                       $"  Disk Usage: {current.DiskUsage:F1}%\n" +
                       $"  Network I/O: {(current.NetworkBytes / 1024):F1} KB";
            }
            catch (Exception ex)
            {
                return $"Error getting performance summary: {ex.Message}";
            }
        }

        public string GetRecoveryStatus()
        {
            try
            {
                if (_recoveryManager == null)
                    return "Recovery status unavailable - RecoveryManager not initialized";

                var inProgress = _recoveryManager.RecoveryInProgress;
                var consecutiveErrors = _recoveryManager.ConsecutiveErrors;
                var history = _recoveryManager.GetRecoveryHistory(5);

                var status = $"Recovery Status:\n" +
                           $"  Recovery In Progress: {inProgress}\n" +
                           $"  Consecutive Errors: {consecutiveErrors}\n" +
                           $"  Recovery Events (Last 5): {history.Count}\n";

                if (history.Count > 0)
                {
                    status += "\nRecent Recovery Events:\n";
                    foreach (var evt in history)
                    {
                        status += $"  [{evt.Timestamp:HH:mm:ss}] {evt.ExceptionType}: {evt.RecoveryAction.ToFriendlyString()} - {(evt.Success ? "Success" : "Failed")}\n";
                    }
                }

                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting recovery status: {ex.Message}";
            }
        }
    }

    // Configuration-specific exception classes
    public class ConfigurationInitializationException : Exception
    {
        public ConfigurationInitializationException() : base() { }
        public ConfigurationInitializationException(string message) : base(message) { }
        public ConfigurationInitializationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ConfigurationValidationException : Exception
    {
        public ConfigurationValidationException() : base() { }
        public ConfigurationValidationException(string message) : base(message) { }
        public ConfigurationValidationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ConfigurationHealthException : Exception
    {
        public ConfigurationHealthException() : base() { }
        public ConfigurationHealthException(string message) : base(message) { }
        public ConfigurationHealthException(string message, Exception innerException) : base(message, innerException) { }
    }
}