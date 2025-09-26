using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerExecutionExceptionTests
    {
        [Fact]
        public void Constructor_Default_ShouldSetDefaultValues()
        {
            // Act
            var exception = new TimerExecutionException();

            // Assert
            Assert.Equal("Timer execution failed", exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.ExecutionId);
            Assert.Null(exception.TimerInterval);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void Constructor_WithMessage_ShouldSetMessage()
        {
            // Arrange
            var message = "Custom error message";

            // Act
            var exception = new TimerExecutionException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
        {
            // Arrange
            var message = "Custom error message";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new TimerExecutionException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(innerException, exception.InnerException);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
        }

        [Fact]
        public void Constructor_WithFullParameters_ShouldSetAllProperties()
        {
            // Arrange
            var message = "Custom error message";
            var state = TimerState.CircuitBreaker;
            var executionId = Guid.NewGuid();
            var consecutiveErrors = 5;
            var shouldTriggerCircuitBreaker = true;
            var timerInterval = TimeSpan.FromSeconds(30);
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new TimerExecutionException(
                message, state, executionId, consecutiveErrors, shouldTriggerCircuitBreaker, timerInterval, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(state, exception.State);
            Assert.Equal(executionId, exception.ExecutionId);
            Assert.Equal(consecutiveErrors, exception.ConsecutiveErrors);
            Assert.Equal(shouldTriggerCircuitBreaker, exception.ShouldTriggerCircuitBreaker);
            Assert.Equal(timerInterval, exception.TimerInterval);
            Assert.Equal(innerException, exception.InnerException);
        }

        [Fact]
        public void StartFailure_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var innerException = new InvalidOperationException("Start failed");
            var interval = TimeSpan.FromSeconds(30);

            // Act
            var exception = TimerExecutionException.StartFailure(innerException, interval);

            // Assert
            Assert.Equal("Failed to start timer: Start failed", exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Equal(interval, exception.TimerInterval);
            Assert.Equal(innerException, exception.InnerException);
        }

        [Fact]
        public void StartFailure_WithoutInterval_ShouldCreateExceptionWithNullInterval()
        {
            // Arrange
            var innerException = new InvalidOperationException("Start failed");

            // Act
            var exception = TimerExecutionException.StartFailure(innerException);

            // Assert
            Assert.Equal("Failed to start timer: Start failed", exception.Message);
            Assert.Null(exception.TimerInterval);
        }

        [Fact]
        public void StopFailure_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var innerException = new InvalidOperationException("Stop failed");

            // Act
            var exception = TimerExecutionException.StopFailure(innerException);

            // Assert
            Assert.Equal("Failed to stop timer: Stop failed", exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.TimerInterval);
            Assert.Equal(innerException, exception.InnerException);
        }

        [Fact]
        public void ExecutionFailure_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var innerException = new InvalidOperationException("Execution failed");
            var consecutiveErrors = 3;
            var shouldTriggerCircuitBreaker = true;

            // Act
            var exception = TimerExecutionException.ExecutionFailure(
                executionId, innerException, consecutiveErrors, shouldTriggerCircuitBreaker);

            // Assert
            Assert.Equal("Timer execution failed: Execution failed", exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(executionId, exception.ExecutionId);
            Assert.Equal(consecutiveErrors, exception.ConsecutiveErrors);
            Assert.Equal(shouldTriggerCircuitBreaker, exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.TimerInterval);
            Assert.Equal(innerException, exception.InnerException);
        }

        [Fact]
        public void CircuitBreakerActivated_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var consecutiveErrors = 5;
            var threshold = 5;

            // Act
            var exception = TimerExecutionException.CircuitBreakerActivated(consecutiveErrors, threshold);

            // Assert
            Assert.Equal($"Circuit breaker activated after {consecutiveErrors} consecutive errors (threshold: {threshold})", exception.Message);
            Assert.Equal(TimerState.CircuitBreaker, exception.State);
            Assert.Equal(consecutiveErrors, exception.ConsecutiveErrors);
            Assert.True(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.ExecutionId);
            Assert.Null(exception.TimerInterval);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void InvalidStateOperation_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var operation = "start";
            var currentState = TimerState.Running;
            var requiredState = TimerState.Stopped;

            // Act
            var exception = TimerExecutionException.InvalidStateOperation(operation, currentState, requiredState);

            // Assert
            Assert.Equal($"Cannot {operation} while timer is in {currentState} state. Required state: {requiredState}", exception.Message);
            Assert.Equal(currentState, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.ExecutionId);
            Assert.Null(exception.TimerInterval);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void TimerDisposed_ShouldCreateExceptionWithCorrectProperties()
        {
            // Act
            var exception = TimerExecutionException.TimerDisposed();

            // Assert
            Assert.Equal("Timer has been disposed and cannot be used", exception.Message);
            Assert.Equal(TimerState.Disposed, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.ExecutionId);
            Assert.Null(exception.TimerInterval);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void InvalidInterval_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var interval = TimeSpan.Zero;

            // Act
            var exception = TimerExecutionException.InvalidInterval(interval);

            // Assert
            Assert.Equal($"Invalid timer interval: {interval}. Interval must be greater than zero.", exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.ExecutionId);
            Assert.Null(exception.TimerInterval);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void ExecutionTimeout_ShouldCreateExceptionWithCorrectProperties()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var timeout = TimeSpan.FromSeconds(30);
            var actualDuration = TimeSpan.FromSeconds(45);

            // Act
            var exception = TimerExecutionException.ExecutionTimeout(executionId, timeout, actualDuration);

            // Assert
            Assert.Equal($"Timer execution {executionId} timed out after {timeout.TotalSeconds:F1} seconds (actual duration: {actualDuration.TotalSeconds:F1} seconds)", exception.Message);
            Assert.Equal(TimerState.Error, exception.State);
            Assert.Equal(executionId, exception.ExecutionId);
            Assert.Equal(1, exception.ConsecutiveErrors);
            Assert.False(exception.ShouldTriggerCircuitBreaker);
            Assert.Null(exception.TimerInterval);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void ToString_ShouldReturnFormattedStringWithAllDetails()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var innerException = new InvalidOperationException("Inner error");
            var exception = new TimerExecutionException(
                "Test error", TimerState.CircuitBreaker, executionId, 3, true, TimeSpan.FromSeconds(30), innerException);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains("Test error", result);
            Assert.Contains("Timer Execution Details:", result);
            Assert.Contains($"State: {TimerState.CircuitBreaker}", result);
            Assert.Contains($"Execution ID: {executionId}", result);
            Assert.Contains("Consecutive Errors: 3", result);
            Assert.Contains("Should Trigger Circuit Breaker: True", result);
            Assert.Contains("Timer Interval: 30000.00ms", result);
            Assert.Contains("Inner error", result);
        }

        [Fact]
        public void ToString_WithNullValues_ShouldHandleGracefully()
        {
            // Arrange
            var exception = new TimerExecutionException("Test error");

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains("Test error", result);
            Assert.Contains("Timer Execution Details:", result);
            Assert.Contains($"State: {TimerState.Error}", result);
            Assert.Contains("Execution ID: N/A", result);
        }

        [Fact]
        public void GetUserFriendlyMessage_CircuitBreaker_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var exception = TimerExecutionException.CircuitBreakerActivated(5, 5);

            // Act
            var message = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("circuit breaker", message.ToLower());
            Assert.Contains("temporarily stopped", message.ToLower());
        }

        [Fact]
        public void GetUserFriendlyMessage_Disposed_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var exception = TimerExecutionException.TimerDisposed();

            // Act
            var message = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("disposed", message.ToLower());
            Assert.Contains("no longer available", message.ToLower());
        }

        [Fact]
        public void GetUserFriendlyMessage_CircuitBreakerState_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var exception = new TimerExecutionException("Test error", TimerState.CircuitBreaker, null, 3, false);

            // Act
            var message = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("circuit breaker", message.ToLower());
            Assert.Contains("cooldown period", message.ToLower());
        }

        [Fact]
        public void GetUserFriendlyMessage_Timeout_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var exception = new TimerExecutionException("Timeout error", TimerState.Error, null, 1, false)
            {
                TimedOut = true
            };

            // Act
            var message = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("timed out", message.ToLower());
            Assert.Contains("timeout value", message.ToLower());
        }

        [Fact]
        public void GetUserFriendlyMessage_Cancelled_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var exception = new TimerExecutionException("Cancelled error", TimerState.Error, null, 1, false)
            {
                Cancelled = true
            };

            // Act
            var message = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("cancelled", message.ToLower());
        }

        [Fact]
        public void GetUserFriendlyMessage_NonZeroExitCode_ShouldReturnAppropriateMessage()
        {
            // Arrange
            var exception = new TimerExecutionException("Exit code error", TimerState.Error, null, 1, false)
            {
                ExitCode = 1,
                Error = "Process failed"
            };

            // Act
            var message = exception.GetUserFriendlyMessage();

            Assert.Contains("exit code 1", message.ToLower());
            Assert.Contains("process failed", message.ToLower());
        }

        [Fact]
        public void GetUserFriendlyMessage_Default_ShouldReturnBaseMessage()
        {
            // Arrange
            var exception = new TimerExecutionException("Default error message");

            // Act
            var message = exception.GetUserFriendlyMessage();

            Assert.Equal("Default error message", message);
        }

        [Fact]
        public void Serialization_ShouldPreserveAllProperties()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var interval = TimeSpan.FromSeconds(30);
            var originalException = new TimerExecutionException(
                "Serialization test", TimerState.CircuitBreaker, executionId, 3, true, interval, new InvalidOperationException("Inner"));

            // Act
            var serializedException = SerializeAndDeserializeException(originalException);

            // Assert
            Assert.Equal(originalException.Message, serializedException.Message);
            Assert.Equal(originalException.State, serializedException.State);
            Assert.Equal(originalException.ExecutionId, serializedException.ExecutionId);
            Assert.Equal(originalException.ConsecutiveErrors, serializedException.ConsecutiveErrors);
            Assert.Equal(originalException.ShouldTriggerCircuitBreaker, serializedException.ShouldTriggerCircuitBreaker);
            Assert.Equal(originalException.TimerInterval, serializedException.TimerInterval);
            Assert.Equal(originalException.InnerException?.Message, serializedException.InnerException?.Message);
        }

        [Fact]
        public void Serialization_WithNullExecutionId_ShouldHandleGracefully()
        {
            // Arrange
            var originalException = new TimerExecutionException(
                "Serialization test", TimerState.Error, null, 1, false);

            // Act
            var serializedException = SerializeAndDeserializeException(originalException);

            // Assert
            Assert.Equal(originalException.Message, serializedException.Message);
            Assert.Equal(originalException.State, serializedException.State);
            Assert.Null(serializedException.ExecutionId);
        }

        private TimerExecutionException SerializeAndDeserializeException(TimerExecutionException exception)
        {
            // Note: BinaryFormatter is obsolete in .NET Core/5+, but this demonstrates the serialization concept
            // In a real application, you'd use a different serialization approach
            using (var memoryStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, exception);
                memoryStream.Position = 0;
                return (TimerExecutionException)formatter.Deserialize(memoryStream);
            }
        }
    }
}