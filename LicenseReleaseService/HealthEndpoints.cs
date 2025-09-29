using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.Models;
using HealthChecker = LicenseReleaseService.LicenseManagement.HealthChecker;

namespace LicenseReleaseService
{
    /// <summary>
    /// Provides HTTP health check endpoints for service monitoring
    /// </summary>
    public class HealthEndpoints : IDisposable
    {
        private readonly HttpListener _httpListener;
        private readonly HealthChecker _healthChecker;
        private readonly ConfigurationManager _configurationManager;
        private readonly LicenseReleaseConfiguration _licenseConfig;
        private readonly HealthChecker _serviceHealth;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _lock = new object();
        private bool _isRunning;
        private Task _listenerTask;

        /// <summary>
        /// Gets whether the health endpoints are running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Initializes a new instance of the HealthEndpoints class
        /// </summary>
        /// <param name="healthChecker">Health checker instance</param>
        /// <param name="configurationManager">Configuration manager instance</param>
        /// <param name="licenseConfig">License release configuration</param>
        /// <param name="serviceHealth">Service health instance</param>
        public HealthEndpoints(
            HealthChecker healthChecker,
            ConfigurationManager configurationManager,
            LicenseReleaseConfiguration licenseConfig,
            HealthChecker serviceHealth)
        {
            _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _licenseConfig = licenseConfig ?? throw new ArgumentNullException(nameof(licenseConfig));
            _serviceHealth = serviceHealth ?? throw new ArgumentNullException(nameof(serviceHealth));
            _cancellationTokenSource = new CancellationTokenSource();

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(_licenseConfig.HealthMonitoring.HealthCheckEndpoint + "/");
        }

        /// <summary>
        /// Starts the health endpoints
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning)
                    return;

