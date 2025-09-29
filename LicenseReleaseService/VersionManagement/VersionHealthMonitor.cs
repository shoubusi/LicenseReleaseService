using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Provides continuous health monitoring for detected SolidWorks versions
    /// </summary>
    public class VersionHealthMonitor : IDisposable
    {
        private readonly ILogger<VersionHealthMonitor> _logger;
        private readonly SolidWorksVersionDetector _versionDetector;
        private readonly VersionHealthConfiguration _configuration;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly Dictionary<string, VersionHealthHistory> _healthHistory;
        private readonly object _healthLock = new object();
        private bool _disposed;
        private bool _isMonitoring;

        /// <summary>
        /// Gets the current health status of all monitored versions
        /// </summary>
        public IReadOnlyDictionary<string, VersionHealthStatus> CurrentHealthStatus
        {
            get
            {
                lock (_healthLock)
                {
                    return new Dictionary<string, VersionHealthStatus>(
                        _healthHistory.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.CurrentStatus));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether monitoring is currently active
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// Event raised when a version's health status changes
        /// </summary>
        public event EventHandler<VersionHealthChangedEventArgs> HealthStatusChanged;

        /// <summary>
        /// Event raised when a critical health issue is detected
        /// </summary>
        public event EventHandler<VersionHealthCriticalEventArgs> CriticalHealthIssueDetected;

        /// <summary>
        /// Initializes a new instance of the VersionHealthMonitor class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="versionDetector">Version detector instance</param>
        /// <param name="configuration">Health monitoring configuration</param>
        public VersionHealthMonitor(ILogger<VersionHealthMonitor> logger, SolidWorksVersionDetector versionDetector, VersionHealthConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionDetector = versionDetector ?? throw new ArgumentNullException(nameof(versionDetector));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _healthHistory = new Dictionary<string, VersionHealthHistory>();
            _healthCheckTimer = new System.Timers.Timer(_configuration.HealthCheckInterval.TotalMilliseconds);
            _healthCheckTimer.Elapsed += HealthCheckTimer_Elapsed;
        }

        /// <summary>
        /// Starts health monitoring for all detected versions
        /// </summary>
        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Health monitoring is already active");
                return;
            }

            try
            {
                _logger.LogInformation("Starting version health monitoring");

                // Perform initial detection and health check
                await PerformInitialHealthCheckAsync();

                // Start the monitoring timer
                _healthCheckTimer.Start();
                _isMonitoring = true;

                _logger.LogInformation("Version health monitoring started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start version health monitoring");
                throw;
            }
        }

        /// <summary>
        /// Stops health monitoring
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Health monitoring is not active");
                return;
            }

            try
            {
                _logger.LogInformation("Stopping version health monitoring");

                _healthCheckTimer.Stop();
                _isMonitoring = false;

                _logger.LogInformation("Version health monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop version health monitoring");
                throw;
            }
        }

        /// <summary>
        /// Forces an immediate health check for all versions
        /// </summary>
        /// <returns>Health check result</returns>
        public async Task<VersionHealthCheckResult> ForceHealthCheckAsync()
        {
            return await PerformHealthCheckAsync();
        }

        /// <summary>
        /// Forces an immediate health check for a specific version
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>Health check result for the specific version</returns>
        public async Task<SingleVersionHealthCheckResult> ForceHealthCheckAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            return await CheckVersionHealthAsync(version);
        }

        /// <summary>
        /// Gets the health history for a specific version
        /// </summary>
        /// <param name="version">Version to get history for</param>
        /// <param name="maxEntries">Maximum number of history entries to return</param>
        /// <returns>Health history for the version</returns>
        public List<VersionHealthRecord> GetHealthHistory(string version, int maxEntries = 100)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            lock (_healthLock)
            {
                if (_healthHistory.TryGetValue(version, out var history))
                {
                    return history.GetRecentRecords(maxEntries);
                }
                return new List<VersionHealthRecord>();
            }
        }

        /// <summary>
        /// Gets the current health statistics for all versions
        /// </summary>
        /// <returns>Health statistics</returns>
        public VersionHealthStatistics GetHealthStatistics()
        {
            lock (_healthLock)
            {
                var stats = new VersionHealthStatistics();

                foreach (var kvp in _healthHistory)
                {
                    var version = kvp.Key;
                    var history = kvp.Value;

                    stats.TotalVersions++;

                    if (history.CurrentStatus.IsHealthy)
                        stats.HealthyVersions++;
                    else if (history.CurrentStatus.Health == VersionHealth.Degraded)
                        stats.DegradedVersions++;
                    else
                        stats.UnhealthyVersions++;

                    if (history.CurrentStatus.IsAvailable)
                        stats.AvailableVersions++;

                    if (history.CurrentStatus.HasIssues)
                        stats.VersionsWithIssues++;

                    // Calculate uptime percentage
                    var uptime = history.CalculateUptimePercentage();
                    stats.UptimePercentages[version] = uptime;
                }

                if (stats.TotalVersions > 0)
                {
                    stats.OverallHealthPercentage = (stats.HealthyVersions * 100.0) / stats.TotalVersions;
                }

                return stats;
            }
        }

        /// <summary>
        /// Performs initial health check for all detected versions
        /// </summary>
        private async Task PerformInitialHealthCheckAsync()
        {
            try
            {
                var detectionResult = await _versionDetector.DetectInstalledVersionsAsync();
                var versions = _versionDetector.DetectedVersions;

                _logger.LogInformation("Performing initial health check for {Count} versions", versions.Count);

                foreach (var version in versions)
                {
                    await InitializeVersionHealthHistoryAsync(version);
                    await CheckVersionHealthAsync(version.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial health check");
                throw;
            }
        }

        /// <summary>
        /// Initializes health history for a version
        /// </summary>
        /// <param name="version">Version to initialize</param>
        private async Task InitializeVersionHealthHistoryAsync(SolidWorksVersionInfo version)
        {
            lock (_healthLock)
            {
                if (!_healthHistory.ContainsKey(version.Version))
                {
                    var history = new VersionHealthHistory(version.Version, _configuration.MaxHistoryRecords);
                    _healthHistory[version.Version] = history;
                }
            }
        }

        /// <summary>
        /// Timer event handler for periodic health checks
        /// </summary>
        private async void HealthCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await PerformHealthCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled health check");
            }
        }

        /// <summary>
        /// Performs health check for all versions
        /// </summary>
        /// <returns>Health check result</returns>
        private async Task<VersionHealthCheckResult> PerformHealthCheckAsync()
        {
            var result = new VersionHealthCheckResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var versions = _versionDetector.DetectedVersions.ToList();
                var checkTasks = versions.Select(v => CheckVersionHealthAsync(v.Version));

                var checkResults = await Task.WhenAll(checkTasks);

                foreach (var singleResult in checkResults)
                {
                    result.VersionResults.Add(singleResult);

                    if (singleResult.HasIssues)
                    {
                        result.VersionsWithIssues++;
                    }

                    if (singleResult.IsCritical)
                    {
                        result.CriticalIssues++;
                    }
                }

                stopwatch.Stop();
                result.CheckDuration = stopwatch.Elapsed;

                _logger.LogDebug("Health check completed in {Duration}ms. Issues found: {IssuesCount}, Critical: {CriticalCount}",
                    stopwatch.ElapsedMilliseconds, result.VersionsWithIssues, result.CriticalIssues);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Performs health check for a specific version
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>Health check result for the version</returns>
        private async Task<SingleVersionHealthCheckResult> CheckVersionHealthAsync(string version)
        {
            var result = new SingleVersionHealthCheckResult { Version = version };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var versionInfo = _versionDetector.GetVersion(version);
                if (versionInfo == null)
                {
                    result.Health = VersionHealth.Unavailable;
                    result.Issues.Add("Version not found in detected versions");
                    result.IsAvailable = false;
                    return result;
                }

                // Perform various health checks
                await CheckFileSystemHealthAsync(versionInfo, result);
                await CheckExecutableHealthAsync(versionInfo, result);
                await CheckLicenseManagerHealthAsync(versionInfo, result);
                await CheckRegistryHealthAsync(versionInfo, result);
                await CheckPerformanceHealthAsync(versionInfo, result);

                // Determine overall health status
                result.Health = DetermineOverallHealth(result);
                result.IsAvailable = result.Health != VersionHealth.Unavailable && result.Health != VersionHealth.Corrupted;
                result.IsHealthy = result.Health == VersionHealth.Healthy;
                result.HasIssues = result.Issues.Count > 0;
                result.IsCritical = result.CriticalIssues.Count > 0;

                // Update health history
                await UpdateHealthHistoryAsync(version, result);

                // Raise events if status changed
                await RaiseHealthEventsAsync(version, result);

                stopwatch.Stop();
                result.CheckDuration = stopwatch.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for version {Version}", version);
                result.Error = ex.Message;
                result.Health = VersionHealth.Corrupted;
                result.IsAvailable = false;
                result.IsHealthy = false;
                result.HasIssues = true;
                result.IsCritical = true;
                return result;
            }
        }

        /// <summary>
        /// Checks file system health for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Health check result</param>
        private async Task CheckFileSystemHealthAsync(SolidWorksVersionInfo versionInfo, SingleVersionHealthCheckResult result)
        {
            try
            {
                if (!Directory.Exists(versionInfo.InstallationPath))
                {
                    result.Issues.Add($"Installation directory does not exist: {versionInfo.InstallationPath}");
                    result.CriticalIssues.Add($"Installation directory missing: {versionInfo.InstallationPath}");
                    return;
                }

                // Check directory permissions
                var testFile = Path.Combine(versionInfo.InstallationPath, $"health_check_{DateTime.UtcNow:yyyyMMddHHmmss}.tmp");
                try
                {
                    File.WriteAllText(testFile, "health check");
                    File.Delete(testFile);
                    result.FilesystemAccessible = true;
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"Cannot write to installation directory: {ex.Message}");
                    result.FilesystemAccessible = false;
                }

                // Check available disk space
                try
                {
                    var driveInfo = new DriveInfo(Path.GetPathRoot(versionInfo.InstallationPath));
                    var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    result.AvailableDiskSpaceGB = freeSpaceGB;

                    if (freeSpaceGB < _configuration.MinimumDiskSpaceGB)
                    {
                        result.Issues.Add($"Low disk space: {freeSpaceGB:F1}GB available");
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"Cannot check disk space: {ex.Message}");
                }

                // Check for log files that might indicate issues
                await CheckLogFilesAsync(versionInfo, result);
            }
            catch (Exception ex)
            {
                result.Issues.Add($"File system health check failed: {ex.Message}");
                result.FilesystemAccessible = false;
            }
        }

        /// <summary>
        /// Checks executable health for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Health check result</param>
        private async Task CheckExecutableHealthAsync(SolidWorksVersionInfo versionInfo, SingleVersionHealthCheckResult result)
        {
            try
            {
                if (!File.Exists(versionInfo.ExecutablePath))
                {
                    result.Issues.Add($"Executable file does not exist: {versionInfo.ExecutablePath}");
                    result.CriticalIssues.Add($"Executable missing: {versionInfo.ExecutablePath}");
                    return;
                }

                var fileInfo = new FileInfo(versionInfo.ExecutablePath);
                result.ExecutableSizeBytes = fileInfo.Length;

                // Check file size
                if (fileInfo.Length < _configuration.MinimumExecutableSizeBytes)
                {
                    result.Issues.Add($"Executable file is unusually small: {fileInfo.Length} bytes");
                }

                // Check file permissions
                try
                {
                    using (var stream = fileInfo.OpenRead())
                    {
                        // Can read the file
                        stream.Close();
                    }
                    result.ExecutableAccessible = true;
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"Cannot read executable file: {ex.Message}");
                    result.ExecutableAccessible = false;
                }

                // Check file integrity (basic check)
                if (fileInfo.Length > 0)
                {
                    try
                    {
                        // Try to read the first few bytes to check file integrity
                        var buffer = new byte[1024];
                        using (var stream = fileInfo.OpenRead())
                        {
                            stream.Read(buffer, 0, buffer.Length);
                        }
                        result.ExecutableIntact = true;
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add($"Executable file appears corrupted: {ex.Message}");
                        result.ExecutableIntact = false;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Executable health check failed: {ex.Message}");
                result.ExecutableAccessible = false;
            }
        }

        /// <summary>
        /// Checks license manager health for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Health check result</param>
        private async Task CheckLicenseManagerHealthAsync(SolidWorksVersionInfo versionInfo, SingleVersionHealthCheckResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.LmutilPath))
                {
                    result.Issues.Add("License manager utility path is not specified");
                    return;
                }

                if (!File.Exists(versionInfo.LmutilPath))
                {
                    result.Issues.Add($"License manager utility does not exist: {versionInfo.LmutilPath}");
                    result.LicenseManagerAccessible = false;
                    return;
                }

                // Try to run the license manager with help command
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = versionInfo.LmutilPath,
                            Arguments = "-help",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    await Task.Run(() => process.WaitForExit(_configuration.LicenseManagerTimeoutMs));

                    if (process.ExitCode == 0)
                    {
                        result.LicenseManagerAccessible = true;
                        result.LicenseManagerResponseTimeMs = process.ExitTime.Ticks - process.StartTime.Ticks;
                    }
                    else
                    {
                        result.Issues.Add($"License manager utility returned error code: {process.ExitCode}");
                        result.LicenseManagerAccessible = false;
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"Cannot execute license manager utility: {ex.Message}");
                    result.LicenseManagerAccessible = false;
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"License manager health check failed: {ex.Message}");
                result.LicenseManagerAccessible = false;
            }
        }

        /// <summary>
        /// Checks registry health for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Health check result</param>
        private async Task CheckRegistryHealthAsync(SolidWorksVersionInfo versionInfo, SingleVersionHealthCheckResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.RegistryKeyPath))
                {
                    result.Issues.Add("Registry key path is not specified");
                    return;
                }

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(versionInfo.RegistryKeyPath))
                {
                    if (key == null)
                    {
                        result.Issues.Add($"Registry key not accessible: {versionInfo.RegistryKeyPath}");
                        result.RegistryAccessible = false;
                        return;
                    }

                    // Check for required registry values
                    var requiredValues = new[] { "InstallPath", "ProductName" };
                    foreach (var valueName in requiredValues)
                    {
                        var value = key.GetValue(valueName);
                        if (value == null)
                        {
                            result.Issues.Add($"Required registry value missing: {valueName}");
                        }
                    }

                    result.RegistryAccessible = true;
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Registry health check failed: {ex.Message}");
                result.RegistryAccessible = false;
            }
        }

        /// <summary>
        /// Checks performance health for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Health check result</param>
        private async Task CheckPerformanceHealthAsync(SolidWorksVersionInfo versionInfo, SingleVersionHealthCheckResult result)
        {
            try
            {
                // Check file system performance
                if (Directory.Exists(versionInfo.InstallationPath))
                {
                    var perfTestFile = Path.Combine(versionInfo.InstallationPath, $"perf_test_{DateTime.UtcNow:yyyyMMddHHmmss}.tmp");
                    var perfStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        // Write performance test
                        var testData = new byte[1024 * 1024]; // 1MB test data
                        File.WriteAllBytes(perfTestFile, testData);
                        File.Delete(perfTestFile);

                        perfStopwatch.Stop();
                        result.FilesystemPerformanceMs = perfStopwatch.ElapsedMilliseconds;

                        if (perfStopwatch.ElapsedMilliseconds > _configuration.MaximumFileOperationMs)
                        {
                            result.Issues.Add($"Slow file system performance: {perfStopwatch.ElapsedMilliseconds}ms");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add($"File system performance test failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Performance health check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks log files for issues
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Health check result</param>
        private async Task CheckLogFilesAsync(SolidWorksVersionInfo versionInfo, SingleVersionHealthCheckResult result)
        {
            try
            {
                var logDirectory = Path.Combine(versionInfo.InstallationPath, "logs");
                if (!Directory.Exists(logDirectory))
                    return;

                var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly);
                var recentLogs = logFiles.Where(f => File.GetLastWriteTimeUtc(f) > DateTime.UtcNow.AddDays(-1));

                foreach (var logFile in recentLogs)
                {
                    try
                    {
                        var logContent = await File.ReadAllTextAsync(logFile);
                        if (logContent.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                            logContent.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                            logContent.Contains("corrupt", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Issues.Add($"Potential issues found in log file: {Path.GetFileName(logFile)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add($"Cannot read log file {logFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Log file check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines overall health status from check results
        /// </summary>
        /// <param name="result">Health check result</param>
        /// <returns>Overall health status</returns>
        private VersionHealth DetermineOverallHealth(SingleVersionHealthCheckResult result)
        {
            if (result.CriticalIssues.Count > 0)
                return VersionHealth.Corrupted;

            if (result.Issues.Count == 0)
                return VersionHealth.Healthy;

            if (result.Issues.Count <= 2)
                return VersionHealth.Degraded;

            return VersionHealth.PerformanceIssues;
        }

        /// <summary>
        /// Updates health history for a version
        /// </summary>
        /// <param name="version">Version to update</param>
        /// <param name="result">Health check result</param>
        private async Task UpdateHealthHistoryAsync(string version, SingleVersionHealthCheckResult result)
        {
            lock (_healthLock)
            {
                if (_healthHistory.TryGetValue(version, out var history))
                {
                    var record = new VersionHealthRecord
                    {
                        Timestamp = DateTime.UtcNow,
                        Health = result.Health,
                        IsAvailable = result.IsAvailable,
                        Issues = result.Issues.ToList(),
                        CriticalIssues = result.CriticalIssues.ToList(),
                        Metrics = new Dictionary<string, object>
                        {
                            ["CheckDurationMs"] = result.CheckDuration.TotalMilliseconds,
                            ["ExecutableAccessible"] = result.ExecutableAccessible,
                            ["LicenseManagerAccessible"] = result.LicenseManagerAccessible,
                            ["RegistryAccessible"] = result.RegistryAccessible,
                            ["FilesystemAccessible"] = result.FilesystemAccessible,
                            ["AvailableDiskSpaceGB"] = result.AvailableDiskSpaceGB,
                            ["ExecutableSizeBytes"] = result.ExecutableSizeBytes
                        }
                    };

                    history.AddRecord(record);
                }
            }
        }

        /// <summary>
        /// Raises health events if status changed
        /// </summary>
        /// <param name="version">Version that was checked</param>
        /// <param name="result">Health check result</param>
        private async Task RaiseHealthEventsAsync(string version, SingleVersionHealthCheckResult result)
        {
            lock (_healthLock)
            {
                if (_healthHistory.TryGetValue(version, out var history))
                {
                    var previousHealth = history.PreviousStatus?.Health ?? VersionHealth.Unknown;
                    var currentHealth = result.Health;

                    if (previousHealth != currentHealth)
                    {
                        var eventArgs = new VersionHealthChangedEventArgs
                        {
                            Version = version,
                            PreviousHealth = previousHealth,
                            CurrentHealth = currentHealth,
                            Timestamp = DateTime.UtcNow,
                            Issues = result.Issues,
                            CriticalIssues = result.CriticalIssues
                        };

                        HealthStatusChanged?.Invoke(this, eventArgs);
                    }

                    if (result.IsCritical)
                    {
                        var criticalArgs = new VersionHealthCriticalEventArgs
                        {
                            Version = version,
                            Health = currentHealth,
                            Timestamp = DateTime.UtcNow,
                            CriticalIssues = result.CriticalIssues,
                            AllIssues = result.Issues
                        };

                        CriticalHealthIssueDetected?.Invoke(this, criticalArgs);
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the health monitor
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the health monitor
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_isMonitoring)
                    {
                        StopMonitoring();
                    }

                    _healthCheckTimer?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Configuration for version health monitoring
    /// </summary>
    public class VersionHealthConfiguration
    {
        /// <summary>
        /// Gets or sets the interval between health checks
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the maximum number of history records to keep per version
        /// </summary>
        public int MaxHistoryRecords { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the minimum required disk space in GB
        /// </summary>
        public double MinimumDiskSpaceGB { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the minimum executable size in bytes
        /// </summary>
        public long MinimumExecutableSizeBytes { get; set; } = 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum allowed file operation time in milliseconds
        /// </summary>
        public int MaximumFileOperationMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the license manager timeout in milliseconds
        /// </summary>
        public int LicenseManagerTimeoutMs { get; set; } = 5000;
    }

    /// <summary>
    /// Represents the current health status of a version
    /// </summary>
    public class VersionHealthStatus
    {
        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is available
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has any issues
        /// </summary>
        public bool HasIssues { get; set; }

        /// <summary>
        /// Gets or sets the last check timestamp
        /// </summary>
        public DateTime LastCheck { get; set; }

        /// <summary>
        /// Gets or sets the uptime percentage
        /// </summary>
        public double UptimePercentage { get; set; }
    }

    /// <summary>
    /// Manages health history for a single version
    /// </summary>
    public class VersionHealthHistory
    {
        private readonly string _version;
        private readonly int _maxRecords;
        private readonly Queue<VersionHealthRecord> _records;
        private readonly object _lock = new object();

        /// <summary>
        /// Gets the current health status
        /// </summary>
        public VersionHealthStatus CurrentStatus { get; private set; }

        /// <summary>
        /// Gets the previous health status
        /// </summary>
        public VersionHealthStatus PreviousStatus { get; private set; }

        /// <summary>
        /// Initializes a new instance of the VersionHealthHistory class
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="maxRecords">Maximum number of records to keep</param>
        public VersionHealthHistory(string version, int maxRecords)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _maxRecords = maxRecords;
            _records = new Queue<VersionHealthRecord>();
            CurrentStatus = new VersionHealthStatus();
        }

        /// <summary>
        /// Adds a health record to the history
        /// </summary>
        /// <param name="record">Health record to add</param>
        public void AddRecord(VersionHealthRecord record)
        {
            lock (_lock)
            {
                PreviousStatus = CurrentStatus;

                _records.Enqueue(record);
                while (_records.Count > _maxRecords)
                {
                    _records.Dequeue();
                }

                UpdateCurrentStatus(record);
            }
        }

        /// <summary>
        /// Gets recent health records
        /// </summary>
        /// <param name="maxEntries">Maximum number of entries to return</param>
        /// <returns>List of recent health records</returns>
        public List<VersionHealthRecord> GetRecentRecords(int maxEntries)
        {
            lock (_lock)
            {
                return _records.TakeLast(maxEntries).ToList();
            }
        }

        /// <summary>
        /// Calculates uptime percentage based on health history
        /// </summary>
        /// <returns>Uptime percentage (0.0-100.0)</returns>
        public double CalculateUptimePercentage()
        {
            lock (_lock)
            {
                if (_records.Count == 0)
                    return 0.0;

                var healthyCount = _records.Count(r => r.IsAvailable);
                return (healthyCount * 100.0) / _records.Count;
            }
        }

        /// <summary>
        /// Updates the current health status based on a record
        /// </summary>
        /// <param name="record">Health record</param>
        private void UpdateCurrentStatus(VersionHealthRecord record)
        {
            CurrentStatus = new VersionHealthStatus
            {
                Health = record.Health,
                IsAvailable = record.IsAvailable,
                IsHealthy = record.Health == VersionHealth.Healthy,
                HasIssues = record.Issues.Count > 0 || record.CriticalIssues.Count > 0,
                LastCheck = record.Timestamp,
                UptimePercentage = CalculateUptimePercentage()
            };
        }
    }

    /// <summary>
    /// Represents a single health check record
    /// </summary>
    public class VersionHealthRecord
    {
        /// <summary>
        /// Gets or sets the timestamp of the check
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version was available
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the list of issues found
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the list of critical issues found
        /// </summary>
        public List<string> CriticalIssues { get; set; }

        /// <summary>
        /// Gets or sets additional metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; }

        /// <summary>
        /// Initializes a new instance of the VersionHealthRecord class
        /// </summary>
        public VersionHealthRecord()
        {
            Issues = new List<string>();
            CriticalIssues = new List<string>();
            Metrics = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Result of a health check operation
    /// </summary>
    public class VersionHealthCheckResult
    {
        /// <summary>
        /// Gets or sets the duration of the health check
        /// </summary>
        public TimeSpan CheckDuration { get; set; }

        /// <summary>
        /// Gets or sets the number of versions with issues
        /// </summary>
        public int VersionsWithIssues { get; set; }

        /// <summary>
        /// Gets or sets the number of critical issues
        /// </summary>
        public int CriticalIssues { get; set; }

        /// <summary>
        /// Gets or sets the error message if the check failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets the list of individual version results
        /// </summary>
        public List<SingleVersionHealthCheckResult> VersionResults { get; }

        /// <summary>
        /// Gets a value indicating whether the check was successful
        /// </summary>
        public bool Success => string.IsNullOrWhiteSpace(Error);

        /// <summary>
        /// Initializes a new instance of the VersionHealthCheckResult class
        /// </summary>
        public VersionHealthCheckResult()
        {
            VersionResults = new List<SingleVersionHealthCheckResult>();
            CheckDuration = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Result of a health check for a single version
    /// </summary>
    public class SingleVersionHealthCheckResult
    {
        /// <summary>
        /// Gets or sets the version that was checked
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets the duration of the check
        /// </summary>
        public TimeSpan CheckDuration { get; set; }

        /// <summary>
        /// Gets or sets the available disk space in GB
        /// </summary>
        public double AvailableDiskSpaceGB { get; set; }

        /// <summary>
        /// Gets or sets the executable size in bytes
        /// </summary>
        public long ExecutableSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the executable is accessible
        /// </summary>
        public bool ExecutableAccessible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the license manager is accessible
        /// </summary>
        public bool LicenseManagerAccessible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the registry is accessible
        /// </summary>
        public bool RegistryAccessible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the file system is accessible
        /// </summary>
        public bool FilesystemAccessible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the executable is intact
        /// </summary>
        public bool ExecutableIntact { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is available
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there are any issues
        /// </summary>
        public bool HasIssues { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there are critical issues
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets or sets the error message if the check failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets the list of issues found
        /// </summary>
        public List<string> Issues { get; }

        /// <summary>
        /// Gets the list of critical issues found
        /// </summary>
        public List<string> CriticalIssues { get; set; }

        /// <summary>
        /// Initializes a new instance of the SingleVersionHealthCheckResult class
        /// </summary>
        public SingleVersionHealthCheckResult()
        {
            Issues = new List<string>();
            CriticalIssues = new List<string>();
            CheckDuration = TimeSpan.Zero;
            Health = VersionHealth.Unknown;
        }
    }

    /// <summary>
    /// Health statistics for all versions
    /// </summary>
    public class VersionHealthStatistics
    {
        /// <summary>
        /// Gets or sets the total number of versions
        /// </summary>
        public int TotalVersions { get; set; }

        /// <summary>
        /// Gets or sets the number of healthy versions
        /// </summary>
        public int HealthyVersions { get; set; }

        /// <summary>
        /// Gets or sets the number of degraded versions
        /// </summary>
        public int DegradedVersions { get; set; }

        /// <summary>
        /// Gets or sets the number of unhealthy versions
        /// </summary>
        public int UnhealthyVersions { get; set; }

        /// <summary>
        /// Gets or sets the number of available versions
        /// </summary>
        public int AvailableVersions { get; set; }

        /// <summary>
        /// Gets or sets the number of versions with issues
        /// </summary>
        public int VersionsWithIssues { get; set; }

        /// <summary>
        /// Gets or sets the overall health percentage
        /// </summary>
        public double OverallHealthPercentage { get; set; }

        /// <summary>
        /// Gets the uptime percentages by version
        /// </summary>
        public Dictionary<string, double> UptimePercentages { get; }

        /// <summary>
        /// Initializes a new instance of the VersionHealthStatistics class
        /// </summary>
        public VersionHealthStatistics()
        {
            UptimePercentages = new Dictionary<string, double>();
        }
    }

    /// <summary>
    /// Event arguments for health status changes
    /// </summary>
    public class VersionHealthChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the version that changed
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the previous health status
        /// </summary>
        public VersionHealth PreviousHealth { get; set; }

        /// <summary>
        /// Gets or sets the current health status
        /// </summary>
        public VersionHealth CurrentHealth { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the change
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the list of issues
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the list of critical issues
        /// </summary>
        public List<string> CriticalIssues { get; set; }
    }

    /// <summary>
    /// Event arguments for critical health issues
    /// </summary>
    public class VersionHealthCriticalEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the version with critical issues
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the issue
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the list of critical issues
        /// </summary>
        public List<string> CriticalIssues { get; set; }

        /// <summary>
        /// Gets or sets the list of all issues
        /// </summary>
        public List<string> AllIssues { get; set; }
    }
}