using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class IdleDetectionEngineTests
    {
        private readonly Mock<ILogger<IdleDetectionEngine>> _mockLogger;
        private readonly Mock<ITimerExecutionService> _mockTimerService;
        private readonly Mock<SessionStateManager> _mockSessionStateManager;
        private readonly Mock<DetectionConsensusEngine> _mockConsensusEngine;
        private readonly IdleDetectionConfiguration _configuration;
        private readonly IdleDetectionEngine _engine;

        public IdleDetectionEngineTests()
        {
            _mockLogger = new Mock<ILogger<IdleDetectionEngine>>();
            _mockTimerService = new Mock<ITimerExecutionService>();
            _mockSessionStateManager = new Mock<SessionStateManager>(
                new SessionStateConfiguration(), Mock.Of<ILogger<SessionStateManager>>());
            _mockConsensusEngine = new Mock<DetectionConsensusEngine>(
                new ConsensusConfiguration(), _mockSessionStateManager.Object, Mock.Of<ILogger<DetectionConsensusEngine>>());

            _configuration = new IdleDetectionConfiguration
            {
                DetectionIntervalSeconds = 60,
                IdleThresholdSeconds = 300,
                ConfidenceThreshold = 0.7,
                IsEnabled = true,
                Priority = 1,
                MaxDetectionTimeMs = 5000,
                TimeoutSeconds = 30,
                RetryCount = 3,
                RetryDelayMs = 1000,
                MaxConcurrentOperations = 5
            };

            _engine = new IdleDetectionEngine(
                _mockTimerService.Object,
                _mockSessionStateManager.Object,
                _mockConsensusEngine.Object,
                _configuration,
                _mockLogger.Object);
        }

        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Assert
            Assert.NotNull(_engine);
            Assert.False(_engine.IsInitialized);
            Assert.False(_engine.IsRunning);
            Assert.Equal(EngineStatus.NotInitialized, _engine.Status);
            Assert.Empty(_engine.Detectors);
            Assert.Equal(0, _engine.TotalDetections);
            Assert.Equal(0, _engine.SuccessfulDetections);
        }

        [Fact]
        public void Constructor_WithNullTimerService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEngine(
                null, _mockSessionStateManager.Object, _mockConsensusEngine.Object, _configuration, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullSessionStateManager_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEngine(
                _mockTimerService.Object, null, _mockConsensusEngine.Object, _configuration, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullConsensusEngine_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEngine(
                _mockTimerService.Object, _mockSessionStateManager.Object, null, _configuration, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEngine(
                _mockTimerService.Object, _mockSessionStateManager.Object, _mockConsensusEngine.Object, null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEngine(
                _mockTimerService.Object, _mockSessionStateManager.Object, _mockConsensusEngine.Object, _configuration, null));
        }

        [Fact]
        public async Task InitializeAsync_ShouldInitializeComponents()
        {
            // Arrange
            Assert.False(_engine.IsInitialized);

            // Act
            await _engine.InitializeAsync();

            // Assert
            Assert.True(_engine.IsInitialized);
            Assert.Equal(EngineStatus.Initialized, _engine.Status);
            _mockSessionStateManager.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockConsensusEngine.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockTimerService.VerifyAdd(x => x.ExecutionStarted += It.IsAny<EventHandler<TimerExecutionEventArgs>>(), Times.Once);
            _mockTimerService.VerifyAdd(x => x.ExecutionCompleted += It.IsAny<EventHandler<TimerExecutionEventArgs>>(), Times.Once);
            _mockTimerService.VerifyAdd(x => x.ExecutionError += It.IsAny<EventHandler<TimerErrorEventArgs>>(), Times.Once);
            _mockTimerService.VerifyAdd(x => x.StateChanged += It.IsAny<EventHandler<TimerStateChangedEventArgs>>(), Times.Once);
            _mockConsensusEngine.VerifyAdd(x => x.IdleDetected += It.IsAny<EventHandler<IdleDetectionEventArgs>>(), Times.Once);
            _mockConsensusEngine.VerifyAdd(x => x.ActivityDetected += It.IsAny<EventHandler<IdleDetectionEventArgs>>(), Times.Once);
            _mockConsensusEngine.VerifyAdd(x => x.ErrorOccurred += It.IsAny<EventHandler<DetectorErrorEventArgs>>(), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ShouldNotReinitialize()
        {
            // Arrange
            await _engine.InitializeAsync();
            _mockSessionStateManager.Invocations.Clear();
            _mockConsensusEngine.Invocations.Clear();

            // Act
            await _engine.InitializeAsync();

            // Assert
            Assert.True(_engine.IsInitialized);
            _mockSessionStateManager.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once); // Still only once
            _mockConsensusEngine.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once); // Still only once
        }

        #endregion

        #region Start and Stop Tests

        [Fact]
        public async Task StartAsync_WhenInitialized_ShouldStartEngine()
        {
            // Arrange
            await _engine.InitializeAsync();
            Assert.False(_engine.IsRunning);

            // Act
            await _engine.StartAsync();

            // Assert
            Assert.True(_engine.IsRunning);
            Assert.Equal(EngineStatus.Running, _engine.Status);
            _mockTimerService.Verify(x => x.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WithRegisteredDetectors_ShouldStartDetectors()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");
            await _engine.RegisterDetectorAsync(mockDetector.Object);

            // Act
            await _engine.StartAsync();

            // Assert
            mockDetector.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WhenNotInitialized_ShouldThrowInvalidOperationException()
        {
            // Arrange
            Assert.False(_engine.IsInitialized);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.StartAsync());
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();
            _mockTimerService.Invocations.Clear();

            // Act
            await _engine.StartAsync();

            // Assert
            Assert.True(_engine.IsRunning);
            _mockTimerService.Verify(x => x.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once); // Still only once
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopEngine()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();
            Assert.True(_engine.IsRunning);

            // Act
            await _engine.StopAsync();

            // Assert
            Assert.False(_engine.IsRunning);
            Assert.Equal(EngineStatus.Stopped, _engine.Status);
            _mockTimerService.Verify(x => x.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WithRunningDetectors_ShouldStopDetectors()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");
            await _engine.RegisterDetectorAsync(mockDetector.Object);
            await _engine.StartAsync();

            // Act
            await _engine.StopAsync();

            // Assert
            mockDetector.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldNotThrow()
        {
            // Arrange
            await _engine.InitializeAsync();
            Assert.False(_engine.IsRunning);

            // Act & Assert
            await _engine.StopAsync(); // Should not throw
            Assert.Equal(EngineStatus.Stopped, _engine.Status);
        }

        #endregion

        #region Register and Unregister Detector Tests

        [Fact]
        public async Task RegisterDetectorAsync_WithValidDetector_ShouldRegisterDetector()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");

            // Act
            await _engine.RegisterDetectorAsync(mockDetector.Object);

            // Assert
            Assert.Single(_engine.Detectors);
            Assert.Equal("TestDetector", _engine.Detectors[0].Name);
            mockDetector.Verify(x => x.InitializeAsync(_configuration, It.IsAny<CancellationToken>()), Times.Once);
            _mockConsensusEngine.Verify(x => x.AddDetector(mockDetector.Object), Times.Once);
        }

        [Fact]
        public async Task RegisterDetectorAsync_WithNullDetector_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.RegisterDetectorAsync(null));
        }

        [Fact]
        public async Task RegisterDetectorAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector1 = CreateMockDetector("TestDetector");
            var mockDetector2 = CreateMockDetector("TestDetector");
            await _engine.RegisterDetectorAsync(mockDetector1.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RegisterDetectorAsync(mockDetector2.Object));
        }

        [Fact]
        public async Task RegisterDetectorAsync_WhenEngineRunning_ShouldStartDetector()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();
            var mockDetector = CreateMockDetector("TestDetector");

            // Act
            await _engine.RegisterDetectorAsync(mockDetector.Object);

            // Assert
            mockDetector.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterDetectorAsync_WhenEngineNotRunning_ShouldNotStartDetector()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");

            // Act
            await _engine.RegisterDetectorAsync(mockDetector.Object);

            // Assert
            mockDetector.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UnregisterDetectorAsync_WithExistingDetector_ShouldUnregisterDetector()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");
            await _engine.RegisterDetectorAsync(mockDetector.Object);
            Assert.Single(_engine.Detectors);

            // Act
            await _engine.UnregisterDetectorAsync("TestDetector");

            // Assert
            Assert.Empty(_engine.Detectors);
            mockDetector.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockConsensusEngine.Verify(x => x.RemoveDetector("TestDetector"), Times.Once);
        }

        [Fact]
        public async Task UnregisterDetectorAsync_WithNonexistentDetector_ShouldNotThrow()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            await _engine.UnregisterDetectorAsync("NonexistentDetector"); // Should not throw
            Assert.Empty(_engine.Detectors);
        }

        [Fact]
        public async Task UnregisterDetectorAsync_WithNullName_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.UnregisterDetectorAsync(null));
        }

        [Fact]
        public async Task UnregisterDetectorAsync_WithEmptyName_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.UnregisterDetectorAsync(""));
        }

        #endregion

        #region DetectIdle Tests

        [Fact]
        public async Task DetectIdleAsync_WithValidParameters_ShouldReturnResult()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            var expectedResult = new IdleDetectionResult
            {
                IsIdle = true,
                Confidence = 0.8,
                IdleTime = TimeSpan.FromMinutes(10),
                ProcessId = 1234,
                UserName = "testuser",
                ComputerName = "TESTPC"
            };

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            _mockConsensusEngine.Setup(x => x.DetectIdleAsync(processInfo, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _engine.DetectIdleAsync(1234, "testuser", "TESTPC");

            // Assert
            Assert.NotNull(result);
            Assert.Same(expectedResult, result);
            Assert.Equal(1, _engine.TotalDetections);
            Assert.Equal(1, _engine.SuccessfulDetections);
        }

        [Fact]
        public async Task DetectIdleAsync_WithNullUserName_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.DetectIdleAsync(1234, null, "TESTPC"));
        }

        [Fact]
        public async Task DetectIdleAsync_WithNullComputerName_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.DetectIdleAsync(1234, "testuser", null));
        }

        [Fact]
        public async Task DetectIdleAsync_WhenNotInitialized_ShouldThrowInvalidOperationException()
        {
            // Arrange
            Assert.False(_engine.IsInitialized);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.DetectIdleAsync(1234, "testuser", "TESTPC"));
        }

        [Fact]
        public async Task DetectIdleAsync_WhenNotRunning_ShouldThrowInvalidOperationException()
        {
            // Arrange
            await _engine.InitializeAsync();
            Assert.False(_engine.IsRunning);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.DetectIdleAsync(1234, "testuser", "TESTPC"));
        }

        [Fact]
        public async Task DetectIdleAsync_WithConsensusEngineReturningNull_ShouldReturnDefaultResult()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            _mockConsensusEngine.Setup(x => x.DetectIdleAsync(processInfo, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IdleDetectionResult)null);

            // Act
            var result = await _engine.DetectIdleAsync(1234, "testuser", "TESTPC");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            Assert.Equal(0, result.Confidence);
            Assert.Equal(1, _engine.TotalDetections);
            Assert.Equal(0, _engine.SuccessfulDetections);
        }

        #endregion

        #region ForceDetection Tests

        [Fact]
        public async Task ForceDetectionAsync_WhenRunning_ShouldExecuteDetection()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            // Act
            await _engine.ForceDetectionAsync();

            // Assert
            _mockTimerService.Verify(x => x.ExecuteNowAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ForceDetectionAsync_WhenNotRunning_ShouldThrowInvalidOperationException()
        {
            // Arrange
            await _engine.InitializeAsync();
            Assert.False(_engine.IsRunning);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.ForceDetectionAsync());
        }

        #endregion

        #region Statistics and Health Tests

        [Fact]
        public void GetStatistics_WithNoDetections_ShouldReturnDefaultStatistics()
        {
            // Arrange
            // No detections performed

            // Act
            var stats = _engine.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(EngineStatus.NotInitialized, stats.Status);
            Assert.False(stats.IsRunning);
            Assert.False(stats.IsInitialized);
            Assert.Equal(0, stats.TotalDetections);
            Assert.Equal(0, stats.SuccessfulDetections);
            Assert.Equal(0.0, stats.SuccessRate);
            Assert.Equal(0, stats.DetectorCount);
            Assert.Equal(0, stats.EnabledDetectorCount);
            Assert.Equal(TimeSpan.FromSeconds(60), stats.DetectionInterval);
        }

        [Fact]
        public async Task GetStatistics_WithDetections_ShouldReturnCorrectStatistics()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            var expectedResult = new IdleDetectionResult
            {
                IsIdle = true,
                Confidence = 0.8,
                ProcessId = 1234,
                UserName = "testuser",
                ComputerName = "TESTPC"
            };

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            _mockConsensusEngine.Setup(x => x.DetectIdleAsync(processInfo, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Perform some detections
            await _engine.DetectIdleAsync(1234, "testuser", "TESTPC");
            await _engine.DetectIdleAsync(1234, "testuser", "TESTPC");

            // Act
            var stats = _engine.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(EngineStatus.Running, stats.Status);
            Assert.True(stats.IsRunning);
            Assert.True(stats.IsInitialized);
            Assert.Equal(2, stats.TotalDetections);
            Assert.Equal(2, stats.SuccessfulDetections);
            Assert.Equal(1.0, stats.SuccessRate);
        }

        [Fact]
        public async Task GetHealthAsync_WithHealthyComponents_ShouldReturnHealthyStatus()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            _mockTimerService.Setup(x => x.IsRunning).Returns(true);
            var mockTimerMetrics = new TimerPerformanceMetrics();
            _mockTimerService.Setup(x => x.GetMetrics()).Returns(mockTimerMetrics);

            var mockDetector = CreateMockDetector("TestDetector");
            await _engine.RegisterDetectorAsync(mockDetector.Object);

            var detectorHealth = new DetectorHealth { IsHealthy = true };
            mockDetector.Setup(x => x.GetHealthAsync()).ReturnsAsync(detectorHealth);

            // Act
            var health = await _engine.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.True(health.IsHealthy);
            Assert.Equal("Healthy", health.StatusMessage);
            Assert.NotNull(health.TimerServiceHealth);
            Assert.True(health.TimerServiceHealth.IsHealthy);
            Assert.Single(health.DetectorHealth);
            Assert.True(health.DetectorHealth[0].IsHealthy);
        }

        [Fact]
        public async Task GetHealthAsync_WithUnhealthyTimerService_ShouldReturnUnhealthyStatus()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();

            _mockTimerService.Setup(x => x.IsRunning).Returns(false);
            _mockTimerService.Setup(x => x.GetMetrics())
                .Throws(new InvalidOperationException("Timer service error"));

            // Act
            var health = await _engine.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.False(health.IsHealthy);
            Assert.NotNull(health.TimerServiceHealth);
            Assert.False(health.TimerServiceHealth.IsHealthy);
            Assert.Contains("error", health.TimerServiceHealth.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task TimerExecutionCompleted_ShouldRaiseDetectionCycleCompletedEvent()
        {
            // Arrange
            await _engine.InitializeAsync();

            var eventRaised = false;
            DetectionCycleCompletedEventArgs capturedEventArgs = null;
            _engine.DetectionCycleCompleted += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            var timerArgs = new TimerExecutionEventArgs
            {
                ExecutionId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(100)
            };

            // Act
            _mockTimerService.Raise(x => x.ExecutionCompleted += null, timerArgs);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.Equal(timerArgs.ExecutionId, capturedEventArgs.ExecutionId);
            Assert.True(capturedEventArgs.Success);
        }

        [Fact]
        public async Task TimerExecutionError_ShouldRaiseErrorOccurredEvent()
        {
            // Arrange
            await _engine.InitializeAsync();

            var eventRaised = false;
            EngineErrorEventArgs capturedEventArgs = null;
            _engine.ErrorOccurred += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            var timerError = new InvalidOperationException("Timer error");
            var timerErrorArgs = new TimerErrorEventArgs(
                timerError, TimerStatus.Error, 1, Guid.NewGuid());

            // Act
            _mockTimerService.Raise(x => x.ExecutionError += null, timerErrorArgs);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.Equal("TimerExecution", capturedEventArgs.Operation);
            Assert.Same(timerError, capturedEventArgs.Error);
        }

        [Fact]
        public async Task ConsensusIdleDetected_ShouldRaiseIdleDetectedEvent()
        {
            // Arrange
            await _engine.InitializeAsync();

            var eventRaised = false;
            IdleDetectionEventArgs capturedEventArgs = null;
            _engine.IdleDetected += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            var consensusArgs = new IdleDetectionEventArgs(
                "session-123", 1234, "testuser", "TESTPC",
                0.8, TimeSpan.FromMinutes(10), "TestMethod", "Test reason");

            // Act
            _mockConsensusEngine.Raise(x => x.IdleDetected += null, consensusArgs);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.Same(consensusArgs, capturedEventArgs);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopEngineAndDisposeResources()
        {
            // Arrange
            await _engine.InitializeAsync();
            await _engine.StartAsync();
            var mockDetector = CreateMockDetector("TestDetector");
            await _engine.RegisterDetectorAsync(mockDetector.Object);

            // Act
            _engine.Dispose();

            // Assert
            Assert.True(_engine.IsDisposed);
            Assert.Equal(EngineStatus.Disposed, _engine.Status);
            mockDetector.Verify(x => x.Dispose(), Times.Once);
            _mockTimerService.VerifyRemove(x => x.ExecutionStarted -= It.IsAny<EventHandler<TimerExecutionEventArgs>>(), Times.Once);
            _mockConsensusEngine.VerifyRemove(x => x.IdleDetected -= It.IsAny<EventHandler<IdleDetectionEventArgs>>(), Times.Once);
        }

        [Fact]
        public void Dispose_WhenNotInitialized_ShouldNotThrow()
        {
            // Arrange
            Assert.False(_engine.IsInitialized);

            // Act & Assert
            _engine.Dispose(); // Should not throw
            Assert.True(_engine.IsDisposed);
        }

        [Fact]
        public async Task Dispose_WhenAlreadyDisposed_ShouldNotThrow()
        {
            // Arrange
            _engine.Dispose();
            Assert.True(_engine.IsDisposed);

            // Act & Assert
            _engine.Dispose(); // Should not throw
            Assert.True(_engine.IsDisposed);
        }

        #endregion

        #region Helper Methods

        private Mock<IIdleDetector> CreateMockDetector(string name)
        {
            var mockDetector = new Mock<IIdleDetector>();
            mockDetector.Setup(x => x.Name).Returns(name);
            mockDetector.Setup(x => x.IsEnabled).Returns(true);
            mockDetector.Setup(x => x.Version).Returns(new Version(1, 0, 0));
            mockDetector.Setup(x => x.Priority).Returns(1);
            mockDetector.Setup(x => x.Description).Returns($"Mock detector {name}");

            mockDetector.Setup(x => x.InitializeAsync(It.IsAny<IdleDetectorConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockDetector.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockDetector.Setup(x => x.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockDetector.Setup(x => x.GetHealthAsync())
                .ReturnsAsync(new DetectorHealth { IsHealthy = true });

            return mockDetector;
        }

        #endregion
    }
}