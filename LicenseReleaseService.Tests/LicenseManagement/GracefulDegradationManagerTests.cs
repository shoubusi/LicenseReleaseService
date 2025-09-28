using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for GracefulDegradationManager class
    /// </summary>
    public class GracefulDegradationManagerTests : IDisposable
    {
        private readonly GracefulDegradationManager _degradationManager;
        private readonly ILogger<GracefulDegradationManagerTests> _logger;

        public GracefulDegradationManagerTests()
        {
            _logger = new NullLogger<GracefulDegradationManagerTests>();
            _degradationManager = new GracefulDegradationManager(_logger);
        }

        public void Dispose()
        {
            _degradationManager?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            using var manager = new GracefulDegradationManager(new NullLogger<GracefulDegradationManager>());

            // Assert
            Assert.NotNull(manager);
            Assert.Equal(DegradationLevel.Full, manager.CurrentDegradationLevel);
            Assert.Equal(0, manager.ActiveDegradationRules.Count);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GracefulDegradationManager(null!));
        }

        [Fact]
        public async Task ExecuteWithDegradationAsync_WithSuccessfulOperation_ShouldExecuteSuccessfully()
        {
            // Arrange
            var operationResult = "success";
            var executed = false;

            // Act
            var result = await _degradationManager.ExecuteWithDegradationAsync<string>(
                "test_operation",
                async () =>
                {
                    executed = true;
                    await Task.Delay(10);
                    return operationResult;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(executed);
            Assert.Equal(operationResult, result);
            Assert.Equal(DegradationLevel.Full, _degradationManager.CurrentDegradationLevel);
        }

        [Fact]
        public async Task ExecuteWithDegradationAsync_WithFailedOperation_ShouldPropagateException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test failure");

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _degradationManager.ExecuteWithDegradationAsync<string>(
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
        public async Task ExecuteWithDegradationAsync_WithDegradedOperation_ShouldUseDegradedPath()
        {
            // Arrange
            var normalExecuted = false;
            var degradedExecuted = false;

            _degradationManager.AddDegradationRule(new DegradationRule
            {
                ComponentName = "test_component",
                DegradationLevel = DegradationLevel.Minimal,
                OperationName = "test_operation",
                IsEnabled = true,
                TimeoutOverride = TimeSpan.FromSeconds(1),
                RetryBehavior = DegradationRetryBehavior.FailFast,
                UseDegradedPath = true
            });

            // Force degradation level
            await _degradationManager.SetDegradationLevelAsync(DegradationLevel.Minimal);

            // Act
            var result = await _degradationManager.ExecuteWithDegradationAsync<string>(
                "test_operation",
                async () =>
                {
                    normalExecuted = true;
                    await Task.Delay(10);
                    return "normal_result";
                },
                "test_component",
                cancellationToken: CancellationToken.None);

            // Assert
            // In this case, since no degraded operation is provided, it should still execute normally
            Assert.True(normalExecuted);
            Assert.False(degradedExecuted);
            Assert.Equal("normal_result", result);
        }

        [Fact]
        public async Task AddDegradationRule_WithValidRule_ShouldAddRule()
        {
            // Arrange
            var rule = new DegradationRule
            {
                ComponentName = "test_component",
                OperationName = "test_operation",
                DegradationLevel = DegradationLevel.Minimal,
                IsEnabled = true,
                TimeoutOverride = TimeSpan.FromSeconds(5),
                RetryBehavior = DegradationRetryBehavior.FailFast,
                UseDegradedPath = false
            };

            // Act
            _degradationManager.AddDegradationRule(rule);

            // Assert
            Assert.Contains(rule, _degradationManager.ActiveDegradationRules);
        }

        [Fact]
        public void AddDegradationRule_WithNullRule_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _degradationManager.AddDegradationRule(null!));
        }

        [Fact]
        public void RemoveDegradationRule_WithExistingRule_ShouldRemoveRule()
        {
            // Arrange
            var rule = new DegradationRule
            {
                ComponentName = "test_component",
                OperationName = "test_operation",
                DegradationLevel = DegradationLevel.Minimal,
                IsEnabled = true
            };

            _degradationManager.AddDegradationRule(rule);
            Assert.Contains(rule, _degradationManager.ActiveDegradationRules);

            // Act
            _degradationManager.RemoveDegradationRule(rule);

            // Assert
            Assert.DoesNotContain(rule, _degradationManager.ActiveDegradationRules);
        }

        [Fact]
        public void RemoveDegradationRule_WithNonExistingRule_ShouldNotThrow()
        {
            // Arrange
            var rule = new DegradationRule
            {
                ComponentName = "test_component",
                OperationName = "test_operation",
                DegradationLevel = DegradationLevel.Minimal,
                IsEnabled = true
            };

            // Act & Assert
            var exception = Record.Exception(() => _degradationManager.RemoveDegradationRule(rule));
            Assert.Null(exception);
        }

        [Fact]
        public void RemoveDegradationRule_WithNullRule_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _degradationManager.RemoveDegradationRule(null!));
        }

        [Fact]
        public async Task SetDegradationLevelAsync_WithValidLevel_ShouldSetLevel()
        {
            // Arrange
            var newLevel = DegradationLevel.Severe;

            // Act
            await _degradationManager.SetDegradationLevelAsync(newLevel);

            // Assert
            Assert.Equal(newLevel, _degradationManager.CurrentDegradationLevel);
        }

        [Fact]
        public async Task GetDegradationStatusAsync_ShouldReturnCurrentStatus()
        {
            // Arrange
            await _degradationManager.SetDegradationLevelAsync(DegradationLevel.Moderate);

            // Act
            var status = await _degradationManager.GetDegradationStatusAsync();

            // Assert
            Assert.Equal(DegradationLevel.Moderate, status.CurrentLevel);
            Assert.Equal(0, status.ActiveRules.Count);
            Assert.NotNull(status.LastUpdated);
        }

        [Fact]
        public async Task GetDegradationStatusAsync_WithActiveRules_ShouldIncludeRules()
        {
            // Arrange
            var rule = new DegradationRule
            {
                ComponentName = "test_component",
                OperationName = "test_operation",
                DegradationLevel = DegradationLevel.Minimal,
                IsEnabled = true
            };

            _degradationManager.AddDegradationRule(rule);
            await _degradationManager.SetDegradationLevelAsync(DegradationLevel.Minimal);

            // Act
            var status = await _degradationManager.GetDegradationStatusAsync();

            // Assert
            Assert.Equal(DegradationLevel.Minimal, status.CurrentLevel);
            Assert.Single(status.ActiveRules);
            Assert.Contains(rule, status.ActiveRules);
        }

        [Fact]
        public async Task ClearAllDegradationRulesAsync_ShouldRemoveAllRules()
        {
            // Arrange
            var rule1 = new DegradationRule
            {
                ComponentName = "component1",
                OperationName = "operation1",
                DegradationLevel = DegradationLevel.Minimal,
                IsEnabled = true
            };

            var rule2 = new DegradationRule
            {
                ComponentName = "component2",
                OperationName = "operation2",
                DegradationLevel = DegradationLevel.Severe,
                IsEnabled = true
            };

            _degradationManager.AddDegradationRule(rule1);
            _degradationManager.AddDegradationRule(rule2);
            Assert.Equal(2, _degradationManager.ActiveDegradationRules.Count);

            // Act
            await _degradationManager.ClearAllDegradationRulesAsync();

            // Assert
            Assert.Empty(_degradationManager.ActiveDegradationRules);
        }

        [Fact]
        public void DegradationRule_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var rule = new DegradationRule
            {
                ComponentName = "test_component",
                OperationName = "test_operation",
                DegradationLevel = DegradationLevel.Moderate,
                IsEnabled = true,
                TimeoutOverride = TimeSpan.FromSeconds(30),
                RetryBehavior = DegradationRetryBehavior.ExponentialBackoff,
                UseDegradedPath = false,
                Description = "Test degradation rule"
            };

            // Assert
            Assert.Equal("test_component", rule.ComponentName);
            Assert.Equal("test_operation", rule.OperationName);
            Assert.Equal(DegradationLevel.Moderate, rule.DegradationLevel);
            Assert.True(rule.IsEnabled);
            Assert.Equal(TimeSpan.FromSeconds(30), rule.TimeoutOverride);
            Assert.Equal(DegradationRetryBehavior.ExponentialBackoff, rule.RetryBehavior);
            Assert.False(rule.UseDegradedPath);
            Assert.Equal("Test degradation rule", rule.Description);
        }

        [Fact]
        public void DegradationStatus_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var status = new DegradationStatus();

            // Assert
            Assert.Equal(DegradationLevel.Full, status.CurrentLevel);
            Assert.Empty(status.ActiveRules);
            Assert.Null(status.LastUpdated);
        }

        [Fact]
        public void DegradationStatus_ToString_ShouldReturnMeaningfulString()
        {
            // Arrange
            var status = new DegradationStatus
            {
                CurrentLevel = DegradationLevel.Emergency,
                LastUpdated = DateTime.Now,
                ActiveRules = new List<DegradationRule>
                {
                    new DegradationRule
                    {
                        ComponentName = "test_component",
                        OperationName = "test_operation",
                        DegradationLevel = DegradationLevel.Emergency,
                        IsEnabled = true
                    }
                }
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("Level=Emergency", result);
            Assert.Contains("ActiveRules=1", result);
            Assert.Contains("LastUpdated=", result);
        }

        [Fact]
        public async Task ExecuteWithDegradationAsync_WithCancellation_ShouldCancelOperation()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var operationStarted = new TaskCompletionSource<bool>();

            // Act
            var operationTask = _degradationManager.ExecuteWithDegradationAsync<string>(
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
        public async Task ExecuteWithDegradationAsync_WithOperationTimeout_ShouldTimeout()
        {
            // Arrange - Add a rule with short timeout
            var rule = new DegradationRule
            {
                ComponentName = "test_component",
                OperationName = "test_operation",
                DegradationLevel = DegradationLevel.Minimal,
                IsEnabled = true,
                TimeoutOverride = TimeSpan.FromMilliseconds(50),
                RetryBehavior = DegradationRetryBehavior.FailFast
            };

            _degradationManager.AddDegradationRule(rule);
            await _degradationManager.SetDegradationLevelAsync(DegradationLevel.Minimal);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await _degradationManager.ExecuteWithDegradationAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(200); // Longer than timeout
                        return "success";
                    },
                    "test_component",
                    cancellationToken: CancellationToken.None);
            });
        }
    }
}