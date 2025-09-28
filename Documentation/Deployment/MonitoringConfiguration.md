# License Release Service - Monitoring Configuration

## Overview

This monitoring configuration guide provides comprehensive instructions for setting up monitoring for the License Release Service with Idle Detection capabilities. This guide covers health checks, performance metrics, business metrics, and alerting configuration.

## Monitoring Architecture

### Components

1. **Service Monitoring**: Windows Service health and performance
2. **Database Monitoring**: SQL Server performance and connectivity
3. **License Manager Monitoring**: SolidWorks License Manager connectivity
4. **Application Monitoring**: Application-specific metrics and health
5. **Business Monitoring**: License usage and optimization metrics
6. **System Monitoring**: Server health and resource utilization

### Monitoring Tools Integration

- **Prometheus/Grafana**: Metrics collection and visualization
- **Windows Performance Monitor**: System performance metrics
- **SQL Server Monitoring**: Database performance and health
- **Custom Health Checks**: Application-specific health endpoints
- **Log Aggregation**: Centralized log collection and analysis

## Health Check Configuration

### Health Check Endpoints

#### 1. Service Health Endpoint

```csharp
// Service Health Check Controller
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IHealthCheckService healthCheckService, ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var healthResult = await _healthCheckService.CheckHealthAsync();

        return healthResult.Status == HealthStatus.Healthy
            ? Ok(healthResult)
            : StatusCode((int)HttpStatusCode.ServiceUnavailable, healthResult);
    }

    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailedHealth()
    {
        var detailedHealth = await _healthCheckService.CheckDetailedHealthAsync();
        return Ok(detailedHealth);
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness()
    {
        var isReady = await _healthCheckService.IsReadyAsync();
        return isReady ? Ok() : StatusCode((int)HttpStatusCode.ServiceUnavailable);
    }

    [HttpGet("live")]
    public async Task<IActionResult> GetLiveness()
    {
        var isAlive = await _healthCheckService.IsAliveAsync();
        return isAlive ? Ok() : StatusCode((int)HttpStatusCode.ServiceUnavailable);
    }
}
```

#### 2. Health Check Service Implementation

