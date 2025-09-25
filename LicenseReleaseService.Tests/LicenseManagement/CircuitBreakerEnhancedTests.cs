using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for CircuitBreakerEnhanced class
    /// </summary>
    public class CircuitBreakerEnhancedTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public CircuitBreakerEnhancedTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);

            // Assert
            Assert.NotNull(circuitBreaker);
            Assert.Equal(0, circuitBreaker.FailureCount);
            Assert.Equal(0, circuitBreaker.SuccessCount);
            Assert.Equal(0, circuitBreaker.TotalOperations);
            Assert.Equal(0, circuitBreaker.SuccessRate);
            Assert.Equal(100, circuitBreaker.HealthScore);
        }

        [Fact]
        public void Constructor_WithAdvancedParameters_ShouldUseProvidedValues()
        {
            // Arrange
            var handledTypes = new List<Type> { typeof(InvalidOperationException) };

            // Act
            var circuitBreaker = new CircuitBreakerEnhanced(
                5,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMinutes(2),
                handledTypes,
                _mockLogger.Object);

            // Assert
            Assert.NotNull(circuitBreaker);
        }

        [Fact]
        public void Constructor_WithInvalidParameters_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new CircuitBreakerEnhanced(0, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object));
            Assert.Throws<ArgumentException>(() => new CircuitBreakerEnhanced(3, TimeSpan.Zero, TimeSpan.FromSeconds(10), _mockLogger.Object));
            Assert.Throws<ArgumentException>(() => new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.Zero, _mockLogger.Object));
        }

        [Fact]
        public async Task ExecuteAsync_WithSuccessfulOperation_ShouldUpdateMetrics()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var operation = new Func<Task<string>>(() => Task.FromResult("success"));

            // Act
            var result = await circuitBreaker.ExecuteAsync(operation);

            // Assert
            Assert.Equal("success", result);
            Assert.Equal(1, circuitBreaker.SuccessCount);
            Assert.Equal(1, circuitBreaker.TotalOperations);
            Assert.Equal(1.0, circuitBreaker.SuccessRate);
            Assert.Equal(100, circuitBreaker.HealthScore);
            Assert.NotEqual(default(DateTime), circuitBreaker.LastSuccessTime);
        }

        [Fact]
        public async Task ExecuteAsync_WithFailure_ShouldUpdateFailureCount()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(2, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Test failure"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));

            // Assert
            Assert.Equal(1, circuitBreaker.FailureCount);
            Assert.Equal(1, circuitBreaker.TotalOperations);
            Assert.Equal(0, circuitBreaker.SuccessRate);
            Assert.True(circuitBreaker.HealthScore < 100);
        }

        [Fact]
        public async Task ExecuteAsync_WithMultipleFailures_ShouldOpenCircuit()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(2, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Test failure"));

            // Act - first failure
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));
            Assert.Equal(CircuitBreaker.CircuitState.Closed, circuitBreaker.State);

            // Act - second failure should open circuit
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));

            // Assert - circuit should be open
            Assert.Equal(CircuitBreaker.CircuitState.Open, circuitBreaker.State);
            Assert.Equal(2, circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithOpenCircuit_ShouldThrowCircuitBreakerOpenException()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(1, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Test failure"));

            // Open the circuit
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));

            // Act & Assert - subsequent calls should throw CircuitBreakerOpenException
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(() => circuitBreaker.ExecuteAsync(operation));
        }

        [Fact]
        public async Task ExecuteAsync_WithFilteredException_ShouldNotCountAsFailure()
        {
            // Arrange
            var handledTypes = new List<Type> { typeof(SocketException) };
            var circuitBreaker = new CircuitBreakerEnhanced(
                2,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMinutes(2),
                handledTypes,
                _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Not handled"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));

            // Assert - failure count should not increase
            Assert.Equal(0, circuitBreaker.FailureCount);
            Assert.Equal(1, circuitBreaker.TotalOperations);
        }

        [Fact]
        public async Task ExecuteAsync_WithHandledException_ShouldCountAsFailure()
        {
            // Arrange
            var handledTypes = new List<Type> { typeof(SocketException) };
            var circuitBreaker = new CircuitBreakerEnhanced(
                2,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMinutes(2),
                handledTypes,
                _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new SocketException());

            // Act & Assert
            await Assert.ThrowsAsync<SocketException>(() => circuitBreaker.ExecuteAsync(operation));

            // Assert - failure count should increase
            Assert.Equal(1, circuitBreaker.FailureCount);
            Assert.Equal(1, circuitBreaker.TotalOperations);
        }

        [Fact]
        public async Task GetStatusAsync_ShouldReturnEnhancedStatus()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            await circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));

            // Act
            var status = await circuitBreaker.GetStatusAsync();

            // Assert
            Assert.NotNull(status);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, status.State);
            Assert.Equal(0, status.FailureCount);
            Assert.Equal(1, status.SuccessCount);
            Assert.Equal(1, status.TotalOperations);
            Assert.Equal(1.0, status.SuccessRate);
            Assert.Equal(100, status.HealthScore);
            Assert.NotEqual(default(DateTime), status.LastSuccessTime);
        }

        [Fact]
        public void GetFailureRateInWindow_ShouldCalculateCorrectly()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);

            // This test would need to manipulate the internal _recentFailures queue
            // For now, we'll test the basic functionality
            var failureRate = circuitBreaker.GetFailureRateInWindow();

            // Assert
            Assert.Equal(0, failureRate); // No failures yet
        }

        [Fact]
        public async Task PerformHealthCheckAsync_ShouldReturnHealthStatus()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            await circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));

            // Act
            var healthCheck = await circuitBreaker.PerformHealthCheckAsync();

            // Assert
            Assert.NotNull(healthCheck);
            Assert.True(healthCheck.IsHealthy);
            Assert.Equal(100, healthCheck.HealthScore);
            Assert.NotNull(healthCheck.Status);
            Assert.Empty(healthCheck.Recommendations);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithLowHealthScore_ShouldHaveRecommendations()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(1, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Test failure"));

            // Create failures to reduce health score
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));

            // Act
            var healthCheck = await circuitBreaker.PerformHealthCheckAsync();

            // Assert
            Assert.NotNull(healthCheck);
            Assert.True(healthCheck.HealthScore < 100);
            Assert.NotEmpty(healthCheck.Recommendations);
        }

        [Fact]
        public async Task PerformHealthCheckAsync_WithOpenCircuit_ShouldNotBeHealthy()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(1, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Test failure"));

            // Open the circuit
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(operation));

            // Act
            var healthCheck = await circuitBreaker.PerformHealthCheckAsync();

            // Assert
            Assert.NotNull(healthCheck);
            Assert.False(healthCheck.IsHealthy);
            Assert.True(healthCheck.HealthScore < 50);
            Assert.Contains("Circuit is open", healthCheck.Recommendations.First());
        }

        [Fact]
        public void HealthScore_ShouldBeCalculatedCorrectly()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);

            // Initially, health score should be 100
            Assert.Equal(100, circuitBreaker.HealthScore);

            // This test would need to simulate operations to test health score calculation
            // For now, we test the initial state
        }

        [Fact]
        public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellation()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var cts = new CancellationTokenSource();
            var operation = new Func<Task<string>>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                return "success";
            });

            // Cancel immediately
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => circuitBreaker.ExecuteAsync(operation, cts.Token));
        }

        [Fact]
        public async Task ExecuteAsync_WithTimeout_ShouldHandleTimeout()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100), _mockLogger.Object);
            var operation = new Func<Task<string>>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return "success";
            });

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => circuitBreaker.ExecuteAsync(operation));
        }

        [Fact]
        public async Task ResetAsync_ShouldResetAllMetrics()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);
            await circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));

            // Verify initial state
            Assert.Equal(1, circuitBreaker.SuccessCount);
            Assert.Equal(1, circuitBreaker.TotalOperations);

            // Act
            await circuitBreaker.ResetAsync();

            // Assert
            Assert.Equal(0, circuitBreaker.FailureCount);
            Assert.Equal(0, circuitBreaker.SuccessCount);
            Assert.Equal(0, circuitBreaker.TotalOperations);
            Assert.Equal(0, circuitBreaker.SuccessRate);
            Assert.Equal(100, circuitBreaker.HealthScore);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, circuitBreaker.State);
        }

        [Fact]
        public async Task ForceOpenAsync_ShouldOpenCircuitImmediately()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), _mockLogger.Object);

            // Act
            await circuitBreaker.ForceOpenAsync();

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Open, circuitBreaker.State);
        }

        [Fact]
        public async Task ExecuteAsync_AfterRecoveryTimeout_ShouldAttemptRecovery()
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerEnhanced(1, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), _mockLogger.Object);
            var failingOperation = new Func<Task<string>>(() => throw new InvalidOperationException("Test failure"));
            var successOperation = new Func<Task<string>>(() => Task.FromResult("success"));

            // Open the circuit
            await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync(failingOperation));

            // Verify circuit is open
            Assert.Equal(CircuitBreaker.CircuitState.Open, circuitBreaker.State);

            // Wait for recovery timeout
            await Task.Delay(TimeSpan.FromMilliseconds(150));

            // Act - should now attempt recovery (half-open state)
            var result = await circuitBreaker.ExecuteAsync(successOperation);

            // Assert
            Assert.Equal("success", result);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, circuitBreaker.State);
        }
    }
}