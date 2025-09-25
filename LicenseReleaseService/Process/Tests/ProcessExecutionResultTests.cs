using System;
using System.Threading;
using LicenseReleaseService.Process;
using Xunit;

namespace LicenseReleaseService.Process.Tests
{
    /// <summary>
    /// Unit tests for ProcessExecutionResult class
    /// </summary>
    public class ProcessExecutionResultTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var result = new ProcessExecutionResult();

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.Output);
            Assert.Equal(string.Empty, result.Error);
            Assert.Equal(TimeSpan.Zero, result.ExecutionTime);
            Assert.Equal(0, result.ProcessId);
            Assert.Equal(default(DateTime), result.StartTime);
            Assert.Equal(default(DateTime), result.EndTime);
            Assert.True(result.Success); // ExitCode 0 means success
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
        }

        [Fact]
        public void Success_ShouldReturnTrue_WhenExitCodeIsZero()
        {
            // Arrange
            var result = new ProcessExecutionResult { ExitCode = 0 };

            // Act & Assert
            Assert.True(result.Success);
        }

        [Fact]
        public void Success_ShouldReturnFalse_WhenExitCodeIsNonZero()
        {
            // Arrange
            var result = new ProcessExecutionResult { ExitCode = 1 };

            // Act & Assert
            Assert.False(result.Success);
        }

        [Fact]
        public void CombinedOutput_ShouldIncludeStdOutAndStdErr_WhenBothPresent()
        {
            // Arrange
            var result = new ProcessExecutionResult
            {
                Output = "Standard output content",
                Error = "Standard error content"
            };

            // Act
            var combined = result.CombinedOutput;

            // Assert
            Assert.Contains("STDOUT:", combined);
            Assert.Contains("Standard output content", combined);
            Assert.Contains("STDERR:", combined);
            Assert.Contains("Standard error content", combined);
        }

        [Fact]
        public void CombinedOutput_ShouldIncludeOnlyStdOut_WhenNoStdErr()
        {
            // Arrange
            var result = new ProcessExecutionResult
            {
                Output = "Standard output content",
                Error = ""
            };

            // Act
            var combined = result.CombinedOutput;

            // Assert
            Assert.Contains("STDOUT:", combined);
            Assert.Contains("Standard output content", combined);
            Assert.DoesNotContain("STDERR:", combined);
        }

        [Fact]
        public void CombinedOutput_ShouldIncludeOnlyStdErr_WhenNoStdOut()
        {
            // Arrange
            var result = new ProcessExecutionResult
            {
                Output = "",
                Error = "Standard error content"
            };

            // Act
            var combined = result.CombinedOutput;

            // Assert
            Assert.DoesNotContain("STDOUT:", combined);
            Assert.Contains("STDERR:", combined);
            Assert.Contains("Standard error content", combined);
        }

        [Fact]
        public void ToString_ShouldIncludeAllDetails()
        {
            // Arrange
            var result = new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = "Test output",
                Error = "Test error",
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                ProcessId = 1234,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMilliseconds(100),
                TimedOut = false,
                Cancelled = false
            };

            // Act
            var toString = result.ToString();

            // Assert
            Assert.Contains("Process Execution Result:", toString);
            Assert.Contains("Exit Code: 0", toString);
            Assert.Contains("Success: True", toString);
            Assert.Contains("Process ID: 1234", toString);
            Assert.Contains("Output:", toString);
            Assert.Contains("Error:", toString);
        }

        [Fact]
        public void SuccessResult_ShouldCreateSuccessfulResult()
        {
            // Arrange
            var output = "Success output";
            var executionTime = TimeSpan.FromMilliseconds(50);
            var processId = 5678;

            // Act
            var result = ProcessExecutionResult.SuccessResult(output, executionTime, processId);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(output, result.Output);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.Equal(processId, result.ProcessId);
            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.Error);
        }

        [Fact]
        public void FailureResult_ShouldCreateFailedResult()
        {
            // Arrange
            var exitCode = 1;
            var error = "Error message";
            var executionTime = TimeSpan.FromMilliseconds(25);
            var processId = 9012;

            // Act
            var result = ProcessExecutionResult.FailureResult(exitCode, error, executionTime, processId);

            // Assert
            Assert.Equal(exitCode, result.ExitCode);
            Assert.Equal(error, result.Error);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.Equal(processId, result.ProcessId);
            Assert.False(result.Success);
            Assert.Equal(string.Empty, result.Output);
        }

        [Fact]
        public void TimeoutResult_ShouldCreateTimeoutResult()
        {
            // Arrange
            var executionTime = TimeSpan.FromSeconds(5);
            var processId = 3456;

            // Act
            var result = ProcessExecutionResult.TimeoutResult(executionTime, processId);

            // Assert
            Assert.Equal(-1, result.ExitCode);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.Equal(processId, result.ProcessId);
            Assert.True(result.TimedOut);
            Assert.Contains("timed out", result.Error);
        }

        [Fact]
        public void CancelledResult_ShouldCreateCancelledResult()
        {
            // Arrange
            var executionTime = TimeSpan.FromSeconds(2);
            var processId = 7890;

            // Act
            var result = ProcessExecutionResult.CancelledResult(executionTime, processId);

            // Assert
            Assert.Equal(-1, result.ExitCode);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.Equal(processId, result.ProcessId);
            Assert.True(result.Cancelled);
            Assert.Contains("cancelled", result.Error);
        }
    }
}