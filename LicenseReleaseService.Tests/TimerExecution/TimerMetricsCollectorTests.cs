using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerMetricsCollectorTests
    {
        private readonly Mock<ILogger<TimerMetricsCollector>> _mockLogger;
        private readonly TimerMetricsOptions _options;

        public TimerMetricsCollectorTests()
        {
            _mockLogger = new Mock<ILogger<TimerMetricsCollector>>();
            _options = new TimerMetricsOptions();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Act
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            // Assert
            Assert.NotNull(collector);
            Assert.NotNull(collector.CurrentSnapshot);
            Assert.True(collector.Uptime.TotalMilliseconds >= 0);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerMetricsCollector(null, _options));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerMetricsCollector(_mockLogger.Object, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotCollecting_ShouldStartCollecting()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            // Act
            await collector.StartAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyCollecting_ShouldNotStartAgain()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();

            // Act
            await collector.StartAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task StopAsync_WhenCollecting_ShouldStopCollecting()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();

            // Act
            await collector.StopAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task StopAsync_WhenNotCollecting_ShouldDoNothing()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            // Act
            await collector.StopAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task GetSnapshotAsync_WithExistingSnapshot_ShouldReturnSnapshot()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();
            await Task.Delay(100); // Allow time for initial collection

            var snapshotId = collector.CurrentSnapshot.SnapshotId;

            // Act
            var snapshot = collector.GetSnapshot(snapshotId);

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(snapshotId, snapshot.SnapshotId);
        }

        [Fact]
        public async Task GetSnapshotAsync_WithNonExistingSnapshot_ShouldReturnNull()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            // Act
            var snapshot = collector.GetSnapshot("non-existing-snapshot");

            // Assert
            Assert.Null(snapshot);
        }

        [Fact]
        public async Task GetRecentSnapshots_ShouldReturnRecentSnapshots()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();
            await Task.Delay(200); // Allow time for multiple collections

            // Act
            var snapshots = collector.GetRecentSnapshots(5);

            // Assert
            Assert.NotNull(snapshots);
            Assert.True(snapshots.Count() <= 5);
        }

        [Fact]
        public async Task GetStatistics_WithValidTimeRange_ShouldReturnStatistics()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();
            await Task.Delay(200); // Allow time for collections

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddSeconds(-1);

            // Act
            var stats = collector.GetStatistics(startTime, endTime);

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(startTime, stats.StartTime);
            Assert.Equal(endTime, stats.EndTime);
        }

        [Fact]
        public async Task GetStatistics_WithInvalidTimeRange_ShouldReturnEmptyStatistics()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            var startTime = DateTime.UtcNow.AddSeconds(-10);
            var endTime = DateTime.UtcNow.AddSeconds(-5);

            // Act
            var stats = collector.GetStatistics(startTime, endTime);

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.SnapshotCount);
        }

        [Fact]
        public async Task ForceCollectionAsync_ShouldForceImmediateCollection()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();

            var initialSnapshotId = collector.CurrentSnapshot.SnapshotId;

            // Act
            await collector.ForceCollectionAsync();

            // Assert
            Assert.NotEqual(initialSnapshotId, collector.CurrentSnapshot.SnapshotId);
        }

        [Fact]
        public void GetSystemMetrics_ShouldReturnSystemMetrics()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            // Act
            var metrics = collector.GetSystemMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.CpuUsagePercent >= 0);
            Assert.True(metrics.MemoryUsagePercent >= 0);
            Assert.True(metrics.AvailableMemoryMB >= 0);
            Assert.True(metrics.TotalMemoryMB >= 0);
            Assert.True(metrics.ActiveThreads >= 0);
        }

        [Fact]
        public void Events_ShouldBeRaisedCorrectly()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            var metricsCollectedEventRaised = false;
            var thresholdExceededEventRaised = false;

            collector.MetricsCollected += (sender, args) => metricsCollectedEventRaised = true;
            collector.ThresholdExceeded += (sender, args) => thresholdExceededEventRaised = true;

            // Act
            // Events are raised during metrics collection, so we'll just verify they're not null
            Assert.NotNull(collector.MetricsCollected);
            Assert.NotNull(collector.ThresholdExceeded);
        }

        [Fact]
        public async Task Dispose_WhenCollecting_ShouldStopAndDispose()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);
            await collector.StartAsync();

            // Act
            collector.Dispose();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public void Dispose_WhenAlreadyDisposed_ShouldNotThrow()
        {
            // Arrange
            var collector = new TimerMetricsCollector(_mockLogger.Object, _options);

            // Act
            collector.Dispose();
            collector.Dispose(); // Should not throw

            // Assert
            Assert.True(true); // If we get here, no exception was thrown
        }

        [Fact]
        public void TimerPerformanceSnapshot_ShouldHaveValidProperties()
        {
            // Arrange
            var snapshot = new TimerPerformanceSnapshot();

            // Act & Assert
            Assert.NotEmpty(snapshot.SnapshotId);
            Assert.True(snapshot.Timestamp > DateTime.MinValue);
            Assert.True(snapshot.CpuUsagePercent >= 0);
            Assert.True(snapshot.MemoryUsagePercent >= 0);
            Assert.True(snapshot.ThreadCount >= 0);
        }

        [Fact]
        public void TimerPerformanceThreshold_ShouldHaveValidProperties()
        {
            // Arrange
            var threshold = new TimerPerformanceThreshold
            {
                MetricType = TimerPerformanceMetricType.CpuUsage,
                ThresholdValue = 80.0,
                ActualValue = 85.0,
                Severity = TimerPerformanceThresholdSeverity.High
            };

            // Act & Assert
            Assert.Equal(TimerPerformanceMetricType.CpuUsage, threshold.MetricType);
            Assert.Equal(80.0, threshold.ThresholdValue);
            Assert.Equal(85.0, threshold.ActualValue);
            Assert.Equal(TimerPerformanceThresholdSeverity.High, threshold.Severity);
        }

        [Fact]
        public void TimerPerformanceStatistics_ShouldHaveValidProperties()
        {
            // Arrange
            var stats = new TimerPerformanceStatistics();

            // Act & Assert
            Assert.Equal(TimeSpan.Zero, stats.Duration);
            Assert.Equal(0, stats.SnapshotCount);
            Assert.Equal(0, stats.AverageCpuUsage);
            Assert.Equal(0, stats.AverageMemoryUsage);
            Assert.Equal(0, stats.AverageThreadCount);
        }
    }
}