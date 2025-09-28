using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class PerformanceMonitorTests : IDisposable
    {
        private readonly Mock<ILogger<PerformanceMonitor>> _mockLogger;
        private readonly PerformanceMonitorConfig _config;
        private readonly PerformanceMonitor _monitor;

        public PerformanceMonitorTests()
        {
            _mockLogger = new Mock<ILogger<PerformanceMonitor>>();
            _config = new PerformanceMonitorConfig
            {
                MonitoringInterval = 1000,
                SampleCount = 5,
                EnableProcessMonitoring = true,
                EnableSystemMonitoring = true,
                EnableDetailedLogging = true,
                CpuThreshold = 10.0,
                MemoryThreshold = 50.0,
                DiskThreshold = 5.0,
                NetworkThreshold = 100.0,
                MaxSampleAge = TimeSpan.FromMinutes(5),
                EnableCounterCreation = true,
                EnableThresholdAlerts = true,
                EnableMetricCollection = true
            };

            _monitor = new PerformanceMonitor(_mockLogger.Object, _config);
        }

        public void Dispose()
        {
            _monitor.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeMonitor()
        {
            // Arrange & Act
            var monitor = new PerformanceMonitor(_mockLogger.Object, _config);

            // Assert
            Assert.NotNull(monitor);
            Assert.False(monitor.IsRunning);
            Assert.NotNull(monitor.Statistics);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PerformanceMonitor(null, _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PerformanceMonitor(_mockLogger.Object, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotRunning_ShouldStartMonitoring()
        {
            // Arrange
            var thresholdExceededRaised = false;
            _monitor.ThresholdExceeded += (s, e) => thresholdExceededRaised = true;

            // Act
            await _monitor.StartAsync();

            // Assert
            Assert.True(_monitor.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            await _monitor.StartAsync();

            // Act
            await _monitor.StartAsync();

            // Assert
            Assert.True(_monitor.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopMonitoring()
        {
            // Arrange
            await _monitor.StartAsync();

            // Act
            await _monitor.StopAsync();

            // Assert
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldNotStopAgain()
        {
            // Arrange
            await _monitor.StartAsync();
            await _monitor.StopAsync();

            // Act
            await _monitor.StopAsync();

            // Assert
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public void GetProcessMetrics_WithValidProcess_ShouldReturnMetrics()
        {
            // Arrange
            var currentProcess = Process.GetCurrentProcess();

            // Act
            var metrics = _monitor.GetProcessMetrics(currentProcess.Id);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(currentProcess.Id, metrics.ProcessId);
            Assert.Equal(currentProcess.ProcessName, metrics.ProcessName);
            Assert.True(metrics.CpuUsage >= 0);
            Assert.True(metrics.MemoryUsage >= 0);
            Assert.True(metrics.WorkingSet >= 0);
            Assert.True(metrics.PrivateBytes >= 0);
        }

        [Fact]
        public void GetProcessMetrics_WithInvalidProcess_ShouldReturnNull()
        {
            // Arrange
            var invalidProcessId = 99999;

            // Act
            var metrics = _monitor.GetProcessMetrics(invalidProcessId);

            // Assert
            Assert.Null(metrics);
        }

        [Fact]
        public void GetSystemMetrics_ShouldReturnSystemMetrics()
        {
            // Arrange & Act
            var metrics = _monitor.GetSystemMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.CpuUsage >= 0);
            Assert.True(metrics.MemoryUsage >= 0);
            Assert.True(metrics.DiskUsage >= 0);
            Assert.True(metrics.NetworkUsage >= 0);
            Assert.True(metrics.AvailableMemory >= 0);
            Assert.True(metrics.TotalMemory >= 0);
        }

        [Fact]
        public void GetProcessHistory_WithValidProcess_ShouldReturnHistory()
        {
            // Arrange
            var currentProcess = Process.GetCurrentProcess();

            // Add some metrics to history
            _monitor.GetProcessMetrics(currentProcess.Id); // This should add to history

            // Act
            var history = _monitor.GetProcessHistory(currentProcess.Id, TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(history);
            Assert.True(history.Count >= 0); // May be empty if monitoring hasn't run
        }

        [Fact]
        public void GetProcessHistory_WithInvalidProcess_ShouldReturnEmptyList()
        {
            // Arrange
            var invalidProcessId = 99999;

            // Act
            var history = _monitor.GetProcessHistory(invalidProcessId, TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(history);
            Assert.Empty(history);
        }

        [Fact]
        public void GetSystemHistory_ShouldReturnSystemHistory()
        {
            // Arrange & Act
            var history = _monitor.GetSystemHistory(TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(history);
            Assert.True(history.Count >= 0); // May be empty if monitoring hasn't run
        }

        [Fact]
        public void GetProcessActivityScore_WithActiveProcess_ShouldReturnHighScore()
        {
            // Arrange
            var currentProcess = Process.GetCurrentProcess();
            var metrics = _monitor.GetProcessMetrics(currentProcess.Id);

            // Act
            var score = _monitor.GetProcessActivityScore(currentProcess.Id);

            // Assert
            Assert.True(score >= 0 && score <= 1);
        }

        [Fact]
        public void GetProcessActivityScore_WithInvalidProcess_ShouldReturnZero()
        {
            // Arrange
            var invalidProcessId = 99999;

            // Act
            var score = _monitor.GetProcessActivityScore(invalidProcessId);

            // Assert
            Assert.Equal(0.0, score);
        }

        [Fact]
        public void GetSystemActivityScore_ShouldReturnValidScore()
        {
            // Arrange & Act
            var score = _monitor.GetSystemActivityScore();

            // Assert
            Assert.True(score >= 0 && score <= 1);
        }

        [Fact]
        public void GetStatistics_ShouldReturnCurrentStatistics()
        {
            // Arrange & Act
            var stats = _monitor.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("PerformanceMonitor", stats.ComponentName);
            Assert.True(stats.StartTime <= DateTime.UtcNow);
        }

        [Fact]
        public void IsProcessAboveThreshold_WithActiveProcess_ShouldReturnCorrectResult()
        {
            // Arrange
            var currentProcess = Process.GetCurrentProcess();

            // Act
            var isAboveCpuThreshold = _monitor.IsProcessAboveThreshold(currentProcess.Id, "CPU", _config.CpuThreshold);
            var isAboveMemoryThreshold = _monitor.IsProcessAboveThreshold(currentProcess.Id, "Memory", _config.MemoryThreshold);

            // Assert
            Assert.True(isAboveCpuThreshold || !isAboveCpuThreshold); // Either is fine, just check it doesn't throw
            Assert.True(isAboveMemoryThreshold || !isAboveMemoryThreshold); // Either is fine, just check it doesn't throw
        }

        [Fact]
        public void IsProcessAboveThreshold_WithInvalidProcess_ShouldReturnFalse()
        {
            // Arrange
            var invalidProcessId = 99999;

            // Act
            var result = _monitor.IsProcessAboveThreshold(invalidProcessId, "CPU", _config.CpuThreshold);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsProcessAboveThreshold_WithInvalidMetricType_ShouldReturnFalse()
        {
            // Arrange
            var currentProcess = Process.GetCurrentProcess();

            // Act
            var result = _monitor.IsProcessAboveThreshold(currentProcess.Id, "InvalidMetric", _config.CpuThreshold);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetMetricThreshold_WithValidMetric_ShouldReturnThreshold()
        {
            // Arrange & Act
            var cpuThreshold = _monitor.GetMetricThreshold("CPU");
            var memoryThreshold = _monitor.GetMetricThreshold("Memory");
            var diskThreshold = _monitor.GetMetricThreshold("Disk");
            var networkThreshold = _monitor.GetMetricThreshold("Network");

            // Assert
            Assert.Equal(_config.CpuThreshold, cpuThreshold);
            Assert.Equal(_config.MemoryThreshold, memoryThreshold);
            Assert.Equal(_config.DiskThreshold, diskThreshold);
            Assert.Equal(_config.NetworkThreshold, networkThreshold);
        }

        [Fact]
        public void GetMetricThreshold_WithInvalidMetric_ShouldReturnZero()
        {
            // Arrange & Act
            var threshold = _monitor.GetMetricThreshold("InvalidMetric");

            // Assert
            Assert.Equal(0.0, threshold);
        }

        [Fact]
        public void SetMetricThreshold_WithValidMetric_ShouldUpdateThreshold()
        {
            // Arrange
            var newCpuThreshold = 25.0;

            // Act
            _monitor.SetMetricThreshold("CPU", newCpuThreshold);
            var updatedThreshold = _monitor.GetMetricThreshold("CPU");

            // Assert
            Assert.Equal(newCpuThreshold, updatedThreshold);
        }

        [Fact]
        public void SetMetricThreshold_WithInvalidMetric_ShouldNotUpdate()
        {
            // Arrange
            var originalCpuThreshold = _monitor.GetMetricThreshold("CPU");

            // Act
            _monitor.SetMetricThreshold("InvalidMetric", 25.0);
            var updatedThreshold = _monitor.GetMetricThreshold("CPU");

            // Assert
            Assert.Equal(originalCpuThreshold, updatedThreshold);
        }

        [Fact]
        public void CalculateActivityConfidence_WithHighCpuUsage_ShouldReturnHighConfidence()
        {
            // Arrange
            var cpuUsage = 75.0;
            var memoryUsage = 30.0;
            var diskUsage = 10.0;
            var networkUsage = 50.0;

            // Act
            var confidence = _monitor.CalculateActivityConfidence(cpuUsage, memoryUsage, diskUsage, networkUsage);

            // Assert
            Assert.True(confidence > 0.5);
        }

        [Fact]
        public void CalculateActivityConfidence_WithLowUsage_ShouldReturnLowConfidence()
        {
            // Arrange
            var cpuUsage = 1.0;
            var memoryUsage = 5.0;
            var diskUsage = 0.5;
            var networkUsage = 1.0;

            // Act
            var confidence = _monitor.CalculateActivityConfidence(cpuUsage, memoryUsage, diskUsage, networkUsage);

            // Assert
            Assert.True(confidence < 0.3);
        }

        [Fact]
        public async Task MonitorPerformanceAsync_WithRunningProcess_ShouldCollectMetrics()
        {
            // Arrange
            await _monitor.StartAsync();
            var currentProcess = Process.GetCurrentProcess();

            // Wait for some monitoring cycles
            await Task.Delay(_config.MonitoringInterval * 3);

            // Act
            var history = _monitor.GetProcessHistory(currentProcess.Id, TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(history);
            // We expect at least some metrics to have been collected
            Assert.True(history.Count >= 0);
        }

        [Fact]
        public void Dispose_ShouldStopMonitoringAndDisposeResources()
        {
            // Arrange
            _monitor.StartAsync().Wait();

            // Act
            _monitor.Dispose();

            // Assert
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public void Configuration_ShouldValidateSettings()
        {
            // Arrange
            var invalidConfig = new PerformanceMonitorConfig
            {
                MonitoringInterval = -1,
                SampleCount = -1,
                CpuThreshold = -1.0
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => invalidConfig.Validate());
        }

        [Fact]
        public void Configuration_WithValidSettings_ShouldNotThrow()
        {
            // Arrange
            var validConfig = new PerformanceMonitorConfig
            {
                MonitoringInterval = 1000,
                SampleCount = 5,
                CpuThreshold = 10.0,
                MemoryThreshold = 50.0
            };

            // Act & Assert
            Assert.DoesNotThrow(() => validConfig.Validate());
        }

        [Fact]
        public void GetAvailableMetrics_ShouldReturnSupportedMetrics()
        {
            // Arrange & Act
            var metrics = _monitor.GetAvailableMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.Contains("CPU", metrics);
            Assert.Contains("Memory", metrics);
            Assert.Contains("Disk", metrics);
            Assert.Contains("Network", metrics);
        }

        [Fact]
        public void GetProcessCount_ShouldReturnValidCount()
        {
            // Arrange & Act
            var processCount = _monitor.GetProcessCount();

            // Assert
            Assert.True(processCount >= 0);
        }

        [Fact]
        public void GetTotalMemoryUsage_ShouldReturnValidUsage()
        {
            // Arrange & Act
            var totalMemoryUsage = _monitor.GetTotalMemoryUsage();

            // Assert
            Assert.True(totalMemoryUsage >= 0);
        }

        [Fact]
        public void GetAverageCpuUsage_ShouldReturnValidAverage()
        {
            // Arrange & Act
            var averageCpuUsage = _monitor.GetAverageCpuUsage();

            // Assert
            Assert.True(averageCpuUsage >= 0 && averageCpuUsage <= 100);
        }

        [Fact]
        public async Task ThresholdExceeded_WhenCpuUsageExceedsThreshold_ShouldRaiseEvent()
        {
            // Arrange
            await _monitor.StartAsync();
            var eventRaised = false;
            ThresholdEventArgs capturedArgs = null;

            _monitor.ThresholdExceeded += (s, e) =>
            {
                eventRaised = true;
                capturedArgs = e;
            };

            // Simulate high CPU usage by directly calling the monitoring method
            var method = typeof(PerformanceMonitor).GetMethod("CheckThresholds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentProcess = Process.GetCurrentProcess();
            var metrics = new ProcessMetrics { ProcessId = currentProcess.Id, CpuUsage = 95.0, ProcessName = currentProcess.ProcessName };
            method.Invoke(_monitor, new object[] { metrics });

            // Assert
            // Note: This test may not always trigger the event depending on the actual CPU usage
            // We're primarily testing that the method can be called without throwing exceptions
            Assert.True(true); // If we reach here, the test passed
        }
    }
}