using System;
using System.Collections.Generic;
using System.Linq;
using LicenseReleaseService.Interfaces;

namespace LicenseReleaseService.Models
{
    /// <summary>
    /// Represents the result of evaluating a license release strategy
    /// </summary>
    public class StrategyEvaluationResult
    {
        /// <summary>
        /// Gets or sets the strategy that was evaluated
        /// </summary>
        public ILicenseReleaseStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the strategy can handle the request
        /// </summary>
        public bool CanHandle { get; set; }

        /// <summary>
        /// Gets or sets the validation result for the strategy
        /// </summary>
        public StrategyValidationResult ValidationResult { get; set; }

        /// <summary>
        /// Gets or sets the confidence score for this strategy (0.0 to 1.0)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets the priority score for this strategy
        /// </summary>
        public double PriorityScore { get; set; }

        /// <summary>
        /// Gets or sets the estimated execution time
        /// </summary>
        public TimeSpan EstimatedExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the health status of the strategy
        /// </summary>
        public StrategyHealthStatus HealthStatus { get; set; }

        /// <summary>
        /// Gets or sets the reasons why the strategy can or cannot handle the request
        /// </summary>
        public List<string> Reasons { get; set; }

        /// <summary>
        /// Gets or sets the evaluation timestamp
        /// </summary>
        public DateTime EvaluatedAt { get; set; }

