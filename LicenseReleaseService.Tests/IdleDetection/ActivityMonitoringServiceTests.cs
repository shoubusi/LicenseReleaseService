using System;
using System.Collections.Generic;
using System.IO;
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
    public class ActivityMonitoringServiceTests
    {
        private readonly Mock<ILogger<ActivityMonitoringService>> _mockLogger;
        private readonly Mock<FileSystemMonitor> _mockFileSystemMonitor;
        private readonly Mock<PerformanceMonitor> _mockPerformanceMonitor;
        private readonly Mock<SystemActivityTracker> _mockSystemActivityTracker;
        private readonly ActivityMonitoringConfig _config;
        private readonly ActivityMonitoringService _service;

        public ActivityMonitoringServiceTests()
        {
            _mockLogger = new Mock<ILogger<ActivityMonitoringService>>();
            _mockFileSystemMonitor = new Mock<FileSystemMonitor>(
                new FileSystemMonitorConfig(), Mock.Of<ILogger<FileSystemMonitor>>());
            _mockPerformanceMonitor = new Mock<PerformanceMonitor>(
                new PerformanceMonitorConfig(), Mock.Of<ILogger<PerformanceMonitor>>());
            _mockSystemActivityTracker = new Mock<SystemActivityTracker>(
                new SystemActivityTrackerConfig(), Mock.Of<ILogger<SystemActivityTracker>>());

            _config = new ActivityMonitoringConfig
            {
                IsEnabled = true,
                IdleThresholdSeconds = 300,
                ActivityScoreThreshold = 0.3,
                EnableFileSystemMonitoring = true,
                EnablePerformanceMonitoring = true,
                EnableSystemActivityMonitoring = true,
                ConfidenceThreshold = 0.7,
                MaxDetectionTimeMs = 5000,
                Priority = 15,
                CleanupIntervalMinutes = 30,
                MaxProcessHistoryCount = 1000
            };

            _service = new ActivityMonitoringService(
                _mockLogger.Object,
                _mockFileSystemMonitor.Object,
                _mockPerformanceMonitor.Object,
                _mockSystemActivityTracker.Object,
                _config);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeService()
        {
            // Arrange & Act
            var service = new ActivityMonitoringService(
                _mockLogger.Object,
                _mockFileSystemMonitor.Object,
                _mockPerformanceMonitor.Object,
                _mockSystemActivityTracker.Object,
                _config);

            // Assert
            Assert.NotNull(service);
            Assert.False(service.IsRunning);
            Assert.NotNull(service.Statistics);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ActivityMonitoringService(
                null,
                _mockFileSystemMonitor.Object,
                _mockPerformanceMonitor.Object,
                _mockSystemActivityTracker.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ActivityMonitoringService(
                _mockLogger.Object,
                _mockFileSystemMonitor.Object,
                _mockPerformanceMonitor.Object,
                _mockSystemActivityTracker.Object,
                null));
        }

        [Fact]
        public void Constructor_WithNullFileSystemMonitor_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ActivityMonitoringService(
                _mockLogger.Object,
                null,
                _mockPerformanceMonitor.Object,
                _mockSystemActivityTracker.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullPerformanceMonitor_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ActivityMonitoringService(
                _mockLogger.Object,
                _mockFileSystemMonitor.Object,
                null,
                _mockSystemActivityTracker.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullSystemActivityTracker_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ActivityMonitoringService(
                _mockLogger.Object,
                _mockFileSystemMonitor.Object,
                _mockPerformanceMonitor.Object,
                null,
                _config));
        }

        [Fact]
        public async Task StartAsync_WhenNotRunning_ShouldStartAllMonitors()
        {
            // Arrange
            _mockFileSystemMonitor.Setup(m => m.StartAsync()).Returns(Task.CompletedTask);
            _mockPerformanceMonitor.Setup(m => m.StartAsync()).Returns(Task.CompletedTask);
            _mockSystemActivityTracker.Setup(m => m.StartAsync()).Returns(Task.CompletedTask);

            // Act
            await _service.StartAsync();

            // Assert
            Assert.True(_service.IsRunning);
            _mockFileSystemMonitor.Verify(m => m.StartAsync(), Times.Once);
            _mockPerformanceMonitor.Verify(m => m.StartAsync(), Times.Once);
            _mockSystemActivityTracker.Verify(m => m.StartAsync(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            await _service.StartAsync();

            // Act
            await _service.StartAsync();

            // Assert
            _mockFileSystemMonitor.Verify(m => m.StartAsync(), Times.Once);
            _mockPerformanceMonitor.Verify(m => m.StartAsync(), Times.Once);
            _mockSystemActivityTracker.Verify(m => m.StartAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopAllMonitors()
        {
            // Arrange
            await _service.StartAsync();
            _mockFileSystemMonitor.Setup(m => m.StopAsync()).Returns(Task.CompletedTask);
            _mockPerformanceMonitor.Setup(m => m.StopAsync()).Returns(Task.CompletedTask);
            _mockSystemActivityTracker.Setup(m => m.StopAsync()).Returns(Task.CompletedTask);

            // Act
            await _service.StopAsync();

            // Assert
            Assert.False(_service.IsRunning);
            _mockFileSystemMonitor.Verify(m => m.StopAsync(), Times.Once);
            _mockPerformanceMonitor.Verify(m => m.StopAsync(), Times.Once);
            _mockSystemActivityTracker.Verify(m => m.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldNotStopAgain()
        {
            // Arrange
            await _service.StartAsync();
            await _service.StopAsync();

            // Act
            await _service.StopAsync();

            // Assert
            _mockFileSystemMonitor.Verify(m => m.StopAsync(), Times.Once);
            _mockPerformanceMonitor.Verify(m => m.StopAsync(), Times.Once);
            _mockSystemActivityTracker.Verify(m => m.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task DetectIdleAsync_WithNoActivity_ShouldReturnIdleResult()
        {
            // Arrange
            await _service.StartAsync();
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTCOMPUTER";

            _mockFileSystemMonitor.Setup(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()))
                .Returns(new List<FileActivityData>());
            _mockPerformanceMonitor.Setup(m => m.GetProcessMetrics(processId))
                .Returns(new ProcessMetrics { ProcessId = processId });
            _mockSystemActivityTracker.Setup(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()))
                .Returns(new List<SystemActivityEvent>());

            // Act
            var result = await _service.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(userName, result.UserName);
            Assert.Equal(computerName, result.ComputerName);
        }

        [Fact]
        public async Task DetectIdleAsync_WithHighActivity_ShouldReturnActiveResult()
        {
            // Arrange
            await _service.StartAsync();
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTCOMPUTER";

            var fileActivities = new List<FileActivityData>
            {
                new FileActivityData
                {
                    ActivityType = FileActivityType.FileWrite,
                    Confidence = 0.9,
                    Timestamp = DateTime.UtcNow
                }
            };

            _mockFileSystemMonitor.Setup(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()))
                .Returns(fileActivities);
            _mockPerformanceMonitor.Setup(m => m.GetProcessMetrics(processId))
                .Returns(new ProcessMetrics { ProcessId = processId, CpuUsage = 50.0 });
            _mockSystemActivityTracker.Setup(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()))
                .Returns(new List<SystemActivityEvent>());

            // Act
            var result = await _service.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            Assert.True(result.Confidence > _config.ConfidenceThreshold);
        }

        [Fact]
        public async Task DetectIdleAsync_WithDisabledMonitors_ShouldOnlyUseEnabledMonitors()
        {
            // Arrange
            _config.EnableFileSystemMonitoring = false;
            _config.EnablePerformanceMonitoring = false;
            _config.EnableSystemActivityTracking = true;

            var service = new ActivityMonitoringService(
                _mockLogger.Object,
                _mockFileSystemMonitor.Object,
                _mockPerformanceMonitor.Object,
                _mockSystemActivityTracker.Object,
                _config);

            await service.StartAsync();
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTCOMPUTER";

            _mockSystemActivityTracker.Setup(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()))
                .Returns(new List<SystemActivityEvent>());

            // Act
            var result = await service.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle);
            _mockFileSystemMonitor.Verify(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()), Times.Never);
            _mockPerformanceMonitor.Verify(m => m.GetProcessMetrics(It.IsAny<int>()), Times.Never);
            _mockSystemActivityTracker.Verify(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task DetectIdleAsync_WithException_ShouldLogErrorAndReturnDefaultResult()
        {
            // Arrange
            await _service.StartAsync();
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTCOMPUTER";

            _mockFileSystemMonitor.Setup(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()))
                .Throws(new InvalidOperationException("Test exception"));

            // Act
            var result = await _service.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle);
            Assert.Equal(0.0, result.Confidence);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error detecting idle state")),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetActivityReportAsync_WithValidProcess_ShouldReturnReport()
        {
            // Arrange
            await _service.StartAsync();
            var processId = 1234;

            var fileActivities = new List<FileActivityData>
            {
                new FileActivityData
                {
                    ActivityType = FileActivityType.FileWrite,
                    Confidence = 0.8,
                    Timestamp = DateTime.UtcNow
                }
            };

            _mockFileSystemMonitor.Setup(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()))
                .Returns(fileActivities);
            _mockPerformanceMonitor.Setup(m => m.GetProcessMetrics(processId))
                .Returns(new ProcessMetrics { ProcessId = processId, CpuUsage = 25.0 });
            _mockSystemActivityTracker.Setup(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()))
                .Returns(new List<SystemActivityEvent>());

            // Act
            var report = await _service.GetActivityReportAsync(processId);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(processId, report.ProcessId);
            Assert.Single(report.FileActivities);
            Assert.Equal(25.0, report.ProcessMetrics.CpuUsage);
        }

        [Fact]
        public async Task GetActivityReportAsync_WithNonexistentProcess_ShouldReturnEmptyReport()
        {
            // Arrange
            await _service.StartAsync();
            var processId = 9999;

            _mockFileSystemMonitor.Setup(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()))
                .Returns(new List<FileActivityData>());
            _mockPerformanceMonitor.Setup(m => m.GetProcessMetrics(processId))
                .Returns((ProcessMetrics)null);
            _mockSystemActivityTracker.Setup(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()))
                .Returns(new List<SystemActivityEvent>());

            // Act
            var report = await _service.GetActivityReportAsync(processId);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(processId, report.ProcessId);
            Assert.Empty(report.FileActivities);
            Assert.Null(report.ProcessMetrics);
        }

        [Fact]
        public void Dispose_ShouldStopServiceAndDisposeResources()
        {
            // Arrange
            _service.StartAsync().Wait();

            // Act
            _service.Dispose();

            // Assert
            Assert.False(_service.IsRunning);
            _mockFileSystemMonitor.Verify(m => m.StopAsync(), Times.Once);
            _mockPerformanceMonitor.Verify(m => m.StopAsync(), Times.Once);
            _mockSystemActivityTracker.Verify(m => m.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task DetectIdleAsync_WithMixedActivitySources_ShouldCalculateCorrectConfidence()
        {
            // Arrange
            await _service.StartAsync();
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTCOMPUTER";

            var fileActivities = new List<FileActivityData>
            {
                new FileActivityData { ActivityType = FileActivityType.FileWrite, Confidence = 0.6, Timestamp = DateTime.UtcNow }
            };

            var systemActivities = new List<SystemActivityEvent>
            {
                new SystemActivityEvent { ActivityType = SystemActivityType.Keyboard, Confidence = 0.8, Timestamp = DateTime.UtcNow }
            };

            _mockFileSystemMonitor.Setup(m => m.GetRecentFileActivities(It.IsAny<TimeSpan>()))
                .Returns(fileActivities);
            _mockPerformanceMonitor.Setup(m => m.GetProcessMetrics(processId))
                .Returns(new ProcessMetrics { ProcessId = processId, CpuUsage = 10.0 });
            _mockSystemActivityTracker.Setup(m => m.GetRecentSystemActivity(It.IsAny<TimeSpan>()))
                .Returns(systemActivities);

            // Act
            var result = await _service.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            // Should combine activities from multiple sources
            Assert.True(result.Confidence > 0.6 && result.Confidence <= 1.0);
            Assert.Equal(3, result.ActivitySources.Count); // Should include all sources
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnCombinedStatistics()
        {
            // Arrange
            await _service.StartAsync();

            // Act
            var stats = await _service.GetStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("ActivityMonitoringService", stats.ComponentName);
            Assert.True(stats.StartTime <= DateTime.UtcNow);
        }
    }
}