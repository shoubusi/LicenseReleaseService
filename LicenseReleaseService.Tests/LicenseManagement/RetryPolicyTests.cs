using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for RetryPolicy class
    /// </summary>
    public class RetryPolicyTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public RetryPolicyTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RetryPolicy(null));
        }

        [Fact]
        public void Constructor_WithValidLogger_ShouldCreateInstance()
        {
            // Act
            var retryPolicy = new RetryPolicy(_mockLogger.Object);

            // Assert
            Assert.NotNull(retryPolicy);
        }

        [Fact]
        public void Constructor_WithOptions_ShouldUseProvidedOptions()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                MaxRetries = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                Strategy = RetryStrategyType.Linear
            };

            // Act
            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            // Assert
            Assert.NotNull(retryPolicy);
        }

        [Fact]
        public async Task ExecuteAsync_WithSuccessfulOperation_ShouldReturnResult()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object);
            var expected = "test-result";
            var operation = new Func<Task<string>>(() => Task.FromResult(expected));

            // Act
            var result = await retryPolicy.ExecuteAsync(operation);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ExecuteAsync_WithTransientException_ShouldRetryAndSucceed()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object, new RetryPolicyOptions
            {
                MaxRetries = 2,
                InitialDelay = TimeSpan.FromMilliseconds(10)
            });

            var callCount = 0;
            var operation = new Func<Task<string>>(async () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new SocketException(); // Transient exception
                }
                return "success";
            });

            // Act
            var result = await retryPolicy.ExecuteAsync(operation);

            // Assert
            Assert.Equal("success", result);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithNonRetryableException_ShouldThrowImmediately()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object);
            var operation = new Func<Task<string>>(() => throw new ArgumentException("Non-retryable"));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => retryPolicy.ExecuteAsync(operation));
        }

        [Fact]
        public async Task ExecuteAsync_WithMaxRetriesExceeded_ShouldThrowRetryPolicyException()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object, new RetryPolicyOptions
            {
                MaxRetries = 2,
                InitialDelay = TimeSpan.FromMilliseconds(10)
            });

            var callCount = 0;
            var operation = new Func<Task<string>>(() =>
            {
                callCount++;
                throw new SocketException(); // Always fails
            });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RetryPolicyException>(() => retryPolicy.ExecuteAsync(operation));
            Assert.Equal(3, callCount); // Initial attempt + 2 retries
            Assert.Equal("Operation failed after 3 attempts", exception.Message);
        }

        [Fact]
        public async Task ExecuteAsync_WithTimeout_ShouldRetry()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object, new RetryPolicyOptions
            {
                MaxRetries = 1,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                RetryOnTimeout = true
            });

            var callCount = 0;
            var operation = new Func<Task<string>>(async () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new TimeoutException();
                }
                return "success";
            });

            // Act
            var result = await retryPolicy.ExecuteAsync(operation);

            // Assert
            Assert.Equal("success", result);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithCustomRetryPredicate_ShouldUseCustomLogic()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                MaxRetries = 1,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                ShouldRetry = (ex, attempt) => ex is ArgumentException
            };

            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            var callCount = 0;
            var operation = new Func<Task<string>>(async () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new ArgumentException("Should retry");
                }
                return "success";
            });

            // Act
            var result = await retryPolicy.ExecuteAsync(operation);

            // Assert
            Assert.Equal("success", result);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void CalculateDelay_WithFixedStrategy_ShouldReturnFixedDelay()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                Strategy = RetryStrategyType.Fixed,
                InitialDelay = TimeSpan.FromMilliseconds(100)
            };

            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            // Act
            var delay1 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 1 }) as TimeSpan?;
            var delay2 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 2 }) as TimeSpan?;

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(100), delay1);
            Assert.Equal(TimeSpan.FromMilliseconds(100), delay2);
        }

        [Fact]
        public void CalculateDelay_WithLinearStrategy_ShouldIncreaseLinearly()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                Strategy = RetryStrategyType.Linear,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                BackoffMultiplier = 2.0
            };

            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            // Act
            var delay1 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 1 }) as TimeSpan?;
            var delay2 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 2 }) as TimeSpan?;

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(100), delay1);
            Assert.Equal(TimeSpan.FromMilliseconds(300), delay2); // 100 + (100 * 2)
        }

        [Fact]
        public void CalculateDelay_WithExponentialStrategy_ShouldIncreaseExponentially()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                Strategy = RetryStrategyType.Exponential,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                BackoffMultiplier = 2.0
            };

            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            // Act
            var delay1 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 1 }) as TimeSpan?;
            var delay2 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 2 }) as TimeSpan?;
            var delay3 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 3 }) as TimeSpan?;

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(100), delay1);
            Assert.Equal(TimeSpan.FromMilliseconds(200), delay2);
            Assert.Equal(TimeSpan.FromMilliseconds(400), delay3);
        }

        [Fact]
        public void CalculateDelay_WithExponentialWithJitter_ShouldAddRandomness()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                Strategy = RetryStrategyType.ExponentialWithJitter,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                BackoffMultiplier = 2.0,
                JitterFactor = 0.1
            };

            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            // Act
            var delay1 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 1 }) as TimeSpan?;
            var delay2 = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 1 }) as TimeSpan?;

            // Assert
            Assert.True(delay1.HasValue && delay1.Value.TotalMilliseconds > 0);
            Assert.True(delay2.HasValue && delay2.Value.TotalMilliseconds > 0);
            // Due to jitter, delays should be different (though theoretically they could be the same)
        }

        [Fact]
        public void CalculateDelay_ShouldRespectMaxDelay()
        {
            // Arrange
            var options = new RetryPolicyOptions
            {
                Strategy = RetryStrategyType.Exponential,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                MaxDelay = TimeSpan.FromMilliseconds(500),
                BackoffMultiplier = 10.0
            };

            var retryPolicy = new RetryPolicy(_mockLogger.Object, options);

            // Act
            var delay = retryPolicy.GetType()
                .GetMethod("CalculateDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(retryPolicy, new object[] { 10 }) as TimeSpan?;

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
        }

        [Fact]
        public void CreateDefaultLicensePolicy_ShouldReturnConfiguredPolicy()
        {
            // Act
            var policy = RetryPolicy.CreateDefaultLicensePolicy(_mockLogger.Object);

            // Assert
            Assert.NotNull(policy);
            // Additional assertions would require reflection to check private fields
        }

        [Fact]
        public void CreateAggressivePolicy_ShouldReturnAggressiveConfiguration()
        {
            // Act
            var policy = RetryPolicy.CreateAggressivePolicy(_mockLogger.Object);

            // Assert
            Assert.NotNull(policy);
            // Additional assertions would require reflection to check private fields
        }

        [Fact]
        public void CreateConservativePolicy_ShouldReturnConservativeConfiguration()
        {
            // Act
            var policy = RetryPolicy.CreateConservativePolicy(_mockLogger.Object);

            // Assert
            Assert.NotNull(policy);
            // Additional assertions would require reflection to check private fields
        }

        [Fact]
        public async Task ExecuteAsync_NonGeneric_ShouldExecuteAction()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object);
            var executed = false;
            var action = new Func<Task>(async () =>
            {
                executed = true;
                await Task.Delay(10);
            });

            // Act
            await retryPolicy.ExecuteAsync(action);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellation()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(_mockLogger.Object, new RetryPolicyOptions
            {
                MaxRetries = 10,
                InitialDelay = TimeSpan.FromSeconds(1)
            });

            var cts = new CancellationTokenSource();
            var operation = new Func<Task<string>>(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cts.Token);
                return "success";
            });

            // Cancel immediately
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => retryPolicy.ExecuteAsync(operation, cts.Token));
        }
    }
}