        /// <summary>
        /// Gets or sets the request that was evaluated
        /// </summary>
        public ReleaseRequest Request { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the evaluation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Gets a value indicating whether the strategy is recommended for execution
        /// </summary>
        public bool IsRecommended => CanHandle &&
                                   ValidationResult.IsValid &&
                                   ConfidenceScore > 0.5 &&
                                   HealthStatus.IsHealthy;

        /// <summary>
        /// Gets a value indicating whether the strategy has any warnings
        /// </summary>
        public bool HasWarnings => ConfidenceScore < 0.8 ||
                                 !HealthStatus.IsHealthy ||
                                 EstimatedExecutionTime > TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the StrategyEvaluationResult class
        /// </summary>
        public StrategyEvaluationResult()
        {
            ValidationResult = StrategyValidationResult.Success();
            HealthStatus = StrategyHealthStatus.Healthy();
            Reasons = new List<string>();
            Metadata = new Dictionary<string, object>();
            EvaluatedAt = DateTime.UtcNow;
            ConfidenceScore = 0.0;
            PriorityScore = 0.0;
            EstimatedExecutionTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a successful evaluation result
        /// </summary>
        /// <param name="strategy">The strategy that was evaluated</param>
        /// <param name="request">The request that was evaluated</param>
        /// <param name="confidenceScore">The confidence score</param>
        /// <returns>Successful evaluation result</returns>
        public static StrategyEvaluationResult CreateSuccess(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            double confidenceScore)
        {
            return new StrategyEvaluationResult
            {
                Strategy = strategy,
                Request = request,
                CanHandle = true,
                ValidationResult = StrategyValidationResult.Success(),
                ConfidenceScore = confidenceScore,
                PriorityScore = strategy.Priority,
                HealthStatus = strategy.GetHealthStatus(),
                EstimatedExecutionTime = TimeSpan.FromSeconds(30), // Default estimate
                Reasons = new List<string> { "Strategy can handle the request" },
                Metadata = new Dictionary<string, object>
                {
                    { "StrategyPriority", strategy.Priority },
                    { "StrategyEnabled", true }
                }
            };
        }

        /// <summary>
        /// Creates a failed evaluation result
        /// </summary>
        /// <param name="strategy">The strategy that was evaluated</param>
        /// <param name="request">The request that was evaluated</param>
        /// <param name="reason">The reason for failure</param>
        /// <returns>Failed evaluation result</returns>
        public static StrategyEvaluationResult CreateFailure(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            string reason)
        {
            return new StrategyEvaluationResult
            {
                Strategy = strategy,
                Request = request,
                CanHandle = false,
                ValidationResult = StrategyValidationResult.Failure(reason),
                ConfidenceScore = 0.0,
                PriorityScore = strategy.Priority,
                HealthStatus = strategy.GetHealthStatus(),
                EstimatedExecutionTime = TimeSpan.Zero,
                Reasons = new List<string> { reason },
                Metadata = new Dictionary<string, object>
                {
                    { "StrategyPriority", strategy.Priority },
                    { "FailureReason", reason }
                }
            };
        }

        /// <summary>
        /// Creates a validation failure result
        /// </summary>
        /// <param name="strategy">The strategy that was evaluated</param>
        /// <param name="request">The request that was evaluated</param>
        /// <param name="validationResult">The validation result</param>
        /// <returns>Validation failure result</returns>
        public static StrategyEvaluationResult CreateValidationFailure(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            StrategyValidationResult validationResult)
        {
            return new StrategyEvaluationResult
            {
                Strategy = strategy,
                Request = request,
                CanHandle = false,
                ValidationResult = validationResult,
                ConfidenceScore = 0.0,
                PriorityScore = strategy.Priority,
                HealthStatus = strategy.GetHealthStatus(),
                EstimatedExecutionTime = TimeSpan.Zero,
                Reasons = new List<string> { validationResult.ErrorMessage },
                Metadata = new Dictionary<string, object>
                {
                    { "StrategyPriority", strategy.Priority },
                    { "ValidationFailure", true },
                    { "ValidationErrors", validationResult.Errors }
                }
            };
        }

        /// <summary>
        /// Creates a health failure result
        /// </summary>
        /// <param name="strategy">The strategy that was evaluated</param>
        /// <param name="request">The request that was evaluated</param>
        /// <param name="healthStatus">The health status</param>
        /// <returns>Health failure result</returns>
        public static StrategyEvaluationResult CreateHealthFailure(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            StrategyHealthStatus healthStatus)
        {
            return new StrategyEvaluationResult
            {
                Strategy = strategy,
                Request = request,
                CanHandle = false,
                ValidationResult = StrategyValidationResult.Failure(healthStatus.StatusMessage),
                ConfidenceScore = 0.0,
                PriorityScore = strategy.Priority,
                HealthStatus = healthStatus,
                EstimatedExecutionTime = TimeSpan.Zero,
                Reasons = new List<string> { healthStatus.StatusMessage },
                Metadata = new Dictionary<string, object>
                {
                    { "StrategyPriority", strategy.Priority },
                    { "HealthFailure", true },
                    { "ConsecutiveFailures", healthStatus.ConsecutiveFailures }
                }
            };
        }

        /// <summary>
        /// Adds a reason to the evaluation result
        /// </summary>
        /// <param name="reason">The reason to add</param>
        public void AddReason(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Reasons.Add(reason);
            }
        }

        /// <summary>
        /// Adds metadata to the evaluation result
        /// </summary>
        /// <param name="key">The metadata key</param>
        /// <param name="value">The metadata value</param>
        public void AddMetadata(string key, object value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// Updates the confidence score with a multiplier
        /// </summary>
        /// <param name="multiplier">The multiplier to apply (0.0 to 1.0)</param>
        public void AdjustConfidence(double multiplier)
        {
            if (multiplier >= 0.0 && multiplier <= 1.0)
            {
                ConfidenceScore *= multiplier;
            }
        }

        /// <summary>
        /// Returns a string representation of the strategy evaluation result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"StrategyEvaluationResult[{Strategy?.Name ?? "Unknown"}, " +
                   $"CanHandle={CanHandle}, Valid={ValidationResult.IsValid}, " +
                   $"Confidence={ConfidenceScore:F2}, Priority={PriorityScore:F1}, " +
                   $"Recommended={IsRecommended}, Warnings={HasWarnings}]";
        }

        /// <summary>
        /// Gets a detailed summary of the evaluation result
        /// </summary>
        /// <returns>Detailed summary</returns>
        public string GetDetailedSummary()
        {
            var summary = $"Strategy: {Strategy?.Name ?? "Unknown"}\n";
            summary += $"Can Handle: {CanHandle}\n";
            summary += $"Is Valid: {ValidationResult.IsValid}\n";
            summary += $"Is Recommended: {IsRecommended}\n";
            summary += $"Confidence Score: {ConfidenceScore:F2}\n";
            summary += $"Priority Score: {PriorityScore:F1}\n";
            summary += $"Health Status: {HealthStatus.StatusMessage}\n";
            summary += $"Estimated Execution Time: {EstimatedExecutionTime.TotalSeconds:F1}s\n";

            if (Reasons.Count > 0)
            {
                summary += "Reasons:\n";
                foreach (var reason in Reasons)
                {
                    summary += $"  - {reason}\n";
                }
            }

            if (HasWarnings)
            {
                summary += "Warnings:\n";
                if (ConfidenceScore < 0.8)
                    summary += $"  - Low confidence score: {ConfidenceScore:F2}\n";
                if (!HealthStatus.IsHealthy)
                    summary += $"  - Strategy health issues: {HealthStatus.StatusMessage}\n";
                if (EstimatedExecutionTime > TimeSpan.FromMinutes(5))
                    summary += $"  - Long estimated execution time: {EstimatedExecutionTime.TotalMinutes:F1}m\n";
            }

            return summary;
        }

        /// <summary>
        /// Gets a score for sorting strategies by recommendation priority
        /// </summary>
        /// <returns>Sort score (higher is better)</returns>
        public double GetSortScore()
        {
            if (!IsRecommended)
                return -1.0;

            // Combine confidence, priority, and health factors
            double healthFactor = HealthStatus.IsHealthy ? 1.0 : 0.5;
            double timeFactor = 1.0 - Math.Min(EstimatedExecutionTime.TotalMinutes / 10.0, 1.0);

            return (ConfidenceScore * 0.5) + (PriorityScore * 0.3) + (healthFactor * 0.1) + (timeFactor * 0.1);
        }
    }

    /// <summary>
    /// Represents the result of evaluating multiple strategies for a request
    /// </summary>
    public class MultiStrategyEvaluationResult
    {
        /// <summary>
        /// Gets or sets the request that was evaluated
        /// </summary>
        public ReleaseRequest Request { get; set; }