```csharp
// Health Check Service
public class HealthCheckService : IHealthCheckService
{
    private readonly ILicenseManager _licenseManager;
    private readonly IDatabaseService _databaseService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        ILicenseManager licenseManager,
        IDatabaseService databaseService,
        IConfigurationService configurationService,
        ILogger<HealthCheckService> logger)
    {
        _licenseManager = licenseManager;
        _databaseService = databaseService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<HealthResult> CheckHealthAsync()
    {
        var checks = new List<HealthCheck>
        {
            await CheckLicenseManagerHealthAsync(),
            await CheckDatabaseHealthAsync(),
            await CheckConfigurationHealthAsync(),
            await CheckServiceHealthAsync()
        };

        var overallStatus = checks.All(c => c.Status == HealthStatus.Healthy)
            ? HealthStatus.Healthy
            : HealthStatus.Unhealthy;

        return new HealthResult
        {
            Status = overallStatus,
            Timestamp = DateTime.UtcNow,
            Checks = checks
        };
    }

    public async Task<DetailedHealthResult> CheckDetailedHealthAsync()
    {
        var basicHealth = await CheckHealthAsync();

        return new DetailedHealthResult
        {
            Status = basicHealth.Status,
            Timestamp = basicHealth.Timestamp,
            Checks = basicHealth.Checks,
            Metrics = await CollectHealthMetricsAsync(),
            Dependencies = await GetDependencyStatusAsync()
        };
    }

    public async Task<bool> IsReadyAsync()
    {
        try
        {
            var health = await CheckHealthAsync();
            return health.Status == HealthStatus.Healthy;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsAliveAsync()
    {
        try
        {
            // Basic liveness check - service is running
            return Process.GetCurrentProcess().Responding;
        }
        catch
        {
            return false;
        }
    }

    private async Task<HealthCheck> CheckLicenseManagerHealthAsync()
    {
        try
        {
            var isConnected = await _licenseManager.CheckConnectionAsync();
            var responseTime = await _licenseManager.GetResponseTimeAsync();

            return new HealthCheck
            {
                Name = "License Manager",
                Status = isConnected ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = isConnected ? "Connected to license manager" : "Cannot connect to license manager",
                ResponseTime = responseTime,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License manager health check failed");
            return new HealthCheck
            {
                Name = "License Manager",
                Status = HealthStatus.Unhealthy,
                Description = $"Health check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<HealthCheck> CheckDatabaseHealthAsync()
    {
        try
        {
            var isConnected = await _databaseService.CheckConnectionAsync();
            var responseTime = await _databaseService.GetResponseTimeAsync();

            return new HealthCheck
            {
                Name = "Database",
                Status = isConnected ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = isConnected ? "Database connection healthy" : "Database connection failed",
                ResponseTime = responseTime,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new HealthCheck
            {
                Name = "Database",
                Status = HealthStatus.Unhealthy,
                Description = $"Health check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<HealthCheck> CheckConfigurationHealthAsync()
    {
        try
        {
            var isValid = await _configurationService.ValidateConfigurationAsync();

            return new HealthCheck
            {
                Name = "Configuration",
                Status = isValid ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = isValid ? "Configuration is valid" : "Configuration validation failed",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration health check failed");
            return new HealthCheck
            {
                Name = "Configuration",
                Status = HealthStatus.Unhealthy,
                Description = $"Health check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<HealthCheck> CheckServiceHealthAsync()
    {
        try
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime;
            var memoryUsage = Process.GetCurrentProcess().WorkingSet64;
            var cpuUsage = GetCpuUsage();

            return new HealthCheck
            {
                Name = "Service",
                Status = HealthStatus.Healthy,
                Description = $"Service running normally (Uptime: {uptime})",
                Metrics = new Dictionary<string, double>
                {
                    ["uptime_seconds"] = uptime.TotalSeconds,
                    ["memory_usage_bytes"] = memoryUsage,
                    ["cpu_usage_percent"] = cpuUsage
                },
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service health check failed");
            return new HealthCheck
            {
                Name = "Service",
                Status = HealthStatus.Unhealthy,
                Description = $"Health check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private double GetCpuUsage()
    {
        // Implementation for CPU usage calculation
        var proc = Process.GetCurrentProcess();
        var currentTime = DateTime.UtcNow;
        var processorTime = proc.TotalProcessorTime;

        // Calculate CPU usage percentage
        return Math.Min(100.0, (processorTime.TotalMilliseconds /
                              (currentTime - proc.StartTimeUtc).TotalMilliseconds) * 100.0);
    }

    private async Task<Dictionary<string, double>> CollectHealthMetricsAsync()
    {
        var metrics = new Dictionary<string, double>();

        try
        {
            var process = Process.GetCurrentProcess();
            metrics["uptime_seconds"] = (DateTime.UtcNow - process.StartTime).TotalSeconds;
            metrics["memory_usage_bytes"] = process.WorkingSet64;
            metrics["cpu_usage_percent"] = GetCpuUsage();
            metrics["thread_count"] = process.Threads.Count;
            metrics["handle_count"] = process.HandleCount;

            // Database metrics
            metrics["database_connection_pool_size"] = await _databaseService.GetConnectionPoolSizeAsync();
            metrics["database_response_time_ms"] = await _databaseService.GetResponseTimeAsync().ContinueWith(t => t.Result.TotalMilliseconds);

            // License manager metrics
            metrics["license_manager_response_time_ms"] = await _licenseManager.GetResponseTimeAsync().ContinueWith(t => t.Result.TotalMilliseconds);
            metrics["active_licenses"] = await _licenseManager.GetActiveLicenseCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect health metrics");
        }

        return metrics;
    }

    private async Task<Dictionary<string, DependencyStatus>> GetDependencyStatusAsync()
    {
        var dependencies = new Dictionary<string, DependencyStatus>();

        try
        {
            // License Manager dependency
            var licenseManagerStatus = await _licenseManager.GetStatusAsync();
            dependencies["license_manager"] = new DependencyStatus
            {
                Name = "License Manager",
                Status = licenseManagerStatus.IsConnected ? DependencyHealth.Healthy : DependencyHealth.Unhealthy,
                ResponseTime = licenseManagerStatus.ResponseTime,
                LastCheck = DateTime.UtcNow
            };

            // Database dependency
            var databaseStatus = await _databaseService.GetStatusAsync();
            dependencies["database"] = new DependencyStatus
            {
                Name = "Database",
                Status = databaseStatus.IsConnected ? DependencyHealth.Healthy : DependencyHealth.Unhealthy,
                ResponseTime = databaseStatus.ResponseTime,
                LastCheck = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dependency status");
        }

        return dependencies;
    }
}
```

## Performance Metrics Configuration

### Windows Performance Monitor Counters

```powershell
# Performance Monitor Configuration Script
# Save as Configure-PerformanceMonitors.ps1

param(
    [string]$ComputerName = $env:COMPUTERNAME
)

# License Release Service Performance Counters
$categoryName = "License Release Service"
$categoryHelp = "Performance counters for License Release Service"
$categoryType = [System.Diagnostics.PerformanceCounterCategoryType]::MultiInstance

# Check if category exists
if (-not [System.Diagnostics.PerformanceCounterCategory]::Exists($categoryName)) {
    # Create category
    $ccdc = New-Object System.Diagnostics.CounterCreationDataCollection

    # Service counters
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Active Users", "Number of active users", [System.Diagnostics.PerformanceCounterType]::NumberOfItems64)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("License Checkouts/sec", "License checkout rate", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("License Releases/sec", "License release rate", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Average License Duration", "Average license duration", [System.Diagnostics.PerformanceCounterType]::AverageCount64)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Average License Duration Base", "Average license duration base", [System.Diagnostics.PerformanceCounterType]::AverageBase)))

    # Idle detection counters
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Active Idle Sessions", "Number of active idle detection sessions", [System.Diagnostics.PerformanceCounterType]::NumberOfItems64)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Idle Detections/sec", "Idle detection rate", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("License Releases by Idle/sec", "License releases due to inactivity", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32)))

    # Performance counters
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Request Processing Time", "Average request processing time", [System.Diagnostics.PerformanceCounterType]::AverageCount64)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Request Processing Time Base", "Average request processing time base", [System.Diagnostics.PerformanceCounterType]::AverageBase)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Requests/sec", "Request rate", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32)))

    # Error counters
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("License Checkout Errors", "License checkout error count", [System.Diagnostics.PerformanceCounterType]::NumberOfItems64)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("License Release Errors", "License release error count", [System.Diagnostics.PerformanceCounterType]::NumberOfItems64)))
    $ccdc.Add((New-Object System.Diagnostics.CounterCreationData("Idle Detection Errors", "Idle detection error count", [System.Diagnostics.PerformanceCounterType]::NumberOfItems64)))

    # Create the category
    [System.Diagnostics.PerformanceCounterCategory]::Create($categoryName, $categoryHelp, $categoryType, $ccdc)
    Write-Host "Performance counter category '$categoryName' created successfully"
} else {
    Write-Host "Performance counter category '$categoryName' already exists"
}

# Configure data collection
$logman = "logman.exe"
$counterSetName = "LicenseReleaseService_Performance"

# Create counter set if it doesn't exist
$existingSets = & $logman query
if ($existingSets -notcontains $counterSetName) {
    $counters = @(
        "\License Release Service(*)\*",
        "\Processor(_Total)\% Processor Time",
        "\Memory\Available MBytes",
        "\Memory\Pages/sec",
        "\PhysicalDisk(_Total)\% Disk Time",
        "\PhysicalDisk(_Total)\Current Disk Queue Length",
        "\Network Interface(*)\Bytes Total/sec",
        "\System\Processor Queue Length",
        "\SQLServer:General Statistics\User Connections",
        "\SQLServer:Locks(_Total)\Number of Deadlocks/sec",
        "\SQLServer:SQL Statistics\Batch Requests/sec"
    )

    $counterPath = $counters -join " "

    & $logman create counter $counterSetName -c $counterPath -si 00:00:15 -max 100 -b "00:00:00" -e "24:00:00" -v nnnnnn -rf hh:mm:ss
    Write-Host "Performance counter set '$counterSetName' created successfully"
} else {
    Write-Host "Performance counter set '$counterSetName' already exists"
}

# Start data collection
& $logman start $counterSetName
Write-Host "Performance data collection started"
```

