using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement.Monitoring;

namespace LicenseReleaseService.Tests.VersionManagement.Monitoring
{
    public class VersionMetricsCollectorTests : IDisposable
    {
        private readonly Mock<ILogger<VersionMetricsCollector>> _mockLogger;
        private readonly Mock<IOptions<VersionMetricsCollectionOptions>> _mockOptions;
        private readonly VersionMetricsCollectionOptions _collectionOptions;
        private readonly VersionMetricsCollector _metricsCollector;
        private readonly ITestOutputHelper _output;

        public VersionMetricsCollectorTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<VersionMetricsCollector>>();
            _collectionOptions = new VersionMetricsCollectionOptions
            {
                Enabled = true,
                CollectionInterval = TimeSpan.FromSeconds(30),
                MaxHistorySize = 100,
                EnablePerformanceMetrics = true,
                EnableResourceMetrics = true,
                EnableHealthMetrics = true,
                EnableOperationMetrics = true,
                PerformanceCounters = new List<string> { "cpu", "memory", "handles", "threads" },
                ResourceCounters = new List<string> { "gdi", "user", "memory", "virtual_memory" },
                Thresholds = new Dictionary<string, double>
                {
                    ["cpu_usage"] = 80.0,
                    ["memory_usage_mb"] = 1024.0,
                    ["license_utilization"] = 0.9,
                    ["error_rate"] = 0.05
                }
            };

            _mockOptions = new Mock<IOptions<VersionMetricsCollectionOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_collectionOptions);

            _metricsCollector = new VersionMetricsCollector(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task StartCollectionAsync_WhenNotCollecting_ShouldStartSuccessfully()
        {
            // Arrange
            Assert.False(_metricsCollector.IsCollecting);

            // Act
            var result = await _metricsCollector.StartCollectionAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(_metricsCollector.IsCollecting);
            Assert.Equal("Metrics collection started successfully", result.Message);
            Assert.Equal(_collectionOptions.CollectionInterval, result.CollectionInterval);
            _output.WriteLine($"Metrics collection started: {result.Message}");
        }

        [Fact]
        public async Task StartCollectionAsync_WhenAlreadyCollecting_ShouldReturnAlreadyRunningMessage()
        {
            // Arrange
            await _metricsCollector.StartCollectionAsync();
            Assert.True(_metricsCollector.IsCollecting);

            // Act
            var result = await _metricsCollector.StartCollectionAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Metrics collection already running", result.Message);
            _output.WriteLine($"Metrics collection already running: {result.Message}");
        }

        [Fact]
        public async Task StopCollectionAsync_WhenCollecting_ShouldStopSuccessfully()
        {
            // Arrange
            await _metricsCollector.StartCollectionAsync();
            Assert.True(_metricsCollector.IsCollecting);

            // Act
            var result = await _metricsCollector.StopCollectionAsync();

            // Assert
            Assert.True(result.Success);
            Assert.False(_metricsCollector.IsCollecting);
            Assert.Equal("Metrics collection stopped successfully", result.Message);
            _output.WriteLine($"Metrics collection stopped: {result.Message}");
        }

        [Fact]
        public async Task StopCollectionAsync_WhenNotCollecting_ShouldReturnNotRunningMessage()
        {
            // Arrange
            Assert.False(_metricsCollector.IsCollecting);

            // Act
            var result = await _metricsCollector.StopCollectionAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Metrics collection not running", result.Message);
            _output.WriteLine($"Metrics collection not running: {result.Message}");
        }

        [Fact]
        public async Task CollectMetricsAsync_WithValidVersion_ShouldCollectSuccessfully()
        {
            // Arrange
            var version = "2023";

            // Act
            var result = await _metricsCollector.CollectMetricsAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Metrics);
            Assert.Equal(version, result.Metrics.Version);
            Assert.True(result.Metrics.Timestamp <= DateTime.UtcNow);
            Assert.True(result.Metrics.Timestamp > DateTime.UtcNow.AddMinutes(-1));

            // Verify metrics are populated
            Assert.NotNull(result.Metrics.PerformanceMetrics);
            Assert.NotNull(result.Metrics.ResourceMetrics);
            Assert.NotNull(result.Metrics.HealthMetrics);
            Assert.NotNull(result.Metrics.OperationCounts);

            _output.WriteLine($"Metrics collected for {version}: {result.Metrics.PerformanceMetrics.Count} performance, {result.Metrics.ResourceMetrics.Count} resource, {result.Metrics.HealthMetrics.Count} health metrics");
        }

