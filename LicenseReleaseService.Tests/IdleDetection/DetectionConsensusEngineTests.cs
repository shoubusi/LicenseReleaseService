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
    public class DetectionConsensusEngineTests
    {
        private readonly Mock<ILogger<DetectionConsensusEngine>> _mockLogger;
        private readonly Mock<SessionStateManager> _mockSessionStateManager;
        private readonly ConsensusConfiguration _configuration;
        private readonly DetectionConsensusEngine _engine;

        public DetectionConsensusEngineTests()
        {
            _mockLogger = new Mock<ILogger<DetectionConsensusEngine>>();
            _mockSessionStateManager = new Mock<SessionStateManager>(
                new SessionStateConfiguration(), Mock.Of<ILogger<SessionStateManager>>());

            _configuration = new ConsensusConfiguration
            {
                ConsensusMethod = ConsensusMethod.Threshold,
                Threshold = 0.7,
                MinimumConfidence = 0.5,
                DetectionTimeoutMs = 5000,
                DetectorWeights = new Dictionary<string, double>(),
                DetectorPriorities = new Dictionary<string, int>()
            };

            _engine = new DetectionConsensusEngine(
                _configuration, _mockSessionStateManager.Object, _mockLogger.Object);
        }

        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Assert
            Assert.NotNull(_engine);
            Assert.Equal(0, _engine.DetectorCount);
            Assert.Equal(0, _engine.EnabledDetectorCount);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DetectionConsensusEngine(
                null, _mockSessionStateManager.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullSessionStateManager_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DetectionConsensusEngine(
                _configuration, null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DetectionConsensusEngine(
                _configuration, _mockSessionStateManager.Object, null));
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetInitializedToTrue()
        {
            // Arrange
            Assert.False(_engine.IsInitialized);

            // Act
            await _engine.InitializeAsync();

            // Assert
            Assert.True(_engine.IsInitialized);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ShouldNotThrow()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            await _engine.InitializeAsync(); // Should not throw
            Assert.True(_engine.IsInitialized);
        }

        [Fact]
        public async Task InitializeAsync_WithInvalidThreshold_ShouldThrowArgumentException()
        {
            // Arrange
            var invalidConfig = new ConsensusConfiguration
            {
                ConsensusMethod = ConsensusMethod.Threshold,
                Threshold = 1.5, // Invalid: > 1.0
                MinimumConfidence = 0.5,
                DetectionTimeoutMs = 5000
            };
            var invalidEngine = new DetectionConsensusEngine(
                invalidConfig, _mockSessionStateManager.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => invalidEngine.InitializeAsync());
        }

        #endregion

        #region AddDetector Tests

        [Fact]
        public void AddDetector_WithValidDetector_ShouldAddDetector()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");

            // Act
            _engine.AddDetector(mockDetector.Object);

            // Assert
            Assert.Equal(1, _engine.DetectorCount);
            Assert.True(_engine.Detectors.ContainsKey("TestDetector"));
        }

        [Fact]
        public void AddDetector_WithNullDetector_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _engine.AddDetector(null));
        }

        [Fact]
        public void AddDetector_WithDuplicateDetectorName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector1 = CreateMockDetector("TestDetector");
            var mockDetector2 = CreateMockDetector("TestDetector");

            _engine.AddDetector(mockDetector1.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _engine.AddDetector(mockDetector2.Object));
        }

        [Fact]
        public void AddDetector_WhenNotInitialized_ShouldNotThrow()
        {
            // Arrange
            var mockDetector = CreateMockDetector("TestDetector");

            // Act & Assert
            _engine.AddDetector(mockDetector.Object); // Should not throw
            Assert.Equal(1, _engine.DetectorCount);
        }

        #endregion

        #region RemoveDetector Tests

        [Fact]
        public void RemoveDetector_WithExistingDetector_ShouldRemoveDetector()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector");
            _engine.AddDetector(mockDetector.Object);

            // Act
            _engine.RemoveDetector("TestDetector");

            // Assert
            Assert.Equal(0, _engine.DetectorCount);
            Assert.False(_engine.Detectors.ContainsKey("TestDetector"));
        }

        [Fact]
        public void RemoveDetector_WithNonexistentDetector_ShouldNotThrow()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            _engine.RemoveDetector("NonexistentDetector"); // Should not throw
            Assert.Equal(0, _engine.DetectorCount);
        }

        [Fact]
        public void RemoveDetector_WithNullDetectorName_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _engine.RemoveDetector(null));
        }

        [Fact]
        public void RemoveDetector_WithEmptyDetectorName_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _engine.RemoveDetector(""));
        }

        #endregion

        #region DetectIdleAsync Tests

        [Fact]
        public async Task DetectIdleAsync_WithValidParameters_ShouldReturnResult()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector", isIdle: true, confidence: 0.8);
            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle);
            Assert.Equal(1234, result.ProcessId);
            Assert.Equal("testuser", result.UserName);
            Assert.Equal("TESTPC", result.ComputerName);
            Assert.Equal(1, _engine.TotalConsensusEvaluations);
            Assert.Equal(1, _engine.SuccessfulConsensusEvaluations);
        }

        [Fact]
        public async Task DetectIdleAsync_WithNullProcessInfo_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _engine.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.DetectIdleAsync(null));
        }

        [Fact]
        public async Task DetectIdleAsync_WhenNotInitialized_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.DetectIdleAsync(processInfo));
        }

        [Fact]
        public async Task DetectIdleAsync_WithNoDetectors_ShouldReturnEmptyResult()
        {
            // Arrange
            await _engine.InitializeAsync();
            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            Assert.Equal(0, result.Confidence);
            Assert.Contains("No detectors available", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_WithDisabledDetectors_ShouldReturnEmptyResult()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector", isIdle: true, confidence: 0.8);
            mockDetector.Setup(x => x.IsEnabled).Returns(false);
            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            Assert.Equal(0, result.Confidence);
            Assert.Contains("No detectors available", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_WithDetectorFailure_ShouldHandleGracefully()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = new Mock<IIdleDetector>();
            mockDetector.Setup(x => x.Name).Returns("TestDetector");
            mockDetector.Setup(x => x.IsEnabled).Returns(true);
            mockDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Detector failed"));

            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            Assert.Equal(0, result.Confidence);
            Assert.Equal(1, _engine.TotalConsensusEvaluations);
            Assert.Equal(1, _engine.SuccessfulConsensusEvaluations);
        }

        #endregion

        #region Consensus Method Tests

        [Fact]
        public async Task DetectIdleAsync_WithMajorityConsensus_ShouldApplyMajorityLogic()
        {
            // Arrange
            _configuration.ConsensusMethod = ConsensusMethod.Majority;
            await _engine.InitializeAsync();

            // Add 3 detectors: 2 idle, 1 active
            var mockDetector1 = CreateMockDetector("Detector1", isIdle: true, confidence: 0.8);
            var mockDetector2 = CreateMockDetector("Detector2", isIdle: true, confidence: 0.7);
            var mockDetector3 = CreateMockDetector("Detector3", isIdle: false, confidence: 0.9);

            _engine.AddDetector(mockDetector1.Object);
            _engine.AddDetector(mockDetector2.Object);
            _engine.AddDetector(mockDetector3.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle); // Majority (2/3) say idle
            Assert.Contains("Majority consensus", result.Reason);
            Assert.Contains("2/3", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_WithUnanimousConsensus_ShouldRequireUnanimousAgreement()
        {
            // Arrange
            _configuration.ConsensusMethod = ConsensusMethod.Unanimous;
            await _engine.InitializeAsync();

            // Add 3 detectors: 2 idle, 1 active (not unanimous)
            var mockDetector1 = CreateMockDetector("Detector1", isIdle: true, confidence: 0.8);
            var mockDetector2 = CreateMockDetector("Detector2", isIdle: true, confidence: 0.7);
            var mockDetector3 = CreateMockDetector("Detector3", isIdle: false, confidence: 0.9);

            _engine.AddDetector(mockDetector1.Object);
            _engine.AddDetector(mockDetector2.Object);
            _engine.AddDetector(mockDetector3.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle); // Not unanimous
            Assert.Contains("detectors disagree", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_WithThresholdConsensus_ShouldApplyThresholdLogic()
        {
            // Arrange
            _configuration.ConsensusMethod = ConsensusMethod.Threshold;
            _configuration.Threshold = 0.6; // 60% threshold
            await _engine.InitializeAsync();

            // Add 3 detectors: 2 idle, 1 active (66% idle >= 60% threshold)
            var mockDetector1 = CreateMockDetector("Detector1", isIdle: true, confidence: 0.8);
            var mockDetector2 = CreateMockDetector("Detector2", isIdle: true, confidence: 0.7);
            var mockDetector3 = CreateMockDetector("Detector3", isIdle: false, confidence: 0.9);

            _engine.AddDetector(mockDetector1.Object);
            _engine.AddDetector(mockDetector2.Object);
            _engine.AddDetector(mockDetector3.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle); // 66% >= 60% threshold
            Assert.Contains("Threshold consensus", result.Reason);
            Assert.Contains("66.67%", result.Reason);
            Assert.Contains("60.00%", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_WithWeightedConsensus_ShouldApplyWeightedLogic()
        {
            // Arrange
            _configuration.ConsensusMethod = ConsensusMethod.Weighted;
            _configuration.DetectorWeights = new Dictionary<string, double>
            {
                { "HighWeightDetector", 3.0 },
                { "LowWeightDetector", 1.0 }
            };
            await _engine.InitializeAsync();

            // High weight detector says idle, low weight detector says active
            var mockHighWeight = CreateMockDetector("HighWeightDetector", isIdle: true, confidence: 0.8);
            var mockLowWeight = CreateMockDetector("LowWeightDetector", isIdle: false, confidence: 0.9);

            _engine.AddDetector(mockHighWeight.Object);
            _engine.AddDetector(mockLowWeight.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle); // High weight detector wins
            Assert.Contains("Weighted consensus", result.Reason);
        }

        [Fact]
        public async Task DetectIdleAsync_WithHierarchicalConsensus_ShouldApplyPriorityLogic()
        {
            // Arrange
            _configuration.ConsensusMethod = ConsensusMethod.Hierarchical;
            _configuration.DetectorPriorities = new Dictionary<string, int>
            {
                { "HighPriorityDetector", 10 },
                { "LowPriorityDetector", 1 }
            };
            _configuration.MinimumConfidence = 0.7;
            await _engine.InitializeAsync();

            // High priority detector has sufficient confidence
            var mockHighPriority = CreateMockDetector("HighPriorityDetector", isIdle: false, confidence: 0.8);
            var mockLowPriority = CreateMockDetector("LowPriorityDetector", isIdle: true, confidence: 0.9);

            _engine.AddDetector(mockHighPriority.Object);
            _engine.AddDetector(mockLowPriority.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle); // High priority detector decides
            Assert.Contains("Hierarchical consensus", result.Reason);
            Assert.Contains("HighPriorityDetector", result.Reason);
        }

        #endregion

        #region Session State Management Tests

        [Fact]
        public async Task DetectIdleAsync_WithIdleResult_ShouldNotRecordActivity()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector", isIdle: true, confidence: 0.8);
            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            _mockSessionStateManager.Setup(x => x.UpdateSessionState(
                It.IsAny<string>(), It.IsAny<SessionState>(), It.IsAny<double>(), It.IsAny<string>()))
                .Returns(true);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsIdle);
            _mockSessionStateManager.Verify(x => x.RecordActivity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockSessionStateManager.Verify(x => x.UpdateSessionState(
                It.IsAny<string>(), SessionState.Idle, It.IsAny<double>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DetectIdleAsync_WithActiveResult_ShouldRecordActivity()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector", isIdle: false, confidence: 0.8);
            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            _mockSessionStateManager.Setup(x => x.UpdateSessionState(
                It.IsAny<string>(), It.IsAny<SessionState>(), It.IsAny<double>(), It.IsAny<string>()))
                .Returns(true);

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsIdle);
            _mockSessionStateManager.Verify(x => x.RecordActivity(
                It.IsAny<string>(), "ConsensusActivityDetected", It.IsAny<string>()), Times.Once);
            _mockSessionStateManager.Verify(x => x.UpdateSessionState(
                It.IsAny<string>(), SessionState.Active, It.IsAny<double>(), It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public void GetStatistics_WithNoEvaluations_ShouldReturnDefaultStatistics()
        {
            // Arrange
            // No evaluations performed

            // Act
            var stats = _engine.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalEvaluations);
            Assert.Equal(0, stats.SuccessfulEvaluations);
            Assert.Equal(0.0, stats.SuccessRate);
            Assert.Equal(0, stats.DetectorCount);
            Assert.Equal(0, stats.EnabledDetectorCount);
            Assert.Equal(ConsensusMethod.Threshold, stats.ConsensusMethod);
        }

        [Fact]
        public void GetStatistics_WithEvaluations_ShouldReturnCorrectStatistics()
        {
            // Arrange
            var mockDetector = CreateMockDetector("TestDetector");
            _engine.AddDetector(mockDetector.Object);

            // Simulate some statistics by accessing private fields through reflection
            var type = typeof(DetectionConsensusEngine);
            var totalField = type.GetField("_totalConsensusEvaluations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var successfulField = type.GetField("_successfulConsensusEvaluations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            totalField?.SetValue(_engine, 10L);
            successfulField?.SetValue(_engine, 8L);

            // Act
            var stats = _engine.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(10, stats.TotalEvaluations);
            Assert.Equal(8, stats.SuccessfulEvaluations);
            Assert.Equal(0.8, stats.SuccessRate);
            Assert.Equal(1, stats.DetectorCount);
            Assert.Equal(1, stats.EnabledDetectorCount);
        }

        #endregion

        #region GetConsensusInfo Tests

        [Fact]
        public void GetConsensusInfo_WithValidSessionId_ShouldReturnConsensusInfo()
        {
            // Arrange
            var sessionId = "test-session-123";

            // Act
            var info = _engine.GetConsensusInfo(sessionId);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(sessionId, info.SessionId);
            Assert.Equal(ConsensusMethod.Threshold, info.ConsensusMethod);
            Assert.Equal(0, info.DetectorCount);
            Assert.Equal(0.7, info.Threshold);
        }

        [Fact]
        public void GetConsensusInfo_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _engine.GetConsensusInfo(null));
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task DetectIdleAsync_WithIdleResult_ShouldRaiseIdleDetectedEvent()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector", isIdle: true, confidence: 0.8);
            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            var eventRaised = false;
            IdleDetectionEventArgs capturedEventArgs = null;
            _engine.IdleDetected += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.True(capturedEventArgs.IsIdle);
            Assert.Equal(1234, capturedEventArgs.ProcessId);
        }

        [Fact]
        public async Task DetectIdleAsync_WithActiveResult_ShouldRaiseActivityDetectedEvent()
        {
            // Arrange
            await _engine.InitializeAsync();
            var mockDetector = CreateMockDetector("TestDetector", isIdle: false, confidence: 0.8);
            _engine.AddDetector(mockDetector.Object);

            var processInfo = new ProcessInfo(1234, "test.exe", "testuser", "TESTPC");
            var mockSessionEntry = CreateMockSessionEntry();

            _mockSessionStateManager.Setup(x => x.GetOrCreateSession(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSessionEntry.Object);

            var eventRaised = false;
            IdleDetectionEventArgs capturedEventArgs = null;
            _engine.ActivityDetected += (sender, args) =>
            {
                eventRaised = true;
                capturedEventArgs = args;
            };

            // Act
            var result = await _engine.DetectIdleAsync(processInfo);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedEventArgs);
            Assert.False(capturedEventArgs.IsIdle);
            Assert.Equal(1234, capturedEventArgs.ProcessId);
        }

        #endregion

        #region Helper Methods

        private Mock<IIdleDetector> CreateMockDetector(string name, bool isIdle = true, double confidence = 0.8)
        {
            var mockDetector = new Mock<IIdleDetector>();
            mockDetector.Setup(x => x.Name).Returns(name);
            mockDetector.Setup(x => x.IsEnabled).Returns(true);
            mockDetector.Setup(x => x.Version).Returns(new Version(1, 0, 0));
            mockDetector.Setup(x => x.Priority).Returns(1);
            mockDetector.Setup(x => x.Description).Returns($"Mock detector {name}");

            mockDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = isIdle,
                    Confidence = confidence,
                    IdleTime = TimeSpan.FromMinutes(isIdle ? 10 : 0),
                    DetectionMethod = "MockMethod",
                    DetectorName = name
                });

            return mockDetector;
        }

        private Mock<SessionStateEntry> CreateMockSessionEntry()
        {
            var mockEntry = new Mock<SessionStateEntry>();
            mockEntry.Setup(x => x.SessionId).Returns("test-session-123");
            mockEntry.Setup(x => x.ProcessId).Returns(1234);
            mockEntry.Setup(x => x.UserName).Returns("testuser");
            mockEntry.Setup(x => x.ComputerName).Returns("TESTPC");
            mockEntry.Setup(x => x.State).Returns(SessionState.Active);
            mockEntry.Setup(x => x.Lock).Returns(new object());

            return mockEntry;
        }

        #endregion
    }
}