### Prometheus Metrics Exporter

```csharp
// Prometheus Metrics Exporter
public class PrometheusMetricsExporter
{
    private readonly IMetricServer _metricServer;
    private readonly Dictionary<string, Gauge> _gauges = new Dictionary<string, Gauge>();
    private readonly Dictionary<string, Counter> _counters = new Dictionary<string, Counter>();
    private readonly Dictionary<string, Histogram> _histograms = new Dictionary<string, Histogram>();
    private readonly ILogger<PrometheusMetricsExporter> _logger;

    public PrometheusMetricsExporter(ILogger<PrometheusMetricsExporter> logger)
    {
        _logger = logger;
        _metricServer = new MetricServer(port: 9095);

        InitializeMetrics();
    }

    public void Start()
    {
        _metricServer.Start();
        _logger.LogInformation("Prometheus metrics server started on port 9095");
    }

    public void Stop()
    {
        _metricServer.Stop();
        _logger.LogInformation("Prometheus metrics server stopped");
    }

    private void InitializeMetrics()
    {
        // Service metrics
        _gauges["service_uptime_seconds"] = Metrics.CreateGauge(
            "license_release_service_uptime_seconds",
            "Service uptime in seconds",
            new GaugeConfiguration
            {
                LabelNames = new[] { "service_name", "version" }
            });

        _gauges["service_memory_usage_bytes"] = Metrics.CreateGauge(
            "license_release_service_memory_usage_bytes",
            "Service memory usage in bytes");

        _gauges["service_cpu_usage_percent"] = Metrics.CreateGauge(
            "license_release_service_cpu_usage_percent",
            "Service CPU usage percentage");

        _gauges["service_active_threads"] = Metrics.CreateGauge(
            "license_release_service_active_threads",
            "Number of active threads");

        // License metrics
        _gauges["license_active_users"] = Metrics.CreateGauge(
            "license_release_service_active_users",
            "Number of active users",
            new GaugeConfiguration
            {
                LabelNames = new[] { "license_type" }
            });

        _counters["license_checkouts_total"] = Metrics.CreateCounter(
            "license_release_service_checkouts_total",
            "Total number of license checkouts",
            new CounterConfiguration
            {
                LabelNames = new[] { "license_type", "status" }
            });

        _counters["license_releases_total"] = Metrics.CreateCounter(
            "license_release_service_releases_total",
            "Total number of license releases",
            new CounterConfiguration
            {
                LabelNames = new[] { "license_type", "release_reason" }
            });

        _histograms["license_duration_seconds"] = Metrics.CreateHistogram(
            "license_release_service_duration_seconds",
            "License duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "license_type" },
                Buckets = Histogram.ExponentialBuckets(60, 2, 10) // 1min, 2min, 4min, 8min, 16min, 32min, 64min, 128min, 256min, 512min
            });

        // Idle detection metrics
        _gauges["idle_detection_active_sessions"] = Metrics.CreateGauge(
            "license_release_service_idle_detection_active_sessions",
            "Number of active idle detection sessions");

        _counters["idle_detection_detections_total"] = Metrics.CreateCounter(
            "license_release_service_idle_detection_detections_total",
            "Total number of idle detections",
            new CounterConfiguration
            {
                LabelNames = new[] { "detection_method", "status" }
            });

        _counters["idle_detection_releases_total"] = Metrics.CreateCounter(
            "license_release_service_idle_detection_releases_total",
            "Total number of license releases due to inactivity",
            new CounterConfiguration
            {
                LabelNames = new[] { "detection_method" }
            });

        _histograms["idle_detection_time_seconds"] = Metrics.CreateHistogram(
            "license_release_service_idle_detection_time_seconds",
            "Time to detect inactivity in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(300, 1.5, 8) // 5min, 7.5min, 11min, 17min, 26min, 39min, 59min, 89min
            });

        // Performance metrics
        _histograms["request_processing_time_seconds"] = Metrics.CreateHistogram(
            "license_release_service_request_processing_time_seconds",
            "Request processing time in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint", "method" }
            });

        _counters["request_errors_total"] = Metrics.CreateCounter(
            "license_release_service_request_errors_total",
            "Total number of request errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "error_type" }
            });

        // Database metrics
        _gauges["database_connection_pool_size"] = Metrics.CreateGauge(
            "license_release_service_database_connection_pool_size",
            "Database connection pool size");

        _histograms["database_query_duration_seconds"] = Metrics.CreateHistogram(
            "license_release_service_database_query_duration_seconds",
            "Database query duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "query_type", "table" }
            });

        // License manager metrics
        _gauges["license_manager_response_time_seconds"] = Metrics.CreateGauge(
            "license_release_service_license_manager_response_time_seconds",
            "License manager response time in seconds");

        _gauges["license_manager_available_licenses"] = Metrics.CreateGauge(
            "license_release_service_license_manager_available_licenses",
            "Number of available licenses",
            new GaugeConfiguration
            {
                LabelNames = new[] { "license_type" }
            });
    }

    public void UpdateServiceMetrics(ServiceMetrics metrics)
    {
        _gauges["service_uptime_seconds"]
            .WithLabels(metrics.ServiceName, metrics.Version)
            .Set(metrics.Uptime.TotalSeconds);

        _gauges["service_memory_usage_bytes"].Set(metrics.MemoryUsage);
        _gauges["service_cpu_usage_percent"].Set(metrics.CpuUsage);
        _gauges["service_active_threads"].Set(metrics.ActiveThreads);
    }

    public void UpdateLicenseMetrics(LicenseMetrics metrics)
    {
        foreach (var activeUsers in metrics.ActiveUsersByLicenseType)
        {
            _gauges["license_active_users"]
                .WithLabels(activeUsers.Key)
                .Set(activeUsers.Value);
        }

        foreach (var checkout in metrics.Checkouts)
        {
            _counters["license_checkouts_total"]
                .WithLabels(checkout.LicenseType, checkout.Status)
                .Inc();
        }

        foreach (var release in metrics.Releases)
        {
            _counters["license_releases_total"]
                .WithLabels(release.LicenseType, release.Reason)
                .Inc();

            _histograms["license_duration_seconds"]
                .WithLabels(release.LicenseType)
                .Observe(release.Duration.TotalSeconds);
        }
    }

    public void UpdateIdleDetectionMetrics(IdleDetectionMetrics metrics)
    {
        _gauges["idle_detection_active_sessions"].Set(metrics.ActiveSessions);

        foreach (var detection in metrics.Detections)
        {
            _counters["idle_detection_detections_total"]
                .WithLabels(detection.Method, detection.Status)
                .Inc();

            if (detection.Status == "detected")
            {
                _histograms["idle_detection_time_seconds"]
                    .Observe(detection.DetectionTime.TotalSeconds);
            }
        }

        foreach (var release in metrics.Releases)
        {
            _counters["idle_detection_releases_total"]
                .WithLabels(release.Method)
                .Inc();
        }
    }

    public void UpdatePerformanceMetrics(PerformanceMetrics metrics)
    {
        foreach (var request in metrics.Requests)
        {
            _histograms["request_processing_time_seconds"]
                .WithLabels(request.Endpoint, request.Method)
                .Observe(request.Duration.TotalSeconds);
        }

        foreach (var error in metrics.Errors)
        {
            _counters["request_errors_total"]
                .WithLabels(error.Endpoint, error.ErrorType)
                .Inc();
        }
    }

    public void UpdateDatabaseMetrics(DatabaseMetrics metrics)
    {
        _gauges["database_connection_pool_size"].Set(metrics.ConnectionPoolSize);

        foreach (var query in metrics.Queries)
        {
            _histograms["database_query_duration_seconds"]
                .WithLabels(query.Type, query.Table)
                .Observe(query.Duration.TotalSeconds);
        }
    }

    public void UpdateLicenseManagerMetrics(LicenseManagerMetrics metrics)
    {
        _gauges["license_manager_response_time_seconds"].Set(metrics.ResponseTime.TotalSeconds);

        foreach (var available in metrics.AvailableLicenses)
        {
            _gauges["license_manager_available_licenses"]
                .WithLabels(available.Key)
                .Set(available.Value);
        }
    }
}
```

