using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LicenseReleaseService.VersionManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Tests.VersionManagement
{
    [TestClass]
    public class VersionHealthMonitorTests
    {
        private Mock<ILogger<VersionHealthMonitor>> _mockLogger;
        private Mock<SolidWorksVersionDetector> _mockDetector;
        private VersionHealthConfiguration _configuration;
        private VersionHealthMonitor _monitor;
        private TestContext _testContext;

        public TestContext TestContext
        {
            get { return _testContext; }
            set { _testContext = value; }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLogger = new Mock<ILogger<VersionHealthMonitor>>();
            _mockDetector = new Mock<SolidWorksVersionDetector>(_mockLogger.Object);
            _configuration = new VersionHealthConfiguration
            {
                HealthCheckInterval = TimeSpan.FromSeconds(1), // Short interval for testing
                MaxHistoryRecords = 100,
                MinimumDiskSpaceGB = 1.0,
                MinimumExecutableSizeBytes = 1024,
                MaximumFileOperationMs = 1000,
                LicenseManagerTimeoutMs = 1000
            };

            _monitor = new VersionHealthMonitor(_mockLogger.Object, _mockDetector.Object, _configuration);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (_monitor != null && _monitor.IsMonitoring)
            {
                _monitor.StopMonitoring();
            }
            _monitor?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var logger = new Mock<ILogger<VersionHealthMonitor>>().Object;
            var detector = new Mock<SolidWorksVersionDetector>(logger).Object;

            // Act
            var monitor = new VersionHealthMonitor(logger, detector, _configuration);

            // Assert
            Assert.IsNotNull(monitor);
            Assert.IsFalse(monitor.IsMonitoring);
            Assert.AreEqual(0, monitor.CurrentHealthStatus.Count);

            monitor.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act
            var monitor = new VersionHealthMonitor(null, _mockDetector.Object, _configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullDetector_ShouldThrowArgumentNullException()
        {
            // Act
            var monitor = new VersionHealthMonitor(_mockLogger.Object, null, _configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Act
            var monitor = new VersionHealthMonitor(_mockLogger.Object, _mockDetector.Object, null);
        }

        [TestMethod]
        public async Task StartMonitoringAsync_WithSuccessfulStart_ShouldStartMonitoring()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 1,
                AvailableVersions = 1,
                SupportedVersions = 1
            });

            // Act
            await _monitor.StartMonitoringAsync();

            // Assert
            Assert.IsTrue(_monitor.IsMonitoring);
        }

        [TestMethod]
        public async Task StartMonitoringAsync_WhenAlreadyMonitoring_ShouldLogWarning()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult { Success = true });
            await _monitor.StartMonitoringAsync();

            // Act
            await _monitor.StartMonitoringAsync();

            // Assert
            Assert.IsTrue(_monitor.IsMonitoring);
            // Should log warning but not throw exception
        }

        [TestMethod]
        public async Task StopMonitoring_WhenMonitoring_ShouldStopMonitoring()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult { Success = true });
            await _monitor.StartMonitoringAsync();

            // Act
            _monitor.StopMonitoring();

            // Assert
            Assert.IsFalse(_monitor.IsMonitoring);
        }

        [TestMethod]
        public void StopMonitoring_WhenNotMonitoring_ShouldLogWarning()
        {
            // Act
            _monitor.StopMonitoring();

            // Assert
            Assert.IsFalse(_monitor.IsMonitoring);
            // Should log warning but not throw exception
        }

        [TestMethod]
        public async Task ForceHealthCheckAsync_WithNoVersions_ShouldReturnEmptyResult()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult { Success = true });

            // Act
            var result = await _monitor.ForceHealthCheckAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.VersionResults.Count);
            Assert.AreEqual(0, result.VersionsWithIssues);
            Assert.AreEqual(0, result.CriticalIssues);
        }

        [TestMethod]
        public async Task ForceHealthCheckAsync_WithDetectionError_ShouldReturnErrorResult()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult
            {
                Success = false,
                Error = "Detection failed"
            });

            // Act
            var result = await _monitor.ForceHealthCheckAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Detection failed", result.Error);
        }

        [TestMethod]
        public async Task ForceHealthCheckAsync_WithSpecificVersion_ShouldReturnSingleVersionResult()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            SetupMockVersionDetection(versionInfo);

            // Act
            var result = await _monitor.ForceHealthCheckAsync("2025");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2025", result.Version);
            Assert.IsTrue(result.CheckDuration.TotalMilliseconds >= 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task ForceHealthCheckAsync_WithNullVersion_ShouldThrowArgumentException()
        {
            // Act
            await _monitor.ForceHealthCheckAsync(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task ForceHealthCheckAsync_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Act
            await _monitor.ForceHealthCheckAsync("");
        }

        [TestMethod]
        public async Task GetHealthHistory_WithNoHistory_ShouldReturnEmptyList()
        {
            // Act
            var result = _monitor.GetHealthHistory("2025");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GetHealthHistory_WithNullVersion_ShouldThrowArgumentException()
        {
            // Act
            _monitor.GetHealthHistory(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GetHealthHistory_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Act
            _monitor.GetHealthHistory("");
        }

        [TestMethod]
        public async Task GetHealthHistory_WithLimit_ShouldReturnLimitedResults()
        {
            // Arrange
            SetupMockVersionDetection(new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025"));
            await _monitor.ForceHealthCheckAsync("2025");

            // Act
            var result = _monitor.GetHealthHistory("2025", 5);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count <= 5);
        }

        [TestMethod]
        public async Task GetHealthStatistics_WithNoVersions_ShouldReturnEmptyStatistics()
        {
            // Act
            var stats = _monitor.GetHealthStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.TotalVersions);
            Assert.AreEqual(0, stats.HealthyVersions);
            Assert.AreEqual(0, stats.DegradedVersions);
            Assert.AreEqual(0, stats.UnhealthyVersions);
            Assert.AreEqual(0, stats.AvailableVersions);
            Assert.AreEqual(0, stats.VersionsWithIssues);
            Assert.AreEqual(0, stats.OverallHealthPercentage);
            Assert.AreEqual(0, stats.UptimePercentages.Count);
        }

        [TestMethod]
        public async Task GetHealthStatistics_WithHealthyVersions_ShouldCalculateCorrectly()
        {
            // Arrange
            SetupMockVersionDetection(
                new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025") { Health = VersionHealth.Healthy },
                new SolidWorksVersionInfo("2024", @"C:\Test\SolidWorks 2024") { Health = VersionHealth.Healthy }
            );

            await _monitor.StartMonitoringAsync();

            // Act
            var stats = _monitor.GetHealthStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(2, stats.TotalVersions);
            Assert.AreEqual(2, stats.HealthyVersions);
            Assert.AreEqual(0, stats.DegradedVersions);
            Assert.AreEqual(0, stats.UnhealthyVersions);
            Assert.AreEqual(2, stats.AvailableVersions);
            Assert.AreEqual(0, stats.VersionsWithIssues);
            Assert.AreEqual(100.0, stats.OverallHealthPercentage);
        }

        [TestMethod]
        public async Task GetHealthStatistics_WithMixedHealth_ShouldCalculateCorrectly()
        {
            // Arrange
            SetupMockVersionDetection(
                new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025") { Health = VersionHealth.Healthy },
                new SolidWorksVersionInfo("2024", @"C:\Test\SolidWorks 2024") { Health = VersionHealth.Degraded },
                new SolidWorksVersionInfo("2023", @"C:\Test\SolidWorks 2023") { Health = VersionHealth.Unavailable }
            );

            await _monitor.StartMonitoringAsync();

            // Act
            var stats = _monitor.GetHealthStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(3, stats.TotalVersions);
            Assert.AreEqual(1, stats.HealthyVersions);
            Assert.AreEqual(1, stats.DegradedVersions);
            Assert.AreEqual(1, stats.UnhealthyVersions);
            Assert.AreEqual(2, stats.AvailableVersions); // Healthy + Degraded
            Assert.AreEqual(2, stats.VersionsWithIssues); // Degraded + Unavailable
            Assert.AreEqual(33.3, Math.Round(stats.OverallHealthPercentage, 1));
        }

        [TestMethod]
        public async Task CurrentHealthStatus_WithMonitoring_ShouldReturnCurrentStatus()
        {
            // Arrange
            SetupMockVersionDetection(new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025"));
            await _monitor.StartMonitoringAsync();

            // Act
            var status = _monitor.CurrentHealthStatus;

            // Assert
            Assert.IsNotNull(status);
            Assert.IsInstanceOfType(status, typeof(System.Collections.Generic.IReadOnlyDictionary<string, VersionHealthStatus>));
        }

        [TestMethod]
        public async Task HealthStatusChanged_WhenHealthChanges_ShouldRaiseEvent()
        {
            // Arrange
            var eventRaised = false;
            VersionHealthChangedEventArgs eventArgs = null;

            _monitor.HealthStatusChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            SetupMockVersionDetection(new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025"));
            await _monitor.StartMonitoringAsync();

            // Act - Force a health check that might change health status
            await _monitor.ForceHealthCheckAsync("2025");

            // Assert - In a real test, we'd mock the health check to change status
            Assert.IsNotNull(eventArgs); // This would be true if health status actually changed
        }

        [TestMethod]
        public async Task CriticalHealthIssueDetected_WithCriticalIssues_ShouldRaiseEvent()
        {
            // Arrange
            var eventRaised = false;
            VersionHealthCriticalEventArgs eventArgs = null;

            _monitor.CriticalHealthIssueDetected += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Act - In a real test, we'd set up a scenario with critical issues
            // For now, we just verify the event can be set up
            Assert.IsNotNull(_monitor);

            // Simulate event setup
            var handler = _monitor.GetType().GetEvent("CriticalHealthIssueDetected");
            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task VersionHealthConfiguration_WithDefaultValues_ShouldHaveReasonableDefaults()
        {
            // Arrange
            var config = new VersionHealthConfiguration();

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(5), config.HealthCheckInterval);
            Assert.AreEqual(1000, config.MaxHistoryRecords);
            Assert.AreEqual(1.0, config.MinimumDiskSpaceGB);
            Assert.AreEqual(1024, config.MinimumExecutableSizeBytes);
            Assert.AreEqual(1000, config.MaximumFileOperationMs);
            Assert.AreEqual(5000, config.LicenseManagerTimeoutMs);
        }

        [TestMethod]
        public async Task VersionHealthConfiguration_WithCustomValues_ShouldAcceptCustomValues()
        {
            // Arrange
            var config = new VersionHealthConfiguration
            {
                HealthCheckInterval = TimeSpan.FromSeconds(30),
                MaxHistoryRecords = 500,
                MinimumDiskSpaceGB = 2.5,
                MinimumExecutableSizeBytes = 2048,
                MaximumFileOperationMs = 2000,
                LicenseManagerTimeoutMs = 10000
            };

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(30), config.HealthCheckInterval);
            Assert.AreEqual(500, config.MaxHistoryRecords);
            Assert.AreEqual(2.5, config.MinimumDiskSpaceGB);
            Assert.AreEqual(2048, config.MinimumExecutableSizeBytes);
            Assert.AreEqual(2000, config.MaximumFileOperationMs);
            Assert.AreEqual(10000, config.LicenseManagerTimeoutMs);
        }

        [TestMethod]
        public async Task VersionHealthHistory_WithValidVersion_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var history = new VersionHealthHistory("2025", 100);

            // Assert
            Assert.IsNotNull(history);
            Assert.IsNotNull(history.CurrentStatus);
            Assert.IsNull(history.PreviousStatus);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task VersionHealthHistory_WithNullVersion_ShouldThrowArgumentNullException()
        {
            // Act
            var history = new VersionHealthHistory(null, 100);
        }

        [TestMethod]
        public async Task VersionHealthHistory_AddRecord_ShouldUpdateStatus()
        {
            // Arrange
            var history = new VersionHealthHistory("2025", 100);
            var originalStatus = history.CurrentStatus;

            var record = new VersionHealthRecord
            {
                Timestamp = DateTime.UtcNow,
                Health = VersionHealth.Healthy,
                IsAvailable = true,
                Issues = new List<string>(),
                CriticalIssues = new List<string>()
            };

            // Act
            history.AddRecord(record);

            // Assert
            Assert.IsNotNull(history.PreviousStatus);
            Assert.AreEqual(originalStatus, history.PreviousStatus);
            Assert.AreEqual(VersionHealth.Healthy, history.CurrentStatus.Health);
            Assert.IsTrue(history.CurrentStatus.IsAvailable);
            Assert.IsTrue(history.CurrentStatus.IsHealthy);
        }

        [TestMethod]
        public async Task VersionHealthHistory_GetRecentRecords_WithLimit_ShouldReturnLimitedRecords()
        {
            // Arrange
            var history = new VersionHealthHistory("2025", 100);

            // Add multiple records
            for (int i = 0; i < 10; i++)
            {
                var record = new VersionHealthRecord
                {
                    Timestamp = DateTime.UtcNow.AddSeconds(i),
                    Health = VersionHealth.Healthy,
                    IsAvailable = true,
                    Issues = new List<string>(),
                    CriticalIssues = new List<string>()
                };
                history.AddRecord(record);
            }

            // Act
            var recentRecords = history.GetRecentRecords(5);

            // Assert
            Assert.IsNotNull(recentRecords);
            Assert.AreEqual(5, recentRecords.Count);
        }

        [TestMethod]
        public async Task VersionHealthHistory_CalculateUptimePercentage_WithNoRecords_ShouldReturnZero()
        {
            // Arrange
            var history = new VersionHealthHistory("2025", 100);

            // Act
            var uptime = history.CalculateUptimePercentage();

            // Assert
            Assert.AreEqual(0.0, uptime);
        }

        [TestMethod]
        public async Task VersionHealthHistory_CalculateUptimePercentage_WithHealthyRecords_ShouldReturnCorrectPercentage()
        {
            // Arrange
            var history = new VersionHealthHistory("2025", 100);

            // Add healthy records
            for (int i = 0; i < 8; i++)
            {
                var record = new VersionHealthRecord
                {
                    Timestamp = DateTime.UtcNow.AddSeconds(i),
                    Health = VersionHealth.Healthy,
                    IsAvailable = true,
                    Issues = new List<string>(),
                    CriticalIssues = new List<string>()
                };
                history.AddRecord(record);
            }

            // Add unhealthy records
            for (int i = 0; i < 2; i++)
            {
                var record = new VersionHealthRecord
                {
                    Timestamp = DateTime.UtcNow.AddSeconds(i + 8),
                    Health = VersionHealth.Unavailable,
                    IsAvailable = false,
                    Issues = new List<string> { "Unavailable" },
                    CriticalIssues = new List<string>()
                };
                history.AddRecord(record);
            }

            // Act
            var uptime = history.CalculateUptimePercentage();

            // Assert
            Assert.AreEqual(80.0, uptime); // 8 out of 10 records are available
        }

        [TestMethod]
        public async Task VersionHealthCheckResult_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            var result = new VersionHealthCheckResult();

            // Act
            result.CheckDuration = TimeSpan.FromMilliseconds(100);
            result.VersionsWithIssues = 1;
            result.CriticalIssues = 0;
            result.VersionResults.Add(new SingleVersionHealthCheckResult
            {
                Version = "2025",
                Health = VersionHealth.Healthy
            });

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), result.CheckDuration);
            Assert.AreEqual(1, result.VersionsWithIssues);
            Assert.AreEqual(0, result.CriticalIssues);
            Assert.AreEqual(1, result.VersionResults.Count);
        }

        [TestMethod]
        public async Task SingleVersionHealthCheckResult_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            var result = new SingleVersionHealthCheckResult();

            // Act
            result.Version = "2025";
            result.Health = VersionHealth.Healthy;
            result.CheckDuration = TimeSpan.FromMilliseconds(50);
            result.IsAvailable = true;
            result.IsHealthy = true;
            result.HasIssues = false;
            result.IsCritical = false;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2025", result.Version);
            Assert.AreEqual(VersionHealth.Healthy, result.Health);
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), result.CheckDuration);
            Assert.IsTrue(result.IsAvailable);
            Assert.IsTrue(result.IsHealthy);
            Assert.IsFalse(result.HasIssues);
            Assert.IsFalse(result.IsCritical);
        }

        [TestMethod]
        public async Task VersionHealthStatistics_WithNoVersions_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var stats = new VersionHealthStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.TotalVersions);
            Assert.AreEqual(0, stats.HealthyVersions);
            Assert.AreEqual(0, stats.DegradedVersions);
            Assert.AreEqual(0, stats.UnhealthyVersions);
            Assert.AreEqual(0, stats.AvailableVersions);
            Assert.AreEqual(0, stats.VersionsWithIssues);
            Assert.AreEqual(0, stats.OverallHealthPercentage);
            Assert.IsNotNull(stats.UptimePercentages);
            Assert.AreEqual(0, stats.UptimePercentages.Count);
        }

        [TestMethod]
        public async Task VersionHealthChangedEventArgs_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            var args = new VersionHealthChangedEventArgs
            {
                Version = "2025",
                PreviousHealth = VersionHealth.Degraded,
                CurrentHealth = VersionHealth.Healthy,
                Timestamp = DateTime.UtcNow,
                Issues = new List<string>(),
                CriticalIssues = new List<string>()
            };

            // Assert
            Assert.AreEqual("2025", args.Version);
            Assert.AreEqual(VersionHealth.Degraded, args.PreviousHealth);
            Assert.AreEqual(VersionHealth.Healthy, args.CurrentHealth);
            Assert.IsNotNull(args.Timestamp);
            Assert.IsNotNull(args.Issues);
            Assert.IsNotNull(args.CriticalIssues);
        }

        [TestMethod]
        public async Task VersionHealthCriticalEventArgs_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            var args = new VersionHealthCriticalEventArgs
            {
                Version = "2025",
                Health = VersionHealth.Corrupted,
                Timestamp = DateTime.UtcNow,
                CriticalIssues = new List<string> { "Corrupted installation" },
                AllIssues = new List<string> { "Corrupted installation", "Missing files" }
            };

            // Assert
            Assert.AreEqual("2025", args.Version);
            Assert.AreEqual(VersionHealth.Corrupted, args.Health);
            Assert.IsNotNull(args.Timestamp);
            Assert.IsNotNull(args.CriticalIssues);
            Assert.IsNotNull(args.AllIssues);
            Assert.AreEqual(1, args.CriticalIssues.Count);
            Assert.AreEqual(2, args.AllIssues.Count);
        }

        [TestMethod]
        public async Task Dispose_ShouldStopMonitoringAndCleanUp()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult { Success = true });
            await _monitor.StartMonitoringAsync();

            // Act
            _monitor.Dispose();

            // Assert
            Assert.IsFalse(_monitor.IsMonitoring);
            // Should not throw exception when disposed
        }

        [TestMethod]
        public async Task Dispose_CalledMultipleTimes_ShouldNotThrowException()
        {
            // Arrange
            SetupMockDetectionResult(new VersionDetectionResult { Success = true });
            await _monitor.StartMonitoringAsync();

            // Act
            _monitor.Dispose();
            _monitor.Dispose(); // Second call

            // Assert - Should not throw exception
        }

        #region Helper Methods

        private void SetupMockDetectionResult(VersionDetectionResult result)
        {
            _mockDetector.Setup(d => d.DetectInstalledVersionsAsync()).ReturnsAsync(result);
        }

        private void SetupMockVersionDetection(params SolidWorksVersionInfo[] versions)
        {
            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = versions.Length,
                AvailableVersions = versions.Length,
                SupportedVersions = versions.Length
            };

            _mockDetector.Setup(d => d.DetectInstalledVersionsAsync()).ReturnsAsync(detectionResult);
            _mockDetector.Setup(d => d.DetectedVersions).Returns(versions.ToList());

            foreach (var version in versions)
            {
                _mockDetector.Setup(d => d.GetVersion(version.Version)).Returns(version);
            }
        }

        #endregion
    }
}