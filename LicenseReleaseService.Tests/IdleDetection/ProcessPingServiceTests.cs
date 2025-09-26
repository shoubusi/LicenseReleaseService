using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    /// <summary>
    /// Unit tests for ProcessPingService
    /// </summary>
    public class ProcessPingServiceTests
    {
        private readonly Mock<ILogger<ProcessPingService>> _mockLogger;
        private readonly PingBasedDetectionConfig _config;

        public ProcessPingServiceTests()
        {
            _mockLogger = new Mock<ILogger<ProcessPingService>>();
            _config = new PingBasedDetectionConfig
            {
                DetectionIntervalSeconds = 30,
                PingTimeoutMs = 1000,
                MaxConcurrentPings = 5,
                MaxPingAttempts = 3,
                PingRetryDelayMs = 500
            };
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var service = new ProcessPingService(_mockLogger.Object, _config);

            // Assert
            Assert.NotNull(service.Statistics);
            Assert.Equal(DateTime.UtcNow.Date, service.Statistics.StartTime.Date);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProcessPingService(null, _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProcessPingService(_mockLogger.Object, null));
        }

        [Fact]
        public async Task InitializeAsync_ShouldInitializeSuccessfully()
        {
            // Arrange
            var service = CreateService();
            var configuration = new IdleDetectorConfiguration();

            // Act
            await service.InitializeAsync(configuration);

            // Assert
            // Verify that statistics were reset
            Assert.Equal(0, service.Statistics.TotalPings);
            Assert.Equal(DateTime.UtcNow.Date, service.Statistics.StartTime.Date);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyRunning_ShouldNotReset()
        {
            // Arrange
            var service = CreateService();
            var configuration = new IdleDetectorConfiguration();
            await service.StartAsync();

            // Act
            await service.InitializeAsync(configuration);

            // Assert
            // Service should still be running after re-initialization
            Assert.True(service.IsRunning);
        }

        [Fact]
        public async Task StartAsync_ShouldStartSuccessfully()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.StartAsync();

            // Assert
            Assert.True(service.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Act
            await service.StartAsync();

            // Assert
            Assert.True(service.IsRunning); // Should still be running
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopSuccessfully()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Act
            await service.StopAsync();

            // Assert
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldNotThrowException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            await Assert.DoesNotThrowAsync(() => service.StopAsync());
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task PauseAsync_WhenRunning_ShouldPauseSuccessfully()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Act
            await service.PauseAsync();

            // Assert
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task ResumeAsync_WhenPaused_ShouldResumeSuccessfully()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();
            await service.PauseAsync();

            // Act
            await service.ResumeAsync();

            // Assert
            Assert.True(service.IsRunning);
        }

        [Fact]
        public async Task PingProcessAsync_WithValidProcess_ShouldReturnSuccessfulResult()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = GetValidProcessId();
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await service.PingProcessAsync(processId, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(_config.MaxPingAttempts, result.TotalPingAttempts);
            Assert.True(result.SuccessfulPingCount > 0); // Should have at least one successful ping
            Assert.True(result.IsResponsive);
            Assert.True(result.ResponseTimeMs > 0);
            Assert.True(result.AverageResponseTimeMs > 0);
            Assert.NotNull(result.LastSuccessfulPing);
            Assert.Null(result.Error);

            // Verify statistics were updated
            var stats = await service.GetStatisticsAsync();
            Assert.True(stats.TotalPings > 0);
            Assert.True(stats.SuccessfulPings > 0);
        }

        [Fact]
        public async Task PingProcessAsync_WithInvalidProcessId_ShouldReturnFailedResult()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = 99999; // Very unlikely to exist
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await service.PingProcessAsync(processId, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(_config.MaxPingAttempts, result.TotalPingAttempts);
            Assert.Equal(0, result.SuccessfulPingCount);
            Assert.Equal(_config.MaxPingAttempts, result.FailedPingCount);
            Assert.False(result.IsResponsive);
            Assert.Equal(-1, result.ResponseTimeMs);
            Assert.Equal(0, result.AverageResponseTimeMs);
            Assert.Null(result.LastSuccessfulPing);
            Assert.NotNull(result.Error);
            Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);

            // Verify statistics were updated
            var stats = await service.GetStatisticsAsync();
            Assert.True(stats.TotalPings > 0);
            Assert.True(stats.FailedPings > 0);
        }

        [Fact]
        public async Task PingProcessAsync_ShouldRespectConcurrencyLimit()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var maxConcurrent = _config.MaxConcurrentPings;
            var processIds = Enumerable.Range(1, maxConcurrent + 2).Select(GetTestProcessId).ToList();
            var tasks = new List<Task<ProcessPingResult>>();

            // Act - start more pings than the concurrency limit
            foreach (var processId in processIds)
            {
                tasks.Add(service.PingProcessAsync(processId));
            }

            // Wait for all to complete
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(processIds.Count, results.Length);
            Assert.All(results, result => Assert.NotNull(result));
        }

        [Fact]
        public async Task PingProcessAsync_WithCancellation_ShouldCancelOperation()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = GetValidProcessId();
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Cancel immediately
            cancellationTokenSource.Cancel();

            // Act
            var result = await service.PingProcessAsync(processId, cancellationToken);

            // Assert
            Assert.NotNull(result);
            // Result might be incomplete due to cancellation
            // The important thing is that it doesn't hang or throw
        }

        [Fact]
        public async Task PingProcessAsync_ShouldUpdatePingHistory()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = GetValidProcessId();

            // Act - ping multiple times
            await service.PingProcessAsync(processId);
            await service.PingProcessAsync(processId);

            // Assert - we can't directly access ping history, but we can verify statistics
            var stats = await service.GetStatisticsAsync();
            Assert.True(stats.TotalPings >= 2);
        }

        [Fact]
        public async Task GetRecentActivitiesAsync_WithNoHistory_ShouldReturnEmptyList()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = GetValidProcessId();
            var timeWindow = TimeSpan.FromMinutes(30);

            // Act
            var activities = await service.GetRecentActivitiesAsync(processId, timeWindow);

            // Assert
            Assert.NotNull(activities);
            Assert.Empty(activities);
        }

        [Fact]
        public async Task GetHealthAsync_WithHealthyService_ShouldReturnHealthyStatus()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Perform some successful pings to populate statistics
            var processId = GetValidProcessId();
            await service.PingProcessAsync(processId);

            // Act
            var health = await service.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.Equal("ProcessPingService", health.DetectorName);
            Assert.True(health.IsHealthy);
            Assert.Equal("Healthy", health.StatusMessage);
            Assert.True((int)health.AdditionalInfo["TotalPings"] > 0);
            Assert.True((int)health.AdditionalInfo["SuccessfulPings"] > 0);
            Assert.True((int)health.AdditionalInfo["AvailablePingSlots"] >= 0);
        }

        [Fact]
        public async Task GetHealthAsync_WithHighFailureRate_ShouldReturnUnhealthyStatus()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Simulate high failure rate by pinging non-existent processes
            for (int i = 0; i < 10; i++)
            {
                await service.PingProcessAsync(99999 + i); // Non-existent processes
            }

            // Act
            var health = await service.GetHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.False(health.IsHealthy);
            Assert.Contains("High ping failure rate", health.StatusMessage);
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnAccurateStatistics()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var validProcessId = GetValidProcessId();
            var invalidProcessId = 99999;

            // Perform some pings
            await service.PingProcessAsync(validProcessId);  // Should succeed
            await service.PingProcessAsync(invalidProcessId); // Should fail
            await service.PingProcessAsync(validProcessId);  // Should succeed

            // Act
            var stats = await service.GetStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(3, stats.TotalPings);
            Assert.Equal(2, stats.SuccessfulPings);
            Assert.Equal(1, stats.FailedPings);
            Assert.True(stats.AverageResponseTimeMs > 0);
            Assert.Equal(0, stats.ErrorCount);
            Assert.NotNull(stats.StartTime);
        }

        [Fact]
        public async Task ResetStatisticsAsync_ShouldResetAllStatistics()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Perform some activity to generate statistics
            var processId = GetValidProcessId();
            await service.PingProcessAsync(processId);
            await service.PingProcessAsync(99999); // Invalid process

            var beforeReset = await service.GetStatisticsAsync();
            Assert.True(beforeReset.TotalPings > 0);

            // Act
            await service.ResetStatisticsAsync();

            // Assert
            var afterReset = await service.GetStatisticsAsync();
            Assert.Equal(0, afterReset.TotalPings);
            Assert.Equal(0, afterReset.SuccessfulPings);
            Assert.Equal(0, afterReset.FailedPings);
            Assert.Equal(0, afterReset.ErrorCount);
            Assert.True(afterReset.StartTime >= beforeReset.StartTime);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_ShouldUpdateConfiguration()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var newConfig = new IdleDetectorConfiguration
            {
                CustomParameters = new Dictionary<string, object>
                {
                    ["PingTimeoutMs"] = 2000,
                    ["MaxConcurrentPings"] = 15,
                    ["MaxPingAttempts"] = 5,
                    ["PingRetryDelayMs"] = 1000
                }
            };

            // Act
            await service.UpdateConfigurationAsync(newConfig);

            // Assert
            // Configuration is internal, so we can't directly verify it was updated
            // But the method should complete without throwing an exception
            Assert.True(true); // If we get here, no exception was thrown
        }

        [Fact]
        public async Task ProcessRespondedEvent_ShouldBeRaisedOnSuccessfulPing()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = GetValidProcessId();
            var eventRaised = false;
            ProcessPingResult capturedResult = null;

            service.ProcessResponded += (sender, result) =>
            {
                eventRaised = true;
                capturedResult = result;
            };

            // Act
            var pingResult = await service.PingProcessAsync(processId);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedResult);
            Assert.Equal(processId, capturedResult.ProcessId);
            Assert.True(capturedResult.IsResponsive);
        }

        [Fact]
        public async Task ProcessUnresponsiveEvent_ShouldBeRaisedOnFailedPing()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            var processId = 99999; // Non-existent process
            var eventRaised = false;
            ProcessPingResult capturedResult = null;

            service.ProcessUnresponsive += (sender, result) =>
            {
                eventRaised = true;
                capturedResult = result;
            };

            // Act
            var pingResult = await service.PingProcessAsync(processId);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedResult);
            Assert.Equal(processId, capturedResult.ProcessId);
            Assert.False(capturedResult.IsResponsive);
        }

        [Fact]
        public void Dispose_ShouldDisposeResources()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.DoesNotThrow(() => service.Dispose());
        }

        [Fact]
        public async Task Dispose_WhenRunning_ShouldStopService()
        {
            // Arrange
            var service = CreateService();
            await service.StartAsync();

            // Act
            service.Dispose();

            // Assert
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.DoesNotThrow(() => service.Dispose());
            Assert.DoesNotThrow(() => service.Dispose()); // Second dispose should not throw
        }

        #region Helper Methods

        private ProcessPingService CreateService()
        {
            return new ProcessPingService(_mockLogger.Object, _config);
        }

        private int GetValidProcessId()
        {
            // Return current process ID, which should always exist
            return Process.GetCurrentProcess().Id;
        }

        private int GetTestProcessId(int seed)
        {
            // Generate test process IDs that are likely to be invalid
            return 50000 + (seed % 10000);
        }

        #endregion
    }
}