## Business Metrics Configuration

### License Usage Metrics

```csharp
// Business Metrics Collector
public class BusinessMetricsCollector
{
    private readonly ILicenseRepository _licenseRepository;
    private readonly IUsageAnalyticsService _usageAnalyticsService;
    private readonly ILogger<BusinessMetricsCollector> _logger;
    private readonly IMetricsPublisher _metricsPublisher;

    public BusinessMetricsCollector(
        ILicenseRepository licenseRepository,
        IUsageAnalyticsService usageAnalyticsService,
        ILogger<BusinessMetricsCollector> logger,
        IMetricsPublisher metricsPublisher)
    {
        _licenseRepository = licenseRepository;
        _usageAnalyticsService = usageAnalyticsService;
        _logger = logger;
        _metricsPublisher = metricsPublisher;
    }

    public async Task CollectAndPublishMetricsAsync()
    {
        try
        {
            var metrics = await CollectBusinessMetricsAsync();
            await _metricsPublisher.PublishAsync(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect and publish business metrics");
        }
    }

    private async Task<BusinessMetrics> CollectBusinessMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var metrics = new BusinessMetrics
        {
            Timestamp = now,
            Period = TimeSpan.FromHours(1)
        };

        // License utilization metrics
        metrics.LicenseUtilization = await CalculateLicenseUtilizationAsync();

        // Cost optimization metrics
        metrics.CostSavings = await CalculateCostSavingsAsync();

        // User activity metrics
        metrics.UserActivity = await CalculateUserActivityMetricsAsync();

        // Performance metrics
        metrics.PerformanceMetrics = await CalculatePerformanceMetricsAsync();

        // Business impact metrics
        metrics.BusinessImpact = await CalculateBusinessImpactMetricsAsync();

        return metrics;
    }

    private async Task<LicenseUtilizationMetrics> CalculateLicenseUtilizationAsync()
    {
        var totalLicenses = await _licenseRepository.GetTotalLicensesAsync();
        var activeLicenses = await _licenseRepository.GetActiveLicensesAsync();
        var idleLicenses = await _licenseRepository.GetIdleLicensesAsync();
        var releasedLicenses = await _licenseRepository.GetReleasedLicensesByIdleDetectionAsync();

        return new LicenseUtilizationMetrics
        {
            TotalLicenses = totalLicenses,
            ActiveLicenses = activeLicenses.Count,
            IdleLicenses = idleLicenses.Count,
            ReleasedLicensesByIdleDetection = releasedLicenses.Count,
            UtilizationRate = totalLicenses > 0 ? (double)activeLicenses.Count / totalLicenses : 0,
            IdleRate = activeLicenses.Count > 0 ? (double)idleLicenses.Count / activeLicenses.Count : 0,
            ReleaseRate = idleLicenses.Count > 0 ? (double)releasedLicenses.Count / idleLicenses.Count : 0
        };
    }

    private async Task<CostSavingsMetrics> CalculateCostSavingsAsync()
    {
        var releasedHours = await _licenseRepository.GetTotalReleasedHoursByIdleDetectionAsync();
        var costPerLicensePerHour = await GetCostPerLicensePerHourAsync();
        var totalSavings = releasedHours * costPerLicensePerHour;

        return new CostSavingsMetrics
        {
            TotalReleasedHours = releasedHours,
            CostPerLicensePerHour = costPerLicensePerHour,
            TotalSavings = totalSavings,
            MonthlyProjectedSavings = totalSavings * (24 * 30), // Project monthly savings
            YearlyProjectedSavings = totalSavings * (24 * 365) // Project yearly savings
        };
    }

    private async Task<UserActivityMetrics> CalculateUserActivityMetricsAsync()
    {
        var userActivity = await _usageAnalyticsService.GetUserActivityAsync();
        var peakUsageHours = await _usageAnalyticsService.GetPeakUsageHoursAsync();
        var averageSessionDuration = await _usageAnalyticsService.GetAverageSessionDurationAsync();

        return new UserActivityMetrics
        {
            TotalActiveUsers = userActivity.TotalActiveUsers,
            UniqueUsersToday = userActivity.UniqueUsersToday,
            PeakConcurrentUsers = userActivity.PeakConcurrentUsers,
            PeakUsageHours = peakUsageHours,
            AverageSessionDuration = averageSessionDuration,
            UserSatisfactionScore = await CalculateUserSatisfactionScoreAsync()
        };
    }

    private async Task<BusinessPerformanceMetrics> CalculatePerformanceMetricsAsync()
    {
        var licenseRequestMetrics = await _usageAnalyticsService.GetLicenseRequestMetricsAsync();
        var idleDetectionMetrics = await _usageAnalyticsService.GetIdleDetectionMetricsAsync();
        var systemPerformanceMetrics = await _usageAnalyticsService.GetSystemPerformanceMetricsAsync();

        return new BusinessPerformanceMetrics
        {
            AverageLicenseRequestTime = licenseRequestMetrics.AverageRequestTime,
            LicenseRequestSuccessRate = licenseRequestMetrics.SuccessRate,
            IdleDetectionAccuracy = idleDetectionMetrics.AccuracyRate,
            IdleDetectionResponseTime = idleDetectionMetrics.AverageResponseTime,
            SystemUptime = systemPerformanceMetrics.Uptime,
            SystemAvailability = systemPerformanceMetrics.Availability
        };
    }

    private async Task<BusinessImpactMetrics> CalculateBusinessImpactMetricsAsync()
    {
        var productivityMetrics = await _usageAnalyticsService.GetProductivityMetricsAsync();
        var resourceOptimizationMetrics = await _usageAnalyticsService.GetResourceOptimizationMetricsAsync();

        return new BusinessImpactMetrics
        {
            ProductivityImprovement = productivityMetrics.ImprovementRate,
            ReducedWaitTime = productivityMetrics.ReducedWaitTime,
            ResourceUtilization = resourceOptimizationMetrics.UtilizationRate,
            RoiAchievement = await CalculateRoiAchievementAsync(),
            UserSatisfaction = await CalculateUserSatisfactionScoreAsync()
        };
    }

    private async Task<double> GetCostPerLicensePerHourAsync()
    {
        // This would typically be retrieved from a configuration or cost management system
        return 10.50; // Example: $10.50 per license per hour
    }

    private async Task<double> CalculateUserSatisfactionScoreAsync()
    {
        // This would typically involve user surveys, feedback, or support ticket analysis
        // For now, return a calculated score based on system performance
        var systemMetrics = await _usageAnalyticsService.GetSystemPerformanceMetricsAsync();
        var licenseMetrics = await _usageAnalyticsService.GetLicenseRequestMetricsAsync();

        // Simple calculation based on performance metrics
        return (systemMetrics.Availability + licenseMetrics.SuccessRate) / 2;
    }

    private async Task<double> CalculateRoiAchievementAsync()
    {
        var costSavings = await CalculateCostSavingsAsync();
        var implementationCost = await GetImplementationCostAsync();

        if (implementationCost == 0) return 0;

        return (costSavings.TotalSavings / implementationCost) * 100;
    }

    private async Task<double> GetImplementationCostAsync()
    {
        // This would typically be retrieved from financial systems
        return 50000.0; // Example: $50,000 implementation cost
    }
}

// Business Metrics Data Models
public class BusinessMetrics
{
    public DateTime Timestamp { get; set; }
    public TimeSpan Period { get; set; }
    public LicenseUtilizationMetrics LicenseUtilization { get; set; }
    public CostSavingsMetrics CostSavings { get; set; }
    public UserActivityMetrics UserActivity { get; set; }
    public BusinessPerformanceMetrics PerformanceMetrics { get; set; }
    public BusinessImpactMetrics BusinessImpact { get; set; }
}

public class LicenseUtilizationMetrics
{
    public int TotalLicenses { get; set; }
    public int ActiveLicenses { get; set; }
    public int IdleLicenses { get; set; }
    public int ReleasedLicensesByIdleDetection { get; set; }
    public double UtilizationRate { get; set; }
    public double IdleRate { get; set; }
    public double ReleaseRate { get; set; }
}

public class CostSavingsMetrics
{
    public double TotalReleasedHours { get; set; }
    public double CostPerLicensePerHour { get; set; }
    public double TotalSavings { get; set; }
    public double MonthlyProjectedSavings { get; set; }
    public double YearlyProjectedSavings { get; set; }
}

public class UserActivityMetrics
{
    public int TotalActiveUsers { get; set; }
    public int UniqueUsersToday { get; set; }
    public int PeakConcurrentUsers { get; set; }
    public List<int> PeakUsageHours { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public double UserSatisfactionScore { get; set; }
}

public class BusinessPerformanceMetrics
{
    public TimeSpan AverageLicenseRequestTime { get; set; }
    public double LicenseRequestSuccessRate { get; set; }
    public double IdleDetectionAccuracy { get; set; }
    public TimeSpan IdleDetectionResponseTime { get; set; }
    public TimeSpan SystemUptime { get; set; }
    public double SystemAvailability { get; set; }
}

public class BusinessImpactMetrics
{
    public double ProductivityImprovement { get; set; }
    public TimeSpan ReducedWaitTime { get; set; }
    public double ResourceUtilization { get; set; }
    public double RoiAchievement { get; set; }
    public double UserSatisfaction { get; set; }
}
```

