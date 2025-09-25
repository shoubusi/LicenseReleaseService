using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for CircuitBreaker class
    /// </summary>
    public class CircuitBreakerTests : IDisposable
    {
        private readonly CircuitBreaker _circuitBreaker;
        private readonly ILogger<CircuitBreakerTests> _logger;

        public CircuitBreakerTests()
        {
            _logger = new NullLogger<CircuitBreakerTests>();
            _circuitBreaker = new CircuitBreaker(
                failureThreshold: 3,
                recoveryTimeout: TimeSpan.FromMinutes(1),
                timeout: TimeSpan.FromSeconds(30),
                _logger);
        }

        public void Dispose()
        {
            _circuitBreaker?.Dispose();
        }

        [Fact]
        public async Task Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            using var cb = new CircuitBreaker(
                failureThreshold: 2,
                recoveryTimeout: TimeSpan.FromSeconds(30),
                timeout: TimeSpan.FromSeconds(10),
                new NullLogger<CircuitBreaker>());

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
            Assert.Equal(0, cb.FailureCount);
        }

        [Fact]
        public void Constructor_WithInvalidFailureThreshold_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new CircuitBreaker(
                failureThreshold: 0,
                recoveryTimeout: TimeSpan.FromSeconds(30),
                timeout: TimeSpan.FromSeconds(10),
                new NullLogger<CircuitBreaker>()));
        }

        [Fact]
        public void Constructor_WithInvalidRecoveryTimeout_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new CircuitBreaker(
                failureThreshold: 3,
                recoveryTimeout: TimeSpan.Zero,
                timeout: TimeSpan.FromSeconds(10),
                new NullLogger<CircuitBreaker>()));
        }

        [Fact]
        public void Constructor_WithInvalidTimeout_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new CircuitBreaker(
                failureThreshold: 3,
                recoveryTimeout: TimeSpan.FromSeconds(30),
                timeout: TimeSpan.Zero,
                new NullLogger<CircuitBreaker>()));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CircuitBreaker(
                failureThreshold: 3,
                recoveryTimeout: TimeSpan.FromSeconds(30),
                timeout: TimeSpan.FromSeconds(10),
                logger: null!));
        }

        [Fact]
        public async Task ExecuteAsync_WithSuccessfulOperation_ShouldExecuteSuccessfully()
        {
            // Arrange
            var operationResult = "success";
            var executed = false;

            // Act
            var result = await _circuitBreaker.ExecuteAsync(async () =>
            {
                executed = true;
                await Task.Delay(10);
                return operationResult;
            });

            // Assert
            Assert.True(executed);
            Assert.Equal(operationResult, result);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithFailedOperation_ShouldIncrementFailureCount()
        {
            // Arrange
            var operationResult = "success";

            // Act - First failure
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync<string>(async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Test failure");
                });
            });

            // Assert
            Assert.Equal(1, _circuitBreaker.FailureCount);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, _circuitBreaker.State);
        }

        [Fact]
        public async Task ExecuteAsync_WithMultipleFailures_ShouldOpenCircuit()
        {
            // Arrange
            var failureThreshold = 3;

            // Act & Assert - Failures 1 and 2
            for (int i = 0; i < failureThreshold - 1; i++)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await _circuitBreaker.ExecuteAsync<string>(async () =>
                    {
                        await Task.Delay(10);
                        throw new InvalidOperationException("Test failure");
                    });
                });

                Assert.Equal(i + 1, _circuitBreaker.FailureCount);
                Assert.Equal(CircuitBreaker.CircuitState.Closed, _circuitBreaker.State);
            }

            // Act - Final failure that should open the circuit
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync<string>(async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Test failure");
                });
            });

            // Assert
            Assert.Equal(failureThreshold, _circuitBreaker.FailureCount);
            Assert.Equal(CircuitBreaker.CircuitState.Open, _circuitBreaker.State);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCircuitIsOpen_ShouldThrowCircuitBreakerOpenException()
        {
            // Arrange - Open the circuit by causing failures
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(async () =>
                    {
                        await Task.Delay(10);
                        throw new InvalidOperationException("Test failure");
                    });
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }
            }

            // Act & Assert
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync<string>(async () =>
                {
                    return "should not execute";
                });
            });

            Assert.Equal(CircuitBreaker.CircuitState.Open, _circuitBreaker.State);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCircuitResets_ShouldAllowOperations()
        {
            // Arrange - Create circuit breaker with short recovery timeout
            using var cb = new CircuitBreaker(
                failureThreshold: 2,
                recoveryTimeout: TimeSpan.FromMilliseconds(100),
                timeout: TimeSpan.FromSeconds(10),
                new NullLogger<CircuitBreaker>());

            // Open the circuit
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    await cb.ExecuteAsync<string>(async () =>
                    {
                        await Task.Delay(10);
                        throw new InvalidOperationException("Test failure");
                    });
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }
            }

            Assert.Equal(CircuitBreaker.CircuitState.Open, cb.State);

            // Wait for recovery timeout
            await Task.Delay(150);

            // Act - Should transition to half-open and execute
            var result = await cb.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                return "success after reset";
            });

            // Assert
            Assert.Equal("success after reset", result);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
            Assert.Equal(0, cb.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithOperationTimeout_ShouldTimeoutAndCountAsFailure()
        {
            // Arrange - Create circuit breaker with short timeout
            using var cb = new CircuitBreaker(
                failureThreshold: 1,
                recoveryTimeout: TimeSpan.FromMinutes(1),
                timeout: TimeSpan.FromMilliseconds(50),
                new NullLogger<CircuitBreaker>());

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await cb.ExecuteAsync<string>(async () =>
                {
                    await Task.Delay(200); // Longer than timeout
                    return "success";
                });
            });

            // Assert
            Assert.Equal(1, cb.FailureCount);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
        }

        [Fact]
        public async Task ExecuteAsync_WithCancellation_ShouldCancelAndCountAsFailure()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var operationStarted = new TaskCompletionSource<bool>();

            // Act - Start operation and cancel it
            var operationTask = _circuitBreaker.ExecuteAsync(async () =>
            {
                operationStarted.SetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                return "success";
            });

            // Wait for operation to start
            await operationStarted.Task;
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await operationTask);
            Assert.Equal(1, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ForceOpenAsync_ShouldOpenCircuitImmediately()
        {
            // Arrange
            Assert.Equal(CircuitBreaker.CircuitState.Closed, _circuitBreaker.State);

            // Act
            await _circuitBreaker.ForceOpenAsync();

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Open, _circuitBreaker.State);

            // Verify operations are blocked
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync<string>(async () => "blocked");
            });
        }

        [Fact]
        public async Task ResetAsync_ShouldCloseCircuitImmediately()
        {
            // Arrange - Open the circuit
            await _circuitBreaker.ForceOpenAsync();
            Assert.Equal(CircuitBreaker.CircuitState.Open, _circuitBreaker.State);

            // Act
            await _circuitBreaker.ResetAsync();

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);

            // Verify operations are allowed
            var result = await _circuitBreaker.ExecuteAsync(async () => "success");
            Assert.Equal("success", result);
        }

        [Fact]
        public async Task GetStatusAsync_ShouldReturnCurrentStatus()
        {
            // Arrange
            await _circuitBreaker.ForceOpenAsync();

            // Act
            var status = await _circuitBreaker.GetStatusAsync();

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Open, status.State);
            Assert.Equal(3, status.FailureCount); // Forced open sets count to threshold
            Assert.Equal(3, status.FailureThreshold);
            Assert.True(status.NextResetAttempt.HasValue);
            Assert.False(status.IsAllowingOperations);
            Assert.True(status.IsInFailureState);
        }

        [Fact]
        public async Task GetStatusAsync_WithClosedCircuit_ShouldReturnClosedStatus()
        {
            // Arrange - Circuit should be closed initially
            Assert.Equal(CircuitBreaker.CircuitState.Closed, _circuitBreaker.State);

            // Act
            var status = await _circuitBreaker.GetStatusAsync();

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Closed, status.State);
            Assert.Equal(0, status.FailureCount);
            Assert.False(status.NextResetAttempt.HasValue);
            Assert.True(status.IsAllowingOperations);
            Assert.False(status.IsInFailureState);
        }

        [Fact]
        public async Task ExecuteAsync_WithHalfOpenSuccess_ShouldCloseCircuit()
        {
            // Arrange - Open circuit first
            await _circuitBreaker.ForceOpenAsync();
            Assert.Equal(CircuitBreaker.CircuitState.Open, _circuitBreaker.State);

            // Manually set to half-open (simulating recovery timeout)
            using var cb = new CircuitBreaker(
                failureThreshold: 1,
                recoveryTimeout: TimeSpan.FromMilliseconds(-1), // Immediate recovery
                timeout: TimeSpan.FromSeconds(10),
                new NullLogger<CircuitBreaker>());

            // Cause a failure to open the circuit
            try
            {
                await cb.ExecuteAsync<string>(async () => throw new InvalidOperationException("failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Wait a tiny bit to allow state transition
            await Task.Delay(10);

            // Act - Successful operation should close the circuit
            var result = await cb.ExecuteAsync(async () => "success");

            // Assert
            Assert.Equal("success", result);
            Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
            Assert.Equal(0, cb.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithHalfOpenFailure_ShouldReopenCircuit()
        {
            // Arrange - Create circuit breaker with immediate recovery
            using var cb = new CircuitBreaker(
                failureThreshold: 1,
                recoveryTimeout: TimeSpan.FromMilliseconds(-1), // Immediate recovery
                timeout: TimeSpan.FromSeconds(10),
                new NullLogger<CircuitBreaker>());

            // Cause a failure to open the circuit
            try
            {
                await cb.ExecuteAsync<string>(async () => throw new InvalidOperationException("failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Wait a tiny bit to allow state transition
            await Task.Delay(10);

            // Act - Another failure should re-open the circuit
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await cb.ExecuteAsync<string>(async () => throw new InvalidOperationException("half-open failure"));
            });

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Open, cb.State);
            Assert.Equal(2, cb.FailureCount); // Original + half-open failure
        }
    }

    /// <summary>
    /// Unit tests for CircuitBreakerStatus class
    /// </summary>
    public class CircuitBreakerStatusTests
    {
        [Fact]
        public void Properties_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var status = new CircuitBreakerStatus();

            // Assert
            Assert.Equal(CircuitBreaker.CircuitState.Closed, status.State);
            Assert.Equal(0, status.FailureCount);
            Assert.Equal(0, status.FailureThreshold);
            Assert.Equal(default, status.LastFailureTime);
            Assert.Equal(TimeSpan.Zero, status.RecoveryTimeout);
            Assert.Equal(TimeSpan.Zero, status.Timeout);
            Assert.Null(status.NextResetAttempt);
        }

        [Fact]
        public void IsAllowingOperations_WithClosedState_ShouldReturnTrue()
        {
            // Arrange
            var status = new CircuitBreakerStatus
            {
                State = CircuitBreaker.CircuitState.Closed
            };

            // Act & Assert
            Assert.True(status.IsAllowingOperations);
        }

        [Fact]
        public void IsAllowingOperations_WithHalfOpenState_ShouldReturnTrue()
        {
            // Arrange
            var status = new CircuitBreakerStatus
            {
                State = CircuitBreaker.CircuitState.HalfOpen
            };

            // Act & Assert
            Assert.True(status.IsAllowingOperations);
        }

        [Fact]
        public void IsAllowingOperations_WithOpenState_ShouldReturnFalse()
        {
            // Arrange
            var status = new CircuitBreakerStatus
            {
                State = CircuitBreaker.CircuitState.Open
            };

            // Act & Assert
            Assert.False(status.IsAllowingOperations);
        }

        [Fact]
        public void IsInFailureState_WithOpenState_ShouldReturnTrue()
        {
            // Arrange
            var status = new CircuitBreakerStatus
            {
                State = CircuitBreaker.CircuitState.Open
            };

            // Act & Assert
            Assert.True(status.IsInFailureState);
        }

        [Theory]
        [InlineData(CircuitBreaker.CircuitState.Closed)]
        [InlineData(CircuitBreaker.CircuitState.HalfOpen)]
        public void IsInFailureState_WithNonOpenState_ShouldReturnFalse(CircuitBreaker.CircuitState state)
        {
            // Arrange
            var status = new CircuitBreakerStatus { State = state };

            // Act & Assert
            Assert.False(status.IsInFailureState);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var status = new CircuitBreakerStatus
            {
                State = CircuitBreaker.CircuitState.Open,
                FailureCount = 3,
                FailureThreshold = 3,
                RecoveryTimeout = TimeSpan.FromMinutes(1),
                Timeout = TimeSpan.FromSeconds(30),
                NextResetAttempt = DateTime.Now.AddMinutes(1)
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("State=Open", result);
            Assert.Contains("Failures=3/3", result);
            Assert.Contains("Timeout=30.0s", result);
            Assert.Contains("Recovery=60.0s", result);
            Assert.Contains("NextReset=", result);
        }

        [Fact]
        public void ToString_WithoutNextResetAttempt_ShouldNotIncludeNextReset()
        {
            // Arrange
            var status = new CircuitBreakerStatus
            {
                State = CircuitBreaker.CircuitState.Closed,
                FailureCount = 0,
                FailureThreshold = 3,
                RecoveryTimeout = TimeSpan.FromMinutes(1),
                Timeout = TimeSpan.FromSeconds(30),
                NextResetAttempt = null
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("State=Closed", result);
            Assert.Contains("Failures=0/3", result);
            Assert.DoesNotContain("NextReset=", result);
        }
    }

    /// <summary>
    /// Unit tests for CircuitBreakerOpenException class
    /// </summary>
    public class CircuitBreakerOpenExceptionTests
    {
        [Fact]
        public void Constructor_WithNoParameters_ShouldHaveDefaultMessage()
        {
            // Arrange & Act
            var exception = new CircuitBreakerOpenException();

            // Assert
            Assert.Equal("Circuit breaker is open", exception.Message);
        }

        [Fact]
        public void Constructor_WithMessage_ShouldUseProvidedMessage()
        {
            // Arrange & Act
            var exception = new CircuitBreakerOpenException("Custom message");

            // Assert
            Assert.Equal("Custom message", exception.Message);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner exception");

            // Act
            var exception = new CircuitBreakerOpenException("Custom message", innerException);

            // Assert
            Assert.Equal("Custom message", exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }
    }
}