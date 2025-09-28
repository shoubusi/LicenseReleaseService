using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.Models
{
    /// <summary>
    /// Comprehensive tests for the StrategyEvaluation class
    /// </summary>
    public class StrategyEvaluationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<StrategyEvaluation>> _mockLogger;
        private readonly List<Mock<ILicenseReleaseStrategy>> _mockStrategies;
        private readonly StrategyEvaluationConfig _config;
        private StrategyEvaluation _evaluation;

        public StrategyEvaluationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<StrategyEvaluation>>();
            _mockStrategies = new List<Mock<ILicenseReleaseStrategy>>();

            // Create several mock strategies
            for (int i = 0; i < 3; i++)
            {
                var mockStrategy = new Mock<ILicenseReleaseStrategy>();
                mockStrategy.Setup(x => x.Name).Returns($"Strategy{i + 1}");
                mockStrategy.Setup(x => x.Description).Returns($"Test strategy {i + 1}");
                mockStrategy.Setup(x => x.Priority).Returns(i + 1);
                mockStrategy.Setup(x => x.GetHealthStatus()).Returns(StrategyHealthStatus.Healthy());
                mockStrategy.Setup(x => x.Validate(It.IsAny<ReleaseRequest>())).Returns(StrategyValidationResult.Success());
                _mockStrategies.Add(mockStrategy);
            }

            _config = new StrategyEvaluationConfig
            {
                MinimumConfidenceThreshold = 0.5,
                EvaluationTimeoutSeconds = 30,
                EnableDetailedLogging = true,
                ParallelEvaluation = true,
                MaxConcurrentEvaluations = 3
            };

            _evaluation = new StrategyEvaluation(
                _mockLogger.Object,
                _mockStrategies.Select(s => s.Object),
                _config);
        }

        public void Dispose()
        {
            _mockLogger.VerifyAll();
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.VerifyAll();
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeEvaluation()
        {
            // Arrange & Act
            var evaluation = new StrategyEvaluation(
                _mockLogger.Object,
                _mockStrategies.Select(s => s.Object),
                _config);

            // Assert
            Assert.NotNull(evaluation);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new StrategyEvaluation(
                null,
                _mockStrategies.Select(s => s.Object),
                _config));
        }

        [Fact]
        public void Constructor_WithNullStrategies_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new StrategyEvaluation(
                _mockLogger.Object,
                null,
                _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new StrategyEvaluation(
                _mockLogger.Object,
                _mockStrategies.Select(s => s.Object),
                null));
        }

        [Fact]
        public void Constructor_WithEmptyStrategies_ShouldInitializeEvaluation()
        {
            // Arrange & Act
            var evaluation = new StrategyEvaluation(
                _mockLogger.Object,
                Enumerable.Empty<ILicenseReleaseStrategy>(),
                _config);

            // Assert
            Assert.NotNull(evaluation);
        }

        #endregion

        #region EvaluateAllStrategiesAsync Tests

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithNullRequest_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _evaluation.EvaluateAllStrategiesAsync(null));
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithNoStrategies_ShouldReturnEmptyResult()
        {
            // Arrange
            var evaluation = new StrategyEvaluation(
                _mockLogger.Object,
                Enumerable.Empty<ILicenseReleaseStrategy>(),
                _config);
            var request = CreateValidReleaseRequest();

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.StrategyResults);
            Assert.Null(result.RecommendedStrategy);
            Assert.False(result.HasViableStrategies);
            Assert.Equal(0, result.ViableStrategyCount);
            Assert.Equal(0.0, result.AverageConfidenceScore);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithAllStrategiesCanHandle_ShouldEvaluateAllStrategies()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup all strategies to handle the request
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Setup(x => x.CanHandle(request)).Returns(true);
            }

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);

            // Assert
            Assert.Equal(_mockStrategies.Count, result.StrategyResults.Count);
            Assert.True(result.HasViableStrategies);
            Assert.Equal(_mockStrategies.Count, result.ViableStrategyCount);
            Assert.NotNull(result.RecommendedStrategy);
            Assert.True(result.AverageConfidenceScore > 0.0);

            // Verify all strategies were evaluated
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Verify(x => x.CanHandle(request), Times.Once);
                mockStrategy.Verify(x => x.Validate(request), Times.Once);
                mockStrategy.Verify(x => x.GetHealthStatus(), Times.Once);
            }
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithNoStrategiesCanHandle_ShouldReturnNoViableStrategies()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup all strategies to not handle the request
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Setup(x => x.CanHandle(request)).Returns(false);
            }

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);

            // Assert
            Assert.Equal(_mockStrategies.Count, result.StrategyResults.Count);
            Assert.False(result.HasViableStrategies);
            Assert.Equal(0, result.ViableStrategyCount);
            Assert.Null(result.RecommendedStrategy);
            Assert.Equal(0.0, result.AverageConfidenceScore);

            // Verify all strategies were evaluated
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Verify(x => x.CanHandle(request), Times.Once);
                mockStrategy.Verify(x => x.Validate(request), Times.Never); // Should not validate if can't handle
                mockStrategy.Verify(x => x.GetHealthStatus(), Times.Never);
            }
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithMixedStrategyHandling_ShouldReturnCorrectViableCount()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup strategies with mixed handling capability
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[1].Setup(x => x.CanHandle(request)).Returns(false);
            _mockStrategies[2].Setup(x => x.CanHandle(request)).Returns(true);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);

            // Assert
            Assert.Equal(_mockStrategies.Count, result.StrategyResults.Count);
            Assert.True(result.HasViableStrategies);
            Assert.Equal(2, result.ViableStrategyCount);
            Assert.NotNull(result.RecommendedStrategy);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithStrategyException_ShouldContinueEvaluation()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup strategies with one throwing an exception
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[1].Setup(x => x.CanHandle(request)).Throws(new InvalidOperationException("Strategy failed"));
            _mockStrategies[2].Setup(x => x.CanHandle(request)).Returns(true);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);

            // Assert
            Assert.Equal(_mockStrategies.Count, result.StrategyResults.Count);
            Assert.True(result.HasViableStrategies);
            Assert.Equal(2, result.ViableStrategyCount); // One strategy failed
            Assert.NotNull(result.RecommendedStrategy);

            // Verify the failed strategy result contains error information
            var failedResult = result.StrategyResults.ElementAt(1);
            Assert.False(failedResult.CanHandle);
            Assert.Contains("Evaluation failed", failedResult.Reasons[0]);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithCancellation_ShouldCancelEvaluation()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var cts = new CancellationTokenSource();

            // Setup strategies with long delays
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Setup(x => x.CanHandle(request))
                    .Returns(() =>
                    {
                        cts.Cancel();
                        return true;
                    });
                mockStrategy.Setup(x => x.Validate(request))
                    .Returns(() =>
                    {
                        Task.Delay(1000, cts.Token).Wait(); // This should be cancelled
                        return StrategyValidationResult.Success();
                    });
            }

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => _evaluation.EvaluateAllStrategiesAsync(request, cts.Token));
        }

        #endregion

        #region EvaluateSingleStrategyAsync Tests (via internal behavior)

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithStrategyCannotHandle_ShouldCreateFailureResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(false);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            Assert.False(strategyResult.CanHandle);
            Assert.Equal("Strategy cannot handle this request", strategyResult.Reasons[0]);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithStrategyValidationFailure_ShouldCreateValidationFailureResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.Validate(request)).Returns(StrategyValidationResult.Failure("Validation failed"));

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            Assert.False(strategyResult.CanHandle);
            Assert.False(strategyResult.ValidationResult.IsValid);
            Assert.Equal("Validation failed", strategyResult.ValidationResult.ErrorMessage);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithStrategyHealthFailure_ShouldCreateHealthFailureResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.GetHealthStatus()).Returns(StrategyHealthStatus.Unhealthy("Health check failed"));

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            Assert.False(strategyResult.CanHandle);
            Assert.False(strategyResult.HealthStatus.IsHealthy);
            Assert.Equal("Health check failed", strategyResult.HealthStatus.StatusMessage);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithValidStrategy_ShouldCreateSuccessResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.Validate(request)).Returns(StrategyValidationResult.Success());
            _mockStrategies[0].Setup(x => x.GetHealthStatus()).Returns(StrategyHealthStatus.Healthy());

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            Assert.True(strategyResult.CanHandle);
            Assert.True(strategyResult.ValidationResult.IsValid);
            Assert.True(strategyResult.HealthStatus.IsHealthy);
            Assert.True(strategyResult.ConfidenceScore > 0.0);
            Assert.True(strategyResult.IsRecommended);
        }

        #endregion

        #region Confidence Calculation Tests

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithHighPriorityStrategy_ShouldHaveHigherPriorityScore()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup strategies with different priorities
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.Priority).Returns(10);

            _mockStrategies[1].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[1].Setup(x => x.Priority).Returns(5);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);

            // Assert
            var firstResult = result.StrategyResults.ElementAt(0);
            var secondResult = result.StrategyResults.ElementAt(1);

            Assert.True(firstResult.PriorityScore > secondResult.PriorityScore);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithCriticalPriorityRequest_ShouldBoostConfidence()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Priority = ReleaseRequestPriority.Critical;

            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.Priority).Returns(5);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            // Critical requests should have higher confidence
            Assert.True(strategyResult.ConfidenceScore > 0.5);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithManualRequest_ShouldBoostConfidence()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Source = "manual";

            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            // Manual requests should have higher confidence
            Assert.True(strategyResult.ConfidenceScore > 0.5);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithForceReleaseRequest_ShouldLowerConfidence()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.ForceRelease = true;

            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var strategyResult = result.StrategyResults.First();

            // Assert
            // Force release requests should have lower confidence
            Assert.True(strategyResult.ConfidenceScore < 0.8);
        }

        [Fact]
        public async Task EvaluateAllStrategiesAsync_WithCustomStrategyWeights_ShouldApplyWeights()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            _config.StrategyWeights["Strategy1"] = 1.5; // Boost weight for first strategy

            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.Name).Returns("Strategy1");
            _mockStrategies[1].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[1].Setup(x => x.Name).Returns("Strategy2");

            // Act
            var result = await evaluation.EvaluateAllStrategiesAsync(request);
            var firstResult = result.StrategyResults.ElementAt(0);
            var secondResult = result.StrategyResults.ElementAt(1);

            // Assert
            // First strategy should have higher confidence due to custom weight
            Assert.True(firstResult.ConfidenceScore > secondResult.ConfidenceScore);
        }

        #endregion

        #region GetBestStrategyAsync Tests

        [Fact]
        public async Task GetBestStrategyAsync_WithViableStrategies_ShouldReturnBestStrategy()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup strategies with different viability
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[0].Setup(x => x.Priority).Returns(5);

            _mockStrategies[1].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[1].Setup(x => x.Priority).Returns(10); // Higher priority

            // Act
            var bestStrategy = await _evaluation.GetBestStrategyAsync(request);

            // Assert
            Assert.NotNull(bestStrategy);
            Assert.Equal("Strategy2", bestStrategy.Name); // Higher priority strategy should be selected
        }

        [Fact]
        public async Task GetBestStrategyAsync_WithNoViableStrategies_ShouldReturnNull()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup all strategies to not handle the request
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Setup(x => x.CanHandle(request)).Returns(false);
            }

            // Act
            var bestStrategy = await _evaluation.GetBestStrategyAsync(request);

            // Assert
            Assert.Null(bestStrategy);
        }

        #endregion

        #region GetViableStrategiesAsync Tests

        [Fact]
        public async Task GetViableStrategiesAsync_WithViableStrategies_ShouldReturnViableStrategies()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup strategies with mixed viability
            _mockStrategies[0].Setup(x => x.CanHandle(request)).Returns(true);
            _mockStrategies[1].Setup(x => x.CanHandle(request)).Returns(false);
            _mockStrategies[2].Setup(x => x.CanHandle(request)).Returns(true);

            // Act
            var viableStrategies = await _evaluation.GetViableStrategiesAsync(request);

            // Assert
            Assert.Equal(2, viableStrategies.Count);
            Assert.Contains(_mockStrategies[0].Object, viableStrategies);
            Assert.Contains(_mockStrategies[2].Object, viableStrategies);
            Assert.DoesNotContain(_mockStrategies[1].Object, viableStrategies);
        }

        [Fact]
        public async Task GetViableStrategiesAsync_WithNoViableStrategies_ShouldReturnEmptyList()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Setup all strategies to not handle the request
            foreach (var mockStrategy in _mockStrategies)
            {
                mockStrategy.Setup(x => x.CanHandle(request)).Returns(false);
            }

            // Act
            var viableStrategies = await _evaluation.GetViableStrategiesAsync(request);

            // Assert
            Assert.Empty(viableStrategies);
        }

        #endregion

        #region Helper Methods

        private ReleaseRequest CreateValidReleaseRequest()
        {
            return new ReleaseRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                Server = "test-server",
                Port = 27000,
                Feature = "test-feature",
                User = "test-user",
                Host = "test-host",
                DisplayName = "Test User",
                Priority = ReleaseRequestPriority.Normal,
                Timeout = TimeSpan.FromSeconds(30),
                MaxRetries = 3,
                Source = "system",
                Initiator = "test-initiator",
                Timestamp = DateTime.Now,
                ForceRelease = false,
                DryRun = false,
                ValidationFlags = ReleaseValidationFlags.Default
            };
        }

        #endregion
    }
}