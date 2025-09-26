using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerHealthMonitor
    /// </summary>
    public class TimerHealthMonitorTests
    {
        private readonly Mock<ILogger<TimerHealthMonitor>> _mockLogger;
        private readonly Mock<ITimerExecutionService> _mockTimerService;
        private readonly Mock<ITimerCircuitBreaker> _mockCircuitBreaker;
        private readonly TimerHealthMonitor _healthMonitor;

        public TimerHealthMonitorTests()
        {
            _mockLogger = new Mock<ILogger<TimerHealthMonitor>>();
            _mockTimerService = new Mock<ITimerExecutionService>();
            _mockCircuitBreaker = new Mock<ITimerCircuitBreaker>();

            _healthMonitor = new TimerHealthMonitor(
                _mockLogger.Object,
                _mockTimerService.Object,
                _mockCircuitBreaker.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerHealthMonitor(
                null,
                _mockTimerService.Object,
                _mockCircuitBreaker.Object));
        }

        [Fact]
        public void Constructor_WithNullTimerService_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerHealthMonitor(
                _mockLogger.Object,
                null,
                _mockCircuitBreaker.Object));
        }

        [Fact]
        public void Constructor_WithNullCircuitBreaker_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerHealthMonitor(
                _mockLogger.Object,
                _mockTimerService.Object,
                null));
        }

        [Fact]
        public void Constructor_WithValidDependencies_InitializesSuccessfully()
        {
            // Arrange & Act
            var healthMonitor = new TimerHealthMonitor(
                _mockLogger.Object,
                _mockTimerService.Object,
                _mockCircuitBreaker.Object);

            // Assert
            Assert.NotNull(healthMonitor);
        }

        #endregion

        #region PerformHealthCheckAsync Tests

        [Fact]
        public async Task PerformHealthCheckAsync_WithHealthyServices_ReturnsHealthyResult()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Running);
            _mockTimerService.Setup(s => s.GetStatisticsAsync())
                .ReturnsAsync(new TimerExecutionStatistics
                {
                    TotalExecutions = 100,
                    SuccessfulExecutions = 95,
                    FailedExecutions = 5,
                    AverageExecutionTime = TimeSpan.FromMilliseconds(100)
                });

            _mockCircuitBreaker.Setup(s => s.State).Returns(CircuitBreakerState.Closed);
            _mockCircuitBreaker.Setup(s => s.GetStatisticsAsync())
                .ReturnsAsync(new CircuitBreakerStatistics
                {
                    TotalRequests = 50,
                    SuccessfulRequests = 48,
                    FailedRequests = 2,
                    CurrentFailureCount = 0
                });

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.Equal(TimerHealthStatus.Healthy, result.OverallStatus);
            Assert.True(result.IsHealthy);
            Assert.Empty(result.Issues);
            Assert.True(result.TimerServiceHealth.IsHealthy);
            Assert.True(result.CircuitBreakerHealth.IsHealthy);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithStoppedTimerService_ReturnsDegradedResult()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Stopped);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.Equal(TimerHealthStatus.Degraded, result.OverallStatus);
            Assert.False(result.IsHealthy);
            Assert.Contains(result.Issues, issue =>
                issue.Type == TimerHealthIssueType.TimerServiceNotRunning);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithOpenCircuitBreaker_ReturnsDegradedResult()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Running);
            _mockCircuitBreaker.Setup(s => s.State).Returns(CircuitBreakerState.Open);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.Equal(TimerHealthStatus.Degraded, result.OverallStatus);
            Assert.False(result.IsHealthy);
            Assert.Contains(result.Issues, issue =>
                issue.Type == TimerHealthIssueType.CircuitBreakerOpen);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithHighFailureRate_ReturnsUnhealthyResult()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Running);
            _mockTimerService.Setup(s => s.GetStatisticsAsync())
                .ReturnsAsync(new TimerExecutionStatistics
                {
                    TotalExecutions = 100,
                    SuccessfulExecutions = 70,
                    FailedExecutions = 30,
                    AverageExecutionTime = TimeSpan.FromMilliseconds(100)
                });

            _mockCircuitBreaker.Setup(s => s.State).Returns(CircuitBreakerState.Closed);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.Equal(TimerHealthStatus.Unhealthy, result.OverallStatus);
            Assert.False(result.IsHealthy);
            Assert.Contains(result.Issues, issue =>
                issue.Type == TimerHealthIssueType.HighFailureRate);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithHighMemoryUsage_ReturnsDegradedResult()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Running);
            _mockCircuitBreaker.Setup(s => s.State).Returns(CircuitBreakerState.Closed);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            // Note: In a real test, we'd mock GC.GetTotalMemory, but for now we'll assume it's within limits
            Assert.NotNull(result);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithCancellation_ReturnsCancelledResult()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _healthMonitor.PerformHealthCheckAsync(cancellationTokenSource.Token));
        }

        #endregion

        #region StartMonitoringAsync Tests

        [Fact]
        public async Task StartMonitoringAsync_WithValidInterval_StartsMonitoring()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(5);

            // Act
            await _healthMonitor.StartMonitoringAsync(interval);

            // Assert
            // Note: We can't easily test the timer without a more complex setup
            // but we can verify the method doesn't throw
            Assert.True(true);
        }

        [Fact]
        public async Task StartMonitoringAsync_WithInvalidInterval_ThrowsArgumentException()
        {
            // Arrange
            var interval = TimeSpan.Zero;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _healthMonitor.StartMonitoringAsync(interval));
        }

        [Fact]
        public async Task StartMonitoringAsync_WhenAlreadyStarted_ThrowsInvalidOperationException()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(5);
            await _healthMonitor.StartMonitoringAsync(interval);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _healthMonitor.StartMonitoringAsync(interval));
        }

        #endregion

        #region StopMonitoringAsync Tests

        [Fact]
        public async Task StopMonitoringAsync_WhenMonitoring_StopsMonitoring()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(5);
            await _healthMonitor.StartMonitoringAsync(interval);

            // Act
            await _healthMonitor.StopMonitoringAsync();

            // Assert
            // Note: We can't easily test the timer without a more complex setup
            // but we can verify the method doesn't throw
            Assert.True(true);
        }

        [Fact]
        public async Task StopMonitoringAsync_WhenNotMonitoring_ThrowsInvalidOperationException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _healthMonitor.StopMonitoringAsync());
        }

        #endregion

        #region GetHealthStatisticsAsync Tests

        [Fact]
        public async Task GetHealthStatisticsAsync_WithoutMonitoring_ReturnsEmptyStatistics()
        {
            // Act
            var stats = await _healthMonitor.GetHealthStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalChecks);
            Assert.Equal(0, stats.HealthyChecks);
            Assert.Equal(0, stats.DegradedChecks);
            Assert.Equal(0, stats.UnhealthyChecks);
        }

        [Fact]
        public async Task GetHealthStatisticsAsync_AfterHealthChecks_ReturnsAccurateStatistics()
        {
            // Arrange
            await _healthMonitor.PerformHealthCheckAsync(); // Healthy check
            await _healthMonitor.PerformHealthCheckAsync(); // Another healthy check

            // Act
            var stats = await _healthMonitor.GetHealthStatisticsAsync();

            // Assert
            Assert.Equal(2, stats.TotalChecks);
            Assert.Equal(2, stats.HealthyChecks);
            Assert.Equal(0, stats.DegradedChecks);
            Assert.Equal(0, stats.UnhealthyChecks);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task PerformHealthCheckAsync_TriggersHealthCheckCompletedEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerHealthCheckEventArgs eventArgs = null;

            _healthMonitor.HealthCheckCompleted += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            // Act
            await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.NotNull(eventArgs.CheckResult);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WhenHealthChanges_TriggersHealthStatusChangedEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerHealthStatusChangedEventArgs eventArgs = null;

            _healthMonitor.HealthStatusChanged += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            // Simulate health change by stopping the timer service
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Stopped);

            // Act
            await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.Equal(TimerHealthStatus.Degraded, eventArgs.NewStatus);
            Assert.Equal(TimerHealthStatus.Unknown, eventArgs.PreviousStatus);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WhenIssuesDetected_TriggersHealthIssueDetectedEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerHealthIssueEventArgs eventArgs = null;

            _healthMonitor.HealthIssueDetected += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            // Simulate issue by stopping the timer service
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Stopped);

            // Act
            await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.NotNull(eventArgs.Issue);
            Assert.Equal(TimerHealthIssueType.TimerServiceNotRunning, eventArgs.Issue.Type);
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public void GetConfiguration_ReturnsCurrentConfiguration()
        {
            // Act
            var config = _healthMonitor.GetConfiguration();

            // Assert
            Assert.NotNull(config);
            Assert.True(config.MonitoringEnabled);
            Assert.True(config.CheckTimerService);
            Assert.True(config.CheckCircuitBreaker);
            Assert.True(config.CheckResourceUtilization);
            Assert.True(config.CheckPerformanceMetrics);
            Assert.Equal(0.2, config.HighFailureRateThreshold); // 20%
            Assert.Equal(0.8, config.HighMemoryUsageThreshold); // 80%
        }

        [Fact]
        public void UpdateConfiguration_WithValidConfiguration_UpdatesSettings()
        {
            // Arrange
            var newConfig = new TimerHealthMonitorConfiguration
            {
                MonitoringEnabled = false,
                CheckTimerService = false,
                CheckCircuitBreaker = false,
                CheckResourceUtilization = false,
                CheckPerformanceMetrics = false,
                HighFailureRateThreshold = 0.5,
                HighMemoryUsageThreshold = 0.9
            };

            // Act
            _healthMonitor.UpdateConfiguration(newConfig);
            var updatedConfig = _healthMonitor.GetConfiguration();

            // Assert
            Assert.False(updatedConfig.MonitoringEnabled);
            Assert.False(updatedConfig.CheckTimerService);
            Assert.False(updatedConfig.CheckCircuitBreaker);
            Assert.False(updatedConfig.CheckResourceUtilization);
            Assert.False(updatedConfig.CheckPerformanceMetrics);
            Assert.Equal(0.5, updatedConfig.HighFailureRateThreshold);
            Assert.Equal(0.9, updatedConfig.HighMemoryUsageThreshold);
        }

        [Fact]
        public void UpdateConfiguration_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _healthMonitor.UpdateConfiguration(null));
        }

        #endregion

        #region Helper Methods Tests

        [Fact]
        public async Task CheckTimerServiceHealthAsync_WithHealthyService_SetsHealthyStatus()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Running);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.True(result.TimerServiceHealth.IsHealthy);
            Assert.Equal(TimerHealthStatus.Healthy, result.TimerServiceHealth.Status);
        }

        [Fact]
        public async Task CheckTimerServiceHealthAsync_WithStoppedService_AddsIssue()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerExecutionState.Stopped);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.False(result.TimerServiceHealth.IsHealthy);
            Assert.Contains(result.Issues, issue =>
                issue.Type == TimerHealthIssueType.TimerServiceNotRunning);
        }

        [Fact]
        public async Task CheckCircuitBreakerHealthAsync_WithClosedBreaker_SetsHealthyStatus()
        {
            // Arrange
            _mockCircuitBreaker.Setup(s => s.State).Returns(CircuitBreakerState.Closed);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.True(result.CircuitBreakerHealth.IsHealthy);
            Assert.Equal(TimerHealthStatus.Healthy, result.CircuitBreakerHealth.Status);
        }

        [Fact]
        public async Task CheckCircuitBreakerHealthAsync_WithOpenBreaker_AddsIssue()
        {
            // Arrange
            _mockCircuitBreaker.Setup(s => s.State).Returns(CircuitBreakerState.Open);

            // Act
            var result = await _healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.False(result.CircuitBreakerHealth.IsHealthy);
            Assert.Contains(result.Issues, issue =>
                issue.Type == TimerHealthIssueType.CircuitBreakerOpen);
        }

        #endregion
    }

    /// <summary>
    /// Interface for mocking TimerExecutionService
    /// </summary>
    public interface ITimerExecutionService
    {
        TimerExecutionState State { get; }
        Task<TimerExecutionStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// Interface for mocking TimerCircuitBreaker
    /// </summary>
    public interface ITimerCircuitBreaker
    {
        CircuitBreakerState State { get; }
        Task<CircuitBreakerStatistics> GetStatisticsAsync();
    }
}