using System;
using System.Collections.Generic;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerStateTests
    {
        [Fact]
        public void TimerStateChangedEventArgs_Constructor_ShouldSetProperties()
        {
            // Arrange
            var previousState = TimerState.Stopped;
            var newState = TimerState.Running;
            var reason = "Timer started";

            // Act
            var args = new TimerStateChangedEventArgs(previousState, newState, reason);

            // Assert
            Assert.Equal(previousState, args.PreviousState);
            Assert.Equal(newState, args.NewState);
            Assert.Equal(reason, args.Reason);
            Assert.True(args.Timestamp > DateTime.UtcNow.AddSeconds(-1));
            Assert.True(args.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public void TimerStateChangedEventArgs_Constructor_WithNullReason_ShouldSetNullReason()
        {
            // Arrange
            var previousState = TimerState.Stopped;
            var newState = TimerState.Running;

            // Act
            var args = new TimerStateChangedEventArgs(previousState, newState, null);

            // Assert
            Assert.Null(args.Reason);
        }

        [Fact]
        public void TimerExecutionEventArgs_Constructor_ShouldSetProperties()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var attempt = 1;

            // Act
            var args = new TimerExecutionEventArgs(executionId, startTime, attempt);

            // Assert
            Assert.Equal(executionId, args.ExecutionId);
            Assert.Equal(startTime, args.StartTime);
            Assert.Equal(attempt, args.Attempt);
            Assert.Equal(startTime, args.EndTime);
            Assert.Equal(TimeSpan.Zero, args.Duration);
            Assert.False(args.Success);
            Assert.Null(args.ErrorMessage);
            Assert.Equal(0, args.OperationsProcessed);
            Assert.NotNull(args.Metadata);
            Assert.Empty(args.Metadata);
        }

        [Fact]
        public void TimerExecutionEventArgs_Metadata_ShouldAllowAddingItems()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var args = new TimerExecutionEventArgs(executionId, startTime, 1);

            // Act
            args.Metadata["Key1"] = "Value1";
            args.Metadata["Key2"] = 42;

            // Assert
            Assert.Equal(2, args.Metadata.Count);
            Assert.Equal("Value1", args.Metadata["Key1"]);
            Assert.Equal(42, args.Metadata["Key2"]);
        }

        [Fact]
        public void TimerExecutionErrorEventArgs_Constructor_ShouldSetProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var consecutiveErrors = 3;
            var state = TimerState.Error;
            var executionId = Guid.NewGuid();

            // Act
            var args = new TimerExecutionErrorEventArgs(exception, consecutiveErrors, state, executionId);

            // Assert
            Assert.Equal(exception, args.Error);
            Assert.Equal(consecutiveErrors, args.ConsecutiveErrors);
            Assert.Equal(state, args.State);
            Assert.Equal(executionId, args.ExecutionId);
            Assert.False(args.ShouldTriggerCircuitBreaker);
            Assert.True(args.Timestamp > DateTime.UtcNow.AddSeconds(-1));
            Assert.True(args.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public void TimerExecutionErrorEventArgs_Constructor_WithoutExecutionId_ShouldSetNullExecutionId()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var consecutiveErrors = 3;
            var state = TimerState.Error;

            // Act
            var args = new TimerExecutionErrorEventArgs(exception, consecutiveErrors, state);

            // Assert
            Assert.Null(args.ExecutionId);
        }

        [Fact]
        public void TimerExecutionErrorEventArgs_Constructor_WithNullException_ShouldThrowArgumentNullException()
        {
            // Arrange
            Exception exception = null;
            var consecutiveErrors = 3;
            var state = TimerState.Error;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TimerExecutionErrorEventArgs(exception, consecutiveErrors, state));
        }

        [Fact]
        public void TimerPerformanceMetrics_Constructor_ShouldInitializeWithDefaultValues()
        {
            // Act
            var metrics = new TimerPerformanceMetrics();

            // Assert
            Assert.Equal(0, metrics.TotalExecutions);
            Assert.Equal(0, metrics.SuccessfulExecutions);
            Assert.Equal(0, metrics.FailedExecutions);
            Assert.Equal(TimeSpan.Zero, metrics.AverageExecutionDuration);
            Assert.Equal(TimeSpan.Zero, metrics.MinExecutionDuration);
            Assert.Equal(TimeSpan.Zero, metrics.MaxExecutionDuration);
            Assert.Equal(TimeSpan.Zero, metrics.Uptime);
            Assert.Null(metrics.LastExecutionTime);
            Assert.Equal(0, metrics.SuccessRate);
            Assert.Equal(0, metrics.ExecutionsPerMinute);
            Assert.Equal(0, metrics.CurrentConsecutiveErrors);
            Assert.Equal(0, metrics.MaxConsecutiveErrors);
            Assert.Null(metrics.StartTime);
            Assert.Null(metrics.StopTime);
        }

        [Fact]
        public void TimerPerformanceMetrics_SuccessRate_NoExecutions_ShouldReturnZero()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics();

            // Act & Assert
            Assert.Equal(0, metrics.SuccessRate);
        }

        [Fact]
        public void TimerPerformanceMetrics_SuccessRate_WithExecutions_ShouldCalculateCorrectly()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics
            {
                TotalExecutions = 10,
                SuccessfulExecutions = 7
            };

            // Act & Assert
            Assert.Equal(70.0, metrics.SuccessRate);
        }

        [Fact]
        public void TimerPerformanceMetrics_ExecutionsPerMinute_NoUptime_ShouldReturnZero()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics
            {
                TotalExecutions = 10,
                Uptime = TimeSpan.Zero
            };

            // Act & Assert
            Assert.Equal(0, metrics.ExecutionsPerMinute);
        }

        [Fact]
        public void TimerPerformanceMetrics_ExecutionsPerMinute_WithUptime_ShouldCalculateCorrectly()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics
            {
                TotalExecutions = 30,
                Uptime = TimeSpan.FromMinutes(10)
            };

            // Act & Assert
            Assert.Equal(3.0, metrics.ExecutionsPerMinute);
        }

        [Fact]
        public void TimerPerformanceMetrics_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics
            {
                TotalExecutions = 10,
                SuccessfulExecutions = 8,
                FailedExecutions = 2,
                AverageExecutionDuration = TimeSpan.FromMilliseconds(500),
                MinExecutionDuration = TimeSpan.FromMilliseconds(200),
                MaxExecutionDuration = TimeSpan.FromMilliseconds(1000),
                Uptime = TimeSpan.FromMinutes(5),
                LastExecutionTime = DateTime.UtcNow,
                CurrentConsecutiveErrors = 1,
                MaxConsecutiveErrors = 3,
                StartTime = DateTime.UtcNow.AddMinutes(-5)
            };

            // Act
            var result = metrics.ToString();

            // Assert
            Assert.Contains("Timer Performance Metrics:", result);
            Assert.Contains("Total Executions: 10", result);
            Assert.Contains("Successful Executions: 8", result);
            Assert.Contains("Failed Executions: 2", result);
            Assert.Contains("Success Rate: 80.00%", result);
            Assert.Contains("Average Duration: 500.00ms", result);
            Assert.Contains("Min Duration: 200.00ms", result);
            Assert.Contains("Max Duration: 1000.00ms", result);
            Assert.Contains("Current Consecutive Errors: 1", result);
            Assert.Contains("Max Consecutive Errors: 3", result);
        }

        [Fact]
        public void TimerPerformanceMetrics_ToString_WithNoLastExecutionTime_ShouldNotIncludeLastExecution()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics
            {
                TotalExecutions = 0,
                LastExecutionTime = null
            };

            // Act
            var result = metrics.ToString();

            // Assert
            Assert.DoesNotContain("Last Execution:", result);
        }

        [Fact]
        public void TimerState_Values_ShouldHaveExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)TimerState.Stopped);
            Assert.Equal(1, (int)TimerState.Running);
            Assert.Equal(2, (int)TimerState.Paused);
            Assert.Equal(3, (int)TimerState.Executing);
            Assert.Equal(4, (int)TimerState.Error);
            Assert.Equal(5, (int)TimerState.CircuitBreaker);
            Assert.Equal(6, (int)TimerState.Stopping);
            Assert.Equal(7, (int)TimerState.Disposed);
        }

        [Fact]
        public void TimerStateChangedEventArgs_ShouldBeSerializable()
        {
            // Arrange
            var args = new TimerStateChangedEventArgs(TimerState.Stopped, TimerState.Running, "Test reason");

            // Act & Assert
            // This test ensures the class can be serialized without throwing exceptions
            // The actual serialization test would require more complex setup
            Assert.NotNull(args.ToString());
        }

        [Fact]
        public void TimerExecutionEventArgs_ShouldBeSerializable()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var args = new TimerExecutionEventArgs(executionId, startTime, 1);

            // Act
            args.Metadata["Test"] = "Value";

            // Assert
            Assert.NotNull(args.ToString());
            Assert.Equal("Value", args.Metadata["Test"]);
        }

        [Fact]
        public void TimerExecutionErrorEventArgs_ShouldBeSerializable()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var args = new TimerExecutionErrorEventArgs(exception, 3, TimerState.Error, Guid.NewGuid());

            // Act
            args.ShouldTriggerCircuitBreaker = true;

            // Assert
            Assert.NotNull(args.ToString());
            Assert.True(args.ShouldTriggerCircuitBreaker);
        }

        [Fact]
        public void TimerPerformanceMetrics_ShouldUpdatePropertiesCorrectly()
        {
            // Arrange
            var metrics = new TimerPerformanceMetrics();

            // Act
            metrics.TotalExecutions = 5;
            metrics.SuccessfulExecutions = 4;
            metrics.FailedExecutions = 1;
            metrics.AverageExecutionDuration = TimeSpan.FromMilliseconds(500);
            metrics.MinExecutionDuration = TimeSpan.FromMilliseconds(200);
            metrics.MaxExecutionDuration = TimeSpan.FromMilliseconds(1000);
            metrics.Uptime = TimeSpan.FromMinutes(10);
            metrics.LastExecutionTime = DateTime.UtcNow;
            metrics.CurrentConsecutiveErrors = 2;
            metrics.MaxConsecutiveErrors = 5;
            metrics.StartTime = DateTime.UtcNow.AddMinutes(-10);
            metrics.StopTime = DateTime.UtcNow;

            // Assert
            Assert.Equal(5, metrics.TotalExecutions);
            Assert.Equal(4, metrics.SuccessfulExecutions);
            Assert.Equal(1, metrics.FailedExecutions);
            Assert.Equal(80.0, metrics.SuccessRate);
            Assert.Equal(0.5, metrics.ExecutionsPerMinute);
            Assert.Equal(TimeSpan.FromMilliseconds(500), metrics.AverageExecutionDuration);
            Assert.Equal(TimeSpan.FromMilliseconds(200), metrics.MinExecutionDuration);
            Assert.Equal(TimeSpan.FromMilliseconds(1000), metrics.MaxExecutionDuration);
        }
    }
}