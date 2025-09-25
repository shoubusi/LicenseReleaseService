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

namespace LicenseReleaseService
{
    public partial class LicenseReleaseService : ServiceBase
    {
        private readonly object _stateLock = new object();
        private ServiceState _currentState = ServiceState.Stopped;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _backgroundTask;
        private readonly ILogger _logger;
        private DateTime _lastHealthCheck = DateTime.MinValue;
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(5);

        public enum ServiceState
        {
            Stopped,
            Starting,
            Running,
            Stopping,
            Paused,
            Pausing,
            Continuing
        }

        public LicenseReleaseService()
        {
            InitializeComponent();
            _logger = new EventLogLogger();
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
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                UpdateState(ServiceState.Starting);
                _logger.LogInformation($"License Release Service starting with {args.Length} arguments");

                if (args.Length > 0)
                {
                    _logger.LogInformation($"Startup arguments: {string.Join(", ", args)}");
                }

                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize service components
                InitializeServiceComponents();

                // Start background task for periodic operations
                _backgroundTask = Task.Run(() => BackgroundServiceLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                UpdateState(ServiceState.Running);
                _logger.LogInformation("License Release Service started successfully");

                // Set initial health check
                _lastHealthCheck = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Service failed to start: {ex.Message}", ex);
                UpdateState(ServiceState.Stopped);
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                UpdateState(ServiceState.Stopping);
                _logger.LogInformation("License Release Service stopping");

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

                UpdateState(ServiceState.Stopped);
                _logger.LogInformation("License Release Service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping service: {ex.Message}", ex);
                UpdateState(ServiceState.Stopped);
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
                UpdateState(ServiceState.Pausing);
                _logger.LogInformation("License Release Service pausing");

                // Pause specific service operations
                PauseServiceOperations();

                UpdateState(ServiceState.Paused);
                _logger.LogInformation("License Release Service paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error pausing service: {ex.Message}", ex);
                throw;
            }
        }

        protected override void OnContinue()
        {
            try
            {
                UpdateState(ServiceState.Continuing);
                _logger.LogInformation("License Release Service continuing");

                // Resume service operations
                ResumeServiceOperations();

                UpdateState(ServiceState.Running);
                _logger.LogInformation("License Release Service resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error continuing service: {ex.Message}", ex);
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

        private void BackgroundServiceLoop(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Background service loop started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Perform periodic health check
                        PerformHealthCheck();

                        // Process license releases (placeholder for actual implementation)
                        ProcessLicenseReleases(cancellationToken);

                        // Wait for next iteration
                        Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).Wait(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in background service loop: {ex.Message}", ex);
                        // Continue running despite errors
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Background service loop ended");
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

        private void PerformHealthCheck()
        {
            if (DateTime.UtcNow - _lastHealthCheck > _healthCheckInterval)
            {
                try
                {
                    // Perform health check operations
                    _logger.LogInformation("Performing service health check");

                    // Update health check timestamp
                    _lastHealthCheck = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Health check failed: {ex.Message}", ex);
                }
            }
        }

        private void ProcessLicenseReleases(CancellationToken cancellationToken)
        {
            // Placeholder for license release processing
            // This will be implemented in future tasks
        }

        private void UpdateState(ServiceState newState)
        {
            lock (_stateLock)
            {
                _currentState = newState;
                _logger.LogInformation($"Service state changed to: {newState}");
            }
        }

        public ServiceState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }

        public bool IsHealthy
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState == ServiceState.Running &&
                           DateTime.UtcNow - _lastHealthCheck < TimeSpan.FromMinutes(10);
                }
            }
        }

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

        public void LogError(string message, Exception exception = null)
        {
            var fullMessage = exception != null ? $"{message}\nException: {exception}\nStackTrace: {exception.StackTrace}" : message;
            EventLog.WriteEntry("LicenseReleaseService", fullMessage, EventLogEntryType.Error);
        }
    }
}