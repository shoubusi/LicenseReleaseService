using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.Models;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Models
{
    /// <summary>
    /// Service responsible for evaluating and selecting the best license release strategy
    /// </summary>
    public class StrategyEvaluation
    {
        private readonly ILogger<StrategyEvaluation> _logger;
        private readonly IEnumerable<ILicenseReleaseStrategy> _strategies;
        private readonly StrategyEvaluationConfig _config;

        /// <summary>
        /// Initializes a new instance of the StrategyEvaluation class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="strategies">Available license release strategies</param>
        /// <param name="config">Evaluation configuration</param>
        public StrategyEvaluation(
            ILogger<StrategyEvaluation> logger,
            IEnumerable<ILicenseReleaseStrategy> strategies,
            StrategyEvaluationConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Evaluates all available strategies for the given request
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Multi-strategy evaluation result</returns>
        public async Task<MultiStrategyEvaluationResult> EvaluateAllStrategiesAsync(
            ReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Evaluating {StrategyCount} strategies for request {RequestId}",
                _strategies.Count(), request.RequestId);

            var startTime = DateTime.UtcNow;
            var result = new MultiStrategyEvaluationResult
            {
                Request = request,
                EvaluatedAt = startTime
            };

            try
            {
                // Evaluate each strategy
                var evaluationTasks = _strategies.Select(strategy =>
                    EvaluateSingleStrategyAsync(strategy, request, cancellationToken));

                var evaluationResults = await Task.WhenAll(evaluationTasks);

                // Add all results to the multi-strategy result
                foreach (var evaluationResult in evaluationResults)
                {
                    result.AddStrategyResult(evaluationResult);
                }

                // Calculate evaluation time
                result.EvaluationTime = DateTime.UtcNow - startTime;

                // Log evaluation summary
                LogEvaluationSummary(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating strategies for request {RequestId}", request.RequestId);

                // Return partial result if possible
                result.EvaluationTime = DateTime.UtcNow - startTime;
                result.Metadata["Error"] = ex.Message;

                return result;
            }
        }

        /// <summary>
        /// Evaluates a single strategy for the given request
        /// </summary>
        /// <param name="strategy">The strategy to evaluate</param>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Strategy evaluation result</returns>
        private async Task<StrategyEvaluationResult> EvaluateSingleStrategyAsync(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            CancellationToken cancellationToken)
        {
            var strategyStartTime = DateTime.UtcNow;

            try
            {
                _logger.LogDebug("Evaluating strategy {StrategyName} for request {RequestId}",
                    strategy.Name, request.RequestId);

                // Check if strategy can handle the request
                if (!strategy.CanHandle(request))
                {
                    var result = StrategyEvaluationResult.CreateFailure(
                        strategy, request, "Strategy cannot handle this request");
                    result.EvaluatedAt = strategyStartTime;
                    result.EstimatedExecutionTime = DateTime.UtcNow - strategyStartTime;
                    return result;
                }

                // Validate the request
                var validationResult = strategy.Validate(request);
                if (!validationResult.IsValid)
                {
                    var result = StrategyEvaluationResult.CreateValidationFailure(
                        strategy, request, validationResult);
                    result.EvaluatedAt = strategyStartTime;
                    result.EstimatedExecutionTime = DateTime.UtcNow - strategyStartTime;
                    return result;
                }

                // Check strategy health
                var healthStatus = strategy.GetHealthStatus();
                if (!healthStatus.IsHealthy)
                {
                    var result = StrategyEvaluationResult.CreateHealthFailure(
                        strategy, request, healthStatus);
                    result.EvaluatedAt = strategyStartTime;
                    result.EstimatedExecutionTime = DateTime.UtcNow - strategyStartTime;
                    return result;
                }

                // Calculate confidence score
                var confidenceScore = await CalculateConfidenceScoreAsync(strategy, request, cancellationToken);

                // Create successful evaluation result
                var evaluationResult = StrategyEvaluationResult.CreateSuccess(strategy, request, confidenceScore);
                evaluationResult.EvaluatedAt = strategyStartTime;
                evaluationResult.EstimatedExecutionTime = DateTime.UtcNow - strategyStartTime;
                evaluationResult.HealthStatus = healthStatus;

                // Add strategy-specific metadata
                await AddStrategyMetadataAsync(evaluationResult, strategy, request, cancellationToken);

                _logger.LogDebug("Strategy {StrategyName} evaluation completed with confidence {Confidence:F2}",
                    strategy.Name, confidenceScore);

                return evaluationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating strategy {StrategyName} for request {RequestId}",
                    strategy.Name, request.RequestId);

                var result = StrategyEvaluationResult.CreateFailure(
                    strategy, request, $"Evaluation failed: {ex.Message}");
                result.EvaluatedAt = strategyStartTime;
                result.EstimatedExecutionTime = DateTime.UtcNow - strategyStartTime;
                result.AddMetadata("EvaluationError", ex.Message);
                result.AddMetadata("EvaluationErrorType", ex.GetType().Name);

                return result;
            }
        }

        /// <summary>
        /// Calculates the confidence score for a strategy
        /// </summary>
        /// <param name="strategy">The strategy to evaluate</param>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Confidence score (0.0 to 1.0)</returns>
        private async Task<double> CalculateConfidenceScoreAsync(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            CancellationToken cancellationToken)
        {
            var confidenceFactors = new List<double>();

            // Base confidence from strategy priority
            var priorityConfidence = Math.Min(strategy.Priority / 10.0, 1.0);
            confidenceFactors.Add(priorityConfidence);

            // Request-specific confidence
            var requestConfidence = await CalculateRequestConfidenceAsync(strategy, request, cancellationToken);
            confidenceFactors.Add(requestConfidence);

            // Time-based confidence
            var timeConfidence = CalculateTimeConfidence(request);
            confidenceFactors.Add(timeConfidence);

            // Health-based confidence
            var healthStatus = strategy.GetHealthStatus();
            var healthConfidence = healthStatus.IsHealthy ? 1.0 : 0.5;
            confidenceFactors.Add(healthConfidence);

            // Apply configuration-based adjustments
            if (_config.StrategyWeights.TryGetValue(strategy.Name, out var weight))
            {
                confidenceFactors.Add(weight);
            }

            // Calculate weighted average
            var finalConfidence = confidenceFactors.Count > 0
                ? confidenceFactors.Average()
                : 0.5; // Default confidence

            _logger.LogDebug("Confidence calculation for {StrategyName}: {Confidence:F2} (factors: {Factors})",
                strategy.Name, finalConfidence, string.Join(", ", confidenceFactors.Select(f => f.ToString("F2"))));

            return Math.Max(0.0, Math.Min(1.0, finalConfidence));
        }

        /// <summary>
        /// Calculates request-specific confidence
        /// </summary>
        /// <param name="strategy">The strategy to evaluate</param>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Request confidence score</returns>
        private async Task<double> CalculateRequestConfidenceAsync(
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            CancellationToken cancellationToken)
        {
            var confidence = 1.0;

            // Adjust based on request priority
            switch (request.Priority)
            {
                case ReleaseRequestPriority.Critical:
                    confidence *= 1.2; // Boost confidence for critical requests
                    break;
                case ReleaseRequestPriority.High:
                    confidence *= 1.1;
                    break;
                case ReleaseRequestPriority.Low:
                    confidence *= 0.8;
                    break;
            }

            // Adjust based on request source
            if (request.Source.Equals("manual", StringComparison.OrdinalIgnoreCase))
            {
                confidence *= 1.1; // Manual requests are more reliable
            }
            else if (request.Source.Equals("scheduled", StringComparison.OrdinalIgnoreCase))
            {
                confidence *= 0.9; // Scheduled requests might need more validation
            }

            // Adjust based on force release flag
            if (request.ForceRelease)
            {
                confidence *= 0.7; // Force release is less predictable
            }

            // Strategy-specific adjustments
            if (strategy.Name.Equals("IdleTimeReleaseStrategy", StringComparison.OrdinalIgnoreCase))
            {
                confidence = await CalculateIdleTimeConfidenceAsync(request, cancellationToken);
            }
            else if (strategy.Name.Equals("BusinessHoursReleaseStrategy", StringComparison.OrdinalIgnoreCase))
            {
                confidence = CalculateBusinessHoursConfidence(request);
            }

            return Math.Max(0.0, Math.Min(1.0, confidence));
        }

        /// <summary>
        /// Calculates confidence for idle time strategy
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Idle time confidence score</returns>
        private async Task<double> CalculateIdleTimeConfidenceAsync(
            ReleaseRequest request,
            CancellationToken cancellationToken)
        {
            // This would integrate with the idle detection service
            // For now, return a reasonable default
            return await Task.FromResult(0.8);
        }

        /// <summary>
        /// Calculates confidence for business hours strategy
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>Business hours confidence score</returns>
        private double CalculateBusinessHoursConfidence(ReleaseRequest request)
        {
            var confidence = 1.0;
            var currentTime = DateTime.Now;

            // Adjust based on time of day
            var currentHour = currentTime.Hour;
            if (currentHour >= 9 && currentHour <= 17)
            {
                confidence *= 1.1; // Business hours boost
            }
            else
            {
                confidence *= 0.6; // Outside business hours penalty
            }

            // Adjust based on day of week
            if (currentTime.DayOfWeek == DayOfWeek.Monday)
            {
                confidence *= 0.9; // Monday penalty (busy day)
            }
            else if (currentTime.DayOfWeek == DayOfWeek.Friday)
            {
                confidence *= 1.1; // Friday boost (wrapping up)
            }

            return Math.Max(0.0, Math.Min(1.0, confidence));
        }

        /// <summary>
        /// Calculates time-based confidence
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>Time-based confidence score</returns>
        private double CalculateTimeConfidence(ReleaseRequest request)
        {
            var confidence = 1.0;

            // Adjust based on scheduled time
            if (request.ScheduledTime.HasValue)
            {
                var timeUntilScheduled = request.ScheduledTime.Value - DateTime.Now;
                if (timeUntilScheduled.TotalMinutes < 5)
                {
                    confidence *= 0.8; // Very soon execution is less confident
                }
                else if (timeUntilScheduled.TotalMinutes > 60)
                {
                    confidence *= 0.9; // Far future execution is less confident
                }
            }

            // Adjust based on timeout
            if (request.Timeout < TimeSpan.FromSeconds(15))
            {
                confidence *= 0.7; // Very short timeout is less confident
            }
            else if (request.Timeout > TimeSpan.FromMinutes(5))
            {
                confidence *= 1.1; // Long timeout is more confident
            }

            return Math.Max(0.0, Math.Min(1.0, confidence));
        }

        /// <summary>
        /// Adds strategy-specific metadata to the evaluation result
        /// </summary>
        /// <param name="result">The evaluation result</param>
        /// <param name="strategy">The strategy being evaluated</param>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task AddStrategyMetadataAsync(
            StrategyEvaluationResult result,
            ILicenseReleaseStrategy strategy,
            ReleaseRequest request,
            CancellationToken cancellationToken)
        {
            // Add basic metadata
            result.AddMetadata("StrategyType", strategy.GetType().Name);
            result.AddMetadata("StrategyDescription", strategy.Description);
            result.AddMetadata("EvaluationTimestamp", DateTime.UtcNow);

            // Add request metadata
            result.AddMetadata("RequestPriority", request.Priority.ToString());
            result.AddMetadata("RequestSource", request.Source);
            result.AddMetadata("RequestInitiator", request.Initiator);

            // Add strategy-specific metadata
            if (strategy.Name.Equals("IdleTimeReleaseStrategy", StringComparison.OrdinalIgnoreCase))
            {
                await AddIdleTimeMetadataAsync(result, request, cancellationToken);
            }
            else if (strategy.Name.Equals("BusinessHoursReleaseStrategy", StringComparison.OrdinalIgnoreCase))
            {
                AddBusinessHoursMetadata(result, request);
            }

            // Add performance metrics
            result.AddMetadata("EvaluationDurationMs", result.EstimatedExecutionTime.TotalMilliseconds);
        }

        /// <summary>
        /// Adds idle time strategy specific metadata
        /// </summary>
        /// <param name="result">The evaluation result</param>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task AddIdleTimeMetadataAsync(
            StrategyEvaluationResult result,
            ReleaseRequest request,
            CancellationToken cancellationToken)
        {
            // This would integrate with idle detection service to get real metadata
            // For now, add placeholder metadata
            result.AddMetadata("IdleDetectionAvailable", true);
            result.AddMetadata("EstimatedIdleTime", TimeSpan.FromMinutes(15));

            await Task.CompletedTask;
        }

        /// <summary>
        /// Adds business hours strategy specific metadata
        /// </summary>
        /// <param name="result">The evaluation result</param>
        /// <param name="request">The license release request</param>
        private void AddBusinessHoursMetadata(
            StrategyEvaluationResult result,
            ReleaseRequest request)
        {
            var currentTime = DateTime.Now;
            result.AddMetadata("CurrentDayOfWeek", currentTime.DayOfWeek.ToString());
            result.AddMetadata("CurrentHour", currentTime.Hour);
            result.AddMetadata("IsBusinessDay", IsBusinessDay(currentTime));
            result.AddMetadata("IsBusinessHours", IsBusinessHours(currentTime));
        }

        /// <summary>
        /// Checks if the given date is a business day
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if it's a business day</returns>
        private bool IsBusinessDay(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }

        /// <summary>
        /// Checks if the given time is within business hours
        /// </summary>
        /// <param name="time">The time to check</param>
        /// <returns>True if within business hours</returns>
        private bool IsBusinessHours(DateTime time)
        {
            var hour = time.Hour;
            return hour >= 9 && hour <= 17;
        }

        /// <summary>
        /// Logs the evaluation summary
        /// </summary>
        /// <param name="result">The evaluation result</param>
        private void LogEvaluationSummary(MultiStrategyEvaluationResult result)
        {
            _logger.LogInformation("Strategy evaluation completed for request {RequestId}: {ViableCount}/{TotalCount} viable strategies",
                result.Request.RequestId, result.ViableStrategyCount, result.StrategyResults.Count);

            if (result.RecommendedStrategy != null)
            {
                _logger.LogInformation("Recommended strategy: {StrategyName} (confidence: {Confidence:F2})",
                    result.RecommendedStrategy.Strategy.Name, result.RecommendedStrategy.ConfidenceScore);
            }

            // Log detailed results for debugging
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                foreach (var strategyResult in result.StrategyResults)
                {
                    _logger.LogDebug("Strategy {StrategyName}: CanHandle={CanHandle}, Valid={Valid}, Confidence={Confidence:F2}",
                        strategyResult.Strategy.Name, strategyResult.CanHandle,
                        strategyResult.ValidationResult.IsValid, strategyResult.ConfidenceScore);
                }
            }
        }

        /// <summary>
        /// Gets the best strategy for the given request
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The best strategy or null if none are viable</returns>
        public async Task<ILicenseReleaseStrategy> GetBestStrategyAsync(
            ReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            var evaluationResult = await EvaluateAllStrategiesAsync(request, cancellationToken);
            return evaluationResult.RecommendedStrategy?.Strategy;
        }

        /// <summary>
        /// Gets all viable strategies for the given request
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of viable strategies</returns>
        public async Task<List<ILicenseReleaseStrategy>> GetViableStrategiesAsync(
            ReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            var evaluationResult = await EvaluateAllStrategiesAsync(request, cancellationToken);
            return evaluationResult.GetRecommendedStrategies()
                .Select(r => r.Strategy)
                .ToList();
        }
    }

    /// <summary>
    /// Configuration for strategy evaluation
    /// </summary>
    public class StrategyEvaluationConfig
    {
        /// <summary>
        /// Gets or sets strategy-specific weights
        /// </summary>
        public Dictionary<string, double> StrategyWeights { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Gets or sets the minimum confidence threshold for strategy selection
        /// </summary>
        public double MinimumConfidenceThreshold { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets the timeout for strategy evaluation in seconds
        /// </summary>
        public int EvaluationTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to enable detailed logging
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to evaluate all strategies in parallel
        /// </summary>
        public bool ParallelEvaluation { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of concurrent strategy evaluations
        /// </summary>
        public int MaxConcurrentEvaluations { get; set; } = 5;

        /// <summary>
        /// Gets or sets custom configuration parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the StrategyEvaluationConfig class
        /// </summary>
        public StrategyEvaluationConfig()
        {
            // Set default strategy weights
            StrategyWeights["IdleTimeReleaseStrategy"] = 1.0;
            StrategyWeights["BusinessHoursReleaseStrategy"] = 0.9;
        }
    }
}