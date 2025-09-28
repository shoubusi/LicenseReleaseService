using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for FallbackStrategyManager class
    /// </summary>
    public class FallbackStrategyManagerTests : IDisposable
    {
        private readonly FallbackStrategyManager _fallbackManager;
        private readonly ILogger<FallbackStrategyManagerTests> _logger;

        public FallbackStrategyManagerTests()
        {
            _logger = new NullLogger<FallbackStrategyManagerTests>();
            _fallbackManager = new FallbackStrategyManager(_logger);
        }

        public void Dispose()
        {
            _fallbackManager?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            using var manager = new FallbackStrategyManager(new NullLogger<FallbackStrategyManager>());

            // Assert
            Assert.NotNull(manager);
            Assert.Equal(0, manager.ActiveStrategies.Count);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FallbackStrategyManager(null!));
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithSuccessfulPrimaryOperation_ShouldExecuteSuccessfully()
        {
            // Arrange
            var operationResult = "success";
            var primaryExecuted = false;

            // Act
            var result = await _fallbackManager.ExecuteWithFallbackAsync<string>(
                "test_operation",
                async () =>
                {
                    primaryExecuted = true;
                    await Task.Delay(10);
                    return operationResult;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(primaryExecuted);
            Assert.Equal(operationResult, result);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithFailedPrimaryAndNoFallbacks_ShouldPropagateException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Primary operation failed");

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fallbackManager.ExecuteWithFallbackAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(10);
                        throw expectedException;
                    },
                    cancellationToken: CancellationToken.None);
            });

            Assert.Same(expectedException, actualException);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithFailedPrimaryAndSuccessfulFallback_ShouldUseFallback()
        {
            // Arrange
            var primaryExecuted = false;
            var fallbackExecuted = false;
            var primaryException = new InvalidOperationException("Primary operation failed");

            // Add fallback strategy
            _fallbackManager.AddFallbackStrategy(new FallbackStrategy
            {
                Name = "test_fallback",
                OperationName = "test_operation",
                FallbackOperation = async () =>
                {
                    fallbackExecuted = true;
                    await Task.Delay(10);
                    return "fallback_result";
                },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            });

            // Act
            var result = await _fallbackManager.ExecuteWithFallbackAsync<string>(
                "test_operation",
                async () =>
                {
                    primaryExecuted = true;
                    await Task.Delay(10);
                    throw primaryException;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(primaryExecuted);
            Assert.True(fallbackExecuted);
            Assert.Equal("fallback_result", result);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithMultipleFallbacks_ShouldExecuteInOrder()
        {
            // Arrange
            var executionOrder = new List<string>();
            var primaryException = new InvalidOperationException("Primary operation failed");

            // Add multiple fallback strategies
            _fallbackManager.AddFallbackStrategy(new FallbackStrategy
            {
                Name = "fallback1",
                OperationName = "test_operation",
                FallbackOperation = async () =>
                {
                    executionOrder.Add("fallback1");
                    await Task.Delay(10);
                    throw new InvalidOperationException("Fallback1 failed");
                },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            });

            _fallbackManager.AddFallbackStrategy(new FallbackStrategy
            {
                Name = "fallback2",
                OperationName = "test_operation",
                FallbackOperation = async () =>
                {
                    executionOrder.Add("fallback2");
                    await Task.Delay(10);
                    return "fallback2_result";
                },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            });

            // Act
            var result = await _fallbackManager.ExecuteWithFallbackAsync<string>(
                "test_operation",
                async () =>
                {
                    executionOrder.Add("primary");
                    await Task.Delay(10);
                    throw primaryException;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(new[] { "primary", "fallback1", "fallback2" }, executionOrder);
            Assert.Equal("fallback2_result", result);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithParallelExecution_ShouldExecuteInParallel()
        {
            // Arrange
            var executionOrder = new List<string>();
            var primaryException = new InvalidOperationException("Primary operation failed");

            // Add parallel fallback strategy
            _fallbackManager.AddFallbackStrategy(new FallbackStrategy
            {
                Name = "parallel_fallback",
                OperationName = "test_operation",
                FallbackOperation = async () =>
                {
                    executionOrder.Add("parallel_fallback");
                    await Task.Delay(50);
                    return "parallel_result";
                },
                ExecutionOrder = FallbackExecutionOrder.Parallel,
                Timeout = TimeSpan.FromSeconds(1),
                IsEnabled = true
            });

            // Act
            var result = await _fallbackManager.ExecuteWithFallbackAsync<string>(
                "test_operation",
                async () =>
                {
                    executionOrder.Add("primary");
                    await Task.Delay(10);
                    throw primaryException;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal("parallel_result", result);
            Assert.Contains("parallel_fallback", executionOrder);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithAllFallbacksFailed_ShouldThrowLastException()
        {
            // Arrange
            var primaryException = new InvalidOperationException("Primary operation failed");
            var fallbackException = new InvalidOperationException("All fallbacks failed");

            // Add fallback strategy that also fails
            _fallbackManager.AddFallbackStrategy(new FallbackStrategy
            {
                Name = "failing_fallback",
                OperationName = "test_operation",
                FallbackOperation = async () =>
                {
                    await Task.Delay(10);
                    throw fallbackException;
                },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            });

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fallbackManager.ExecuteWithFallbackAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(10);
                        throw primaryException;
                    },
                    cancellationToken: CancellationToken.None);
            });

            Assert.Same(fallbackException, actualException);
        }

        [Fact]
        public void AddFallbackStrategy_WithValidStrategy_ShouldAddStrategy()
        {
            // Arrange
            var strategy = new FallbackStrategy
            {
                Name = "test_strategy",
                OperationName = "test_operation",
                FallbackOperation = async () => { await Task.Delay(10); return "fallback_result"; },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            };

            // Act
            _fallbackManager.AddFallbackStrategy(strategy);

            // Assert
            Assert.Contains(strategy, _fallbackManager.ActiveStrategies);
        }

        [Fact]
        public void AddFallbackStrategy_WithNullStrategy_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _fallbackManager.AddFallbackStrategy(null!));
        }

        [Fact]
        public void RemoveFallbackStrategy_WithExistingStrategy_ShouldRemoveStrategy()
        {
            // Arrange
            var strategy = new FallbackStrategy
            {
                Name = "test_strategy",
                OperationName = "test_operation",
                FallbackOperation = async () => { await Task.Delay(10); return "fallback_result"; },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            };

            _fallbackManager.AddFallbackStrategy(strategy);
            Assert.Contains(strategy, _fallbackManager.ActiveStrategies);

            // Act
            _fallbackManager.RemoveFallbackStrategy(strategy);

            // Assert
            Assert.DoesNotContain(strategy, _fallbackManager.ActiveStrategies);
        }

        [Fact]
        public void RemoveFallbackStrategy_WithNonExistingStrategy_ShouldNotThrow()
        {
            // Arrange
            var strategy = new FallbackStrategy
            {
                Name = "test_strategy",
                OperationName = "test_operation",
                FallbackOperation = async () => { await Task.Delay(10); return "fallback_result"; },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            };

            // Act & Assert
            var exception = Record.Exception(() => _fallbackManager.RemoveFallbackStrategy(strategy));
            Assert.Null(exception);
        }

        [Fact]
        public void RemoveFallbackStrategy_WithNullStrategy_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _fallbackManager.RemoveFallbackStrategy(null!));
        }

        [Fact]
        public async Task GetFallbackStatusAsync_ShouldReturnCurrentStatus()
        {
            // Arrange
            var strategy = new FallbackStrategy
            {
                Name = "test_strategy",
                OperationName = "test_operation",
                FallbackOperation = async () => { await Task.Delay(10); return "result"; },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            };

            _fallbackManager.AddFallbackStrategy(strategy);

            // Act
            var status = await _fallbackManager.GetFallbackStatusAsync();

            // Assert
            Assert.Single(status.ActiveStrategies);
            Assert.Contains(strategy, status.ActiveStrategies);
            Assert.NotNull(status.LastUpdated);
        }

        [Fact]
        public async Task GetFallbackStatusAsync_WithNoStrategies_ShouldReturnEmptyStatus()
        {
            // Arrange - No strategies added

            // Act
            var status = await _fallbackManager.GetFallbackStatusAsync();

            // Assert
            Assert.Empty(status.ActiveStrategies);
            Assert.NotNull(status.LastUpdated);
        }

        [Fact]
        public async Task ClearAllFallbackStrategiesAsync_ShouldRemoveAllStrategies()
        {
            // Arrange
            var strategy1 = new FallbackStrategy
            {
                Name = "strategy1",
                OperationName = "operation1",
                FallbackOperation = async () => { await Task.Delay(10); return "result1"; },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            };

            var strategy2 = new FallbackStrategy
            {
                Name = "strategy2",
                OperationName = "operation2",
                FallbackOperation = async () => { await Task.Delay(10); return "result2"; },
                ExecutionOrder = FallbackExecutionOrder.Parallel,
                Timeout = TimeSpan.FromSeconds(5),
                IsEnabled = true
            };

            _fallbackManager.AddFallbackStrategy(strategy1);
            _fallbackManager.AddFallbackStrategy(strategy2);
            Assert.Equal(2, _fallbackManager.ActiveStrategies.Count);

            // Act
            await _fallbackManager.ClearAllFallbackStrategiesAsync();

            // Assert
            Assert.Empty(_fallbackManager.ActiveStrategies);
        }

        [Fact]
        public void FallbackStrategy_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var strategy = new FallbackStrategy
            {
                Name = "test_strategy",
                OperationName = "test_operation",
                FallbackOperation = async () => { await Task.Delay(10); return "fallback_result"; },
                ExecutionOrder = FallbackExecutionOrder.Priority,
                Timeout = TimeSpan.FromSeconds(30),
                IsEnabled = true,
                Priority = 5,
                Description = "Test fallback strategy"
            };

            // Assert
            Assert.Equal("test_strategy", strategy.Name);
            Assert.Equal("test_operation", strategy.OperationName);
            Assert.NotNull(strategy.FallbackOperation);
            Assert.Equal(FallbackExecutionOrder.Priority, strategy.ExecutionOrder);
            Assert.Equal(TimeSpan.FromSeconds(30), strategy.Timeout);
            Assert.True(strategy.IsEnabled);
            Assert.Equal(5, strategy.Priority);
            Assert.Equal("Test fallback strategy", strategy.Description);
        }

        [Fact]
        public void FallbackResult_WithSuccess_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var result = new FallbackResult<string>
            {
                Success = true,
                Result = "success_result",
                StrategyName = "test_strategy",
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                ErrorMessage = null
            };

            // Assert
            Assert.True(result.Success);
            Assert.Equal("success_result", result.Result);
            Assert.Equal("test_strategy", result.StrategyName);
            Assert.Equal(TimeSpan.FromMilliseconds(100), result.ExecutionTime);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void FallbackResult_WithFailure_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var result = new FallbackResult<string>
            {
                Success = false,
                Result = default,
                StrategyName = "test_strategy",
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                ErrorMessage = "Fallback operation failed"
            };

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Result);
            Assert.Equal("test_strategy", result.StrategyName);
            Assert.Equal(TimeSpan.FromMilliseconds(100), result.ExecutionTime);
            Assert.Equal("Fallback operation failed", result.ErrorMessage);
        }

        [Fact]
        public void FallbackStatus_ToString_ShouldReturnMeaningfulString()
        {
            // Arrange
            var status = new FallbackStatus
            {
                ActiveStrategies = new List<FallbackStrategy>
                {
                    new FallbackStrategy
                    {
                        Name = "test_strategy",
                        OperationName = "test_operation",
                        FallbackOperation = async () => { await Task.Delay(10); return "result"; },
                        ExecutionOrder = FallbackExecutionOrder.Sequential,
                        Timeout = TimeSpan.FromSeconds(5),
                        IsEnabled = true
                    }
                },
                LastUpdated = DateTime.Now
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("ActiveStrategies=1", result);
            Assert.Contains("LastUpdated=", result);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithCancellation_ShouldCancelOperation()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var operationStarted = new TaskCompletionSource<bool>();

            // Act
            var operationTask = _fallbackManager.ExecuteWithFallbackAsync<string>(
                "test_operation",
                async () =>
                {
                    operationStarted.SetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                    return "success";
                },
                cancellationToken: cts.Token);

            // Wait for operation to start
            await operationStarted.Task;
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await operationTask);
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WithFallbackTimeout_ShouldTimeout()
        {
            // Arrange
            var primaryException = new InvalidOperationException("Primary operation failed");

            // Add fallback strategy with short timeout
            _fallbackManager.AddFallbackStrategy(new FallbackStrategy
            {
                Name = "timeout_fallback",
                OperationName = "test_operation",
                FallbackOperation = async () =>
                {
                    await Task.Delay(200); // Longer than timeout
                    return "fallback_result";
                },
                ExecutionOrder = FallbackExecutionOrder.Sequential,
                Timeout = TimeSpan.FromMilliseconds(50),
                IsEnabled = true
            });

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await _fallbackManager.ExecuteWithFallbackAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(10);
                        throw primaryException;
                    },
                    cancellationToken: CancellationToken.None);
            });
        }
    }
}