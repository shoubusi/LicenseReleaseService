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
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService
{
    public partial class LicenseReleaseService : ServiceBase
    {
        private readonly ServiceState _serviceState;
        private readonly HealthChecker _healthChecker;
        private readonly ILogger _logger;
        private readonly RecoveryManager _recoveryManager;
        private readonly ConfigurationManager _configurationManager;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _backgroundTask;
        private readonly PerformanceCounters _performanceCounters;
        private bool _configurationInitialized;

        public LicenseReleaseService()
        {
            InitializeComponent();
            _logger = new EventLogLogger();
            _serviceState = new ServiceState();
            _healthChecker = new HealthChecker();
            _configurationManager = ConfigurationManager.Instance;
            _recoveryManager = new RecoveryManager(_serviceState, _logger);
            _performanceCounters = new PerformanceCounters();
            InitializeService();
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

                var validationErrors = _configurationManager.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    var errorString = string.Join("; ", validationErrors);
                    _logger.LogError($"Configuration validation failed: {errorString}");
                    throw new ConfigurationValidationException($"Configuration validation failed: {errorString}");
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
            // Placeholder for initializing service components
        }

        private void CleanupServiceComponents()
        {
            _logger.LogInformation("Cleaning up service components");
            // Placeholder for cleaning up service components
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
                    _serviceState.RecordError($"Configuration health error: {string.Join(", ", e.Issues)}", null);
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

        public PerformanceMetrics GetPerformanceMetrics() => _performanceCounters.GetCurrentMetrics();

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

    public interface ILogger
    {
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
    }

    public class EventLogLogger : ILogger
    {
        public void LogInformation(string message)
        {
            EventLog.WriteEntry("LicenseReleaseService", message, EventLogEntryType.Information);
        }

        public void LogWarning(string message)
        {
            EventLog.WriteEntry("LicenseReleaseService", message, EventLogEntryType.Warning);
        }

        // Enhanced methods for interactive console mode
        public string GetServiceStatus()
        {
            var state = _serviceState.Status;
            var health = _healthChecker.OverallStatus;
            var metrics = _serviceState.GetMetrics();

            return $"Service Status: {state.ToFriendlyString()}\n" +
                   $"Health Status: {health.ToFriendlyString()}\n" +
                   $"Uptime: {metrics.TotalRunTime:hh\\:mm\\:ss}\n" +
                   $"Current State Duration: {metrics.CurrentStateDuration:hh\\:mm\\:ss}\n" +
                   $"Error Count: {metrics.ErrorCount}";
        }

        public async Task<string> GetDetailedHealthReportAsync()
        {
            try
            {
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
                var metrics = _performanceCounters.GetCurrentMetrics();
                var current = metrics.Current;

                return $"Performance Summary:\n" +
                       $"  CPU Usage: {current.CpuUsage:F1}%\n" +
                       $"  Memory Usage: {current.MemoryUsageMB:F1} MB\n" +
                       $"  Private Memory: {current.MemoryUsageMBPrivate:F1} MB\n" +
                       $"  Thread Count: {current.ThreadCount}\n" +
                       $"  Handle Count: {current.HandleCount}\n" +
                       $"  Uptime: {current.Uptime:F0} seconds\n" +
                       $"  GC Collections (Gen0/1/2): {current.GcGeneration0}/{current.GcGeneration1}/{current.GcGeneration2}\n" +
                       $"  Sample Count: {metrics.SampleCount}";
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

        public void LogError(string message, Exception exception = null)
        {
            var fullMessage = exception != null ? $"{message}\nException: {exception}\nStackTrace: {exception.StackTrace}" : message;
            EventLog.WriteEntry("LicenseReleaseService", fullMessage, EventLogEntryType.Error);
        }

        // Additional methods needed for configuration integration
        public void LogInfo(string message)
        {
            EventLog.WriteEntry("LicenseReleaseService", message, EventLogEntryType.Information);
        }

        public void LogWarning(string message)
        {
            EventLog.WriteEntry("LicenseReleaseService", message, EventLogEntryType.Warning);
        }

        public void LogDebug(string message)
        {
            EventLog.WriteEntry("LicenseReleaseService", message, EventLogEntryType.Information);
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