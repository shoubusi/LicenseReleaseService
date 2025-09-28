using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Management;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Provides comprehensive system-wide activity tracking including user input,
    /// window focus, process monitoring, and system resource utilization
    /// </summary>
    public class SystemActivityTracker : IDisposable
    {
        private readonly ILogger<SystemActivityTracker> _logger;
        private readonly SystemActivityTrackerConfig _config;
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<int, ProcessActivityRecord> _processRecords = new ConcurrentDictionary<int, ProcessActivityRecord>();
        private readonly List<SystemActivityEvent> _activityHistory = new List<SystemActivityEvent>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _trackingTask;
        private ManagementEventWatcher _processWatcher;
        private bool _isDisposed;

        // Windows API imports for enhanced activity tracking
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool QueryUnbiasedInterruptTime(out long lpUnbiasedInterruptTime);

        [DllImport("powrprof.dll")]
        private static extern uint CallNtPowerInformation(int InformationLevel, IntPtr lpInputBuffer,
            int nInputBufferSize, IntPtr lpOutputBuffer, int nOutputBufferSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // Power information constants
        private const int SystemPowerInformation = 12;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_INFORMATION
        {
            public uint MaxIdlenessAllowed;
            public uint Idleness;
            public uint TimeRemaining;
            public byte CoolingMode;
        }

        /// <summary>
        /// Event raised when system activity is detected
        /// </summary>
        public event EventHandler<SystemActivityEvent> SystemActivityDetected;

        /// <summary>
        /// Event raised when process activity is detected
        /// </summary>
        public event EventHandler<ProcessActivityRecord> ProcessActivityDetected;

        /// <summary>
        /// Gets a value indicating whether the tracker is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the current system idle time
        /// </summary>
        public TimeSpan CurrentSystemIdleTime { get; private set; }

        /// <summary>
        /// Gets the last system activity timestamp
        /// </summary>
        public DateTime LastSystemActivity { get; private set; }

        /// <summary>
        /// Gets tracking statistics
        /// </summary>
        public SystemActivityTrackerStats Statistics { get; } = new SystemActivityTrackerStats();

        /// <summary>
        /// Initializes a new instance of the SystemActivityTracker class
        /// </summary>
        public SystemActivityTracker(ILogger<SystemActivityTracker> logger, SystemActivityTrackerConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LastSystemActivity = DateTime.UtcNow;
            CurrentSystemIdleTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Starts the system activity tracking
        /// </summary>
        public async Task StartAsync()
        {
            if (IsRunning)
            {
                _logger.LogWarning("System activity tracker is already running");
                return;
            }

            lock (_lock)
            {
                if (IsRunning)
                    return;

                _cancellationTokenSource = new CancellationTokenSource();
                IsRunning = true;
            }

            _logger.LogInformation("Starting system activity tracker with configuration: {Config}", _config);

            // Initialize process monitoring
            InitializeProcessMonitoring();

            // Start the main tracking task
            _trackingTask = Task.Run(() => TrackingLoop(_cancellationTokenSource.Token));

            // Perform initial detection
            await PerformInitialTrackingAsync();

            _logger.LogInformation("System activity tracker started successfully");
        }

        /// <summary>
        /// Stops the system activity tracking
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("System activity tracker is not running");
                return;
            }

            lock (_lock)
            {
                if (!IsRunning)
                    return;

                IsRunning = false;
                _cancellationTokenSource?.Cancel();
            }

            // Stop process monitoring
            StopProcessMonitoring();

            if (_trackingTask != null)
            {
                try
                {
                    await _trackingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping system activity tracker");
                }
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _trackingTask = null;

            _logger.LogInformation("System activity tracker stopped");
        }

        /// <summary>
        /// Gets recent system activity events
        /// </summary>
        /// <param name="timeWindow">The time window to look back</param>
        /// <returns>List of recent activity events</returns>
        public List<SystemActivityEvent> GetRecentSystemActivity(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _activityHistory
                    .Where(a => a.Timestamp > cutoffTime)
                    .OrderByDescending(a => a.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets process activity records for a specific process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Process activity record</returns>
        public ProcessActivityRecord GetProcessActivityRecord(int processId)
        {
            return _processRecords.TryGetValue(processId, out var record) ? record : null;
        }

        /// <summary>
        /// Gets all process activity records
        /// </summary>
        /// <returns>All process activity records</returns>
        public List<ProcessActivityRecord> GetAllProcessActivityRecords()
        {
            return _processRecords.Values.ToList();
        }

        /// <summary>
        /// Gets the current system idle time with high precision
        /// </summary>
        /// <returns>System idle time</returns>
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

                // Fallback to unbiased interrupt time
                if (QueryUnbiasedInterruptTime(out long interruptTime))
                {
                    var interruptTimeMs = interruptTime / 10000; // Convert to milliseconds
                    // This would need additional logic to track last activity time
                    return TimeSpan.Zero;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system idle time");
                Statistics.ErrorCount++;
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Gets system power information
        /// </summary>
        /// <returns>System power information</returns>
        public SystemPowerInfo GetSystemPowerInfo()
        {
            try
            {
                var powerInfo = new SYSTEM_POWER_INFORMATION();
                var powerInfoSize = Marshal.SizeOf(powerInfo);
                var powerInfoPtr = Marshal.AllocHGlobal(powerInfoSize);

                try
                {
                    var result = CallNtPowerInformation(SystemPowerInformation, IntPtr.Zero, 0,
                        powerInfoPtr, powerInfoSize);

                    if (result == 0)
                    {
                        powerInfo = (SYSTEM_POWER_INFORMATION)Marshal.PtrToStructure(powerInfoPtr, typeof(SYSTEM_POWER_INFORMATION));

                        return new SystemPowerInfo
                        {
                            MaxIdlenessAllowed = powerInfo.MaxIdlenessAllowed,
                            CurrentIdleness = powerInfo.Idleness,
                            TimeRemaining = TimeSpan.FromSeconds(powerInfo.TimeRemaining),
                            CoolingMode = powerInfo.CoolingMode
                        };
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(powerInfoPtr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system power information");
                Statistics.ErrorCount++;
            }

            return new SystemPowerInfo();
        }

        /// <summary>
        /// Gets the foreground window information
        /// </summary>
        /// <returns>Foreground window information</returns>
        public WindowInfo GetForegroundWindowInfo()
        {
            try
            {
                var hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                    return new WindowInfo();

                GetWindowThreadProcessId(hWnd, out uint processId);

                const int nChars = 256;
                var buffer = new System.Text.StringBuilder(nChars);
                var titleLength = GetWindowText(hWnd, buffer, nChars);

                var windowInfo = new WindowInfo
                {
                    Handle = hWnd,
                    ProcessId = (int)processId,
                    Title = titleLength > 0 ? buffer.ToString() : string.Empty
                };

                // Try to get process name
                try
                {
                    var process = Process.GetProcessById(windowInfo.ProcessId);
                    windowInfo.ProcessName = process.ProcessName;
                }
                catch
                {
                    // Process may not be accessible
                }

                return windowInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting foreground window info");
                Statistics.ErrorCount++;
            }

            return new WindowInfo();
        }

        /// <summary>
        /// Gets mouse position and movement information
        /// </summary>
        /// <returns>Mouse position info</returns>
        public MousePositionInfo GetMousePosition()
        {
            try
            {
                if (GetCursorPos(out POINT point))
                {
                    return new MousePositionInfo
                    {
                        X = point.X,
                        Y = point.Y,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mouse position");
                Statistics.ErrorCount++;
            }

            return new MousePositionInfo();
        }

        /// <summary>
        /// Initializes process monitoring using WMI
        /// </summary>
        private void InitializeProcessMonitoring()
        {
            if (!_config.EnableProcessMonitoring)
                return;

            try
            {
                var query = new WqlEventQuery(
                    "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName LIKE '%sldworks%'");

                _processWatcher = new ManagementEventWatcher(query);
                _processWatcher.EventArrived += OnProcessStarted;
                _processWatcher.Start();

                _logger.LogDebug("Process monitoring initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing process monitoring");
                Statistics.ErrorCount++;
            }
        }

        /// <summary>
        /// Stops process monitoring
        /// </summary>
        private void StopProcessMonitoring()
        {
            if (_processWatcher != null)
            {
                _processWatcher.EventArrived -= OnProcessStarted;
                _processWatcher.Stop();
                _processWatcher.Dispose();
                _processWatcher = null;
            }
        }

        /// <summary>
        /// Handles process start events
        /// </summary>
        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessId"].Value);
                var processName = e.NewEvent.Properties["ProcessName"].Value.ToString();

                var activityEvent = new SystemActivityEvent
                {
                    ActivityType = SystemActivityType.ProcessStart,
                    Timestamp = DateTime.UtcNow,
                    Confidence = 0.9,
                    ProcessId = processId,
                    ProcessName = processName
                };

                StoreActivityEvent(activityEvent);
                OnSystemActivityDetected(activityEvent);

                _logger.LogDebug("Process start detected: {ProcessName} (ID: {ProcessId})", processName, processId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling process start event");
                Statistics.ErrorCount++;
            }
        }

        /// <summary>
        /// Performs initial tracking activities
        /// </summary>
        private async Task PerformInitialTrackingAsync()
        {
            try
            {
                var allEvents = new List<SystemActivityEvent>();

                // Detect initial system state
                if (_config.EnableInputTracking)
                {
                    allEvents.AddRange(DetectInputActivity());
                }

                if (_config.EnableSystemResourceTracking)
                {
                    allEvents.AddRange(DetectSystemResourceActivity());
                }

                if (_config.EnableProcessMonitoring)
                {
                    allEvents.AddRange(DetectProcessActivity());
                }

                if (_config.EnableWindowTracking)
                {
                    allEvents.AddRange(DetectWindowActivity());
                }

                // Store events and raise notifications
                foreach (var activityEvent in allEvents)
                {
                    StoreActivityEvent(activityEvent);
                    OnSystemActivityDetected(activityEvent);
                }

                Statistics.TotalEventsProcessed += allEvents.Count;
                _logger.LogDebug("Initial tracking completed: {Count} events detected", allEvents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial tracking");
                Statistics.ErrorCount++;
            }
        }

        /// <summary>
        /// Main tracking loop
        /// </summary>
        private async Task TrackingLoop(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting system activity tracking loop");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.TrackingInterval, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var allEvents = new List<SystemActivityEvent>();

                    // Detect different types of activity based on configuration
                    if (_config.EnableInputTracking)
                    {
                        allEvents.AddRange(DetectInputActivity());
                    }

                    if (_config.EnableSystemResourceTracking)
                    {
                        allEvents.AddRange(DetectSystemResourceActivity());
                    }

                    if (_config.EnableProcessMonitoring)
                    {
                        allEvents.AddRange(DetectProcessActivity());
                    }

                    if (_config.EnableWindowTracking)
                    {
                        allEvents.AddRange(DetectWindowActivity());
                    }

                    // Store events and raise notifications
                    foreach (var activityEvent in allEvents)
                    {
                        StoreActivityEvent(activityEvent);
                        OnSystemActivityDetected(activityEvent);
                    }

                    Statistics.TotalEventsProcessed += allEvents.Count;

                    // Clean up old events and records
                    CleanupOldData();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in system activity tracking loop");
                    Statistics.ErrorCount++;
                }
            }

            _logger.LogDebug("System activity tracking loop stopped");
        }

        /// <summary>
        /// Detects input activity (keyboard, mouse)
        /// </summary>
        private List<SystemActivityEvent> DetectInputActivity()
        {
            var events = new List<SystemActivityEvent>();

            try
            {
                var idleTime = GetSystemIdleTime();
                CurrentSystemIdleTime = idleTime;

                var wasActive = idleTime < _config.TrackingInterval;

                if (wasActive)
                {
                    LastSystemActivity = DateTime.UtcNow;

                    if (_config.EnableKeyboardTracking)
                    {
                        var keyboardEvent = new SystemActivityEvent
                        {
                            ActivityType = SystemActivityType.Keyboard,
                            Timestamp = DateTime.UtcNow,
                            Confidence = 0.95,
                            IdleTime = idleTime
                        };
                        keyboardEvent.Metadata["IdleTimeMs"] = idleTime.TotalMilliseconds;
                        events.Add(keyboardEvent);
                    }

                    if (_config.EnableMouseTracking)
                    {
                        var mouseEvent = new SystemActivityEvent
                        {
                            ActivityType = SystemActivityType.Mouse,
                            Timestamp = DateTime.UtcNow,
                            Confidence = 0.95,
                            IdleTime = idleTime
                        };
                        mouseEvent.Metadata["IdleTimeMs"] = idleTime.TotalMilliseconds;

                        // Add mouse position if tracking is enabled
                        if (_config.EnableMousePositionTracking)
                        {
                            var mousePos = GetMousePosition();
                            mouseEvent.Metadata["MouseX"] = mousePos.X;
                            mouseEvent.Metadata["MouseY"] = mousePos.Y;
                        }

                        events.Add(mouseEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting input activity");
                Statistics.ErrorCount++;
            }

            return events;
        }

        /// <summary>
        /// Detects system resource activity (CPU, memory, disk, network)
        /// </summary>
        private List<SystemActivityEvent> DetectSystemResourceActivity()
        {
            var events = new List<SystemActivityEvent>();

            try
            {
                // CPU Activity
                var cpuUsage = GetCpuUsage();
                if (cpuUsage > _config.CpuActivityThreshold)
                {
                    var cpuEvent = new SystemActivityEvent
                    {
                        ActivityType = SystemActivityType.CpuUsage,
                        Timestamp = DateTime.UtcNow,
                        Confidence = Math.Min(1.0, cpuUsage / 100),
                        CpuUsage = cpuUsage
                    };
                    cpuEvent.Metadata["CpuUsage"] = cpuUsage;
                    cpuEvent.Metadata["Threshold"] = _config.CpuActivityThreshold;
                    events.Add(cpuEvent);
                }

                // Memory Activity
                var memoryUsage = GetMemoryUsage();
                if (memoryUsage > _config.MemoryActivityThreshold)
                {
                    var memoryEvent = new SystemActivityEvent
                    {
                        ActivityType = SystemActivityType.MemoryUsage,
                        Timestamp = DateTime.UtcNow,
                        Confidence = Math.Min(1.0, memoryUsage / 100),
                        MemoryUsage = memoryUsage
                    };
                    memoryEvent.Metadata["MemoryUsage"] = memoryUsage;
                    memoryEvent.Metadata["Threshold"] = _config.MemoryActivityThreshold;
                    events.Add(memoryEvent);
                }

                // Disk Activity
                var diskUsage = GetDiskUsage();
                if (diskUsage > _config.DiskActivityThreshold)
                {
                    var diskEvent = new SystemActivityEvent
                    {
                        ActivityType = SystemActivityType.DiskUsage,
                        Timestamp = DateTime.UtcNow,
                        Confidence = Math.Min(1.0, diskUsage / 100),
                        DiskUsage = diskUsage
                    };
                    diskEvent.Metadata["DiskUsage"] = diskUsage;
                    diskEvent.Metadata["Threshold"] = _config.DiskActivityThreshold;
                    events.Add(diskEvent);
                }

                // Network Activity
                var networkUsage = GetNetworkUsage();
                if (networkUsage > _config.NetworkActivityThreshold)
                {
                    var networkEvent = new SystemActivityEvent
                    {
                        ActivityType = SystemActivityType.NetworkUsage,
                        Timestamp = DateTime.UtcNow,
                        Confidence = Math.Min(1.0, networkUsage / 100),
                        NetworkUsage = networkUsage
                    };
                    networkEvent.Metadata["NetworkUsage"] = networkUsage;
                    networkEvent.Metadata["Threshold"] = _config.NetworkActivityThreshold;
                    events.Add(networkEvent);
                }

                // Power state changes
                var powerInfo = GetSystemPowerInfo();
                if (powerInfo.CurrentIdleness > 0)
                {
                    var powerEvent = new SystemActivityEvent
                    {
                        ActivityType = SystemActivityType.PowerState,
                        Timestamp = DateTime.UtcNow,
                        Confidence = Math.Min(1.0, powerInfo.CurrentIdleness / 100)
                    };
                    powerEvent.Metadata["Idleness"] = powerInfo.CurrentIdleness;
                    powerEvent.Metadata["TimeRemainingSec"] = powerInfo.TimeRemaining.TotalSeconds;
                    events.Add(powerEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting system resource activity");
                Statistics.ErrorCount++;
            }

            return events;
        }

        /// <summary>
        /// Detects process activity
        /// </summary>
        private List<SystemActivityEvent> DetectProcessActivity()
        {
            var events = new List<SystemActivityEvent>();

            try
            {
                var processes = Process.GetProcesses();
                var solidWorksProcesses = processes
                    .Where(p => p.ProcessName.Contains("SLDWORKS", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var process in solidWorksProcesses)
                {
                    try
                    {
                        if (process.Responding && !process.HasExited)
                        {
                            var processRecord = UpdateProcessRecord(process);

                            var activityEvent = new SystemActivityEvent
                            {
                                ActivityType = SystemActivityType.ProcessActivity,
                                Timestamp = DateTime.UtcNow,
                                Confidence = 0.8,
                                ProcessId = process.Id,
                                ProcessName = process.ProcessName
                            };
                            activityEvent.Metadata["Responding"] = process.Responding;
                            activityEvent.Metadata["WorkingSet"] = process.WorkingSet64;
                            activityEvent.Metadata["PrivateMemory"] = process.PrivateMemorySize64;
                            activityEvent.Metadata["CpuTime"] = process.TotalProcessorTime.TotalMilliseconds;
                            activityEvent.Metadata["ThreadCount"] = process.Threads.Count;

                            events.Add(activityEvent);

                            // Raise process activity event
                            OnProcessActivityDetected(processRecord);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error monitoring SolidWorks process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting process activity");
                Statistics.ErrorCount++;
            }

            return events;
        }

        /// <summary>
        /// Detects window activity
        /// </summary>
        private List<SystemActivityEvent> DetectWindowActivity()
        {
            var events = new List<SystemActivityEvent>();

            try
            {
                var windowInfo = GetForegroundWindowInfo();
                if (!string.IsNullOrEmpty(windowInfo.Title))
                {
                    var isSolidWorksWindow = windowInfo.ProcessName?.Contains("SLDWORKS", StringComparison.OrdinalIgnoreCase) ?? false;

                    var windowEvent = new SystemActivityEvent
                    {
                        ActivityType = SystemActivityType.WindowFocus,
                        Timestamp = DateTime.UtcNow,
                        Confidence = isSolidWorksWindow ? 0.9 : 0.7,
                        WindowTitle = windowInfo.Title,
                        ProcessId = windowInfo.ProcessId,
                        ProcessName = windowInfo.ProcessName
                    };
                    windowEvent.Metadata["WindowHandle"] = windowInfo.Handle.ToString();
                    windowEvent.Metadata["IsSolidWorks"] = isSolidWorksWindow;

                    events.Add(windowEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting window activity");
                Statistics.ErrorCount++;
            }

            return events;
        }

        /// <summary>
        /// Updates process activity record
        /// </summary>
        private ProcessActivityRecord UpdateProcessRecord(System.Diagnostics.Process process)
        {
            return _processRecords.AddOrUpdate(process.Id,
                pid => CreateProcessRecord(process),
                (pid, existingRecord) => UpdateExistingProcessRecord(existingRecord, process));
        }

        /// <summary>
        /// Creates a new process activity record
        /// </summary>
        private ProcessActivityRecord CreateProcessRecord(System.Diagnostics.Process process)
        {
            return new ProcessActivityRecord
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                StartTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive = true,
                WorkingSetSize = process.WorkingSet64,
                PrivateMemorySize = process.PrivateMemorySize64,
                ThreadCount = process.Threads.Count,
                CpuTimeMs = process.TotalProcessorTime.TotalMilliseconds,
                IsResponding = process.Responding
            };
        }

        /// <summary>
        /// Updates an existing process activity record
        /// </summary>
        private ProcessActivityRecord UpdateExistingProcessRecord(ProcessActivityRecord record, System.Diagnostics.Process process)
        {
            record.LastActivityTime = DateTime.UtcNow;
            record.WorkingSetSize = process.WorkingSet64;
            record.PrivateMemorySize = process.PrivateMemorySize64;
            record.ThreadCount = process.Threads.Count;
            record.CpuTimeMs = process.TotalProcessorTime.TotalMilliseconds;
            record.IsResponding = process.Responding;
            record.IsActive = process.Responding && !process.HasExited;

            return record;
        }

        /// <summary>
        /// Gets CPU usage percentage
        /// </summary>
        private double GetCpuUsage()
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0, need to wait
                Thread.Sleep(50);
                return cpuCounter.NextValue();
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Gets memory usage percentage
        /// </summary>
        private double GetMemoryUsage()
        {
            try
            {
                var memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                return memCounter.NextValue();
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Gets disk usage percentage
        /// </summary>
        private double GetDiskUsage()
        {
            try
            {
                var diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                return diskCounter.NextValue();
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Gets network usage
        /// </summary>
        private double GetNetworkUsage()
        {
            try
            {
                var networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", "*");
                return networkCounter.NextValue() / 1024; // Convert to KB/s
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Stores activity event in history
        /// </summary>
        private void StoreActivityEvent(SystemActivityEvent activityEvent)
        {
            lock (_lock)
            {
                _activityHistory.Add(activityEvent);

                // Keep only recent events (configurable retention period)
                var cutoffTime = DateTime.UtcNow - _config.EventRetentionPeriod;
                _activityHistory.RemoveAll(e => e.Timestamp < cutoffTime);
            }
        }

        /// <summary>
        /// Cleans up old data
        /// </summary>
        private void CleanupOldData()
        {
            lock (_lock)
            {
                // Clean up old activity events
                var cutoffTime = DateTime.UtcNow - _config.EventRetentionPeriod;
                _activityHistory.RemoveAll(e => e.Timestamp < cutoffTime);

                // Clean up inactive process records
                var processCutoffTime = DateTime.UtcNow - _config.ProcessRecordRetentionPeriod;
                var inactiveProcesses = _processRecords
                    .Where(p => p.Value.LastActivityTime < processCutoffTime)
                    .Select(p => p.Key)
                    .ToList();

                foreach (var processId in inactiveProcesses)
                {
                    _processRecords.TryRemove(processId, out _);
                }
            }
        }

        /// <summary>
        /// Raises the SystemActivityDetected event
        /// </summary>
        private void OnSystemActivityDetected(SystemActivityEvent activityEvent)
        {
            try
            {
                SystemActivityDetected?.Invoke(this, activityEvent);
                Statistics.EventsNotified++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising SystemActivityDetected event");
                Statistics.ErrorCount++;
            }
        }

        /// <summary>
        /// Raises the ProcessActivityDetected event
        /// </summary>
        private void OnProcessActivityDetected(ProcessActivityRecord processRecord)
        {
            try
            {
                ProcessActivityDetected?.Invoke(this, processRecord);
                Statistics.ProcessEventsNotified++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising ProcessActivityDetected event");
                Statistics.ErrorCount++;
            }
        }

        /// <summary>
        /// Disposes the tracker resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the tracker resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                    StopProcessMonitoring();
                }

                _isDisposed = true;
            }
        }
    }
}