using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Implements consensus logic between multiple idle detectors using configurable strategies
    /// </summary>
    public class DetectionConsensusEngine : IDisposable
    {
        private readonly object _lock = new object();
        private readonly ILogger<DetectionConsensusEngine> _logger;
        private readonly ConsensusConfiguration _configuration;
        private readonly Dictionary<string, IIdleDetector> _detectors;
        private readonly SessionStateManager _sessionStateManager;
        private bool _isDisposed;
        private bool _isInitialized;
        private long _totalConsensusEvaluations;
        private long _successfulConsensusEvaluations;

        #region Events

        /// <summary>
        /// Event raised when consensus determines idle state
        /// </summary>
        public event EventHandler<IdleDetectionEventArgs> IdleDetected;

        /// <summary>
        /// Event raised when consensus determines activity state
        /// </summary>
        public event EventHandler<IdleDetectionEventArgs> ActivityDetected;

        /// <summary>
        /// Event raised when consensus is inconclusive
        /// </summary>
        public event EventHandler<ConsensusInconclusiveEventArgs> ConsensusInconclusive;

        /// <summary>
        /// Event raised when consensus engine encounters an error
        /// </summary>
        public event EventHandler<ConsensusErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Event raised when consensus decision is made
        /// </summary>
        public event EventHandler<ConsensusDecisionEventArgs> ConsensusDecision;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether the engine is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the configuration
        /// </summary>
        public ConsensusConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets the registered detectors
        /// </summary>
        public IReadOnlyDictionary<string, IIdleDetector> Detectors =>
            new Dictionary<string, IIdleDetector>(_detectors).AsReadOnly();

        /// <summary>
        /// Gets the number of registered detectors
        /// </summary>
        public int DetectorCount => _detectors.Count;

        /// <summary>
        /// Gets the number of enabled detectors
        /// </summary>
        public int EnabledDetectorCount => _detectors.Values.Count(d => d.IsEnabled);

        /// <summary>
        /// Gets the total number of consensus evaluations
        /// </summary>
        public long TotalConsensusEvaluations => _totalConsensusEvaluations;

        /// <summary>
        /// Gets the number of successful consensus evaluations
        /// </summary>
        public long SuccessfulConsensusEvaluations => _successfulConsensusEvaluations;

        /// <summary>
        /// Gets the consensus success rate
        /// </summary>
        public double SuccessRate => _totalConsensusEvaluations > 0 ?
            (double)_successfulConsensusEvaluations / _totalConsensusEvaluations : 0.0;

        #endregion

        /// <summary>
        /// Initializes a new instance of the DetectionConsensusEngine class
        /// </summary>
        /// <param name="configuration">The consensus configuration</param>
        /// <param name="sessionStateManager">The session state manager</param>
        /// <param name="logger">The logger</param>
        public DetectionConsensusEngine(
            ConsensusConfiguration configuration,
            SessionStateManager sessionStateManager,
            ILogger<DetectionConsensusEngine> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _sessionStateManager = sessionStateManager ?? throw new ArgumentNullException(nameof(sessionStateManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _detectors = new Dictionary<string, IIdleDetector>();
        }

        /// <summary>
        /// Initializes the consensus engine
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the initialization operation</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(DetectionConsensusEngine));

                if (_isInitialized)
                    return;
            }

            try
            {
                _logger.LogInformation("Initializing DetectionConsensusEngine");

                // Validate configuration
                ValidateConfiguration();

                _isInitialized = true;

                _logger.LogInformation("DetectionConsensusEngine initialized successfully with method: {Method}",
                    _configuration.ConsensusMethod);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing DetectionConsensusEngine");
                OnErrorOccurred("InitializeAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        /// <summary>
        /// Adds a detector to the consensus engine
        /// </summary>
        /// <param name="detector">The detector to add</param>
        public void AddDetector(IIdleDetector detector)
        {
            if (detector == null)
                throw new ArgumentNullException(nameof(detector));

            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(DetectionConsensusEngine));

                if (_detectors.ContainsKey(detector.Name))
                    throw new InvalidOperationException($"Detector '{detector.Name}' is already registered");

                _detectors[detector.Name] = detector;

                _logger.LogDebug("Added detector to consensus engine: {DetectorName}", detector.Name);
            }
        }

        /// <summary>
        /// Removes a detector from the consensus engine
        /// </summary>
        /// <param name="detectorName">The name of the detector to remove</param>
        public void RemoveDetector(string detectorName)
        {
            if (string.IsNullOrWhiteSpace(detectorName))
                throw new ArgumentNullException(nameof(detectorName));

            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(DetectionConsensusEngine));

                if (_detectors.Remove(detectorName))
                {
                    _logger.LogDebug("Removed detector from consensus engine: {DetectorName}", detectorName);
                }
            }
        }

        /// <summary>
        /// Performs consensus-based idle detection for a process
        /// </summary>
        /// <param name="processInfo">The process information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Idle detection result</returns>
        public async Task<IdleDetectionResult> DetectIdleAsync(
            ProcessInfo processInfo,
            CancellationToken cancellationToken = default)
        {
            if (processInfo == null)
                throw new ArgumentNullException(nameof(processInfo));

            if (!_isInitialized)
                throw new InvalidOperationException("Consensus engine is not initialized");

            Interlocked.Increment(ref _totalConsensusEvaluations);

            try
            {
                _logger.LogDebug("Performing consensus detection for process {ProcessId}", processInfo.ProcessId);

                // Get or create session state entry
                var sessionId = GenerateSessionId(processInfo.ProcessId, processInfo.UserName, processInfo.ComputerName);
                var sessionEntry = _sessionStateManager.GetOrCreateSession(
                    sessionId, processInfo.ProcessId, processInfo.UserName, processInfo.ComputerName);

                // Collect detector results
                var detectorResults = await CollectDetectorResultsAsync(processInfo, cancellationToken);

                if (detectorResults.Count == 0)
                {
                    _logger.LogWarning("No detectors available for consensus evaluation");
                    return CreateEmptyResult(processInfo, "No detectors available");
                }

                // Apply consensus logic
                var consensusResult = ApplyConsensusLogic(processInfo, detectorResults);

                // Record activity if not idle
                if (!consensusResult.IsIdle)
                {
                    _sessionStateManager.RecordActivity(sessionId, "ConsensusActivityDetected");
                }

                // Update session state
                var newState = consensusResult.IsIdle ? SessionState.Idle : SessionState.Active;
                _sessionStateManager.UpdateSessionState(sessionId, newState, consensusResult.Confidence, consensusResult.Reason);

                // Raise consensus decision event
                OnConsensusDecision(sessionId, processInfo.ProcessId, consensusResult, detectorResults);

                Interlocked.Increment(ref _successfulConsensusEvaluations);

                return consensusResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing consensus detection for process {ProcessId}", processInfo.ProcessId);
                OnErrorOccurred($"DetectIdle for process {processInfo.ProcessId}", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        /// <summary>
        /// Gets consensus statistics
        /// </summary>
        /// <returns>Consensus statistics</returns>
        public ConsensusStatistics GetStatistics()
        {
            return new ConsensusStatistics
            {
                TotalEvaluations = _totalConsensusEvaluations,
                SuccessfulEvaluations = _successfulConsensusEvaluations,
                SuccessRate = SuccessRate,
                DetectorCount = DetectorCount,
                EnabledDetectorCount = EnabledDetectorCount,
                ConsensusMethod = _configuration.ConsensusMethod,
                AverageConfidence = CalculateAverageConfidence(),
                InconclusiveRate = CalculateInconclusiveRate()
            };
        }

        /// <summary>
        /// Gets detailed consensus information for a session
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>Consensus information</returns>
        public ConsensusInfo GetConsensusInfo(string sessionId)
        {
            // This would require tracking consensus history per session
            // For now, return basic information
            return new ConsensusInfo
            {
                SessionId = sessionId,
                ConsensusMethod = _configuration.ConsensusMethod,
                DetectorCount = EnabledDetectorCount,
                Threshold = _configuration.Threshold,
                Timestamp = DateTime.UtcNow
            };
        }

        #region Private Methods

        private async Task<List<DetectorResult>> CollectDetectorResultsAsync(
            ProcessInfo processInfo,
            CancellationToken cancellationToken)
        {
            var results = new List<DetectorResult>();
            var enabledDetectors = _detectors.Values.Where(d => d.IsEnabled).ToList();

            if (enabledDetectors.Count == 0)
                return results;

            // Execute detectors in parallel with timeout
            var detectionTasks = enabledDetectors.Select(async detector =>
            {
                try
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(_configuration.DetectionTimeoutMs));

                    var result = await detector.DetectIdleAsync(
                        processInfo.ProcessId,
                        processInfo.UserName,
                        processInfo.ComputerName,
                        cts.Token);

                    return new DetectorResult
                    {
                        DetectorName = detector.Name,
                        IsIdle = result.IsIdle,
                        Confidence = result.Confidence,
                        IdleTime = result.IdleTime,
                        DetectionMethod = result.DetectionMethod,
                        Metadata = result.Metadata
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Detector {DetectorName} failed for process {ProcessId}",
                        detector.Name, processInfo.ProcessId);

                    return new DetectorResult
                    {
                        DetectorName = detector.Name,
                        IsIdle = false,
                        Confidence = 0.0,
                        IdleTime = TimeSpan.Zero,
                        DetectionMethod = "Error",
                        Metadata = new Dictionary<string, object>
                        {
                            ["Error"] = ex.Message
                        }
                    };
                }
            });

            var detectorResults = await Task.WhenAll(detectionTasks);
            results.AddRange(detectorResults);

            return results;
        }

        private IdleDetectionResult ApplyConsensusLogic(
            ProcessInfo processInfo,
            List<DetectorResult> detectorResults)
        {
            return _configuration.ConsensusMethod switch
            {
                ConsensusMethod.Majority => ApplyMajorityConsensus(processInfo, detectorResults),
                ConsensusMethod.Weighted => ApplyWeightedConsensus(processInfo, detectorResults),
                ConsensusMethod.Unanimous => ApplyUnanimousConsensus(processInfo, detectorResults),
                ConsensusMethod.Threshold => ApplyThresholdConsensus(processInfo, detectorResults),
                ConsensusMethod.Hierarchical => ApplyHierarchicalConsensus(processInfo, detectorResults),
                _ => ApplyMajorityConsensus(processInfo, detectorResults)
            };
        }

        private IdleDetectionResult ApplyMajorityConsensus(
            ProcessInfo processInfo,
            List<DetectorResult> detectorResults)
        {
            var idleCount = detectorResults.Count(r => r.IsIdle);
            var activeCount = detectorResults.Count - idleCount;
            var majorityThreshold = Math.Ceiling(detectorResults.Count / 2.0);

            var isIdle = idleCount >= majorityThreshold;
            var confidence = isIdle ? (double)idleCount / detectorResults.Count : (double)activeCount / detectorResults.Count;
            var averageIdleTime = detectorResults.Where(r => r.IsIdle).Select(r => r.IdleTime).DefaultIfEmpty(TimeSpan.Zero).Average();
            var reason = isIdle
                ? $"Majority consensus: {idleCount}/{detectorResults.Count} detectors indicate idle"
                : $"Majority consensus: {activeCount}/{detectorResults.Count} detectors indicate active";

            return CreateConsensusResult(processInfo, isIdle, confidence, averageIdleTime, reason, detectorResults);
        }

        private IdleDetectionResult ApplyWeightedConsensus(
            ProcessInfo processInfo,
            List<DetectorResult> detectorResults)
        {
            var totalWeight = detectorResults.Sum(r => GetDetectorWeight(r.DetectorName));
            var idleWeight = detectorResults.Where(r => r.IsIdle).Sum(r => GetDetectorWeight(r.DetectorName) * r.Confidence);
            var activeWeight = detectorResults.Where(r => !r.IsIdle).Sum(r => GetDetectorWeight(r.DetectorName) * r.Confidence);

            var isIdle = idleWeight > activeWeight;
            var confidence = Math.Max(idleWeight, activeWeight) / totalWeight;
            var weightedIdleTime = CalculateWeightedIdleTime(detectorResults);
            var reason = isIdle
                ? $"Weighted consensus: idle weight {idleWeight:F2} > active weight {activeWeight:F2}"
                : $"Weighted consensus: active weight {activeWeight:F2} > idle weight {idleWeight:F2}";

            return CreateConsensusResult(processInfo, isIdle, confidence, weightedIdleTime, reason, detectorResults);
        }

        private IdleDetectionResult ApplyUnanimousConsensus(
            ProcessInfo processInfo,
            List<DetectorResult> detectorResults)
        {
            var allIdle = detectorResults.All(r => r.IsIdle);
            var allActive = detectorResults.All(r => !r.IsIdle);
            var isIdle = allIdle;

            double confidence;
            TimeSpan averageIdleTime;
            string reason;

            if (allIdle)
            {
                confidence = 1.0;
                averageIdleTime = detectorResults.Select(r => r.IdleTime).Average();
                reason = "Unanimous consensus: all detectors indicate idle";
            }
            else if (allActive)
            {
                confidence = 1.0;
                averageIdleTime = TimeSpan.Zero;
                reason = "Unanimous consensus: all detectors indicate active";
            }
            else
            {
                confidence = 0.0;
                averageIdleTime = TimeSpan.Zero;
                reason = "Unanimous consensus: detectors disagree";
            }

            return CreateConsensusResult(processInfo, isIdle, confidence, averageIdleTime, reason, detectorResults);
        }

        private IdleDetectionResult ApplyThresholdConsensus(
            ProcessInfo processInfo,
            List<DetectorResult> detectorResults)
        {
            var idleCount = detectorResults.Count(r => r.IsIdle);
            var idleRatio = (double)idleCount / detectorResults.Count;
            var threshold = _configuration.Threshold;

            var isIdle = idleRatio >= threshold;
            var confidence = isIdle ? idleRatio : 1.0 - idleRatio;
            var averageIdleTime = detectorResults.Where(r => r.IsIdle).Select(r => r.IdleTime).DefaultIfEmpty(TimeSpan.Zero).Average();
            var reason = isIdle
                ? $"Threshold consensus: {idleRatio:P2} >= {threshold:P2} idle detectors"
                : $"Threshold consensus: {idleRatio:P2} < {threshold:P2} idle detectors";

            return CreateConsensusResult(processInfo, isIdle, confidence, averageIdleTime, reason, detectorResults);
        }

        private IdleDetectionResult ApplyHierarchicalConsensus(
            ProcessInfo processInfo,
            List<DetectorResult> detectorResults)
        {
            // Sort detectors by priority (descending)
            var sortedDetectors = detectorResults
                .OrderByDescending(r => GetDetectorPriority(r.DetectorName))
                .ToList();

            // Find the highest priority detector with sufficient confidence
            var decisiveDetector = sortedDetectors.FirstOrDefault(r => r.Confidence >= _configuration.MinimumConfidence);

            if (decisiveDetector != null)
            {
                var confidence = decisiveDetector.Confidence;
                var reason = $"Hierarchical consensus: {decisiveDetector.DetectorName} (priority {GetDetectorPriority(decisiveDetector.DetectorName)}) is decisive";
                return CreateConsensusResult(processInfo, decisiveDetector.IsIdle, confidence, decisiveDetector.IdleTime, reason, detectorResults);
            }

            // Fall back to majority if no decisive detector
            return ApplyMajorityConsensus(processInfo, detectorResults);
        }

        private IdleDetectionResult CreateConsensusResult(
            ProcessInfo processInfo,
            bool isIdle,
            double confidence,
            TimeSpan idleTime,
            string reason,
            List<DetectorResult> detectorResults)
        {
            var sessionId = GenerateSessionId(processInfo.ProcessId, processInfo.UserName, processInfo.ComputerName);

            var result = new IdleDetectionResult
            {
                SessionId = sessionId,
                ProcessId = processInfo.ProcessId,
                UserName = processInfo.UserName,
                ComputerName = processInfo.ComputerName,
                IsIdle = isIdle,
                Confidence = confidence,
                IdleTime = idleTime,
                DetectionMethod = $"Consensus-{_configuration.ConsensusMethod}",
                DetectorName = "ConsensusEngine",
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };

            // Add detector results as metadata
            result.Metadata["DetectorResults"] = detectorResults;
            result.Metadata["ConsensusMethod"] = _configuration.ConsensusMethod.ToString();
            result.Metadata["DetectorCount"] = detectorResults.Count;

            // Raise appropriate event
            if (isIdle)
            {
                OnIdleDetected(result);
            }
            else
            {
                OnActivityDetected(result);
            }

            return result;
        }

        private IdleDetectionResult CreateEmptyResult(ProcessInfo processInfo, string reason)
        {
            var sessionId = GenerateSessionId(processInfo.ProcessId, processInfo.UserName, processInfo.ComputerName);

            return new IdleDetectionResult
            {
                SessionId = sessionId,
                ProcessId = processInfo.ProcessId,
                UserName = processInfo.UserName,
                ComputerName = processInfo.ComputerName,
                IsIdle = false,
                Confidence = 0.0,
                IdleTime = TimeSpan.Zero,
                DetectionMethod = "Consensus",
                DetectorName = "ConsensusEngine",
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };
        }

        private double GetDetectorWeight(string detectorName)
        {
            return _configuration.DetectorWeights.TryGetValue(detectorName, out var weight) ? weight : 1.0;
        }

        private int GetDetectorPriority(string detectorName)
        {
            return _configuration.DetectorPriorities.TryGetValue(detectorName, out var priority) ? priority : 1;
        }

        private TimeSpan CalculateWeightedIdleTime(List<DetectorResult> detectorResults)
        {
            var idleResults = detectorResults.Where(r => r.IsIdle).ToList();
            if (!idleResults.Any())
                return TimeSpan.Zero;

            var totalWeight = idleResults.Sum(r => GetDetectorWeight(r.DetectorName));
            var weightedTime = idleResults.Sum(r => GetDetectorWeight(r.DetectorName) * r.IdleTime.TotalSeconds);

            return TimeSpan.FromSeconds(weightedTime / totalWeight);
        }

        private double CalculateAverageConfidence()
        {
            // This would need to track historical confidence values
            // For now, return the configuration threshold
            return _configuration.Threshold;
        }

        private double CalculateInconclusiveRate()
        {
            // This would need to track historical inconclusive results
            // For now, return a default value
            return 0.0;
        }

        private void ValidateConfiguration()
        {
            if (_configuration.Threshold < 0.0 || _configuration.Threshold > 1.0)
            {
                throw new ArgumentException("Threshold must be between 0.0 and 1.0", nameof(_configuration.Threshold));
            }

            if (_configuration.MinimumConfidence < 0.0 || _configuration.MinimumConfidence > 1.0)
            {
                throw new ArgumentException("Minimum confidence must be between 0.0 and 1.0", nameof(_configuration.MinimumConfidence));
            }

            if (_configuration.DetectionTimeoutMs <= 0)
            {
                throw new ArgumentException("Detection timeout must be positive", nameof(_configuration.DetectionTimeoutMs));
            }
        }

        private static string GenerateSessionId(int processId, string userName, string computerName)
        {
            return $"{processId}_{userName}_{computerName}".Replace(" ", "_").Replace("\\", "_");
        }

        #endregion

        #region Event Raisers

        protected virtual void OnIdleDetected(IdleDetectionEventArgs e)
        {
            IdleDetected?.Invoke(this, e);
        }

        protected virtual void OnActivityDetected(IdleDetectionEventArgs e)
        {
            ActivityDetected?.Invoke(this, e);
        }

        protected virtual void OnConsensusInconclusive(ConsensusInconclusiveEventArgs e)
        {
            ConsensusInconclusive?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(string operation, Exception error, ErrorSeverity severity)
        {
            ErrorOccurred?.Invoke(this, new ConsensusErrorEventArgs(operation, error, severity));
        }

        protected virtual void OnConsensusDecision(
            string sessionId,
            int processId,
            IdleDetectionResult result,
            List<DetectorResult> detectorResults)
        {
            ConsensusDecision?.Invoke(this, new ConsensusDecisionEventArgs
            {
                SessionId = sessionId,
                ProcessId = processId,
                IsIdle = result.IsIdle,
                Confidence = result.Confidence,
                ConsensusMethod = _configuration.ConsensusMethod,
                DetectorResults = detectorResults,
                Timestamp = DateTime.UtcNow
            });
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Detectors are owned by the engine and will be disposed there
                }

                _isDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for consensus decision
    /// </summary>
    public class ConsensusDecisionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets a value indicating whether the session is idle
        /// </summary>
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets the consensus method used
        /// </summary>
        public ConsensusMethod ConsensusMethod { get; set; }

        /// <summary>
        /// Gets the detector results
        /// </summary>
        public List<DetectorResult> DetectorResults { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for inconclusive consensus
    /// </summary>
    public class ConsensusInconclusiveEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets the process identifier
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the detector results
        /// </summary>
        public List<DetectorResult> DetectorResults { get; set; }

        /// <summary>
        /// Gets the reason for inconclusive result
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for consensus errors
    /// </summary>
    public class ConsensusErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the operation that failed
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets the error exception
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// Gets the error severity
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the ConsensusErrorEventArgs class
        /// </summary>
        /// <param name="operation">The operation that failed</param>
        /// <param name="error">The error exception</param>
        /// <param name="severity">The error severity</param>
        public ConsensusErrorEventArgs(string operation, Exception error, ErrorSeverity severity)
        {
            Operation = operation;
            Error = error;
            Severity = severity;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents consensus statistics
    /// </summary>
    public class ConsensusStatistics
    {
        /// <summary>
        /// Gets the total number of consensus evaluations
        /// </summary>
        public long TotalEvaluations { get; set; }

        /// <summary>
        /// Gets the number of successful consensus evaluations
        /// </summary>
        public long SuccessfulEvaluations { get; set; }

        /// <summary>
        /// Gets the success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets the number of detectors
        /// </summary>
        public int DetectorCount { get; set; }

        /// <summary>
        /// Gets the number of enabled detectors
        /// </summary>
        public int EnabledDetectorCount { get; set; }

        /// <summary>
        /// Gets the consensus method
        /// </summary>
        public ConsensusMethod ConsensusMethod { get; set; }

        /// <summary>
        /// Gets the average confidence level
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Gets the inconclusive rate
        /// </summary>
        public double InconclusiveRate { get; set; }
    }

    /// <summary>
    /// Represents consensus information
    /// </summary>
    public class ConsensusInfo
    {
        /// <summary>
        /// Gets the session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets the consensus method
        /// </summary>
        public ConsensusMethod ConsensusMethod { get; set; }

        /// <summary>
        /// Gets the number of detectors
        /// </summary>
        public int DetectorCount { get; set; }

        /// <summary>
        /// Gets the consensus threshold
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Configuration for consensus engine
    /// </summary>
    public class ConsensusConfiguration
    {
        /// <summary>
        /// Gets or sets the consensus method to use
        /// </summary>
        public ConsensusMethod ConsensusMethod { get; set; } = ConsensusMethod.Threshold;

        /// <summary>
        /// Gets or sets the consensus threshold (0.0 to 1.0)
        /// </summary>
        public double Threshold { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets the minimum confidence required (0.0 to 1.0)
        /// </summary>
        public double MinimumConfidence { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets the detection timeout in milliseconds
        /// </summary>
        public int DetectionTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the detector weights (for weighted consensus)
        /// </summary>
        public Dictionary<string, double> DetectorWeights { get; set; }

        /// <summary>
        /// Gets or sets the detector priorities (for hierarchical consensus)
        /// </summary>
        public Dictionary<string, int> DetectorPriorities { get; set; }

        /// <summary>
        /// Initializes a new instance of the ConsensusConfiguration class
        /// </summary>
        public ConsensusConfiguration()
        {
            DetectorWeights = new Dictionary<string, double>();
            DetectorPriorities = new Dictionary<string, int>();
        }
    }
}