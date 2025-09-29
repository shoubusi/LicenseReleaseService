using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Performance counter integration for system monitoring
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX(uint size)
            {
                dwLength = size;
                dwMemoryLoad = 0;
                ullTotalPhys = 0;
                ullAvailPhys = 0;
                ullTotalPageFile = 0;
                ullAvailPageFile = 0;
                ullTotalVirtual = 0;
                ullAvailVirtual = 0;
                ullAvailExtendedVirtual = 0;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly PerformanceMonitorConfig _config;
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<int, ProcessPerformanceHistory> _processHistories = new ConcurrentDictionary<int, ProcessPerformanceHistory>();
        private readonly Dictionary<string, PerformanceCounter> _systemCounters = new Dictionary<string, PerformanceCounter>();
        private readonly ConcurrentDictionary<int, Dictionary<string, PerformanceCounter>> _processCounters = new ConcurrentDictionary<int, Dictionary<string, PerformanceCounter>>();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private bool _isDisposed;
        private bool _isRunning;

        #region Windows API Imports

        [DllImport("psapi.dll")]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX pmc, uint cb);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS_EX
        {
            public uint cb;
            public uint PageFaultCount;
            public ulong PeakWorkingSetSize;
            public ulong WorkingSetSize;
            public ulong QuotaPeakPagedPoolUsage;
            public ulong QuotaPagedPoolUsage;
            public ulong QuotaPeakNonPagedPoolUsage;
            public ulong QuotaNonPagedPoolUsage;
            public ulong PagefileUsage;
            public ulong PeakPagefileUsage;
            public ulong PrivateUsage;
        }

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when performance metrics are updated
        /// </summary>
        public event EventHandler<PerformanceMetricsEventArgs> PerformanceMetricsUpdated;

        /// <summary>
        /// Event raised when performance threshold is exceeded
        /// </summary>
        public event EventHandler<PerformanceThresholdEventArgs> PerformanceThresholdExceeded;

        /// <summary>
        /// Gets a value indicating whether the monitor is running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the monitor statistics
        /// </summary>
        public PerformanceMonitorStatistics Statistics { get; private set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the PerformanceMonitor class
        /// </summary>
        public PerformanceMonitor(ILogger<PerformanceMonitor> logger, PerformanceMonitorConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Statistics = new PerformanceMonitorStatistics();

            // Initialize cleanup timer for old performance histories
            _cleanupTimer = new Timer(CleanupTimerCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            _logger.LogInformation("PerformanceMonitor initialized with configuration: {Config}", _config);
        }

        /// <summary>
        /// Initializes the monitor with configuration
        /// </summary>
        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing PerformanceMonitor");

                lock (_lock)
                {
                    if (_isRunning)
                    {
                        _logger.LogWarning("PerformanceMonitor is already running");
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Clear any existing counters and histories
                lock (_lock)
                {
                    // PerformanceCounter doesn't implement IDisposable in .NET Framework 4.8
                    // No need to dispose counters
                    _systemCounters.Clear();

                    foreach (var processCounters in _processCounters.Values)
                    {
                        // PerformanceCounter doesn't implement IDisposable in .NET Framework 4.8
                        // No need to dispose counters
                    }
                    _processCounters.Clear();
                    _processHistories.Clear();
                    Statistics.Reset();
                }

                // Initialize system performance counters
                await InitializeSystemCountersAsync();

                _logger.LogInformation("PerformanceMonitor initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing PerformanceMonitor");
                throw;
            }
        }

        /// <summary>
        /// Starts the performance monitoring
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting PerformanceMonitor");

                lock (_lock)
                {
                    if (_isRunning)
                    {
                        _logger.LogWarning("PerformanceMonitor is already running");
                        return;
                    }

                    _isRunning = true;
                }

                // Start monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("PerformanceMonitor started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting PerformanceMonitor");
                throw;
            }
        }

        /// <summary>
        /// Stops the performance monitoring
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping PerformanceMonitor");

                lock (_lock)
                {
                    if (!_isRunning)
                    {
                        _logger.LogWarning("PerformanceMonitor is not running");
                        return;
                    }

                    _isRunning = false;
                }

                // Stop monitoring task
                _cancellationTokenSource?.Cancel();

                // Wait for monitoring task to complete
                if (_monitoringTask != null)
                {
                    await Task.WhenAny(_monitoringTask, Task.Delay(TimeSpan.FromSeconds(5)));
                }

                _logger.LogInformation("PerformanceMonitor stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping PerformanceMonitor");
                throw;
            }
        }

        /// <summary>
        /// Pauses the performance monitoring
        /// </summary>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing PerformanceMonitor");

                lock (_lock)
                {
                    if (!_isRunning)
                    {
                        _logger.LogWarning("PerformanceMonitor is not running");
                        return;
                    }

                    _isRunning = false;
                }

                _cancellationTokenSource?.Cancel();

                _logger.LogInformation("PerformanceMonitor paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing PerformanceMonitor");
                throw;
            }
        }

        /// <summary>
        /// Resumes the performance monitoring
        /// </summary>
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming PerformanceMonitor");

                lock (_lock)
                {
                    if (_isRunning)
                    {
                        _logger.LogWarning("PerformanceMonitor is already running");
                        return;
                    }

                    _isRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Restart monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("PerformanceMonitor resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming PerformanceMonitor");
                throw;
            }
        }

        /// <summary>
        /// Gets performance activities for a specific process
        /// </summary>
        public async Task<List<ActivityData>> GetProcessActivitiesAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_processHistories.TryGetValue(processId, out var history))
                {
                    var recentMetrics = history.GetRecentMetrics(TimeSpan.FromMinutes(30));
                    return recentMetrics.Select(m => new ActivityData
                    {
                        Type = ActivityType.Performance,
                        Timestamp = m.Timestamp,
                        Confidence = CalculateActivityConfidence(m),
                        Source = "PerformanceMonitor",
                        Details = new Dictionary<string, object>
                        {
                            ["CpuUsage"] = m.CpuUsage,
                            ["MemoryUsage"] = m.MemoryUsage,
                            ["DiskUsage"] = m.DiskUsage,
                            ["NetworkUsage"] = m.NetworkUsage,
                            ["HandleCount"] = m.HandleCount,
                            ["ThreadCount"] = m.ThreadCount,
                            ["GpuUsage"] = m.GpuUsage
                        }
                    }).ToList();
                }

                return new List<ActivityData>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance activities for process {ProcessId}", processId);
                return new List<ActivityData>();
            }
        }

        /// <summary>
        /// Updates the monitor configuration
        /// </summary>
        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating PerformanceMonitor configuration");

                if (configuration?.CustomParameters != null)
                {
                    // Update performance monitoring specific parameters
                    if (configuration.CustomParameters.ContainsKey("EnablePerformanceMonitoring"))
                    {
                        _config.EnablePerformanceMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnablePerformanceMonitoring"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("EnableCpuMonitoring"))
                    {
                        _config.EnableCpuMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableCpuMonitoring"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("EnableMemoryMonitoring"))
                    {
                        _config.EnableMemoryMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableMemoryMonitoring"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("EnableDiskMonitoring"))
                    {
                        _config.EnableDiskMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableDiskMonitoring"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("EnableNetworkMonitoring"))
                    {
                        _config.EnableNetworkMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableNetworkMonitoring"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("CpuUsageThreshold"))
                    {
                        _config.CpuUsageThreshold = Convert.ToDouble(configuration.CustomParameters["CpuUsageThreshold"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("MemoryUsageThreshold"))
                    {
                        _config.MemoryUsageThreshold = Convert.ToDouble(configuration.CustomParameters["EnableMemoryMonitoring"]);
                    }
                }

                // Re-initialize counters if configuration changed
                if (_isRunning)
                {
                    await StopAsync(cancellationToken);
                    await InitializeAsync(configuration, cancellationToken);
                    await StartAsync(cancellationToken);
                }

                _logger.LogInformation("PerformanceMonitor configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PerformanceMonitor configuration");
                throw;
            }
        }

        /// <summary>
        /// Gets the health status of the monitor
        /// </summary>
        public async Task<DetectorHealth> GetHealthAsync()
        {
            try
            {
                var health = new DetectorHealth
                {
                    DetectorName = "PerformanceMonitor",
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                var isHealthy = true;
                var issues = new List<string>();

                // Check service status
                if (!_isRunning)
                {
                    issues.Add("Monitor is not running");
                    isHealthy = false;
                }

                // Check counter availability
                lock (_lock)
                {
                    if (_systemCounters.Count == 0)
                    {
                        issues.Add("No system performance counters available");
                    }

                    if (_processCounters.Count > 100)
                    {
                        issues.Add($"High process counter count: {_processCounters.Count}");
                    }
                }

                // Check performance history size
                if (_processHistories.Count > 1000)
                {
                    issues.Add($"High performance history count: {_processHistories.Count}");
                }

                // Check statistics for abnormal patterns
                if (Statistics.TotalMetricsCollected > 0)
                {
                    var errorRate = (double)Statistics.ErrorCount / Statistics.TotalMetricsCollected;
                    if (errorRate > 0.1)
                    {
                        issues.Add($"High error rate: {errorRate:P1}");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["SystemCounterCount"] = _systemCounters.Count;
                health.AdditionalInfo["ProcessCounterCount"] = _processCounters.Count;
                health.AdditionalInfo["PerformanceHistoryCount"] = _processHistories.Count;
                health.AdditionalInfo["TotalMetricsCollected"] = Statistics.TotalMetricsCollected;
                health.AdditionalInfo["SuccessfulCollections"] = Statistics.SuccessfulCollections;
                health.AdditionalInfo["FailedCollections"] = Statistics.FailedCollections;
                health.AdditionalInfo["ErrorCount"] = Statistics.ErrorCount;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting PerformanceMonitor health");
                return new DetectorHealth
                {
                    DetectorName = "PerformanceMonitor",
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        /// <summary>
        /// Gets monitor statistics
        /// </summary>
        public async Task<PerformanceMonitorStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                return new PerformanceMonitorStatistics
                {
                    TotalMetricsCollected = Statistics.TotalMetricsCollected,
                    CpuMetricsCollected = Statistics.CpuMetricsCollected,
                    MemoryMetricsCollected = Statistics.MemoryMetricsCollected,
                    DiskMetricsCollected = Statistics.DiskMetricsCollected,
                    NetworkMetricsCollected = Statistics.NetworkMetricsCollected,
                    GpuMetricsCollected = Statistics.GpuMetricsCollected,
                    ThresholdExceededEvents = Statistics.ThresholdExceededEvents,
                    SuccessfulCollections = Statistics.SuccessfulCollections,
                    FailedCollections = Statistics.FailedCollections,
                    ErrorCount = Statistics.ErrorCount,
                    AverageCollectionTimeMs = Statistics.AverageCollectionTimeMs,
                    StartTime = Statistics.StartTime,
                    LastCollectionTime = Statistics.LastCollectionTime
                };
            }
        }

        /// <summary>
        /// Resets monitor statistics
        /// </summary>
        public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    Statistics.Reset();
                    _processHistories.Clear();
                }

                _logger.LogInformation("PerformanceMonitor statistics reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting PerformanceMonitor statistics");
                throw;
            }
        }

        #region Private Methods

        /// <summary>
        /// Initializes system performance counters
        /// </summary>
        private async Task InitializeSystemCountersAsync()
        {
            try
            {
                if (!_config.EnablePerformanceMonitoring)
                {
                    _logger.LogInformation("Performance monitoring is disabled");
                    return;
                }

                var countersToInitialize = new List<(string name, string category, string counter, string instance)>();

                // CPU counters
                if (_config.EnableCpuMonitoring)
                {
                    countersToInitialize.Add(("CPU_Total", "Processor", "% Processor Time", "_Total"));
                    countersToInitialize.Add(("CPU_Usage", "Processor", "% Processor Time", "_Total"));
                }

                // Memory counters
                if (_config.EnableMemoryMonitoring)
                {
                    countersToInitialize.Add(("Memory_Available", "Memory", "Available MBytes", null));
                    countersToInitialize.Add(("Memory_Committed", "Memory", "% Committed Bytes In Use", null));
                    countersToInitialize.Add(("Memory_PagesPerSec", "Memory", "Pages/sec", null));
                }

                // Disk counters
                if (_config.EnableDiskMonitoring)
                {
                    countersToInitialize.Add(("Disk_ReadBytesPerSec", "PhysicalDisk", "Disk Read Bytes/sec", "_Total"));
                    countersToInitialize.Add(("Disk_WriteBytesPerSec", "PhysicalDisk", "Disk Write Bytes/sec", "_Total"));
                    countersToInitialize.Add(("Disk_QueueLength", "PhysicalDisk", "Current Disk Queue Length", "_Total"));
                }

                // Network counters
                if (_config.EnableNetworkMonitoring)
                {
                    countersToInitialize.Add(("Network_BytesReceivedPerSec", "Network Interface", "Bytes Received/sec", null));
                    countersToInitialize.Add(("Network_BytesSentPerSec", "Network Interface", "Bytes Sent/sec", null));
                }

                foreach (var (name, category, counter, instance) in countersToInitialize)
                {
                    try
                    {
                        var perfCounter = new PerformanceCounter(category, counter, instance);
                        perfCounter.NextValue(); // Initialize counter

                        lock (_lock)
                        {
                            _systemCounters[name] = perfCounter;
                        }

                        _logger.LogDebug("Initialized performance counter: {Name}", name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to initialize performance counter: {Name}", name);
                        // Continue with other counters
                    }
                }

                _logger.LogInformation("System performance counters initialized: {Count}/{Total}",
                    _systemCounters.Count, countersToInitialize.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing system performance counters");
                throw;
            }
        }

        /// <summary>
        /// Main monitoring loop
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting performance monitoring loop");

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // Wait for the monitoring interval
                    await Task.Delay(TimeSpan.FromSeconds(_config.MonitoringIntervalSeconds), cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Monitor system performance
                    await MonitorSystemPerformanceAsync(cancellationToken);

                    // Monitor active SolidWorks processes
                    await MonitorSolidWorksProcessesAsync(cancellationToken);

                    // Clean up old performance histories
                    CleanupOldPerformanceHistories();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in performance monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Delay on error
                }
            }

            _logger.LogDebug("Performance monitoring loop stopped");
        }

        /// <summary>
        /// Monitors system performance
        /// </summary>
        private async Task MonitorSystemPerformanceAsync(CancellationToken cancellationToken)
        {
            try
            {
                var systemMetrics = new SystemPerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow
                };

                // Collect system counter values
                lock (_lock)
                {
                    if (_systemCounters.TryGetValue("CPU_Total", out var cpuCounter))
                    {
                        try
                        {
                            systemMetrics.CpuUsage = cpuCounter.NextValue();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading CPU counter");
                        }
                    }

                    if (_systemCounters.TryGetValue("Memory_Available", out var memCounter))
                    {
                        try
                        {
                            var availableMB = memCounter.NextValue();
                            var totalMemory = GetTotalSystemMemory();
                            systemMetrics.MemoryUsage = totalMemory > 0 ? (totalMemory - availableMB) / totalMemory * 100 : 0;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading memory counter");
                        }
                    }

                    if (_systemCounters.TryGetValue("Disk_ReadBytesPerSec", out var diskReadCounter))
                    {
                        try
                        {
                            systemMetrics.DiskReadBytesPerSec = diskReadCounter.NextValue();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading disk read counter");
                        }
                    }

                    if (_systemCounters.TryGetValue("Disk_WriteBytesPerSec", out var diskWriteCounter))
                    {
                        try
                        {
                            systemMetrics.DiskWriteBytesPerSec = diskWriteCounter.NextValue();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading disk write counter");
                        }
                    }

                    if (_systemCounters.TryGetValue("Network_BytesReceivedPerSec", out var netReceiveCounter))
                    {
                        try
                        {
                            systemMetrics.NetworkBytesReceivedPerSec = netReceiveCounter.NextValue();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading network receive counter");
                        }
                    }

                    if (_systemCounters.TryGetValue("Network_BytesSentPerSec", out var netSendCounter))
                    {
                        try
                        {
                            systemMetrics.NetworkBytesSentPerSec = netSendCounter.NextValue();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading network send counter");
                        }
                    }
                }

                // Calculate derived metrics
                systemMetrics.DiskUsage = CalculateDiskUsage(systemMetrics.DiskReadBytesPerSec, systemMetrics.DiskWriteBytesPerSec);
                systemMetrics.NetworkUsage = CalculateNetworkUsage(systemMetrics.NetworkBytesReceivedPerSec, systemMetrics.NetworkBytesSentPerSec);

                // Check system thresholds
                CheckSystemThresholds(systemMetrics);

                // Update statistics
                lock (_lock)
                {
                    Statistics.TotalMetricsCollected++;
                    Statistics.LastCollectionTime = DateTime.UtcNow;
                    Statistics.SuccessfulCollections++;
                }

                _logger.LogDebug("System performance metrics collected: CPU={CpuUsage:F1}%, Memory={MemoryUsage:F1}%",
                    systemMetrics.CpuUsage, systemMetrics.MemoryUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring system performance");
                lock (_lock)
                {
                    Statistics.ErrorCount++;
                    Statistics.FailedCollections++;
                }
            }
        }

        /// <summary>
        /// Monitors SolidWorks processes performance
        /// </summary>
        private async Task MonitorSolidWorksProcessesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var solidWorksProcesses = System.Diagnostics.Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited && p.Responding)
                    .ToList();

                foreach (var process in solidWorksProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await MonitorProcessPerformanceAsync(process, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error monitoring process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring SolidWorks processes");
            }
        }

        /// <summary>
        /// Monitors performance for a specific process
        /// </summary>
        private async Task MonitorProcessPerformanceAsync(System.Diagnostics.Process process, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var metrics = await CollectProcessMetricsAsync(process);

                // Record metrics in process history
                var history = _processHistories.GetOrAdd(process.Id, id => new ProcessPerformanceHistory(id));
                history.RecordMetrics(metrics);

                // Calculate activity level
                var activityLevel = CalculateProcessActivityLevel(metrics);

                // Update process-specific counters
                await UpdateProcessCountersAsync(process.Id, metrics);

                // Check process thresholds
                CheckProcessThresholds(process.Id, metrics);

                // Raise performance metrics updated event
                PerformanceMetricsUpdated?.Invoke(this, new PerformanceMetricsEventArgs
                {
                    ProcessId = process.Id,
                    CpuUsage = metrics.CpuUsage,
                    MemoryUsage = metrics.MemoryUsage,
                    DiskUsage = metrics.DiskUsage,
                    NetworkUsage = metrics.NetworkUsage,
                    ActivityLevel = activityLevel,
                    Timestamp = DateTime.UtcNow,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["HandleCount"] = metrics.HandleCount,
                        ["ThreadCount"] = metrics.ThreadCount,
                        ["GpuUsage"] = metrics.GpuUsage,
                        ["WorkingSetSize"] = metrics.WorkingSetSize,
                        ["PrivateMemorySize"] = metrics.PrivateMemorySize,
                        ["ProcessName"] = process.ProcessName
                    }
                });

                // Update statistics
                var collectionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                lock (_lock)
                {
                    Statistics.TotalMetricsCollected++;
                    Statistics.CpuMetricsCollected++;
                    Statistics.MemoryMetricsCollected++;
                    Statistics.DiskMetricsCollected++;
                    Statistics.NetworkMetricsCollected++;
                    Statistics.AverageCollectionTimeMs = (Statistics.AverageCollectionTimeMs * (Statistics.TotalMetricsCollected - 1) + collectionTime) / Statistics.TotalMetricsCollected;
                    Statistics.SuccessfulCollections++;
                }

                _logger.LogDebug("Process performance metrics collected for {ProcessId}: CPU={CpuUsage:F1}%, Memory={MemoryUsage:F1}%",
                    process.Id, metrics.CpuUsage, metrics.MemoryUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring process {ProcessId}", process.Id);
                lock (_lock)
                {
                    Statistics.ErrorCount++;
                    Statistics.FailedCollections++;
                }
            }
        }

        /// <summary>
        /// Collects performance metrics for a specific process
        /// </summary>
        private async Task<ProcessPerformanceMetrics> CollectProcessMetricsAsync(System.Diagnostics.Process process)
        {
            var metrics = new ProcessPerformanceMetrics
            {
                ProcessId = process.Id,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // CPU usage
                metrics.CpuUsage = await GetProcessCpuUsageAsync(process);

                // Memory usage
                metrics.MemoryUsage = await GetProcessMemoryUsageAsync(process);

                // Process information
                metrics.HandleCount = process.HandleCount;
                metrics.ThreadCount = process.Threads.Count;

                // Working set and private memory
                try
                {
                    metrics.WorkingSetSize = process.WorkingSet64;
                    metrics.PrivateMemorySize = process.PrivateMemorySize64;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error getting memory info for process {ProcessId}", process.Id);
                }

                // GPU usage (if available)
                metrics.GpuUsage = await GetProcessGpuUsageAsync(process);

                // Disk usage (simplified - based on I/O counters)
                try
                {
                    metrics.DiskUsage = await GetProcessDiskUsageAsync(process);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error getting disk usage for process {ProcessId}", process.Id);
                    metrics.DiskUsage = 0;
                }

                // Network usage (simplified - hard to track per process without advanced APIs)
                metrics.NetworkUsage = 0; // Placeholder

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics for process {ProcessId}", process.Id);
                return metrics;
            }
        }

        /// <summary>
        /// Gets CPU usage for a process
        /// </summary>
        private async Task<double> GetProcessCpuUsageAsync(System.Diagnostics.Process process)
        {
            try
            {
                // Get process CPU time
                var cpuTime = process.TotalProcessorTime;
                await Task.Delay(100); // Wait for measurement
                var newCpuTime = process.TotalProcessorTime;
                var cpuUsage = (newCpuTime - cpuTime).TotalMilliseconds / 100.0; // Percentage

                return Math.Min(100.0, Math.Max(0.0, cpuUsage));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting CPU usage for process {ProcessId}", process.Id);
                return 0;
            }
        }

        /// <summary>
        /// Gets memory usage for a process
        /// </summary>
        private async Task<double> GetProcessMemoryUsageAsync(System.Diagnostics.Process process)
        {
            try
            {
                // Get process handle
                var processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)process.Id);
                if (processHandle == IntPtr.Zero)
                    return 0;

                // Get memory information
                var memInfo = new PROCESS_MEMORY_COUNTERS_EX();
                memInfo.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));

                if (GetProcessMemoryInfo(processHandle, out memInfo, memInfo.cb))
                {
                    // Calculate memory usage percentage
                    var totalMemory = GetTotalSystemMemory();
                    var memoryUsage = totalMemory > 0 ? (double)memInfo.PrivateUsage / totalMemory * 100 : 0;

                    CloseHandle(processHandle);
                    return Math.Min(100.0, Math.Max(0.0, memoryUsage));
                }

                CloseHandle(processHandle);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting memory usage for process {ProcessId}", process.Id);
                return 0;
            }
        }

        /// <summary>
        /// Gets GPU usage for a process (simplified implementation)
        /// </summary>
        private async Task<double> GetProcessGpuUsageAsync(System.Diagnostics.Process process)
        {
            try
            {
                // This is a simplified implementation
                // In a real-world scenario, you might use NVIDIA NvAPI, AMD ADL, or Windows GPU APIs
                // For now, we'll estimate based on memory usage and process type

                if (process.ProcessName.IndexOf("SLDWORKS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // SolidWorks processes likely use GPU for rendering
                    var memoryUsage = await GetProcessMemoryUsageAsync(process);
                    return Math.Min(100.0, memoryUsage * 0.5); // Assume up to 50% of memory usage correlates to GPU usage
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting GPU usage for process {ProcessId}", process.Id);
                return 0;
            }
        }

        /// <summary>
        /// Gets disk usage for a process
        /// </summary>
        private async Task<double> GetProcessDiskUsageAsync(System.Diagnostics.Process process)
        {
            try
            {
                // This is a simplified implementation
                // In a real-world scenario, you would track I/O operations over time
                var startTime = DateTime.UtcNow;
                var startIo = process.TotalProcessorTime; // Placeholder - real implementation would use I/O counters

                await Task.Delay(100);

                var endIo = process.TotalProcessorTime; // Placeholder
                var ioDelta = (endIo - startIo).TotalMilliseconds;

                // Convert to percentage (simplified)
                return Math.Min(100.0, Math.Max(0.0, ioDelta / 10.0));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting disk usage for process {ProcessId}", process.Id);
                return 0;
            }
        }

        /// <summary>
        /// Gets total system memory
        /// </summary>
        private ulong GetTotalSystemMemory()
        {
            try
            {
                // Use memory status for .NET Framework 4.8
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    return memStatus.ullTotalPhys;
                }
                return 8UL * 1024 * 1024 * 1024; // 8GB fallback
            }
            catch
            {
                return 8UL * 1024 * 1024 * 1024; // 8GB fallback
            }
        }

        /// <summary>
        /// Calculates disk usage percentage
        /// </summary>
        private double CalculateDiskUsage(double readBytesPerSec, double writeBytesPerSec)
        {
            try
            {
                // Simplified disk usage calculation
                var totalBytesPerSec = readBytesPerSec + writeBytesPerSec;
                var maxBytesPerSec = 100 * 1024 * 1024; // 100MB/s as maximum

                return Math.Min(100.0, Math.Max(0.0, totalBytesPerSec / maxBytesPerSec * 100));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating disk usage");
                return 0;
            }
        }

        /// <summary>
        /// Calculates network usage percentage
        /// </summary>
        private double CalculateNetworkUsage(double bytesReceivedPerSec, double bytesSentPerSec)
        {
            try
            {
                // Simplified network usage calculation
                var totalBytesPerSec = bytesReceivedPerSec + bytesSentPerSec;
                var maxBytesPerSec = 100 * 1024 * 1024; // 100MB/s as maximum

                return Math.Min(100.0, Math.Max(0.0, totalBytesPerSec / maxBytesPerSec * 100));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating network usage");
                return 0;
            }
        }

        /// <summary>
        /// Calculates activity confidence from metrics
        /// </summary>
        private double CalculateActivityConfidence(ProcessPerformanceMetrics metrics)
        {
            try
            {
                var confidence = 0.0;

                // CPU usage contributes to activity
                confidence += Math.Min(1.0, metrics.CpuUsage / 100.0) * 0.3;

                // Memory usage contributes to activity
                confidence += Math.Min(1.0, metrics.MemoryUsage / 100.0) * 0.2;

                // Disk usage contributes to activity
                confidence += Math.Min(1.0, metrics.DiskUsage / 100.0) * 0.2;

                // High handle/thread count indicates activity
                var handleScore = Math.Min(1.0, metrics.HandleCount / 1000.0) * 0.1;
                var threadScore = Math.Min(1.0, metrics.ThreadCount / 100.0) * 0.1;
                confidence += handleScore + threadScore;

                // GPU usage contributes to activity
                confidence += Math.Min(1.0, metrics.GpuUsage / 100.0) * 0.1;

                return Math.Min(1.0, Math.Max(0.0, confidence));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating activity confidence");
                return 0.5;
            }
        }

        /// <summary>
        /// Calculates process activity level
        /// </summary>
        private double CalculateProcessActivityLevel(ProcessPerformanceMetrics metrics)
        {
            try
            {
                var activityLevel = 0.0;

                // Weighted calculation of different metrics
                activityLevel += metrics.CpuUsage * 0.4;        // 40% weight
                activityLevel += metrics.MemoryUsage * 0.3;   // 30% weight
                activityLevel += metrics.DiskUsage * 0.2;     // 20% weight
                activityLevel += metrics.GpuUsage * 0.1;     // 10% weight

                return Math.Min(100.0, Math.Max(0.0, activityLevel));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating process activity level");
                return 0;
            }
        }

        /// <summary>
        /// Updates process-specific performance counters
        /// </summary>
        private async Task UpdateProcessCountersAsync(int processId, ProcessPerformanceMetrics metrics)
        {
            try
            {
                // Get or create process counters
                var processCounters = _processCounters.GetOrAdd(processId, id => new Dictionary<string, PerformanceCounter>());

                // This is a simplified implementation
                // In a real-world scenario, you might create process-specific counters
                // For now, we'll just track the metrics in the history

                _logger.LogDebug("Updated process counters for process {ProcessId}", processId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating process counters for process {ProcessId}", processId);
            }
        }

        /// <summary>
        /// Checks system performance thresholds
        /// </summary>
        private void CheckSystemThresholds(SystemPerformanceMetrics metrics)
        {
            try
            {
                // Check CPU threshold
                if (metrics.CpuUsage > _config.CpuUsageThreshold)
                {
                    RaiseThresholdExceededEvent("System", "CPU", metrics.CpuUsage, _config.CpuUsageThreshold, metrics);
                }

                // Check memory threshold
                if (metrics.MemoryUsage > _config.MemoryUsageThreshold)
                {
                    RaiseThresholdExceededEvent("System", "Memory", metrics.MemoryUsage, _config.MemoryUsageThreshold, metrics);
                }

                // Check disk threshold
                if (metrics.DiskUsage > _config.DiskUsageThreshold)
                {
                    RaiseThresholdExceededEvent("System", "Disk", metrics.DiskUsage, _config.DiskUsageThreshold, metrics);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system thresholds");
            }
        }

        /// <summary>
        /// Checks process performance thresholds
        /// </summary>
        private void CheckProcessThresholds(int processId, ProcessPerformanceMetrics metrics)
        {
            try
            {
                // Check process CPU threshold
                if (metrics.CpuUsage > _config.ProcessCpuUsageThreshold)
                {
                    RaiseThresholdExceededEvent(processId.ToString(), "ProcessCPU", metrics.CpuUsage, _config.ProcessCpuUsageThreshold, metrics);
                }

                // Check process memory threshold
                if (metrics.MemoryUsage > _config.ProcessMemoryUsageThreshold)
                {
                    RaiseThresholdExceededEvent(processId.ToString(), "ProcessMemory", metrics.MemoryUsage, _config.ProcessMemoryUsageThreshold, metrics);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking process thresholds for process {ProcessId}", processId);
            }
        }

        /// <summary>
        /// Raises threshold exceeded event
        /// </summary>
        private void RaiseThresholdExceededEvent(string source, string metricType, double currentValue, double threshold, SystemPerformanceMetrics metrics)
        {
            try
            {
                PerformanceThresholdExceeded?.Invoke(this, new PerformanceThresholdEventArgs
                {
                    Source = source,
                    MetricType = metricType,
                    CurrentValue = (float)currentValue,
                    Threshold = threshold,
                    Timestamp = DateTime.UtcNow,
                    Severity = currentValue > threshold * 1.5 ? ThresholdSeverity.Critical : ThresholdSeverity.Warning,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["CpuUsage"] = metrics.CpuUsage,
                        ["MemoryUsage"] = metrics.MemoryUsage,
                        ["DiskUsage"] = metrics.DiskUsage,
                        ["NetworkUsage"] = metrics.NetworkUsage
                    }
                });

                lock (_lock)
                {
                    Statistics.ThresholdExceededEvents++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising threshold exceeded event for {Source}:{MetricType}", source, metricType);
            }
        }

        /// <summary>
        /// Raises the ThresholdExceeded event with process-specific metrics
        /// </summary>
        private void RaiseThresholdExceededEvent(string source, string metricType, double currentValue, double threshold, ProcessPerformanceMetrics metrics)
        {
            try
            {
                PerformanceThresholdExceeded?.Invoke(this, new PerformanceThresholdEventArgs
                {
                    Source = source,
                    MetricType = metricType,
                    CurrentValue = (float)currentValue,
                    Threshold = threshold,
                    Timestamp = DateTime.UtcNow,
                    Severity = currentValue > threshold * 1.5 ? ThresholdSeverity.Critical : ThresholdSeverity.Warning,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["CpuUsage"] = metrics.CpuUsage,
                        ["MemoryUsage"] = metrics.MemoryUsage,
                        ["HandleCount"] = metrics.HandleCount,
                        ["ThreadCount"] = metrics.ThreadCount
                    }
                });

                lock (_lock)
                {
                    Statistics.ThresholdExceededEvents++;
                }

                _logger.LogWarning("Performance threshold exceeded: {Source}.{MetricType} = {CurrentValue:F1}% > {Threshold:F1}%",
                    source, metricType, currentValue, threshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising threshold exceeded event");
            }
        }

        /// <summary>
        /// Cleans up old performance histories
        /// </summary>
        private void CleanupOldPerformanceHistories()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-2);
                var toRemove = new List<int>();

                foreach (var kvp in _processHistories)
                {
                    if (kvp.Value.LastMetricsTime < cutoffTime)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var processId in toRemove)
                {
                    _processHistories.TryRemove(processId, out _);
                }

                if (toRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old performance histories", toRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance history cleanup");
            }
        }

        /// <summary>
        /// Timer callback for cleaning up old performance histories
        /// </summary>
        private void CleanupTimerCallback(object state)
        {
            try
            {
                CleanupOldPerformanceHistories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup timer callback");
            }
        }

        #endregion

        #region IDisposable Implementation

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
                    _logger.LogInformation("Disposing PerformanceMonitor");

                    try
                    {
                        StopAsync().Wait(TimeSpan.FromSeconds(5));
                        _cleanupTimer?.Dispose();
                        _cancellationTokenSource?.Dispose();

                        // PerformanceCounter doesn't implement IDisposable in .NET Framework 4.8
                        // No need to dispose counters
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during disposal");
                    }
                }

                _isDisposed = true;
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Configuration for performance monitoring
    /// </summary>
    public class PerformanceMonitorConfig
    {
        /// <summary>
        /// Gets or sets whether performance monitoring is enabled
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether CPU monitoring is enabled
        /// </summary>
        public bool EnableCpuMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether memory monitoring is enabled
        /// </summary>
        public bool EnableMemoryMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether disk monitoring is enabled
        /// </summary>
        public bool EnableDiskMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether network monitoring is enabled
        /// </summary>
        public bool EnableNetworkMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets the monitoring interval in seconds
        /// </summary>
        public int MonitoringIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Gets or sets the CPU usage threshold (percentage)
        /// </summary>
        public double CpuUsageThreshold { get; set; } = 80.0;

        /// <summary>
        /// Gets or sets the memory usage threshold (percentage)
        /// </summary>
        public double MemoryUsageThreshold { get; set; } = 85.0;

        /// <summary>
        /// Gets or sets the disk usage threshold (percentage)
        /// </summary>
        public double DiskUsageThreshold { get; set; } = 90.0;

        /// <summary>
        /// Gets or sets the network usage threshold (percentage)
        /// </summary>
        public double NetworkUsageThreshold { get; set; } = 90.0;

        /// <summary>
        /// Gets or sets the process CPU usage threshold (percentage)
        /// </summary>
        public double ProcessCpuUsageThreshold { get; set; } = 95.0;

        /// <summary>
        /// Gets or sets the process memory usage threshold (percentage)
        /// </summary>
        public double ProcessMemoryUsageThreshold { get; set; } = 95.0;

        /// <summary>
        /// Gets or sets the activity confidence threshold (0.0 to 1.0)
        /// </summary>
        public double ActivityConfidenceThreshold { get; set; } = 0.3;

        /// <summary>
        /// Initializes a new instance of the PerformanceMonitorConfig class
        /// </summary>
        public PerformanceMonitorConfig()
        {
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"PerformanceMonitor[Enabled={EnablePerformanceMonitoring}, CPU={EnableCpuMonitoring}, " +
                   $"Memory={EnableMemoryMonitoring}, Disk={EnableDiskMonitoring}, Network={EnableNetworkMonitoring}]";
        }
    }

    /// <summary>
    /// Statistics for the performance monitor
    /// </summary>
    public class PerformanceMonitorStatistics
    {
        public long TotalMetricsCollected { get; set; }
        public long CpuMetricsCollected { get; set; }
        public long MemoryMetricsCollected { get; set; }
        public long DiskMetricsCollected { get; set; }
        public long NetworkMetricsCollected { get; set; }
        public long GpuMetricsCollected { get; set; }
        public long ThresholdExceededEvents { get; set; }
        public long SuccessfulCollections { get; set; }
        public long FailedCollections { get; set; }
        public long ErrorCount { get; set; }
        public double AverageCollectionTimeMs { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? LastCollectionTime { get; set; }

        public PerformanceMonitorStatistics()
        {
            StartTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            TotalMetricsCollected = 0;
            CpuMetricsCollected = 0;
            MemoryMetricsCollected = 0;
            DiskMetricsCollected = 0;
            NetworkMetricsCollected = 0;
            GpuMetricsCollected = 0;
            ThresholdExceededEvents = 0;
            SuccessfulCollections = 0;
            FailedCollections = 0;
            ErrorCount = 0;
            AverageCollectionTimeMs = 0;
            StartTime = DateTime.UtcNow;
            LastCollectionTime = null;
        }
    }

    /// <summary>
    /// Represents performance metrics for a process
    /// </summary>
    public class ProcessPerformanceMetrics
    {
        public int ProcessId { get; set; }
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
        public double GpuUsage { get; set; }
        public long WorkingSetSize { get; set; }
        public long PrivateMemorySize { get; set; }
        public int HandleCount { get; set; }
        public int ThreadCount { get; set; }

        public ProcessPerformanceMetrics()
        {
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents system performance metrics
    /// </summary>
    public class SystemPerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
        public double DiskReadBytesPerSec { get; set; }
        public double DiskWriteBytesPerSec { get; set; }
        public double NetworkBytesReceivedPerSec { get; set; }
        public double NetworkBytesSentPerSec { get; set; }

        public SystemPerformanceMetrics()
        {
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Maintains performance history for a specific process
    /// </summary>
    public class ProcessPerformanceHistory
    {
        private readonly object _lock = new object();
        private readonly Queue<ProcessPerformanceMetrics> _recentMetrics = new Queue<ProcessPerformanceMetrics>(100);
        private readonly int _processId;

        public int ProcessId => _processId;
        public DateTime LastMetricsTime { get; private set; }
        public int TotalMetrics { get; private set; }

        public ProcessPerformanceHistory(int processId)
        {
            _processId = processId;
            LastMetricsTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records performance metrics
        /// </summary>
        public void RecordMetrics(ProcessPerformanceMetrics metrics)
        {
            lock (_lock)
            {
                _recentMetrics.Enqueue(metrics);
                if (_recentMetrics.Count > 100)
                    _recentMetrics.Dequeue();

                LastMetricsTime = metrics.Timestamp;
                TotalMetrics++;
            }
        }

        /// <summary>
        /// Gets recent metrics within the specified time window
        /// </summary>
        public List<ProcessPerformanceMetrics> GetRecentMetrics(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentMetrics.Where(m => m.Timestamp > cutoffTime).ToList();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
    }

    /// <summary>
    /// Defines threshold severity levels
    /// </summary>
    public enum ThresholdSeverity
    {
        /// <summary>
        /// Warning level threshold exceeded
        /// </summary>
        Warning,

        /// <summary>
        /// Critical level threshold exceeded
        /// </summary>
        Critical
    }

    #endregion
}