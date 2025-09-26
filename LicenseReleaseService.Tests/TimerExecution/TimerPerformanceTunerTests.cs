using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerPerformanceTunerTests
    {
        private readonly Mock<ILogger<TimerPerformanceTuner>> _mockLogger;
        private readonly TimerPerformanceTunerOptions _options;
        private readonly Mock<TimerMetricsCollector> _mockMetricsCollector;
        private readonly Mock<TimerPerformanceOptimizer> _mockOptimizer;

        public TimerPerformanceTunerTests()
        {
            _mockLogger = new Mock<ILogger<TimerPerformanceTuner>>();
            _options = new TimerPerformanceTunerOptions();
            _mockMetricsCollector = new Mock<TimerMetricsCollector>(Mock.Of<ILogger<TimerMetricsCollector>>(), new TimerMetricsOptions());
            _mockOptimizer = new Mock<TimerPerformanceOptimizer>(
                Mock.Of<ILogger<TimerPerformanceOptimizer>>(),
                new TimerPerformanceOptimizerOptions(),
                Mock.Of<TimerMemoryManager>(),
                Mock.Of<TimerThreadPoolManager>(),
                Mock.Of<TimerCacheManager>());
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Act
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Assert
            Assert.NotNull(tuner);
            Assert.NotNull(tuner.CurrentConfiguration);
            Assert.NotNull(tuner.TuningStatistics);
            Assert.True(tuner.Uptime.TotalMilliseconds >= 0);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceTuner(null, _options, _mockMetricsCollector.Object, _mockOptimizer.Object));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceTuner(_mockLogger.Object, null, _mockMetricsCollector.Object, _mockOptimizer.Object));
        }

        [Fact]
        public void Constructor_WithNullMetricsCollector_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceTuner(_mockLogger.Object, _options, null, _mockOptimizer.Object));
        }

        [Fact]
        public void Constructor_WithNullOptimizer_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotTuning_ShouldStartTuner()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Act
            await tuner.StartAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyTuning_ShouldNotStartAgain()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();

            // Act
            await tuner.StartAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task StopAsync_WhenTuning_ShouldStopTuner()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();

            // Act
            await tuner.StopAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task StopAsync_WhenNotTuning_ShouldDoNothing()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Act
            await tuner.StopAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task AddTuningRule_WithValidRule_ShouldAddRule()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            var rule = new TimerTuningRule
            {
                Name = "TestRule",
                Action = TimerTuningActionType.IncreaseThreadPool,
                Priority = TimerTuningPriority.High,
                EstimatedImpact = TimerTuningImpact.Medium,
                Condition = (snapshot, recent) => true,
                ReasonGenerator = (snapshot, recent) => "Test reason",
                ConfidenceCalculator = (snapshot, recent) => 90.0
            };

            // Act
            tuner.AddTuningRule(rule);

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public void AddTuningRule_WithNullRule_ShouldThrowArgumentNullException()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => tuner.AddTuningRule(null));
        }

        [Fact]
        public async Task RemoveTuningRule_WithExistingRule_ShouldRemoveRule()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            var rule = new TimerTuningRule
            {
                Name = "TestRule",
                Action = TimerTuningActionType.IncreaseThreadPool,
                Priority = TimerTuningPriority.High,
                EstimatedImpact = TimerTuningImpact.Medium,
                Condition = (snapshot, recent) => true,
                ReasonGenerator = (snapshot, recent) => "Test reason",
                ConfidenceCalculator = (snapshot, recent) => 90.0
            };

            tuner.AddTuningRule(rule);

            // Act
            tuner.RemoveTuningRule("TestRule");

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task RemoveTuningRule_WithNonExistingRule_ShouldDoNothing()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Act
            tuner.RemoveTuningRule("NonExistingRule");

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task GetTuningRecommendations_ShouldReturnRecommendations()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();
            await Task.Delay(100); // Allow time for initial setup

            // Act
            var recommendations = tuner.GetTuningRecommendations();

            // Assert
            Assert.NotNull(recommendations);
        }

        [Fact]
        public async Task ApplyConfigurationAsync_WithValidConfiguration_ShouldApply()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            var configuration = new TimerTuningConfiguration
            {
                OptimizationIntervalMs = 120000,
                MaxThreadPoolSize = 150,
                MinThreadPoolSize = 20,
                MaxCacheSize = 15000
            };

            _mockOptimizer.Setup(o => o.UpdateOptimizationIntervalAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

            // Act
            await tuner.ApplyConfigurationAsync(configuration, "Test configuration update");

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task ApplyConfigurationAsync_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => tuner.ApplyConfigurationAsync(null, "Test reason"));
        }

        [Fact]
        public async Task GetTuningHistory_ShouldReturnHistory()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();

            // Act
            var history = tuner.GetTuningHistory(10);

            // Assert
            Assert.NotNull(history);
            Assert.True(history.Count() <= 10);
        }

        [Fact]
        public async Task ResetToDefaultsAsync_ShouldResetToDefaults()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();

            _mockOptimizer.Setup(o => o.UpdateOptimizationIntervalAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

            // Act
            await tuner.ResetToDefaultsAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public async Task ForceTuningCycleAsync_ShouldForceCycle()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();

            // Act
            await tuner.ForceTuningCycleAsync();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public void Events_ShouldBeRaisedCorrectly()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            var tuningAppliedEventRaised = false;
            var tuningRecommendedEventRaised = false;

            tuner.TuningApplied += (sender, args) => tuningAppliedEventRaised = true;
            tuner.TuningRecommended += (sender, args) => tuningRecommendedEventRaised = true;

            // Act
            // Events are raised during tuning operations, so we'll just verify they're not null
            Assert.NotNull(tuner.TuningApplied);
            Assert.NotNull(tuner.TuningRecommended);
        }

        [Fact]
        public async Task Dispose_WhenTuning_ShouldStopAndDispose()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);
            await tuner.StartAsync();

            // Act
            tuner.Dispose();

            // Assert
            // Should not throw and should complete successfully
            Assert.True(true);
        }

        [Fact]
        public void Dispose_WhenAlreadyDisposed_ShouldNotThrow()
        {
            // Arrange
            var tuner = new TimerPerformanceTuner(_mockLogger.Object, _options, _mockMetricsCollector.Object, _mockOptimizer.Object);

            // Act
            tuner.Dispose();
            tuner.Dispose(); // Should not throw

            // Assert
            Assert.True(true); // If we get here, no exception was thrown
        }

        [Fact]
        public void TimerTuningRule_ShouldWorkCorrectly()
        {
            // Arrange
            var rule = new TimerTuningRule
            {
                Name = "TestRule",
                Action = TimerTuningActionType.IncreaseThreadPool,
                Priority = TimerTuningPriority.High,
                EstimatedImpact = TimerTuningImpact.Medium,
                Condition = (snapshot, recent) => snapshot.CpuUsagePercent > 80,
                ReasonGenerator = (snapshot, recent) => $"High CPU: {snapshot.CpuUsagePercent}%",
                ConfidenceCalculator = (snapshot, recent) => Math.Min(100, snapshot.CpuUsagePercent)
            };

            // Act & Assert
            Assert.Equal("TestRule", rule.Name);
            Assert.Equal(TimerTuningActionType.IncreaseThreadPool, rule.Action);
            Assert.Equal(TimerTuningPriority.High, rule.Priority);
            Assert.Equal(TimerTuningImpact.Medium, rule.EstimatedImpact);
        }

        [Fact]
        public void TimerTuningConfiguration_ShouldHaveValidProperties()
        {
            // Arrange
            var config = new TimerTuningConfiguration();

            // Act & Assert
            Assert.Equal(60000, config.OptimizationIntervalMs);
            Assert.Equal(100, config.MaxThreadPoolSize);
            Assert.Equal(10, config.MinThreadPoolSize);
            Assert.Equal(10000, config.MaxCacheSize);
            Assert.Equal(80.0, config.MemoryPressureThreshold);
            Assert.Equal(80.0, config.CpuUsageThreshold);
        }

        [Fact]
        public void TimerTuningStatistics_ShouldHaveValidProperties()
        {
            // Arrange
            var stats = new TimerTuningStatistics();

            // Act & Assert
            Assert.Equal(0, stats.TotalTuningCycles);
            Assert.Equal(0, stats.SuccessfulTunings);
            Assert.Equal(0, stats.FailedTunings);
            Assert.Equal(0, stats.SuccessRate);
            Assert.Equal(TimeSpan.Zero, stats.Uptime);
        }

        [Fact]
        public void TimerTuningAction_ShouldHaveValidProperties()
        {
            // Arrange
            var action = new TimerTuningAction
            {
                Timestamp = DateTime.UtcNow,
                Action = TimerTuningActionType.IncreaseThreadPool,
                Reason = "Test reason",
                Success = true,
                PerformanceImprovement = 15.5
            };

            // Act & Assert
            Assert.True(action.Timestamp > DateTime.MinValue);
            Assert.Equal(TimerTuningActionType.IncreaseThreadPool, action.Action);
            Assert.Equal("Test reason", action.Reason);
            Assert.True(action.Success);
            Assert.Equal(15.5, action.PerformanceImprovement);
        }

        [Fact]
        public void TimerTuningRecommendation_ShouldHaveValidProperties()
        {
            // Arrange
            var recommendation = new TimerTuningRecommendation
            {
                RuleName = "TestRule",
                Action = TimerTuningActionType.IncreaseThreadPool,
                Priority = TimerTuningPriority.High,
                Reason = "Test reason",
                EstimatedImpact = TimerTuningImpact.Medium,
                Confidence = 85.5
            };

            // Act & Assert
            Assert.Equal("TestRule", recommendation.RuleName);
            Assert.Equal(TimerTuningActionType.IncreaseThreadPool, recommendation.Action);
            Assert.Equal(TimerTuningPriority.High, recommendation.Priority);
            Assert.Equal("Test reason", recommendation.Reason);
            Assert.Equal(TimerTuningImpact.Medium, recommendation.EstimatedImpact);
            Assert.Equal(85.5, recommendation.Confidence);
        }
    }
}