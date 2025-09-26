using System;
using System.Collections.Generic;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerErrorEvents components
    /// </summary>
    public class TimerErrorEventsTests
    {
        #region TimerErrorEventArgs Tests

        [Fact]
        public void TimerErrorEventArgs_Constructor_WithParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var executionId = Guid.NewGuid();
            var timerState = TimerState.Running;
            var consecutiveErrors = 3;

            // Act
            var args = new TimerErrorEventArgs(exception, executionId, timerState, consecutiveErrors);

            // Assert
            Assert.NotNull(args.ErrorId);
            Assert.NotEqual(Guid.Empty, args.ErrorId);
            Assert.Equal(exception, args.Exception);
            Assert.Equal(executionId, args.ExecutionId);
            Assert.Equal(timerState, args.TimerState);
            Assert.Equal(consecutiveErrors, args.ConsecutiveErrors);
            Assert.Equal(TimerErrorSeverity.Medium, args.Severity);
            Assert.Equal(TimerErrorCategory.Unknown, args.Category);
            Assert.Equal(TimerRecoveryAction.LogAndContinue, args.RecommendedAction);
            Assert.True(args.Timestamp <= DateTime.UtcNow);
            Assert.NotNull(args.Context);
            Assert.Equal(1, args.RecoveryAttempts);
            Assert.False(args.WasHandled);
            Assert.Null(args.HandlingResult);
            Assert.False(args.RecoverySuccessful);
        }

        [Fact]
        public void TimerErrorEventArgs_DefaultConstructor_InitializesDefaults()
        {
            // Act
            var args = new TimerErrorEventArgs();

            // Assert
            Assert.NotNull(args.ErrorId);
            Assert.NotEqual(Guid.Empty, args.ErrorId);
            Assert.Null(args.Exception);
            Assert.Null(args.ExecutionId);
            Assert.Equal(TimerState.Stopped, args.TimerState);
            Assert.Equal(1, args.ConsecutiveErrors);
            Assert.Equal(TimerErrorSeverity.Medium, args.Severity);
            Assert.Equal(TimerErrorCategory.Unknown, args.Category);
            Assert.Equal(TimerRecoveryAction.LogAndContinue, args.RecommendedAction);
            Assert.True(args.Timestamp <= DateTime.UtcNow);
            Assert.NotNull(args.Context);
            Assert.Equal(0, args.Context.Count);
            Assert.Equal(1, args.RecoveryAttempts);
            Assert.False(args.WasHandled);
            Assert.Null(args.HandlingResult);
            Assert.False(args.RecoverySuccessful);
        }

        [Theory]
        [InlineData(TimerErrorSeverity.Low, TimerErrorCategory.Network, false, false)]
        [InlineData(TimerErrorSeverity.Medium, TimerErrorCategory.Timeout, false, false)]
        [InlineData(TimerErrorSeverity.High, TimerErrorCategory.Resource, true, false)]
        [InlineData(TimerErrorSeverity.Critical, TimerErrorCategory.Configuration, true, true)]
        [InlineData(TimerErrorSeverity.Medium, TimerErrorCategory.LicenseServer, true, false)]
        public void TimerErrorEventArgs_ShouldTriggerCircuitBreaker_ReturnsExpectedValue(
            TimerErrorSeverity severity, TimerErrorCategory category, bool expectedTrigger, bool expectedStop)
        {
            // Arrange
            var args = new TimerErrorEventArgs
            {
                Severity = severity,
                Category = category,
                ConsecutiveErrors = 1
            };

            // Act & Assert
            Assert.Equal(expectedTrigger, args.ShouldTriggerCircuitBreaker);
            Assert.Equal(expectedStop, args.ShouldStopTimer);
        }

        [Theory]
        [InlineData(TimerErrorSeverity.Low, true)]
        [InlineData(TimerErrorSeverity.Medium, true)]
        [InlineData(TimerErrorSeverity.High, false)]
        [InlineData(TimerErrorSeverity.Critical, false)]
        public void TimerErrorEventArgs_ShouldStopTimer_ReturnsExpectedValue(TimerErrorSeverity severity, bool expectedAllowsRetry)
        {
            // Arrange
            var args = new TimerErrorEventArgs
            {
                Severity = severity,
                Category = TimerErrorCategory.Execution,
                ConsecutiveErrors = 2
            };

            // Assert
            Assert.Equal(expectedAllowsRetry, args.AllowsRetry);
        }

        [Theory]
        [InlineData(1, false)]
        [InlineData(4, false)]
        [InlineData(5, true)]
        [InlineData(10, true)]
        public void TimerErrorEventArgs_ShouldTriggerCircuitBreaker_WithConsecutiveErrors_ReturnsExpectedValue(int consecutiveErrors, bool expected)
        {
            // Arrange
            var args = new TimerErrorEventArgs
            {
                Severity = TimerErrorSeverity.Medium,
                Category = TimerErrorCategory.Execution,
                ConsecutiveErrors = consecutiveErrors
            };

            // Assert
            Assert.Equal(expected, args.ShouldTriggerCircuitBreaker);
        }

        [Fact]
        public void TimerErrorEventArgs_AddContext_AddsKeyValuePair()
        {
            // Arrange
            var args = new TimerErrorEventArgs();
            var key = "TestKey";
            var value = "TestValue";

            // Act
            args.AddContext(key, value);

            // Assert
            Assert.Single(args.Context);
            Assert.Equal(value, args.GetContext<string>(key));
        }

        [Fact]
        public void TimerErrorEventArgs_GetContext_WithNonExistentKey_ReturnsDefault()
        {
            // Arrange
            var args = new TimerErrorEventArgs();

            // Act & Assert
            Assert.Null(args.GetContext<string>("NonExistentKey"));
            Assert.Equal(0, args.GetContext<int>("NonExistentKey"));
        }

        [Fact]
        public void TimerErrorEventArgs_ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var exception = new InvalidOperationException("Test message");
            var executionId = Guid.NewGuid();
            var args = new TimerErrorEventArgs(exception, executionId, TimerState.Running, 2)
            {
                Severity = TimerErrorSeverity.High,
                Category = TimerErrorCategory.Network,
                RecommendedAction = TimerRecoveryAction.Retry,
                WasHandled = true
            };

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("TimerErrorEventArgs", result);
            Assert.Contains("High", result);
            Assert.Contains("Network", result);
            Assert.Contains("Retry", result);
            Assert.Contains("True", result);
            Assert.Contains(executionId.ToString(), result);
            Assert.Contains("InvalidOperationException", result);
            Assert.Contains("Test message", result);
        }

        #endregion

        #region TimerRecoveryEventArgs Tests

        [Fact]
        public void TimerRecoveryEventArgs_Constructor_WithParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var errorEvent = new TimerErrorEventArgs();
            var action = TimerRecoveryAction.Retry;

            // Act
            var args = new TimerRecoveryEventArgs(errorEvent, action);

            // Assert
            Assert.NotNull(args.RecoveryId);
            Assert.NotEqual(Guid.Empty, args.RecoveryId);
            Assert.Equal(errorEvent, args.ErrorEvent);
            Assert.Equal(action, args.Action);
            Assert.True(args.StartTime <= DateTime.UtcNow);
            Assert.Equal(DateTime.MinValue, args.EndTime);
            Assert.Equal(TimeSpan.Zero, args.Duration);
            Assert.False(args.Success);
            Assert.Null(args.ResultMessage);
            Assert.Null(args.RecoveryException);
            Assert.NotNull(args.Context);
            Assert.Equal(0, args.Context.Count);
        }

        [Fact]
        public void TimerRecoveryEventArgs_DefaultConstructor_InitializesDefaults()
        {
            // Act
            var args = new TimerRecoveryEventArgs();

            // Assert
            Assert.NotNull(args.RecoveryId);
            Assert.NotEqual(Guid.Empty, args.RecoveryId);
            Assert.Null(args.ErrorEvent);
            Assert.Equal((TimerRecoveryAction)0, args.Action);
            Assert.True(args.StartTime <= DateTime.UtcNow);
            Assert.Equal(DateTime.MinValue, args.EndTime);
            Assert.Equal(TimeSpan.Zero, args.Duration);
            Assert.False(args.Success);
            Assert.Null(args.ResultMessage);
            Assert.Null(args.RecoveryException);
            Assert.NotNull(args.Context);
            Assert.Equal(0, args.Context.Count);
        }

        [Fact]
        public void TimerRecoveryEventArgs_Complete_SetsCompletionProperties()
        {
            // Arrange
            var args = new TimerRecoveryEventArgs();
            var startTime = DateTime.UtcNow.AddSeconds(-1);
            args.StartTime = startTime;

            // Act
            args.Complete(true, "Test completion message");

            // Assert
            Assert.True(args.Success);
            Assert.Equal("Test completion message", args.ResultMessage);
            Assert.True(args.EndTime >= startTime);
            Assert.True(args.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void TimerRecoveryEventArgs_Complete_WithDefaultMessage_SetsDefaultMessage()
        {
            // Arrange
            var args = new TimerRecoveryEventArgs();

            // Act
            args.Complete(true);

            // Assert
            Assert.True(args.Success);
            Assert.Equal("Recovery completed successfully", args.ResultMessage);
        }

        [Fact]
        public void TimerRecoveryEventArgs_Complete_WithFailure_SetsFailureMessage()
        {
            // Arrange
            var args = new TimerRecoveryEventArgs();

            // Act
            args.Complete(false);

            // Assert
            Assert.False(args.Success);
            Assert.Equal("Recovery failed", args.ResultMessage);
        }

        [Fact]
        public void TimerRecoveryEventArgs_AddContext_AddsKeyValuePair()
        {
            // Arrange
            var args = new TimerRecoveryEventArgs();
            var key = "TestKey";
            var value = "TestValue";

            // Act
            args.AddContext(key, value);

            // Assert
            Assert.Single(args.Context);
            Assert.Equal(value, args.Context[key]);
        }

        [Fact]
        public void TimerRecoveryEventArgs_ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var errorEvent = new TimerErrorEventArgs();
            var args = new TimerRecoveryEventArgs(errorEvent, TimerRecoveryAction.Retry);
            args.Complete(true, "Test success message");

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("TimerRecoveryEventArgs", result);
            Assert.Contains("Retry", result);
            Assert.Contains("True", result);
            Assert.Contains("Test success message", result);
        }

        #endregion

        #region TimerHealthEventArgs Tests

        [Fact]
        public void TimerHealthEventArgs_DefaultConstructor_InitializesDefaults()
        {
            // Act
            var args = new TimerHealthEventArgs();

            // Assert
            Assert.NotNull(args.HealthCheckId);
            Assert.NotEqual(Guid.Empty, args.HealthCheckId);
            Assert.True(args.Timestamp <= DateTime.UtcNow);
            Assert.Equal(TimerState.Stopped, args.TimerState);
            Assert.True(args.IsHealthy);
            Assert.Equal(100, args.HealthScore);
            Assert.NotNull(args.Issues);
            Assert.Empty(args.Issues);
            Assert.NotNull(args.Metrics);
            Assert.Empty(args.Metrics);
            Assert.NotNull(args.Recommendations);
            Assert.Empty(args.Recommendations);
        }

        [Theory]
        [InlineData(TimerErrorSeverity.Low, 95)]
        [InlineData(TimerErrorSeverity.Medium, 85)]
        [InlineData(TimerErrorSeverity.High, 70)]
        [InlineData(TimerErrorSeverity.Critical, 50)]
        public void TimerHealthEventArgs_AddIssue_ReducesHealthScore(TimerErrorSeverity severity, int expectedScore)
        {
            // Arrange
            var args = new TimerHealthEventArgs();

            // Act
            args.AddIssue("Test issue", severity);

            // Assert
            Assert.False(args.IsHealthy);
            Assert.Equal(expectedScore, args.HealthScore);
            Assert.Single(args.Issues);
            Assert.Contains("Test issue", args.Issues);
        }

        [Fact]
        public void TimerHealthEventArgs_AddIssue_MultipleIssues_AccumulatesScoreReduction()
        {
            // Arrange
            var args = new TimerHealthEventArgs();

            // Act
            args.AddIssue("Issue 1", TimerErrorSeverity.Low);   // -5
            args.AddIssue("Issue 2", TimerErrorSeverity.Medium); // -15
            args.AddIssue("Issue 3", TimerErrorSeverity.High);   // -30

            // Assert
            Assert.False(args.IsHealthy);
            Assert.Equal(50, args.HealthScore);
            Assert.Equal(3, args.Issues.Count);
        }

        [Fact]
        public void TimerHealthEventArgs_AddMetric_AddsKeyValuePair()
        {
            // Arrange
            var args = new TimerHealthEventArgs();
            var key = "TestMetric";
            var value = 42;

            // Act
            args.AddMetric(key, value);

            // Assert
            Assert.Single(args.Metrics);
            Assert.Equal(value, args.Metrics[key]);
        }

        [Fact]
        public void TimerHealthEventArgs_AddRecommendation_AddsToCollection()
        {
            // Arrange
            var args = new TimerHealthEventArgs();
            var recommendation = "Test recommendation";

            // Act
            args.AddRecommendation(recommendation);

            // Assert
            Assert.Single(args.Recommendations);
            Assert.Contains(recommendation, args.Recommendations);
        }

        [Fact]
        public void TimerHealthEventArgs_ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var args = new TimerHealthEventArgs
            {
                TimerState = TimerState.Running,
                HealthScore = 75
            };
            args.AddIssue("Test issue");

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("TimerHealthEventArgs", result);
            Assert.Contains("75", result);
            Assert.Contains("Running", result);
            Assert.Contains("False", result);
            Assert.Contains("1", result);
        }

        #endregion

        #region TimerCircuitBreakerEventArgs Tests

        [Fact]
        public void TimerCircuitBreakerEventArgs_Constructor_WithParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var oldState = CircuitBreakerState.Closed;
            var newState = CircuitBreakerState.Open;
            var reason = "Test reason";
            var failureCount = 5;
            var failureThreshold = 3;

            // Act
            var args = new TimerCircuitBreakerEventArgs(oldState, newState, reason, failureCount, failureThreshold);

            // Assert
            Assert.NotNull(args.EventId);
            Assert.NotEqual(Guid.Empty, args.EventId);
            Assert.Equal(oldState, args.OldState);
            Assert.Equal(newState, args.NewState);
            Assert.Equal(reason, args.Reason);
            Assert.Equal(failureCount, args.FailureCount);
            Assert.Equal(failureThreshold, args.FailureThreshold);
            Assert.True(args.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public void TimerCircuitBreakerEventArgs_DefaultConstructor_InitializesDefaults()
        {
            // Act
            var args = new TimerCircuitBreakerEventArgs();

            // Assert
            Assert.NotNull(args.EventId);
            Assert.NotEqual(Guid.Empty, args.EventId);
            Assert.True(args.Timestamp <= DateTime.UtcNow);
            Assert.Equal((CircuitBreakerState)0, args.OldState);
            Assert.Equal((CircuitBreakerState)0, args.NewState);
            Assert.Null(args.Reason);
            Assert.Equal(0, args.FailureCount);
            Assert.Equal(0, args.FailureThreshold);
            Assert.Equal(TimeSpan.Zero, args.CooldownPeriod);
            Assert.Null(args.EstimatedResetTime);
        }

        [Fact]
        public void TimerCircuitBreakerEventArgs_ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var oldState = CircuitBreakerState.Closed;
            var newState = CircuitBreakerState.Open;
            var resetTime = DateTime.UtcNow.AddMinutes(5);
            var args = new TimerCircuitBreakerEventArgs(oldState, newState, "Test trip", 5, 3)
            {
                CooldownPeriod = TimeSpan.FromMinutes(5),
                EstimatedResetTime = resetTime
            };

            // Act
            var result = args.ToString();

            // Assert
            Assert.Contains("TimerCircuitBreakerEventArgs", result);
            Assert.Contains("Closed", result);
            Assert.Contains("Open", result);
            Assert.Contains("5/3", result);
            Assert.Contains("Test trip", result);
            Assert.Contains(resetTime.ToString("yyyy-MM-dd HH:mm:ss"), result);
        }

        #endregion

        #region Enum Tests

        [Fact]
        public void TimerErrorSeverity_HasExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(4, Enum.GetValues(typeof(TimerErrorSeverity)).Length);
            Assert.True(Enum.IsDefined(typeof(TimerErrorSeverity), TimerErrorSeverity.Low));
            Assert.True(Enum.IsDefined(typeof(TimerErrorSeverity), TimerErrorSeverity.Medium));
            Assert.True(Enum.IsDefined(typeof(TimerErrorSeverity), TimerErrorSeverity.High));
            Assert.True(Enum.IsDefined(typeof(TimerErrorSeverity), TimerErrorSeverity.Critical));
        }

        [Fact]
        public void TimerErrorCategory_HasExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(9, Enum.GetValues(typeof(TimerErrorCategory)).Length);
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Configuration));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Execution));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Network));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Process));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Resource));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.LicenseServer));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Timeout));
            Assert.True(Enum.IsDefined(typeof(TimerErrorCategory), TimerErrorCategory.Unknown));
        }

        [Fact]
        public void TimerRecoveryAction_HasExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(11, Enum.GetValues(typeof(TimerRecoveryAction)).Length);
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.None));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.Retry));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.ResetTimer));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.RestartService));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.IncreaseInterval));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.DecreaseInterval));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.EnableCircuitBreaker));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.DisableTimer));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.LogAndContinue));
            Assert.True(Enum.IsDefined(typeof(TimerRecoveryAction), TimerRecoveryAction.Custom));
        }

        [Fact]
        public void CircuitBreakerState_HasExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(3, Enum.GetValues(typeof(CircuitBreakerState)).Length);
            Assert.True(Enum.IsDefined(typeof(CircuitBreakerState), CircuitBreakerState.Closed));
            Assert.True(Enum.IsDefined(typeof(CircuitBreakerState), CircuitBreakerState.Open));
            Assert.True(Enum.IsDefined(typeof(CircuitBreakerState), CircuitBreakerState.HalfOpen));
        }

        #endregion
    }
}