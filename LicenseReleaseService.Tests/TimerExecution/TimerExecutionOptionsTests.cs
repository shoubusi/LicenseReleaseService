using System;
using System.Collections.Generic;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerExecutionOptionsTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldSetCorrectDefaults()
        {
            // Arrange & Act
            var options = new TimerExecutionOptions();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(1), options.DefaultInterval);
            Assert.Equal(5, options.MaxConsecutiveErrors);
            Assert.Equal(1, options.MaxConcurrentExecutions);
            Assert.Equal(TimeSpan.FromMinutes(5), options.CircuitBreakerCooldown);
            Assert.True(options.EnableAutoRestart);
            Assert.True(options.EnableExecutionTimeout);
            Assert.Equal(TimeSpan.FromMinutes(5), options.ExecutionTimeout);
            Assert.True(options.EnableMetrics);
            Assert.True(options.EnableDetailedLogging);
            Assert.True(options.EnableCircuitBreaker);
            Assert.Equal(100, options.MaxExecutionHistory);
            Assert.Equal(TimeSpan.FromSeconds(1), options.MinInterval);
            Assert.Equal(TimeSpan.FromDays(1), options.MaxInterval);
            Assert.Equal(TimeSpan.FromSeconds(30), options.SyncTimeout);
            Assert.False(options.StopOnUnhandledException);
            Assert.Equal(TimeSpan.FromSeconds(5), options.DisposalGracePeriod);
            Assert.True(options.PreventExecutionOverlap);
        }

        [Fact]
        public void Constructor_WithDefaultInterval_ShouldSetInterval()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(30);

            // Act
            var options = new TimerExecutionOptions(interval);

            // Assert
            Assert.Equal(interval, options.DefaultInterval);
        }

        [Fact]
        public void Constructor_WithFullParameters_ShouldSetAllValues()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(45);
            var maxErrors = 3;
            var cooldown = TimeSpan.FromMinutes(2);

            // Act
            var options = new TimerExecutionOptions(interval, maxErrors, cooldown);

            // Assert
            Assert.Equal(interval, options.DefaultInterval);
            Assert.Equal(maxErrors, options.MaxConsecutiveErrors);
            Assert.Equal(cooldown, options.CircuitBreakerCooldown);
        }

        [Fact]
        public void DefaultInterval_SetInvalidValue_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.DefaultInterval = TimeSpan.Zero);
            Assert.Throws<ArgumentException>(() => options.DefaultInterval = TimeSpan.FromSeconds(-1));
        }

        [Fact]
        public void MaxConsecutiveErrors_SetInvalidValue_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.MaxConsecutiveErrors = 0);
            Assert.Throws<ArgumentException>(() => options.MaxConsecutiveErrors = -1);
        }

        [Fact]
        public void CircuitBreakerCooldown_SetInvalidValue_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.CircuitBreakerCooldown = TimeSpan.Zero);
            Assert.Throws<ArgumentException>(() => options.CircuitBreakerCooldown = TimeSpan.FromSeconds(-1));
        }

        [Fact]
        public void Validate_ValidOptions_ShouldReturnEmptyList()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidDefaultInterval_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.Zero
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Default interval must be greater than zero"));
        }

        [Fact]
        public void Validate_InvalidMaxConsecutiveErrors_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                MaxConsecutiveErrors = 0
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Max consecutive errors must be at least 1"));
        }

        [Fact]
        public void Validate_InvalidExecutionTimeout_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                ExecutionTimeout = TimeSpan.Zero
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Execution timeout must be greater than zero"));
        }

        [Fact]
        public void Validate_MinIntervalGreaterThanMaxInterval_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                MinInterval = TimeSpan.FromMinutes(1),
                MaxInterval = TimeSpan.FromSeconds(30)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Min interval cannot be greater than max interval"));
        }

        [Fact]
        public void Validate_DefaultIntervalLessThanMinInterval_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(30),
                MinInterval = TimeSpan.FromMinutes(1)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Default interval cannot be less than min interval"));
        }

        [Fact]
        public void Validate_DefaultIntervalGreaterThanMaxInterval_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromHours(2),
                MaxInterval = TimeSpan.FromHours(1)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Default interval cannot be greater than max interval"));
        }

        [Fact]
        public void Clone_ShouldCreateExactCopy()
        {
            // Arrange
            var original = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(45),
                MaxConsecutiveErrors = 3,
                MaxConcurrentExecutions = 2,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(2),
                EnableAutoRestart = false,
                EnableExecutionTimeout = false,
                ExecutionTimeout = TimeSpan.FromMinutes(10),
                EnableMetrics = false,
                EnableDetailedLogging = false,
                EnableCircuitBreaker = false,
                MaxExecutionHistory = 50,
                MinInterval = TimeSpan.FromSeconds(5),
                MaxInterval = TimeSpan.FromHours(2),
                SyncTimeout = TimeSpan.FromMinutes(1),
                StopOnUnhandledException = true,
                DisposalGracePeriod = TimeSpan.FromSeconds(10),
                PreventExecutionOverlap = false
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.DefaultInterval, clone.DefaultInterval);
            Assert.Equal(original.MaxConsecutiveErrors, clone.MaxConsecutiveErrors);
            Assert.Equal(original.MaxConcurrentExecutions, clone.MaxConcurrentExecutions);
            Assert.Equal(original.CircuitBreakerCooldown, clone.CircuitBreakerCooldown);
            Assert.Equal(original.EnableAutoRestart, clone.EnableAutoRestart);
            Assert.Equal(original.EnableExecutionTimeout, clone.EnableExecutionTimeout);
            Assert.Equal(original.ExecutionTimeout, clone.ExecutionTimeout);
            Assert.Equal(original.EnableMetrics, clone.EnableMetrics);
            Assert.Equal(original.EnableDetailedLogging, clone.EnableDetailedLogging);
            Assert.Equal(original.EnableCircuitBreaker, clone.EnableCircuitBreaker);
            Assert.Equal(original.MaxExecutionHistory, clone.MaxExecutionHistory);
            Assert.Equal(original.MinInterval, clone.MinInterval);
            Assert.Equal(original.MaxInterval, clone.MaxInterval);
            Assert.Equal(original.SyncTimeout, clone.SyncTimeout);
            Assert.Equal(original.StopOnUnhandledException, clone.StopOnUnhandledException);
            Assert.Equal(original.DisposalGracePeriod, clone.DisposalGracePeriod);
            Assert.Equal(original.PreventExecutionOverlap, clone.PreventExecutionOverlap);
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(30),
                MaxConsecutiveErrors = 3,
                ExecutionTimeout = TimeSpan.FromMinutes(2)
            };

            // Act
            var result = options.ToString();

            // Assert
            Assert.Contains("Timer Execution Options:", result);
            Assert.Contains("Default Interval: 30000.00ms", result);
            Assert.Contains("Max Consecutive Errors: 3", result);
            Assert.Contains("Execution Timeout: 120000.00ms", result);
        }

        [Fact]
        public void DefaultLicenseMonitoringOptions_ShouldReturnAppropriateConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.DefaultLicenseMonitoringOptions();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(5), options.DefaultInterval);
            Assert.Equal(3, options.MaxConsecutiveErrors);
            Assert.Equal(1, options.MaxConcurrentExecutions);
            Assert.Equal(TimeSpan.FromMinutes(10), options.CircuitBreakerCooldown);
            Assert.True(options.EnableAutoRestart);
            Assert.True(options.EnableExecutionTimeout);
            Assert.Equal(TimeSpan.FromMinutes(2), options.ExecutionTimeout);
            Assert.True(options.EnableMetrics);
            Assert.True(options.EnableDetailedLogging);
            Assert.True(options.EnableCircuitBreaker);
            Assert.Equal(50, options.MaxExecutionHistory);
            Assert.Equal(TimeSpan.FromSeconds(30), options.MinInterval);
            Assert.Equal(TimeSpan.FromHours(1), options.MaxInterval);
            Assert.True(options.PreventExecutionOverlap);
        }

        [Fact]
        public void HighFrequencyOptions_ShouldReturnAppropriateConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.HighFrequencyOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(30), options.DefaultInterval);
            Assert.Equal(5, options.MaxConsecutiveErrors);
            Assert.Equal(1, options.MaxConcurrentExecutions);
            Assert.Equal(TimeSpan.FromMinutes(2), options.CircuitBreakerCooldown);
            Assert.True(options.EnableAutoRestart);
            Assert.True(options.EnableExecutionTimeout);
            Assert.Equal(TimeSpan.FromSeconds(15), options.ExecutionTimeout);
            Assert.True(options.EnableMetrics);
            Assert.False(options.EnableDetailedLogging);
            Assert.True(options.EnableCircuitBreaker);
            Assert.Equal(200, options.MaxExecutionHistory);
            Assert.Equal(TimeSpan.FromSeconds(5), options.MinInterval);
            Assert.Equal(TimeSpan.FromMinutes(10), options.MaxInterval);
            Assert.True(options.PreventExecutionOverlap);
        }

        [Fact]
        public void LowFrequencyOptions_ShouldReturnAppropriateConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.LowFrequencyOptions();

            // Assert
            Assert.Equal(TimeSpan.FromHours(1), options.DefaultInterval);
            Assert.Equal(2, options.MaxConsecutiveErrors);
            Assert.Equal(1, options.MaxConcurrentExecutions);
            Assert.Equal(TimeSpan.FromHours(1), options.CircuitBreakerCooldown);
            Assert.True(options.EnableAutoRestart);
            Assert.True(options.EnableExecutionTimeout);
            Assert.Equal(TimeSpan.FromMinutes(10), options.ExecutionTimeout);
            Assert.True(options.EnableMetrics);
            Assert.True(options.EnableDetailedLogging);
            Assert.True(options.EnableCircuitBreaker);
            Assert.Equal(25, options.MaxExecutionHistory);
            Assert.Equal(TimeSpan.FromMinutes(5), options.MinInterval);
            Assert.Equal(TimeSpan.FromDays(7), options.MaxInterval);
            Assert.False(options.PreventExecutionOverlap);
        }

        [Fact]
        public void TestOptions_ShouldReturnAppropriateConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.TestOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(1), options.DefaultInterval);
            Assert.Equal(2, options.MaxConsecutiveErrors);
            Assert.Equal(1, options.MaxConcurrentExecutions);
            Assert.Equal(TimeSpan.FromSeconds(5), options.CircuitBreakerCooldown);
            Assert.True(options.EnableAutoRestart);
            Assert.True(options.EnableExecutionTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), options.ExecutionTimeout);
            Assert.True(options.EnableMetrics);
            Assert.True(options.EnableDetailedLogging);
            Assert.True(options.EnableCircuitBreaker);
            Assert.Equal(10, options.MaxExecutionHistory);
            Assert.Equal(TimeSpan.FromMilliseconds(100), options.MinInterval);
            Assert.Equal(TimeSpan.FromSeconds(10), options.MaxInterval);
            Assert.True(options.StopOnUnhandledException);
            Assert.True(options.PreventExecutionOverlap);
        }

        [Fact]
        public void Clone_ShouldNotReferenceOriginal()
        {
            // Arrange
            var original = new TimerExecutionOptions();
            var clone = original.Clone();

            // Act
            original.DefaultInterval = TimeSpan.FromMinutes(10);
            original.MaxConsecutiveErrors = 10;

            // Assert
            Assert.NotEqual(original.DefaultInterval, clone.DefaultInterval);
            Assert.NotEqual(original.MaxConsecutiveErrors, clone.MaxConsecutiveErrors);
        }
    }
}