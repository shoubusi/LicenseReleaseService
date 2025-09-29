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
    /// Windows API-based process ping service for active process responsiveness checking
    /// </summary>
    public class ProcessPingService : IDisposable
    {
        private readonly ILogger<ProcessPingService> _logger;
        private readonly PingBasedDetectionConfig _config;
        private readonly object _lock = new object();
        private readonly Dictionary<int, ProcessPingHistory> _pingHistories = new Dictionary<int, ProcessPingHistory>();
        private readonly SemaphoreSlim _pingSemaphore;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;

        #region Windows API Imports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags,
            System.Text.StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS pmc, uint cb);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public uint PeakWorkingSetSize;
            public uint WorkingSetSize;
            public uint QuotaPeakPagedPoolUsage;
            public uint QuotaPagedPoolUsage;
            public uint QuotaPeakNonPagedPoolUsage;
            public uint QuotaNonPagedPoolUsage;
            public uint PagefileUsage;
            public uint PeakPagefileUsage;
        }

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_DUP_HANDLE = 0x0040;
        private const uint STILL_ACTIVE = 0x00000103;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a process responds to ping
        /// </summary>
        public event EventHandler<ProcessPingResult> ProcessResponded;

        /// <summary>
        /// Event raised when a process is unresponsive to ping
        /// </summary>
        public event EventHandler<ProcessPingResult> ProcessUnresponsive;

        /// <summary>
        /// Gets a value indicating whether the service is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the service statistics
        /// </summary>
        public ProcessPingStatistics Statistics { get; private set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the ProcessPingService class
        /// </summary>
        public ProcessPingService(ILogger<ProcessPingService> logger, PingBasedDetectionConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _pingSemaphore = new SemaphoreSlim(config.MaxConcurrentPings);
            Statistics = new ProcessPingStatistics();

            _logger.LogInformation("ProcessPingService initialized with configuration: {Config}", config);
        }

        /// <summary>
        /// Initializes the service with configuration
        /// </summary>
        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing ProcessPingService");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("ProcessPingService is already running");
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Clear any existing ping histories
                lock (_lock)
                {
                    _pingHistories.Clear();
                    Statistics.Reset();
                }

                _logger.LogInformation("ProcessPingService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ProcessPingService");
                throw;
            }
        }

        /// <summary>
        /// Starts the process ping service
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting ProcessPingService");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("ProcessPingService is already running");
                        return;
                    }

                    IsRunning = true;
                }

                _logger.LogInformation("ProcessPingService started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting ProcessPingService");
                throw;
            }
        }

        /// <summary>
        /// Stops the process ping service
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping ProcessPingService");

                lock (_lock)
                {
                    if (!IsRunning)
                    {
                        _logger.LogWarning("ProcessPingService is not running");
                        return;
                    }

                    IsRunning = false;
                }

                // Cancel any pending operations
                _cancellationTokenSource?.Cancel();

                _logger.LogInformation("ProcessPingService stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping ProcessPingService");
                throw;
            }
        }

        /// <summary>
        /// Pauses the process ping service
        /// </summary>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing ProcessPingService");

                lock (_lock)
                {
                    if (!IsRunning)
                    {
                        _logger.LogWarning("ProcessPingService is not running");
                        return;
                    }

                    IsRunning = false;
                }

                _logger.LogInformation("ProcessPingService paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing ProcessPingService");
                throw;
            }
        }

        /// <summary>
        /// Resumes the process ping service
        /// </summary>
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming ProcessPingService");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("ProcessPingService is already running");
                        return;
                    }

                    IsRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                _logger.LogInformation("ProcessPingService resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming ProcessPingService");
                throw;
            }
        }

        /// <summary>
        /// Pings a specific process to check responsiveness
        /// </summary>
        public async Task<ProcessPingResult> PingProcessAsync(int processId, CancellationToken cancellationToken = default)
        {
            await _pingSemaphore.WaitAsync(cancellationToken);
            try
            {
                var startTime = DateTime.UtcNow;
                var pingResult = new ProcessPingResult
                {
                    ProcessId = processId,
                    TotalPingAttempts = _config.MaxPingAttempts,
                    SuccessfulPingCount = 0,
                    FailedPingCount = 0
                };

                _logger.LogDebug("Starting ping for process {ProcessId} with {Attempts} attempts",
                    processId, _config.MaxPingAttempts);

                // Get or create ping history
                var history = GetOrCreatePingHistory(processId);

                // Perform multiple ping attempts
                var responseTimes = new List<int>();
                for (var attempt = 1; attempt <= _config.MaxPingAttempts; attempt++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var attemptResult = await PerformSinglePingAttemptAsync(processId, attempt, cancellationToken);

                    if (attemptResult.IsResponsive)
                    {
                        responseTimes.Add(attemptResult.ResponseTimeMs);
                        pingResult.SuccessfulPingCount++;
                        history.RecordSuccessfulPing(DateTime.UtcNow, attemptResult.ResponseTimeMs);
                    }
                    else
                    {
                        pingResult.FailedPingCount++;
                        history.RecordFailedPing(DateTime.UtcNow, attemptResult.Error);
                    }

                    // Delay between attempts (except for the last one)
                    if (attempt < _config.MaxPingAttempts)
                    {
                        await Task.Delay(_config.PingRetryDelayMs, cancellationToken);
                    }
                }

                // Calculate overall result
                pingResult.IsResponsive = pingResult.SuccessfulPingCount > 0;
                pingResult.AverageResponseTimeMs = responseTimes.Count > 0 ?
                    responseTimes.Average() : 0;
                pingResult.ResponseTimeMs = responseTimes.Count > 0 ?
                    responseTimes.Last() : -1;
                pingResult.LastSuccessfulPing = history.LastSuccessfulPing;

                // Update statistics
                UpdateStatistics(pingResult);

                // Raise events
                if (pingResult.IsResponsive)
                {
                    ProcessResponded?.Invoke(this, pingResult);
                }
                else
                {
                    ProcessUnresponsive?.Invoke(this, pingResult);
                }

                var totalTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Ping completed for process {ProcessId}: Responsive={Responsive}, " +
                    "Success={Success}/{Attempts}, AvgTime={AvgTime:F1}ms, TotalTime={TotalTime}ms",
                    processId, pingResult.IsResponsive, pingResult.SuccessfulPingCount,
                    pingResult.TotalPingAttempts, pingResult.AverageResponseTimeMs, totalTime.TotalMilliseconds);

                return pingResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinging process {ProcessId}", processId);
                return new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = false,
                    TotalPingAttempts = _config.MaxPingAttempts,
                    SuccessfulPingCount = 0,
                    FailedPingCount = _config.MaxPingAttempts,
                    Error = ex.Message
                };
            }
            finally
            {
                _pingSemaphore.Release();
            }
        }

        /// <summary>
        /// Performs a single ping attempt on a process
        /// </summary>
        private async Task<ProcessPingResult> PerformSinglePingAttemptAsync(int processId, int attemptNumber, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var result = new ProcessPingResult { ProcessId = processId };

            try
            {
                // Method 1: Basic process accessibility check
                using var process = GetProcessHandle(processId);
                if (process == IntPtr.Zero)
                {
                    result.Error = "Process not found or access denied";
                    return result;
                }

                // Method 2: Check if process is still running
                if (!IsProcessRunning(process))
                {
                    result.Error = "Process has exited";
                    return result;
                }

                // Method 3: Query process memory information
                if (!GetProcessMemoryInfo(process))
                {
                    result.Error = "Unable to query process memory info";
                    return result;
                }

                // Method 4: Check for associated windows (for GUI applications)
                var hasWindows = await FindProcessWindowsAsync(processId, cancellationToken);

                // Method 5: Query full process image name
                var imagePath = GetProcessImagePath(process);

                // If we reach here, the process is responsive
                var responseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                result.IsResponsive = true;
                result.ResponseTimeMs = responseTime;
                result.Error = null;

                _logger.LogDebug("Ping attempt {Attempt} for process {ProcessId} successful: {Time}ms, Windows={HasWindows}, Path={Path}",
                    attemptNumber, processId, responseTime, hasWindows, imagePath ?? "unknown");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Ping attempt failed: {ex.Message}";
                _logger.LogDebug(ex, "Ping attempt {Attempt} for process {ProcessId} failed", attemptNumber, processId);
                return result;
            }
        }

        /// <summary>
        /// Gets a handle to the specified process
        /// </summary>
        private IntPtr GetProcessHandle(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return process.Handle;
            }
            catch (ArgumentException)
            {
                // Process not found
                return IntPtr.Zero;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Access denied or other error
                _logger.LogDebug(ex, "Access denied to process {ProcessId}", processId);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Checks if a process is still running
        /// </summary>
        private bool IsProcessRunning(IntPtr processHandle)
        {
            try
            {
                return GetExitCodeProcess(processHandle, out var exitCode) && exitCode == STILL_ACTIVE;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking if process is running");
                return false;
            }
        }

        /// <summary>
        /// Gets process memory information
        /// </summary>
        private bool GetProcessMemoryInfo(IntPtr processHandle)
        {
            try
            {
                var counters = new PROCESS_MEMORY_COUNTERS { cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS)) };
                return GetProcessMemoryInfo(processHandle, out counters, counters.cb);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting process memory info");
                return false;
            }
        }

        /// <summary>
        /// Finds windows associated with the process
        /// </summary>
        private async Task<bool> FindProcessWindowsAsync(int processId, CancellationToken cancellationToken)
        {
            try
            {
                var windowsFound = false;
                var windowCount = 0;

                // Enumerate all windows and check if any belong to our process
                EnumWindows((hWnd, lParam) =>
                {
                    if (GetWindowThreadProcessId(hWnd, out var windowProcessId) != IntPtr.Zero &&
                        windowProcessId == processId)
                    {
                        windowsFound = true;
                        windowCount++;

                        // Get window title for logging
                        var title = GetWindowTitle(hWnd);
                        if (!string.IsNullOrEmpty(title))
                        {
                            _logger.LogDebug("Found window for process {ProcessId}: '{Title}'", processId, title);
                        }
                    }
                    return true; // Continue enumeration
                }, IntPtr.Zero);

                _logger.LogDebug("Found {Count} windows for process {ProcessId}", windowCount, processId);
                return windowsFound;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error finding process windows");
                return false;
            }
        }

        /// <summary>
        /// Gets the title of a window
        /// </summary>
        private string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                var length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return null;

                var builder = new System.Text.StringBuilder(length + 1);
                return GetWindowText(hWnd, builder, builder.Length) > 0 ? builder.ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the length of window text
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        /// <summary>
        /// Gets the full process image path
        /// </summary>
        private string GetProcessImagePath(IntPtr processHandle)
        {
            try
            {
                var bufferSize = 1024;
                var builder = new System.Text.StringBuilder(bufferSize);
                var result = QueryFullProcessImageName(processHandle, 0, builder, ref bufferSize);
                return result ? builder.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting process image path");
                return null;
            }
        }

        /// <summary>
        /// Gets or creates ping history for a process
        /// </summary>
        private ProcessPingHistory GetOrCreatePingHistory(int processId)
        {
            lock (_lock)
            {
                if (!_pingHistories.TryGetValue(processId, out var history))
                {
                    history = new ProcessPingHistory(processId);
                    _pingHistories[processId] = history;
                }
                return history;
            }
        }

        /// <summary>
        /// Updates service statistics
        /// </summary>
        private void UpdateStatistics(ProcessPingResult result)
        {
            lock (_lock)
            {
                Statistics.TotalPings++;

                if (result.IsResponsive)
                {
                    Statistics.SuccessfulPings++;
                    Statistics.TotalResponseTimeMs += result.ResponseTimeMs;
                }
                else
                {
                    Statistics.FailedPings++;
                }

                Statistics.AverageResponseTimeMs = Statistics.SuccessfulPings > 0 ?
                    Statistics.TotalResponseTimeMs / Statistics.SuccessfulPings : 0;

                // Clean up old histories periodically
                if (Statistics.TotalPings % 100 == 0)
                {
                    CleanupOldHistories();
                }
            }
        }

        /// <summary>
        /// Cleans up old ping histories
        /// </summary>
        private void CleanupOldHistories()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(1);
                var toRemove = new List<int>();

                lock (_lock)
                {
                    foreach (var kvp in _pingHistories)
                    {
                        if (kvp.Value.LastPingTime < cutoffTime)
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var processId in toRemove)
                    {
                        _pingHistories.Remove(processId);
                    }
                }

                if (toRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old ping histories", toRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up ping histories");
            }
        }

        /// <summary>
        /// Updates the service configuration
        /// </summary>
        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating ProcessPingService configuration");

                if (configuration?.CustomParameters != null)
                {
                    // Update ping-specific parameters
                    if (configuration.CustomParameters.ContainsKey("PingTimeoutMs"))
                    {
                        _config.PingTimeoutMs = Convert.ToInt32(configuration.CustomParameters["PingTimeoutMs"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("MaxConcurrentPings"))
                    {
                        var newMaxConcurrent = Convert.ToInt32(configuration.CustomParameters["MaxConcurrentPings"]);
                        if (newMaxConcurrent != _config.MaxConcurrentPings)
                        {
                            // Recreate semaphore with new size
                            _pingSemaphore.Dispose();
                            _pingSemaphore = new SemaphoreSlim(newMaxConcurrent);
                            _config.MaxConcurrentPings = newMaxConcurrent;
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("MaxPingAttempts"))
                    {
                        _config.MaxPingAttempts = Convert.ToInt32(configuration.CustomParameters["MaxPingAttempts"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("PingRetryDelayMs"))
                    {
                        _config.PingRetryDelayMs = Convert.ToInt32(configuration.CustomParameters["PingRetryDelayMs"]);
                    }
                }

                _logger.LogInformation("ProcessPingService configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ProcessPingService configuration");
                throw;
            }
        }

        /// <summary>
        /// Gets the health status of the service
        /// </summary>
        public async Task<DetectorHealth> GetHealthAsync()
        {
            try
            {
                var health = new DetectorHealth
                {
                    DetectorName = "ProcessPingService",
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                var isHealthy = true;
                var issues = new List<string>();

                // Check service status
                if (!IsRunning)
                {
                    issues.Add("Service is not running");
                    isHealthy = false;
                }

                // Check semaphore availability
                if (_pingSemaphore.CurrentCount == 0)
                {
                    issues.Add("All ping slots are in use");
                }

                // Check ping history size
                lock (_lock)
                {
                    if (_pingHistories.Count > 1000)
                    {
                        issues.Add($"High ping history count: {_pingHistories.Count}");
                    }
                }

                // Check statistics for abnormal patterns
                if (Statistics.TotalPings > 0)
                {
                    var failureRate = (double)Statistics.FailedPings / Statistics.TotalPings;
                    if (failureRate > 0.8)
                    {
                        issues.Add($"High ping failure rate: {failureRate:P1}");
                    }

                    if (Statistics.AverageResponseTimeMs > 5000)
                    {
                        issues.Add($"High average response time: {Statistics.AverageResponseTimeMs:F1}ms");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["PingHistoryCount"] = _pingHistories.Count;
                health.AdditionalInfo["AvailablePingSlots"] = _pingSemaphore.CurrentCount;
                health.AdditionalInfo["TotalPings"] = Statistics.TotalPings;
                health.AdditionalInfo["SuccessfulPings"] = Statistics.SuccessfulPings;
                health.AdditionalInfo["FailedPings"] = Statistics.FailedPings;
                health.AdditionalInfo["AverageResponseTimeMs"] = Statistics.AverageResponseTimeMs;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ProcessPingService health");
                return new DetectorHealth
                {
                    DetectorName = "ProcessPingService",
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        /// <summary>
        /// Gets service statistics
        /// </summary>
        public async Task<ProcessPingStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                return new ProcessPingStatistics
                {
                    TotalPings = Statistics.TotalPings,
                    SuccessfulPings = Statistics.SuccessfulPings,
                    FailedPings = Statistics.FailedPings,
                    AverageResponseTimeMs = Statistics.AverageResponseTimeMs,
                    TotalResponseTimeMs = Statistics.TotalResponseTimeMs,
                    ErrorCount = Statistics.ErrorCount,
                    StartTime = Statistics.StartTime,
                    LastPingTime = Statistics.LastPingTime
                };
            }
        }

        /// <summary>
        /// Resets service statistics
        /// </summary>
        public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    Statistics.Reset();
                    _pingHistories.Clear();
                }

                _logger.LogInformation("ProcessPingService statistics reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting ProcessPingService statistics");
                throw;
            }
        }

        /// <summary>
        /// Disposes the service
        /// </summary>
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
                    _logger.LogInformation("Disposing ProcessPingService");

                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                    _pingSemaphore?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Maintains ping history for a specific process
    /// </summary>
    public class ProcessPingHistory
    {
        private readonly object _lock = new object();
        private readonly Queue<PingAttempt> _recentAttempts = new Queue<PingAttempt>(50);
        private readonly int _processId;

        public int ProcessId => _processId;
        public DateTime LastPingTime { get; private set; }
        public DateTime? LastSuccessfulPing { get; private set; }
        public int TotalAttempts { get; private set; }
        public int SuccessfulAttempts { get; private set; }
        public int FailedAttempts { get; private set; }
        public double AverageResponseTimeMs { get; private set; }
        public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulAttempts / TotalAttempts : 0.0;

        public ProcessPingHistory(int processId)
        {
            _processId = processId;
            LastPingTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records a successful ping attempt
        /// </summary>
        public void RecordSuccessfulPing(DateTime timestamp, int responseTimeMs)
        {
            lock (_lock)
            {
                var attempt = new PingAttempt
                {
                    Timestamp = timestamp,
                    IsSuccessful = true,
                    ResponseTimeMs = responseTimeMs
                };

                _recentAttempts.Enqueue(attempt);
                if (_recentAttempts.Count > 50)
                    _recentAttempts.Dequeue();

                LastPingTime = timestamp;
                LastSuccessfulPing = timestamp;
                TotalAttempts++;
                SuccessfulAttempts++;

                UpdateAverageResponseTime();
            }
        }

        /// <summary>
        /// Records a failed ping attempt
        /// </summary>
        public void RecordFailedPing(DateTime timestamp, string error)
        {
            lock (_lock)
            {
                var attempt = new PingAttempt
                {
                    Timestamp = timestamp,
                    IsSuccessful = false,
                    Error = error
                };

                _recentAttempts.Enqueue(attempt);
                if (_recentAttempts.Count > 50)
                    _recentAttempts.Dequeue();

                LastPingTime = timestamp;
                TotalAttempts++;
                FailedAttempts++;
            }
        }

        /// <summary>
        /// Updates the average response time
        /// </summary>
        private void UpdateAverageResponseTime()
        {
            var successfulAttempts = _recentAttempts.Where(a => a.IsSuccessful).ToList();
            if (successfulAttempts.Count > 0)
            {
                AverageResponseTimeMs = successfulAttempts.Average(a => a.ResponseTimeMs);
            }
        }

        /// <summary>
        /// Gets recent ping attempts within the specified time window
        /// </summary>
        public List<PingAttempt> GetRecentAttempts(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentAttempts.Where(a => a.Timestamp > cutoffTime).ToList();
            }
        }

        /// <summary>
        /// Gets the success rate within the specified time window
        /// </summary>
        public double GetRecentSuccessRate(TimeSpan timeWindow)
        {
            var recentAttempts = GetRecentAttempts(timeWindow);
            if (recentAttempts.Count == 0)
                return 0.0;

            return (double)recentAttempts.Count(a => a.IsSuccessful) / recentAttempts.Count;
        }
    }

    /// <summary>
    /// Represents a single ping attempt
    /// </summary>
    public class PingAttempt
    {
        public DateTime Timestamp { get; set; }
        public bool IsSuccessful { get; set; }
        public int ResponseTimeMs { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Statistics for the process ping service
    /// </summary>
    public class ProcessPingStatistics
    {
        public long TotalPings { get; set; }
        public long SuccessfulPings { get; set; }
        public long FailedPings { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public long TotalResponseTimeMs { get; set; }
        public long ErrorCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastPingTime { get; set; }

        public ProcessPingStatistics()
        {
            StartTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            TotalPings = 0;
            SuccessfulPings = 0;
            FailedPings = 0;
            AverageResponseTimeMs = 0;
            TotalResponseTimeMs = 0;
            ErrorCount = 0;
            StartTime = DateTime.UtcNow;
            LastPingTime = null;
        }
    }
}