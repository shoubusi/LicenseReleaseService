using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerCircuitBreaker
    /// </summary>
    public class TimerCircuitBreakerTests : IDisposable
    {
        private readonly Mock<ILogger<TimerCircuitBreaker>> _mockLogger;
        private readonly TimerExecutionOptions _options;
        private readonly TimerCircuitBreaker _circuitBreaker;

        public TimerCircuitBreakerTests()
        {
            _mockLogger = new Mock<ILogger<TimerCircuitBreaker>>();
            _options = new TimerExecutionOptions
            {
                MaxConsecutiveErrors = 3,
                CircuitBreakerCooldown = TimeSpan.FromSeconds(5)
            };
            _circuitBreaker = new TimerCircuitBreaker(_mockLogger.Object, _options);
        }

        public void Dispose()
        {
            _circuitBreaker?.Dispose();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerCircuitBreaker(null, _options));
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerCircuitBreaker(_mockLogger.Object, null));
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Arrange & Act
            var circuitBreaker = new TimerCircuitBreaker(_mockLogger.Object, _options);

            // Assert
            Assert.Equal(CircuitBreakerState.Closed, circuitBreaker.State);
            Assert.Equal(0, circuitBreaker.FailureCount);
            Assert.Equal(3, circuitBreaker.FailureThreshold);
            Assert.Equal(TimeSpan.FromSeconds(5), circuitBreaker.CooldownPeriod);
            Assert.True(circuitBreaker.IsAllowingOperations);
            Assert.False(circuitBreaker.IsInFailureState);
            Assert.Null(circuitBreaker.EstimatedResetTime);
            Assert.Null(circuitBreaker.TimeUntilReset);
        }

        #endregion

        #region RecordSuccess Tests

        [Fact]
        public async Task RecordSuccessAsync_ResetsFailureCount()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);

            // Act
            await _circuitBreaker.RecordSuccessAsync();

            // Assert
            Assert.Equal(0, _circuitBreaker.FailureCount);
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
        }

        [Fact]
        public async Task RecordSuccessAsync_FromHalfOpenState_ClosesCircuit()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Should trip circuit breaker

            // Verify circuit is open
            Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.State);

            // Wait for cooldown period to expire
            await Task.Delay(_options.CircuitBreakerCooldown.Add(TimeSpan.FromMilliseconds(100)));

            // Attempt an operation to move to half-open state
            await Assert.ThrowsAsync<TimerCircuitBreakerOpenException>(() =>
                _circuitBreaker.ExecuteAsync(() => Task.FromResult(1)));

            Assert.Equal(CircuitBreakerState.HalfOpen, _circuitBreaker.State);

            // Act
            await _circuitBreaker.RecordSuccessAsync();

            // Assert
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task RecordSuccessAsync_WithOperationId_PassesThrough()
        {
            // Arrange
            var operationId = Guid.NewGuid();

            // Act
            await _circuitBreaker.RecordSuccessAsync(operationId);

            // Assert - should not throw and complete successfully
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
        }

        #endregion

        #region RecordFailure Tests

        [Fact]
        public async Task RecordFailureAsync_IncrementsFailureCount()
        {
            // Arrange
            var exception = new Exception("Test failure");

            // Act
            await _circuitBreaker.RecordFailureAsync(exception);

            // Assert
            Assert.Equal(1, _circuitBreaker.FailureCount);
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
        }

        [Fact]
        public async Task RecordFailureAsync_ReachesThreshold_TripsCircuit()
        {
            // Arrange
            var exception = new Exception("Test failure");

            // Act
            await _circuitBreaker.RecordFailureAsync(exception); // 1 failure
            await _circuitBreaker.RecordFailureAsync(exception); // 2 failures
            await _circuitBreaker.RecordFailureAsync(exception); // 3 failures - should trip

            // Assert
            Assert.Equal(3, _circuitBreaker.FailureCount);
            Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.State);
            Assert.True(_circuitBreaker.IsInFailureState);
            Assert.NotNull(_circuitBreaker.EstimatedResetTime);
            Assert.True(_circuitBreaker.TimeUntilReset.HasValue);
            Assert.True(_circuitBreaker.TimeUntilReset.Value > TimeSpan.Zero);
        }

        [Fact]
        public async Task RecordFailureAsync_InHalfOpenState_OpensCircuitAndIncreasesTimeout()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Trip circuit

            var originalTimeout = _circuitBreaker.CurrentTimeout;

            // Wait for cooldown and attempt operation to move to half-open
            await Task.Delay(_options.CircuitBreakerCooldown.Add(TimeSpan.FromMilliseconds(100)));
            await Assert.ThrowsAsync<TimerCircuitBreakerOpenException>(() =>
                _circuitBreaker.ExecuteAsync(() => Task.FromResult(1)));

            Assert.Equal(CircuitBreakerState.HalfOpen, _circuitBreaker.State);

            // Act
            await _circuitBreaker.RecordFailureAsync(exception);

            // Assert
            Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.State);
            Assert.True(_circuitBreaker.CurrentTimeout > originalTimeout);
        }

        [Fact]
        public async Task RecordFailureAsync_WithOperationId_PassesThrough()
        {
            // Arrange
            var exception = new Exception("Test failure");
            var operationId = Guid.NewGuid();

            // Act
            await _circuitBreaker.RecordFailureAsync(exception, operationId);

            // Assert
            Assert.Equal(1, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task RecordFailureAsync_WithNullException_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _circuitBreaker.RecordFailureAsync(null));
        }

        #endregion

        #region ExecuteAsync Tests

        [Fact]
        public async Task ExecuteAsync_WhenClosed_ExecutesOperation()
        {
            // Arrange
            var expectedResult = 42;
            var operationId = Guid.NewGuid();

            // Act
            var result = await _circuitBreaker.ExecuteAsync(() => Task.FromResult(expectedResult), operationId);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOpen_ThrowsException()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Trip circuit

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TimerCircuitBreakerOpenException>(() =>
                _circuitBreaker.ExecuteAsync(() => Task.FromResult(42)));

            Assert.Equal("Circuit breaker is open", ex.Message);
            Assert.Equal(3, ex.FailureCount);
            Assert.Equal(3, ex.FailureThreshold);
            Assert.NotNull(ex.EstimatedResetTime);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOpenAndCooldownExpired_AttemptsReset()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Trip circuit

            // Wait for cooldown to expire
            await Task.Delay(_options.CircuitBreakerCooldown.Add(TimeSpan.FromMilliseconds(100)));

            // Act - this should attempt reset and execute operation
            var result = await _circuitBreaker.ExecuteAsync(() => Task.FromResult(42));

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_OperationSucceeds_ResetsFailureCount()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);

            // Act
            var result = await _circuitBreaker.ExecuteAsync(() => Task.FromResult(42));

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(0, _circuitBreaker.FailureCount);
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
        }

        [Fact]
        public async Task ExecuteAsync_OperationFails_IncrementsFailureCount()
        {
            // Arrange
            var exception = new Exception("Operation failure");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _circuitBreaker.ExecuteAsync(() => throw exception));

            Assert.Equal(exception, ex);
            Assert.Equal(1, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _circuitBreaker.ExecuteAsync((Func<Task<int>>)null));
        }

        [Fact]
        public async Task ExecuteAsync_VoidVersion_ExecutesOperation()
        {
            // Arrange
            var executed = false;

            // Act
            await _circuitBreaker.ExecuteAsync(async () =>
            {
                executed = true;
                await Task.Delay(10);
            });

            // Assert
            Assert.True(executed);
        }

        #endregion

        #region ForceOpen Tests

        [Fact]
        public async Task ForceOpenAsync_WhenClosed_OpensCircuit()
        {
            // Arrange
            var reason = "Manual test trip";
            var operationId = Guid.NewGuid();

            // Act
            await _circuitBreaker.ForceOpenAsync(reason, operationId);

            // Assert
            Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.State);
            Assert.Equal(3, _circuitBreaker.FailureCount); // Set to threshold
            Assert.True(_circuitBreaker.IsInFailureState);
            Assert.NotNull(_circuitBreaker.EstimatedResetTime);
        }

        [Fact]
        public async Task ForceOpenAsync_WhenAlreadyOpen_DoesNotChangeState()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Trip circuit

            var originalEstimatedResetTime = _circuitBreaker.EstimatedResetTime;

            // Act
            await _circuitBreaker.ForceOpenAsync("Second force open");

            // Assert
            Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.State);
            Assert.Equal(3, _circuitBreaker.FailureCount);
            Assert.Equal(originalEstimatedResetTime, _circuitBreaker.EstimatedResetTime);
        }

        #endregion

        #region Reset Tests

        [Fact]
        public async Task ResetAsync_WhenOpen_ClosesCircuit()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Trip circuit

            var reason = "Manual reset";
            var operationId = Guid.NewGuid();

            // Act
            await _circuitBreaker.ResetAsync(reason, operationId);

            // Assert
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
            Assert.True(_circuitBreaker.IsAllowingOperations);
            Assert.Null(_circuitBreaker.EstimatedResetTime);
        }

        [Fact]
        public async Task ResetAsync_WhenAlreadyClosed_DoesNotChangeState()
        {
            // Arrange
            var originalFailureCount = _circuitBreaker.FailureCount;

            // Act
            await _circuitBreaker.ResetAsync("Reset when already closed");

            // Assert
            Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        #endregion

        #region GetStatusAsync Tests

        [Fact]
        public async Task GetStatusAsync_ReturnsCompleteStatus()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);

            // Act
            var status = await _circuitBreaker.GetStatusAsync();

            // Assert
            Assert.Equal(CircuitBreakerState.Closed, status.State);
            Assert.Equal(1, status.FailureCount);
            Assert.Equal(3, status.FailureThreshold);
            Assert.NotEqual(default(DateTime), status.LastFailureTime);
            Assert.NotEqual(default(DateTime), status.StateChangeTime);
            Assert.Equal(_options.CircuitBreakerCooldown, status.CurrentTimeout);
            Assert.Equal(_options.CircuitBreakerCooldown, status.CooldownPeriod);
            Assert.Null(status.EstimatedResetTime);
            Assert.Null(status.TimeUntilReset);
            Assert.True(status.IsAllowingOperations);
            Assert.False(status.IsInFailureState);
            Assert.True(status.IsHealthy);
            Assert.Equal(100, status.HealthScore);
            Assert.NotEmpty(status.RecentFailures);
        }

        [Fact]
        public async Task GetStatusAsync_WhenOpen_ReturnsFailureState()
        {
            // Arrange
            var exception = new Exception("Test failure");
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Trip circuit

            // Act
            var status = await _circuitBreaker.GetStatusAsync();

            // Assert
            Assert.Equal(CircuitBreakerState.Open, status.State);
            Assert.Equal(3, status.FailureCount);
            Assert.False(status.IsAllowingOperations);
            Assert.True(status.IsInFailureState);
            Assert.NotNull(status.EstimatedResetTime);
            Assert.NotNull(status.TimeUntilReset);
            Assert.False(status.IsHealthy);
            Assert.True(status.HealthScore < 100);
        }

        #endregion

        #region Timeout Management Tests

        [Fact]
        public async Task CircuitBreaker_ImplementsExponentialBackoff()
        {
            // Arrange
            var exception = new Exception("Test failure");

            // Trip circuit breaker multiple times to test exponential backoff
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // First trip

            var firstTimeout = _circuitBreaker.CurrentTimeout;

            // Wait for cooldown and fail again in half-open state
            await Task.Delay(_options.CircuitBreakerCooldown.Add(TimeSpan.FromMilliseconds(100)));
            await Assert.ThrowsAsync<TimerCircuitBreakerOpenException>(() =>
                _circuitBreaker.ExecuteAsync(() => Task.FromResult(1)));
            await _circuitBreaker.RecordFailureAsync(exception); // Fail in half-open

            var secondTimeout = _circuitBreaker.CurrentTimeout;

            // Assert
            Assert.True(secondTimeout > firstTimeout);
            Assert.Equal(2 * firstTimeout.Ticks, secondTimeout.Ticks);
        }

        [Fact]
        public async Task CircuitBreaker_TimeoutCapsAtMaximum()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                MaxConsecutiveErrors = 3,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(30) // Start with 30 minutes
            };
            var circuitBreaker = new TimerCircuitBreaker(_mockLogger.Object, options);
            var exception = new Exception("Test failure");

            try
            {
                // Trip circuit breaker multiple times
                for (int i = 0; i < 5; i++)
                {
                    await circuitBreaker.RecordFailureAsync(exception);
                    await circuitBreaker.RecordFailureAsync(exception);
                    await circuitBreaker.RecordFailureAsync(exception); // Trip

                    // Wait for cooldown and fail again
                    await Task.Delay(options.CircuitBreakerCooldown.Add(TimeSpan.FromMilliseconds(100)));
                    await Assert.ThrowsAsync<TimerCircuitBreakerOpenException>(() =>
                        circuitBreaker.ExecuteAsync(() => Task.FromResult(1)));
                    await circuitBreaker.RecordFailureAsync(exception); // Fail in half-open
                }

                // Act
                var finalTimeout = circuitBreaker.CurrentTimeout;

                // Assert - should be capped at 1 hour
                Assert.Equal(TimeSpan.FromHours(1), finalTimeout);
            }
            finally
            {
                circuitBreaker?.Dispose();
            }
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task CircuitBreaker_TriggersStateChangedEvent()
        {
            // Arrange
            var eventTriggered = false;
            CircuitBreakerState? oldState = null;
            CircuitBreakerState? newState = null;

            _circuitBreaker.StateChanged += (s, e) =>
            {
                eventTriggered = true;
                oldState = e.OldState;
                newState = e.NewState;
            };

            var exception = new Exception("Test failure");

            // Act
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Should trip

            // Assert
            Assert.True(eventTriggered);
            Assert.Equal(CircuitBreakerState.Closed, oldState);
            Assert.Equal(CircuitBreakerState.Open, newState);
        }

        [Fact]
        public async Task CircuitBreaker_TriggersTrippedEvent()
        {
            // Arrange
            var eventTriggered = false;

            _circuitBreaker.Tripped += (s, e) =>
            {
                eventTriggered = true;
            };

            var exception = new Exception("Test failure");

            // Act
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception); // Should trip

            // Assert
            Assert.True(eventTriggered);
        }

        [Fact]
        public async Task CircuitBreaker_TriggersResetEvent()
        {
            // Arrange
            var eventTriggered = false;

            _circuitBreaker.Reset += (s, e) =>
            {
                eventTriggered = true;
            };

            var exception = new Exception("Test failure");

            // Trip the circuit breaker
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);
            await _circuitBreaker.RecordFailureAsync(exception);

            // Wait for cooldown and succeed to reset
            await Task.Delay(_options.CircuitBreakerCooldown.Add(TimeSpan.FromMilliseconds(100)));
            await _circuitBreaker.ExecuteAsync(() => Task.FromResult(42));

            // Assert
            Assert.True(eventTriggered);
        }

        #endregion

        #region TimerCircuitBreakerStatus Tests

        [Fact]
        public async Task TimerCircuitBreakerStatus_CalculatesHealthScoreCorrectly()
        {
            // Arrange
            var status = new TimerCircuitBreakerStatus
            {
                State = CircuitBreakerState.Closed,
                FailureCount = 0,
                FailureThreshold = 3
            };

            // Act & Assert
            Assert.Equal(100, status.HealthScore);
            Assert.True(status.IsHealthy);
            Assert.False(status.IsDegraded);

            // Modify for degraded state
            status.FailureCount = 1;
            Assert.Equal(90, status.HealthScore);
            Assert.True(status.IsHealthy);
            Assert.True(status.IsDegraded);

            // Modify for failure state
            status.State = CircuitBreakerState.Open;
            status.FailureCount = 3;
            Assert.Equal(40, status.HealthScore);
            Assert.False(status.IsHealthy);
            Assert.True(status.IsDegraded);
        }

        [Fact]
        public void TimerCircuitBreakerStatus_ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var status = new TimerCircuitBreakerStatus
            {
                State = CircuitBreakerState.Open,
                FailureCount = 3,
                FailureThreshold = 3,
                CurrentTimeout = TimeSpan.FromSeconds(30),
                EstimatedResetTime = DateTime.UtcNow.AddMinutes(5)
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("TimerCircuitBreakerStatus", result);
            Assert.Contains("Open", result);
            Assert.Contains("3/3", result);
            Assert.Contains("30.0s", result);
            Assert.Contains("Health=40", result);
        }

        #endregion

        #region TimerCircuitBreakerOpenException Tests

        [Fact]
        public void TimerCircuitBreakerOpenException_Constructor_WithMessage_SetsProperties()
        {
            // Arrange
            var message = "Circuit is open";
            var failureCount = 5;
            var threshold = 3;
            var estimatedResetTime = DateTime.UtcNow.AddMinutes(10);

            // Act
            var ex = new TimerCircuitBreakerOpenException(message, failureCount, threshold, estimatedResetTime);

            // Assert
            Assert.Equal(message, ex.Message);
            Assert.Equal(failureCount, ex.FailureCount);
            Assert.Equal(threshold, ex.FailureThreshold);
            Assert.Equal(estimatedResetTime, ex.EstimatedResetTime);
        }

        [Fact]
        public void TimerCircuitBreakerOpenException_Constructor_WithInnerException_SetsProperties()
        {
            // Arrange
            var message = "Circuit is open";
            var innerException = new Exception("Inner exception");
            var failureCount = 5;
            var threshold = 3;
            var estimatedResetTime = DateTime.UtcNow.AddMinutes(10);

            // Act
            var ex = new TimerCircuitBreakerOpenException(message, innerException, failureCount, threshold, estimatedResetTime);

            // Assert
            Assert.Equal(message, ex.Message);
            Assert.Equal(innerException, ex.InnerException);
            Assert.Equal(failureCount, ex.FailureCount);
            Assert.Equal(threshold, ex.FailureThreshold);
            Assert.Equal(estimatedResetTime, ex.EstimatedResetTime);
        }

        #endregion
    }
}