using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    public class ProcessMetricsTests
    {
        private readonly Mock<ILogger<ProcessMetrics>> _mockLogger;
        private readonly ProcessMetrics _processMetrics;

        public ProcessMetricsTests()
        {
            _mockLogger = new Mock<ILogger<ProcessMetrics>>();
            _processMetrics = new ProcessMetrics(_mockLogger.Object, 100);
        }

        [Fact]
        public async Task RecordExecutionAsync_ValidMetrics_ShouldRecordSuccessfully()
        {
            // Arrange
            var metrics = new ProcessExecutionMetrics
            {
                ProcessId = 1234,
                OperationType = "lmstat",
                Server = "test-server",
                Port = 27000,
                StartTime = DateTime.Now.AddSeconds(-1),
                EndTime = DateTime.Now,
                ExecutionDuration = TimeSpan.FromSeconds(1),
                ExitCode = 0,
                Success = true,
                ConfiguredTimeout = TimeSpan.FromSeconds(30),
                Command = "lmstat -c 27000@test-server"
            };

            // Act
            await _processMetrics.RecordExecutionAsync(metrics);

            // Assert
            var recentMetrics = await _processMetrics.GetRecentMetricsAsync(10);
            Assert.Single(recentMetrics);
            Assert.Equal(metrics.ProcessId, recentMetrics[0].ProcessId);
            Assert.Equal(metrics.OperationType, recentMetrics[0].OperationType);
            Assert.Equal(metrics.Server, recentMetrics[0].Server);
            Assert.Equal(metrics.Port, recentMetrics[0].Port);
            Assert.True(recentMetrics[0].Success);
        }

        [Fact]
        public async Task RecordExecutionAsync_MultipleMetrics_ShouldUpdateSummary()
        {
            // Arrange
            var metrics1 = new ProcessExecutionMetrics
            {
                ProcessId = 1234,
                OperationType = "lmstat",
                Server = "test-server",
                Port = 27000,
                StartTime = DateTime.Now.AddSeconds(-2),
                EndTime = DateTime.Now.AddSeconds(-1),
                ExecutionDuration = TimeSpan.FromSeconds(1),
                ExitCode = 0,
                Success = true,
                ConfiguredTimeout = TimeSpan.FromSeconds(30)
            };

            var metrics2 = new ProcessExecutionMetrics
            {
                ProcessId = 1235,
                OperationType = "lmstat",
                Server = "test-server",
                Port = 27000,
                StartTime = DateTime.Now.AddSeconds(-1),
                EndTime = DateTime.Now,
                ExecutionDuration = TimeSpan.FromSeconds(2),
                ExitCode = 0,
                Success = false,
                ConfiguredTimeout = TimeSpan.FromSeconds(30)
            };

            // Act
            await _processMetrics.RecordExecutionAsync(metrics1);
            await _processMetrics.RecordExecutionAsync(metrics2);

            // Assert
            var summary = await _processMetrics.GetOperationSummaryAsync("lmstat", "test-server", 27000);
            Assert.NotNull(summary);
            Assert.Equal(2, summary.TotalExecutions);
            Assert.Equal(1, summary.SuccessfulExecutions);
            Assert.Equal(1, summary.FailedExecutions);
            Assert.Equal(50.0, summary.SuccessRate, 1);
            Assert.Equal(TimeSpan.FromSeconds(1.5), summary.AverageExecutionTime);
        }

        [Fact]
        public async Task GetRecentMetricsAsync_ExceedingMaxCount_ShouldReturnLimitedResults()
        {
            // Arrange
            for (int i = 0; i < 150; i++)
            {
                var metrics = new ProcessExecutionMetrics
                {
                    ProcessId = 1000 + i,
                    OperationType = "lmstat",
                    Server = "test-server",
                    Port = 27000,
                    StartTime = DateTime.Now.AddSeconds(-i),
                    EndTime = DateTime.Now.AddSeconds(-i + 1),
                    ExecutionDuration = TimeSpan.FromSeconds(1),
                    ExitCode = 0,
                    Success = true,
                    ConfiguredTimeout = TimeSpan.FromSeconds(30)
                };
                await _processMetrics.RecordExecutionAsync(metrics);
            }

            // Act
            var recentMetrics = await _processMetrics.GetRecentMetricsAsync(100);

            // Assert
            Assert.Equal(100, recentMetrics.Count);
            Assert.Equal(1149, recentMetrics[0].ProcessId); // Should be the 50th most recent
            Assert.Equal(1248, recentMetrics[99].ProcessId); // Should be the most recent
        }

        [Fact]
        public async Task GetMetricsByTimeRangeAsync_ValidRange_ShouldReturnFilteredMetrics()
        {
            // Arrange
            var now = DateTime.Now;
            var metrics1 = new ProcessExecutionMetrics
            {
                ProcessId = 1234,
                OperationType = "lmstat",
                Server = "test-server",
                Port = 27000,
                StartTime = now.AddMinutes(-10),
                EndTime = now.AddMinutes(-9),
                ExecutionDuration = TimeSpan.FromSeconds(1),
                ExitCode = 0,
                Success = true,
                ConfiguredTimeout = TimeSpan.FromSeconds(30)
            };

            var metrics2 = new ProcessExecutionMetrics
            {
                ProcessId = 1235,
                OperationType = "lmstat",
                Server = "test-server",
                Port = 27000,
                StartTime = now.AddMinutes(-5),
                EndTime = now.AddMinutes(-4),
                ExecutionDuration = TimeSpan.FromSeconds(1),
                ExitCode = 0,
                Success = true,
                ConfiguredTimeout = TimeSpan.FromSeconds(30)
            };

            await _processMetrics.RecordExecutionAsync(metrics1);
            await _processMetrics.RecordExecutionAsync(metrics2);

            // Act
            var filteredMetrics = await _processMetrics.GetMetricsByTimeRangeAsync(now.AddMinutes(-7), now.AddMinutes(-3));

            // Assert
            Assert.Single(filteredMetrics);
            Assert.Equal(1235, filteredMetrics[0].ProcessId);
        }

        [Fact]
        public async Task ClearAsync_ShouldClearAllMetricsAndSummaries()
        {
            // Arrange
            var metrics = new ProcessExecutionMetrics
            {
                ProcessId = 1234,
                OperationType = "lmstat",
                Server = "test-server",
                Port = 27000,
                StartTime = DateTime.Now.AddSeconds(-1),
                EndTime = DateTime.Now,
                ExecutionDuration = TimeSpan.FromSeconds(1),
                ExitCode = 0,
                Success = true,
                ConfiguredTimeout = TimeSpan.FromSeconds(30)
            };

            await _processMetrics.RecordExecutionAsync(metrics);

            // Act
            await _processMetrics.ClearAsync();

            // Assert
            var recentMetrics = await _processMetrics.GetRecentMetricsAsync(10);
            var summary = await _processMetrics.GetOperationSummaryAsync("lmstat", "test-server", 27000);

            Assert.Empty(recentMetrics);
            Assert.Null(summary);
        }

        [Fact]
        public async Task RecordExecutionAsync_NullMetrics_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _processMetrics.RecordExecutionAsync(null));
        }

        [Fact]
        public void ProcessExecutionMetrics_SuccessProperty_ShouldReturnCorrectValue()
        {
            // Arrange & Act & Assert
            var metrics = new ProcessExecutionMetrics
            {
                ExitCode = 0,
                TimedOut = false,
                Cancelled = false
            };
            Assert.True(metrics.Success);

            metrics.ExitCode = 1;
            Assert.False(metrics.Success);

            metrics.ExitCode = 0;
            metrics.TimedOut = true;
            Assert.False(metrics.Success);

            metrics.TimedOut = false;
            metrics.Cancelled = true;
            Assert.False(metrics.Success);
        }

        [Fact]
        public void ProcessExecutionMetrics_ThroughputRatio_ShouldCalculateCorrectly()
        {
            // Arrange & Act & Assert
            var metrics = new ProcessExecutionMetrics
            {
                ExecutionDuration = TimeSpan.FromMilliseconds(500),
                ConfiguredTimeout = TimeSpan.FromMilliseconds(1000)
            };
            Assert.Equal(0.5, metrics.ThroughputRatio);

            metrics.ExecutionDuration = TimeSpan.FromMilliseconds(1500);
            metrics.ConfiguredTimeout = TimeSpan.FromMilliseconds(1000);
            Assert.Equal(1.5, metrics.ThroughputRatio);

            metrics.ConfiguredTimeout = TimeSpan.Zero;
            Assert.Equal(1.0, metrics.ThroughputRatio);
        }

        [Fact]
        public void ProcessMetricsSummary_SuccessRate_ShouldCalculateCorrectly()
        {
            // Arrange & Act & Assert
            var summary = new ProcessMetricsSummary
            {
                TotalExecutions = 10,
                SuccessfulExecutions = 8
            };
            Assert.Equal(80.0, summary.SuccessRate);

            summary.TotalExecutions = 0;
            Assert.Equal(0.0, summary.SuccessRate);
        }

        [Fact]
        public void ProcessMetricsSummary_TimeoutRate_ShouldCalculateCorrectly()
        {
            // Arrange & Act & Assert
            var summary = new ProcessMetricsSummary
            {
                TotalExecutions = 20,
                TimeoutCount = 2
            };
            Assert.Equal(10.0, summary.TimeoutRate);

            summary.TotalExecutions = 0;
            Assert.Equal(0.0, summary.TimeoutRate);
        }

        [Fact]
        public void ProcessMetricsSummary_CancellationRate_ShouldCalculateCorrectly()
        {
            // Arrange & Act & Assert
            var summary = new ProcessMetricsSummary
            {
                TotalExecutions = 15,
                CancellationCount = 3
            };
            Assert.Equal(20.0, summary.CancellationRate);

            summary.TotalExecutions = 0;
            Assert.Equal(0.0, summary.CancellationRate);
        }

        [Fact]
        public void Dispose_ShouldDisposeResources()
        {
            // Arrange
            var processMetrics = new ProcessMetrics(_mockLogger.Object, 100);

            // Act
            processMetrics.Dispose();

            // Assert - No exception should be thrown
            // Additional verification would require checking internal state through reflection
        }
    }
}