## Monitoring Dashboard Configuration

### Grafana Dashboard Configuration

```json
{
  "dashboard": {
    "id": null,
    "title": "License Release Service Monitoring",
    "description": "Comprehensive monitoring dashboard for License Release Service",
    "tags": ["license", "release", "service", "monitoring"],
    "timezone": "browser",
    "panels": [
      {
        "id": 1,
        "title": "Service Health",
        "type": "stat",
        "targets": [
          {
            "expr": "license_release_service_uptime_seconds",
            "legendFormat": "Uptime",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "s",
            "displayName": "Service Uptime"
          }
        },
        "gridPos": {"h": 4, "w": 6, "x": 0, "y": 0}
      },
      {
        "id": 2,
        "title": "Active Users",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(license_release_service_active_users)",
            "legendFormat": "Active Users",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "none",
            "displayName": "Active Users"
          }
        },
        "gridPos": {"h": 4, "w": 6, "x": 6, "y": 0}
      },
      {
        "id": 3,
        "title": "License Utilization Rate",
        "type": "gauge",
        "targets": [
          {
            "expr": "rate(license_release_service_checkouts_total[5m])",
            "legendFormat": "Checkout Rate",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "ops",
            "min": 0,
            "max": 100
          }
        },
        "gridPos": {"h": 4, "w": 6, "x": 12, "y": 0}
      },
      {
        "id": 4,
        "title": "Memory Usage",
        "type": "graph",
        "targets": [
          {
            "expr": "license_release_service_memory_usage_bytes",
            "legendFormat": "Memory Usage",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "bytes",
            "displayName": "Memory Usage"
          }
        },
        "gridPos": {"h": 4, "w": 6, "x": 18, "y": 0}
      },
      {
        "id": 5,
        "title": "License Checkouts",
        "type": "timeseries",
        "targets": [
          {
            "expr": "rate(license_release_service_checkouts_total[5m])",
            "legendFormat": "License Checkouts",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "ops",
            "displayName": "License Checkouts"
          }
        },
        "gridPos": {"h": 8, "w": 12, "x": 0, "y": 4}
      },
      {
        "id": 6,
        "title": "License Releases",
        "type": "timeseries",
        "targets": [
          {
            "expr": "rate(license_release_service_releases_total[5m])",
            "legendFormat": "License Releases",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "ops",
            "displayName": "License Releases"
          }
        },
        "gridPos": {"h": 8, "w": 12, "x": 12, "y": 4}
      },
      {
        "id": 7,
        "title": "Idle Detection Sessions",
        "type": "timeseries",
        "targets": [
          {
            "expr": "license_release_service_idle_detection_active_sessions",
            "legendFormat": "Active Sessions",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "none",
            "displayName": "Idle Detection Sessions"
          }
        },
        "gridPos": {"h": 8, "w": 12, "x": 0, "y": 12}
      },
      {
        "id": 8,
        "title": "Request Processing Time",
        "type": "timeseries",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(license_release_service_request_processing_time_seconds_bucket[5m]))",
            "legendFormat": "95th Percentile",
            "refId": "A"
          },
          {
            "expr": "histogram_quantile(0.50, rate(license_release_service_request_processing_time_seconds_bucket[5m]))",
            "legendFormat": "50th Percentile",
            "refId": "B"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "s",
            "displayName": "Request Processing Time"
          }
        },
        "gridPos": {"h": 8, "w": 12, "x": 12, "y": 12}
      }
    ],
    "time": {
      "from": "now-1h",
      "to": "now"
    },
    "timepicker": {
      "refresh_intervals": ["5s", "10s", "30s", "1m", "5m", "15m", "30m", "1h", "2h", "1d"]
    },
    "templating": {
      "list": []
    },
    "annotations": {
      "list": []
    },
    "refresh": "30s",
    "version": 1,
    "links": []
  }
}
```

