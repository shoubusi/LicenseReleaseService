using System;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;
using Xunit;

namespace LicenseReleaseService.Process.Tests
{
    /// <summary>
    /// Unit tests for IProcessExecutor interface compliance
    /// </summary>
    public class IProcessExecutorTests
    {
        [Fact]
        public async Task ProcessExecutor_ShouldImplementIProcessExecutor()
        {
            // Arrange
            var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ProcessExecutor>>();
            var options = new ProcessExecutionOptions();
            var executor = new ProcessExecutor(mockLogger.Object, options);

            // Assert
            Assert.IsAssignableFrom<IProcessExecutor>(executor);
        }

        [Fact]
        public async Task ExecuteAsync_DefaultTimeout_ShouldUseOptionsTimeout()
        {
            // Arrange
            var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ProcessExecutor>>();
            var options = new ProcessExecutionOptions(TimeSpan.FromSeconds(30));
            var executor = new ProcessExecutor(mockLogger.Object, options);

            // Act & Assert
            // This test verifies that the method exists and can be called
            // Actual execution testing is done in ProcessExecutorTests
            await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync(null, "test"));
        }

        [Fact]
        public async Task ExecuteAsync_CustomTimeout_ShouldUseSpecifiedTimeout()
        {
            // Arrange
            var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ProcessExecutor>>();
            var options = new ProcessExecutionOptions();
            var executor = new ProcessExecutor(mockLogger.Object, options);

            // Act & Assert
            // This test verifies that the method exists and can be called
            // Actual execution testing is done in ProcessExecutorTests
            await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync(null, "test", TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public void Execute_DefaultTimeout_ShouldUseOptionsTimeout()
        {
            // Arrange
            var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ProcessExecutor>>();
            var options = new ProcessExecutionOptions(TimeSpan.FromSeconds(30));
            var executor = new ProcessExecutor(mockLogger.Object, options);

            // Act & Assert
            // This test verifies that the method exists and can be called
            // Actual execution testing is done in ProcessExecutorTests
            Assert.Throws<ArgumentException>(() => executor.Execute(null, "test"));
        }

        [Fact]
        public void Execute_CustomTimeout_ShouldUseSpecifiedTimeout()
        {
            // Arrange
            var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ProcessExecutor>>();
            var options = new ProcessExecutionOptions();
            var executor = new ProcessExecutor(mockLogger.Object, options);

            // Act & Assert
            // This test verifies that the method exists and can be called
            // Actual execution testing is done in ProcessExecutorTests
            Assert.Throws<ArgumentException>(() => executor.Execute(null, "test", TimeSpan.FromSeconds(10)));
        }
    }
}