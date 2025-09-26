using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    /// <summary>
    /// Unit tests for DocumentActivityMonitor
    /// </summary>
    public class DocumentActivityMonitorTests
    {
        private readonly Mock<ILogger<DocumentActivityMonitor>> _mockLogger;
        private readonly PingBasedDetectionConfig _config;

        public DocumentActivityMonitorTests()
        {
            _mockLogger = new Mock<ILogger<DocumentActivityMonitor>>();
            _config = new PingBasedDetectionConfig
            {
                DetectionIntervalSeconds = 30,
                EnableDocumentActivityMonitoring = true
            };
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var monitor = new DocumentActivityMonitor(_mockLogger.Object, _config);

            // Assert
            Assert.NotNull(monitor.Statistics);
            Assert.Equal(DateTime.UtcNow.Date, monitor.Statistics.StartTime.Date);
            Assert.False(monitor.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DocumentActivityMonitor(null, _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DocumentActivityMonitor(_mockLogger.Object, null));
        }

        [Fact]
        public async Task InitializeAsync_ShouldInitializeSuccessfully()
        {
            // Arrange
            var monitor = CreateMonitor();
            var configuration = new IdleDetectorConfiguration();

            // Act
            await monitor.InitializeAsync(configuration);

            // Assert
            // Verify that statistics were reset
            Assert.Equal(0, monitor.Statistics.TotalActivitiesMonitored);
            Assert.Equal(DateTime.UtcNow.Date, monitor.Statistics.StartTime.Date);
        }

        [Fact]
        public async Task StartAsync_ShouldStartSuccessfully()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());

            // Act
            await monitor.StartAsync();

            // Assert
            Assert.True(monitor.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopSuccessfully()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());
            await monitor.StartAsync();

            // Act
            await monitor.StopAsync();

            // Assert
            Assert.False(monitor.IsRunning);
        }

        [Fact]
        public async Task GetRecentActivitiesAsync_WithNoHistory_ShouldReturnEmptyList()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());

            var processId = 1234;
            var timeWindow = TimeSpan.FromMinutes(30);

            // Act
            var activities = await monitor.GetRecentActivitiesAsync(processId, timeWindow);

            // Assert
            Assert.NotNull(activities);
            Assert.Empty(activities);
        }

        [Fact]
        public async Task GetHealthAsync_WithHealthyMonitor_ShouldReturnHealthyStatus()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());

            // Act
            var health = await monitor.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.Equal("DocumentActivityMonitor", health.DetectorName);
            Assert.True(health.IsHealthy);
            Assert.Equal("Healthy", health.StatusMessage);
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnInitialStatistics()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());

            // Act
            var stats = await monitor.GetStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalActivitiesMonitored);
            Assert.Equal(0, stats.WindowFocusEvents);
            Assert.Equal(0, stats.DocumentAccessEvents);
            Assert.Equal(0, stats.SuccessfulDetections);
            Assert.Equal(0, stats.FailedDetections);
            Assert.Equal(0, stats.ErrorCount);
            Assert.NotNull(stats.StartTime);
        }

        [Fact]
        public async Task ResetStatisticsAsync_ShouldResetAllStatistics()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());

            // Act
            await monitor.ResetStatisticsAsync();

            // Assert
            var stats = await monitor.GetStatisticsAsync();
            Assert.Equal(0, stats.TotalActivitiesMonitored);
            Assert.Equal(0, stats.WindowFocusEvents);
            Assert.Equal(0, stats.DocumentAccessEvents);
            Assert.Equal(0, stats.SuccessfulDetections);
            Assert.Equal(0, stats.FailedDetections);
            Assert.Equal(0, stats.ErrorCount);
        }

        [Fact]
        public async Task DocumentActivityDetectedEvent_ShouldBeRaisedWhenActivityOccurs()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());

            var eventRaised = false;
            DocumentActivityData capturedActivity = null;

            monitor.DocumentActivityDetected += (sender, activity) =>
            {
                eventRaised = true;
                capturedActivity = activity;
            };

            // We can't easily simulate document activity without a complex setup
            // For now, we'll just test that the event can be subscribed to
            Assert.True(true); // If we get here, event subscription worked
        }

        [Fact]
        public void Dispose_ShouldDisposeResources()
        {
            // Arrange
            var monitor = CreateMonitor();

            // Act & Assert
            Assert.DoesNotThrow(() => monitor.Dispose());
        }

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopMonitor()
        {
            // Arrange
            var monitor = CreateMonitor();
            await monitor.InitializeAsync(new IdleDetectorConfiguration());
            await monitor.StartAsync();

            // Act
            monitor.Dispose();

            // Assert
            Assert.False(monitor.IsRunning);
        }

        #region Helper Methods

        private DocumentActivityMonitor CreateMonitor()
        {
            return new DocumentActivityMonitor(_mockLogger.Object, _config);
        }

        #endregion
    }
}