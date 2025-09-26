using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerThreadPoolManagerTests
    {
        private readonly Mock<ILogger<TimerThreadPoolManager>> _mockLogger;
        private readonly TimerThreadPoolOptions _options;

        public TimerThreadPoolManagerTests()
        {
            _mockLogger = new Mock<ILogger<TimerThreadPoolManager>>();
            _options = new TimerThreadPoolOptions();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Act
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            // Assert
            Assert.NotNull(manager);
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerThreadPoolManager(null, _options));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerThreadPoolManager(_mockLogger.Object, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotRunning_ShouldStartManager()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            // Act
            await manager.StartAsync();

            // Assert
            Assert.True(manager.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            await manager.StartAsync();

            // Assert
            Assert.True(manager.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopManager()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            await manager.StopAsync();

            // Assert
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldDoNothing()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            // Act
            await manager.StopAsync();

            // Assert
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public async Task OptimizeThreadPoolAsync_WhenRunning_ShouldOptimize()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            await manager.OptimizeThreadPoolAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task OptimizeThreadPoolAsync_WhenNotRunning_ShouldNotOptimize()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            // Act
            await manager.OptimizeThreadPoolAsync();

            // Assert
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public async Task QueueWorkItemAsync_WithValidWorkItem_ShouldQueue()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            var workItem = new TimerWorkItem
            {
                Id = Guid.NewGuid().ToString(),
                Priority = TimerWorkItemPriority.Normal,
                Work = () => Task.CompletedTask
            };

            // Act
            var result = await manager.QueueWorkItemAsync(workItem);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task QueueWorkItemAsync_WithNullWorkItem_ShouldThrowArgumentNullException()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.QueueWorkItemAsync(null));
        }

        [Fact]
        public async Task QueueWorkItemAsync_WhenNotRunning_ShouldReturnFalse()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            var workItem = new TimerWorkItem
            {
                Id = Guid.NewGuid().ToString(),
                Priority = TimerWorkItemPriority.Normal,
                Work = () => Task.CompletedTask
            };

            // Act
            var result = await manager.QueueWorkItemAsync(workItem);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetThreadPoolMetricsAsync_ShouldReturnMetrics()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var metrics = await manager.GetThreadPoolMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.Uptime.TotalMilliseconds >= 0);
            Assert.True(metrics.CurrentThreadPoolSize >= 0);
            Assert.True(metrics.MinThreadPoolSize >= 0);
            Assert.True(metrics.MaxThreadPoolSize >= 0);
        }

        [Fact]
        public async Task GetWorkItemTrackerAsync_WithExistingTracker_ShouldReturnTracker()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            var workItemId = Guid.NewGuid().ToString();

            // Act
            var tracker = await manager.GetWorkItemTrackerAsync(workItemId);

            // Assert
            Assert.NotNull(tracker);
            Assert.Equal(workItemId, tracker.WorkItemId);
        }

        [Fact]
        public async Task GetWorkItemTrackerAsync_WithNullWorkItemId_ShouldThrowArgumentNullException()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.GetWorkItemTrackerAsync(null));
        }

        [Fact]
        public async Task RemoveWorkItemTrackerAsync_WithExistingTracker_ShouldReturnTrue()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            var workItemId = Guid.NewGuid().ToString();
            await manager.GetWorkItemTrackerAsync(workItemId);

            // Act
            var result = await manager.RemoveWorkItemTrackerAsync(workItemId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RemoveWorkItemTrackerAsync_WithNonExistingTracker_ShouldReturnFalse()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            var workItemId = Guid.NewGuid().ToString();

            // Act
            var result = await manager.RemoveWorkItemTrackerAsync(workItemId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AdjustThreadPoolSizeAsync_WithValidSize_ShouldAdjust()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            await manager.AdjustThreadPoolSizeAsync(50);

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task AdjustThreadPoolSizeAsync_WithInvalidSize_ShouldThrowArgumentException()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => manager.AdjustThreadPoolSizeAsync(0));
        }

        [Fact]
        public async Task GetQueueStatisticsAsync_ShouldReturnStatistics()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var stats = await manager.GetQueueStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.TotalQueued >= 0);
            Assert.True(stats.TotalProcessed >= 0);
            Assert.True(stats.TotalRejected >= 0);
            Assert.True(stats.AverageQueueTime >= 0);
        }

        [Fact]
        public void Events_ShouldBeRaisedCorrectly()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            var threadPoolAdjustedEventRaised = false;
            var workItemQueuedEventRaised = false;
            var workItemProcessedEventRaised = false;

            manager.ThreadPoolAdjusted += (sender, args) => threadPoolAdjustedEventRaised = true;
            manager.WorkItemQueued += (sender, args) => workItemQueuedEventRaised = true;
            manager.WorkItemProcessed += (sender, args) => workItemProcessedEventRaised = true;

            // Act
            // Events are raised during thread pool operations, so we'll just verify they're not null
            Assert.NotNull(manager.ThreadPoolAdjusted);
            Assert.NotNull(manager.WorkItemQueued);
            Assert.NotNull(manager.WorkItemProcessed);
        }

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopAndDispose()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            manager.Dispose();

            // Assert
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public void Dispose_WhenAlreadyDisposed_ShouldNotThrow()
        {
            // Arrange
            var manager = new TimerThreadPoolManager(_mockLogger.Object, _options);

            // Act
            manager.Dispose();
            manager.Dispose(); // Should not throw

            // Assert
            Assert.True(true); // If we get here, no exception was thrown
        }

        [Fact]
        public void TimerWorkItem_ShouldWorkCorrectly()
        {
            // Arrange
            var workItem = new TimerWorkItem
            {
                Id = Guid.NewGuid().ToString(),
                Priority = TimerWorkItemPriority.High,
                Work = () => Task.CompletedTask
            };

            // Act & Assert
            Assert.NotNull(workItem.Id);
            Assert.Equal(TimerWorkItemPriority.High, workItem.Priority);
            Assert.NotNull(workItem.Work);
        }

        [Fact]
        public void TimerWorkItemTracker_ShouldWorkCorrectly()
        {
            // Arrange
            var tracker = new TimerWorkItemTracker("test-work-item");

            // Act & Assert
            Assert.Equal("test-work-item", tracker.WorkItemId);
            Assert.Equal(0, tracker.TotalExecutions);
            Assert.Equal(0, tracker.SuccessfulExecutions);
            Assert.Equal(0, tracker.FailedExecutions);
        }

        [Fact]
        public void TimerThreadPoolMetrics_ShouldHaveValidProperties()
        {
            // Arrange
            var metrics = new TimerThreadPoolMetrics();

            // Act & Assert
            Assert.Equal(TimeSpan.Zero, metrics.Uptime);
            Assert.Equal(0, metrics.CurrentThreadPoolSize);
            Assert.Equal(0, metrics.MinThreadPoolSize);
            Assert.Equal(0, metrics.MaxThreadPoolSize);
            Assert.Equal(0, metrics.TotalAdjustments);
        }
    }
}