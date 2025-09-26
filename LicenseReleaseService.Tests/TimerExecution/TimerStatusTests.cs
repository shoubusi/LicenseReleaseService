using System;
using Xunit;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerStatus enum and extension methods
    /// </summary>
    public class TimerStatusTests
    {
        #region IsRunning Tests

        [Theory]
        [InlineData(TimerStatus.Running, true)]
        [InlineData(TimerStatus.Executing, true)]
        [InlineData(TimerStatus.Waiting, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Paused, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Error, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Disposed, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        public void IsRunning_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.IsRunning();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsTransitional Tests

        [Theory]
        [InlineData(TimerStatus.Starting, true)]
        [InlineData(TimerStatus.Stopping, true)]
        [InlineData(TimerStatus.Restarting, true)]
        [InlineData(TimerStatus.Waiting, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Running, false)]
        [InlineData(TimerStatus.Paused, false)]
        [InlineData(TimerStatus.Executing, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Error, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Disposed, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        public void IsTransitional_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.IsTransitional();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsError Tests

        [Theory]
        [InlineData(TimerStatus.Error, true)]
        [InlineData(TimerStatus.CircuitBreaker, true)]
        [InlineData(TimerStatus.Cancelled, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Running, false)]
        [InlineData(TimerStatus.Executing, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Paused, false)]
        [InlineData(TimerStatus.Waiting, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Disposed, false)]
        public void IsError_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.IsError();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsTerminal Tests

        [Theory]
        [InlineData(TimerStatus.Stopped, true)]
        [InlineData(TimerStatus.Disposed, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Running, false)]
        [InlineData(TimerStatus.Executing, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Paused, false)]
        [InlineData(TimerStatus.Waiting, false)]
        [InlineData(TimerStatus.Error, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        public void IsTerminal_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.IsTerminal();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region CanStart Tests

        [Theory]
        [InlineData(TimerStatus.Created, true)]
        [InlineData(TimerStatus.Stopped, true)]
        [InlineData(TimerStatus.Error, true)]
        [InlineData(TimerStatus.Cancelled, true)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Running, false)]
        [InlineData(TimerStatus.Executing, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Paused, false)]
        [InlineData(TimerStatus.Waiting, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Disposed, false)]
        public void CanStart_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.CanStart();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region CanStop Tests

        [Theory]
        [InlineData(TimerStatus.Running, true)]
        [InlineData(TimerStatus.Executing, true)]
        [InlineData(TimerStatus.Paused, true)]
        [InlineData(TimerStatus.Waiting, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Error, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        [InlineData(TimerStatus.Disposed, false)]
        public void CanStop_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.CanStop();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region CanPause Tests

        [Theory]
        [InlineData(TimerStatus.Running, true)]
        [InlineData(TimerStatus.Executing, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Paused, false)]
        [InlineData(TimerStatus.Waiting, false)]
        [InlineData(TimerStatus.Error, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        [InlineData(TimerStatus.Disposed, false)]
        public void CanPause_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.CanPause();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region CanResume Tests

        [Theory]
        [InlineData(TimerStatus.Paused, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Running, false)]
        [InlineData(TimerStatus.Executing, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Waiting, false)]
        [InlineData(TimerStatus.Error, false)]
        [InlineData(TimerStatus.CircuitBreaker, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        [InlineData(TimerStatus.Disposed, false)]
        public void CanResume_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.CanResume();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region CanRestart Tests

        [Theory]
        [InlineData(TimerStatus.Running, true)]
        [InlineData(TimerStatus.Executing, true)]
        [InlineData(TimerStatus.Paused, true)]
        [InlineData(TimerStatus.Error, true)]
        [InlineData(TimerStatus.CircuitBreaker, true)]
        [InlineData(TimerStatus.Created, false)]
        [InlineData(TimerStatus.Starting, false)]
        [InlineData(TimerStatus.Stopping, false)]
        [InlineData(TimerStatus.Stopped, false)]
        [InlineData(TimerStatus.Waiting, false)]
        [InlineData(TimerStatus.Restarting, false)]
        [InlineData(TimerStatus.Cancelled, false)]
        [InlineData(TimerStatus.Disposed, false)]
        public void CanRestart_ShouldReturnCorrectValue(TimerStatus status, bool expected)
        {
            // Act
            var result = status.CanRestart();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetDescription Tests

        [Theory]
        [InlineData(TimerStatus.Created, "Timer created but not started")]
        [InlineData(TimerStatus.Starting, "Timer is starting up")]
        [InlineData(TimerStatus.Running, "Timer is running and executing periodically")]
        [InlineData(TimerStatus.Paused, "Timer is paused and not executing")]
        [InlineData(TimerStatus.Executing, "Timer is currently executing a callback")]
        [InlineData(TimerStatus.Stopping, "Timer is stopping and cleaning up resources")]
        [InlineData(TimerStatus.Stopped, "Timer is stopped and not running")]
        [InlineData(TimerStatus.Error, "Timer is in error state due to failures")]
        [InlineData(TimerStatus.CircuitBreaker, "Timer is in circuit breaker state due to too many consecutive errors")]
        [InlineData(TimerStatus.Disposed, "Timer is disposed and cannot be used")]
        [InlineData(TimerStatus.Restarting, "Timer is restarting")]
        [InlineData(TimerStatus.Waiting, "Timer is waiting for execution to complete")]
        [InlineData(TimerStatus.Cancelled, "Timer is cancelled and stopping execution")]
        public void GetDescription_ShouldReturnCorrectDescription(TimerStatus status, string expected)
        {
            // Act
            var result = status.GetDescription();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetColorCode Tests

        [Theory]
        [InlineData(TimerStatus.Created, "Gray")]
        [InlineData(TimerStatus.Starting, "Blue")]
        [InlineData(TimerStatus.Running, "Green")]
        [InlineData(TimerStatus.Paused, "Yellow")]
        [InlineData(TimerStatus.Executing, "DarkGreen")]
        [InlineData(TimerStatus.Stopping, "Orange")]
        [InlineData(TimerStatus.Stopped, "Gray")]
        [InlineData(TimerStatus.Error, "Red")]
        [InlineData(TimerStatus.CircuitBreaker, "DarkRed")]
        [InlineData(TimerStatus.Disposed, "DarkGray")]
        [InlineData(TimerStatus.Restarting, "Blue")]
        [InlineData(TimerStatus.Waiting, "Cyan")]
        [InlineData(TimerStatus.Cancelled, "Red")]
        public void GetColorCode_ShouldReturnCorrectColorCode(TimerStatus status, string expected)
        {
            // Act
            var result = status.GetColorCode();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetPriority Tests

        [Theory]
        [InlineData(TimerStatus.Disposed, 0)]
        [InlineData(TimerStatus.Created, 1)]
        [InlineData(TimerStatus.Stopped, 2)]
        [InlineData(TimerStatus.Paused, 3)]
        [InlineData(TimerStatus.Running, 4)]
        [InlineData(TimerStatus.Starting, 5)]
        [InlineData(TimerStatus.Executing, 6)]
        [InlineData(TimerStatus.Stopping, 7)]
        [InlineData(TimerStatus.Restarting, 8)]
        [InlineData(TimerStatus.Waiting, 9)]
        [InlineData(TimerStatus.Error, 10)]
        [InlineData(TimerStatus.Cancelled, 11)]
        [InlineData(TimerStatus.CircuitBreaker, 12)]
        public void GetPriority_ShouldReturnCorrectPriority(TimerStatus status, int expected)
        {
            // Act
            var result = status.GetPriority();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Edge Cases and Combinations

        [Fact]
        public void AllStatusValues_ShouldHaveValidDescriptions()
        {
            // Arrange
            var allStatusValues = (TimerStatus[])Enum.GetValues(typeof(TimerStatus));

            // Act & Assert
            foreach (var status in allStatusValues)
            {
                var description = status.GetDescription();
                Assert.False(string.IsNullOrWhiteSpace(description), $"Status {status} should have a valid description");
                Assert.Contains("timer", description.ToLower(), $"Description for {status} should mention 'timer'");
            }
        }

        [Fact]
        public void AllStatusValues_ShouldHaveValidColorCodes()
        {
            // Arrange
            var allStatusValues = (TimerStatus[])Enum.GetValues(typeof(TimerStatus));

            // Act & Assert
            foreach (var status in allStatusValues)
            {
                var colorCode = status.GetColorCode();
                Assert.False(string.IsNullOrWhiteSpace(colorCode), $"Status {status} should have a valid color code");
            }
        }

        [Fact]
        public void AllStatusValues_ShouldHaveValidPriorities()
        {
            // Arrange
            var allStatusValues = (TimerStatus[])Enum.GetValues(typeof(TimerStatus));

            // Act & Assert
            foreach (var status in allStatusValues)
            {
                var priority = status.GetPriority();
                Assert.True(priority >= 0, $"Priority for {status} should be non-negative");
            }
        }

        [Fact]
        public void StatusTransitions_ShouldHaveValidRules()
        {
            // Test logical consistency of state transition rules

            // If a status can be started, it should not be running
            foreach (TimerStatus status in Enum.GetValues(typeof(TimerStatus)))
            {
                if (status.CanStart())
                {
                    Assert.False(status.IsRunning(), $"{status} should not be running if it can be started");
                }
            }

            // If a status is terminal, it should not be running
            foreach (TimerStatus status in Enum.GetValues(typeof(TimerStatus)))
            {
                if (status.IsTerminal())
                {
                    Assert.False(status.IsRunning(), $"{status} should not be running if it's terminal");
                }
            }

            // If a status is transitional, it should not be terminal
            foreach (TimerStatus status in Enum.GetValues(typeof(TimerStatus)))
            {
                if (status.IsTransitional())
                {
                    Assert.False(status.IsTerminal(), $"{status} should not be terminal if it's transitional");
                }
            }
        }

        [Fact]
        public void StatusValues_ShouldBeUnique()
        {
            // Arrange
            var allStatusValues = (TimerStatus[])Enum.GetValues(typeof(TimerStatus));

            // Act & Assert
            var uniqueValues = new HashSet<TimerStatus>(allStatusValues);
            Assert.Equal(allStatusValues.Length, uniqueValues.Count, "All TimerStatus values should be unique");
        }

        [Fact]
        public void StatusValueCount_ShouldMatchExpected()
        {
            // Arrange
            var allStatusValues = (TimerStatus[])Enum.GetValues(typeof(TimerStatus));

            // Act & Assert
            Assert.Equal(13, allStatusValues.Length, "TimerStatus should have exactly 13 values");
        }

        #endregion
    }
}