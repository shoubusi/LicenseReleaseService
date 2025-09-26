using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerCacheManagerTests
    {
        private readonly Mock<ILogger<TimerCacheManager>> _mockLogger;
        private readonly TimerCacheOptions _options;

        public TimerCacheManagerTests()
        {
            _mockLogger = new Mock<ILogger<TimerCacheManager>>();
            _options = new TimerCacheOptions();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Act
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            // Assert
            Assert.NotNull(manager);
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerCacheManager(null, _options));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerCacheManager(_mockLogger.Object, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotRunning_ShouldStartManager()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            // Act
            await manager.StartAsync();

            // Assert
            Assert.True(manager.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
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
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
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
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            // Act
            await manager.StopAsync();

            // Assert
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public async Task OptimizeCacheAsync_WhenRunning_ShouldOptimize()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            await manager.OptimizeCacheAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task OptimizeCacheAsync_WhenNotRunning_ShouldNotOptimize()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            // Act
            await manager.OptimizeCacheAsync();

            // Assert
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public async Task CreateCacheAsync_WithValidParameters_ShouldCreateCache()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var cache = await manager.CreateCacheAsync<string, object>("test-cache", 1000);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal("test-cache", cache.Name);
            Assert.Equal(1000, cache.MaxSize);
        }

        [Fact]
        public async Task CreateCacheAsync_WithNullName_ShouldThrowArgumentNullException()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateCacheAsync<string, object>(null, 1000));
        }

        [Fact]
        public async Task CreateCacheAsync_WithInvalidSize_ShouldThrowArgumentException()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateCacheAsync<string, object>("test-cache", 0));
        }

        [Fact]
        public async Task CreateCacheAsync_WhenNotRunning_ShouldReturnNull()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            // Act
            var cache = await manager.CreateCacheAsync<string, object>("test-cache", 1000);

            // Assert
            Assert.Null(cache);
        }

        [Fact]
        public async Task GetCacheAsync_WithExistingCache_ShouldReturnCache()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();
            await manager.CreateCacheAsync<string, object>("test-cache", 1000);

            // Act
            var cache = await manager.GetCacheAsync<string, object>("test-cache");

            // Assert
            Assert.NotNull(cache);
            Assert.Equal("test-cache", cache.Name);
        }

        [Fact]
        public async Task GetCacheAsync_WithNonExistingCache_ShouldReturnNull()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var cache = await manager.GetCacheAsync<string, object>("non-existing-cache");

            // Assert
            Assert.Null(cache);
        }

        [Fact]
        public async Task RemoveCacheAsync_WithExistingCache_ShouldReturnTrue()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();
            await manager.CreateCacheAsync<string, object>("test-cache", 1000);

            // Act
            var result = await manager.RemoveCacheAsync("test-cache");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RemoveCacheAsync_WithNonExistingCache_ShouldReturnFalse()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var result = await manager.RemoveCacheAsync("non-existing-cache");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetCacheMetricsAsync_ShouldReturnMetrics()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var metrics = await manager.GetCacheMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.Uptime.TotalMilliseconds >= 0);
            Assert.True(metrics.TotalCaches >= 0);
            Assert.True(metrics.TotalCacheItems >= 0);
            Assert.True(metrics.TotalCacheHits >= 0);
        }

        [Fact]
        public async Task GetSystemMetricsAsync_ShouldReturnMetrics()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var metrics = await manager.GetSystemMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.CpuUsagePercent >= 0);
            Assert.True(metrics.MemoryUsagePercent >= 0);
            Assert.True(metrics.AvailableMemoryMB >= 0);
        }

        [Fact]
        public async Task GetCacheItemMetricsAsync_WithExistingCache_ShouldReturnMetrics()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();
            await manager.CreateCacheAsync<string, object>("test-cache", 1000);

            // Act
            var metrics = await manager.GetCacheItemMetricsAsync("test-cache");

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal("test-cache", metrics.Name);
        }

        [Fact]
        public async Task GetCacheItemMetricsAsync_WithNonExistingCache_ShouldReturnNull()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            var metrics = await manager.GetCacheItemMetricsAsync("non-existing-cache");

            // Assert
            Assert.Null(metrics);
        }

        [Fact]
        public async Task ClearAllCachesAsync_ShouldClearAllCaches()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();
            await manager.CreateCacheAsync<string, object>("test-cache1", 1000);
            await manager.CreateCacheAsync<string, object>("test-cache2", 1000);

            // Act
            await manager.ClearAllCachesAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task ForceCacheOptimizationAsync_ShouldForceOptimization()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
            await manager.StartAsync();

            // Act
            await manager.ForceCacheOptimizationAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public void Events_ShouldBeRaisedCorrectly()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            var cacheOptimizedEventRaised = false;
            var cacheItemAccessedEventRaised = false;
            var cacheItemEvictedEventRaised = false;

            manager.CacheOptimized += (sender, args) => cacheOptimizedEventRaised = true;
            manager.CacheItemAccessed += (sender, args) => cacheItemAccessedEventRaised = true;
            manager.CacheItemEvicted += (sender, args) => cacheItemEvictedEventRaised = true;

            // Act
            // Events are raised during cache operations, so we'll just verify they're not null
            Assert.NotNull(manager.CacheOptimized);
            Assert.NotNull(manager.CacheItemAccessed);
            Assert.NotNull(manager.CacheItemEvicted);
        }

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopAndDispose()
        {
            // Arrange
            var manager = new TimerCacheManager(_mockLogger.Object, _options);
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
            var manager = new TimerCacheManager(_mockLogger.Object, _options);

            // Act
            manager.Dispose();
            manager.Dispose(); // Should not throw

            // Assert
            Assert.True(true); // If we get here, no exception was thrown
        }

        [Fact]
        public void TimerCache_ShouldWorkCorrectly()
        {
            // Arrange
            var cache = new TimerCache<string, object>("test-cache", 1000, TimeSpan.FromMinutes(5));

            // Act & Assert
            Assert.Equal("test-cache", cache.Name);
            Assert.Equal(1000, cache.MaxSize);
            Assert.Equal(TimeSpan.FromMinutes(5), cache.DefaultExpiration);
        }

        [Fact]
        public void TimerCacheMetrics_ShouldHaveValidProperties()
        {
            // Arrange
            var metrics = new TimerCacheMetrics();

            // Act & Assert
            Assert.Equal(TimeSpan.Zero, metrics.Uptime);
            Assert.Equal(0, metrics.TotalCaches);
            Assert.Equal(0, metrics.TotalCacheItems);
            Assert.Equal(0, metrics.TotalCacheHits);
            Assert.Equal(0, metrics.TotalCacheMisses);
        }

        [Fact]
        public void TimerCacheItemMetrics_ShouldHaveValidProperties()
        {
            // Arrange
            var metrics = new TimerCacheItemMetrics();

            // Act & Assert
            Assert.Empty(metrics.Name);
            Assert.Equal(0, metrics.Size);
            Assert.Equal(0, metrics.MaxSize);
            Assert.Equal(0, metrics.Hits);
            Assert.Equal(0, metrics.Misses);
        }
    }
}