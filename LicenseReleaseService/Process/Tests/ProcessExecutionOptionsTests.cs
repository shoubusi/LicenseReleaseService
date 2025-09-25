using System;
using System.Collections.Generic;
using System.Text;
using LicenseReleaseService.Process;
using Xunit;

namespace LicenseReleaseService.Process.Tests
{
    /// <summary>
    /// Unit tests for ProcessExecutionOptions class
    /// </summary>
    public class ProcessExecutionOptionsTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var options = new ProcessExecutionOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(5), options.RetryDelay);
            Assert.Equal(3, options.MaxRetries);
            Assert.False(options.RedirectStandardInput);
            Assert.True(options.CreateNoWindow);
            Assert.False(options.UseShellExecute);
            Assert.Equal(string.Empty, options.WorkingDirectory);
            Assert.NotNull(options.EnvironmentVariables);
            Assert.Empty(options.EnvironmentVariables);
            Assert.True(options.KillProcessTreeOnTimeout);
            Assert.True(options.EnableDetailedLogging);
            Assert.Equal(4096, options.BufferSize);
            Assert.Equal(Encoding.UTF8, options.Encoding);
            Assert.False(options.ThrowOnNonZeroExitCode);
            Assert.Equal(0, options.MaxOutputSize);
        }

        [Fact]
        public void Timeout_ShouldThrowException_WhenSetToZeroOrNegative()
        {
            // Arrange
            var options = new ProcessExecutionOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.Timeout = TimeSpan.Zero);
            Assert.Throws<ArgumentException>(() => options.Timeout = TimeSpan.FromSeconds(-1));
        }

        [Fact]
        public void Timeout_ShouldAcceptPositiveValue()
        {
            // Arrange
            var options = new ProcessExecutionOptions();
            var timeout = TimeSpan.FromSeconds(60);

            // Act
            options.Timeout = timeout;

            // Assert
            Assert.Equal(timeout, options.Timeout);
        }

        [Fact]
        public void RetryDelay_ShouldThrowException_WhenSetToNegative()
        {
            // Arrange
            var options = new ProcessExecutionOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.RetryDelay = TimeSpan.FromSeconds(-1));
        }

        [Fact]
        public void RetryDelay_ShouldAcceptZeroOrPositiveValue()
        {
            // Arrange
            var options = new ProcessExecutionOptions();
            var retryDelay = TimeSpan.FromSeconds(10);

            // Act
            options.RetryDelay = retryDelay;

            // Assert
            Assert.Equal(retryDelay, options.RetryDelay);
        }

        [Fact]
        public void MaxRetries_ShouldThrowException_WhenSetToNegative()
        {
            // Arrange
            var options = new ProcessExecutionOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.MaxRetries = -1);
        }

        [Fact]
        public void MaxRetries_ShouldAcceptZeroOrPositiveValue()
        {
            // Arrange
            var options = new ProcessExecutionOptions();
            var maxRetries = 5;

            // Act
            options.MaxRetries = maxRetries;

            // Assert
            Assert.Equal(maxRetries, options.MaxRetries);
        }

        [Fact]
        public void Constructor_WithTimeout_ShouldSetTimeout()
        {
            // Arrange
            var timeout = TimeSpan.FromSeconds(45);

            // Act
            var options = new ProcessExecutionOptions(timeout);

            // Assert
            Assert.Equal(timeout, options.Timeout);
            Assert.Equal(3, options.MaxRetries); // Default value
            Assert.Equal(TimeSpan.FromSeconds(5), options.RetryDelay); // Default value
        }

        [Fact]
        public void Constructor_WithTimeoutAndRetrySettings_ShouldSetAllValues()
        {
            // Arrange
            var timeout = TimeSpan.FromSeconds(90);
            var maxRetries = 7;
            var retryDelay = TimeSpan.FromSeconds(15);

            // Act
            var options = new ProcessExecutionOptions(timeout, maxRetries, retryDelay);

            // Assert
            Assert.Equal(timeout, options.Timeout);
            Assert.Equal(maxRetries, options.MaxRetries);
            Assert.Equal(retryDelay, options.RetryDelay);
        }

        [Fact]
        public void Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new ProcessExecutionOptions
            {
                Timeout = TimeSpan.FromSeconds(120),
                MaxRetries = 10,
                RetryDelay = TimeSpan.FromSeconds(30),
                WorkingDirectory = @"C:\Test\Directory",
                EnableDetailedLogging = false,
                ThrowOnNonZeroExitCode = true,
                MaxOutputSize = 1024 * 1024
            };

            original.EnvironmentVariables["TEST_VAR"] = "TEST_VALUE";

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.Timeout, clone.Timeout);
            Assert.Equal(original.MaxRetries, clone.MaxRetries);
            Assert.Equal(original.RetryDelay, clone.RetryDelay);
            Assert.Equal(original.WorkingDirectory, clone.WorkingDirectory);
            Assert.Equal(original.EnableDetailedLogging, clone.EnableDetailedLogging);
            Assert.Equal(original.ThrowOnNonZeroExitCode, clone.ThrowOnNonZeroExitCode);
            Assert.Equal(original.MaxOutputSize, clone.MaxOutputSize);
            Assert.Equal(original.EnvironmentVariables["TEST_VAR"], clone.EnvironmentVariables["TEST_VAR"]);

            // Ensure it's a deep copy
            original.EnvironmentVariables["TEST_VAR"] = "MODIFIED_VALUE";
            Assert.Equal("TEST_VALUE", clone.EnvironmentVariables["TEST_VAR"]);
        }

        [Fact]
        public void Validate_ShouldReturnEmptyList_WhenAllValuesAreValid()
        {
            // Arrange
            var options = new ProcessExecutionOptions();

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_ShouldReturnErrors_WhenValuesAreInvalid()
        {
            // Arrange
            var options = new ProcessExecutionOptions();
            options.Timeout = TimeSpan.Zero;
            options.RetryDelay = TimeSpan.FromSeconds(-1);
            options.MaxRetries = -1;
            options.BufferSize = 0;
            options.MaxOutputSize = -1;

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Equal(5, errors.Count);
            Assert.Contains(errors, e => e.Contains("Timeout must be greater than zero"));
            Assert.Contains(errors, e => e.Contains("Retry delay cannot be negative"));
            Assert.Contains(errors, e => e.Contains("Max retries cannot be negative"));
            Assert.Contains(errors, e => e.Contains("Buffer size must be greater than zero"));
            Assert.Contains(errors, e => e.Contains("Max output size cannot be negative"));
        }

        [Fact]
        public void ToString_ShouldIncludeAllRelevantDetails()
        {
            // Arrange
            var options = new ProcessExecutionOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                MaxRetries = 5,
                RetryDelay = TimeSpan.FromSeconds(10),
                WorkingDirectory = @"C:\Test",
                EnableDetailedLogging = false
            };

            options.EnvironmentVariables["TEST"] = "VALUE";

            // Act
            var toString = options.ToString();

            // Assert
            Assert.Contains("Process Execution Options:", toString);
            Assert.Contains("Timeout: 60.0s", toString);
            Assert.Contains("Max Retries: 5", toString);
            Assert.Contains("Retry Delay: 10.0s", toString);
            Assert.Contains("Working Directory: C:\\Test", toString);
            Assert.Contains("Enable Detailed Logging: False", toString);
            Assert.Contains("Environment Variables: 1 variables", toString);
        }

        [Fact]
        public void DefaultLicenseManagerOptions_ShouldReturnAppropriateSettings()
        {
            // Act
            var options = ProcessExecutionOptions.DefaultLicenseManagerOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(60), options.Timeout);
            Assert.Equal(3, options.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(2), options.RetryDelay);
            Assert.True(options.CreateNoWindow);
            Assert.False(options.UseShellExecute);
            Assert.True(options.EnableDetailedLogging);
            Assert.False(options.ThrowOnNonZeroExitCode);
        }

        [Fact]
        public void QuickOperationOptions_ShouldReturnFastSettings()
        {
            // Act
            var options = ProcessExecutionOptions.QuickOperationOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(10), options.Timeout);
            Assert.Equal(1, options.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(1), options.RetryDelay);
            Assert.True(options.CreateNoWindow);
            Assert.False(options.UseShellExecute);
            Assert.False(options.EnableDetailedLogging);
        }

        [Fact]
        public void LongOperationOptions_ShouldReturnExtendedSettings()
        {
            // Act
            var options = ProcessExecutionOptions.LongOperationOptions();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
            Assert.Equal(5, options.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(10), options.RetryDelay);
            Assert.True(options.CreateNoWindow);
            Assert.False(options.UseShellExecute);
            Assert.True(options.EnableDetailedLogging);
            Assert.Equal(10 * 1024 * 1024, options.MaxOutputSize); // 10MB
        }
    }
}