## Monitoring Scripts

### PowerShell Monitoring Script

```powershell
# License Release Service Monitoring Script
# Save as Monitor-LicenseReleaseService.ps1

param(
    [string]$ServiceName = "LicenseReleaseService",
    [string]$ComputerName = $env:COMPUTERNAME,
    [int]$IntervalSeconds = 30,
    [string]$LogPath = "C:\ProgramData\LicenseReleaseService\Logs\Monitoring",
    [string]$AlertEmail = "alerts@company.com"
)

# Initialize logging
if (-not (Test-Path $LogPath)) {
    New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
}

$logFile = Join-Path $LogPath "Monitoring_$(Get-Date -Format 'yyyyMMdd').log"

function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path $logFile -Value $logMessage

    if ($Level -eq "ERROR") {
        Write-Error $logMessage
    } else {
        Write-Host $logMessage
    }
}

function Test-ServiceHealth {
    param(
        [string]$ServiceName,
        [string]$ComputerName
    )

    try {
        $service = Get-Service -Name $ServiceName -ComputerName $ComputerName -ErrorAction Stop

        $health = @{
            Status = $service.Status
            Name = $service.Name
            DisplayName = $service.DisplayName
            ComputerName = $ComputerName
            Timestamp = Get-Date
            CanStart = $service.CanStart
            CanStop = $service.CanStop
            CanPauseAndContinue = $service.CanPauseAndContinue
        }

        # Test service endpoint if running
        if ($service.Status -eq 'Running') {
            try {
                $response = Invoke-RestMethod -Uri "http://$ComputerName`:8080/api/health" -TimeoutSec 5
                $health['HealthCheck'] = $response
            } catch {
                $health['HealthCheckError'] = $_.Exception.Message
            }
        }

        return $health
    } catch {
        Write-Log -Message "Failed to check service health: $($_.Exception.Message)" -Level "ERROR"
        return $null
    }
}

