using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Manages runtime context for a specific SolidWorks version including state,
    /// metrics, and operation history
    /// </summary>
    public class VersionRuntimeContext : IDisposable
    {
        private readonly SolidWorksVersionInfo _versionInfo;
        private readonly RuntimeManagementConfiguration _configuration;
        private readonly VersionRuntimeMetrics _metrics;
        private readonly OperationHistoryTracker _historyTracker;
        private readonly HealthMonitor _healthMonitor;
        private readonly object _contextLock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the version information for this context
        /// </summary>
        public SolidWorksVersionInfo VersionInfo => _versionInfo;

        /// <summary>
        /// Gets the version string
        /// </summary>
        public string Version => _versionInfo.Version;

        /// <summary>
        /// Gets whether this context is active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets when this context was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets when this context was last activated
        /// </summary>
        public DateTime LastActivatedAt { get; private set; }

        /// <summary>
        /// Gets the current health status
        /// </summary>
        public VersionHealthStatus HealthStatus { get; private set; }

        /// <summary>
        /// Initializes a new instance of the VersionRuntimeContext class
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="configuration">Runtime management configuration</param>
        public VersionRuntimeContext(
            SolidWorksVersionInfo versionInfo,
            RuntimeManagementConfiguration configuration)
        {
            _versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _metrics = new VersionRuntimeMetrics();
            _historyTracker = new OperationHistoryTracker(_configuration.MaxOperationHistory);
            _healthMonitor = new HealthMonitor();
            CreatedAt = DateTime.UtcNow;
            LastActivatedAt = CreatedAt;
            HealthStatus = VersionHealthStatus.Unknown;
        }

        /// <summary>
        /// Initializes the runtime context
        /// </summary>
        /// <returns>Initialization result</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                await _healthMonitor.InitializeAsync();
                IsActive = true;
                HealthStatus = VersionHealthStatus.Healthy;
                return true;
            }
            catch (Exception ex)
            {
                IsActive = false;
                HealthStatus = VersionHealthStatus.Error;
                return false;
            }
        }

        /// <summary>
        /// Checks if this context is suitable for a specific operation
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="requirements">Operation requirements</param>
        /// <returns>True if suitable</returns>
        public async Task<bool> IsSuitableForOperationAsync(
            VersionOperationType operationType,
            VersionOperationRequirements requirements)
        {
            if (!IsActive)
                return false;

            if (HealthStatus != VersionHealthStatus.Healthy)
                return false;

            // Check if we have capacity for the operation
            var currentLoad = await GetCurrentLoadAsync();
            if (currentLoad > 0.9) // 90% capacity threshold
                return false;

            // Check resource requirements
            if (requirements.MemoryRequirements > 0 && _metrics.MemoryUsage > requirements.MemoryRequirements)
                return false;

            if (requirements.CpuRequirements > 0 && _metrics.CpuUsage > requirements.CpuRequirements)
                return false;

            return true;
        }

        /// <summary>
        /// Records a successful operation
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="timestamp">Timestamp</param>
        public void RecordSuccessfulOperation(VersionOperationType operationType, DateTime timestamp)
        {
            lock (_contextLock)
            {
                _metrics.RecordSuccessfulOperation();
                _historyTracker.RecordOperation(new OperationRecord
                {
                    OperationType = operationType,
                    Success = true,
                    Timestamp = timestamp,
                    Duration = TimeSpan.FromMilliseconds(100) // Placeholder
                });
            }
        }

        /// <summary>
        /// Records a failed operation
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="timestamp">Timestamp</param>
        public void RecordFailedOperation(VersionOperationType operationType, DateTime timestamp)
        {
            lock (_contextLock)
            {
                _metrics.RecordFailedOperation();
                _historyTracker.RecordOperation(new OperationRecord
                {
                    OperationType = operationType,
                    Success = false,
                    Timestamp = timestamp,
                    ErrorMessage = "Operation failed"
                });
            }
        }

        /// <summary>
        /// Records a version selection
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="timestamp">Timestamp</param>
        public void RecordSelection(VersionOperationType operationType, DateTime timestamp)
        {
            lock (_contextLock)
            {
                _metrics.RecordSelection();
                _historyTracker.RecordSelection(new SelectionRecord
                {
                    Version = Version,
                    OperationType = operationType,
                    Timestamp = timestamp
                });
            }

            LastActivatedAt = timestamp;
        }

        /// <summary>
        /// Updates version information
        /// </summary>
        /// <param name="newVersionInfo">New version information</param>
        public void UpdateVersionInfo(SolidWorksVersionInfo newVersionInfo)
        {
            lock (_contextLock)
            {
                _versionInfo.UpdateFrom(newVersionInfo);
            }
        }

        /// <summary>
        /// Gets current runtime metrics
        /// </summary>
        /// <returns>Current metrics</returns>
        public async Task<VersionRuntimeMetrics> GetMetricsAsync()
        {
            await Task.CompletedTask; // Async for consistency

            lock (_contextLock)
            {
                return _metrics.Clone();
            }
        }

        /// <summary>
        /// Gets health status
        /// </summary>
        /// <returns>Health status</returns>
        public async Task<VersionRuntimeHealth> GetHealthStatusAsync()
        {
            await Task.CompletedTask; // Async for consistency

            lock (_contextLock)
            {
                return new VersionRuntimeHealth
                {
                    Version = Version,
                    IsHealthy = HealthStatus == VersionHealthStatus.Healthy,
                    Status = HealthStatus.ToString(),
                    CurrentLoad = _metrics.CurrentLoad,
                    MemoryUsage = _metrics.MemoryUsage,
                    CpuUsage = _metrics.CpuUsage,
                    SuccessRate = _metrics.SuccessRate,
                    LastOperationTime = _metrics.LastOperationTime,
                    Uptime = DateTime.UtcNow - CreatedAt
                };
            }
        }

        /// <summary>
        /// Performs health check
        /// </summary>
        /// <returns>Health check result</returns>
        public async Task<bool> PerformHealthCheckAsync()
        {
            try
            {
                var isHealthy = await _healthMonitor.PerformHealthCheckAsync();

                lock (_contextLock)
                {
                    HealthStatus = isHealthy ? VersionHealthStatus.Healthy : VersionHealthStatus.Unhealthy;
                }

                return isHealthy;
            }
            catch (Exception ex)
            {
                lock (_contextLock)
                {
                    HealthStatus = VersionHealthStatus.Error;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets current load
        /// </summary>
        /// <returns>Current load (0.0 to 1.0)</returns>
        private async Task<double> GetCurrentLoadAsync()
        {
            var metrics = await GetMetricsAsync();
            return metrics.CurrentLoad;
        }

        /// <summary>
        /// Disposes the runtime context
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the runtime context
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_contextLock)
                    {
                        IsActive = false;
                        HealthStatus = VersionHealthStatus.Disposed;
                        _healthMonitor?.Dispose();
                        _historyTracker?.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Tracks runtime metrics for a version
    /// </summary>
    public class VersionRuntimeMetrics
    {
        private readonly object _metricsLock = new object();

        public long TotalOperations { get; private set; }
        public long SuccessfulOperations { get; private set; }
        public long FailedOperations { get; private set; }
        public TimeSpan AverageOperationTime { get; private set; }
        public TimeSpan PeakOperationTime { get; private set; }
        public long MemoryUsage { get; private set; }
        public long PeakMemoryUsage { get; private set; }
        public int CpuUsage { get; private set; }
        public int PeakCpuUsage { get; private set; }
        public TimeSpan TotalCpuTime { get; private set; }
        public double CurrentLoad { get; private set; }
        public double StabilityScore { get; private set; }
        public double PerformanceScore { get; private set; }
        public DateTime? LastOperationTime { get; private set; }
        public DateTime? LastSelectionTime { get; private set; }
        public DateTime Timestamp { get; private set; }

        public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0;
        public double FailureRate => TotalOperations > 0 ? (double)FailedOperations / TotalOperations : 0;

        public VersionRuntimeMetrics()
        {
            Timestamp = DateTime.UtcNow;
        }

        public void RecordSuccessfulOperation(TimeSpan duration = default)
        {
            lock (_metricsLock)
            {
                TotalOperations++;
                SuccessfulOperations++;
                LastOperationTime = DateTime.UtcNow;

                if (duration != default)
                {
                    UpdateOperationTimeMetrics(duration);
                }

                UpdateLoadAndPerformanceMetrics();
            }
        }

        public void RecordFailedOperation()
        {
            lock (_metricsLock)
            {
                TotalOperations++;
                FailedOperations++;
                LastOperationTime = DateTime.UtcNow;

                UpdateLoadAndPerformanceMetrics();
            }
        }

        public void RecordSelection()
        {
            lock (_metricsLock)
            {
                LastSelectionTime = DateTime.UtcNow;
                StabilityScore = Math.Min(1.0, StabilityScore + 0.01);
            }
        }

        public VersionRuntimeMetrics Clone()
        {
            lock (_metricsLock)
            {
                return new VersionRuntimeMetrics
                {
                    TotalOperations = TotalOperations,
                    SuccessfulOperations = SuccessfulOperations,
                    FailedOperations = FailedOperations,
                    AverageOperationTime = AverageOperationTime,
                    PeakOperationTime = PeakOperationTime,
                    MemoryUsage = MemoryUsage,
                    PeakMemoryUsage = PeakMemoryUsage,
                    CpuUsage = CpuUsage,
                    PeakCpuUsage = PeakCpuUsage,
                    TotalCpuTime = TotalCpuTime,
                    CurrentLoad = CurrentLoad,
                    StabilityScore = StabilityScore,
                    PerformanceScore = PerformanceScore,
                    LastOperationTime = LastOperationTime,
                    LastSelectionTime = LastSelectionTime,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private void UpdateOperationTimeMetrics(TimeSpan duration)
        {
            if (TotalOperations == 1)
            {
                AverageOperationTime = duration;
                PeakOperationTime = duration;
            }
            else
            {
                var totalTicks = AverageOperationTime.Ticks * (TotalOperations - 1) + duration.Ticks;
                AverageOperationTime = TimeSpan.FromTicks(totalTicks / TotalOperations);

                if (duration > PeakOperationTime)
                {
                    PeakOperationTime = duration;
                }
            }
        }

        private void UpdateLoadAndPerformanceMetrics()
        {
            // Simulate load and performance metrics
            var random = new Random();

            // Current load based on recent operations
            CurrentLoad = Math.Min(1.0, TotalOperations / 100.0);

            // Simulate memory usage (in bytes)
            MemoryUsage = (long)(random.NextDouble() * 1024 * 1024 * 1024); // Up to 1GB
            if (MemoryUsage > PeakMemoryUsage)
            {
                PeakMemoryUsage = MemoryUsage;
            }

            // Simulate CPU usage (percentage)
            CpuUsage = (int)(random.NextDouble() * 100);
            if (CpuUsage > PeakCpuUsage)
            {
                PeakCpuUsage = CpuUsage;
            }

            // Update total CPU time
            TotalCpuTime += TimeSpan.FromMilliseconds(random.Next(1, 100));

            // Calculate performance score
            PerformanceScore = CalculatePerformanceScore();
        }

        private double CalculatePerformanceScore()
        {
            if (TotalOperations == 0)
                return 0.5; // Neutral score for no operations

            var successRateScore = SuccessRate;
            var speedScore = AverageOperationTime.TotalMilliseconds < 1000 ? 1.0 : 1000.0 / AverageOperationTime.TotalMilliseconds;
            var resourceScore = (1.0 - (MemoryUsage / (1024.0 * 1024.0 * 1024.0))) * 0.5 + (1.0 - (CpuUsage / 100.0)) * 0.5;

            return (successRateScore + speedScore + resourceScore) / 3;
        }
    }

    /// <summary>
    /// Tracks operation history
    /// </summary>
    internal class OperationHistoryTracker : IDisposable
    {
        private readonly Queue<OperationRecord> _operationHistory;
        private readonly Queue<SelectionRecord> _selectionHistory;
        private readonly object _historyLock = new object();
        private readonly int _maxHistorySize;
        private bool _disposed;

        public OperationHistoryTracker(int maxHistorySize)
        {
            _maxHistorySize = maxHistorySize;
            _operationHistory = new Queue<OperationRecord>();
            _selectionHistory = new Queue<SelectionRecord>();
        }

        public void RecordOperation(OperationRecord record)
        {
            lock (_historyLock)
            {
                _operationHistory.Enqueue(record);

                while (_operationHistory.Count > _maxHistorySize)
                {
                    _operationHistory.Dequeue();
                }
            }
        }

        public void RecordSelection(SelectionRecord record)
        {
            lock (_historyLock)
            {
                _selectionHistory.Enqueue(record);

                while (_selectionHistory.Count > _maxHistorySize)
                {
                    _selectionHistory.Dequeue();
                }
            }
        }

        public List<OperationRecord> GetOperationHistory()
        {
            lock (_historyLock)
            {
                return _operationHistory.ToList();
            }
        }

        public List<SelectionRecord> GetSelectionHistory()
        {
            lock (_historyLock)
            {
                return _selectionHistory.ToList();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_historyLock)
                    {
                        _operationHistory.Clear();
                        _selectionHistory.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Monitors health of a version context
    /// </summary>
    internal class HealthMonitor : IDisposable
    {
        private readonly Random _random;
        private bool _disposed;

        public HealthMonitor()
        {
            _random = new Random();
        }

        public async Task<bool> InitializeAsync()
        {
            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> PerformHealthCheckAsync()
        {
            await Task.CompletedTask;

            // Simulate health check - 95% success rate
            return _random.NextDouble() < 0.95;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }

    #region Supporting Classes

    public class VersionRuntimeHealth
    {
        public string Version { get; set; }
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public double CurrentLoad { get; set; }
        public long MemoryUsage { get; set; }
        public int CpuUsage { get; set; }
        public double SuccessRate { get; set; }
        public DateTime? LastOperationTime { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    
    public class OperationRecord
    {
        public VersionOperationType OperationType { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class SelectionRecord
    {
        public string Version { get; set; }
        public VersionOperationType OperationType { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> SelectionCriteria { get; set; } = new Dictionary<string, object>();
    }

    #endregion
}