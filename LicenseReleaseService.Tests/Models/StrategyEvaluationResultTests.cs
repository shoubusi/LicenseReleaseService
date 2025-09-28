using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.Models;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.Models
{
    /// <summary>
    /// Comprehensive tests for the StrategyEvaluationResult and MultiStrategyEvaluationResult models
    /// </summary>
    public class StrategyEvaluationResultTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILicenseReleaseStrategy> _mockStrategy;
        private readonly Mock<ILicenseManager> _mockLicenseManager;

        public StrategyEvaluationResultTests(ITestOutputHelper output)
        {
            _output = output;
            _mockStrategy = new Mock<ILicenseReleaseStrategy>();
            _mockLicenseManager = new Mock<ILicenseManager>();

            // Setup basic strategy mock
            _mockStrategy.Setup(x => x.Name).Returns("TestStrategy");
            _mockStrategy.Setup(x => x.Description).Returns("Test strategy description");
            _mockStrategy.Setup(x => x.Priority).Returns(5);
            _mockStrategy.Setup(x => x.GetHealthStatus()).Returns(StrategyHealthStatus.Healthy());
            _mockStrategy.Setup(x => x.Validate(It.IsAny<ReleaseRequest>())).Returns(StrategyValidationResult.Success());
        }

        #region StrategyEvaluationResult Constructor Tests

        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var result = new StrategyEvaluationResult();

            // Assert
            Assert.Null(result.Strategy);
            Assert.False(result.CanHandle);
            Assert.NotNull(result.ValidationResult);
            Assert.True(result.ValidationResult.IsValid);
            Assert.Equal(0.0, result.ConfidenceScore);
            Assert.Equal(0.0, result.PriorityScore);
            Assert.Equal(TimeSpan.Zero, result.EstimatedExecutionTime);
            Assert.NotNull(result.HealthStatus);
            Assert.True(result.HealthStatus.IsHealthy);
            Assert.NotNull(result.Reasons);
            Assert.Empty(result.Reasons);
            Assert.NotNull(result.Metadata);
            Assert.Empty(result.Metadata);
            Assert.False(result.IsRecommended);
            Assert.False(result.HasWarnings);
        }

        #endregion

        #region StrategyEvaluationResult Properties Tests

        [Fact]
        public void IsRecommended_WithCanHandleAndValidAndHealthyAndHighConfidence_ShouldReturnTrue()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.8,
                HealthStatus = StrategyHealthStatus.Healthy()
            };

            // Act & Assert
            Assert.True(result.IsRecommended);
        }

        [Fact]
        public void IsRecommended_WithCannotHandle_ShouldReturnFalse()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = false,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.8,
                HealthStatus = StrategyHealthStatus.Healthy()
            };

            // Act & Assert
            Assert.False(result.IsRecommended);
        }

        [Fact]
        public void IsRecommended_WithInvalidValidation_ShouldReturnFalse()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Failure("Invalid"),
                ConfidenceScore = 0.8,
                HealthStatus = StrategyHealthStatus.Healthy()
            };

            // Act & Assert
            Assert.False(result.IsRecommended);
        }

        [Fact]
        public void IsRecommended_WithLowConfidence_ShouldReturnFalse()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.4,
                HealthStatus = StrategyHealthStatus.Healthy()
            };

            // Act & Assert
            Assert.False(result.IsRecommended);
        }

        [Fact]
        public void IsRecommended_WithUnhealthyStrategy_ShouldReturnFalse()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.8,
                HealthStatus = StrategyHealthStatus.Unhealthy("Strategy is sick")
            };

            // Act & Assert
            Assert.False(result.IsRecommended);
        }

        [Fact]
        public void HasWarnings_WithLowConfidence_ShouldReturnTrue()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                ConfidenceScore = 0.7
            };

            // Act & Assert
            Assert.True(result.HasWarnings);
        }

        [Fact]
        public void HasWarnings_WithUnhealthyStrategy_ShouldReturnTrue()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                HealthStatus = StrategyHealthStatus.Unhealthy("Strategy is sick")
            };

            // Act & Assert
            Assert.True(result.HasWarnings);
        }

        [Fact]
        public void HasWarnings_WithLongExecutionTime_ShouldReturnTrue()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                EstimatedExecutionTime = TimeSpan.FromMinutes(6)
            };

            // Act & Assert
            Assert.True(result.HasWarnings);
        }

        [Fact]
        public void HasWarnings_WithGoodMetrics_ShouldReturnFalse()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                ConfidenceScore = 0.9,
                HealthStatus = StrategyHealthStatus.Healthy(),
                EstimatedExecutionTime = TimeSpan.FromSeconds(30)
            };

            // Act & Assert
            Assert.False(result.HasWarnings);
        }

        #endregion

        #region StrategyEvaluationResult Factory Methods Tests

        [Fact]
        public void CreateSuccess_WithValidParameters_ShouldCreateSuccessfulResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var confidenceScore = 0.85;

            // Act
            var result = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, confidenceScore);

            // Assert
            Assert.Same(_mockStrategy.Object, result.Strategy);
            Assert.Same(request, result.Request);
            Assert.True(result.CanHandle);
            Assert.True(result.ValidationResult.IsValid);
            Assert.Equal(confidenceScore, result.ConfidenceScore);
            Assert.Equal(_mockStrategy.Object.Priority, result.PriorityScore);
            Assert.True(result.HealthStatus.IsHealthy);
            Assert.True(result.IsRecommended);
            Assert.False(result.HasWarnings);
            Assert.NotEmpty(result.Reasons);
            Assert.Contains("Strategy can handle the request", result.Reasons);
        }

        [Fact]
        public void CreateFailure_WithValidParameters_ShouldCreateFailedResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var reason = "Test failure reason";

            // Act
            var result = StrategyEvaluationResult.CreateFailure(_mockStrategy.Object, request, reason);

            // Assert
            Assert.Same(_mockStrategy.Object, result.Strategy);
            Assert.Same(request, result.Request);
            Assert.False(result.CanHandle);
            Assert.False(result.ValidationResult.IsValid);
            Assert.Equal(0.0, result.ConfidenceScore);
            Assert.Equal(_mockStrategy.Object.Priority, result.PriorityScore);
            Assert.False(result.IsRecommended);
            Assert.NotEmpty(result.Reasons);
            Assert.Contains(reason, result.Reasons);
        }

        [Fact]
        public void CreateValidationFailure_WithValidParameters_ShouldCreateValidationFailureResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var validationResult = StrategyValidationResult.Failure("Validation failed");

            // Act
            var result = StrategyEvaluationResult.CreateValidationFailure(_mockStrategy.Object, request, validationResult);

            // Assert
            Assert.Same(_mockStrategy.Object, result.Strategy);
            Assert.Same(request, result.Request);
            Assert.False(result.CanHandle);
            Assert.Same(validationResult, result.ValidationResult);
            Assert.False(result.IsRecommended);
            Assert.NotEmpty(result.Reasons);
            Assert.Contains(validationResult.ErrorMessage, result.Reasons);
        }

        [Fact]
        public void CreateHealthFailure_WithValidParameters_ShouldCreateHealthFailureResult()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var healthStatus = StrategyHealthStatus.Unhealthy("Health check failed");

            // Act
            var result = StrategyEvaluationResult.CreateHealthFailure(_mockStrategy.Object, request, healthStatus);

            // Assert
            Assert.Same(_mockStrategy.Object, result.Strategy);
            Assert.Same(request, result.Request);
            Assert.False(result.CanHandle);
            Assert.Same(healthStatus, result.HealthStatus);
            Assert.False(result.IsRecommended);
            Assert.NotEmpty(result.Reasons);
            Assert.Contains(healthStatus.StatusMessage, result.Reasons);
        }

        #endregion

        #region StrategyEvaluationResult Utility Methods Tests

        [Fact]
        public void AddReason_WithValidReason_ShouldAddReason()
        {
            // Arrange
            var result = new StrategyEvaluationResult();
            var reason = "Test reason";

            // Act
            result.AddReason(reason);

            // Assert
            Assert.Contains(reason, result.Reasons);
            Assert.Single(result.Reasons);
        }

        [Fact]
        public void AddReason_WithEmptyReason_ShouldNotAddReason()
        {
            // Arrange
            var result = new StrategyEvaluationResult();

            // Act
            result.AddReason("");
            result.AddReason("   ");
            result.AddReason(null);

            // Assert
            Assert.Empty(result.Reasons);
        }

        [Fact]
        public void AddMetadata_WithValidParameters_ShouldAddMetadata()
        {
            // Arrange
            var result = new StrategyEvaluationResult();
            var key = "TestKey";
            var value = "TestValue";

            // Act
            result.AddMetadata(key, value);

            // Assert
            Assert.True(result.Metadata.ContainsKey(key));
            Assert.Equal(value, result.Metadata[key]);
        }

        [Fact]
        public void AdjustConfidence_WithValidMultiplier_ShouldAdjustConfidence()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                ConfidenceScore = 0.8
            };

            // Act
            result.AdjustConfidence(0.5);

            // Assert
            Assert.Equal(0.4, result.ConfidenceScore);
        }

        [Fact]
        public void AdjustConfidence_WithMultiplierBelowZero_ShouldNotAdjustConfidence()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                ConfidenceScore = 0.8
            };

            // Act
            result.AdjustConfidence(-0.5);

            // Assert
            Assert.Equal(0.8, result.ConfidenceScore); // Should remain unchanged
        }

        [Fact]
        public void AdjustConfidence_WithMultiplierAboveOne_ShouldNotAdjustConfidence()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                ConfidenceScore = 0.8
            };

            // Act
            result.AdjustConfidence(1.5);

            // Assert
            Assert.Equal(0.8, result.ConfidenceScore); // Should remain unchanged
        }

        [Fact]
        public void GetSortScore_WithNonRecommendedStrategy_ShouldReturnNegativeOne()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = false,
                ValidationResult = StrategyValidationResult.Failure("Invalid"),
                ConfidenceScore = 0.8,
                HealthStatus = StrategyHealthStatus.Healthy()
            };

            // Act
            var score = result.GetSortScore();

            // Assert
            Assert.Equal(-1.0, score);
        }

        [Fact]
        public void GetSortScore_WithRecommendedStrategy_ShouldReturnPositiveScore()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.8,
                PriorityScore = 5,
                HealthStatus = StrategyHealthStatus.Healthy(),
                EstimatedExecutionTime = TimeSpan.FromSeconds(30)
            };

            // Act
            var score = result.GetSortScore();

            // Assert
            Assert.True(score > 0.0);
            Assert.True(score <= 1.0);
        }

        #endregion

        #region StrategyEvaluationResult ToString Tests

        [Fact]
        public void ToString_WithValidResult_ShouldReturnCorrectString()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                Strategy = _mockStrategy.Object,
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.85,
                PriorityScore = 5,
                HealthStatus = StrategyHealthStatus.Healthy(),
                EstimatedExecutionTime = TimeSpan.FromSeconds(30)
            };

            // Act
            var resultString = result.ToString();

            // Assert
            Assert.Contains("TestStrategy", resultString);
            Assert.Contains("CanHandle=True", resultString);
            Assert.Contains("Valid=True", resultString);
            Assert.Contains("Confidence=0.85", resultString);
            Assert.Contains("Priority=5.0", resultString);
            Assert.Contains("Recommended=True", resultString);
            Assert.Contains("Warnings=False", resultString);
        }

        [Fact]
        public void GetDetailedSummary_WithValidResult_ShouldReturnDetailedSummary()
        {
            // Arrange
            var result = new StrategyEvaluationResult
            {
                Strategy = _mockStrategy.Object,
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = 0.85,
                PriorityScore = 5,
                HealthStatus = StrategyHealthStatus.Healthy(),
                EstimatedExecutionTime = TimeSpan.FromSeconds(30),
                Reasons = new List<string> { "Reason 1", "Reason 2" }
            };

            // Act
            var summary = result.GetDetailedSummary();

            // Assert
            Assert.Contains("Strategy: TestStrategy", summary);
            Assert.Contains("Can Handle: True", summary);
            Assert.Contains("Is Valid: True", summary);
            Assert.Contains("Is Recommended: True", summary);
            Assert.Contains("Confidence Score: 0.85", summary);
            Assert.Contains("Priority Score: 5.0", summary);
            Assert.Contains("Reason 1", summary);
            Assert.Contains("Reason 2", summary);
        }

        #endregion

        #region MultiStrategyEvaluationResult Tests

        [Fact]
        public void MultiStrategyEvaluationResult_Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var result = new MultiStrategyEvaluationResult();

            // Assert
            Assert.Null(result.Request);
            Assert.NotNull(result.StrategyResults);
            Assert.Empty(result.StrategyResults);
            Assert.Null(result.RecommendedStrategy);
            Assert.Equal(TimeSpan.Zero, result.EvaluationTime);
            Assert.NotNull(result.Metadata);
            Assert.Empty(result.Metadata);
            Assert.False(result.HasViableStrategies);
            Assert.Equal(0, result.ViableStrategyCount);
            Assert.Equal(0.0, result.AverageConfidenceScore);
        }

        [Fact]
        public void AddStrategyResult_WithRecommendedStrategy_ShouldUpdateRecommendedStrategy()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var result = new MultiStrategyEvaluationResult { Request = request };
            var strategyResult1 = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.8);
            var strategyResult2 = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.9);

            // Act
            result.AddStrategyResult(strategyResult1);
            result.AddStrategyResult(strategyResult2);

            // Assert
            Assert.Equal(2, result.StrategyResults.Count);
            Assert.Same(strategyResult2, result.RecommendedStrategy); // Higher confidence should be recommended
            Assert.True(result.HasViableStrategies);
            Assert.Equal(2, result.ViableStrategyCount);
            Assert.Equal(0.85, result.AverageConfidenceScore);
        }

        [Fact]
        public void AddStrategyResult_WithNonRecommendedStrategy_ShouldNotUpdateRecommendedStrategy()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var result = new MultiStrategyEvaluationResult { Request = request };
            var strategyResult1 = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.8);
            var strategyResult2 = StrategyEvaluationResult.CreateFailure(_mockStrategy.Object, request, "Cannot handle");

            // Act
            result.AddStrategyResult(strategyResult1);
            result.AddStrategyResult(strategyResult2);

            // Assert
            Assert.Equal(2, result.StrategyResults.Count);
            Assert.Same(strategyResult1, result.RecommendedStrategy);
            Assert.True(result.HasViableStrategies);
            Assert.Equal(1, result.ViableStrategyCount);
            Assert.Equal(0.8, result.AverageConfidenceScore);
        }

        [Fact]
        public void GetRecommendedStrategies_WithMultipleStrategies_ShouldReturnSortedByScore()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var result = new MultiStrategyEvaluationResult { Request = request };

            var lowConfidenceResult = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.6);
            var highConfidenceResult = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.9);
            var mediumConfidenceResult = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.75);

            result.AddStrategyResult(lowConfidenceResult);
            result.AddStrategyResult(highConfidenceResult);
            result.AddStrategyResult(mediumConfidenceResult);

            // Act
            var recommendedStrategies = result.GetRecommendedStrategies();

            // Assert
            Assert.Equal(3, recommendedStrategies.Count);
            Assert.Same(highConfidenceResult, recommendedStrategies[0]);
            Assert.Same(mediumConfidenceResult, recommendedStrategies[1]);
            Assert.Same(lowConfidenceResult, recommendedStrategies[2]);
        }

        [Fact]
        public void GetStrategiesWithWarnings_WithStrategiesHavingWarnings_ShouldReturnCorrectStrategies()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var result = new MultiStrategyEvaluationResult { Request = request };

            var healthyStrategy = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.9);
            var lowConfidenceStrategy = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.7);
            var unhealthyStrategy = StrategyEvaluationResult.CreateHealthFailure(_mockStrategy.Object, request,
                StrategyHealthStatus.Unhealthy("Sick"));

            result.AddStrategyResult(healthyStrategy);
            result.AddStrategyResult(lowConfidenceStrategy);
            result.AddStrategyResult(unhealthyStrategy);

            // Act
            var strategiesWithWarnings = result.GetStrategiesWithWarnings();

            // Assert
            Assert.Equal(2, strategiesWithWarnings.Count);
            Assert.Contains(lowConfidenceStrategy, strategiesWithWarnings);
            Assert.Contains(unhealthyStrategy, strategiesWithWarnings);
            Assert.DoesNotContain(healthyStrategy, strategiesWithWarnings);
        }

        [Fact]
        public void ToString_WithValidResult_ShouldReturnCorrectString()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var result = new MultiStrategyEvaluationResult
            {
                Request = request,
                EvaluationTime = TimeSpan.FromMilliseconds(150)
            };

            var strategyResult = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.8);
            result.AddStrategyResult(strategyResult);

            // Act
            var resultString = result.ToString();

            // Assert
            Assert.Contains(request.RequestId, resultString);
            Assert.Contains("Strategies=1", resultString);
            Assert.Contains("Viable=1", resultString);
            Assert.Contains("Recommended=TestStrategy", resultString);
            Assert.Contains("AvgConfidence=0.80", resultString);
        }

        [Fact]
        public void GetDetailedSummary_WithValidResult_ShouldReturnDetailedSummary()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            var result = new MultiStrategyEvaluationResult
            {
                Request = request,
                EvaluationTime = TimeSpan.FromMilliseconds(150)
            };

            var strategyResult = StrategyEvaluationResult.CreateSuccess(_mockStrategy.Object, request, 0.8);
            result.AddStrategyResult(strategyResult);

            // Act
            var summary = result.GetDetailedSummary();

            // Assert
            Assert.Contains("Multi-Strategy Evaluation Summary", summary);
            Assert.Contains(request.RequestId, summary);
            Assert.Contains("Total Strategies Evaluated: 1", summary);
            Assert.Contains("Viable Strategies: 1", summary);
            Assert.Contains("Average Confidence: 0.80", summary);
            Assert.Contains("Evaluation Time: 150.0ms", summary);
            Assert.Contains("Recommended Strategy: TestStrategy", summary);
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