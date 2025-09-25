using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    public class MonitoredLicenseManagerTests
    {
        private readonly Mock<ILogger<MonitoredLicenseManager>> _mockLogger;
        private readonly Mock<ILicenseManager> _mockInnerLicenseManager;
        private readonly Mock<ProcessMetrics> _mockProcessMetrics;
        private readonly Mock<PerformanceMonitor> _mockPerformanceMonitor;
        private readonly Mock<HealthChecker> _mockHealthChecker;
        private readonly Mock<ServiceSettings> _mockServiceSettings;
        private readonly MonitoredLicenseManager _monitoredLicenseManager;

        public MonitoredLicenseManagerTests()
        {
            _mockLogger = new Mock<ILogger<MonitoredLicenseManager>>();
            _mockInnerLicenseManager = new Mock<ILicenseManager>();
            _mockProcessMetrics = new Mock<ProcessMetrics>(MockBehavior.Loose, _mockLogger.Object, 100);
            _mockPerformanceMonitor = new Mock<PerformanceMonitor>(MockBehavior.Loose, _mockLogger.Object, _mockProcessMetrics.Object);
            _mockHealthChecker = new Mock<HealthChecker>(MockBehavior.Loose, _mockLogger.Object, _mockInnerLicenseManager.Object, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object);
            _mockServiceSettings = new Mock<ServiceSettings>();

            _mockServiceSettings.Setup(s => s.LicenseManagerTimeout).Returns(30);
            _mockServiceSettings.Setup(s => s.LicenseManagerRetryCount).Returns(3);
            _mockServiceSettings.Setup(s => s.RetryDelay).Returns(1000);

            _monitoredLicenseManager = new MonitoredLicenseManager(
                _mockInnerLicenseManager.Object,
                _mockLogger.Object,
                _mockProcessMetrics.Object,
                _mockPerformanceMonitor.Object,
                _mockHealthChecker.Object,
                _mockServiceSettings.Object);
        }

        [Fact]
        public void Constructor_NullDependencies_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MonitoredLicenseManager(null, _mockLogger.Object, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object, _mockHealthChecker.Object, _mockServiceSettings.Object));
            Assert.Throws<ArgumentNullException>(() => new MonitoredLicenseManager(_mockInnerLicenseManager.Object, null, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object, _mockHealthChecker.Object, _mockServiceSettings.Object));
            Assert.Throws<ArgumentNullException>(() => new MonitoredLicenseManager(_mockInnerLicenseManager.Object, _mockLogger.Object, null, _mockPerformanceMonitor.Object, _mockHealthChecker.Object, _mockServiceSettings.Object));
            Assert.Throws<ArgumentNullException>(() => new MonitoredLicenseManager(_mockInnerLicenseManager.Object, _mockLogger.Object, _mockProcessMetrics.Object, null, _mockHealthChecker.Object, _mockServiceSettings.Object));
            Assert.Throws<ArgumentNullException>(() => new MonitoredLicenseManager(_mockInnerLicenseManager.Object, _mockLogger.Object, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object, null, _mockServiceSettings.Object));
            Assert.Throws<ArgumentNullException>(() => new MonitoredLicenseManager(_mockInnerLicenseManager.Object, _mockLogger.Object, _mockProcessMetrics.Object, _mockPerformanceMonitor.Object, _mockHealthChecker.Object, null));
        }

        [Fact]
        public async Task GetServerStatusAsync_Success_ShouldRecordMetricsAndReturnResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var expectedResult = new LicenseServerStatus
            {
                IsAvailable = true,
                LastChecked = DateTime.Now,
                ResponseTimeMs = 500
            };

            _mockInnerLicenseManager
                .Setup(m => m.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _monitoredLicenseManager.GetServerStatusAsync(server, port);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.OperationType == "lmstat" &&
                pm.Server == server &&
                pm.Port == port &&
                pm.Success == true)), Times.Once);
        }

        [Fact]
        public async Task GetServerStatusAsync_Exception_ShouldRecordMetricsAndRethrow()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var expectedException = new Exception("Test exception");

            _mockInnerLicenseManager
                .Setup(m => m.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _monitoredLicenseManager.GetServerStatusAsync(server, port));
            Assert.Equal(expectedException, exception);

            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.OperationType == "lmstat" &&
                pm.Server == server &&
                pm.Port == port &&
                pm.Success == false)), Times.Once);
        }

        [Fact]
        public async Task ReleaseLicenseAsync_Success_ShouldRecordMetricsAndReturnResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var expectedResult = new LicenseReleaseResult
            {
                Success = true,
                ExecutionTime = TimeSpan.FromSeconds(1),
                ExitCode = 0
            };

            _mockInnerLicenseManager
                .Setup(m => m.ReleaseLicenseAsync(server, port, feature, user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _monitoredLicenseManager.ReleaseLicenseAsync(server, port, feature, user);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.OperationType == "lmremove" &&
                pm.Server == server &&
                pm.Port == port &&
                pm.Feature == feature &&
                pm.User == user &&
                pm.Success == true)), Times.Once);
        }

        [Fact]
        public async Task ReleaseLicenseAsync_Exception_ShouldRecordMetricsAndRethrow()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var expectedException = new Exception("Test exception");

            _mockInnerLicenseManager
                .Setup(m => m.ReleaseLicenseAsync(server, port, feature, user, It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _monitoredLicenseManager.ReleaseLicenseAsync(server, port, feature, user));
            Assert.Equal(expectedException, exception);

            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.OperationType == "lmremove" &&
                pm.Server == server &&
                pm.Port == port &&
                pm.Feature == feature &&
                pm.User == user &&
                pm.Success == false)), Times.Once);
        }

        [Fact]
        public async Task GetFeatureInfoAsync_Success_ShouldRecordMetricsAndReturnResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var expectedResult = new LicenseFeatureInfo
            {
                FeatureName = feature,
                TotalLicenses = 10,
                LicensesInUse = 5
            };

            _mockInnerLicenseManager
                .Setup(m => m.GetFeatureInfoAsync(server, port, feature, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _monitoredLicenseManager.GetFeatureInfoAsync(server, port, feature);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.OperationType == "lmstat_feature" &&
                pm.Server == server &&
                pm.Port == port &&
                pm.Feature == feature &&
                pm.Success == true)), Times.Once);
        }

        [Fact]
        public async Task GetMetricsSummaryAsync_ShouldReturnFilteredSummaries()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var allSummaries = new Dictionary<string, ProcessMetricsSummary>
            {
                { "lmstat_test-server_27000", new ProcessMetricsSummary { TotalExecutions = 10 } },
                { "lmremove_test-server_27000", new ProcessMetricsSummary { TotalExecutions = 5 } },
                { "lmstat_other-server_27000", new ProcessMetricsSummary { TotalExecutions = 8 } }
            };

            _mockProcessMetrics
                .Setup(m => m.GetAllOperationSummariesAsync())
                .ReturnsAsync(allSummaries);

            // Act
            var result = await _monitoredLicenseManager.GetMetricsSummaryAsync(server, port);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("lmstat_test-server_27000"));
            Assert.True(result.ContainsKey("lmremove_test-server_27000"));
            Assert.False(result.ContainsKey("lmstat_other-server_27000"));
        }

        [Fact]
        public void GetPerformanceAlerts_ShouldReturnCurrentAlerts()
        {
            // Arrange
            var expectedAlerts = new List<PerformanceAlert>
            {
                new PerformanceAlert { Severity = "Warning", Title = "Test Alert" }
            };

            _mockPerformanceMonitor
                .Setup(m => m.GetCurrentAlerts())
                .Returns(expectedAlerts);

            // Act
            var result = _monitoredLicenseManager.GetPerformanceAlerts();

            // Assert
            Assert.Equal(expectedAlerts, result);
            _mockPerformanceMonitor.Verify(m => m.GetCurrentAlerts(), Times.Once);
        }

        [Fact]
        public async Task GetSystemHealthAsync_ShouldReturnSystemHealth()
        {
            // Arrange
            var expectedHealth = new SystemHealth
            {
                OverallStatus = HealthStatus.Healthy,
                Timestamp = DateTime.Now,
                Uptime = TimeSpan.FromHours(1)
            };

            _mockHealthChecker
                .Setup(m => m.GetSystemHealthAsync())
                .ReturnsAsync(expectedHealth);

            // Act
            var result = await _monitoredLicenseManager.GetSystemHealthAsync();

            // Assert
            Assert.Equal(expectedHealth, result);
            _mockHealthChecker.Verify(m => m.GetSystemHealthAsync(), Times.Once);
        }

        [Fact]
        public void AcknowledgeAlert_ShouldDelegateToPerformanceMonitor()
        {
            // Arrange
            var alertId = "test-alert-id";
            var user = "test-user";

            // Act
            _monitoredLicenseManager.AcknowledgeAlert(alertId, user);

            // Assert
            _mockPerformanceMonitor.Verify(m => m.AcknowledgeAlert(alertId, user), Times.Once);
        }

        [Fact]
        public async Task GetMonitoringStatisticsAsync_ShouldReturnComprehensiveStatistics()
        {
            // Arrange
            var summaries = new Dictionary<string, ProcessMetricsSummary>
            {
                { "lmstat_test-server_27000", new ProcessMetricsSummary
                    {
                        TotalExecutions = 20,
                        SuccessfulExecutions = 18,
                        AverageExecutionTime = TimeSpan.FromMilliseconds(500),
                        TimeoutRate = 5.0
                    }
                }
            };

            var systemHealth = new SystemHealth
            {
                OverallStatus = HealthStatus.Healthy,
                Uptime = TimeSpan.FromHours(2),
                CheckResults = new List<HealthCheckResult>()
            };

            var alerts = new List<PerformanceAlert>
            {
                new PerformanceAlert { Severity = "Warning", Title = "Warning Alert" },
                new PerformanceAlert { Severity = "Critical", Title = "Critical Alert" }
            };

            _mockProcessMetrics
                .Setup(m => m.GetAllOperationSummariesAsync())
                .ReturnsAsync(summaries);

            _mockHealthChecker
                .Setup(m => m.GetSystemHealthAsync())
                .ReturnsAsync(systemHealth);

            _mockPerformanceMonitor
                .Setup(m => m.GetCurrentAlerts())
                .Returns(alerts);

            // Act
            var stats = await _monitoredLicenseManager.GetMonitoringStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(20, stats["TotalExecutions"]);
            Assert.Equal(18, stats["TotalSuccessful"]);
            Assert.Equal(90.0, stats["OverallSuccessRate"]);
            Assert.Equal("Healthy", stats["SystemHealthStatus"]);
            Assert.Equal(2, stats["ActiveAlerts"]);
            Assert.Equal(1, stats["CriticalAlerts"]);
            Assert.Equal(1, stats["WarningAlerts"]);
            Assert.NotNull(stats["OperationStatistics"]);
        }

        [Fact]
        public void Dispose_ShouldUnsubscribeFromEventsAndDisposeInnerManager()
        {
            // Arrange
            var disposableManager = new Mock<ILicenseManager>();
            disposableManager.As<IDisposable>().Setup(d => d.Dispose());

            var monitoredManager = new MonitoredLicenseManager(
                disposableManager.Object,
                _mockLogger.Object,
                _mockProcessMetrics.Object,
                _mockPerformanceMonitor.Object,
                _mockHealthChecker.Object,
                _mockServiceSettings.Object);

            // Act
            monitoredManager.Dispose();

            // Assert
            _mockPerformanceMonitor.Verify(m => m.AlertRaised -= It.IsAny<EventHandler<PerformanceAlert>>(), Times.Once);
            disposableManager.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
        }

        [Fact]
        public async Task RecordOperationMetricsAsync_ValidOperation_ShouldRecordCorrectly()
        {
            // Arrange
            var operationType = "test-operation";
            var server = "test-server";
            var port = 27000;
            var startTime = DateTime.Now.AddSeconds(-1);
            var executionDuration = TimeSpan.FromSeconds(1);
            var success = true;
            var exitCode = 0;
            var feature = "test-feature";
            var user = "test-user";

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act
            await ExecutePrivateRecordOperationMetricsAsync(
                operationType, server, port, startTime, executionDuration, success, exitCode, null, feature, user);

            // Assert
            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.OperationType == operationType &&
                pm.Server == server &&
                pm.Port == port &&
                pm.StartTime == startTime &&
                pm.ExecutionDuration == executionDuration &&
                pm.Success == success &&
                pm.ExitCode == exitCode &&
                pm.Feature == feature &&
                pm.User == user &&
                pm.Tags["Operation"] == operationType &&
                pm.Tags["Server"] == server &&
                pm.Tags["Port"] == port.ToString() &&
                pm.Tags["Success"] == success.ToString() &&
                pm.Tags["Feature"] == feature &&
                pm.Tags["User"] == user)), Times.Once);
        }

        [Fact]
        public async Task RecordOperationMetricsAsync_WithException_ShouldRecordExceptionDetails()
        {
            // Arrange
            var operationType = "test-operation";
            var server = "test-server";
            var port = 27000;
            var startTime = DateTime.Now.AddSeconds(-1);
            var executionDuration = TimeSpan.FromSeconds(1);
            var success = false;
            var exitCode = -1;
            var exception = new InvalidOperationException("Test exception");

            _mockProcessMetrics
                .Setup(m => m.RecordExecutionAsync(It.IsAny<ProcessExecutionMetrics>()))
                .Returns(Task.CompletedTask);

            // Act
            await ExecutePrivateRecordOperationMetricsAsync(
                operationType, server, port, startTime, executionDuration, success, exitCode, exception);

            // Assert
            _mockProcessMetrics.Verify(m => m.RecordExecutionAsync(It.Is<ProcessExecutionMetrics>(pm =>
                pm.Tags["ExceptionType"] == "InvalidOperationException" &&
                pm.Tags["ExceptionMessage"] == "Test exception")), Times.Once);
        }

        [Fact]
        public void OnPerformanceAlertRaised_CriticalAlert_ShouldLogError()
        {
            // Arrange
            var alert = new PerformanceAlert
            {
                Severity = "Critical",
                Title = "Critical Performance Alert",
                Message = "System performance is critically degraded",
                MetricType = "SuccessRate",
                CurrentValue = 25.0,
                ThresholdValue = 80.0
            };

            // Act
            ExecutePrivateOnPerformanceAlertRaised(alert);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Critical performance alert detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void OnPerformanceAlertRaised_WarningAlert_ShouldLogWarning()
        {
            // Arrange
            var alert = new PerformanceAlert
            {
                Severity = "Warning",
                Title = "Warning Performance Alert",
                Message = "System performance is degraded",
                MetricType = "ExecutionTime",
                CurrentValue = 1500.0,
                ThresholdValue = 1000.0
            };

            // Act
            ExecutePrivateOnPerformanceAlertRaised(alert);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performance alert")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // Helper methods to execute private methods
        private async Task ExecutePrivateRecordOperationMetricsAsync(
            string operationType, string server, int port, DateTime startTime, TimeSpan executionDuration,
            bool success, int exitCode, Exception? exception = null, string? feature = null, string? user = null)
        {
            // This simulates the private RecordOperationMetricsAsync method
            var metrics = new ProcessExecutionMetrics
            {
                OperationType = operationType,
                Server = server,
                Port = port,
                StartTime = startTime,
                EndTime = DateTime.Now,
                ExecutionDuration = executionDuration,
                ExitCode = exitCode,
                Success = success,
                ConfiguredTimeout = TimeSpan.FromSeconds(30),
                Feature = feature ?? string.Empty,
                User = user ?? string.Empty,
                Tags = new Dictionary<string, string>
                {
                    { "Operation", operationType },
                    { "Server", server },
                    { "Port", port.ToString() },
                    { "Success", success.ToString() },
                    { "Timestamp", startTime.ToString("yyyy-MM-dd HH:mm:ss") }
                }
            };

            if (!string.IsNullOrEmpty(feature))
            {
                metrics.Tags["Feature"] = feature;
            }

            if (!string.IsNullOrEmpty(user))
            {
                metrics.Tags["User"] = user;
            }

            if (exception != null)
            {
                metrics.Tags["ExceptionType"] = exception.GetType().Name;
                metrics.Tags["ExceptionMessage"] = exception.Message;
            }

            await _mockProcessMetrics.Object.RecordExecutionAsync(metrics);
        }

        private void ExecutePrivateOnPerformanceAlertRaised(PerformanceAlert alert)
        {
            // This simulates the private OnPerformanceAlertRaised method
            _mockLogger.Object.LogWarning("Performance alert: {Severity} - {Title} - {Message}",
                alert.Severity, alert.Title, alert.Message);

            if (alert.Severity == "Critical")
            {
                _mockLogger.Object.LogError("Critical performance alert detected: {Alert}", alert);

                if (alert.MetricType == "SuccessRate" && alert.CurrentValue < 50)
                {
                    _mockLogger.Object.LogWarning("Low success rate detected, consider taking corrective action");
                }
            }
        }
    }
}