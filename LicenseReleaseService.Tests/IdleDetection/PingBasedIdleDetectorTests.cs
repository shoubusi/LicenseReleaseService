using System;
using System.Collections.Generic;
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
    /// Unit tests for PingBasedIdleDetector
    /// </summary>
    public class PingBasedIdleDetectorTests
    {
        private readonly Mock<ILogger<PingBasedIdleDetector>> _mockLogger;
        private readonly Mock<ProcessPingService> _mockProcessPingService;
        private readonly Mock<DocumentActivityMonitor> _mockDocumentMonitor;
        private readonly Mock<NetworkActivityMonitor> _mockNetworkMonitor;
        private readonly PingBasedDetectionConfig _config;

        public PingBasedIdleDetectorTests()
        {
            _mockLogger = new Mock<ILogger<PingBasedIdleDetector>>();
            _mockProcessPingService = new Mock<ProcessPingService>(MockBehavior.Loose, null, null);
            _mockDocumentMonitor = new Mock<DocumentActivityMonitor>(MockBehavior.Loose, null, null);
            _mockNetworkMonitor = new Mock<NetworkActivityMonitor>(MockBehavior.Loose, null, null);

            _config = new PingBasedDetectionConfig
            {
                DetectionIntervalSeconds = 30,
                PingTimeoutMs = 1000,
                MaxConcurrentPings = 5,
                RequiredSuccessRate = 0.3,
                ActivityThresholdMinutes = 5,
                DefaultIdleThresholdMinutes = 15,
                MaxPingAttempts = 3,
                PingRetryDelayMs = 500,
                EnableWindowsAPIChecking = true,
                EnableDocumentActivityMonitoring = true,
                EnableNetworkActivityMonitoring = true
            };
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var detector = new PingBasedIdleDetector(
                _mockLogger.Object,
                _mockProcessPingService.Object,
                _mockDocumentMonitor.Object,
                _mockNetworkMonitor.Object,
                _config);

            // Assert
            Assert.Equal("PingBasedIdleDetector", detector.Name);
            Assert.Equal("Active probing-based idle detection with process responsiveness checking", detector.Description);
            Assert.Equal(new Version(1, 0, 0, 0), detector.Version);
            Assert.Equal(20, detector.Priority);
            Assert.False(detector.IsInitialized);
            Assert.Equal(DetectorStatus.NotInitialized, detector.Status);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PingBasedIdleDetector(
                null,
                _mockProcessPingService.Object,
                _mockDocumentMonitor.Object,
                _mockNetworkMonitor.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullProcessPingService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PingBasedIdleDetector(
                _mockLogger.Object,
                null,
                _mockDocumentMonitor.Object,
                _mockNetworkMonitor.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullDocumentMonitor_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PingBasedIdleDetector(
                _mockLogger.Object,
                _mockProcessPingService.Object,
                null,
                _mockNetworkMonitor.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullNetworkMonitor_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PingBasedIdleDetector(
                _mockLogger.Object,
                _mockProcessPingService.Object,
                _mockDocumentMonitor.Object,
                null,
                _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PingBasedIdleDetector(
                _mockLogger.Object,
                _mockProcessPingService.Object,
                _mockDocumentMonitor.Object,
                _mockNetworkMonitor.Object,
                null));
        }

        [Fact]
        public async Task InitializeAsync_WithValidConfiguration_ShouldInitializeSuccessfully()
        {
            // Arrange
            var detector = CreateDetector();
            var configuration = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 30,
                IdleThresholdSeconds = 900,
                ConfidenceThreshold = 0.7,
                IsEnabled = true
            };

            _mockProcessPingService.Setup(s => s.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDocumentMonitor.Setup(s => s.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockNetworkMonitor.Setup(s => s.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await detector.InitializeAsync(configuration);

            // Assert
            Assert.True(detector.IsInitialized);
            Assert.Equal(DetectorStatus.Initialized, detector.Status);

            _mockProcessPingService.Verify(s => s.InitializeAsync(configuration, It.IsAny<CancellationToken>()), Times.Once);
            _mockDocumentMonitor.Verify(s => s.InitializeAsync(configuration, It.IsAny<CancellationToken>()), Times.Once);
            _mockNetworkMonitor.Verify(s => s.InitializeAsync(configuration, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ShouldNotInitializeAgain()
        {
            // Arrange
            var detector = CreateDetector();
            var configuration = new IdleDetectorConfiguration();
            await detector.InitializeAsync(configuration);

            // Reset mock to track calls after first initialization
            _mockProcessPingService.Invocations.Clear();
            _mockDocumentMonitor.Invocations.Clear();
            _mockNetworkMonitor.Invocations.Clear();

            // Act
            await detector.InitializeAsync(configuration);

            // Assert
            _mockProcessPingService.Verify(s => s.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockDocumentMonitor.Verify(s => s.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockNetworkMonitor.Verify(s => s.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InitializeAsync_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            var detector = CreateDetector();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => detector.InitializeAsync(null));
        }

        [Fact]
        public async Task StartAsync_WhenInitialized_ShouldStartSuccessfully()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());

            _mockProcessPingService.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDocumentMonitor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockNetworkMonitor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await detector.StartAsync();

            // Assert
            Assert.Equal(DetectorStatus.Running, detector.Status);

            _mockProcessPingService.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockDocumentMonitor.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockNetworkMonitor.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WhenNotInitialized_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var detector = CreateDetector();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => detector.StartAsync());
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            // Reset mock to track calls after first start
            _mockProcessPingService.Invocations.Clear();
            _mockDocumentMonitor.Invocations.Clear();
            _mockNetworkMonitor.Invocations.Clear();

            // Act
            await detector.StartAsync();

            // Assert
            _mockProcessPingService.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
            _mockDocumentMonitor.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
            _mockNetworkMonitor.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopSuccessfully()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            _mockProcessPingService.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDocumentMonitor.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockNetworkMonitor.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await detector.StopAsync();

            // Assert
            Assert.Equal(DetectorStatus.Stopped, detector.Status);

            _mockProcessPingService.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockDocumentMonitor.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockNetworkMonitor.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DetectIdleAsync_WithResponsiveProcess_ShouldReturnActiveResult()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            var pingResult = new ProcessPingResult
            {
                ProcessId = processId,
                IsResponsive = true,
                ResponseTimeMs = 50,
                TotalPingAttempts = 3,
                SuccessfulPingCount = 3,
                FailedPingCount = 0,
                AverageResponseTimeMs = 50,
                LastSuccessfulPing = DateTime.UtcNow
            };

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pingResult);
            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentActivityData>());
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            // Act
            var result = await detector.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(userName, result.UserName);
            Assert.Equal(computerName, result.ComputerName);
            Assert.False(result.IsIdle); // Process is responsive, so not idle
            Assert.True(result.Confidence > 0.5); // Should have good confidence for responsive process
            Assert.Equal("ProcessPing", result.DetectionMethod);
            Assert.Contains("responsive", result.Reason, StringComparison.OrdinalIgnoreCase);

            _mockProcessPingService.Verify(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()), Times.Once);
            _mockDocumentMonitor.Verify(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockNetworkMonitor.Verify(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DetectIdleAsync_WithUnresponsiveProcessAndNoActivity_ShouldReturnIdleResult()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            var pingResult = new ProcessPingResult
            {
                ProcessId = processId,
                IsResponsive = false,
                TotalPingAttempts = 3,
                SuccessfulPingCount = 0,
                FailedPingCount = 3,
                AverageResponseTimeMs = 0,
                LastSuccessfulPing = null
            };

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pingResult);
            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentActivityData>());
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            // Act
            var result = await detector.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(userName, result.UserName);
            Assert.Equal(computerName, result.ComputerName);
            Assert.True(result.IsIdle); // Process is unresponsive with no activity
            Assert.True(result.Confidence > 0.5); // Should have good confidence for idle process
            Assert.Equal("ProcessPing", result.DetectionMethod);
            Assert.Contains("unresponsive", result.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DetectIdleAsync_WithRecentDocumentActivity_ShouldReturnActiveResult()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            var pingResult = new ProcessPingResult
            {
                ProcessId = processId,
                IsResponsive = false,
                TotalPingAttempts = 3,
                SuccessfulPingCount = 0,
                FailedPingCount = 3
            };

            var recentDocumentActivity = new List<DocumentActivityData>
            {
                new DocumentActivityData
                {
                    ProcessId = processId,
                    ActivityType = "DocumentSave",
                    DocumentPath = "C:\\test.sldprt",
                    Timestamp = DateTime.UtcNow.AddMinutes(-1), // Recent activity
                    Confidence = 0.9
                }
            };

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pingResult);
            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recentDocumentActivity);
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            // Act
            var result = await detector.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(userName, result.UserName);
            Assert.Equal(computerName, result.ComputerName);
            Assert.False(result.IsIdle); // Recent document activity indicates active process
            Assert.Contains("activity detected", result.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DetectIdleAsync_WithPingServiceError_ShouldHandleGracefully()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Process not found"));
            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentActivityData>());
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            // Act
            var result = await detector.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(userName, result.UserName);
            Assert.Equal(computerName, computerName);
            Assert.False(result.IsIdle); // Error typically means we can't confirm idle state
            Assert.Equal(0.0, result.Confidence);
            Assert.Equal("Error", result.DetectionMethod);
            Assert.Contains("Process not found", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_BatchDetection_ShouldProcessAllProcesses()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processes = new List<ProcessInfo>
            {
                new ProcessInfo(1234, "SLDWORKS", "user1", "PC1"),
                new ProcessInfo(5678, "SLDWORKS", "user2", "PC2"),
                new ProcessInfo(9012, "SLDWORKS", "user3", "PC3")
            };

            // Setup mock responses for each process
            foreach (var process in processes)
            {
                _mockProcessPingService.Setup(s => s.PingProcessAsync(process.ProcessId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProcessPingResult
                    {
                        ProcessId = process.ProcessId,
                        IsResponsive = true,
                        ResponseTimeMs = 50,
                        TotalPingAttempts = 3,
                        SuccessfulPingCount = 3,
                        FailedPingCount = 0
                    });

                _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(process.ProcessId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<DocumentActivityData>());

                _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(process.ProcessId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<NetworkActivityData>());
            }

            // Act
            var results = await detector.DetectIdleAsync(processes);

            // Assert
            Assert.Equal(processes.Count, results.Count);
            Assert.All(results, result => Assert.False(result.IsIdle)); // All processes responsive
            Assert.All(results, result => Assert.Equal(processes.First(p => p.ProcessId == result.ProcessId).UserName, result.UserName));
            Assert.All(results, result => Assert.Equal(processes.First(p => p.ProcessId == result.ProcessId).ComputerName, result.ComputerName));

            // Verify each process was pinged
            foreach (var process in processes)
            {
                _mockProcessPingService.Verify(s => s.PingProcessAsync(process.ProcessId, It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Fact]
        public async Task GetIdleTimeAsync_WithSuccessfulPing_ShouldReturnZero()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = true,
                    ResponseTimeMs = 50,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 3,
                    FailedPingCount = 0,
                    LastSuccessfulPing = DateTime.UtcNow
                });

            // Act
            var idleTime = await detector.GetIdleTimeAsync(processId);

            // Assert
            Assert.Equal(TimeSpan.Zero, idleTime);
        }

        [Fact]
        public async Task GetIdleTimeAsync_WithFailedPing_ShouldReturnConfiguredThreshold()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = false,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 0,
                    FailedPingCount = 3,
                    LastSuccessfulPing = null
                });

            // Act
            var idleTime = await detector.GetIdleTimeAsync(processId);

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(_config.DefaultIdleThresholdMinutes), idleTime);
        }

        [Fact]
        public async Task IsIdleAsync_WithResponsiveProcess_ShouldReturnFalse()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = true,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 3,
                    FailedPingCount = 0
                });

            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentActivityData>());
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            // Act
            var isIdle = await detector.IsIdleAsync(processId);

            // Assert
            Assert.False(isIdle);
        }

        [Fact]
        public async Task GetIdleInfoAsync_ShouldReturnComprehensiveInformation()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;

            var pingResult = new ProcessPingResult
            {
                ProcessId = processId,
                IsResponsive = false,
                TotalPingAttempts = 3,
                SuccessfulPingCount = 0,
                FailedPingCount = 3,
                AverageResponseTimeMs = 0
            };

            var documentActivities = new List<DocumentActivityData>
            {
                new DocumentActivityData
                {
                    ProcessId = processId,
                    ActivityType = "DocumentOpen",
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    Confidence = 0.8
                }
            };

            var networkActivities = new List<NetworkActivityData>
            {
                new NetworkActivityData
                {
                    ProcessId = processId,
                    ActivityType = "TCP_Connection",
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    Confidence = 0.7
                }
            };

            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pingResult);
            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(documentActivities);
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(networkActivities);

            // Act
            var idleInfo = await detector.GetIdleInfoAsync(processId);

            // Assert
            Assert.NotNull(idleInfo);
            Assert.Equal(processId, idleInfo.ProcessId);
            Assert.True(idleInfo.IdleTime.TotalMinutes > 0);
            Assert.Equal(SessionState.Inactive, idleInfo.State); // Should be inactive due to ping failure
            Assert.True(idleInfo.Confidence > 0.5);
            Assert.Contains("ProcessPing", idleInfo.DetectionMethods);
            Assert.Contains("DocumentActivity", idleInfo.DetectionMethods);
            Assert.Contains("NetworkActivity", idleInfo.DetectionMethods);
            Assert.True((double)idleInfo.AdditionalInfo["DocumentActivityCount"] > 0);
            Assert.True((double)idleInfo.AdditionalInfo["NetworkActivityCount"] > 0);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_ShouldUpdateAllServices()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());

            var newConfig = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 60,
                IdleThresholdSeconds = 1800,
                ConfidenceThreshold = 0.8,
                CustomParameters = new Dictionary<string, object>
                {
                    ["PingTimeoutMs"] = 2000,
                    ["MaxConcurrentPings"] = 15
                }
            };

            _mockProcessPingService.Setup(s => s.UpdateConfigurationAsync(newConfig, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDocumentMonitor.Setup(s => s.UpdateConfigurationAsync(newConfig, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockNetworkMonitor.Setup(s => s.UpdateConfigurationAsync(newConfig, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await detector.UpdateConfigurationAsync(newConfig);

            // Assert
            _mockProcessPingService.Verify(s => s.UpdateConfigurationAsync(newConfig, It.IsAny<CancellationToken>()), Times.Once);
            _mockDocumentMonitor.Verify(s => s.UpdateConfigurationAsync(newConfig, It.IsAny<CancellationToken>()), Times.Once);
            _mockNetworkMonitor.Verify(s => s.UpdateConfigurationAsync(newConfig, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetHealthAsync_WithHealthyServices_ShouldReturnHealthyStatus()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var pingHealth = new DetectorHealth { IsHealthy = true, StatusMessage = "Healthy" };
            var documentHealth = new DetectorHealth { IsHealthy = true, StatusMessage = "Healthy" };
            var networkHealth = new DetectorHealth { IsHealthy = true, StatusMessage = "Healthy" };

            _mockProcessPingService.Setup(s => s.GetHealthAsync())
                .ReturnsAsync(pingHealth);
            _mockDocumentMonitor.Setup(s => s.GetHealthAsync())
                .ReturnsAsync(documentHealth);
            _mockNetworkMonitor.Setup(s => s.GetHealthAsync())
                .ReturnsAsync(networkHealth);

            // Act
            var health = await detector.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.Equal("PingBasedIdleDetector", health.DetectorName);
            Assert.True(health.IsHealthy);
            Assert.Equal("Healthy", health.StatusMessage);
        }

        [Fact]
        public async Task GetHealthAsync_WithUnhealthyService_ShouldReturnUnhealthyStatus()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var pingHealth = new DetectorHealth { IsHealthy = false, StatusMessage = "Service unavailable" };
            var documentHealth = new DetectorHealth { IsHealthy = true, StatusMessage = "Healthy" };
            var networkHealth = new DetectorHealth { IsHealthy = true, StatusMessage = "Healthy" };

            _mockProcessPingService.Setup(s => s.GetHealthAsync())
                .ReturnsAsync(pingHealth);
            _mockDocumentMonitor.Setup(s => s.GetHealthAsync())
                .ReturnsAsync(documentHealth);
            _mockNetworkMonitor.Setup(s => s.GetHealthAsync())
                .ReturnsAsync(networkHealth);

            // Act
            var health = await detector.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.False(health.IsHealthy);
            Assert.Contains("Process ping service unhealthy", health.StatusMessage);
        }

        [Fact]
        public void ValidateConfiguration_WithValidConfiguration_ShouldReturnValidResult()
        {
            // Arrange
            var detector = CreateDetector();
            var configuration = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 30,
                IdleThresholdSeconds = 900,
                ConfidenceThreshold = 0.7,
                CustomParameters = new Dictionary<string, object>
                {
                    ["PingTimeoutMs"] = 1000,
                    ["MaxConcurrentPings"] = 10,
                    ["RequiredSuccessRate"] = 0.5
                }
            };

            // Act
            var result = detector.ValidateConfiguration(configuration);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidDetectionInterval_ShouldReturnInvalidResult()
        {
            // Arrange
            var detector = CreateDetector();
            var configuration = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 2, // Too low for ping-based detection
                IdleThresholdSeconds = 900,
                ConfidenceThreshold = 0.7
            };

            // Act
            var result = detector.ValidateConfiguration(configuration);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Detection interval must be at least 5 seconds"));
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidConfidenceThreshold_ShouldReturnInvalidResult()
        {
            // Arrange
            var detector = CreateDetector();
            var configuration = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 30,
                IdleThresholdSeconds = 900,
                ConfidenceThreshold = 0.3 // Too low for ping-based detection
            };

            // Act
            var result = detector.ValidateConfiguration(configuration);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Confidence threshold must be between 0.5 and 1.0"));
        }

        [Fact]
        public void ValidateConfiguration_WithNullConfiguration_ShouldReturnInvalidResult()
        {
            // Arrange
            var detector = CreateDetector();

            // Act
            var result = detector.ValidateConfiguration(null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Configuration cannot be null"));
        }

        [Fact]
        public async Task IdleDetectedEvent_ShouldBeRaisedWhenProcessBecomesIdle()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            var eventRaised = false;
            IdleDetectionEventArgs capturedEventArgs = null;

            detector.IdleDetected += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            // First call - process is responsive (active)
            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = true,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 3,
                    FailedPingCount = 0
                });

            await detector.DetectIdleAsync(processId, userName, computerName);

            // Second call - process becomes unresponsive (idle)
            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = false,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 0,
                    FailedPingCount = 3
                });

            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentActivityData>());
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            await detector.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.Equal(processId, capturedEventArgs.ProcessId);
            Assert.Equal(userName, capturedEventArgs.UserName);
            Assert.Equal(computerName, capturedEventArgs.ComputerName);
            Assert.True(capturedEventArgs.Confidence > 0.5);
        }

        [Fact]
        public async Task ActivityDetectedEvent_ShouldBeRaisedWhenProcessBecomesActive()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            var eventRaised = false;
            IdleDetectionEventArgs capturedEventArgs = null;

            detector.ActivityDetected += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            // First call - process is unresponsive (idle)
            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = false,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 0,
                    FailedPingCount = 3
                });

            _mockDocumentMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DocumentActivityData>());
            _mockNetworkMonitor.Setup(s => s.GetRecentActivitiesAsync(processId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkActivityData>());

            await detector.DetectIdleAsync(processId, userName, computerName);

            // Second call - process becomes responsive (active)
            _mockProcessPingService.Setup(s => s.PingProcessAsync(processId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessPingResult
                {
                    ProcessId = processId,
                    IsResponsive = true,
                    ResponseTimeMs = 50,
                    TotalPingAttempts = 3,
                    SuccessfulPingCount = 3,
                    FailedPingCount = 0
                });

            await detector.DetectIdleAsync(processId, userName, computerName);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.Equal(processId, capturedEventArgs.ProcessId);
            Assert.Equal(userName, capturedEventArgs.UserName);
            Assert.Equal(computerName, capturedEventArgs.ComputerName);
            Assert.True(capturedEventArgs.Confidence > 0.5);
        }

        [Fact]
        public void Dispose_ShouldDisposeAllResources()
        {
            // Arrange
            var detector = CreateDetector();

            // Act & Assert
            Assert.DoesNotThrow(() => detector.Dispose());
        }

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopServices()
        {
            // Arrange
            var detector = CreateDetector();
            await detector.InitializeAsync(new IdleDetectorConfiguration());
            await detector.StartAsync();

            _mockProcessPingService.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDocumentMonitor.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockNetworkMonitor.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            detector.Dispose();

            // Assert
            _mockProcessPingService.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockDocumentMonitor.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockNetworkMonitor.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        private PingBasedIdleDetector CreateDetector()
        {
            return new PingBasedIdleDetector(
                _mockLogger.Object,
                _mockProcessPingService.Object,
                _mockDocumentMonitor.Object,
                _mockNetworkMonitor.Object,
                _config);
        }
    }
}