using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Unit tests for VersionPerformanceOptimizer
    /// </summary>
    public class VersionPerformanceOptimizerTests : IDisposable
    {
        private readonly Mock<ILogger<VersionPerformanceOptimizer>> _mockLogger;
        private readonly Mock<IVersionResourceManager> _mockResourceManager;
        private readonly ITestOutputHelper _outputHelper;
        private readonly VersionPerformanceOptimizerOptions _options;
        private VersionPerformanceOptimizer _performanceOptimizer;

        public VersionPerformanceOptimizerTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _mockLogger = new Mock<ILogger<VersionPerformanceOptimizer>>();
            _mockResourceManager = new Mock<IVersionResourceManager>();

            _options = new VersionPerformanceOptimizerOptions
            {
                EnableAutomaticOptimization = true,
                OptimizationInterval = TimeSpan.FromMinutes(10),
                PerformanceDataRetentionPeriod = TimeSpan.FromDays(7),
                EnableMetricsCollection = true,
                MetricsCollectionInterval = TimeSpan.FromMinutes(1),
                EnableProfiling = true,
                EnableRecommendations = true,
                MaxOptimizationHistory = 100,
                MinPerformanceThreshold = 0.8,
                MaxOptimizationAttempts = 3,
                EnableAdaptiveOptimization = true,
                EnableMachineLearningOptimization = false
            };

            SetupMockDependencies();
            _performanceOptimizer = new VersionPerformanceOptimizer(
                _mockLogger.Object,
                Options.Create(_options),
                _mockResourceManager.Object);
        }

        private void SetupMockDependencies()
        {
            // Setup resource manager mock for optimization scenarios
            _mockResourceManager.Setup(x => x.GetVersionUtilizationAsync(It.IsAny<string>()))
                .ReturnsAsync((string version) => new VersionResourceUtilization
                {
                    Version = version,
                    MemoryUtilization = version == "2024" ? 1500L * 1024 * 1024 : 500L * 1024 * 1024,
                    CpuUtilization = version == "2024" ? 35 : 15,
                    ActiveAllocations = version == "2024" ? 3 : 1,
                    LastUpdated = DateTime.UtcNow
                });

            _mockResourceManager.Setup(x => x.GetResourceMetricsAsync())
                .ReturnsAsync(new ResourceMetrics
                {
                    VersionCount = 2,
                    TotalAllocations = 4,
                    TotalMemoryUtilization = 2000L * 1024 * 1024,
                    TotalCpuUtilization = 50,
                    Timestamp = DateTime.UtcNow
                });
        }

        [Fact]
        public async Task OptimizeForOperationAsync_ShouldApplyOptimizationSuccessfully()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var result = await _performanceOptimizer.OptimizeForOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.StrategyName);
            Assert.NotEqual(default, result.AppliedAt);
            Assert.True(result.Duration.TotalMilliseconds >= 0);

            // Verify strategy was applied
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("optimization applied")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task OptimizeForOperationAsync_ShouldHandleOptimizationFailure_Gracefully()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Simulate optimization failure by setting up a problematic scenario
            _mockResourceManager.Setup(x => x.GetVersionUtilizationAsync("2024"))
                .ThrowsAsync(new Exception("Resource monitoring failed"));

            // Act
            var result = await _performanceOptimizer.OptimizeForOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("optimization failed", result.ErrorMessage.ToLower());

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("optimization failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetOptimizationRecommendationsAsync_ShouldReturnRecommendations()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var recommendations = await _performanceOptimizer.GetOptimizationRecommendationsAsync("2024");

            // Assert
            Assert.NotNull(recommendations);
            Assert.NotEmpty(recommendations);

            var recommendation = recommendations.First();
            Assert.NotNull(recommendation.Type);
            Assert.NotNull(recommendation.Priority);
            Assert.NotNull(recommendation.Title);
            Assert.NotNull(recommendation.Description);
        }

        [Fact]
        public async Task GetOptimizationRecommendationsAsync_ShouldReturnEmptyList_WhenVersionDoesNotExist()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var recommendations = await _performanceOptimizer.GetOptimizationRecommendationsAsync("NonExistent");

            // Assert
            Assert.NotNull(recommendations);
            Assert.Empty(recommendations);
        }

        [Fact]
        public async Task GetPerformanceProfileAsync_ShouldReturnProfile_WhenProfileExists()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // First, create a profile by optimizing
            await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024");

            // Act
            var profile = await _performanceOptimizer.GetPerformanceProfileAsync("2024");

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("2024", profile.Version);
            Assert.NotEmpty(profile.Metrics);
            Assert.True(profile.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task GetPerformanceProfileAsync_ShouldReturnNull_WhenProfileDoesNotExist()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var profile = await _performanceOptimizer.GetPerformanceProfileAsync("NonExistent");

            // Assert
            Assert.Null(profile);
        }

        [Fact]
        public async Task GetOptimizationHistoryAsync_ShouldReturnHistory()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Apply some optimizations
            await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024");
            await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseQuery, "2023");

            // Act
            var history = await _performanceOptimizer.GetOptimizationHistoryAsync("2024");

            // Assert
            Assert.NotNull(history);
            Assert.NotEmpty(history);

            var optimization = history.First();
            Assert.NotNull(optimization.StrategyName);
            Assert.NotNull(optimization.Type);
            Assert.True(optimization.AppliedAt > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task GetOptimizationHistoryAsync_ShouldReturnEmptyList_WhenVersionHasNoHistory()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var history = await _performanceOptimizer.GetOptimizationHistoryAsync("NonExistent");

            // Assert
            Assert.NotNull(history);
            Assert.Empty(history);
        }

        [Fact]
        public async Task GetOptimizerMetricsAsync_ShouldReturnComprehensiveMetrics()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Apply some optimizations to generate metrics
            await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024");
            await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseQuery, "2023");

            // Act
            var metrics = await _performanceOptimizer.GetOptimizerMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.TotalOptimizations > 0);
            Assert.True(metrics.SuccessfulOptimizations > 0);
            Assert.True(metrics.AverageOptimizationTime.TotalMilliseconds >= 0);
            Assert.True(metrics.Timestamp > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task GetVersionPerformanceTrendsAsync_ShouldReturnTrends()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Apply multiple optimizations to establish trends
            for (int i = 0; i < 5; i++)
            {
                await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024");
                await Task.Delay(10); // Small delay to create different timestamps
            }

            // Act
            var trends = await _performanceOptimizer.GetVersionPerformanceTrendsAsync("2024");

            // Assert
            Assert.NotNull(trends);
            Assert.Equal("2024", trends.Version);
            Assert.NotEmpty(trends.TrendData);
            Assert.True(trends.StartTime > DateTime.UtcNow.AddMinutes(-1));
            Assert.True(trends.EndTime > trends.StartTime);
        }

        [Fact]
        public async Task ApplyOptimizationStrategyAsync_ShouldApplySpecificStrategy()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            var strategy = new OptimizationStrategy
            {
                Name = "TestStrategy",
                Type = OptimizationType.PerformanceTuning,
                Parameters = new Dictionary<string, object>
                {
                    ["Priority"] = "High",
                    ["Target"] = "Memory"
                }
            };

            // Act
            var result = await _performanceOptimizer.ApplyOptimizationStrategyAsync(strategy, "2024");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("TestStrategy", result.StrategyName);
            Assert.Equal(OptimizationType.PerformanceTuning, result.Type);
        }

        [Fact]
        public async Task ApplyOptimizationStrategyAsync_ShouldHandleInvalidStrategy_Gracefully()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            var strategy = new OptimizationStrategy
            {
                Name = "InvalidStrategy",
                Type = OptimizationType.Generic,
                Parameters = null // Invalid parameters
            };

            // Act
            var result = await _performanceOptimizer.ApplyOptimizationStrategyAsync(strategy, "2024");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task GetAvailableStrategiesAsync_ShouldReturnAllAvailableStrategies()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var strategies = await _performanceOptimizer.GetAvailableStrategiesAsync();

            // Assert
            Assert.NotNull(strategies);
            Assert.NotEmpty(strategies);
            Assert.True(strategies.All(s => !string.IsNullOrWhiteSpace(s.Name)));
            Assert.True(strategies.All(s => s.Type != OptimizationType.Generic));
        }

        [Fact]
        public async Task GetOptimizationStatusAsync_ShouldReturnCurrentStatus()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var status = await _performanceOptimizer.GetOptimizationStatusAsync();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.IsEnabled);
            Assert.True(status.IsRunning);
            Assert.NotNull(status.OptimizerHealth);
            Assert.True(status.VersionCount >= 0);
            Assert.True(status.Timestamp > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task AnalyzePerformanceIssuesAsync_ShouldIdentifyIssues()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Simulate a version with performance issues
            _mockResourceManager.Setup(x => x.GetVersionUtilizationAsync("2024"))
                .ReturnsAsync(new VersionResourceUtilization
                {
                    Version = "2024",
                    MemoryUtilization = 1900L * 1024 * 1024, // Near limit
                    CpuUtilization = 45, // Near limit
                    ActiveAllocations = 10,
                    LastUpdated = DateTime.UtcNow
                });

            // Act
            var issues = await _performanceOptimizer.AnalyzePerformanceIssuesAsync("2024");

            // Assert
            Assert.NotNull(issues);
            Assert.NotEmpty(issues);
            Assert.True(issues.All(i => !string.IsNullOrWhiteSpace(i.Description)));
            Assert.True(issues.All(i => i.Severity != OptimizationIssueSeverity.None));
        }

        [Fact]
        public async Task AnalyzePerformanceIssuesAsync_ShouldReturnEmptyList_WhenNoIssuesFound()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Simulate a healthy version
            _mockResourceManager.Setup(x => x.GetVersionUtilizationAsync("2024"))
                .ReturnsAsync(new VersionResourceUtilization
                {
                    Version = "2024",
                    MemoryUtilization = 500L * 1024 * 1024,
                    CpuUtilization = 15,
                    ActiveAllocations = 2,
                    LastUpdated = DateTime.UtcNow
                });

            // Act
            var issues = await _performanceOptimizer.AnalyzePerformanceIssuesAsync("2024");

            // Assert
            Assert.NotNull(issues);
            Assert.Empty(issues);
        }

        [Fact]
        public async Task GetOptimizationTargetsAsync_ShouldReturnTargets()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act
            var targets = await _performanceOptimizer.GetOptimizationTargetsAsync();

            // Assert
            Assert.NotNull(targets);
            Assert.NotEmpty(targets);
            Assert.True(targets.All(t => !string.IsNullOrWhiteSpace(t.Version)));
            Assert.True(targets.All(t => t.Priority != OptimizationPriority.None));
        }

        [Fact]
        public async Task UpdateOptimizationTargetsAsync_ShouldUpdateTargetsSuccessfully()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            var targets = new List<OptimizationTarget>
            {
                new OptimizationTarget
                {
                    Version = "2024",
                    Priority = OptimizationPriority.High,
                    TargetMetrics = new Dictionary<string, double>
                    {
                        ["Memory"] = 1024L * 1024 * 1024,
                        ["Cpu"] = 25
                    }
                }
            };

            // Act
            var result = await _performanceOptimizer.UpdateOptimizationTargetsAsync(targets);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.UpdatedTargets);
        }

        [Fact]
        public async Task ResetOptimizationHistoryAsync_ShouldResetHistorySuccessfully()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Apply some optimizations first
            await _performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024");

            // Act
            var result = await _performanceOptimizer.ResetOptimizationHistoryAsync("2024");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("2024", result.Version);
            Assert.Equal(0, result.RemovedOptimizations);

            // Verify history is cleared
            var history = await _performanceOptimizer.GetOptimizationHistoryAsync("2024");
            Assert.Empty(history);
        }

        [Fact]
        public async Task EnableDisableOptimizationAsync_ShouldControlOptimizationState()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act - disable optimization
            var disableResult = await _performanceOptimizer.DisableOptimizationAsync("2024");
            Assert.True(disableResult.Success);

            var status = await _performanceOptimizer.GetOptimizationStatusAsync();
            Assert.False(status.VersionOptimizationEnabled["2024"]);

            // Act - enable optimization
            var enableResult = await _performanceOptimizer.EnableOptimizationAsync("2024");
            Assert.True(enableResult.Success);

            status = await _performanceOptimizer.GetOptimizationStatusAsync();
            Assert.True(status.VersionOptimizationEnabled["2024"]);
        }

        [Fact]
        public async Task Concurrency_ShouldHandleConcurrentOptimizations()
        {
            // Arrange
            await _performanceOptimizer.InitializeAsync();

            // Act - optimize concurrently
            var tasks = new List<Task<AppliedOptimization>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_performanceOptimizer.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(5, results.Length);
            Assert.True(results.All(r => r.Success));

            // Verify metrics reflect concurrent operations
            var metrics = await _performanceOptimizer.GetOptimizerMetricsAsync();
            Assert.True(metrics.TotalOptimizations >= 5);
        }

        public void Dispose()
        {
            _performanceOptimizer?.Dispose();
        }
    }
}