function Get-PerformanceMetrics {
    param(
        [string]$ComputerName
    )

    try {
        $metrics = @{
            Timestamp = Get-Date
            ComputerName = $ComputerName
            CPU = (Get-Counter "\Processor(_Total)\% Processor Time" -ComputerName $ComputerName).CounterSamples[0].CookedValue
            Memory = (Get-Counter "\Memory\Available MBytes" -ComputerName $ComputerName).CounterSamples[0].CookedValue
            Disk = (Get-Counter "\PhysicalDisk(_Total)\% Disk Time" -ComputerName $ComputerName).CounterSamples[0].CookedValue
            Network = (Get-Counter "\Network Interface(*)\Bytes Total/sec" -ComputerName $ComputerName).CounterSamples[0].CookedValue
        }

        # Get license service specific metrics
        if (Get-Counter "\\$ComputerName\License Release Service(*)\Active Users" -ErrorAction SilentlyContinue) {
            $metrics['ActiveUsers'] = (Get-Counter "\\$ComputerName\License Release Service(*)\Active Users" -ComputerName $ComputerName).CounterSamples[0].CookedValue
            $metrics['LicenseCheckouts'] = (Get-Counter "\\$ComputerName\License Release Service(*)\License Checkouts/sec" -ComputerName $ComputerName).CounterSamples[0].CookedValue
            $metrics['LicenseReleases'] = (Get-Counter "\\$ComputerName\License Release Service(*)\License Releases/sec" -ComputerName $ComputerName).CounterSamples[0].CookedValue
        }

        return $metrics
    } catch {
        Write-Log -Message "Failed to get performance metrics: $($_.Exception.Message)" -Level "ERROR"
        return $null
    }
}