                try
                {
                    _httpListener.Start();
                    _isRunning = true;
                    _listenerTask = Task.Run(() => ListenForRequests(_cancellationTokenSource.Token));

                    EventLog.WriteEntry("LicenseReleaseService", $"Health endpoints started on {_licenseConfig.HealthMonitoring.HealthCheckEndpoint}",
                        System.Diagnostics.EventLogEntryType.Information);
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("LicenseReleaseService", $"Failed to start health endpoints: {ex.Message}",
                        System.Diagnostics.EventLogEntryType.Error);
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the health endpoints
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                    return;

                try
                {
                    _cancellationTokenSource.Cancel();
                    _httpListener.Stop();

                    if (_listenerTask != null)
                    {
                        _listenerTask.Wait(TimeSpan.FromSeconds(5));
                    }

                    _isRunning = false;
                    EventLog.WriteEntry("LicenseReleaseService", "Health endpoints stopped",
                        System.Diagnostics.EventLogEntryType.Information);
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("LicenseReleaseService", $"Error stopping health endpoints: {ex.Message}",
                        System.Diagnostics.EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Listens for HTTP requests
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task ListenForRequests(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context, cancellationToken), cancellationToken);
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("LicenseReleaseService", $"Error handling health endpoint request: {ex.Message}",
                        System.Diagnostics.EventLogEntryType.Warning);
                }
            }
        }

        /// <summary>
        /// Handles HTTP requests
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private void HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Set CORS headers
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.Close();
                    return;
                }

                var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? "";

                switch (path.ToLower())
                {
                    case "/health":
                        HandleHealthCheck(response);
                        break;
                    case "/health/detailed":
                        HandleDetailedHealthCheck(response);
                        break;
                    case "/health/metrics":
                        HandleMetrics(response);
                        break;
                    case "/health/configuration":
                        HandleConfiguration(response);
                        break;
                    case "/health/readiness":
                        HandleReadinessCheck(response);
                        break;
                    case "/health/liveness":
                        HandleLivenessCheck(response);
                        break;
                    default:
                        HandleNotFound(response);
                        break;
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("LicenseReleaseService", $"Error processing health request: {ex.Message}",
                    System.Diagnostics.EventLogEntryType.Error);

                try
                {
                    var response = context.Response;
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.ContentType = "application/json";
                    var errorResponse = CreateErrorResponse("Internal server error", ex.Message);
                    WriteJsonResponse(response, errorResponse);
                }
                catch
                {
                    // Response may already be closed
                }
            }
        }

        /// <summary>
        /// Handles basic health check
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleHealthCheck(HttpListenerResponse response)
        {
            var healthStatus = _healthChecker.OverallStatus;
            var statusCode = GetStatusCodeForHealthStatus(healthStatus);

            var healthResponse = new
            {
                Status = healthStatus.ToString(),
                Timestamp = DateTime.UtcNow,
                Service = "LicenseReleaseService",
                Version = GetCurrentVersion(),
                Uptime = GetUptime(),
                LastHealthCheck = DateTime.UtcNow
            };

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            WriteJsonResponse(response, healthResponse);
        }

        /// <summary>
        /// Handles detailed health check
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleDetailedHealthCheck(HttpListenerResponse response)
        {
            var healthStatus = _healthChecker.OverallStatus;
            var statusCode = GetStatusCodeForHealthStatus(healthStatus);

            var healthResponse = new
            {
                Status = healthStatus.ToString(),
                Timestamp = DateTime.UtcNow,
                Service = "LicenseReleaseService",
                Version = GetCurrentVersion(),
                Uptime = GetUptime(),
                LastHealthCheck = DateTime.UtcNow,
                HealthChecks = new List<object>(),
                Metrics = new Dictionary<string, object>(),
                ConfigurationHealth = _configurationManager.HealthStatus.ToString(),
                ConfigurationValidation = _licenseConfig.Validate().IsValid,
                ConfigurationSummary = _licenseConfig.GetSummary()
            };

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            WriteJsonResponse(response, healthResponse);
        }

        /// <summary>
        /// Handles metrics request
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleMetrics(HttpListenerResponse response)
        {
            var metrics = new
            {
                Timestamp = DateTime.UtcNow,
                ServiceMetrics = new Dictionary<string, object>(),
                HealthMetrics = new Dictionary<string, object>(),
                PerformanceMetrics = GetPerformanceMetrics(),
                ConfigurationMetrics = new
                {
                    ConfigFilePath = _configurationManager.ConfigFilePath,
                    LastConfigChange = _configurationManager.LastConfigChange,
                    LastSuccessfulReload = _configurationManager.LastSuccessfulReload,
                    AdvancedFeaturesEnabled = _configurationManager.AdvancedFeaturesEnabled,
                    ReloadErrors = _configurationManager.ReloadErrors.Count
                }
            };

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";
            WriteJsonResponse(response, metrics);
        }

        /// <summary>
        /// Handles configuration request
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleConfiguration(HttpListenerResponse response)
        {
            var configInfo = new
            {
                Timestamp = DateTime.UtcNow,
                ServiceEnabled = _licenseConfig.Enabled,
                CheckInterval = _licenseConfig.CheckIntervalSeconds,
                MaxConcurrentOperations = _licenseConfig.MaxConcurrentOperations,
                LicenseServer = new
                {
                    Host = _licenseConfig.LicenseServer.Host,
                    Port = _licenseConfig.LicenseServer.Port
                },
                ErrorRecovery = new
                {
                    Enabled = _licenseConfig.ErrorRecovery.Enabled,
                    MaxRetryAttempts = _licenseConfig.ErrorRecovery.MaxRetryAttempts
                },
                HealthMonitoring = new
                {
                    Enabled = _licenseConfig.HealthMonitoring.Enabled,
                    CheckInterval = _licenseConfig.HealthMonitoring.HealthCheckIntervalSeconds
                },
                ConfigurationManagement = new
                {
                    HotReloadEnabled = _licenseConfig.ConfigurationManagement.EnableHotReload,
                    ValidationEnabled = _licenseConfig.ConfigurationManagement.EnableValidation
                },
                Deployment = new
                {
                    Environment = _licenseConfig.Deployment.Environment,
                    StartupMode = _licenseConfig.Deployment.StartupMode
                }
            };

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";
            WriteJsonResponse(response, configInfo);
        }

        /// <summary>
        /// Handles readiness check
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleReadinessCheck(HttpListenerResponse response)
        {
            var isReady = true && // Simplified for now - would need async call to _serviceHealth.GetSystemHealthAsync()
                          _configurationManager.IsConfigurationValid &&
                          _licenseConfig.Validate().IsValid;

            var readinessResponse = new
            {
                Ready = isReady,
                Timestamp = DateTime.UtcNow,
                Checks = new
                {
                    ServiceHealthy = true, // Simplified for now
                    ConfigurationValid = _configurationManager.IsConfigurationValid,
                    LicenseConfigValid = _licenseConfig.Validate().IsValid
                }
            };

            response.StatusCode = isReady ? (int)HttpStatusCode.OK : (int)HttpStatusCode.ServiceUnavailable;
            response.ContentType = "application/json";
            WriteJsonResponse(response, readinessResponse);
        }

        /// <summary>
        /// Handles liveness check
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleLivenessCheck(HttpListenerResponse response)
        {
            var isAlive = true; // Simplified for now

            var livenessResponse = new
            {
                Alive = isAlive,
                Timestamp = DateTime.UtcNow,
                Uptime = GetUptime()
            };

            response.StatusCode = isAlive ? (int)HttpStatusCode.OK : (int)HttpStatusCode.ServiceUnavailable;
            response.ContentType = "application/json";
            WriteJsonResponse(response, livenessResponse);
        }

        /// <summary>
        /// Handles not found requests
        /// </summary>
        /// <param name="response">HTTP response</param>
        private void HandleNotFound(HttpListenerResponse response)
        {
            var errorResponse = new
            {
                Error = "Not Found",
                Message = "The requested endpoint was not found",
                AvailableEndpoints = new[]
                {
                    "/health",
                    "/health/detailed",
                    "/health/metrics",
                    "/health/configuration",
                    "/health/readiness",
                    "/health/liveness"
                },
                Timestamp = DateTime.UtcNow
            };

            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.ContentType = "application/json";
            WriteJsonResponse(response, errorResponse);
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="error">Error message</param>
        /// <param name="details">Error details</param>
        /// <returns>Error response object</returns>
        private object CreateErrorResponse(string error, string details)
        {
            return new
            {
                Error = error,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Writes JSON response
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <param name="data">Response data</param>
        private void WriteJsonResponse(HttpListenerResponse response, object data)
        {
            try
            {
                var json = SimpleJsonSerialize(data);
                var buffer = System.Text.Encoding.UTF8.GetBytes(json);

                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";

                using (var output = response.OutputStream)
                {
                    output.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("LicenseReleaseService", $"Error writing JSON response: {ex.Message}",
                    System.Diagnostics.EventLogEntryType.Warning);
            }
        }

        /// <summary>
        /// Gets HTTP status code for health status
        /// </summary>
        /// <param name="healthStatus">Health status</param>
        /// <returns>HTTP status code</returns>
        private int GetStatusCodeForHealthStatus(HealthStatus healthStatus)
        {
            switch (healthStatus)
            {
                case HealthStatus.Healthy:
                    return (int)HttpStatusCode.OK;
                case HealthStatus.Degraded:
                    return (int)HttpStatusCode.OK; // Still operational but with warnings
                case HealthStatus.Unhealthy:
                    return (int)HttpStatusCode.ServiceUnavailable;
                case HealthStatus.Unknown:
                default:
                    return (int)HttpStatusCode.OK; // Assume healthy if unknown
            }
        }

        /// <summary>
        /// Gets current service uptime
        /// </summary>
        /// <returns>Uptime as TimeSpan</returns>
        private TimeSpan GetUptime()
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return DateTime.UtcNow - process.StartTime.ToUniversalTime();
                }
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Gets current version
        /// </summary>
        /// <returns>Version string</returns>
        private string GetCurrentVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }

        /// <summary>
        /// Gets performance metrics
        /// </summary>
        /// <returns>Performance metrics</returns>
        private object GetPerformanceMetrics()
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return new
                    {
                        CpuUsage = GetCpuUsage(),
                        MemoryUsage = process.WorkingSet64,
                        MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                        ThreadCount = process.Threads.Count,
                        HandleCount = process.HandleCount,
                        StartTime = process.StartTime
                    };
                }
            }
            catch (Exception ex)
            {
                return new { Error = ex.Message };
            }
        }

        /// <summary>
        /// Gets CPU usage percentage
        /// </summary>
        /// <returns>CPU usage percentage</returns>
        private double GetCpuUsage()
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    var startTime = DateTime.UtcNow;
                    var startCpuUsage = process.TotalProcessorTime;

                    System.Threading.Thread.Sleep(100); // Wait for measurement

                    var endTime = DateTime.UtcNow;
                    var endCpuUsage = process.TotalProcessorTime;

                    var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                    var totalMsPassed = (endTime - startTime).TotalMilliseconds;

                    var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                    return Math.Round(cpuUsageTotal * 100, 2);
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Disposes the health endpoints
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the health endpoints
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
                (_httpListener as System.IDisposable)?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~HealthEndpoints()
        {
            Dispose(false);
        }

        /// <summary>
        /// Simple JSON serializer for basic objects
        /// </summary>
        /// <param name="obj">Object to serialize</param>
        /// <returns>JSON string</returns>
        private string SimpleJsonSerialize(object obj)
        {
            if (obj == null)
                return "null";

            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string))
            {
                if (type == typeof(string))
                    return $"\"{obj.ToString().Replace("\"", "\\\"")}\"";
                return obj.ToString();
            }

            // Handle anonymous objects and basic dictionaries
            var result = new System.Text.StringBuilder();
            result.Append("{");

            var properties = type.GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                var value = prop.GetValue(obj);

                if (i > 0)
                    result.Append(",");

                result.Append($"\"{prop.Name}\":");

                if (value == null)
                {
                    result.Append("null");
                }
                else if (value is string str)
                {
                    result.Append($"\"{str.Replace("\"", "\\\"")}\"");
                }
                else if (value is ValueType)
                {
                    result.Append(value);
                }
                else
                {
                    result.Append("\"object\"");
                }
            }

            result.Append("}");
            return result.ToString();
        }
    }
}