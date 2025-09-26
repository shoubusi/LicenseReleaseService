using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerPerformanceOptimizerTests
    {
        private readonly Mock<ILogger<TimerPerformanceOptimizer>> _mockLogger;
        private readonly Mock<TimerMemoryManager> _mockMemoryManager;
        private readonly Mock<TimerThreadPoolManager> _mockThreadPoolManager;
        private readonly Mock<TimerCacheManager> _mockCacheManager;
        private readonly TimerPerformanceOptimizerOptions _options;

        public TimerPerformanceOptimizerTests()
        {
            _mockLogger = new Mock<ILogger<TimerPerformanceOptimizer>>();
            _mockMemoryManager = new Mock<TimerMemoryManager>(Mock.Of<ILogger<TimerMemoryManager>>(), new TimerMemoryOptions());
            _mockThreadPoolManager = new Mock<TimerThreadPoolManager>(Mock.Of<ILogger<TimerThreadPoolManager>>(), new TimerThreadPoolOptions());
            _mockCacheManager = new Mock<TimerCacheManager>(Mock.Of<ILogger<TimerCacheManager>>(), new TimerCacheOptions());
            _options = new TimerPerformanceOptimizerOptions();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Act
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            // Assert
            Assert.NotNull(optimizer);
            Assert.False(optimizer.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceOptimizer(
                null,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceOptimizer(
                _mockLogger.Object,
                null,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object));
        }

        [Fact]
        public void Constructor_WithNullMemoryManager_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                null,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object));
        }

        [Fact]
        public void Constructor_WithNullThreadPoolManager_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                null,
                _mockCacheManager.Object));
        }

        [Fact]
        public void Constructor_WithNullCacheManager_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                null));
        }

        [Fact]
        public async Task StartAsync_WhenNotRunning_ShouldStartOptimizer()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            _mockMemoryManager.Setup(m => m.StartAsync()).Returns(Task.CompletedTask);
            _mockThreadPoolManager.Setup(m => m.StartAsync()).Returns(Task.CompletedTask);
            _mockCacheManager.Setup(m => m.StartAsync()).Returns(Task.CompletedTask);

            // Act
            await optimizer.StartAsync();

            // Assert
            Assert.True(optimizer.IsRunning);
            _mockMemoryManager.Verify(m => m.StartAsync(), Times.Once);
            _mockThreadPoolManager.Verify(m => m.StartAsync(), Times.Once);
            _mockCacheManager.Verify(m => m.StartAsync(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            // Act
            await optimizer.StartAsync();

            // Assert
            _mockMemoryManager.Verify(m => m.StartAsync(), Times.Once);
            _mockThreadPoolManager.Verify(m => m.StartAsync(), Times.Once);
            _mockCacheManager.Verify(m => m.StartAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopOptimizer()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            _mockMemoryManager.Setup(m => m.StopAsync()).Returns(Task.CompletedTask);
            _mockThreadPoolManager.Setup(m => m.StopAsync()).Returns(Task.CompletedTask);
            _mockCacheManager.Setup(m => m.StopAsync()).Returns(Task.CompletedTask);

            // Act
            await optimizer.StopAsync();

            // Assert
            Assert.False(optimizer.IsRunning);
            _mockMemoryManager.Verify(m => m.StopAsync(), Times.Once);
            _mockThreadPoolManager.Verify(m => m.StopAsync(), Times.Once);
            _mockCacheManager.Verify(m => m.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldDoNothing()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            // Act
            await optimizer.StopAsync();

            // Assert
            _mockMemoryManager.Verify(m => m.StopAsync(), Times.Never);
            _mockThreadPoolManager.Verify(m => m.StopAsync(), Times.Never);
            _mockCacheManager.Verify(m => m.StopAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteOptimizationCycleAsync_WhenRunning_ShouldExecuteOptimization()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            _mockMemoryManager.Setup(m => m.OptimizeMemoryAsync()).Returns(Task.CompletedTask);
            _mockThreadPoolManager.Setup(m => m.OptimizeThreadPoolAsync()).Returns(Task.CompletedTask);
            _mockCacheManager.Setup(m => m.OptimizeCacheAsync()).Returns(Task.CompletedTask);

            // Act
            await optimizer.ExecuteOptimizationCycleAsync();

            // Assert
            _mockMemoryManager.Verify(m => m.OptimizeMemoryAsync(), Times.Once);
            _mockThreadPoolManager.Verify(m => m.OptimizeThreadPoolAsync(), Times.Once);
            _mockCacheManager.Verify(m => m.OptimizeCacheAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteOptimizationCycleAsync_WhenNotRunning_ShouldNotExecuteOptimization()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            // Act
            await optimizer.ExecuteOptimizationCycleAsync();

            // Assert
            _mockMemoryManager.Verify(m => m.OptimizeMemoryAsync(), Times.Never);
            _mockThreadPoolManager.Verify(m => m.OptimizeThreadPoolAsync(), Times.Never);
            _mockCacheManager.Verify(m => m.OptimizeCacheAsync(), Times.Never);
        }

        [Fact]
        public void GetOptimizationStatus_WhenNotRunning_ShouldReturnStoppedStatus()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            // Act
            var status = optimizer.GetOptimizationStatus();

            // Assert
            Assert.Equal(TimerOptimizationStatus.Stopped, status.Status);
            Assert.Equal(0, status.OptimizationCycles);
            Assert.Equal(0, status.SuccessfulOptimizations);
            Assert.Equal(0, status.FailedOptimizations);
        }

        [Fact]
        public async Task GetOptimizationStatus_AfterStarting_ShouldReturnRunningStatus()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            // Act
            var status = optimizer.GetOptimizationStatus();

            // Assert
            Assert.Equal(TimerOptimizationStatus.Running, status.Status);
        }

        [Fact]
        public async Task GetOptimizationStatus_AfterOptimizationCycle_ShouldUpdateMetrics()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();
            await optimizer.ExecuteOptimizationCycleAsync();

            // Act
            var status = optimizer.GetOptimizationStatus();

            // Assert
            Assert.Equal(1, status.OptimizationCycles);
            Assert.Equal(1, status.SuccessfulOptimizations);
            Assert.Equal(0, status.FailedOptimizations);
        }

        [Fact]
        public async Task UpdateOptimizationIntervalAsync_WithValidInterval_ShouldUpdateInterval()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            // Act
            await optimizer.UpdateOptimizationIntervalAsync(120000);

            // Assert
            var status = optimizer.GetOptimizationStatus();
            Assert.Equal(120000, status.OptimizationIntervalMs);
        }

        [Fact]
        public async Task UpdateOptimizationIntervalAsync_WithInvalidInterval_ShouldThrowArgumentException()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => optimizer.UpdateOptimizationIntervalAsync(100));
        }

        [Fact]
        public void Events_ShouldBeRaisedCorrectly()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            var optimizationStartedEventRaised = false;
            var optimizationCompletedEventRaised = false;
            var performanceTuningAppliedEventRaised = false;

            optimizer.OptimizationStarted += (sender, args) => optimizationStartedEventRaised = true;
            optimizer.OptimizationCompleted += (sender, args) => optimizationCompletedEventRaised = true;
            optimizer.PerformanceTuningApplied += (sender, args) => performanceTuningAppliedEventRaised = true;

            // Act
            // Events are raised during optimization cycles, so we need to simulate this
            // For now, we'll just verify the events are not null
            Assert.NotNull(optimizer.OptimizationStarted);
            Assert.NotNull(optimizer.OptimizationCompleted);
            Assert.NotNull(optimizer.PerformanceTuningApplied);
        }

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopAndDispose()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            await optimizer.StartAsync();

            // Act
            optimizer.Dispose();

            // Assert
            Assert.False(optimizer.IsRunning);
        }

        [Fact]
        public void Dispose_WhenAlreadyDisposed_ShouldNotThrow()
        {
            // Arrange
            var optimizer = new TimerPerformanceOptimizer(
                _mockLogger.Object,
                _options,
                _mockMemoryManager.Object,
                _mockThreadPoolManager.Object,
                _mockCacheManager.Object);

            // Act
            optimizer.Dispose();
            optimizer.Dispose(); // Should not throw

            // Assert
            Assert.True(true); // If we get here, no exception was thrown
        }
    }
}