function Test-DatabaseConnectivity {
    param(
        [string]$ServerInstance,
        [string]$Database
    )

    try {
        $query = "SELECT 1"
        $result = Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $Database -Query $query -ErrorAction Stop

        return @{
            Status = "Connected"
            ServerInstance = $ServerInstance
            Database = $Database
            Timestamp = Get-Date
        }
    } catch {
        Write-Log -Message "Database connectivity test failed: $($_.Exception.Message)" -Level "ERROR"
        return @{
            Status = "Disconnected"
            ServerInstance = $ServerInstance
            Database = $Database
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

function Send-Alert {
    param(
        [string]$Subject,
        [string]$Body,
        [string]$To
    )

    try {
        $smtpServer = "smtp.company.com"
        $from = "monitoring@company.com"

        Send-MailMessage -To $To -From $from -Subject $Subject -Body $Body -SmtpServer $smtpServer
        Write-Log -Message "Alert sent: $Subject"
    } catch {
        Write-Log -Message "Failed to send alert: $($_.Exception.Message)" -Level "ERROR"
    }
}

function Check-Alerts {
    param(
        [hashtable]$ServiceHealth,
        [hashtable]$PerformanceMetrics,
        [hashtable]$DatabaseStatus
    )

    $alerts = @()

    # Service health alerts
    if ($ServiceHealth -and $ServiceHealth.Status -ne 'Running') {
        $alerts += @{
            Type = "Service"
            Severity = "Critical"
            Message = "Service $($ServiceHealth.Name) is $($ServiceHealth.Status) on $($ServiceHealth.ComputerName)"
        }
    }

    # Performance alerts
    if ($PerformanceMetrics) {
        if ($PerformanceMetrics.CPU -gt 90) {
            $alerts += @{
                Type = "Performance"
                Severity = "Warning"
                Message = "High CPU usage: $($PerformanceMetrics.CPU)% on $($PerformanceMetrics.ComputerName)"
            }
        }

        if ($PerformanceMetrics.Memory -lt 100) {
            $alerts += @{
                Type = "Performance"
                Severity = "Warning"
                Message = "Low memory available: $($PerformanceMetrics.Memory)MB on $($PerformanceMetrics.ComputerName)"
            }
        }

        if ($PerformanceMetrics.Disk -gt 90) {
            $alerts += @{
                Type = "Performance"
                Severity = "Warning"
                Message = "High disk usage: $($PerformanceMetrics.Disk)% on $($PerformanceMetrics.ComputerName)"
            }
        }
    }

    # Database alerts
    if ($DatabaseStatus -and $DatabaseStatus.Status -eq 'Disconnected') {
        $alerts += @{
            Type = "Database"
            Severity = "Critical"
            Message = "Database connectivity lost: $($DatabaseStatus.ServerInstance)\$($DatabaseStatus.Database)"
        }
    }

    # Send alerts
    foreach ($alert in $alerts) {
        Write-Log -Message "$($alert.Severity) alert: $($alert.Message)" -Level "WARNING"

        $subject = "License Release Service $($alert.Severity) Alert"
        $body = @"
Alert Type: $($alert.Type)
Severity: $($alert.Severity)
Message: $($alert.Message)
Timestamp: $(Get-Date)
"@

        Send-Alert -Subject $subject -Body $body -To $AlertEmail
    }

    return $alerts
}

# Main monitoring loop
Write-Log -Message "Starting License Release Service monitoring..."
Write-Log -Message "Monitoring service: $ServiceName on $ComputerName"
Write-Log -Message "Monitoring interval: $IntervalSeconds seconds"

try {
    while ($true) {
        $timestamp = Get-Date

        Write-Log -Message "Starting monitoring cycle at $timestamp"

        # Collect monitoring data
        $serviceHealth = Test-ServiceHealth -ServiceName $ServiceName -ComputerName $ComputerName
        $performanceMetrics = Get-PerformanceMetrics -ComputerName $ComputerName
        $databaseStatus = Test-DatabaseConnectivity -ServerInstance "db-server" -Database "LicenseReleaseService"

        # Log collected data
        Write-Log -Message "Service Status: $($serviceHealth.Status)"
        Write-Log -Message "CPU Usage: $($performanceMetrics.CPU)%"
        Write-Log -Message "Memory Available: $($performanceMetrics.Memory)MB"
        Write-Log -Message "Database Status: $($databaseStatus.Status)"

        # Check for alerts
        $alerts = Check-Alerts -ServiceHealth $serviceHealth -PerformanceMetrics $performanceMetrics -DatabaseStatus $databaseStatus

        if ($alerts.Count -eq 0) {
            Write-Log -Message "No alerts detected"
        }

        Write-Log -Message "Monitoring cycle completed at $(Get-Date)"

        # Wait for next cycle
        Start-Sleep -Seconds $IntervalSeconds
    }
} catch {
    Write-Log -Message "Monitoring stopped due to error: $($_.Exception.Message)" -Level "ERROR"
    Send-Alert -Subject "License Release Service Monitoring Stopped" -Body "Monitoring script has stopped: $($_.Exception.Message)" -To $AlertEmail
}
```

This comprehensive monitoring configuration provides:

1. **Health Check System**: Complete health check implementation with REST API endpoints
2. **Performance Monitoring**: Windows Performance Counters and Prometheus metrics
3. **Business Metrics**: License utilization, cost savings, and business impact tracking
4. **Grafana Dashboard**: Pre-configured dashboard for visualizing metrics
5. **Monitoring Scripts**: PowerShell scripts for comprehensive monitoring and alerting

The monitoring system covers all aspects of the License Release Service including service health, performance metrics, business metrics, and automated alerting. This ensures comprehensive visibility into the system's operation and early detection of potential issues.