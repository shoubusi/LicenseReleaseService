using System;
using System.Collections.Generic;
using Xunit;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class IdleDetectionEventsTests
    {
        #region IdleDetectionEventArgs Tests

        [Fact]
        public void IdleDetectionEventArgs_Constructor_WithValidParameters_ShouldInitializeProperties()
        {
            // Arrange
            var sessionId = "test-session-123";
            var processId = 1234;
            var userName = "testuser";
            var computerName = "TESTPC";
            var confidence = 0.85;
            var idleTime = TimeSpan.FromMinutes(15);
            var detectionMethod = "SystemIdle";
            var reason = "No user activity detected";

            // Act
            var args = new IdleDetectionEventArgs(
                sessionId, processId, userName, computerName,
                confidence, idleTime, detectionMethod, reason);

            // Assert
            Assert.Equal(sessionId, args.SessionId);
            Assert.Equal(processId, args.ProcessId);
            Assert.Equal(userName, args.UserName);
            Assert.Equal(computerName, args.ComputerName);
            Assert.Equal(confidence, args.Confidence);
            Assert.Equal(idleTime, args.IdleTime);
            Assert.Equal(detectionMethod, args.DetectionMethod);
            Assert.Equal(reason, args.Reason);
            Assert.NotEqual(default, args.Timestamp);
            Assert.NotNull(args.Metadata);
            Assert.Empty(args.Metadata);
        }

        [Fact]
        public void IdleDetectionEventArgs_Constructor_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEventArgs(
                null, 1234, "user", "computer", 0.5, TimeSpan.Zero, "method", "reason"));
        }

        [Fact]
        public void IdleDetectionEventArgs_Constructor_WithNullUserName_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEventArgs(
                "session", 1234, null, "computer", 0.5, TimeSpan.Zero, "method", "reason"));
        }

        [Fact]
        public void IdleDetectionEventArgs_Constructor_WithNullComputerName_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEventArgs(
                "session", 1234, "user", null, 0.5, TimeSpan.Zero, "method", "reason"));
        }

        [Fact]
        public void IdleDetectionEventArgs_Constructor_WithNullDetectionMethod_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEventArgs(
                "session", 1234, "user", "computer", 0.5, TimeSpan.Zero, null, "reason"));
        }

        [Fact]
        public void IdleDetectionEventArgs_Constructor_WithNullReason_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleDetectionEventArgs(
                "session", 1234, "user", "computer", 0.5, TimeSpan.Zero, "method", null));
        }

        [Fact]
        public void IdleDetectionEventArgs_AddMetadata_WithValidParameters_ShouldAddToMetadata()
        {
            // Arrange
            var args = new IdleDetectionEventArgs(
                "session", 1234, "user", "computer", 0.5, TimeSpan.Zero, "method", "reason");
            var key = "TestKey";
            var value = "TestValue";

            // Act
            args.AddMetadata(key, value);

            // Assert
            Assert.Single(args.Metadata);
            Assert.True(args.Metadata.ContainsKey(key));
            Assert.Equal(value, args.Metadata[key]);
        }

        [Fact]
        public void IdleDetectionEventArgs_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var args = new IdleDetectionEventArgs(
                "session-123", 1234, "testuser", "TESTPC",
                0.85, TimeSpan.FromMinutes(15.5), "SystemIdle", "No activity");

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("session-123", result);
            Assert.Contains("1234", result);
            Assert.Contains("testuser", result);
            Assert.Contains("TESTPC", result);
            Assert.Contains("0.85", result);
            Assert.Contains("15.5", result);
            Assert.Contains("SystemIdle", result);
            Assert.Contains("No activity", result);
        }

        #endregion

        #region SessionStateChangedEventArgs Tests

        [Fact]
        public void SessionStateChangedEventArgs_Constructor_WithValidParameters_ShouldInitializeProperties()
        {
            // Arrange
            var sessionId = "test-session-123";
            var processId = 1234;
            var previousState = SessionState.Active;
            var newState = SessionState.Idle;
            var reason = "User idle timeout";

            // Act
            var args = new SessionStateChangedEventArgs(
                sessionId, processId, previousState, newState, reason);

            // Assert
            Assert.Equal(sessionId, args.SessionId);
            Assert.Equal(processId, args.ProcessId);
            Assert.Equal(previousState, args.PreviousState);
            Assert.Equal(newState, args.NewState);
            Assert.Equal(reason, args.Reason);
            Assert.NotEqual(default, args.Timestamp);
            Assert.NotNull(args.Context);
            Assert.Empty(args.Context);
        }

        [Fact]
        public void SessionStateChangedEventArgs_Constructor_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SessionStateChangedEventArgs(
                null, 1234, SessionState.Active, SessionState.Idle, "reason"));
        }

        [Fact]
        public void SessionStateChangedEventArgs_Constructor_WithNullReason_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SessionStateChangedEventArgs(
                "session", 1234, SessionState.Active, SessionState.Idle, null));
        }

        [Fact]
        public void SessionStateChangedEventArgs_AddContext_WithValidParameters_ShouldAddToContext()
        {
            // Arrange
            var args = new SessionStateChangedEventArgs(
                "session", 1234, SessionState.Active, SessionState.Idle, "reason");
            var key = "TestKey";
            var value = "TestValue";

            // Act
            args.AddContext(key, value);

            // Assert
            Assert.Single(args.Context);
            Assert.True(args.Context.ContainsKey(key));
            Assert.Equal(value, args.Context[key]);
        }

        [Fact]
        public void SessionStateChangedEventArgs_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var args = new SessionStateChangedEventArgs(
                "session-123", 1234, SessionState.Active, SessionState.Idle, "User timeout");

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("session-123", result);
            Assert.Contains("1234", result);
            Assert.Contains("Active", result);
            Assert.Contains("Idle", result);
            Assert.Contains("User timeout", result);
        }

        #endregion

        #region ConsensusDetectionEventArgs Tests

        [Fact]
        public void ConsensusDetectionEventArgs_Constructor_WithValidParameters_ShouldInitializeProperties()
        {
            // Arrange
            var sessionId = "test-session-123";
            var processId = 1234;
            var decision = ConsensusDecision.Idle;
            var confidence = 0.9;
            var detectorCount = 3;
            var agreementCount = 2;
            var detectorResults = new List<DetectorResult>
            {
                new DetectorResult { DetectorName = "Detector1", IsIdle = true, Confidence = 0.8 },
                new DetectorResult { DetectorName = "Detector2", IsIdle = true, Confidence = 0.9 },
                new DetectorResult { DetectorName = "Detector3", IsIdle = false, Confidence = 0.7 }
            };
            var consensusMethod = ConsensusMethod.Majority;

            // Act
            var args = new ConsensusDetectionEventArgs(
                sessionId, processId, decision, confidence,
                detectorCount, agreementCount, detectorResults, consensusMethod);

            // Assert
            Assert.Equal(sessionId, args.SessionId);
            Assert.Equal(processId, args.ProcessId);
            Assert.Equal(decision, args.Decision);
            Assert.Equal(confidence, args.Confidence);
            Assert.Equal(detectorCount, args.DetectorCount);
            Assert.Equal(agreementCount, args.AgreementCount);
            Assert.Equal(detectorResults, args.DetectorResults);
            Assert.Equal(consensusMethod, args.ConsensusMethod);
            Assert.NotEqual(default, args.Timestamp);
        }

        [Fact]
        public void ConsensusDetectionEventArgs_Constructor_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ConsensusDetectionEventArgs(
                null, 1234, ConsensusDecision.Idle, 0.5, 3, 2, new List<DetectorResult>(), ConsensusMethod.Majority));
        }

        [Fact]
        public void ConsensusDetectionEventArgs_Constructor_WithNullDetectorResults_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ConsensusDetectionEventArgs(
                "session", 1234, ConsensusDecision.Idle, 0.5, 3, 2, null, ConsensusMethod.Majority));
        }

        [Fact]
        public void ConsensusDetectionEventArgs_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var args = new ConsensusDetectionEventArgs(
                "session-123", 1234, ConsensusDecision.Idle, 0.85,
                3, 2, new List<DetectorResult>(), ConsensusMethod.Majority);

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("session-123", result);
            Assert.Contains("1234", result);
            Assert.Contains("Idle", result);
            Assert.Contains("0.85", result);
            Assert.Contains("2/3", result);
            Assert.Contains("Majority", result);
        }

        #endregion

        #region DetectorResult Tests

        [Fact]
        public void DetectorResult_Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var result = new DetectorResult();

            // Assert
            Assert.NotNull(result.Metadata);
            Assert.Empty(result.Metadata);
            Assert.Equal(0.0, result.Confidence);
            Assert.Equal(TimeSpan.Zero, result.IdleTime);
            Assert.Null(result.DetectorName);
            Assert.Null(result.DetectionMethod);
        }

        [Fact]
        public void DetectorResult_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var result = new DetectorResult
            {
                DetectorName = "TestDetector",
                IsIdle = true,
                Confidence = 0.85,
                IdleTime = TimeSpan.FromMinutes(10.5),
                DetectionMethod = "SystemCheck"
            };

            // Act
            var resultString = result.ToString();

            // Assert
            Assert.Contains("TestDetector", resultString);
            Assert.Contains("True", resultString);
            Assert.Contains("0.85", resultString);
            Assert.Contains("10.5", resultString);
            Assert.Contains("SystemCheck", resultString);
        }

        #endregion

        #region Enum Value Tests

        [Fact]
        public void SessionState_ShouldHaveExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)SessionState.Active);
            Assert.Equal(1, (int)SessionState.Idle);
            Assert.Equal(2, (int)SessionState.LongIdle);
            Assert.Equal(3, (int)SessionState.Inactive);
            Assert.Equal(4, (int)SessionState.Unknown);
        }

        [Fact]
        public void ConsensusDecision_ShouldHaveExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)ConsensusDecision.Active);
            Assert.Equal(1, (int)ConsensusDecision.Idle);
            Assert.Equal(2, (int)ConsensusDecision.Inconclusive);
            Assert.Equal(3, (int)ConsensusDecision.Release);
        }

        [Fact]
        public void ConsensusMethod_ShouldHaveExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)ConsensusMethod.Majority);
            Assert.Equal(1, (int)ConsensusMethod.Weighted);
            Assert.Equal(2, (int)ConsensusMethod.Unanimous);
            Assert.Equal(3, (int)ConsensusMethod.Threshold);
            Assert.Equal(4, (int)ConsensusMethod.Hierarchical);
        }

        #endregion
    }
}