        /// <summary>
        /// Gets or sets the individual strategy evaluation results
        /// </summary>
        public List<StrategyEvaluationResult> StrategyResults { get; set; }

        /// <summary>
        /// Gets or sets the recommended strategy
        /// </summary>
        public StrategyEvaluationResult RecommendedStrategy { get; set; }

        /// <summary>
        /// Gets or sets the evaluation timestamp
        /// </summary>
        public DateTime EvaluatedAt { get; set; }

        /// <summary>
        /// Gets or sets the total evaluation time
        /// </summary>
        public TimeSpan EvaluationTime { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the evaluation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Gets a value indicating whether any strategy can handle the request
        /// </summary>
        public bool HasViableStrategies => StrategyResults?.Any(r => r.IsRecommended) ?? false;

        /// <summary>
        /// Gets the number of strategies that can handle the request
        /// </summary>
        public int ViableStrategyCount => StrategyResults?.Where(r => r.IsRecommended).Count() ?? 0;

        /// <summary>
        /// Gets the average confidence score of all viable strategies
        /// </summary>
        public double AverageConfidenceScore => StrategyResults?
            .Where(r => r.IsRecommended)
            .Select(r => r.ConfidenceScore)
            .DefaultIfEmpty(0.0)
            .Average() ?? 0.0;

        /// <summary>
        /// Initializes a new instance of the MultiStrategyEvaluationResult class
        /// </summary>
        public MultiStrategyEvaluationResult()
        {
            StrategyResults = new List<StrategyEvaluationResult>();
            Metadata = new Dictionary<string, object>();
            EvaluatedAt = DateTime.UtcNow;
            EvaluationTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Gets all recommended strategies sorted by recommendation priority
        /// </summary>
        /// <returns>List of recommended strategies sorted by priority</returns>
        public List<StrategyEvaluationResult> GetRecommendedStrategies()
        {
            return StrategyResults?
                .Where(r => r.IsRecommended)
                .OrderByDescending(r => r.GetSortScore())
                .ToList() ?? new List<StrategyEvaluationResult>();
        }

        /// <summary>
        /// Gets all strategies that have warnings
        /// </summary>
        /// <returns>List of strategies with warnings</returns>
        public List<StrategyEvaluationResult> GetStrategiesWithWarnings()
        {
            return StrategyResults?
                .Where(r => r.HasWarnings)
                .ToList() ?? new List<StrategyEvaluationResult>();
        }

        /// <summary>
        /// Adds a strategy evaluation result to the multi-strategy result
        /// </summary>
        /// <param name="result">The strategy evaluation result to add</param>
        public void AddStrategyResult(StrategyEvaluationResult result)
        {
            StrategyResults.Add(result);

            // Update recommended strategy if this one is better
            if (result.IsRecommended &&
                (RecommendedStrategy == null || result.GetSortScore() > RecommendedStrategy.GetSortScore()))
            {
                RecommendedStrategy = result;
            }
        }

        /// <summary>
        /// Returns a string representation of the multi-strategy evaluation result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"MultiStrategyEvaluationResult[Request={Request?.RequestId ?? "Unknown"}, " +
                   $"Strategies={StrategyResults.Count}, Viable={ViableStrategyCount}, " +
                   $"Recommended={RecommendedStrategy?.Strategy?.Name ?? "None"}, " +
                   $"AvgConfidence={AverageConfidenceScore:F2}]";
        }

        /// <summary>
        /// Gets a detailed summary of the multi-strategy evaluation result
        /// </summary>
        /// <returns>Detailed summary</returns>
        public string GetDetailedSummary()
        {
            var summary = $"Multi-Strategy Evaluation Summary\n";
            summary += $"===================================\n";
            summary += $"Request: {Request?.RequestId ?? "Unknown"}\n";
            summary += $"Total Strategies Evaluated: {StrategyResults.Count}\n";
            summary += $"Viable Strategies: {ViableStrategyCount}\n";
            summary += $"Average Confidence: {AverageConfidenceScore:F2}\n";
            summary += $"Evaluation Time: {EvaluationTime.TotalMilliseconds:F1}ms\n";
            summary += $"Recommended Strategy: {RecommendedStrategy?.Strategy?.Name ?? "None"}\n\n";

            if (RecommendedStrategy != null)
            {
                summary += "Recommended Strategy Details:\n";
                summary += RecommendedStrategy.GetDetailedSummary() + "\n";
            }

            if (GetStrategiesWithWarnings().Count > 0)
            {
                summary += "Strategies with Warnings:\n";
                foreach (var strategy in GetStrategiesWithWarnings())
                {
                    summary += $"- {strategy.Strategy?.Name}: {string.Join(", ", strategy.Reasons)}\n";
                }
                summary += "\n";
            }

            var otherStrategies = StrategyResults.Where(r => !r.IsRecommended).ToList();
            if (otherStrategies.Count > 0)
            {
                summary += "Non-Viable Strategies:\n";
                foreach (var strategy in otherStrategies)
                {
                    summary += $"- {strategy.Strategy?.Name}: {string.Join(", ", strategy.Reasons)}\n";
                }
            }

            return summary;
        }
    }
}