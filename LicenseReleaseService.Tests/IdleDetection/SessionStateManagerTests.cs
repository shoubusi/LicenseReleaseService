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
    public class SessionStateManagerTests
    {
        private readonly Mock<ILogger<SessionStateManager>> _mockLogger;
        private readonly SessionStateConfiguration _configuration;
        private readonly SessionStateManager _manager;

        public SessionStateManagerTests()
        {
            _mockLogger = new Mock<ILogger<SessionStateManager>>();
            _configuration = new SessionStateConfiguration
            {
                ConfidenceThreshold = 0.7,
                ActiveThreshold = TimeSpan.FromSeconds(30),
                SessionExpiration = TimeSpan.FromHours(24),
                ActivityHistoryRetention = TimeSpan.FromHours(2),
                StateHistoryRetention = TimeSpan.FromHours(4),
                ActiveStateHoldTime = TimeSpan.FromMinutes(1),
                IdleStateHoldTime = TimeSpan.FromMinutes(5),
                LongIdleStateHoldTime = TimeSpan.FromMinutes(15),
                InactiveStateHoldTime = TimeSpan.FromMinutes(30),
                DefaultStateHoldTime = TimeSpan.FromMinutes(2),
                FlappingDetectionWindow = TimeSpan.FromMinutes(10),
                MinimumFlappingTransitions = 5,
                FlappingThreshold = 2.0
            };

            _manager = new SessionStateManager(_configuration, _mockLogger.Object);
        }

        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Assert
            Assert.NotNull(_manager);
            Assert.Equal(0, _manager.SessionCount);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SessionStateManager(null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SessionStateManager(_configuration, null));
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetInitializedToTrue()
        {
            // Arrange
            Assert.False(_manager.IsInitialized);

            // Act
            await _manager.InitializeAsync();

            // Assert
            Assert.True(_manager.IsInitialized);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ShouldNotThrow()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act & Assert
            await _manager.InitializeAsync(); // Should not throw
            Assert.True(_manager.IsInitialized);
        }

        #endregion

        #region GetOrCreateSession Tests

        [Fact]
        public void GetOrCreateSession_WithValidParameters_ShouldCreateNewSession()
        {
            // Arrange
            var sessionId = "test-session-123";
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            // Act
            var entry = _manager.GetOrCreateSession(sessionId, processId, userName, computerName);

            // Assert
            Assert.NotNull(entry);
            Assert.Equal(sessionId, entry.SessionId);
            Assert.Equal(processId, entry.ProcessId);
            Assert.Equal(userName, entry.UserName);
            Assert.Equal(computerName, entry.ComputerName);
            Assert.Equal(SessionState.Active, entry.State);
            Assert.Equal(1, _manager.SessionCount);
        }

        [Fact]
        public void GetOrCreateSession_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.GetOrCreateSession(null, 1234, "user", "computer"));
        }

        [Fact]
        public void GetOrCreateSession_WithNullUserName_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.GetOrCreateSession("session", 1234, null, "computer"));
        }

        [Fact]
        public void GetOrCreateSession_WithNullComputerName_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.GetOrCreateSession("session", 1234, "user", null));
        }

        [Fact]
        public void GetOrCreateSession_WithExistingSession_ShouldReturnExistingEntry()
        {
            // Arrange
            var sessionId = "test-session-123";
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";

            // Act
            var entry1 = _manager.GetOrCreateSession(sessionId, processId, userName, computerName);
            var entry2 = _manager.GetOrCreateSession(sessionId, processId, userName, computerName);

            // Assert
            Assert.Same(entry1, entry2);
            Assert.Equal(1, _manager.SessionCount);
        }

        #endregion

        #region UpdateSessionState Tests

        [Fact]
        public void UpdateSessionState_WithValidParameters_ShouldUpdateState()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";
            var entry = _manager.GetOrCreateSession(sessionId, processId, userName, computerName);

            // Act
            var result = _manager.UpdateSessionState(sessionId, SessionState.Idle, 0.8, "User idle timeout");

            // Assert
            Assert.True(result);
            Assert.Equal(SessionState.Idle, entry.State);
            Assert.True(_manager.TotalStateTransitions > 0);
        }

        [Fact]
        public void UpdateSessionState_WithNonexistentSession_ShouldReturnFalse()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act
            var result = _manager.UpdateSessionState("nonexistent-session", SessionState.Idle, 0.8, "Test");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void UpdateSessionState_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.UpdateSessionState(null, SessionState.Idle, 0.8, "Test"));
        }

        [Fact]
        public void UpdateSessionState_WithSameState_ShouldNotUpdate()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");
            var initialState = entry.State;
            var initialTransitions = _manager.TotalStateTransitions;

            // Act
            var result = _manager.UpdateSessionState(sessionId, initialState, 0.8, "Same state");

            // Assert
            Assert.False(result);
            Assert.Equal(initialState, entry.State);
            Assert.Equal(initialTransitions, _manager.TotalStateTransitions);
        }

        [Fact]
        public void UpdateSessionState_WithLowConfidence_ShouldSuppressTransition()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");
            var initialTransitions = _manager.TotalStateTransitions;
            var initialSuppressed = _manager.SuppressedTransitions;

            // Act
            var result = _manager.UpdateSessionState(sessionId, SessionState.Idle, 0.3, "Low confidence");

            // Assert
            Assert.False(result);
            Assert.Equal(SessionState.Active, entry.State);
            Assert.Equal(initialTransitions, _manager.TotalStateTransitions);
            Assert.True(_manager.SuppressedTransitions > initialSuppressed);
        }

        #endregion

        #region RecordActivity Tests

        [Fact]
        public void RecordActivity_WithValidParameters_ShouldRecordActivity()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");
            var initialActivityTime = entry.LastActivityTime;

            // Act
            _manager.RecordActivity(sessionId, "MouseMove", "User moved mouse");

            // Assert
            Assert.True(entry.LastActivityTime > initialActivityTime);
            Assert.Single(entry.ActivityHistory);
            Assert.Equal("MouseMove", entry.ActivityHistory[0].ActivityType);
            Assert.Equal("User moved mouse", entry.ActivityHistory[0].Details);
        }

        [Fact]
        public void RecordActivity_WithNonexistentSession_ShouldNotThrow()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act & Assert
            _manager.RecordActivity("nonexistent-session", "Test"); // Should not throw
        }

        [Fact]
        public void RecordActivity_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.RecordActivity(null, "Test"));
        }

        [Fact]
        public void RecordActivity_WithIdleSession_ShouldTransitionToActive()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");
            _manager.UpdateSessionState(sessionId, SessionState.Idle, 0.8, "User idle");
            Assert.Equal(SessionState.Idle, entry.State);

            // Act
            _manager.RecordActivity(sessionId, "KeyPress", "User pressed key");

            // Assert
            Assert.Equal(SessionState.Active, entry.State);
        }

        #endregion

        #region GetSessionState Tests

        [Fact]
        public void GetSessionState_WithExistingSession_ShouldReturnState()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");

            // Act
            var state = _manager.GetSessionState(sessionId);

            // Assert
            Assert.NotNull(state);
            Assert.Equal(SessionState.Active, state.Value);
        }

        [Fact]
        public void GetSessionState_WithNonexistentSession_ShouldReturnNull()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act
            var state = _manager.GetSessionState("nonexistent-session");

            // Assert
            Assert.Null(state);
        }

        [Fact]
        public void GetSessionState_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.GetSessionState(null));
        }

        #endregion

        #region GetSessionInfo Tests

        [Fact]
        public void GetSessionInfo_WithExistingSession_ShouldReturnSessionInfo()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";
            _manager.GetOrCreateSession(sessionId, processId, userName, computerName);

            // Act
            var info = _manager.GetSessionInfo(sessionId);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(sessionId, info.SessionId);
            Assert.Equal(processId, info.ProcessId);
            Assert.Equal(userName, info.UserName);
            Assert.Equal(computerName, info.ComputerName);
            Assert.Equal(SessionState.Active, info.State);
        }

        [Fact]
        public void GetSessionInfo_WithNonexistentSession_ShouldReturnNull()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act
            var info = _manager.GetSessionInfo("nonexistent-session");

            // Assert
            Assert.Null(info);
        }

        [Fact]
        public void GetSessionInfo_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.GetSessionInfo(null));
        }

        #endregion

        #region GetActiveSessions Tests

        [Fact]
        public void GetActiveSessions_WithMultipleSessions_ShouldReturnActiveSessions()
        {
            // Arrange
            await _manager.InitializeAsync();
            _manager.GetOrCreateSession("session1", 1234, "user1", "computer1");
            _manager.GetOrCreateSession("session2", 1235, "user2", "computer2");
            _manager.UpdateSessionState("session2", SessionState.Inactive, 0.9, "Test inactive");

            // Act
            var activeSessions = _manager.GetActiveSessions();

            // Assert
            Assert.Single(activeSessions);
            Assert.Equal("session1", activeSessions[0].SessionId);
            Assert.Equal(SessionState.Active, activeSessions[0].State);
        }

        [Fact]
        public void GetActiveSessions_WithNoSessions_ShouldReturnEmptyList()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act
            var activeSessions = _manager.GetActiveSessions();

            // Assert
            Assert.Empty(activeSessions);
        }

        #endregion

        #region GetSessionsByState Tests

        [Fact]
        public void GetSessionsByState_WithMatchingState_ShouldReturnSessions()
        {
            // Arrange
            await _manager.InitializeAsync();
            _manager.GetOrCreateSession("session1", 1234, "user1", "computer1");
            _manager.GetOrCreateSession("session2", 1235, "user2", "computer2");
            _manager.UpdateSessionState("session2", SessionState.Idle, 0.8, "Test idle");

            // Act
            var idleSessions = _manager.GetSessionsByState(SessionState.Idle);

            // Assert
            Assert.Single(idleSessions);
            Assert.Equal("session2", idleSessions[0].SessionId);
            Assert.Equal(SessionState.Idle, idleSessions[0].State);
        }

        [Fact]
        public void GetSessionsByState_WithNoMatchingSessions_ShouldReturnEmptyList()
        {
            // Arrange
            await _manager.InitializeAsync();
            _manager.GetOrCreateSession("session1", 1234, "user1", "computer1");

            // Act
            var inactiveSessions = _manager.GetSessionsByState(SessionState.Inactive);

            // Assert
            Assert.Empty(inactiveSessions);
        }

        #endregion

        #region RemoveSession Tests

        [Fact]
        public void RemoveSession_WithExistingSession_ShouldRemoveSession()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");
            Assert.Equal(1, _manager.SessionCount);

            // Act
            var result = _manager.RemoveSession(sessionId, "Test removal");

            // Assert
            Assert.True(result);
            Assert.Equal(0, _manager.SessionCount);
        }

        [Fact]
        public void RemoveSession_WithNonexistentSession_ShouldReturnFalse()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act
            var result = _manager.RemoveSession("nonexistent-session", "Test");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveSession_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.RemoveSession(null, "Test"));
        }

        #endregion

        #region RemoveExpiredSessions Tests

        [Fact]
        public void RemoveExpiredSessions_WithExpiredSessions_ShouldRemoveExpiredSessions()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");

            // Simulate expired session by setting last activity time in the past
            entry.LastActivityTime = DateTime.UtcNow - TimeSpan.FromHours(25);
            Assert.Equal(1, _manager.SessionCount);

            // Act
            _manager.RemoveExpiredSessions();

            // Assert
            Assert.Equal(0, _manager.SessionCount);
        }

        [Fact]
        public void RemoveExpiredSessions_WithActiveSessions_ShouldNotRemoveActiveSessions()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");
            Assert.Equal(1, _manager.SessionCount);

            // Act
            _manager.RemoveExpiredSessions();

            // Assert
            Assert.Equal(1, _manager.SessionCount);
        }

        #endregion

        #region GetStatistics Tests

        [Fact]
        public void GetStatistics_WithNoSessions_ShouldReturnEmptyStatistics()
        {
            // Arrange
            await _manager.InitializeAsync();

            // Act
            var stats = _manager.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalSessions);
            Assert.Empty(stats.StateCounts);
            Assert.Equal(0, stats.TotalStateTransitions);
            Assert.Equal(0, stats.SuppressedTransitions);
            Assert.Equal(0.0, stats.SuppressionRate);
        }

        [Fact]
        public void GetStatistics_WithSessions_ShouldReturnCorrectStatistics()
        {
            // Arrange
            await _manager.InitializeAsync();
            _manager.GetOrCreateSession("session1", 1234, "user1", "computer1");
            _manager.GetOrCreateSession("session2", 1235, "user2", "computer2");
            _manager.UpdateSessionState("session2", SessionState.Idle, 0.8, "Test idle");

            // Act
            var stats = _manager.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(2, stats.TotalSessions);
            Assert.Equal(2, stats.StateCounts.Count);
            Assert.Equal(1, stats.StateCounts[SessionState.Active]);
            Assert.Equal(1, stats.StateCounts[SessionState.Idle]);
            Assert.Equal(1, stats.TotalStateTransitions);
            Assert.Equal(0, stats.SuppressedTransitions);
            Assert.Equal(0.0, stats.SuppressionRate);
        }

        #endregion

        #region ClearAllSessions Tests

        [Fact]
        public void ClearAllSessions_WithSessions_ShouldRemoveAllSessions()
        {
            // Arrange
            await _manager.InitializeAsync();
            _manager.GetOrCreateSession("session1", 1234, "user1", "computer1");
            _manager.GetOrCreateSession("session2", 1235, "user2", "computer2");
            Assert.Equal(2, _manager.SessionCount);

            // Act
            _manager.ClearAllSessions("Test clear");

            // Assert
            Assert.Equal(0, _manager.SessionCount);
        }

        [Fact]
        public void ClearAllSessions_WithNoSessions_ShouldNotThrow()
        {
            // Arrange
            await _manager.InitializeAsync();
            Assert.Equal(0, _manager.SessionCount);

            // Act & Assert
            _manager.ClearAllSessions("Test clear"); // Should not throw
            Assert.Equal(0, _manager.SessionCount);
        }

        #endregion

        #region Hysteresis Tests

        [Fact]
        public void UpdateSessionState_WithInsufficientHoldTime_ShouldSuppressTransition()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");

            // Set last state change time very recent (within hold time)
            entry.LastStateChangeTime = DateTime.UtcNow - TimeSpan.FromSeconds(30);

            var initialTransitions = _manager.TotalStateTransitions;
            var initialSuppressed = _manager.SuppressedTransitions;

            // Act
            var result = _manager.UpdateSessionState(sessionId, SessionState.Idle, 0.8, "Test");

            // Assert
            Assert.False(result);
            Assert.Equal(SessionState.Active, entry.State);
            Assert.Equal(initialTransitions, _manager.TotalStateTransitions);
            Assert.True(_manager.SuppressedTransitions > initialSuppressed);
        }

        [Fact]
        public void UpdateSessionState_WithStateFlapping_ShouldSuppressTransition()
        {
            // Arrange
            await _manager.InitializeAsync();
            var sessionId = "test-session-123";
            var entry = _manager.GetOrCreateSession(sessionId, 1234, "user", "computer");

            // Create rapid state transitions to simulate flapping
            var now = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                entry.StateHistory.Add(new StateTransition
                {
                    FromState = i % 2 == 0 ? SessionState.Active : SessionState.Idle,
                    ToState = i % 2 == 1 ? SessionState.Active : SessionState.Idle,
                    Timestamp = now - TimeSpan.FromMinutes(i),
                    Confidence = 0.8,
                    Reason = "Test"
                });
            }

            var initialTransitions = _manager.TotalStateTransitions;
            var initialSuppressed = _manager.SuppressedTransitions;

            // Act
            var result = _manager.UpdateSessionState(sessionId, SessionState.Idle, 0.8, "Test");

            // Assert
            Assert.False(result);
            Assert.Equal(initialTransitions, _manager.TotalStateTransitions);
            Assert.True(_manager.SuppressedTransitions > initialSuppressed);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            await _manager.InitializeAsync();
            _manager.GetOrCreateSession("session1", 1234, "user1", "computer1");
            _manager.GetOrCreateSession("session2", 1235, "user2", "computer2");

            // Act
            _manager.Dispose();

            // Assert
            // Note: After dispose, operations should throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => _manager.GetSessionState("session1"));
        }

        #endregion
    }
}