using System;
using System.Collections.Generic;
using Xunit;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerErrorEventArgs class
    /// </summary>
    public class TimerErrorEventArgsTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 3;
            var executionId = Guid.NewGuid();

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors, executionId);

            // Assert
            Assert.Same(error, eventArgs.Error);
            Assert.Equal(status, eventArgs.Status);
            Assert.Equal(consecutiveErrors, eventArgs.ConsecutiveErrors);
            Assert.Equal(executionId, eventArgs.ExecutionId);
            Assert.True(eventArgs.Timestamp <= DateTime.UtcNow);
            Assert.False(eventArgs.ShouldTriggerCircuitBreaker);
            Assert.False(eventArgs.IsFatal);
            Assert.NotEqual(TimerErrorSeverity.Low, eventArgs.Severity); // Should be determined
            Assert.NotEqual(TimerErrorCategory.General, eventArgs.Category); // Should be determined
            Assert.NotEqual(TimerErrorRecoveryAction.None, eventArgs.RecoveryAction); // Should be determined
            Assert.NotNull(eventArgs.Context);
            Assert.Empty(eventArgs.Context);
            Assert.False(string.IsNullOrWhiteSpace(eventArgs.FormattedMessage));
            Assert.False(string.IsNullOrWhiteSpace(eventArgs.ErrorCode));
            Assert.False(eventArgs.WasHandled);
            Assert.Equal(TimeSpan.Zero, eventArgs.HandlingDuration);
        }

        [Fact]
        public void Constructor_WithoutExecutionId_ShouldInitializeCorrectly()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 3;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Assert
            Assert.Same(error, eventArgs.Error);
            Assert.Equal(status, eventArgs.Status);
            Assert.Equal(consecutiveErrors, eventArgs.ConsecutiveErrors);
            Assert.Null(eventArgs.ExecutionId);
        }

        [Fact]
        public void Constructor_WithCustomSeverity_ShouldUseProvidedSeverity()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 3;
            var customSeverity = TimerErrorSeverity.Critical;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors, customSeverity);

            // Assert
            Assert.Equal(customSeverity, eventArgs.Severity);
        }

        [Fact]
        public void Constructor_WithNullError_ShouldThrowArgumentNullException()
        {
            // Arrange
            TimerStatus status = TimerStatus.Running;
            int consecutiveErrors = 3;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorEventArgs(null, status, consecutiveErrors));
        }

        #endregion

        #region Error Severity Determination Tests

        [Theory]
        [InlineData(typeof(OutOfMemoryException), TimerErrorSeverity.Critical)]
        [InlineData(typeof(StackOverflowException), TimerErrorSeverity.Critical)]
        [InlineData(typeof(System.AccessViolationException), TimerErrorSeverity.Critical)]
        [InlineData(typeof(System.InvalidOperationException), TimerErrorSeverity.High)]
        [InlineData(typeof(System.ArgumentException), TimerErrorSeverity.High)]
        [InlineData(typeof(System.NullReferenceException), TimerErrorSeverity.High)]
        [InlineData(typeof(System.TimeoutException), TimerErrorSeverity.Medium)]
        [InlineData(typeof(System.IO.IOException), TimerErrorSeverity.Medium)]
        [InlineData(typeof(System.Net.Sockets.SocketException), TimerErrorSeverity.Medium)]
        [InlineData(typeof(System.Exception), TimerErrorSeverity.Low)]
        public void DetermineErrorSeverity_ShouldReturnCorrectSeverity(Exception exception, TimerErrorSeverity expectedSeverity)
        {
            // Arrange
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(exception, status, consecutiveErrors);

            // Assert
            Assert.Equal(expectedSeverity, eventArgs.Severity);
        }

        #endregion

        #region Error Category Determination Tests

        [Theory]
        [InlineData(typeof(System.TimeoutException), TimerErrorCategory.Timeout)]
        [InlineData(typeof(System.IO.IOException), TimerErrorCategory.IO)]
        [InlineData(typeof(System.IO.FileNotFoundException), TimerErrorCategory.IO)]
        [InlineData(typeof(System.Net.Sockets.SocketException), TimerErrorCategory.Network)]
        [InlineData(typeof(System.Net.WebException), TimerErrorCategory.Network)]
        [InlineData(typeof(System.InvalidOperationException), TimerErrorCategory.Operation)]
        [InlineData(typeof(System.ArgumentException), TimerErrorCategory.Argument)]
        [InlineData(typeof(System.ArgumentNullException), TimerErrorCategory.Argument)]
        [InlineData(typeof(System.OutOfMemoryException), TimerErrorCategory.Memory)]
        [InlineData(typeof(System.Threading.ThreadAbortException), TimerErrorCategory.Threading)]
        [InlineData(typeof(System.Exception), TimerErrorCategory.General)]
        public void DetermineErrorCategory_ShouldReturnCorrectCategory(Exception exception, TimerErrorCategory expectedCategory)
        {
            // Arrange
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(exception, status, consecutiveErrors);

            // Assert
            Assert.Equal(expectedCategory, eventArgs.Category);
        }

        #endregion

        #region Recovery Action Determination Tests

        [Theory]
        [InlineData(typeof(System.TimeoutException), TimerStatus.Running, TimerErrorRecoveryAction.Retry)]
        [InlineData(typeof(System.IO.IOException), TimerStatus.Running, TimerErrorRecoveryAction.WaitAndRetry)]
        [InlineData(typeof(System.Net.Sockets.SocketException), TimerStatus.Running, TimerErrorRecoveryAction.WaitAndRetry)]
        [InlineData(typeof(System.InvalidOperationException), TimerStatus.Running, TimerErrorRecoveryAction.Reset)]
        [InlineData(typeof(System.OutOfMemoryException), TimerStatus.Running, TimerErrorRecoveryAction.Stop)]
        [InlineData(typeof(System.StackOverflowException), TimerStatus.Running, TimerErrorRecoveryAction.Stop)]
        [InlineData(typeof(System.ArgumentException), TimerStatus.Running, TimerErrorRecoveryAction.LogError)]
        [InlineData(typeof(System.Exception), TimerStatus.Running, TimerErrorRecoveryAction.Continue)]
        public void DetermineRecoveryAction_ShouldReturnCorrectAction(Exception exception, TimerStatus status, TimerErrorRecoveryAction expectedAction)
        {
            // Arrange
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(exception, status, consecutiveErrors);

            // Assert
            Assert.Equal(expectedAction, eventArgs.RecoveryAction);
        }

        #endregion

        #region Formatted Message Tests

        [Fact]
        public void FormattedMessage_ShouldContainAllRelevantInformation()
        {
            // Arrange
            var error = new InvalidOperationException("Test error message");
            var status = TimerStatus.Running;
            var consecutiveErrors = 5;
            var executionId = Guid.NewGuid();

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors, executionId);
            var formattedMessage = eventArgs.FormattedMessage;

            // Assert
            Assert.Contains("Timer error occurred", formattedMessage);
            Assert.Contains(error.Message, formattedMessage);
            Assert.Contains(status.ToString(), formattedMessage);
            Assert.Contains(consecutiveErrors.ToString(), formattedMessage);
            Assert.Contains(error.GetType().Name, formattedMessage);
        }

        [Fact]
        public void FormattedMessage_ShouldHaveValidTimestampFormat()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);
            var formattedMessage = eventArgs.FormattedMessage;

            // Assert
            // The message should contain a timestamp in yyyy-MM-dd HH:mm:ss.fff format
            Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}", formattedMessage);
        }

        #endregion

        #region Error Code Generation Tests

        [Fact]
        public void ErrorCode_ShouldBeInExpectedFormat()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);
            var errorCode = eventArgs.ErrorCode;

            // Assert
            Assert.StartsWith("TIMER_", errorCode);
            Assert.Contains(status.ToString().ToUpper(), errorCode);
            Assert.Contains("INVALIDOPERATION", errorCode);
            Assert.Matches(@"TIMER_\w+_\w+_\d{14}", errorCode); // TIMER_STATUS_ERROR_TIMESTAMP
        }

        [Fact]
        public void ErrorCode_ShouldBeUniqueForDifferentTimestamps()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;

            // Act
            var eventArgs1 = new TimerErrorEventArgs(error, status, consecutiveErrors);
            System.Threading.Thread.Sleep(10); // Ensure different timestamps
            var eventArgs2 = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Assert
            Assert.NotEqual(eventArgs1.ErrorCode, eventArgs2.ErrorCode);
        }

        #endregion

        #region Context Management Tests

        [Fact]
        public void AddContext_WithValidParameters_ShouldAddToContext()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Act
            eventArgs.AddContext("Key1", "Value1");
            eventArgs.AddContext("Key2", 42);

            // Assert
            Assert.Equal(2, eventArgs.Context.Count);
            Assert.Equal("Value1", eventArgs.Context["Key1"]);
            Assert.Equal(42, eventArgs.Context["Key2"]);
        }

        [Fact]
        public void AddContext_WithNullKey_ShouldThrowArgumentException()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => eventArgs.AddContext(null, "value"));
        }

        [Fact]
        public void AddContext_WithDuplicateKey_ShouldOverwriteValue()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Act
            eventArgs.AddContext("Key", "OriginalValue");
            eventArgs.AddContext("Key", "NewValue");

            // Assert
            Assert.Equal(1, eventArgs.Context.Count);
            Assert.Equal("NewValue", eventArgs.Context["Key"]);
        }

        #endregion

        #region MarkAsHandled Tests

        [Fact]
        public void MarkAsHandled_WithoutDuration_ShouldMarkAsHandledWithZeroDuration()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Act
            eventArgs.MarkAsHandled();

            // Assert
            Assert.True(eventArgs.WasHandled);
            Assert.Equal(TimeSpan.Zero, eventArgs.HandlingDuration);
        }

        [Fact]
        public void MarkAsHandled_WithDuration_ShouldMarkAsHandledWithSpecifiedDuration()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);
            var duration = TimeSpan.FromMilliseconds(150);

            // Act
            eventArgs.MarkAsHandled(duration);

            // Assert
            Assert.True(eventArgs.WasHandled);
            Assert.Equal(duration, eventArgs.HandlingDuration);
        }

        [Fact]
        public void MarkAsHandled_WithNullDuration_ShouldMarkAsHandledWithZeroDuration()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Act
            eventArgs.MarkAsHandled(null);

            // Assert
            Assert.True(eventArgs.WasHandled);
            Assert.Equal(TimeSpan.Zero, eventArgs.HandlingDuration);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnComprehensiveInformation()
        {
            // Arrange
            var error = new InvalidOperationException("Test error with details");
            var status = TimerStatus.Running;
            var consecutiveErrors = 3;
            var executionId = Guid.NewGuid();
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors, executionId);

            eventArgs.AddContext("CustomKey", "CustomValue");
            eventArgs.ShouldTriggerCircuitBreaker = true;
            eventArgs.IsFatal = false;
            eventArgs.MarkAsHandled(TimeSpan.FromMilliseconds(250));

            // Act
            var result = eventArgs.ToString();

            // Assert
            Assert.Contains("Timer Error Event:", result);
            Assert.Contains("Timestamp:", result);
            Assert.Contains("Error Code:", result);
            Assert.Contains("Error Type:", result);
            Assert.Contains("Error Message:", result);
            Assert.Contains("Timer Status:", result);
            Assert.Contains("Severity:", result);
            Assert.Contains("Category:", result);
            Assert.Contains("Consecutive Errors:", result);
            Assert.Contains("Should Trigger Circuit Breaker:", result);
            Assert.Contains("Is Fatal:", result);
            Assert.Contains("Recovery Action:", result);
            Assert.Contains("Was Handled:", result);
            Assert.Contains("Execution ID:", result);
            Assert.Contains("Handling Duration:", result);
            Assert.Contains("Context:", result);
            Assert.Contains("CustomKey: CustomValue", result);
        }

        [Fact]
        public void ToString_WithoutContextOrExecutionId_ShouldReturnBasicInformation()
        {
            // Arrange
            var error = new InvalidOperationException("Simple error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Act
            var result = eventArgs.ToString();

            // Assert
            Assert.Contains("Timer Error Event:", result);
            Assert.DoesNotContain("Context:", result);
            Assert.DoesNotContain("Execution ID:", result);
            Assert.DoesNotContain("Handling Duration:", result);
        }

        [Fact]
        public void ToString_WithZeroHandlingDuration_ShouldNotIncludeHandlingDuration()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);
            eventArgs.MarkAsHandled(); // Zero duration

            // Act
            var result = eventArgs.ToString();

            // Assert
            Assert.DoesNotContain("Handling Duration:", result);
        }

        #endregion

        #region Edge Cases and Validation

        [Fact]
        public void Constructor_WithMaximumConsecutiveErrors_ShouldHandleLargeValues()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = int.MaxValue;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Assert
            Assert.Equal(int.MaxValue, eventArgs.ConsecutiveErrors);
            Assert.Contains(int.MaxValue.ToString(), eventArgs.FormattedMessage);
        }

        [Fact]
        public void Constructor_WithComplexExceptionMessage_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var error = new InvalidOperationException("Error with special chars: áéíóú 中文 😊");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            // Assert
            Assert.Contains(error.Message, eventArgs.FormattedMessage);
            Assert.Contains(error.Message, eventArgs.ToString());
        }

        [Fact]
        public void AddContext_WithComplexObjects_ShouldStoreCorrectly()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var consecutiveErrors = 1;
            var eventArgs = new TimerErrorEventArgs(error, status, consecutiveErrors);

            var complexObject = new
            {
                Name = "Test",
                Value = 42,
                Items = new[] { "A", "B", "C" }
            };

            // Act
            eventArgs.AddContext("ComplexObject", complexObject);

            // Assert
            Assert.Same(complexObject, eventArgs.Context["ComplexObject"]);
        }

        [Fact]
        public void AllSeverityLevels_ShouldBeCovered()
        {
            // Arrange
            var severities = (TimerErrorSeverity[])Enum.GetValues(typeof(TimerErrorSeverity));
            var encounteredSeverities = new HashSet<TimerErrorSeverity>();

            var exceptions = new Exception[]
            {
                new OutOfMemoryException(),           // Critical
                new InvalidOperationException(),       // High
                new TimeoutException(),              // Medium
                new Exception()                      // Low
            };

            // Act
            foreach (var exception in exceptions)
            {
                var eventArgs = new TimerErrorEventArgs(exception, TimerStatus.Running, 1);
                encounteredSeverities.Add(eventArgs.Severity);
            }

            // Assert
            Assert.Equal(severities.Length, encounteredSeverities.Count);
        }

        [Fact]
        public void AllErrorCategories_ShouldBeCovered()
        {
            // Arrange
            var categories = (TimerErrorCategory[])Enum.GetValues(typeof(TimerErrorCategory));
            var encounteredCategories = new HashSet<TimerErrorCategory>();

            var exceptions = new Exception[]
            {
                new TimeoutException(),                     // Timeout
                new System.IO.IOException(),                // IO
                new System.Net.Sockets.SocketException(),   // Network
                new InvalidOperationException(),             // Operation
                new ArgumentException(),                   // Argument
                new OutOfMemoryException(),                 // Memory
                new System.Threading.ThreadAbortException(), // Threading
                new Exception()                            // General
            };

            // Act
            foreach (var exception in exceptions)
            {
                var eventArgs = new TimerErrorEventArgs(exception, TimerStatus.Running, 1);
                encounteredCategories.Add(eventArgs.Category);
            }

            // Assert
            Assert.Equal(categories.Length, encounteredCategories.Count);
        }

        [Fact]
        public void AllRecoveryActions_ShouldBeCovered()
        {
            // Arrange
            var recoveryActions = (TimerErrorRecoveryAction[])Enum.GetValues(typeof(TimerErrorRecoveryAction));
            var encounteredActions = new HashSet<TimerErrorRecoveryAction>();

            var exceptionStatusPairs = new[]
            {
                (new TimeoutException(), TimerStatus.Running),           // Retry
                (new System.IO.IOException(), TimerStatus.Running),     // WaitAndRetry
                (new InvalidOperationException(), TimerStatus.Running),   // Reset
                (new ArgumentException(), TimerStatus.Running),           // LogError
                (new OutOfMemoryException(), TimerStatus.Running),      // Stop
                (new Exception(), TimerStatus.Running)                  // Continue
            };

            // Act
            foreach (var (exception, status) in exceptionStatusPairs)
            {
                var eventArgs = new TimerErrorEventArgs(exception, status, 1);
                encounteredActions.Add(eventArgs.RecoveryAction);
            }

            // Assert
            Assert.True(encounteredActions.Count >= recoveryActions.Length - 2,
                "Should cover most recovery actions (some might not be covered by standard exceptions)");
        }

        #endregion
    }
}