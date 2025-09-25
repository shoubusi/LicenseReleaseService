using System;
using LicenseReleaseService.Process;
using Xunit;

namespace LicenseReleaseService.Process.Tests
{
    /// <summary>
    /// Unit tests for ProcessExecutionException class
    /// </summary>
    public class ProcessExecutionExceptionTests
    {
        [Fact]
        public void DefaultConstructor_ShouldCreateExceptionWithDefaultMessage()
        {
            // Arrange & Act
            var exception = new ProcessExecutionException();

            // Assert
            Assert.Equal("Process execution failed", exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void MessageConstructor_ShouldCreateExceptionWithSpecifiedMessage()
        {
            // Arrange
            var message = "Custom error message";

            // Act
            var exception = new ProcessExecutionException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void MessageAndInnerExceptionConstructor_ShouldCreateExceptionWithBoth()
        {
            // Arrange
            var message = "Custom error message";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new ProcessExecutionException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void DetailedConstructor_ShouldSetAllProperties()
        {
            // Arrange
            var message = "Process failed";
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var exitCode = 1;
            var output = "Process output";
            var error = "Process error";

            // Act
            var exception = new ProcessExecutionException(message, filePath, arguments, exitCode, output, error);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(filePath, exception.FilePath);
            Assert.Equal(arguments, exception.Arguments);
            Assert.Equal(exitCode, exception.ExitCode);
            Assert.Equal(output, exception.Output);
            Assert.Equal(error, exception.Error);
        }

        [Fact]
        public void ResultConstructor_ShouldSetAllPropertiesFromResult()
        {
            // Arrange
            var message = "Process failed";
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var result = new ProcessExecutionResult
            {
                ExitCode = 2,
                ProcessId = 1234,
                Output = "Success output",
                Error = "Error output",
                ExecutionTime = TimeSpan.FromSeconds(5),
                TimedOut = false,
                Cancelled = false
            };

            // Act
            var exception = new ProcessExecutionException(message, filePath, arguments, result);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(filePath, exception.FilePath);
            Assert.Equal(arguments, exception.Arguments);
            Assert.Equal(result.ExitCode, exception.ExitCode);
            Assert.Equal(result.ProcessId, exception.ProcessId);
            Assert.Equal(result.Output, exception.Output);
            Assert.Equal(result.Error, exception.Error);
            Assert.Equal(result.ExecutionTime, exception.ExecutionTime);
            Assert.Equal(result.TimedOut, exception.TimedOut);
            Assert.Equal(result.Cancelled, exception.Cancelled);
        }

        [Fact]
        public void TimeoutException_ShouldCreateTimeoutException()
        {
            // Arrange
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var timeout = TimeSpan.FromSeconds(30);
            var executionTime = TimeSpan.FromSeconds(25);

            // Act
            var exception = ProcessExecutionException.TimeoutException(filePath, arguments, timeout, executionTime);

            // Assert
            Assert.Contains("timed out after 30.0 seconds", exception.Message);
            Assert.Equal(filePath, exception.FilePath);
            Assert.Equal(arguments, exception.Arguments);
            Assert.Null(exception.ExitCode);
            Assert.True(exception.TimedOut);
            Assert.Equal(executionTime, exception.ExecutionTime);
            Assert.Contains("executed for 25.0 seconds", exception.Error);
        }

        [Fact]
        public void CancelledException_ShouldCreateCancelledException()
        {
            // Arrange
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var executionTime = TimeSpan.FromSeconds(10);

            // Act
            var exception = ProcessExecutionException.CancelledException(filePath, arguments, executionTime);

            // Assert
            Assert.Equal("Process execution was cancelled", exception.Message);
            Assert.Equal(filePath, exception.FilePath);
            Assert.Equal(arguments, exception.Arguments);
            Assert.Null(exception.ExitCode);
            Assert.True(exception.Cancelled);
            Assert.Equal(executionTime, exception.ExecutionTime);
            Assert.Equal("Process execution was cancelled", exception.Error);
        }

        [Fact]
        public void StartFailureException_ShouldCreateStartFailureException()
        {
            // Arrange
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var innerException = new System.ComponentModel.Win32Exception("Access denied");

            // Act
            var exception = ProcessExecutionException.StartFailureException(filePath, arguments, innerException);

            // Assert
            Assert.Equal("Failed to start process: C:\\test.exe", exception.Message);
            Assert.Equal(filePath, exception.FilePath);
            Assert.Equal(arguments, exception.Arguments);
            Assert.Equal(-1, exception.ExitCode);
            Assert.Equal("Access denied", exception.Error);
        }

        [Fact]
        public void NonZeroExitCodeException_ShouldCreateNonZeroExitCodeException()
        {
            // Arrange
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var result = new ProcessExecutionResult
            {
                ExitCode = 1,
                Output = "Success output",
                Error = "Error occurred"
            };

            // Act
            var exception = ProcessExecutionException.NonZeroExitCodeException(filePath, arguments, result);

            // Assert
            Assert.Equal("Process exited with non-zero code: 1", exception.Message);
            Assert.Equal(filePath, exception.FilePath);
            Assert.Equal(arguments, exception.Arguments);
            Assert.Equal(result.ExitCode, exception.ExitCode);
            Assert.Equal(result.Output, exception.Output);
            Assert.Equal(result.Error, exception.Error);
        }

        [Fact]
        public void ToString_ShouldIncludeAllDetails()
        {
            // Arrange
            var filePath = @"C:\test.exe";
            var arguments = "arg1 arg2";
            var result = new ProcessExecutionResult
            {
                ExitCode = 1,
                ProcessId = 1234,
                Output = "Success output",
                Error = "Error occurred",
                ExecutionTime = TimeSpan.FromSeconds(5),
                TimedOut = false,
                Cancelled = false
            };

            var exception = new ProcessExecutionException("Process failed", filePath, arguments, result);

            // Act
            var toString = exception.ToString();

            // Assert
            Assert.Contains("Process failed", toString);
            Assert.Contains("File Path: C:\\test.exe", toString);
            Assert.Contains("Arguments: arg1 arg2", toString);
            Assert.Contains("Exit Code: 1", toString);
            Assert.Contains("Process ID: 1234", toString);
            Assert.Contains("Success output", toString);
            Assert.Contains("Error occurred", toString);
            Assert.Contains("Execution Time: 5000.00ms", toString);
        }

        [Fact]
        public void GetUserFriendlyMessage_ShouldReturnAppropriateMessageForTimeout()
        {
            // Arrange
            var exception = ProcessExecutionException.TimeoutException(@"C:\test.exe", "args", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(25));

            // Act
            var friendlyMessage = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Equal("The operation timed out. Please try again or increase the timeout value.", friendlyMessage);
        }

        [Fact]
        public void GetUserFriendlyMessage_ShouldReturnAppropriateMessageForCancellation()
        {
            // Arrange
            var exception = ProcessExecutionException.CancelledException(@"C:\test.exe", "args", TimeSpan.FromSeconds(5));

            // Act
            var friendlyMessage = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Equal("The operation was cancelled.", friendlyMessage);
        }

        [Fact]
        public void GetUserFriendlyMessage_ShouldReturnAppropriateMessageForNonZeroExitCode()
        {
            // Arrange
            var result = new ProcessExecutionResult
            {
                ExitCode = 2,
                Error = "File not found"
            };
            var exception = new ProcessExecutionException("Process failed", @"C:\test.exe", "args", result);

            // Act
            var friendlyMessage = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Equal("The operation failed with exit code 2. Error: File not found", friendlyMessage);
        }

        [Fact]
        public void GetUserFriendlyMessage_ShouldReturnOriginalMessageForOtherCases()
        {
            // Arrange
            var exception = new ProcessExecutionException("Custom error message");

            // Act
            var friendlyMessage = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Equal("Custom error message", friendlyMessage);
        }

        [Fact]
        public void GetUserFriendlyMessage_ShouldHandleEmptyErrorForNonZeroExitCode()
        {
            // Arrange
            var result = new ProcessExecutionResult
            {
                ExitCode = 1,
                Error = ""
            };
            var exception = new ProcessExecutionException("Process failed", @"C:\test.exe", "args", result);

            // Act
            var friendlyMessage = exception.GetUserFriendlyMessage();

            // Assert
            Assert.Equal("The operation failed with exit code 1. ", friendlyMessage);
        }
    }
}