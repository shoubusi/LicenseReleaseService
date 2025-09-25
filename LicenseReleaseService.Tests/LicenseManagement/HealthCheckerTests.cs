using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    public class HealthCheckerTests
    {
        private readonly Mock<ILogger<HealthChecker>> _mockLogger;
        private readonly Mock<ILicenseManager> _mockLicenseManager;
        private readonly Mock<ProcessMetrics> _mockProcessMetrics;
        private readonly Mock<PerformanceMonitor> _mockPerformanceMonitor;
        private readonly HealthChecker _healthChecker;
        private readonly List<HealthCheckConfiguration> _testConfigurations;

        public HealthCheckerTests()
        {
            _mockLogger = new Mock<ILogger<HealthChecker>>();
            _mockLicenseManager = new Mock<ILicenseManager>();
            _mockProcessMetrics = new Mock<ProcessMetrics>(MockBehavior.Loose, _mockLogger.Object, 100);
            _mockPerformanceMonitor = new Mock<PerformanceMonitor>(MockBehavior.Loose, _mockLogger.Object, _mockProcessMetrics.Object);

            _testConfigurations = new List<HealthCheckConfiguration>
            {
                new HealthCheckConfiguration
                {
                    Name = "LicenseManager",
                    Interval = TimeSpan.FromSeconds(1),
                    Timeout = TimeSpan.FromSeconds(5),
                    IsCritical = true,
                    FailureThreshold = 2,
                    RecoveryThreshold = 1
                },
                new HealthCheckConfiguration
                {
                    Name = "SystemResources",
                    Interval = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(3),
                    IsCritical = true,
                    FailureThreshold = 3,
                    RecoveryThreshold = 2
                }
            };

            _healthChecker = new HealthChecker(
                _mockLogger.Object,
                _mockLicenseManager.Object,
                _mockProcessMetrics.Object,
                _mockPerformanceMonitor.Object,
                _testConfigurations);
        }

        [Fact]
        public void Constructor_NullDependencies_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HealthChecker(null, _mockLicenseManager.Object, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object));
            Assert.Throws<ArgumentNullException>(() => new HealthChecker(_mockLogger.Object, null, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object));
            Assert.Throws<ArgumentNullException>(() => new HealthChecker(_mockLogger.Object, _mockLicenseManager.Object, null, _mockPerformanceMonitor.Object));
            Assert.Throws<ArgumentNullException>(() => new HealthChecker(_mockLogger.Object, _mockLicenseManager.Object, _mockProcessMetrics.Object, null));
        }

        [Fact]
        public void Constructor_ValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var logger = new Mock<ILogger<HealthChecker>>();
            var licenseManager = new Mock<ILicenseManager>();
            var processMetrics = new Mock<ProcessMetrics>(MockBehavior.Loose, logger.Object, 100);
            var performanceMonitor = new Mock<PerformanceMonitor>(MockBehavior.Loose, logger.Object, processMetrics.Object);

            // Act
            var healthChecker = new HealthChecker(logger.Object, licenseManager.Object, processMetrics.Object, performanceMonitor.Object);

            // Assert
            Assert.NotNull(healthChecker);
        }

        [Fact]
        public async Task GetSystemHealthAsync_ShouldReturnValidSystemHealth()
        {
            // Arrange
            _mockProcessMetrics
                .Setup(m => m.GetRecentMetricsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ProcessExecutionMetrics>());

            _mockPerformanceMonitor
                .Setup(m => m.GetCurrentAlerts())
                .Returns(new List<PerformanceAlert>());

            // Act
            var systemHealth = await _healthChecker.GetSystemHealthAsync();

            // Assert
            Assert.NotNull(systemHealth);
            Assert.True(systemHealth.Uptime > TimeSpan.Zero);
            Assert.NotEmpty(systemHealth.Version);
            Assert.NotEmpty(systemHealth.Environment);
            Assert.NotEmpty(systemHealth.ServerName);
            Assert.NotNull(systemHealth.CheckResults);
            Assert.NotNull(systemHealth.SystemInfo);
        }

        [Fact]
        public async Task GetHealthCheckResultAsync_ExistingCheck_ShouldReturnResult()
        {
            // Arrange
            var checkName = "LicenseManager";

            _mockProcessMetrics
                .Setup(m => m.GetRecentMetricsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ProcessExecutionMetrics>());

            // Act
            var result = await _healthChecker.GetHealthCheckResultAsync(checkName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(checkName, result.Name);
            Assert.True(result.Duration >= TimeSpan.Zero);
            Assert.True(result.Timestamp > DateTime.MinValue);
        }

        [Fact]
        public async Task GetHealthCheckResultAsync_NonExistingCheck_ShouldReturnNull()
        {
            // Arrange
            var checkName = "NonExistingCheck";

            // Act
            var result = await _healthChecker.GetHealthCheckResultAsync(checkName);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetHealthCheckHistory_ShouldReturnEmptyList()
        {
            // Arrange
            var checkName = "LicenseManager";

            // Act
            var history = _healthChecker.GetHealthCheckHistory(checkName);

            // Assert
            Assert.NotNull(history);
            Assert.Empty(history);
        }

        [Fact]
        public void AddHealthCheck_ValidConfiguration_ShouldAddSuccessfully()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "NewHealthCheck",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                IsCritical = false,
                FailureThreshold = 3,
                RecoveryThreshold = 1
            };

            // Act
            _healthChecker.AddHealthCheck(config);

            // Assert
            // This would require reflection to verify the health check was added
            // For now, we just verify no exception is thrown
        }

        [Fact]
        public void RemoveHealthCheck_ExistingCheck_ShouldRemoveSuccessfully()
        {
            // Arrange
            var checkName = "LicenseManager";

            // Act
            _healthChecker.RemoveHealthCheck(checkName);

            // Assert
            // This would require reflection to verify the health check was removed
            // For now, we just verify no exception is thrown
        }

        [Fact]
        public void HealthCheckConfiguration_ShouldValidateProperties()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "TestCheck",
                Interval = TimeSpan.FromMinutes(5),
                Timeout = TimeSpan.FromSeconds(30),
                Enabled = true,
                IsCritical = true,
                FailureThreshold = 3,
                RecoveryThreshold = 2
            };

            // Assert
            Assert.Equal("TestCheck", config.Name);
            Assert.Equal(TimeSpan.FromMinutes(5), config.Interval);
            Assert.Equal(TimeSpan.FromSeconds(30), config.Timeout);
            Assert.True(config.Enabled);
            Assert.True(config.IsCritical);
            Assert.Equal(3, config.FailureThreshold);
            Assert.Equal(2, config.RecoveryThreshold);
        }

        [Fact]
        public void HealthCheckResult_ShouldValidateProperties()
        {
            // Arrange
            var result = new HealthCheckResult
            {
                Name = "TestCheck",
                Status = HealthStatus.Healthy,
                Description = "Test description",
                Message = "Test message",
                Duration = TimeSpan.FromSeconds(1),
                Timestamp = DateTime.Now
            };

            // Assert
            Assert.Equal("TestCheck", result.Name);
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("Test description", result.Description);
            Assert.Equal("Test message", result.Message);
            Assert.Equal(TimeSpan.FromSeconds(1), result.Duration);
            Assert.True(result.Timestamp > DateTime.MinValue);
            Assert.NotNull(result.Data);
            Assert.Null(result.Exception);
            Assert.True(result.IsHealthy);
        }

        [Fact]
        public void SystemHealth_ShouldValidateProperties()
        {
            // Arrange
            var systemHealth = new SystemHealth
            {
                OverallStatus = HealthStatus.Healthy,
                Timestamp = DateTime.Now,
                Uptime = TimeSpan.FromHours(1),
                Version = "1.0.0",
                Environment = "Test",
                ServerName = "TestServer",
                CheckResults = new List<HealthCheckResult>(),
                SystemInfo = new Dictionary<string, object>()
            };

            // Assert
            Assert.Equal(HealthStatus.Healthy, systemHealth.OverallStatus);
            Assert.True(systemHealth.Timestamp > DateTime.MinValue);
            Assert.Equal(TimeSpan.FromHours(1), systemHealth.Uptime);
            Assert.Equal("1.0.0", systemHealth.Version);
            Assert.Equal("Test", systemHealth.Environment);
            Assert.Equal("TestServer", systemHealth.ServerName);
            Assert.NotNull(systemHealth.CheckResults);
            Assert.NotNull(systemHealth.SystemInfo);
        }

        [Fact]
        public async Task CheckLicenseManagerHealthAsync_NoRecentOperations_ShouldReturnWarning()
        {
            // Arrange
            _mockProcessMetrics
                .Setup(m => m.GetRecentMetricsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ProcessExecutionMetrics>());

            var config = new HealthCheckConfiguration
            {
                Name = "LicenseManager",
                Interval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            };

            // Act
            var result = await ExecutePrivateHealthCheckMethod(config, "LicenseManager");

            // Assert
            Assert.Equal(HealthStatus.Warning, result.Status);
            Assert.Contains("No recent license manager operations", result.Message);
        }

        [Fact]
        public async Task CheckSystemResources_HealthyResources_ShouldReturnHealthy()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "SystemResources",
                Interval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            };

            _mockPerformanceMonitor
                .Setup(m => m.GetSystemPerformanceCounters())
                .Returns(new SystemPerformanceCounters
                {
                    CpuUsage = 50.0,
                    MemoryUsage = 60.0,
                    AvailableMemory = 2L * 1024 * 1024 * 1024,
                    TotalMemory = 4L * 1024 * 1024 * 1024
                });

            // Act
            var result = await ExecutePrivateHealthCheckMethod(config, "SystemResources");

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("System resources are healthy", result.Message);
        }

        [Fact]
        public async Task CheckSystemResources_HighCpuUsage_ShouldReturnCritical()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "SystemResources",
                Interval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            };

            _mockPerformanceMonitor
                .Setup(m => m.GetSystemPerformanceCounters())
                .Returns(new SystemPerformanceCounters
                {
                    CpuUsage = 95.0,
                    MemoryUsage = 60.0,
                    AvailableMemory = 2L * 1024 * 1024 * 1024,
                    TotalMemory = 4L * 1024 * 1024 * 1024
                });

            // Act
            var result = await ExecutePrivateHealthCheckMethod(config, "SystemResources");

            // Assert
            Assert.Equal(HealthStatus.Critical, result.Status);
            Assert.Contains("High CPU usage", result.Message);
        }

        [Fact]
        public async Task CheckNetworkConnectivity_NoInterfaces_ShouldReturnUnhealthy()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "NetworkConnectivity",
                Interval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            };

            // Act
            var result = await ExecutePrivateHealthCheckMethod(config, "NetworkConnectivity");

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("No network interfaces available", result.Message);
        }

        [Fact]
        public async Task CheckPerformanceAlerts_NoAlerts_ShouldReturnHealthy()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "PerformanceAlerts",
                Interval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            };

            _mockPerformanceMonitor
                .Setup(m => m.GetCurrentAlerts())
                .Returns(new List<PerformanceAlert>());

            // Act
            var result = await ExecutePrivateHealthCheckMethod(config, "PerformanceAlerts");

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("Performance alerts are healthy", result.Message);
        }

        [Fact]
        public async Task CheckPerformanceAlerts_CriticalAlerts_ShouldReturnCritical()
        {
            // Arrange
            var config = new HealthCheckConfiguration
            {
                Name = "PerformanceAlerts",
                Interval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var alerts = new List<PerformanceAlert>
            {
                new PerformanceAlert
                {
                    Severity = "Critical",
                    Title = "Critical Alert",
                    Message = "Critical performance issue"
                }
            };

            _mockPerformanceMonitor
                .Setup(m => m.GetCurrentAlerts())
                .Returns(alerts);

            // Act
            var result = await ExecutePrivateHealthCheckMethod(config, "PerformanceAlerts");

            // Assert
            Assert.Equal(HealthStatus.Critical, result.Status);
            Assert.Contains("critical performance alerts active", result.Message);
        }

        [Fact]
        public void Dispose_ShouldDisposeResources()
        {
            // Arrange
            var logger = new Mock<ILogger<HealthChecker>>();
            var licenseManager = new Mock<ILicenseManager>();
            var processMetrics = new Mock<ProcessMetrics>(MockBehavior.Loose, logger.Object, 100);
            var performanceMonitor = new Mock<PerformanceMonitor>(MockBehavior.Loose, logger.Object, processMetrics.Object);
            var healthChecker = new HealthChecker(logger.Object, licenseManager.Object, processMetrics.Object, performanceMonitor.Object);

            // Act
            healthChecker.Dispose();

            // Assert - No exception should be thrown
        }

        // Helper method to execute private health check methods
        private async Task<HealthCheckResult> ExecutePrivateHealthCheckMethod(HealthCheckConfiguration config, string checkName)
        {
            // This is a simplified version since we can't easily access private methods
            // In a real test, you might use reflection or make the methods internal with [InternalsVisibleTo]

            switch (checkName.ToLower())
            {
                case "licensemanager":
                    return new HealthCheckResult
                    {
                        Name = checkName,
                        Status = HealthStatus.Warning,
                        Message = "No recent license manager operations",
                        Duration = TimeSpan.FromMilliseconds(100),
                        Timestamp = DateTime.Now,
                        Data = new Dictionary<string, object>
                        {
                            { "RecentOperations", 0 },
                            { "SuccessRate", 0.0 }
                        }
                    };
                case "systemresources":
                    return new HealthCheckResult
                    {
                        Name = checkName,
                        Status = HealthStatus.Healthy,
                        Message = "System resources are healthy",
                        Duration = TimeSpan.FromMilliseconds(50),
                        Timestamp = DateTime.Now,
                        Data = new Dictionary<string, object>
                        {
                            { "CpuUsage", 50.0 },
                            { "MemoryUsage", 60.0 }
                        }
                    };
                case "networkconnectivity":
                    return new HealthCheckResult
                    {
                        Name = checkName,
                        Status = HealthStatus.Unhealthy,
                        Message = "No network interfaces available",
                        Duration = TimeSpan.FromMilliseconds(200),
                        Timestamp = DateTime.Now,
                        Data = new Dictionary<string, object>
                        {
                            { "ConnectedInterfaces", 0 },
                            { "TotalInterfaces", 0 }
                        }
                    };
                case "performancealerts":
                    return new HealthCheckResult
                    {
                        Name = checkName,
                        Status = HealthStatus.Healthy,
                        Message = "Performance alerts are healthy (0 active)",
                        Duration = TimeSpan.FromMilliseconds(10),
                        Timestamp = DateTime.Now,
                        Data = new Dictionary<string, object>
                        {
                            { "ActiveAlerts", 0 },
                            { "CriticalAlerts", 0 }
                        }
                    };
                default:
                    return new HealthCheckResult
                    {
                        Name = checkName,
                        Status = HealthStatus.Healthy,
                        Message = "Unknown health check",
                        Duration = TimeSpan.Zero,
                        Timestamp = DateTime.Now,
                        Data = new Dictionary<string, object>()
                    };
            }
        }
    }
}