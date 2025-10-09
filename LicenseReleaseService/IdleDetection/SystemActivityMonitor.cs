using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Monitors system-wide user activity using Windows API
    /// </summary>
    public class SystemActivityMonitor : IDisposable
    {
        private readonly ILogger<SystemActivityMonitor> _logger;
        private readonly TimeBasedDetectionConfig _config;
        private readonly object _lock = new object();
        private readonly List<ActivityData> _recentActivities = new List<ActivityData>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private bool _isDisposed;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("kernel32.dll")]
        private static extern bool QueryUnbiasedInterruptTime(out long lpUnbiasedInterruptTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        /// <summary>
        /// Event raised when user activity is detected
        /// </summary>
        public event EventHandler<ActivityData> ActivityDetected;

        /// <summary>
        /// Gets a value indicating whether the monitor is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the current system idle time
        /// </summary>
        public TimeSpan CurrentIdleTime { get; private set; }

        /// <summary>
        /// Gets the last activity timestamp
        /// </summary>
        public DateTime LastActivity { get; private set; }

        /// <summary>
        /// Initializes a new instance of the SystemActivityMonitor class
        /// </summary>
        public SystemActivityMonitor(ILogger<SystemActivityMonitor> logger, TimeBasedDetectionConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LastActivity = DateTime.UtcNow;
            CurrentIdleTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Starts the activity monitoring
        /// </summary>
        public async Task StartAsync()
        {
            if (IsRunning)
            {
                _logger.LogWarning("System activity monitor is already running");
                return;
            }

            lock (_lock)
            {
                if (IsRunning)
                    return;

                _cancellationTokenSource = new CancellationTokenSource();
                IsRunning = true;
            }

            _logger.LogInformation("Starting system activity monitor with configuration: {Config}", _config);

            _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

            // Perform initial detection
            await DetectInitialActivityAsync();
        }

        /// <summary>
        /// Stops the activity monitoring
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("System activity monitor is not running");
                return;
            }

            lock (_lock)
            {
                if (!IsRunning)
                    return;

                IsRunning = false;
                _cancellationTokenSource?.Cancel();
            }

            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping system activity monitor");
                }
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _monitoringTask = null;

            _logger.LogInformation("System activity monitor stopped");
        }

        /// <summary>
        /// Gets the recent activity data
        /// </summary>
        /// <param name="timeWindow">The time window to look back</param>
        /// <returns>List of recent activities</returns>
        public List<ActivityData> GetRecentActivities(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentActivities
                    .Where(a => a.Timestamp > cutoffTime)
                    .OrderByDescending(a => a.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the idle time since the last user input
        /// </summary>
        /// <returns>Idle time duration</returns>
        public TimeSpan GetSystemIdleTime()
        {
            try
            {
                var lastInputInfo = new LASTINPUTINFO
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
                };

                if (GetLastInputInfo(ref lastInputInfo))
                {
                    var lastInputTime = lastInputInfo.dwTime;
                    var systemUptime = Environment.TickCount;
                    var idleMilliseconds = systemUptime - lastInputTime;

                    return TimeSpan.FromMilliseconds(idleMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system idle time");
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Gets the title of the currently active window
        /// </summary>
        /// <returns>Active window title</returns>
        public string GetActiveWindowTitle()
        {
            try
            {
                var hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                    return string.Empty;

                const int nChars = 256;
                var buffer = new System.Text.StringBuilder(nChars);
                if (GetWindowText(hWnd, buffer, nChars) > 0)
                {
                    return buffer.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active window title");
            }

            return string.Empty;
        }

        /// <summary>
        /// Checks if SolidWorks is currently active
        /// </summary>
        /// <returns>True if SolidWorks is the active application</returns>
        public bool IsSolidWorksActive()
        {
            try
            {
                var windowTitle = GetActiveWindowTitle();
                return !string.IsNullOrEmpty(windowTitle) &&
                       (windowTitle.Contains("SolidWorks", StringComparison.OrdinalIgnoreCase) ||
                        windowTitle.Contains("SOLIDWORKS", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if SolidWorks is active");
            }

            return false;
        }

        /// <summary>
        /// Gets the current CPU usage
        /// </summary>
        /// <returns>CPU usage percentage (0-100)</returns>
        public double GetCpuUsage()
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0, need to wait
                Thread.Sleep(100);
                return cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU usage");
            }

            return 0.0;
        }

        /// <summary>
        /// Gets the current memory usage
        /// </summary>
        /// <returns>Memory usage percentage (0-100)</returns>
        public double GetMemoryUsage()
        {
            try
            {
                var memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                return memCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory usage");
            }

            return 0.0;
        }

        /// <summary>
        /// Detects activity from running processes
        /// </summary>
        /// <returns>List of detected activities</returns>
        private List<ActivityData> DetectProcessActivity()
        {
            var activities = new List<ActivityData>();

            try
            {
                var processes = System.Diagnostics.Process.GetProcesses();
                var solidWorksProcesses = processes
                    .Where(p => p.ProcessName.Contains("SLDWORKS", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (solidWorksProcesses.Any())
                {
                    foreach (var process in solidWorksProcesses)
                    {
                        try
                        {
                            if (process.Responding && !process.HasExited)
                            {
                                var activity = new ActivityData
                                {
                                    ActivityType = ActivityType.SolidWorks,
                                    Confidence = 0.8,
                                    Timestamp = DateTime.UtcNow
                                };
                                activity.Metadata["ProcessId"] = process.Id;
                                activity.Metadata["ProcessName"] = process.ProcessName;
                                activity.Metadata["StartTime"] = process.StartTime;
                                activity.Metadata["Responding"] = process.Responding;

                                activities.Add(activity);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error checking SolidWorks process {ProcessId}", process.Id);
                        }
                    }
                }

                // Check for any high CPU usage processes
                var highCpuProcesses = processes
                    .Where(p => p.TotalProcessorTime.TotalMilliseconds > 0)
                    .OrderByDescending(p => p.TotalProcessorTime.TotalMilliseconds)
                    .Take(5)
                    .ToList();

                foreach (var process in highCpuProcesses)
                {
                    try
                    {
                        var cpuTime = process.TotalProcessorTime.TotalMilliseconds;
                        if (cpuTime > 1000) // More than 1 second of CPU time
                        {
                            var activity = new ActivityData
                            {
                                ActivityType = ActivityType.System,
                                Confidence = Math.Min(1.0, cpuTime / 10000), // Scale to 0-1
                                Timestamp = DateTime.UtcNow
                            };
                            activity.Metadata["ProcessId"] = process.Id;
                            activity.Metadata["ProcessName"] = process.ProcessName;
                            activity.Metadata["CpuTimeMs"] = cpuTime;

                            activities.Add(activity);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking process {ProcessId} CPU activity", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting process activity");
            }

            return activities;
        }

        /// <summary>
        /// Detects keyboard and mouse activity
        /// </summary>
        /// <returns>List of detected activities</returns>
        private List<ActivityData> DetectInputActivity()
        {
            var activities = new List<ActivityData>();

            try
            {
                var idleTime = GetSystemIdleTime();
                var wasActive = idleTime < _config.MonitoringSampleRate;

                if (wasActive)
                {
                    if (_config.EnableKeyboardMonitoring)
                    {
                        var keyboardActivity = new ActivityData
                        {
                            ActivityType = ActivityType.Keyboard,
                            Confidence = 0.9,
                            Timestamp = DateTime.UtcNow
                        };
                        keyboardActivity.Metadata["IdleTimeMs"] = idleTime.TotalMilliseconds;
                        activities.Add(keyboardActivity);
                    }

                    if (_config.EnableMouseMonitoring)
                    {
                        var mouseActivity = new ActivityData
                        {
                            ActivityType = ActivityType.Mouse,
                            Confidence = 0.9,
                            Timestamp = DateTime.UtcNow
                        };
                        mouseActivity.Metadata["IdleTimeMs"] = idleTime.TotalMilliseconds;
                        activities.Add(mouseActivity);
                    }
                }

                // Update current idle time
                CurrentIdleTime = idleTime;

                // Update last activity time if activity was detected
                if (wasActive)
                {
                    LastActivity = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting input activity");
            }

            return activities;
        }

        /// <summary>
        /// Detects window focus activity
        /// </summary>
        /// <returns>List of detected activities</returns>
        private List<ActivityData> DetectWindowActivity()
        {
            var activities = new List<ActivityData>();

            try
            {
                var windowTitle = GetActiveWindowTitle();
                if (!string.IsNullOrEmpty(windowTitle))
                {
                    var activity = new ActivityData
                    {
                        ActivityType = ActivityType.WindowFocus,
                        Confidence = 0.7,
                        Timestamp = DateTime.UtcNow
                    };
                    activity.Metadata["WindowTitle"] = windowTitle;
                    activity.Metadata["IsSolidWorks"] = IsSolidWorksActive();

                    activities.Add(activity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting window activity");
            }

            return activities;
        }

        /// <summary>
        /// Detects system resource activity
        /// </summary>
        /// <returns>List of detected activities</returns>
        private List<ActivityData> DetectSystemActivity()
        {
            var activities = new List<ActivityData>();

            try
            {
                if (_config.EnableSystemMonitoring)
                {
                    var cpuUsage = GetCpuUsage();
                    var memoryUsage = GetMemoryUsage();

                    // High CPU usage indicates activity
                    if (cpuUsage > 10) // More than 10% CPU usage
                    {
                        var cpuActivity = new ActivityData
                        {
                            ActivityType = ActivityType.System,
                            Confidence = Math.Min(1.0, cpuUsage / 100),
                            Timestamp = DateTime.UtcNow
                        };
                        cpuActivity.Metadata["CpuUsage"] = cpuUsage;
                        cpuActivity.Metadata["Metric"] = "CPU";
                        activities.Add(cpuActivity);
                    }

                    // High memory usage indicates activity
                    if (memoryUsage > 50) // More than 50% memory usage
                    {
                        var memoryActivity = new ActivityData
                        {
                            ActivityType = ActivityType.System,
                            Confidence = Math.Min(1.0, memoryUsage / 100),
                            Timestamp = DateTime.UtcNow
                        };
                        memoryActivity.Metadata["MemoryUsage"] = memoryUsage;
                        memoryActivity.Metadata["Metric"] = "Memory";
                        activities.Add(memoryActivity);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting system activity");
            }

            return activities;
        }

        /// <summary>
        /// Performs initial activity detection
        /// </summary>
        private async Task DetectInitialActivityAsync()
        {
            try
            {
                var allActivities = new List<ActivityData>();

                if (_config.EnableKeyboardMonitoring || _config.EnableMouseMonitoring)
                {
                    allActivities.AddRange(DetectInputActivity());
                }

                if (_config.EnableSystemMonitoring)
                {
                    allActivities.AddRange(DetectSystemActivity());
                }

                if (_config.EnableSolidWorksMonitoring)
                {
                    allActivities.AddRange(DetectProcessActivity());
                }

                allActivities.AddRange(DetectWindowActivity());

                // Store activities and raise events
                foreach (var activity in allActivities)
                {
                    StoreActivity(activity);
                    OnActivityDetected(activity);
                }

                _logger.LogDebug("Initial activity detection completed: {Count} activities detected", allActivities.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial activity detection");
            }
        }

        /// <summary>
        /// Main monitoring loop
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting activity monitoring loop");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for the sample interval
                    await Task.Delay(_config.MonitoringSampleRate, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var allActivities = new List<ActivityData>();

                    // Detect different types of activity based on configuration
                    if (_config.EnableKeyboardMonitoring || _config.EnableMouseMonitoring)
                    {
                        allActivities.AddRange(DetectInputActivity());
                    }

                    if (_config.EnableSystemMonitoring)
                    {
                        allActivities.AddRange(DetectSystemActivity());
                    }

                    if (_config.EnableSolidWorksMonitoring)
                    {
                        allActivities.AddRange(DetectProcessActivity());
                    }

                    allActivities.AddRange(DetectWindowActivity());

                    // Store activities and raise events
                    foreach (var activity in allActivities)
                    {
                        StoreActivity(activity);
                        OnActivityDetected(activity);
                    }

                    // Clean up old activities
                    CleanupOldActivities();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in activity monitoring loop");
                }
            }

            _logger.LogDebug("Activity monitoring loop stopped");
        }

        /// <summary>
        /// Stores activity data in the recent activities list
        /// </summary>
        private void StoreActivity(ActivityData activity)
        {
            lock (_lock)
            {
                _recentActivities.Add(activity);

                // Keep only recent activities (last hour)
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                _recentActivities.RemoveAll(a => a.Timestamp < cutoffTime);
            }
        }

        /// <summary>
        /// Cleans up old activities from the list
        /// </summary>
        private void CleanupOldActivities()
        {
            lock (_lock)
            {
                // Keep only activities from the last 30 minutes for performance
                var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
                _recentActivities.RemoveAll(a => a.Timestamp < cutoffTime);
            }
        }

        /// <summary>
        /// Raises the ActivityDetected event
        /// </summary>
        private void OnActivityDetected(ActivityData activity)
        {
            try
            {
                ActivityDetected?.Invoke(this, activity);
                _logger.LogDebug("Activity detected: {ActivityType}, Confidence: {Confidence:F2}",
                    activity.ActivityType, activity.Confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising ActivityDetected event");
            }
        }

        /// <summary>
        /// Disposes the monitor resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the monitor resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop monitoring
                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                }

                _isDisposed = true;
            }
        }
    }
}