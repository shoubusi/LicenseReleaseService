using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Monitors network activity for SolidWorks processes, including collaborative sessions
    /// </summary>
    public class NetworkActivityMonitor : IDisposable
    {
        private readonly ILogger<NetworkActivityMonitor> _logger;
        private readonly PingBasedDetectionConfig _config;
        private readonly object _lock = new object();
        private readonly Dictionary<int, NetworkActivityHistory> _activityHistories = new Dictionary<int, NetworkActivityHistory>();
        private readonly Timer _cleanupTimer;
        private readonly Timer _monitoringTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private bool _isDisposed;

        #region Windows API Imports for Network Monitoring

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, TCP_TABLE_CLASS tblClass, int reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedUdpTable(IntPtr pUdpTable, ref int dwOutBufLen, bool sort, int ipVersion, UDP_TABLE_CLASS tblClass, int reserved);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        private enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        private enum UDP_TABLE_CLASS
        {
            UDP_TABLE_BASIC,
            UDP_TABLE_OWNER_PID,
            UDP_TABLE_OWNER_MODULE
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint State;
            public uint LocalAddr;
            public byte LocalPort1;
            public byte LocalPort2;
            public byte LocalPort3;
            public byte LocalPort4;
            public uint RemoteAddr;
            public byte RemotePort1;
            public byte RemotePort2;
            public byte RemotePort3;
            public byte RemotePort4;
            public int OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint LocalAddr;
            public byte LocalPort1;
            public byte LocalPort2;
            public byte LocalPort3;
            public byte LocalPort4;
            public int OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            MIB_TCPROW_OWNER_PID table;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when network activity is detected
        /// </summary>
        public event EventHandler<NetworkActivityData> NetworkActivityDetected;

        /// <summary>
        /// Gets a value indicating whether the monitor is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the monitor statistics
        /// </summary>
        public NetworkActivityStatistics Statistics { get; private set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the NetworkActivityMonitor class
        /// </summary>
        public NetworkActivityMonitor(ILogger<NetworkActivityMonitor> logger, PingBasedDetectionConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Statistics = new NetworkActivityStatistics();

            // Initialize cleanup timer for old activity histories
            _cleanupTimer = new Timer(CleanupTimerCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            // Initialize monitoring timer for periodic network checks
            _monitoringTimer = new Timer(MonitoringTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            _logger.LogInformation("NetworkActivityMonitor initialized with configuration: {Config}", config);
        }

        /// <summary>
        /// Initializes the monitor with configuration
        /// </summary>
        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing NetworkActivityMonitor");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("NetworkActivityMonitor is already running");
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Clear any existing activity histories
                lock (_lock)
                {
                    _activityHistories.Clear();
                    Statistics.Reset();
                }

                // Initialize network monitoring components
                await InitializeNetworkMonitoringAsync();

                _logger.LogInformation("NetworkActivityMonitor initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing NetworkActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Starts the network activity monitoring
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting NetworkActivityMonitor");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("NetworkActivityMonitor is already running");
                        return;
                    }

                    IsRunning = true;
                }

                // Start monitoring timer
                var dueTime = TimeSpan.FromSeconds(15); // Check every 15 seconds
                var period = TimeSpan.FromSeconds(15);
                _monitoringTimer.Change(dueTime, period);

                // Start continuous monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("NetworkActivityMonitor started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting NetworkActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Stops the network activity monitoring
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping NetworkActivityMonitor");

                lock (_lock)
                {
                    if (!IsRunning)
                    {
                        _logger.LogWarning("NetworkActivityMonitor is not running");
                        return;
                    }

                    IsRunning = false;
                }

                // Stop monitoring timer
                _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Cancel monitoring task
                _cancellationTokenSource?.Cancel();

                // Wait for monitoring task to complete
                if (_monitoringTask != null)
                {
                    // Wait for monitoring task with timeout using WaitAsync
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _monitoringTask.WaitAsync(timeoutCts.Token);
                }

                _logger.LogInformation("NetworkActivityMonitor stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping NetworkActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Pauses the network activity monitoring
        /// </summary>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing NetworkActivityMonitor");

                lock (_lock)
                {
                    if (!IsRunning)
                    {
                        _logger.LogWarning("NetworkActivityMonitor is not running");
                        return;
                    }

                    IsRunning = false;
                }

                // Stop monitoring timer
                _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);

                _cancellationTokenSource?.Cancel();

                _logger.LogInformation("NetworkActivityMonitor paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing NetworkActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Resumes the network activity monitoring
        /// </summary>
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming NetworkActivityMonitor");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("NetworkActivityMonitor is already running");
                        return;
                    }

                    IsRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Start monitoring timer
                var dueTime = TimeSpan.FromSeconds(15);
                var period = TimeSpan.FromSeconds(15);
                _monitoringTimer.Change(dueTime, period);

                // Restart monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("NetworkActivityMonitor resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming NetworkActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Gets recent network activities for a process
        /// </summary>
        public async Task<IList<NetworkActivityData>> GetRecentActivitiesAsync(int processId, TimeSpan timeWindow, CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    if (_activityHistories.TryGetValue(processId, out var history))
                    {
                        return history.GetRecentActivities(timeWindow);
                    }
                }

                return new List<NetworkActivityData>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent network activities for process {ProcessId}", processId);
                return new List<NetworkActivityData>();
            }
        }

        /// <summary>
        /// Initializes network monitoring components
        /// </summary>
        private async Task InitializeNetworkMonitoringAsync()
        {
            try
            {
                // Test network monitoring capabilities
                var canMonitorTcp = await TestTcpMonitoringAsync();
                var canMonitorUdp = await TestUdpMonitoringAsync();

                _logger.LogInformation("Network monitoring capabilities: TCP={TcpCapable}, UDP={UdpCapable}",
                    canMonitorTcp, canMonitorUdp);

                // Initialize known SolidWorks network ports and protocols
                InitializeSolidWorksNetworkProfiles();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing network monitoring");
                throw;
            }
        }

        /// <summary>
        /// Tests TCP monitoring capabilities
        /// </summary>
        private async Task<bool> TestTcpMonitoringAsync()
        {
            try
            {
                // Try to get TCP connections table
                var bufferSize = 0;
                var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

                if (result == 122) // ERROR_INSUFFICIENT_BUFFER
                {
                    var buffer = Marshal.AllocHGlobal(bufferSize);
                    try
                    {
                        result = GetExtendedTcpTable(buffer, ref bufferSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                        return result == 0;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TCP monitoring test failed");
                return false;
            }
        }

        /// <summary>
        /// Tests UDP monitoring capabilities
        /// </summary>
        private async Task<bool> TestUdpMonitoringAsync()
        {
            try
            {
                // Try to get UDP endpoints table
                var bufferSize = 0;
                var result = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

                if (result == 122) // ERROR_INSUFFICIENT_BUFFER
                {
                    var buffer = Marshal.AllocHGlobal(bufferSize);
                    try
                    {
                        result = GetExtendedUdpTable(buffer, ref bufferSize, true, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                        return result == 0;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UDP monitoring test failed");
                return false;
            }
        }

        /// <summary>
        /// Initializes known SolidWorks network profiles
        /// </summary>
        private void InitializeSolidWorksNetworkProfiles()
        {
            try
            {
                // SolidWorks uses various network protocols and ports for different features:
                // - Network licensing (port 25734 by default)
                // - Collaborative sessions and PDM/TeamWorks (various ports)
                // - Network file access
                // - Add-in communications
                // - Cloud services

                _logger.LogDebug("Initialized SolidWorks network monitoring profiles");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SolidWorks network profiles");
            }
        }

        /// <summary>
        /// Main monitoring loop for network activity
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                try
                {
                    // Monitor TCP connections for SolidWorks processes
                    await MonitorTcpConnectionsAsync(cancellationToken);

                    // Monitor UDP endpoints for SolidWorks processes
                    await MonitorUdpEndpointsAsync(cancellationToken);

                    // Monitor network interface activity
                    await MonitorNetworkInterfacesAsync(cancellationToken);

                    // Check for specific SolidWorks network patterns
                    await CheckSolidWorksNetworkPatternsAsync(cancellationToken);

                    // Wait before next check
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in network activity monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Delay on error
                }
            }
        }

        /// <summary>
        /// Monitors TCP connections for SolidWorks processes
        /// </summary>
        private async Task MonitorTcpConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var tcpConnections = GetProcessTcpConnections();
                var solidWorksProcesses = GetSolidWorksProcesses();

                foreach (var process in solidWorksProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var processConnections = tcpConnections.Where(c => c.ProcessId == process.Id).ToList();

                    foreach (var connection in processConnections)
                    {
                        // Check if this is a significant network connection
                        if (IsSignificantNetworkConnection(connection))
                        {
                            var activity = new NetworkActivityData
                            {
                                ProcessId = process.Id,
                                ActivityType = "TCP_Connection",
                                RemoteEndpoint = $"{connection.RemoteAddress}:{connection.RemotePort}",
                                Timestamp = DateTime.UtcNow,
                                Confidence = CalculateConnectionConfidence(connection),
                                Metadata = new Dictionary<string, object>
                                {
                                    ["LocalAddress"] = connection.LocalAddress.ToString(),
                                    ["LocalPort"] = connection.LocalPort,
                                    ["RemoteAddress"] = connection.RemoteAddress.ToString(),
                                    ["RemotePort"] = connection.RemotePort,
                                    ["State"] = connection.State.ToString(),
                                    ["Protocol"] = "TCP",
                                    ["ProcessName"] = process.ProcessName,
                                    ["ConnectionDuration"] = connection.Duration
                                }
                            };

                            await RecordNetworkActivityAsync(activity);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring TCP connections");
            }
        }

        /// <summary>
        /// Monitors UDP endpoints for SolidWorks processes
        /// </summary>
        private async Task MonitorUdpEndpointsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var udpEndpoints = GetProcessUdpEndpoints();
                var solidWorksProcesses = GetSolidWorksProcesses();

                foreach (var process in solidWorksProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var processEndpoints = udpEndpoints.Where(e => e.ProcessId == process.Id).ToList();

                    foreach (var endpoint in processEndpoints)
                    {
                        // Check if this is a significant UDP endpoint
                        if (IsSignificantUdpEndpoint(endpoint))
                        {
                            var activity = new NetworkActivityData
                            {
                                ProcessId = process.Id,
                                ActivityType = "UDP_Endpoint",
                                RemoteEndpoint = $"{endpoint.LocalAddress}:{endpoint.LocalPort}",
                                Timestamp = DateTime.UtcNow,
                                Confidence = 0.7,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["LocalAddress"] = endpoint.LocalAddress.ToString(),
                                    ["LocalPort"] = endpoint.LocalPort,
                                    ["Protocol"] = "UDP",
                                    ["ProcessName"] = process.ProcessName,
                                    ["EndpointType"] = "Bound"
                                }
                            };

                            await RecordNetworkActivityAsync(activity);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring UDP endpoints");
            }
        }

        /// <summary>
        /// Monitors network interface activity
        /// </summary>
        private async Task MonitorNetworkInterfacesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var networkInterface in interfaces)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                        networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        var stats = networkInterface.GetIPStatistics();

                        // Check for significant network activity
                        if (stats.BytesReceived > 0 || stats.BytesSent > 0)
                        {
                            // This is system-wide activity, not process-specific
                            // In a full implementation, you might correlate this with process activity
                            _logger.LogDebug("Network interface activity detected: {InterfaceName}, Sent={Sent}, Received={Received}",
                                networkInterface.Name, stats.BytesSent, stats.BytesReceived);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring network interfaces");
            }
        }

        /// <summary>
        /// Checks for SolidWorks-specific network patterns
        /// </summary>
        private async Task CheckSolidWorksNetworkPatternsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Check for SolidWorks PDM/TeamWorks activity
                await CheckPdmActivityAsync(cancellationToken);

                // Check for network licensing activity
                await CheckLicensingActivityAsync(cancellationToken);

                // Check for collaborative session activity
                await CheckCollaborativeActivityAsync(cancellationToken);

                // Check for cloud service activity
                await CheckCloudServiceActivityAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SolidWorks network patterns");
            }
        }

        /// <summary>
        /// Checks for PDM (Product Data Management) activity
        /// </summary>
        private async Task CheckPdmActivityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // SolidWorks PDM typically uses specific ports and communication patterns
                // This is a simplified implementation
                var pdmPorts = new[] { 25734, 80, 443, 1433 }; // Common PDM ports

                var connections = GetProcessTcpConnections();
                var pdmConnections = connections.Where(c =>
                    pdmPorts.Contains(c.RemotePort) ||
                    c.RemoteAddress.ToString().Contains("solidworks") ||
                    c.RemoteAddress.ToString().Contains("dassault"));

                foreach (var connection in pdmConnections)
                {
                    var solidWorksProcesses = GetSolidWorksProcesses();
                    var process = solidWorksProcesses.FirstOrDefault(p => p.Id == connection.ProcessId);

                    if (process != null)
                    {
                        var activity = new NetworkActivityData
                        {
                            ProcessId = process.Id,
                            ActivityType = "PDM_Activity",
                            RemoteEndpoint = $"{connection.RemoteAddress}:{connection.RemotePort}",
                            Timestamp = DateTime.UtcNow,
                            Confidence = 0.9,
                            Metadata = new Dictionary<string, object>
                            {
                                ["PDMType"] = "VaultConnection",
                                ["LocalAddress"] = connection.LocalAddress.ToString(),
                                ["LocalPort"] = connection.LocalPort,
                                ["RemoteAddress"] = connection.RemoteAddress.ToString(),
                                ["RemotePort"] = connection.RemotePort,
                                ["ProcessName"] = process.ProcessName
                            }
                        };

                        await RecordNetworkActivityAsync(activity);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking PDM activity");
            }
        }

        /// <summary>
        /// Checks for network licensing activity
        /// </summary>
        private async Task CheckLicensingActivityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // SolidWorks network licensing typically uses port 25734
                var licensingConnections = GetProcessTcpConnections()
                    .Where(c => c.RemotePort == 25734 || c.LocalPort == 25734);

                foreach (var connection in licensingConnections)
                {
                    var solidWorksProcesses = GetSolidWorksProcesses();
                    var process = solidWorksProcesses.FirstOrDefault(p => p.Id == connection.ProcessId);

                    if (process != null)
                    {
                        var activity = new NetworkActivityData
                        {
                            ProcessId = process.Id,
                            ActivityType = "Licensing_Check",
                            RemoteEndpoint = $"{connection.RemoteAddress}:{connection.RemotePort}",
                            Timestamp = DateTime.UtcNow,
                            Confidence = 0.8,
                            Metadata = new Dictionary<string, object>
                            {
                                ["LicenseServer"] = connection.RemoteAddress.ToString(),
                                ["LicensePort"] = connection.RemotePort,
                                ["ConnectionState"] = connection.State.ToString(),
                                ["ProcessName"] = process.ProcessName
                            }
                        };

                        await RecordNetworkActivityAsync(activity);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking licensing activity");
            }
        }

        /// <summary>
        /// Checks for collaborative session activity
        /// </summary>
        private async Task CheckCollaborativeActivityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Check for connections that might indicate collaborative sessions
                var connections = GetProcessTcpConnections();
                var collaborativeConnections = connections.Where(c =>
                    c.RemotePort > 1024 && // High port numbers
                    c.State == TcpState.Established &&
                    (c.RemoteAddress.ToString().StartsWith("192.168.") ||
                     c.RemoteAddress.ToString().StartsWith("10.") ||
                     c.RemoteAddress.ToString().StartsWith("172."))); // Internal network ranges

                foreach (var connection in collaborativeConnections)
                {
                    var solidWorksProcesses = GetSolidWorksProcesses();
                    var process = solidWorksProcesses.FirstOrDefault(p => p.Id == connection.ProcessId);

                    if (process != null)
                    {
                        var activity = new NetworkActivityData
                        {
                            ProcessId = process.Id,
                            ActivityType = "Collaborative_Session",
                            RemoteEndpoint = $"{connection.RemoteAddress}:{connection.RemotePort}",
                            Timestamp = DateTime.UtcNow,
                            Confidence = 0.6,
                            Metadata = new Dictionary<string, object>
                            {
                                ["SessionType"] = "PeerToPeer",
                                ["LocalAddress"] = connection.LocalAddress.ToString(),
                                ["LocalPort"] = connection.LocalPort,
                                ["RemoteAddress"] = connection.RemoteAddress.ToString(),
                                ["RemotePort"] = connection.RemotePort,
                                ["ProcessName"] = process.ProcessName,
                                ["ConnectionAge"] = connection.Duration
                            }
                        };

                        await RecordNetworkActivityAsync(activity);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking collaborative activity");
            }
        }

        /// <summary>
        /// Checks for cloud service activity
        /// </summary>
        private async Task CheckCloudServiceActivityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Check for connections to known cloud services
                var connections = GetProcessTcpConnections();
                var cloudEndpoints = new[] { "3dexperience.3ds.com", "solidworks.com", "dassault-systemes.com" };

                var cloudConnections = connections.Where(c =>
                    cloudEndpoints.Any(endpoint => c.RemoteAddress.ToString().Contains(endpoint)));

                foreach (var connection in cloudConnections)
                {
                    var solidWorksProcesses = GetSolidWorksProcesses();
                    var process = solidWorksProcesses.FirstOrDefault(p => p.Id == connection.ProcessId);

                    if (process != null)
                    {
                        var activity = new NetworkActivityData
                        {
                            ProcessId = process.Id,
                            ActivityType = "Cloud_Service",
                            RemoteEndpoint = $"{connection.RemoteAddress}:{connection.RemotePort}",
                            Timestamp = DateTime.UtcNow,
                            Confidence = 0.7,
                            Metadata = new Dictionary<string, object>
                            {
                                ["CloudProvider"] = "3DEXPERIENCE",
                                ["ServiceType"] = "CloudServices",
                                ["LocalAddress"] = connection.LocalAddress.ToString(),
                                ["LocalPort"] = connection.LocalPort,
                                ["RemoteAddress"] = connection.RemoteAddress.ToString(),
                                ["RemotePort"] = connection.RemotePort,
                                ["ProcessName"] = process.ProcessName
                            }
                        };

                        await RecordNetworkActivityAsync(activity);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cloud service activity");
            }
        }

        /// <summary>
        /// Gets TCP connections for all processes
        /// </summary>
        private List<ProcessTcpConnection> GetProcessTcpConnections()
        {
            var connections = new List<ProcessTcpConnection>();

            try
            {
                var bufferSize = 0;
                var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

                if (result != 122) // Not ERROR_INSUFFICIENT_BUFFER
                    return connections;

                var buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    result = GetExtendedTcpTable(buffer, ref bufferSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                    if (result != 0)
                        return connections;

                    var table = (MIB_TCPTABLE_OWNER_PID)Marshal.PtrToStructure(buffer, typeof(MIB_TCPTABLE_OWNER_PID));
                    var rowPtr = (IntPtr)((long)buffer + Marshal.SizeOf(table.dwNumEntries));

                    for (var i = 0; i < table.dwNumEntries; i++)
                    {
                        var row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                        var connection = CreateTcpConnectionFromRow(row);
                        connections.Add(connection);

                        rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID)));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TCP connections");
            }

            return connections;
        }

        /// <summary>
        /// Gets UDP endpoints for all processes
        /// </summary>
        private List<ProcessUdpEndpoint> GetProcessUdpEndpoints()
        {
            var endpoints = new List<ProcessUdpEndpoint>();

            try
            {
                var bufferSize = 0;
                var result = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

                if (result != 122) // Not ERROR_INSUFFICIENT_BUFFER
                    return endpoints;

                var buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    result = GetExtendedUdpTable(buffer, ref bufferSize, true, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                    if (result != 0)
                        return endpoints;

                    var numEntries = BitConverter.ToUInt32(BitConverter.GetBytes(Marshal.ReadInt32(buffer)), 0);
                    var rowPtr = (IntPtr)((long)buffer + 4);

                    for (var i = 0; i < numEntries; i++)
                    {
                        var row = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDPROW_OWNER_PID));
                        var endpoint = CreateUdpEndpointFromRow(row);
                        endpoints.Add(endpoint);

                        rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID)));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting UDP endpoints");
            }

            return endpoints;
        }

        /// <summary>
        /// Creates a TCP connection from a Windows API row
        /// </summary>
        private ProcessTcpConnection CreateTcpConnectionFromRow(MIB_TCPROW_OWNER_PID row)
        {
            return new ProcessTcpConnection
            {
                ProcessId = row.OwningPid,
                LocalAddress = new IPAddress(BitConverter.GetBytes(row.LocalAddr)),
                LocalPort = (row.LocalPort1 << 8) | row.LocalPort2,
                RemoteAddress = new IPAddress(BitConverter.GetBytes(row.RemoteAddr)),
                RemotePort = (row.RemotePort1 << 8) | row.RemotePort2,
                State = (TcpState)row.State,
                Duration = TimeSpan.FromSeconds(0) // Duration tracking would require additional logic
            };
        }

        /// <summary>
        /// Creates a UDP endpoint from a Windows API row
        /// </summary>
        private ProcessUdpEndpoint CreateUdpEndpointFromRow(MIB_UDPROW_OWNER_PID row)
        {
            return new ProcessUdpEndpoint
            {
                ProcessId = row.OwningPid,
                LocalAddress = new IPAddress(BitConverter.GetBytes(row.LocalAddr)),
                LocalPort = (row.LocalPort1 << 8) | row.LocalPort2
            };
        }

        /// <summary>
        /// Gets all running SolidWorks processes
        /// </summary>
        private List<System.Diagnostics.Process> GetSolidWorksProcesses()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SolidWorks processes");
                return new List<System.Diagnostics.Process>();
            }
        }

        /// <summary>
        /// Determines if a TCP connection is significant
        /// </summary>
        private bool IsSignificantNetworkConnection(ProcessTcpConnection connection)
        {
            // Consider connections significant if:
            // 1. They're established (not listening or time_wait)
            // 2. They're to remote addresses (not loopback)
            // 3. They're on significant ports
            return connection.State == TcpState.Established &&
                   !connection.RemoteAddress.Equals(IPAddress.Loopback) &&
                   !connection.RemoteAddress.Equals(IPAddress.Any) &&
                   (connection.RemotePort > 1024 || // User port
                    connection.RemotePort == 80 ||    // HTTP
                    connection.RemotePort == 443 ||   // HTTPS
                    connection.RemotePort == 25734);  // SolidWorks licensing
        }

        /// <summary>
        /// Determines if a UDP endpoint is significant
        /// </summary>
        private bool IsSignificantUdpEndpoint(ProcessUdpEndpoint endpoint)
        {
            // Consider UDP endpoints significant if:
            // 1. They're on significant ports
            // 2. They're not loopback
            return !endpoint.LocalAddress.Equals(IPAddress.Loopback) &&
                   !endpoint.LocalAddress.Equals(IPAddress.Any) &&
                   (endpoint.LocalPort > 1024 ||
                    endpoint.LocalPort == 25734); // SolidWorks licensing
        }

        /// <summary>
        /// Calculates confidence score for a network connection
        /// </summary>
        private double CalculateConnectionConfidence(ProcessTcpConnection connection)
        {
            var confidence = 0.5; // Base confidence

            // Higher confidence for established connections
            if (connection.State == TcpState.Established)
                confidence += 0.2;

            // Higher confidence for connections to known SolidWorks services
            if (connection.RemotePort == 25734) // SolidWorks licensing
                confidence += 0.2;

            // Higher confidence for connections to internal network addresses
            if (connection.RemoteAddress.ToString().StartsWith("192.168.") ||
                connection.RemoteAddress.ToString().StartsWith("10.") ||
                connection.RemoteAddress.ToString().StartsWith("172."))
                confidence += 0.1;

            return Math.Min(1.0, confidence);
        }

        /// <summary>
        /// Records network activity
        /// </summary>
        private async Task RecordNetworkActivityAsync(NetworkActivityData activity)
        {
            try
            {
                lock (_lock)
                {
                    // Get or create activity history
                    if (!_activityHistories.TryGetValue(activity.ProcessId, out var history))
                    {
                        history = new NetworkActivityHistory(activity.ProcessId);
                        _activityHistories[activity.ProcessId] = history;
                    }

                    // Record the activity
                    history.RecordActivity(activity);

                    // Update statistics
                    Statistics.TotalActivitiesMonitored++;

                    switch (activity.ActivityType)
                    {
                        case "TCP_Connection":
                            Statistics.TcpConnections++;
                            break;
                        case "UDP_Endpoint":
                            Statistics.UdpEndpoints++;
                            break;
                        case "PDM_Activity":
                            Statistics.PdmActivities++;
                            break;
                        case "Licensing_Check":
                            Statistics.LicensingChecks++;
                            break;
                        case "Collaborative_Session":
                            Statistics.CollaborativeSessions++;
                            break;
                        case "Cloud_Service":
                            Statistics.CloudServiceActivities++;
                            break;
                    }
                }

                // Raise event
                NetworkActivityDetected?.Invoke(this, activity);

                _logger.LogDebug("Network activity recorded: Process={ProcessId}, Activity={Activity}, Endpoint={Endpoint}",
                    activity.ProcessId, activity.ActivityType, activity.RemoteEndpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording network activity for process {ProcessId}", activity.ProcessId);
            }
        }

        /// <summary>
        /// Timer callback for periodic network monitoring
        /// </summary>
        private void MonitoringTimerCallback(object state)
        {
            try
            {
                if (!IsRunning)
                    return;

                // This timer callback can be used for periodic checks
                // that don't need to be in the main monitoring loop
                _logger.LogDebug("Network monitoring timer callback triggered");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in network monitoring timer callback");
            }
        }

        /// <summary>
        /// Timer callback for cleaning up old activity histories
        /// </summary>
        private void CleanupTimerCallback(object state)
        {
            try
            {
                lock (_lock)
                {
                    var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(2);
                    var toRemove = new List<int>();

                    foreach (var kvp in _activityHistories)
                    {
                        if (kvp.Value.LastActivityTime < cutoffTime)
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var processId in toRemove)
                    {
                        _activityHistories.Remove(processId);
                    }

                    if (toRemove.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} old network activity histories", toRemove.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in network activity cleanup timer");
            }
        }

        /// <summary>
        /// Updates the monitor configuration
        /// </summary>
        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating NetworkActivityMonitor configuration");

                if (configuration?.CustomParameters != null)
                {
                    // Update network monitoring specific parameters
                    if (configuration.CustomParameters.ContainsKey("EnableNetworkActivityMonitoring"))
                    {
                        _config.EnableNetworkActivityMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableNetworkActivityMonitoring"]);
                    }
                }

                _logger.LogInformation("NetworkActivityMonitor configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating NetworkActivityMonitor configuration");
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
                    DetectorName = "NetworkActivityMonitor",
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                var isHealthy = true;
                var issues = new List<string>();

                // Check service status
                if (!IsRunning)
                {
                    issues.Add("Monitor is not running");
                    isHealthy = false;
                }

                // Check activity history size
                lock (_lock)
                {
                    if (_activityHistories.Count > 500)
                    {
                        issues.Add($"High activity history count: {_activityHistories.Count}");
                    }
                }

                // Check statistics for abnormal patterns
                if (Statistics.TotalActivitiesMonitored > 0)
                {
                    var errorRate = (double)Statistics.ErrorCount / Statistics.TotalActivitiesMonitored;
                    if (errorRate > 0.5)
                    {
                        issues.Add($"High error rate: {errorRate:P1}");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["ActivityHistoryCount"] = _activityHistories.Count;
                health.AdditionalInfo["TotalActivitiesMonitored"] = Statistics.TotalActivitiesMonitored;
                health.AdditionalInfo["TcpConnections"] = Statistics.TcpConnections;
                health.AdditionalInfo["UdpEndpoints"] = Statistics.UdpEndpoints;
                health.AdditionalInfo["PdmActivities"] = Statistics.PdmActivities;
                health.AdditionalInfo["LicensingChecks"] = Statistics.LicensingChecks;
                health.AdditionalInfo["CollaborativeSessions"] = Statistics.CollaborativeSessions;
                health.AdditionalInfo["CloudServiceActivities"] = Statistics.CloudServiceActivities;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting NetworkActivityMonitor health");
                return new DetectorHealth
                {
                    DetectorName = "NetworkActivityMonitor",
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        /// <summary>
        /// Gets monitor statistics
        /// </summary>
        public async Task<NetworkActivityStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                return new NetworkActivityStatistics
                {
                    TotalActivitiesMonitored = Statistics.TotalActivitiesMonitored,
                    TcpConnections = Statistics.TcpConnections,
                    UdpEndpoints = Statistics.UdpEndpoints,
                    PdmActivities = Statistics.PdmActivities,
                    LicensingChecks = Statistics.LicensingChecks,
                    CollaborativeSessions = Statistics.CollaborativeSessions,
                    CloudServiceActivities = Statistics.CloudServiceActivities,
                    SuccessfulDetections = Statistics.SuccessfulDetections,
                    FailedDetections = Statistics.FailedDetections,
                    ErrorCount = Statistics.ErrorCount,
                    StartTime = Statistics.StartTime,
                    LastActivityTime = Statistics.LastActivityTime
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
                    _activityHistories.Clear();
                }

                _logger.LogInformation("NetworkActivityMonitor statistics reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting NetworkActivityMonitor statistics");
                throw;
            }
        }

        /// <summary>
        /// Disposes the monitor
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
                    _logger.LogInformation("Disposing NetworkActivityMonitor");

                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                    _cleanupTimer?.Dispose();
                    _monitoringTimer?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a TCP connection for a process
    /// </summary>
    public class ProcessTcpConnection
    {
        public int ProcessId { get; set; }
        public IPAddress LocalAddress { get; set; }
        public int LocalPort { get; set; }
        public IPAddress RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public TcpState State { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Represents a UDP endpoint for a process
    /// </summary>
    public class ProcessUdpEndpoint
    {
        public int ProcessId { get; set; }
        public IPAddress LocalAddress { get; set; }
        public int LocalPort { get; set; }
    }

    /// <summary>
    /// Maintains network activity history for a specific process
    /// </summary>
    public class NetworkActivityHistory
    {
        private readonly object _lock = new object();
        private readonly Queue<NetworkActivityData> _recentActivities = new Queue<NetworkActivityData>(100);
        private readonly int _processId;

        public int ProcessId => _processId;
        public DateTime LastActivityTime { get; private set; }
        public int TotalActivities { get; set; }
        public DateTime? LastTcpConnection { get; private set; }
        public DateTime? LastUdpActivity { get; private set; }
        public string MostActiveEndpoint { get; private set; }

        public NetworkActivityHistory(int processId)
        {
            _processId = processId;
            LastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records network activity
        /// </summary>
        public void RecordActivity(NetworkActivityData activity)
        {
            lock (_lock)
            {
                _recentActivities.Enqueue(activity);
                if (_recentActivities.Count > 100)
                    _recentActivities.Dequeue();

                LastActivityTime = activity.Timestamp;
                TotalActivities++;

                if (activity.ActivityType.Contains("TCP"))
                {
                    LastTcpConnection = activity.Timestamp;
                }
                else if (activity.ActivityType.Contains("UDP"))
                {
                    LastUdpActivity = activity.Timestamp;
                }

                if (!string.IsNullOrEmpty(activity.RemoteEndpoint))
                {
                    MostActiveEndpoint = activity.RemoteEndpoint;
                }
            }
        }

        /// <summary>
        /// Gets recent activities within the specified time window
        /// </summary>
        public List<NetworkActivityData> GetRecentActivities(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentActivities.Where(a => a.Timestamp > cutoffTime).ToList();
            }
        }

        /// <summary>
        /// Gets the activity count within the specified time window
        /// </summary>
        public int GetRecentActivityCount(TimeSpan timeWindow)
        {
            return GetRecentActivities(timeWindow).Count;
        }

        /// <summary>
        /// Gets the time since last activity
        /// </summary>
        public TimeSpan GetTimeSinceLastActivity()
        {
            lock (_lock)
            {
                return DateTime.UtcNow - LastActivityTime;
            }
        }
    }

    /// <summary>
    /// Statistics for the network activity monitor
    /// </summary>
    public class NetworkActivityStatistics
    {
        public long TotalActivitiesMonitored { get; set; }
        public long TcpConnections { get; set; }
        public long UdpEndpoints { get; set; }
        public long PdmActivities { get; set; }
        public long LicensingChecks { get; set; }
        public long CollaborativeSessions { get; set; }
        public long CloudServiceActivities { get; set; }
        public long SuccessfulDetections { get; set; }
        public long FailedDetections { get; set; }
        public long ErrorCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastActivityTime { get; set; }

        public NetworkActivityStatistics()
        {
            StartTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            TotalActivitiesMonitored = 0;
            TcpConnections = 0;
            UdpEndpoints = 0;
            PdmActivities = 0;
            LicensingChecks = 0;
            CollaborativeSessions = 0;
            CloudServiceActivities = 0;
            SuccessfulDetections = 0;
            FailedDetections = 0;
            ErrorCount = 0;
            StartTime = DateTime.UtcNow;
            LastActivityTime = null;
        }
    }
}