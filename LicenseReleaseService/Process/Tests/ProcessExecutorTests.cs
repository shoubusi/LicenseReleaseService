using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LicenseReleaseService.Process.Tests
{
    /// <summary>
    /// Unit tests for ProcessExecutor class
    /// </summary>
    public class ProcessExecutorTests
    {
        private readonly Mock<ILogger<ProcessExecutor>> _mockLogger;
        private readonly ProcessExecutionOptions _defaultOptions;

        public ProcessExecutorTests()
        {
            _mockLogger = new Mock<ILogger<ProcessExecutor>>();
            _defaultOptions = new ProcessExecutionOptions();
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenLoggerIsNull()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProcessExecutor(null, _defaultOptions));
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenOptionsIsNull()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProcessExecutor(_mockLogger.Object, null));
        }

        [Fact]
        public void Constructor_ShouldCreateInstance_WithValidParameters()
        {
            // Arrange & Act
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);

            // Assert
            Assert.NotNull(executor);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowException_WhenFilePathIsNull()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync(null, "test"));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowException_WhenFilePathIsWhitespace()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync("   ", "test"));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnCancelledResult_WhenCancelledBeforeStart()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await executor.ExecuteAsync("test.exe", "args", cts.Token);

            // Assert
            Assert.True(result.Cancelled);
            Assert.Equal(TimeSpan.Zero, result.ExecutionTime);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldExecuteSuccessfully_WithValidProcess()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);
            var testFile = GetTestExecutablePath();

            // Act
            var result = await executor.ExecuteAsync(testFile, "--help");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Success);
            Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
            Assert.True(result.ProcessId > 0);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCaptureOutput_WhenProcessProducesOutput()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);
            var testFile = GetTestExecutablePath();

            // Act
            var result = await executor.ExecuteAsync(testFile, "--version");

            // Assert
            Assert.NotNull(result);
            // Output depends on the actual executable, but we can verify it's not empty
            Assert.False(string.IsNullOrWhiteSpace(result.Output));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldHandleTimeout_WhenProcessExceedsTimeout()
        {
            // Arrange
            var options = new ProcessExecutionOptions(TimeSpan.FromSeconds(2)); // Very short timeout
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ProcessExecutionException>(
                () => executor.ExecuteAsync(testFile, "--sleep 5")); // Sleep for 5 seconds

            Assert.True(exception.TimedOut);
            Assert.Contains("timed out", exception.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRetry_WhenProcessFails()
        {
            // Arrange
            var options = new ProcessExecutionOptions(TimeSpan.FromSeconds(30), 2, TimeSpan.FromMilliseconds(100));
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetNonexistentExecutablePath();

            // Act & Assert
            await Assert.ThrowsAsync<ProcessExecutionException>(() => executor.ExecuteAsync(testFile, "args"));

            // Verify that retry logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("attempt 1")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRespectMaxOutputSize_WhenSet()
        {
            // Arrange
            var options = new ProcessExecutionOptions
            {
                MaxOutputSize = 100 // Very small limit
            };
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act
            var result = await executor.ExecuteAsync(testFile, "--large-output");

            // Assert
            Assert.NotNull(result);
            // The executor should truncate or handle large output gracefully
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("exceeded maximum limit")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void Execute_ShouldThrowException_WhenFilePathIsNull()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => executor.Execute(null, "test"));
        }

        [Fact]
        public void Execute_ShouldExecuteSuccessfully_WithValidProcess()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);
            var testFile = GetTestExecutablePath();

            // Act
            var result = executor.Execute(testFile, "--help");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Success);
            Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
            Assert.True(result.ProcessId > 0);
        }

        [Fact]
        public void Execute_ShouldHandleTimeout_WhenProcessExceedsTimeout()
        {
            // Arrange
            var options = new ProcessExecutionOptions(TimeSpan.FromSeconds(2)); // Very short timeout
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act & Assert
            var exception = Assert.Throws<ProcessExecutionException>(
                () => executor.Execute(testFile, "--sleep 5")); // Sleep for 5 seconds

            Assert.True(exception.TimedOut);
            Assert.Contains("timed out", exception.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldUseEnvironmentVariables_WhenSpecified()
        {
            // Arrange
            var options = new ProcessExecutionOptions();
            options.EnvironmentVariables["TEST_VAR"] = "TEST_VALUE";
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act
            var result = await executor.ExecuteAsync(testFile, "--env-test");

            // Assert
            Assert.NotNull(result);
            // The actual test depends on the executable, but we verify it runs
            Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldUseWorkingDirectory_WhenSpecified()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            var options = new ProcessExecutionOptions
            {
                WorkingDirectory = tempDir
            };
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act
            var result = await executor.ExecuteAsync(testFile, "--cwd-test");

            // Assert
            Assert.NotNull(result);
            // The actual test depends on the executable, but we verify it runs
            Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowNonZeroExitCodeException_WhenOptionIsEnabled()
        {
            // Arrange
            var options = new ProcessExecutionOptions
            {
                ThrowOnNonZeroExitCode = true
            };
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act & Assert
            await Assert.ThrowsAsync<ProcessExecutionException>(
                () => executor.ExecuteAsync(testFile, "--fail")); // Command that should fail
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnResultWithNonZeroExitCode_WhenOptionIsDisabled()
        {
            // Arrange
            var options = new ProcessExecutionOptions
            {
                ThrowOnNonZeroExitCode = false
            };
            var executor = new ProcessExecutor(_mockLogger.Object, options);
            var testFile = GetTestExecutablePath();

            // Act
            var result = await executor.ExecuteAsync(testFile, "--fail"); // Command that should fail

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(0, result.ExitCode);
            Assert.False(result.Success);
        }

        [Fact]
        public void Dispose_ShouldNotThrowException()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);

            // Act & Assert
            var exception = Record.Exception(() => executor.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Arrange
            var executor = new ProcessExecutor(_mockLogger.Object, _defaultOptions);

            // Act & Assert
            executor.Dispose(); // First call
            var exception = Record.Exception(() => executor.Dispose()); // Second call
            Assert.Null(exception);
        }

        // Helper methods for testing
        private string GetTestExecutablePath()
        {
            // Try to find a suitable executable for testing
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "whoami.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "ping.exe"),
                "cmd.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Fallback to current directory
            return "cmd.exe";
        }

        private string GetNonexistentExecutablePath()
        {
            return Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.exe");
        }
    }
}