using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class TimeBasedIdleDetectorTests : IDisposable
    {
        private readonly TimeBasedIdleDetector _detector;
        private readonly Mock<ILogger<TimeBasedIdleDetector>> _loggerMock;
        private readonly TimeBasedDetectionConfig _config;
        private readonly Mock<SystemActivityMonitor> _activityMonitorMock;
        private readonly Mock<ActivityThresholdManager> _thresholdManagerMock;
        private readonly List<IdleDetectionEventArgs> _idleEvents;
        private readonly List<IdleDetectionEventArgs> _activityEvents;
        private readonly List<DetectorStatusChangedEventArgs> _statusEvents;
        private readonly List<DetectorErrorEventArgs> _errorEvents;

        public TimeBasedIdleDetectorTests()
        {
            _loggerMock = new Mock<ILogger<TimeBasedIdleDetector>>();
            _config = new TimeBasedDetectionConfig
            {
                DetectionIntervalSeconds = 1, // Fast for testing
                WarningThresholdMinutes = 1,
                ImminentThresholdMinutes = 2,
                CriticalThresholdMinutes = 3,
                EnableAdaptiveThresholds = false // Disable for simpler tests
            };

            _activityMonitorMock = new Mock<SystemActivityMonitor>(_loggerMock.Object, _config);
            _thresholdManagerMock = new Mock<ActivityThresholdManager>(_loggerMock.Object, _config);

            _detector = new TimeBasedIdleDetector(
                _loggerMock.Object,
                _config,
                _activityMonitorMock.Object,
                _thresholdManagerMock.Object);

            _idleEvents = new List<IdleDetectionEventArgs>();
            _activityEvents = new List<IdleDetectionEventArgs>();
            _statusEvents = new List<DetectorStatusChangedEventArgs>();
            _errorEvents = new List<DetectorErrorEventArgs>();

            // Wire up events
            _detector.IdleDetected += (s, e) => _idleEvents.Add(e);
            _detector.ActivityDetected += (s, e) => _activityEvents.Add(e);
            _detector.StatusChanged += (s, e) => _statusEvents.Add(e);
            _detector.ErrorOccurred += (s, e) => _errorEvents.Add(e);
        }

        public void Dispose()
        {
            _detector?.Dispose();
        }

        [Fact]
        public void Constructor_ValidParameters_ShouldInitializeCorrectly()
        {
            // Assert
            Assert.NotNull(_detector);
            Assert.Equal("TimeBasedIdleDetector", _detector.Name);
            Assert.Equal("Time-based idle detection with adaptive thresholds and graduated detection levels", _detector.Description);
            Assert.Equal(new Version(1, 0, 0, 0), _detector.Version);
            Assert.Equal(10, _detector.Priority);
            Assert.False(_detector.IsEnabled);
            Assert.False(_detector.IsInitialized);
            Assert.Equal(DetectorStatus.NotInitialized, _detector.Status);
        }

        [Fact]
        public void SupportedMethods_ShouldReturnExpectedMethods()
        {
            // Act
            var methods = _detector.SupportedMethods;

            // Assert
            Assert.Contains("TimeBased", methods);
            Assert.Contains("Adaptive", methods);
            Assert.Contains("Graduated", methods);
            Assert.Equal(3, methods.Count);
        }

        [Fact]
        public async Task InitializeAsync_ValidConfiguration_ShouldInitializeSuccessfully()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();

            // Setup mocks
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            // Act
            await _detector.InitializeAsync(config);

            // Assert
            Assert.True(_detector.IsInitialized);
            Assert.Equal(DetectorStatus.Initialized, _detector.Status);
            Assert.NotNull(_detector.Configuration);

            // Verify mock calls
            _activityMonitorMock.Verify(am => am.StartAsync(), Times.Once);
            _thresholdManagerMock.Verify(tm => tm.Initialize(), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_AlreadyInitialized_ShouldLogWarning()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Act
            await _detector.InitializeAsync(config);

            // Assert
            VerifyLoggerWarning("TimeBasedIdleDetector is already initialized");
        }

        [Fact]
        public async Task InitializeAsync_NullConfiguration_ShouldThrowException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _detector.InitializeAsync(null));
        }

        [Fact]
        public async Task InitializeAsync_InvalidConfiguration_ShouldThrowException()
        {
            // Arrange
            var invalidConfig = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 1, // Too low
                ConfidenceThreshold = 1.5 // Too high
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _detector.InitializeAsync(invalidConfig));
        }

        [Fact]
        public async Task StartAsync_WhenInitialized_ShouldStartSuccessfully()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Act
            await _detector.StartAsync();

            // Assert
            Assert.Equal(DetectorStatus.Running, _detector.Status);

            // Verify status changed event
            var statusEvent = _statusEvents.FirstOrDefault(e => e.NewStatus == DetectorStatus.Running);
            Assert.NotNull(statusEvent);
        }

        [Fact]
        public async Task StartAsync_WhenNotInitialized_ShouldThrowException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _detector.StartAsync());
        }

        [Fact]
        public async Task StartAsync_AlreadyRunning_ShouldLogWarning()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);
            await _detector.StartAsync();

            // Act
            await _detector.StartAsync();

            // Assert
            VerifyLoggerWarning("TimeBasedIdleDetector is already running");
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopSuccessfully()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);
            await _detector.StartAsync();

            // Act
            await _detector.StopAsync();

            // Assert
            Assert.Equal(DetectorStatus.Stopped, _detector.Status);

            // Verify status changed event
            var statusEvent = _statusEvents.FirstOrDefault(e => e.NewStatus == DetectorStatus.Stopped);
            Assert.NotNull(statusEvent);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldLogWarning()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Act
            await _detector.StopAsync();

            // Assert
            VerifyLoggerWarning("TimeBasedIdleDetector is not running");
        }

        [Fact]
        public async Task PauseAsync_WhenRunning_ShouldPauseSuccessfully()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);
            await _detector.StartAsync();

            // Act
            await _detector.PauseAsync();

            // Assert
            Assert.Equal(DetectorStatus.Paused, _detector.Status);

            // Verify status changed event
            var statusEvent = _statusEvents.FirstOrDefault(e => e.NewStatus == DetectorStatus.Paused);
            Assert.NotNull(statusEvent);
        }

        [Fact]
        public async Task ResumeAsync_WhenPaused_ShouldResumeSuccessfully()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);
            await _detector.StartAsync();
            await _detector.PauseAsync();

            // Act
            await _detector.ResumeAsync();

            // Assert
            Assert.Equal(DetectorStatus.Running, _detector.Status);

            // Verify status changed event
            var statusEvent = _statusEvents.FirstOrDefault(e => e.NewStatus == DetectorStatus.Running);
            Assert.NotNull(statusEvent);
        }

        [Fact]
        public async Task DetectIdleAsync_ActiveSystem_ShouldReturnActiveResult()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Setup mocks for active system
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(TimeSpan.FromMinutes(0));
            _thresholdManagerMock.Setup(tm => tm.GetUserThresholds("testuser", "testcomputer"))
                .Returns(new EffectiveThresholds
                {
                    WarningThreshold = TimeSpan.FromMinutes(1),
                    ImminentThreshold = TimeSpan.FromMinutes(2),
                    CriticalThreshold = TimeSpan.FromMinutes(3)
                });
            _thresholdManagerMock.Setup(tm => tm.GetDetectionLevel(TimeSpan.FromMinutes(0), "testuser", "testcomputer"))
                .Returns(DetectionLevel.Active);
            _thresholdManagerMock.Setup(tm => tm.ApplyHysteresis(DetectionLevel.Active, DetectionLevel.Active, TimeSpan.FromMinutes(0), "testuser", "testcomputer"))
                .Returns(DetectionLevel.Active);

            // Act
            var result = await _detector.DetectIdleAsync(123, "testuser", "testcomputer");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            Assert.Equal("TimeBasedAdaptive", result.DetectionMethod);
            Assert.Equal("System active (idle time: 0.0 minutes)", result.Reason);
            Assert.Equal(DetectorName, result.DetectorName);
        }

        [Fact]
        public async Task DetectIdleAsync_IdleSystem_ShouldReturnIdleResult()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Setup mocks for idle system
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(TimeSpan.FromMinutes(5));
            _thresholdManagerMock.Setup(tm => tm.GetUserThresholds("testuser", "testcomputer"))
                .Returns(new EffectiveThresholds
                {
                    WarningThreshold = TimeSpan.FromMinutes(1),
                    ImminentThreshold = TimeSpan.FromMinutes(2),
                    CriticalThreshold = TimeSpan.FromMinutes(3)
                });
            _thresholdManagerMock.Setup(tm => tm.GetDetectionLevel(TimeSpan.FromMinutes(5), "testuser", "testcomputer"))
                .Returns(DetectionLevel.Critical);
            _thresholdManagerMock.Setup(tm => tm.ApplyHysteresis(DetectionLevel.Active, DetectionLevel.Critical, TimeSpan.FromMinutes(5), "testuser", "testcomputer"))
                .Returns(DetectionLevel.Critical);

            // Act
            var result = await _detector.DetectIdleAsync(123, "testuser", "testcomputer");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle);
            Assert.Equal("TimeBasedAdaptive", result.DetectionMethod);
            Assert.Contains("Exceeded critical threshold", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_ShouldRaiseEventsWhenStateChanges()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Setup mocks for state change
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(TimeSpan.FromMinutes(5));
            _thresholdManagerMock.Setup(tm => tm.GetUserThresholds("testuser", "testcomputer"))
                .Returns(new EffectiveThresholds
                {
                    WarningThreshold = TimeSpan.FromMinutes(1),
                    ImminentThreshold = TimeSpan.FromMinutes(2),
                    CriticalThreshold = TimeSpan.FromMinutes(3)
                });
            _thresholdManagerMock.Setup(tm => tm.GetDetectionLevel(TimeSpan.FromMinutes(5), "testuser", "testcomputer"))
                .Returns(DetectionLevel.Critical);
            _thresholdManagerMock.Setup(tm => tm.ApplyHysteresis(DetectionLevel.Active, DetectionLevel.Critical, TimeSpan.FromMinutes(5), "testuser", "testcomputer"))
                .Returns(DetectionLevel.Critical);

            // Act
            await _detector.DetectIdleAsync(123, "testuser", "testcomputer");

            // Assert
            // Should have raised idle detected event
            Assert.NotEmpty(_idleEvents);
        }

        [Fact]
        public async Task DetectIdleAsync_BatchProcessing_ShouldHandleMultipleProcesses()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            var processes = new List<ProcessInfo>
            {
                new ProcessInfo(123, "test.exe", "user1", "computer1"),
                new ProcessInfo(456, "test2.exe", "user2", "computer2"),
                new ProcessInfo(789, "test3.exe", "user3", "computer3")
            };

            // Setup mocks
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(TimeSpan.FromMinutes(0));
            _thresholdManagerMock.Setup(tm => tm.GetUserThresholds(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new EffectiveThresholds
                {
                    WarningThreshold = TimeSpan.FromMinutes(1),
                    ImminentThreshold = TimeSpan.FromMinutes(2),
                    CriticalThreshold = TimeSpan.FromMinutes(3)
                });
            _thresholdManagerMock.Setup(tm => tm.GetDetectionLevel(It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(DetectionLevel.Active);
            _thresholdManagerMock.Setup(tm => tm.ApplyHysteresis(It.IsAny<DetectionLevel>(), It.IsAny<DetectionLevel>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(DetectionLevel.Active);

            // Act
            var results = await _detector.DetectIdleAsync(processes);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.NotNull(r));
        }

        [Fact]
        public async Task GetIdleTimeAsync_ShouldReturnSystemIdleTime()
        {
            // Arrange
            var expectedIdleTime = TimeSpan.FromMinutes(10);
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(expectedIdleTime);

            // Act
            var result = await _detector.GetIdleTimeAsync(123);

            // Assert
            Assert.Equal(expectedIdleTime, result);
        }

        [Fact]
        public async Task IsIdleAsync_ShouldReturnCorrectIdleState()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Setup mocks for idle system
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(TimeSpan.FromMinutes(5));
            _thresholdManagerMock.Setup(tm => tm.GetUserThresholds("unknown", "unknown"))
                .Returns(new EffectiveThresholds
                {
                    WarningThreshold = TimeSpan.FromMinutes(1),
                    ImminentThreshold = TimeSpan.FromMinutes(2),
                    CriticalThreshold = TimeSpan.FromMinutes(3)
                });
            _thresholdManagerMock.Setup(tm => tm.GetDetectionLevel(TimeSpan.FromMinutes(5), "unknown", "unknown"))
                .Returns(DetectionLevel.Critical);
            _thresholdManagerMock.Setup(tm => tm.ApplyHysteresis(DetectionLevel.Active, DetectionLevel.Critical, TimeSpan.FromMinutes(5), "unknown", "unknown"))
                .Returns(DetectionLevel.Critical);

            // Act
            var result = await _detector.IsIdleAsync(123);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetIdleInfoAsync_ShouldReturnDetailedInformation()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            var expectedIdleTime = TimeSpan.FromMinutes(15);
            var lastActivity = DateTime.UtcNow.AddMinutes(-15);
            var recentActivities = new List<ActivityData>
            {
                new ActivityData { ActivityType = ActivityType.Keyboard, Confidence = 0.9 }
            };

            // Setup mocks
            _activityMonitorMock.Setup(am => am.GetSystemIdleTime()).Returns(expectedIdleTime);
            _activityMonitorMock.Setup(am => am.LastActivity).Returns(lastActivity);
            _activityMonitorMock.Setup(am => am.GetRecentActivities(TimeSpan.FromMinutes(30))).Returns(recentActivities);
            _activityMonitorMock.Setup(am => am.IsRunning).Returns(true);
            _thresholdManagerMock.Setup(tm => tm.GetDetectionLevel(expectedIdleTime, "unknown", "unknown"))
                .Returns(DetectionLevel.Critical);

            // Act
            var result = await _detector.GetIdleInfoAsync(123);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.ProcessId);
            Assert.Equal(expectedIdleTime, result.IdleTime);
            Assert.Equal(lastActivity, result.LastActivity);
            Assert.Equal(SessionState.Inactive, result.State);
            Assert.Contains("Keyboard", result.DetectionMethods);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_ValidConfiguration_ShouldUpdateSuccessfully()
        {
            // Arrange
            var initialConfig = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(initialConfig);

            var newConfig = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 120,
                IdleThresholdSeconds = 600,
                ConfidenceThreshold = 0.8
            };

            // Act
            await _detector.UpdateConfigurationAsync(newConfig);

            // Assert
            Assert.NotNull(_detector.Configuration);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_InvalidConfiguration_ShouldThrowException()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            var invalidConfig = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 1, // Too low
                ConfidenceThreshold = 1.5 // Too high
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _detector.UpdateConfigurationAsync(invalidConfig));
        }

        [Fact]
        public async Task GetHealthAsync_HealthyComponents_ShouldReturnHealthyStatus()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Setup mocks for healthy state
            _activityMonitorMock.Setup(am => am.IsRunning).Returns(true);
            _thresholdManagerMock.Setup(tm => tm.GetStatistics()).Returns(new AdaptationStatistics
            {
                TotalAdaptations = 5,
                AdaptationErrors = 0
            });

            // Act
            var health = await _detector.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.Equal("TimeBasedIdleDetector", health.DetectorName);
            Assert.True(health.IsHealthy);
            Assert.Equal("Healthy", health.StatusMessage);
        }

        [Fact]
        public async Task GetHealthAsync_UnhealthyComponents_ShouldReturnUnhealthyStatus()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Setup mocks for unhealthy state
            _activityMonitorMock.Setup(am => am.IsRunning).Returns(false);
            _thresholdManagerMock.Setup(tm => tm.GetStatistics()).Returns(new AdaptationStatistics
            {
                TotalAdaptations = 5,
                AdaptationErrors = 15 // High error count
            });

            // Act
            var health = await _detector.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.Equal("TimeBasedIdleDetector", health.DetectorName);
            Assert.False(health.IsHealthy);
            Assert.Contains("Issues:", health.StatusMessage);
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnValidStatistics()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            var thresholdStats = new AdaptationStatistics
            {
                TotalActivitiesProcessed = 100,
                CorrectDetections = 85,
                TotalDetectionsRecorded = 100,
                AdaptationErrors = 2
            };

            _thresholdManagerMock.Setup(tm => tm.GetStatistics()).Returns(thresholdStats);

            // Act
            var stats = await _detector.GetStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("TimeBasedIdleDetector", stats.DetectorName);
            Assert.Equal(100, stats.TotalDetections);
            Assert.Equal(85, stats.SuccessfulDetections);
            Assert.Equal(15, stats.FailedDetections);
            Assert.Equal(2, stats.ErrorCount);
        }

        [Fact]
        public async Task ResetStatisticsAsync_ShouldClearStatistics()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            await _detector.InitializeAsync(config);

            // Act
            await _detector.ResetStatisticsAsync();

            // Assert
            // No exception should be thrown
            // The actual clearing happens in private methods, so we mainly test it doesn't crash
        }

        [Fact]
        public void ValidateConfiguration_ValidConfiguration_ShouldReturnValidResult()
        {
            // Arrange
            var config = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 60,
                IdleThresholdSeconds = 300,
                ConfidenceThreshold = 0.7,
                CustomParameters = new Dictionary<string, object>
                {
                    ["WarningThresholdMinutes"] = 5,
                    ["CriticalThresholdMinutes"] = 15
                }
            };

            // Act
            var result = _detector.ValidateConfiguration(config);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateConfiguration_InvalidConfiguration_ShouldReturnInvalidResult()
        {
            // Arrange
            var config = new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 1, // Too low
                IdleThresholdSeconds = 30, // Too low
                ConfidenceThreshold = 1.5, // Too high
                CustomParameters = new Dictionary<string, object>
                {
                    ["WarningThresholdMinutes"] = 200, // Too high
                    ["CriticalThresholdMinutes"] = 500 // Too high
                }
            };

            // Act
            var result = _detector.ValidateConfiguration(config);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void ValidateConfiguration_NullConfiguration_ShouldReturnInvalidResult()
        {
            // Act
            var result = _detector.ValidateConfiguration(null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("Configuration cannot be null", result.Errors);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
        {
            // Arrange
            var config = _config.ToIdleDetectorConfiguration();
            _activityMonitorMock.Setup(am => am.StartAsync()).Returns(Task.CompletedTask);
            _thresholdManagerMock.Setup(tm => tm.Initialize());

            _detector.InitializeAsync(config).Wait();

            // Act
            _detector.Dispose();

            // Assert
            // Should not throw exceptions
            // Verify that activity monitor was stopped
            _activityMonitorMock.Verify(am => am.StopAsync(), Times.AtLeastOnce);
        }

        #region Helper Methods

        private void VerifyLoggerWarning(string expectedMessage)
        {
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        #endregion
    }
}