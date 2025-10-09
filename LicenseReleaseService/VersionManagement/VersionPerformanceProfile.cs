using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Manages performance profile for a specific SolidWorks version including metrics,
    /// optimization history, and performance targets
    /// </summary>
    public class VersionPerformanceProfile : IDisposable
    {
        private readonly string _version;
        private readonly PerformanceOptimizationConfiguration _configuration;
        private readonly PerformanceMetricsCollector _metricsCollector;
        private readonly OptimizationHistoryTracker _historyTracker;
        private readonly PerformanceTargetManager _targetManager;
        private readonly Timer _metricsTimer;
        private bool _disposed;

        public string Version => _version;
        public PerformanceTargetLevel TargetPerformanceLevel => _targetManager.CurrentTargets;
        public ResourceLimits ResourceLimits => _targetManager.ResourceLimits;

        /// <summary>
        /// Initializes a new instance of the VersionPerformanceProfile class
        /// </summary>
        /// <param name="version">Version string</param>
        /// <param name="configuration">Performance optimization configuration</param>
        public VersionPerformanceProfile(string version, PerformanceOptimizationConfiguration configuration)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _metricsCollector = new PerformanceMetricsCollector();
            _historyTracker = new OptimizationHistoryTracker(configuration.MaxOptimizationHistory);
            _targetManager = new PerformanceTargetManager(version);

            // Initialize metrics collection timer
            _metricsTimer = new Timer(MetricsCollectionCallback, null,
                TimeSpan.FromSeconds(configuration.MetricsCollectionIntervalSeconds),
                TimeSpan.FromSeconds(configuration.MetricsCollectionIntervalSeconds));
        }

        /// <summary>
        /// Initializes the performance profile
        /// </summary>
        /// <returns>Initialization result</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                await _targetManager.InitializeTargetsAsync();
                await _metricsCollector.InitializeAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log error but don't fail initialization
                Console.WriteLine($"Error initializing performance profile for version {_version}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Records an optimization in the history
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="optimizations">Applied optimizations</param>
        /// <param name="timestamp">Timestamp</param>
        public void RecordOptimization(
            VersionOperationType operationType,
            List<AppliedOptimization> optimizations,
            DateTime timestamp)
        {
            var optimizationRecord = new OptimizationRecord
            {
                Version = _version,
                OperationType = operationType,
                Optimizations = optimizations,
                Timestamp = timestamp,
                Success = optimizations.Any(o => o.Success)
            };

            _historyTracker.RecordOptimization(optimizationRecord);
        }

        /// <summary>
        /// Records a context optimization
        /// </summary>
        /// <param name="optimizations">Applied optimizations</param>
        /// <param name="timestamp">Timestamp</param>
        public void RecordContextOptimization(
            List<AppliedOptimization> optimizations,
            DateTime timestamp)
        {
            var optimizationRecord = new OptimizationRecord
            {
                Version = _version,
                Optimizations = optimizations,
                Timestamp = timestamp,
                Success = optimizations.Any(o => o.Success),
                IsContextOptimization = true
            };

            _historyTracker.RecordOptimization(optimizationRecord);
        }

        /// <summary>
        /// Records a manual optimization
        /// </summary>
        /// <param name="optimizationType">Type of optimization</param>
        /// <param name="optimization">Applied optimization</param>
        /// <param name="timestamp">Timestamp</param>
        public void RecordManualOptimization(
            OptimizationType optimizationType,
            AppliedOptimization optimization,
            DateTime timestamp)
        {
            var optimizationRecord = new OptimizationRecord
            {
                Version = _version,
                OptimizationType = optimizationType,
                Optimizations = new List<AppliedOptimization> { optimization },
                Timestamp = timestamp,
                Success = optimization.Success,
                IsManualOptimization = true
            };

            _historyTracker.RecordOptimization(optimizationRecord);
        }

        /// <summary>
        /// Gets current performance metrics
        /// </summary>
        /// <returns>Current metrics</returns>
        public async Task<VersionRuntimeMetrics> GetCurrentMetricsAsync()
        {
            return await _metricsCollector.GetCurrentMetricsAsync();
        }

        /// <summary>
        /// Gets performance statistics
        /// </summary>
        /// <returns>Performance statistics</returns>
        public async Task<VersionPerformanceStatistics> GetStatisticsAsync()
        {
            var currentMetrics = await GetCurrentMetricsAsync();
            var optimizationHistory = _historyTracker.GetOptimizationHistory();

            return new VersionPerformanceStatistics
            {
                Version = _version,
                TotalOptimizations = optimizationHistory.Count,
                SuccessfulOptimizations = optimizationHistory.Count(o => o.Success),
                FailedOptimizations = optimizationHistory.Count(o => !o.Success),
                AverageOptimizationTime = CalculateAverageOptimizationTime(optimizationHistory),
                PerformanceImprovementScore = CalculatePerformanceImprovementScore(currentMetrics, optimizationHistory),
                CurrentMetrics = currentMetrics,
                LastOptimizationTime = optimizationHistory.LastOrDefault()?.Timestamp,
                OptimizationSuccessRate = optimizationHistory.Any() ?
                    (double)optimizationHistory.Count(o => o.Success) / optimizationHistory.Count * 100 : 0
            };
        }

        /// <summary>
        /// Analyzes performance trends
        /// </summary>
        /// <returns>Performance trend analysis</returns>
        public PerformanceTrendAnalysis AnalyzePerformanceTrends()
        {
            var metricsHistory = _metricsCollector.GetMetricsHistory();
            return AnalyzePerformanceTrendsInternal(metricsHistory);
        }

        /// <summary>
        /// Analyzes resource usage trends
        /// </summary>
        /// <returns>Resource usage trend analysis</returns>
        public ResourceUsageTrendAnalysis AnalyzeResourceUsageTrends()
        {
            var metricsHistory = _metricsCollector.GetMetricsHistory();
            return AnalyzeResourceTrendsInternal(metricsHistory);
        }

        /// <summary>
        /// Determines if optimization is needed
        /// </summary>
        /// <returns>True if optimization is needed</returns>
        public bool NeedsOptimization()
        {
            var currentMetrics = _metricsCollector.GetCurrentMetricsSync();
            return CheckOptimizationNeeds(currentMetrics);
        }

        /// <summary>
        /// Collects metrics
        /// </summary>
        /// <returns>Collection result</returns>
        public async Task<bool> CollectMetricsAsync()
        {
            try
            {
                await _metricsCollector.CollectMetricsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting metrics for version {_version}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disposes the performance profile
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the performance profile
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _metricsTimer?.Dispose();
                    _metricsCollector?.Dispose();
                    _historyTracker?.Dispose();
                    _targetManager?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Creates a default performance profile
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <returns>Default performance profile</returns>
        public static VersionPerformanceProfile CreateDefault(
            PerformanceOptimizationConfiguration configuration)
        {
            return new VersionPerformanceProfile("default", configuration);
        }

        #region Private Methods

        /// <summary>
        /// Metrics collection timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private async void MetricsCollectionCallback(object state)
        {
            try
            {
                await CollectMetricsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in metrics collection callback for version {_version}: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates average optimization time
        /// </summary>
        /// <param name="optimizationHistory">Optimization history</param>
        /// <returns>Average optimization time</returns>
        private TimeSpan CalculateAverageOptimizationTime(List<OptimizationRecord> optimizationHistory)
        {
            var successfulOptimizations = optimizationHistory
                .Where(o => o.Success && o.Optimizations.Any())
                .ToList();

            if (!successfulOptimizations.Any())
                return TimeSpan.Zero;

            var totalDuration = successfulOptimizations
                .Sum(o => o.Optimizations.Sum(opt => opt.Duration.Ticks));

            return TimeSpan.FromTicks(totalDuration / successfulOptimizations.Count);
        }

        /// <summary>
        /// Calculates performance improvement score
        /// </summary>
        /// <param name="currentMetrics">Current metrics</param>
        /// <param name="optimizationHistory">Optimization history</param>
        /// <returns>Performance improvement score</returns>
        private double CalculatePerformanceImprovementScore(
            VersionRuntimeMetrics currentMetrics,
            List<OptimizationRecord> optimizationHistory)
        {
            // This is a simplified calculation
            // In a real implementation, this would compare metrics before and after optimizations
            var successfulOptimizations = optimizationHistory.Count(o => o.Success);
            var totalOptimizations = optimizationHistory.Count;

            if (totalOptimizations == 0)
                return 0;

            var baseScore = (double)successfulOptimizations / totalOptimizations;

            // Adjust based on current performance metrics
            var performanceScore = CalculatePerformanceScore(currentMetrics);
            var reliabilityScore = 1.0 - currentMetrics.FailureRate;

            return (baseScore + performanceScore + reliabilityScore) / 3;
        }

        /// <summary>
        /// Calculates performance score based on metrics
        /// </summary>
        /// <param name="metrics">Current metrics</param>
        /// <returns>Performance score</returns>
        private double CalculatePerformanceScore(VersionRuntimeMetrics metrics)
        {
            if (metrics.TotalOperations == 0)
                return 0;

            // Calculate score based on operation time, success rate, and resource usage
            var timeScore = CalculateTimeScore(metrics);
            var resourceScore = CalculateResourceScore(metrics);

            return (timeScore + resourceScore) / 2;
        }

        /// <summary>
        /// Calculates time-based performance score
        /// </summary>
        /// <param name="metrics">Current metrics</param>
        /// <returns>Time score</returns>
        private double CalculateTimeScore(VersionRuntimeMetrics metrics)
        {
            if (metrics.AverageOperationTime == TimeSpan.Zero)
                return 1.0;

            var targetTime = TargetPerformanceLevel.TargetOperationTime;
            var maxAcceptableTime = TargetPerformanceLevel.MaxAcceptableTime;

            if (metrics.AverageOperationTime <= targetTime)
                return 1.0;

            if (metrics.AverageOperationTime >= maxAcceptableTime)
                return 0.0;

            var ratio = (metrics.AverageOperationTime - targetTime).TotalMilliseconds /
                       (maxAcceptableTime - targetTime).TotalMilliseconds;

            return 1.0 - ratio;
        }

        /// <summary>
        /// Calculates resource-based performance score
        /// </summary>
        /// <param name="metrics">Current metrics</param>
        /// <returns>Resource score</returns>
        private double CalculateResourceScore(VersionRuntimeMetrics metrics)
        {
            var memoryScore = CalculateMemoryScore(metrics);
            var cpuScore = CalculateCpuScore(metrics);

            return (memoryScore + cpuScore) / 2;
        }

        /// <summary>
        /// Calculates memory score
        /// </summary>
        /// <param name="metrics">Current metrics</param>
        /// <returns>Memory score</returns>
        private double CalculateMemoryScore(VersionRuntimeMetrics metrics)
        {
            if (ResourceLimits.MaxMemory == 0)
                return 1.0;

            var ratio = (double)metrics.MemoryUsage / ResourceLimits.MaxMemory;
            return Math.Max(0, 1.0 - ratio);
        }

        /// <summary>
        /// Calculates CPU score
        /// </summary>
        /// <param name="metrics">Current metrics</param>
        /// <returns>CPU score</returns>
        private double CalculateCpuScore(VersionRuntimeMetrics metrics)
        {
            if (ResourceLimits.MaxCpu == 0)
                return 1.0;

            var ratio = (double)metrics.CpuUsage / ResourceLimits.MaxCpu;
            return Math.Max(0, 1.0 - ratio);
        }

        /// <summary>
        /// Analyzes performance trends
        /// </summary>
        /// <param name="metricsHistory">Metrics history</param>
        /// <returns>Performance trend analysis</returns>
        private PerformanceTrendAnalysis AnalyzePerformanceTrendsInternal(
            List<VersionRuntimeMetrics> metricsHistory)
        {
            var analysis = new PerformanceTrendAnalysis();

            if (metricsHistory.Count < 2)
                return analysis;

            // Calculate trends
            var recentMetrics = metricsHistory.TakeLast(10).ToList();
            var olderMetrics = metricsHistory.Take(metricsHistory.Count - 10).ToList();

            if (recentMetrics.Any() && olderMetrics.Any())
            {
                var recentAverageTime = recentMetrics.Average(m => m.AverageOperationTime.TotalMilliseconds);
                var olderAverageTime = olderMetrics.Average(m => m.AverageOperationTime.TotalMilliseconds);

                analysis.IsPerformanceDegrading = recentAverageTime > olderAverageTime * 1.1; // 10% degradation threshold
                analysis.PerformanceChangePercentage = ((recentAverageTime - olderAverageTime) / olderAverageTime) * 100;
            }

            return analysis;
        }

        /// <summary>
        /// Analyzes resource trends
        /// </summary>
        /// <param name="metricsHistory">Metrics history</param>
        /// <returns>Resource trend analysis</returns>
        private ResourceUsageTrendAnalysis AnalyzeResourceTrendsInternal(
            List<VersionRuntimeMetrics> metricsHistory)
        {
            var analysis = new ResourceUsageTrendAnalysis();

            if (metricsHistory.Count < 2)
                return analysis;

            // Calculate memory trends
            var recentMemory = metricsHistory.TakeLast(10).Average(m => m.MemoryUsage);
            var olderMemory = metricsHistory.Take(metricsHistory.Count - 10).Average(m => m.MemoryUsage);

            analysis.IsMemoryUsageIncreasing = recentMemory > olderMemory * 1.1; // 10% increase threshold
            analysis.MemoryChangePercentage = ((recentMemory - olderMemory) / olderMemory) * 100;

            // Calculate CPU trends
            var recentCpu = metricsHistory.TakeLast(10).Average(m => m.CpuUsage);
            var olderCpu = metricsHistory.Take(metricsHistory.Count - 10).Average(m => m.CpuUsage);

            analysis.IsCpuUsageIncreasing = recentCpu > olderCpu * 1.1; // 10% increase threshold
            analysis.CpuChangePercentage = ((recentCpu - olderCpu) / olderCpu) * 100;

            return analysis;
        }

        /// <summary>
        /// Checks if optimization is needed
        /// </summary>
        /// <param name="currentMetrics">Current metrics</param>
        /// <returns>True if optimization is needed</returns>
        private bool CheckOptimizationNeeds(VersionRuntimeMetrics currentMetrics)
        {
            // Check if current metrics exceed targets
            if (currentMetrics.AverageOperationTime > TargetPerformanceLevel.MaxAcceptableTime)
                return true;

            if (currentMetrics.FailureRate > TargetPerformanceLevel.MaxFailureRate)
                return true;

            if (currentMetrics.MemoryUsage > ResourceLimits.MaxMemory * 0.9)
                return true;

            if (currentMetrics.CpuUsage > ResourceLimits.MaxCpu * 0.9)
                return true;

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Manages performance targets for a version
    /// </summary>
    internal class PerformanceTargetManager : IDisposable
    {
        private readonly string _version;
        private PerformanceTargetLevel _currentTargets;
        private ResourceLimits _resourceLimits;
        private bool _disposed;

        public PerformanceTargetLevel CurrentTargets => _currentTargets;
        public ResourceLimits ResourceLimits => _resourceLimits;

        public PerformanceTargetManager(string version)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _currentTargets = GetDefaultTargets(version);
            _resourceLimits = GetDefaultResourceLimits(version);
        }

        public async Task<bool> InitializeTargetsAsync()
        {
            try
            {
                // In a real implementation, this would load targets from configuration
                // or calculate them based on version characteristics
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing targets for version {_version}: {ex.Message}");
                return false;
            }
        }

        private PerformanceTargetLevel GetDefaultTargets(string version)
        {
            // Set different targets based on version
            if (int.TryParse(version, out int versionYear))
            {
                if (versionYear >= 2024)
                {
                    return new PerformanceTargetLevel
                    {
                        TargetOperationTime = TimeSpan.FromMilliseconds(500),
                        MaxAcceptableTime = TimeSpan.FromMilliseconds(2000),
                        MaxFailureRate = 0.02, // 2%
                        MinSuccessRate = 0.98  // 98%
                    };
                }
                else if (versionYear >= 2022)
                {
                    return new PerformanceTargetLevel
                    {
                        TargetOperationTime = TimeSpan.FromMilliseconds(800),
                        MaxAcceptableTime = TimeSpan.FromMilliseconds(3000),
                        MaxFailureRate = 0.03, // 3%
                        MinSuccessRate = 0.97  // 97%
                    };
                }
            }

            // Default targets for older versions
            return new PerformanceTargetLevel
            {
                TargetOperationTime = TimeSpan.FromMilliseconds(1000),
                MaxAcceptableTime = TimeSpan.FromMilliseconds(5000),
                MaxFailureRate = 0.05, // 5%
                MinSuccessRate = 0.95  // 95%
            };
        }

        private ResourceLimits GetDefaultResourceLimits(string version)
        {
            // Set different resource limits based on version
            if (int.TryParse(version, out int versionYear))
            {
                if (versionYear >= 2024)
                {
                    return new ResourceLimits
                    {
                        MaxMemory = 2L * 1024 * 1024 * 1024, // 2GB
                        MaxCpu = 75, // 75%
                        MaxThreads = 50,
                        MaxConnections = 100
                    };
                }
                else if (versionYear >= 2022)
                {
                    return new ResourceLimits
                    {
                        MaxMemory = 1536 * 1024 * 1024, // 1.5GB
                        MaxCpu = 60, // 60%
                        MaxThreads = 40,
                        MaxConnections = 80
                    };
                }
            }

            // Default limits for older versions
            return new ResourceLimits
            {
                MaxMemory = 1024 * 1024 * 1024, // 1GB
                MaxCpu = 50, // 50%
                MaxThreads = 30,
                MaxConnections = 50
            };
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

    /// <summary>
    /// Collects and manages performance metrics
    /// </summary>
    internal class PerformanceMetricsCollector : IDisposable
    {
        private readonly Queue<VersionRuntimeMetrics> _metricsHistory;
        private readonly object _historyLock = new object();
        private readonly Random _random;
        private bool _disposed;

        public PerformanceMetricsCollector()
        {
            _metricsHistory = new Queue<VersionRuntimeMetrics>();
            _random = new Random();
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing metrics collector: {ex.Message}");
                return false;
            }
        }

        public async Task<VersionRuntimeMetrics> GetCurrentMetricsAsync()
        {
            await Task.CompletedTask; // Simulate async operation

            // In a real implementation, this would collect actual metrics
            // For now, we'll generate simulated metrics
            return GenerateSimulatedMetrics();
        }

        public VersionRuntimeMetrics GetCurrentMetricsSync()
        {
            return GenerateSimulatedMetrics();
        }

        public async Task<bool> CollectMetricsAsync()
        {
            try
            {
                var metrics = await GetCurrentMetricsAsync();

                lock (_historyLock)
                {
                    _metricsHistory.Enqueue(metrics);

                    // Keep only the last 100 metrics
                    while (_metricsHistory.Count > 100)
                    {
                        _metricsHistory.Dequeue();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting metrics: {ex.Message}");
                return false;
            }
        }

        public List<VersionRuntimeMetrics> GetMetricsHistory()
        {
            lock (_historyLock)
            {
                return _metricsHistory.ToList();
            }
        }

        private VersionRuntimeMetrics GenerateSimulatedMetrics()
        {
            var totalOperations = _random.Next(100, 1000);
            var successfulOperations = (int)(_random.NextDouble() * 900 + 50);
            var failedOperations = (int)(_random.NextDouble() * 50);
            var averageOperationTime = TimeSpan.FromMilliseconds(_random.Next(100, 2000));
            var peakOperationTime = TimeSpan.FromMilliseconds(_random.Next(500, 5000));
            var memoryUsage = (long)(_random.NextDouble() * 1024 * 1024 * 1024); // Up to 1GB
            var peakMemoryUsage = (long)(_random.NextDouble() * 2 * 1024 * 1024 * 1024); // Up to 2GB
            var cpuUsage = (int)(_random.NextDouble() * 100);
            var peakCpuUsage = (int)(_random.NextDouble() * 100);
            var totalCpuTime = TimeSpan.FromMinutes(_random.Next(1, 60));
            var currentLoad = _random.NextDouble();
            var stabilityScore = _random.NextDouble();
            var performanceScore = _random.NextDouble();

            return new VersionRuntimeMetrics(
                totalOperations: totalOperations,
                successfulOperations: successfulOperations,
                failedOperations: failedOperations,
                averageOperationTime: averageOperationTime,
                peakOperationTime: peakOperationTime,
                memoryUsage: memoryUsage,
                peakMemoryUsage: peakMemoryUsage,
                cpuUsage: cpuUsage,
                peakCpuUsage: peakCpuUsage,
                totalCpuTime: totalCpuTime,
                currentLoad: currentLoad,
                stabilityScore: stabilityScore,
                performanceScore: performanceScore,
                timestamp: DateTime.UtcNow
            );
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
                        _metricsHistory.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Tracks optimization history
    /// </summary>
    internal class OptimizationHistoryTracker : IDisposable
    {
        private readonly Queue<OptimizationRecord> _optimizationHistory;
        private readonly object _historyLock = new object();
        private readonly int _maxHistorySize;
        private bool _disposed;

        public OptimizationHistoryTracker(int maxHistorySize)
        {
            _maxHistorySize = maxHistorySize;
            _optimizationHistory = new Queue<OptimizationRecord>();
        }

        public void RecordOptimization(OptimizationRecord record)
        {
            lock (_historyLock)
            {
                _optimizationHistory.Enqueue(record);

                // Keep only the most recent records
                while (_optimizationHistory.Count > _maxHistorySize)
                {
                    _optimizationHistory.Dequeue();
                }
            }
        }

        public List<OptimizationRecord> GetOptimizationHistory()
        {
            lock (_historyLock)
            {
                return _optimizationHistory.ToList();
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
                        _optimizationHistory.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }

    #region Supporting Classes

    public class PerformanceTargetLevel
    {
        public TimeSpan TargetOperationTime { get; set; }
        public TimeSpan MaxAcceptableTime { get; set; }
        public double MaxFailureRate { get; set; }
        public double MinSuccessRate { get; set; }
    }

    public class ResourceLimits
    {
        public long MaxMemory { get; set; }
        public int MaxCpu { get; set; }
        public int MaxThreads { get; set; }
        public int MaxConnections { get; set; }
    }

    public class OptimizationRecord
    {
        public string Version { get; set; }
        public VersionOperationType? OperationType { get; set; }
        public OptimizationType? OptimizationType { get; set; }
        public List<AppliedOptimization> Optimizations { get; set; } = new List<AppliedOptimization>();
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public bool IsContextOptimization { get; set; }
        public bool IsManualOptimization { get; set; }
    }

    public class PerformanceTrendAnalysis
    {
        public bool IsPerformanceDegrading { get; set; }
        public double PerformanceChangePercentage { get; set; }
        public bool IsPerformanceImproving { get; set; }
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class ResourceUsageTrendAnalysis
    {
        public bool IsMemoryUsageIncreasing { get; set; }
        public double MemoryChangePercentage { get; set; }
        public bool IsCpuUsageIncreasing { get; set; }
        public double CpuChangePercentage { get; set; }
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class VersionPerformanceStatistics
    {
        public string Version { get; set; }
        public long TotalOptimizations { get; set; }
        public long SuccessfulOptimizations { get; set; }
        public long FailedOptimizations { get; set; }
        public TimeSpan AverageOptimizationTime { get; set; }
        public double PerformanceImprovementScore { get; set; }
        public VersionRuntimeMetrics CurrentMetrics { get; set; }
        public DateTime? LastOptimizationTime { get; set; }
        public double OptimizationSuccessRate { get; set; }
    }

    #endregion
}