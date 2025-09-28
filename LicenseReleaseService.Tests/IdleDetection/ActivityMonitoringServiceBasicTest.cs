using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class ActivityMonitoringServiceBasicTest
    {
        [Fact]
        public async Task ActivityMonitoringService_Initialization_ShouldWork()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ActivityMonitoringService>>();
            var mockFileSystemMonitor = new Mock<FileSystemMonitor>(
                new FileSystemMonitorConfig(), Mock.Of<ILogger<FileSystemMonitor>>());
            var mockPerformanceMonitor = new Mock<PerformanceMonitor>(
                new PerformanceMonitorConfig(), Mock.Of<ILogger<PerformanceMonitor>>());
            var mockSystemActivityTracker = new Mock<SystemActivityTracker>(
                new SystemActivityTrackerConfig(), Mock.Of<ILogger<SystemActivityTracker>>());

            var config = new ActivityMonitoringConfig
            {
                IsEnabled = true,
                IdleThresholdSeconds = 300,
                EnableFileSystemMonitoring = true,
                EnablePerformanceMonitoring = true,
                EnableSystemActivityMonitoring = true
            };

            var service = new ActivityMonitoringService(
                mockLogger.Object,
                mockFileSystemMonitor.Object,
                mockPerformanceMonitor.Object,
                mockSystemActivityTracker.Object,
                config);

            // Act & Assert
            Assert.NotNull(service);
            Assert.Equal("ActivityMonitoringService", service.Name);
            Assert.Equal(15, service.Priority);
            Assert.True(service.IsEnabled);
        }

        [Fact]
        public async Task ActivityMonitoringService_ValidateInterfaceImplementation()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ActivityMonitoringService>>();
            var mockFileSystemMonitor = new Mock<FileSystemMonitor>(
                new FileSystemMonitorConfig(), Mock.Of<ILogger<FileSystemMonitor>>());
            var mockPerformanceMonitor = new Mock<PerformanceMonitor>(
                new PerformanceMonitorConfig(), Mock.Of<ILogger<PerformanceMonitor>>());
            var mockSystemActivityTracker = new Mock<SystemActivityTracker>(
                new SystemActivityTrackerConfig(), Mock.Of<ILogger<SystemActivityTracker>>());

            var config = new ActivityMonitoringConfig();

            var service = new ActivityMonitoringService(
                mockLogger.Object,
                mockFileSystemMonitor.Object,
                mockPerformanceMonitor.Object,
                mockSystemActivityTracker.Object,
                config);

            // Act & Assert
            Assert.IsAssignableFrom<IIdleDetector>(service);
            Assert.IsAssignableFrom<IDisposable>(service);
        }
    }
}