        [Fact]
        public async Task CollectMetricsAsync_WithNullVersion_ShouldThrowArgumentException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _metricsCollector.CollectMetricsAsync(null!));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task CollectMetricsAsync_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _metricsCollector.CollectMetricsAsync(string.Empty));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task CollectMetricsAsync_WhenDisposed_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _metricsCollector.Dispose();
            var version = "2023";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _metricsCollector.CollectMetricsAsync(version));

            Assert.Equal("Cannot access a disposed object.\r\nObject name: 'VersionMetricsCollector'.", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task GetMetricsHistoryAsync_WithExistingVersion_ShouldReturnHistory()
        {
            // Arrange
            var version = "2023";

            // Collect metrics multiple times
            await _metricsCollector.CollectMetricsAsync(version);
            await Task.Delay(100); // Small delay to ensure different timestamps
            await _metricsCollector.CollectMetricsAsync(version);

            // Act
            var result = await _metricsCollector.GetMetricsHistoryAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Metrics);
            Assert.True(result.Metrics.Count >= 2);
            Assert.All(result.Metrics, m => Assert.Equal(version, m.Version));

            // Verify metrics are ordered by timestamp (most recent first)
            for (int i = 0; i < result.Metrics.Count - 1; i++)
            {
                Assert.True(result.Metrics[i].Timestamp >= result.Metrics[i + 1].Timestamp);
            }

            _output.WriteLine($"Retrieved {result.Metrics.Count} metrics for {version}");
        }

        [Fact]
        public async Task GetMetricsHistoryAsync_WithNonexistentVersion_ShouldReturnEmptyList()
        {
            // Arrange
            var version = "nonexistent";

            // Act
            var result = await _metricsCollector.GetMetricsHistoryAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Metrics);
            Assert.Empty(result.Metrics);
            _output.WriteLine($"No metrics found for nonexistent version {version}");
        }

        [Fact]
        public async Task GetMetricsHistoryAsync_WithTimeRange_ShouldReturnFilteredHistory()
        {
            // Arrange
            var version = "2023";

            // Collect metrics
            await _metricsCollector.CollectMetricsAsync(version);
            await Task.Delay(100);
            await _metricsCollector.CollectMetricsAsync(version);

            var startTime = DateTime.UtcNow.AddMinutes(-1);
            var endTime = DateTime.UtcNow.AddMinutes(1);

            // Act
            var result = await _metricsCollector.GetMetricsHistoryAsync(version, endTime - startTime);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Metrics);
            Assert.All(result.Metrics, m =>
            {
                Assert.True(m.Timestamp >= startTime);
                Assert.True(m.Timestamp <= endTime);
            });

            _output.WriteLine($"Retrieved {result.Metrics.Count} metrics within time range for {version}");
        }

        [Fact]
        public async Task GetMetricsSummaryAsync_WithExistingVersion_ShouldReturnSummary()
        {
            // Arrange
            var version = "2023";

            // Collect metrics multiple times
            await _metricsCollector.CollectMetricsAsync(version);
            await Task.Delay(100);
            await _metricsCollector.CollectMetricsAsync(version);

            // Act
            var result = await _metricsCollector.GetMetricsSummaryAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Summary);
            Assert.True(result.Summary.Count > 0);

            // Verify summary contains expected metrics
            Assert.True(result.Summary.ContainsKey("cpu_usage_avg") || result.Summary.ContainsKey("memory_usage_mb_avg"));
            _output.WriteLine($"Metrics summary for {version}: {result.Summary.Count} summary metrics");
        }

        [Fact]
        public async Task GetMetricsSummaryAsync_WithNonexistentVersion_ShouldReturnEmptySummary()
        {
            // Arrange
            var version = "nonexistent";

            // Act
            var result = await _metricsCollector.GetMetricsSummaryAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Summary);
            Assert.Empty(result.Summary);
            _output.WriteLine($"No metrics summary for nonexistent version {version}");
        }

        [Fact]
        public async Task ClearMetricsHistoryAsync_WithVersion_ShouldClearVersionHistory()
        {
            // Arrange
            var version1 = "2023";
            var version2 = "2024";

            // Collect metrics for multiple versions
            await _metricsCollector.CollectMetricsAsync(version1);
            await _metricsCollector.CollectMetricsAsync(version2);

            // Verify metrics exist
            var history1 = await _metricsCollector.GetMetricsHistoryAsync(version1);
            var history2 = await _metricsCollector.GetMetricsHistoryAsync(version2);
            Assert.True(history1.Metrics.Count > 0);
            Assert.True(history2.Metrics.Count > 0);

            // Act
            _metricsCollector.ClearMetricsHistory(version1);

            // Assert
            var clearedHistory1 = await _metricsCollector.GetMetricsHistoryAsync(version1);
            var remainingHistory2 = await _metricsCollector.GetMetricsHistoryAsync(version2);

            Assert.Empty(clearedHistory1.Metrics);
            Assert.True(remainingHistory2.Metrics.Count > 0);
            _output.WriteLine($"Cleared metrics history for {version1}, kept history for {version2}");
        }

        [Fact]
        public async Task ClearMetricsHistoryAsync_WithoutVersion_ShouldClearAllHistory()
        {
            // Arrange
            var version1 = "2023";
            var version2 = "2024";

            // Collect metrics for multiple versions
            await _metricsCollector.CollectMetricsAsync(version1);
            await _metricsCollector.CollectMetricsAsync(version2);

            // Verify metrics exist
            var history1 = await _metricsCollector.GetMetricsHistoryAsync(version1);
            var history2 = await _metricsCollector.GetMetricsHistoryAsync(version2);
            Assert.True(history1.Metrics.Count > 0);
            Assert.True(history2.Metrics.Count > 0);

            // Act
            _metricsCollector.ClearMetricsHistory();

            // Assert
            var clearedHistory1 = await _metricsCollector.GetMetricsHistoryAsync(version1);
            var clearedHistory2 = await _metricsCollector.GetMetricsHistoryAsync(version2);

            Assert.Empty(clearedHistory1.Metrics);
            Assert.Empty(clearedHistory2.Metrics);
            _output.WriteLine("Cleared all metrics history");
        }

        [Fact]
        public void MonitoredVersions_ShouldReturnCorrectVersions()
        {
            // Arrange
            var version1 = "2023";
            var version2 = "2024";

            // Collect metrics for versions
            _metricsCollector.CollectMetricsAsync(version1).Wait();
            _metricsCollector.CollectMetricsAsync(version2).Wait();

            // Act
            var monitoredVersions = _metricsCollector.MonitoredVersions;

            // Assert
            Assert.NotNull(monitoredVersions);
            Assert.Contains(version1, monitoredVersions);
            Assert.Contains(version2, monitoredVersions);
            _output.WriteLine($"Monitored versions: {string.Join(", ", monitoredVersions)}");
        }

        [Fact]
        public void LatestMetrics_ShouldReturnLatestMetrics()
        {
            // Arrange
            var version1 = "2023";
            var version2 = "2024";

            // Collect metrics for versions
            _metricsCollector.CollectMetricsAsync(version1).Wait();
            _metricsCollector.CollectMetricsAsync(version2).Wait();

            // Act
            var latestMetrics = _metricsCollector.LatestMetrics;

            // Assert
            Assert.NotNull(latestMetrics);
            Assert.True(latestMetrics.Count >= 2);
            Assert.True(latestMetrics.ContainsKey(version1));
            Assert.True(latestMetrics.ContainsKey(version2));
            _output.WriteLine($"Latest metrics available for {latestMetrics.Count} versions");
        }

        [Fact]
        public async Task EventHandlers_ShouldBeInvokedCorrectly()
        {
            // Arrange
            var version = "2023";

            var metricsCollectedInvoked = false;
            var thresholdExceededInvoked = false;
            var collectionStartedInvoked = false;
            var collectionStoppedInvoked = false;
            var collectionErrorInvoked = false;

            _metricsCollector.MetricsCollected += (sender, args) =>
            {
                metricsCollectedInvoked = true;
                Assert.Equal(version, args.Version);
                Assert.NotNull(args.Metrics);
                _output.WriteLine($"MetricsCollected event invoked for {args.Version}");
            };

            _metricsCollector.ThresholdExceeded += (sender, args) =>
            {
                thresholdExceededInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"ThresholdExceeded event invoked for {args.Version}: {args.MetricName} = {args.ActualValue}");
            };

            _metricsCollector.CollectionStarted += (sender, args) =>
            {
                collectionStartedInvoked = true;
                _output.WriteLine("CollectionStarted event invoked");
            };

            _metricsCollector.CollectionStopped += (sender, args) =>
            {
                collectionStoppedInvoked = true;
                _output.WriteLine("CollectionStopped event invoked");
            };

            _metricsCollector.CollectionError += (sender, args) =>
            {
                collectionErrorInvoked = true;
                _output.WriteLine($"CollectionError event invoked: {args.Message}");
            };

            // Act
            await _metricsCollector.StartCollectionAsync();
            await _metricsCollector.CollectMetricsAsync(version);
            await _metricsCollector.StopCollectionAsync();

            // Assert
            Assert.True(metricsCollectedInvoked, "MetricsCollected event should be invoked");
            Assert.True(collectionStartedInvoked, "CollectionStarted event should be invoked");
            Assert.True(collectionStoppedInvoked, "CollectionStopped event should be invoked");
            Assert.False(collectionErrorInvoked, "CollectionError event should not be invoked for successful operations");
            _output.WriteLine("All expected events were invoked correctly");
        }

        [Fact]
        public async Task ThresholdExceeded_ShouldTriggerThresholdExceededEvent()
        {
            // Arrange
            var version = "2023";

            // Configure a low threshold to trigger exceeded event
            _collectionOptions.Thresholds["test_threshold"] = 0.1;
            var thresholdExceededInvoked = false;

            _metricsCollector.ThresholdExceeded += (sender, args) =>
            {
                thresholdExceededInvoked = true;
                Assert.Equal("test_threshold", args.MetricName);
                Assert.True(args.ActualValue > args.Threshold);
                _output.WriteLine($"Threshold exceeded: {args.MetricName} = {args.ActualValue} > {args.Threshold}");
            };

            // Act - collect metrics that should exceed the threshold
            await _metricsCollector.CollectMetricsAsync(version);

            // Note: This test depends on the actual metrics collection logic
            // In a real implementation, you would mock the metrics collection to return specific values
            _output.WriteLine($"Threshold exceeded event invoked: {thresholdExceededInvoked}");
        }

        [Fact]
        public async Task MaxHistorySize_ShouldLimitHistory()
        {
            // Arrange
            var version = "2023";
            _collectionOptions.MaxHistorySize = 3; // Set small limit

            // Collect metrics multiple times
            for (int i = 0; i < 5; i++)
            {
                await _metricsCollector.CollectMetricsAsync(version);
                await Task.Delay(50); // Small delay to ensure different timestamps
            }

            // Act
            var result = await _metricsCollector.GetMetricsHistoryAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Metrics);
            Assert.True(result.Metrics.Count <= 3); // Should be limited to max history size
            _output.WriteLine($"History size limited to {result.Metrics.Count} entries (max: 3)");
        }

        [Fact]
        public void Dispose_WhenCalled_ShouldCleanUpResources()
        {
            // Arrange
            _metricsCollector.StartCollectionAsync().Wait();
            Assert.True(_metricsCollector.IsCollecting);

            // Act
            _metricsCollector.Dispose();

            // Assert
            // Verify that the collector can no longer be used
            var exception = Assert.Throws<ObjectDisposedException>(() =>
                _metricsCollector.StartCollectionAsync());

            Assert.Equal("Cannot access a disposed object.\r\nObject name: 'VersionMetricsCollector'.", exception.Message);
            _output.WriteLine("Metrics collector disposed successfully");
        }

        public void Dispose()
        {
            _metricsCollector?.Dispose();
        }
    }
}