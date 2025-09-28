using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.IdleDetection.Models;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Integrates idle detection with the license management system
    /// </summary>
    public class LicenseManagementIntegration : IDisposable
    {
        private readonly ILicenseManager _licenseManager;
        private readonly ConfigurationIntegration _configurationIntegration;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private Timer _licenseCheckTimer;
        private Dictionary<string, LicenseUserSession> _activeSessions;
        private Dictionary<string, IdleDetectionResult> _detectionResults;
        private DateTime _lastLicenseCheck;
        private readonly TimeSpan _licenseCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Event raised when license release is triggered by idle detection
        /// </summary>
        public event EventHandler<LicenseReleaseEventArgs> LicenseReleaseTriggered;

        /// <summary>
        /// Event raised when license session state changes
        /// </summary>
        public event EventHandler<LicenseSessionEventArgs> SessionStateChanged;

        /// <summary>
        /// Event raised when idle detection affects license usage
        /// </summary>
        public event EventHandler<IdleDetectionLicenseEventArgs> IdleDetectionAffectedLicenseUsage;

        /// <summary>
        /// Gets the active license sessions
        /// </summary>
        public IReadOnlyDictionary<string, LicenseUserSession> ActiveSessions
        {
            get
            {
                lock (_lock)
                {
                    return _activeSessions.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the current detection results
        /// </summary>
        public IReadOnlyDictionary<string, IdleDetectionResult> DetectionResults
        {
            get
            {
                lock (_lock)
                {
                    return _detectionResults.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets whether license management integration is enabled
        /// </summary>
        public bool IsEnabled => _configurationIntegration?.IsIdleDetectionEnabled ?? false;

        /// <summary>
        /// Gets the last license check time
        /// </summary>
        public DateTime LastLicenseCheck => _lastLicenseCheck;

        /// <summary>
        /// Initializes a new instance of the LicenseManagementIntegration class
        /// </summary>
        public LicenseManagementIntegration(ILicenseManager licenseManager, ConfigurationIntegration configurationIntegration)
        {
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _configurationIntegration = configurationIntegration ?? throw new ArgumentNullException(nameof(configurationIntegration));

            _activeSessions = new Dictionary<string, LicenseUserSession>();
            _detectionResults = new Dictionary<string, IdleDetectionResult>();
            _lastLicenseCheck = DateTime.MinValue;

            InitializeLicenseMonitoring();
        }

        /// <summary>
        /// Processes idle detection results and potentially triggers license releases
        /// </summary>
        public async Task ProcessIdleDetectionResultsAsync(IEnumerable<IdleDetectionResult> results, CancellationToken cancellationToken = default)
        {
            if (results == null || !IsEnabled)
            {
                return;
            }

            try
            {
                var resultsList = results.ToList();
                if (!resultsList.Any())
                {
                    return;
                }

                // Update detection results
                lock (_lock)
                {
                    foreach (var result in resultsList)
                    {
                        _detectionResults[result.SessionId] = result;
                    }
                }

                // Get current license sessions
                var currentSessions = await GetCurrentLicenseSessionsAsync(cancellationToken);
                if (!currentSessions.Any())
                {
                    return;
                }

                // Process each detection result
                foreach (var result in resultsList)
                {
                    await ProcessDetectionResultAsync(result, currentSessions, cancellationToken);
                }

                // Clean up stale sessions and results
                CleanupStaleData();

                // Raise event for license usage changes
                OnIdleDetectionAffectedLicenseUsage(new IdleDetectionLicenseEventArgs(
                    resultsList, currentSessions, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application
                await LogErrorAsync($"Error processing idle detection results: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets license sessions that should be released based on idle detection
        /// </summary>
        public async Task<IEnumerable<LicenseReleaseCandidate>> GetReleaseCandidatesAsync(CancellationToken cancellationToken = default)
        {
            var candidates = new List<LicenseReleaseCandidate>();

            try
            {
                if (!IsEnabled)
                {
                    return candidates;
                }

                var currentSessions = await GetCurrentLicenseSessionsAsync(cancellationToken);
                var detectionResultsCopy = new Dictionary<string, IdleDetectionResult>();

                lock (_lock)
                {
                    detectionResultsCopy = new Dictionary<string, IdleDetectionResult>(_detectionResults);
                }

                foreach (var session in currentSessions)
                {
                    if (detectionResultsCopy.TryGetValue(session.SessionId, out var detectionResult))
                    {
                        if (ShouldReleaseLicense(detectionResult, session))
                        {
                            candidates.Add(new LicenseReleaseCandidate
                            {
                                SessionId = session.SessionId,
                                UserName = session.UserName,
                                Feature = session.Feature,
                                Server = session.Server,
                                Port = session.Port,
                                IdleTime = detectionResult.IdleDuration,
                                ConfidenceScore = detectionResult.ConfidenceScore,
                                DetectionMethods = detectionResult.DetectionMethods,
                                LastActivity = detectionResult.LastActivityTime,
                                Reason = GetReleaseReason(detectionResult, session)
                            });
                        }
                    }
                }

                return candidates.OrderBy(c => c.IdleTime).ToList();
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error getting release candidates: {ex.Message}");
                return candidates;
            }
        }

        /// <summary>
        /// Releases licenses for idle sessions
        /// </summary>
        public async Task<LicenseReleaseResult> ReleaseIdleLicensesAsync(CancellationToken cancellationToken = default)
        {
            var results = new LicenseReleaseResult
            {
                Success = true,
                ReleasedLicenses = new List<ReleasedLicenseInfo>(),
                FailedReleases = new List<FailedLicenseRelease>(),
                SkippedReleases = new List<SkippedLicenseRelease>()
            };

            try
            {
                if (!IsEnabled)
                {
                    results.Success = false;
                    results.ErrorMessage = "Idle detection is not enabled";
                    return results;
                }

                var candidates = await GetReleaseCandidatesAsync(cancellationToken);
                if (!candidates.Any())
                {
                    return results;
                }

                foreach (var candidate in candidates)
                {
                    var releaseResult = await ReleaseLicenseAsync(candidate, cancellationToken);
                    if (releaseResult.Success)
                    {
                        results.ReleasedLicenses.Add(releaseResult.ReleasedLicense);
                    }
                    else
                    {
                        results.FailedReleases.Add(new FailedLicenseRelease
                        {
                            Candidate = candidate,
                            ErrorMessage = releaseResult.ErrorMessage,
                            Exception = releaseResult.Exception
                        });
                    }
                }

                results.TotalCandidates = candidates.Count;
                results.TotalReleased = results.ReleasedLicenses.Count;
                results.TotalFailed = results.FailedReleases.Count;
                results.TotalSkipped = results.SkippedReleases.Count;

                return results;
            }
            catch (Exception ex)
            {
                results.Success = false;
                results.ErrorMessage = ex.Message;
                results.Exception = ex;
                return results;
            }
        }

        /// <summary>
        /// Gets license session statistics
        /// </summary>
        public async Task<LicenseSessionStatistics> GetSessionStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var currentSessions = await GetCurrentLicenseSessionsAsync(cancellationToken);
                var detectionResultsCopy = new Dictionary<string, IdleDetectionResult>();

                lock (_lock)
                {
                    detectionResultsCopy = new Dictionary<string, IdleDetectionResult>(_detectionResults);
                }

                var now = DateTime.UtcNow;
                var statistics = new LicenseSessionStatistics
                {
                    TotalSessions = currentSessions.Count,
                    ActiveSessions = currentSessions.Count(s => s.IsActive),
                    IdleSessions = currentSessions.Count(s => s.IsIdle),
                    MonitoredSessions = currentSessions.Count(s => detectionResultsCopy.ContainsKey(s.SessionId)),
                    TotalIdleTime = TimeSpan.Zero,
                    AverageIdleTime = TimeSpan.Zero,
                    LongestIdleTime = TimeSpan.Zero,
                    ConfidenceThreshold = _configurationIntegration?.ConfidenceThreshold ?? 0.7
                };

                if (statistics.MonitoredSessions > 0)
                {
                    var idleTimes = new List<TimeSpan>();
                    var confidenceScores = new List<double>();

                    foreach (var session in currentSessions)
                    {
                        if (detectionResultsCopy.TryGetValue(session.SessionId, out var result))
                        {
                            idleTimes.Add(result.IdleDuration);
                            confidenceScores.Add(result.ConfidenceScore);
                        }
                    }

                    if (idleTimes.Any())
                    {
                        statistics.TotalIdleTime = TimeSpan.FromSeconds(idleTimes.Sum(t => t.TotalSeconds));
                        statistics.AverageIdleTime = TimeSpan.FromSeconds(idleTimes.Average(t => t.TotalSeconds));
                        statistics.LongestIdleTime = idleTimes.Max();
                        statistics.AverageConfidenceScore = confidenceScores.Average();
                        statistics.MinConfidenceScore = confidenceScores.Min();
                        statistics.MaxConfidenceScore = confidenceScores.Max();
                    }
                }

                return statistics;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error getting session statistics: {ex.Message}");
                return new LicenseSessionStatistics();
            }
        }

        /// <summary>
        /// Validates license session compatibility with idle detection
        /// </summary>
        public async Task<LicenseSessionValidationResult> ValidateSessionCompatibilityAsync(
            LicenseUserSession session, CancellationToken cancellationToken = default)
        {
            var result = new LicenseSessionValidationResult
            {
                Session = session,
                IsValid = true,
                CompatibilityIssues = new List<string>()
            };

            try
            {
                // Check if session is active
                if (!session.IsActive)
                {
                    result.IsValid = false;
                    result.CompatibilityIssues.Add("Session is not active");
                }

                // Check if session has valid identifier
                if (string.IsNullOrWhiteSpace(session.SessionId))
                {
                    result.IsValid = false;
                    result.CompatibilityIssues.Add("Session ID is missing or invalid");
                }

                // Check if user is valid
                if (string.IsNullOrWhiteSpace(session.UserName))
                {
                    result.IsValid = false;
                    result.CompatibilityIssues.Add("Username is missing or invalid");
                }

                // Check if feature is valid
                if (string.IsNullOrWhiteSpace(session.Feature))
                {
                    result.IsValid = false;
                    result.CompatibilityIssues.Add("License feature is missing or invalid");
                }

                // Check if server is accessible
                try
                {
                    var serverAvailable = await _licenseManager.IsServerAvailableAsync(session.Server, session.Port, cancellationToken);
                    if (!serverAvailable)
                    {
                        result.CompatibilityIssues.Add($"License server {session.Server}:{session.Port} is not available");
                    }
                }
                catch (Exception ex)
                {
                    result.CompatibilityIssues.Add($"Error checking server availability: {ex.Message}");
                }

                // Check if session is already being monitored
                bool isMonitored;
                lock (_lock)
                {
                    isMonitored = _detectionResults.ContainsKey(session.SessionId);
                }

                if (isMonitored)
                {
                    result.CompatibilityIssues.Add("Session is already being monitored");
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.CompatibilityIssues.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Gets the current license sessions from the license server
        /// </summary>
        private async Task<List<LicenseUserSession>> GetCurrentLicenseSessionsAsync(CancellationToken cancellationToken)
        {
            var sessions = new List<LicenseUserSession>();

            try
            {
                // Get configuration
                var config = _configurationIntegration?.CurrentConfiguration;
                if (config == null)
                {
                    return sessions;
                }

                // Get users from license server
                var server = config.GetCustomParameter("licenseServer") ?? "localhost";
                var port = config.GetCustomParameter<int>("licensePort") ?? 27000;

                var users = await _licenseManager.GetUsersAsync(server, port, cancellationToken);
                if (!users.Any())
                {
                    return sessions;
                }

                // Convert to session objects
                foreach (var userUsage in users)
                {
                    foreach (var featureUsage in userUsage.FeatureUsages)
                    {
                        sessions.Add(new LicenseUserSession
                        {
                            SessionId = $"{userUsage.User}_{featureUsage.Feature}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                            UserName = userUsage.User,
                            Feature = featureUsage.Feature,
                            Server = server,
                            Port = port,
                            LoginTime = featureUsage.LoginTime,
                            LastActivity = featureUsage.LastActivity,
                            IsActive = featureUsage.IsActive,
                            IsIdle = featureUsage.IsIdle,
                            FeatureUsage = featureUsage
                        });
                    }
                }

                return sessions;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error getting current license sessions: {ex.Message}");
                return sessions;
            }
        }

        /// <summary>
        /// Processes a single idle detection result
        /// </summary>
        private async Task ProcessDetectionResultAsync(IdleDetectionResult result,
            IEnumerable<LicenseUserSession> currentSessions, CancellationToken cancellationToken)
        {
            try
            {
                // Find the corresponding session
                var session = currentSessions.FirstOrDefault(s => s.SessionId == result.SessionId);
                if (session == null)
                {
                    // Session not found, might have been released already
                    return;
                }

                // Update session state
                var oldState = session.Clone();
                session.LastActivity = result.LastActivityTime;
                session.IsIdle = result.IsIdle;

                // Check if we should release the license
                if (ShouldReleaseLicense(result, session))
                {
                    var releaseCandidate = new LicenseReleaseCandidate
                    {
                        SessionId = session.SessionId,
                        UserName = session.UserName,
                        Feature = session.Feature,
                        Server = session.Server,
                        Port = session.Port,
                        IdleTime = result.IdleDuration,
                        ConfidenceScore = result.ConfidenceScore,
                        DetectionMethods = result.DetectionMethods,
                        LastActivity = result.LastActivityTime,
                        Reason = GetReleaseReason(result, session)
                    };

                    var releaseResult = await ReleaseLicenseAsync(releaseCandidate, cancellationToken);

                    // Raise license release event
                    OnLicenseReleaseTriggered(new LicenseReleaseEventArgs(
                        releaseCandidate, releaseResult, DateTime.UtcNow));
                }

                // Raise session state change event
                OnSessionStateChanged(new LicenseSessionEventArgs(
                    session, oldState, result, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error processing detection result for session {result.SessionId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if a license should be released based on detection result
        /// </summary>
        private bool ShouldReleaseLicense(IdleDetectionResult result, LicenseUserSession session)
        {
            if (!session.IsActive || session.IsIdle)
            {
                return false; // Already inactive or idle
            }

            // Check confidence threshold
            var confidenceThreshold = _configurationIntegration?.ConfidenceThreshold ?? 0.7;
            if (result.ConfidenceScore < confidenceThreshold)
            {
                return false;
            }

            // Check idle duration
            var idleThreshold = _configurationIntegration?.DefaultIdleThreshold ?? TimeSpan.FromMinutes(15);
            if (result.IdleDuration < idleThreshold)
            {
                return false;
            }

            // Check if session is in a releasable state
            if (!session.FeatureUsage.CanBeReleased)
            {
                return false;
            }

            // Additional business logic checks can be added here
            return true;
        }

        /// <summary>
        /// Gets the reason for license release
        /// </summary>
        private string GetReleaseReason(IdleDetectionResult result, LicenseUserSession session)
        {
            var reasons = new List<string>();

            if (result.IsIdle)
            {
                reasons.Add($"Idle for {result.IdleDuration.TotalMinutes:F1} minutes");
            }

            if (result.ConfidenceScore < (_configurationIntegration?.ConfidenceThreshold ?? 0.7))
            {
                reasons.Add($"Low confidence detection ({result.ConfidenceScore:F2})");
            }

            if (result.DetectionMethods.Any())
            {
                reasons.Add($"Detected by: {string.Join(", ", result.DetectionMethods)}");
            }

            return reasons.Any() ? string.Join("; ", reasons) : "Idle detection";
        }

        /// <summary>
        /// Releases a license for a specific session
        /// </summary>
        private async Task<LicenseReleaseOperationResult> ReleaseLicenseAsync(
            LicenseReleaseCandidate candidate, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _licenseManager.ReleaseLicenseAsync(
                    candidate.Server, candidate.Port, candidate.Feature, candidate.UserName, cancellationToken);

                if (result.Success)
                {
                    // Remove from active sessions and detection results
                    lock (_lock)
                    {
                        _activeSessions.Remove(candidate.SessionId);
                        _detectionResults.Remove(candidate.SessionId);
                    }

                    return new LicenseReleaseOperationResult
                    {
                        Success = true,
                        ReleasedLicense = new ReleasedLicenseInfo
                        {
                            SessionId = candidate.SessionId,
                            UserName = candidate.UserName,
                            Feature = candidate.Feature,
                            Server = candidate.Server,
                            Port = candidate.Port,
                            ReleasedAt = DateTime.UtcNow,
                            IdleTime = candidate.IdleTime,
                            Reason = candidate.Reason
                        }
                    };
                }
                else
                {
                    return new LicenseReleaseOperationResult
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage ?? "License release failed"
                    };
                }
            }
            catch (Exception ex)
            {
                return new LicenseReleaseOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Initializes license monitoring
        /// </summary>
        private void InitializeLicenseMonitoring()
        {
            if (!IsEnabled)
            {
                return;
            }

            _licenseCheckTimer = new Timer(CheckLicensesCallback, null,
                TimeSpan.FromMinutes(1), _licenseCheckInterval);
        }

        /// <summary>
        /// Callback for periodic license checking
        /// </summary>
        private async void CheckLicensesCallback(object state)
        {
            try
            {
                await CheckLicensesAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error in license check callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Periodic license checking
        /// </summary>
        private async Task CheckLicensesAsync()
        {
            try
            {
                _lastLicenseCheck = DateTime.UtcNow;
                var cancellationToken = CancellationToken.None;

                // Get current sessions
                var currentSessions = await GetCurrentLicenseSessionsAsync(cancellationToken);

                // Update active sessions
                lock (_lock)
                {
                    _activeSessions = currentSessions.ToDictionary(s => s.SessionId);
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error checking licenses: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up stale sessions and detection results
        /// </summary>
        private void CleanupStaleData()
        {
            try
            {
                var now = DateTime.UtcNow;
                var staleThreshold = TimeSpan.FromHours(1); // Remove data older than 1 hour

                lock (_lock)
                {
                    // Clean up stale detection results
                    var staleResults = _detectionResults
                        .Where(kvp => now - kvp.Value.DetectionTime > staleThreshold)
                        .ToList();

                    foreach (var staleResult in staleResults)
                    {
                        _detectionResults.Remove(staleResult.Key);
                    }

                    // Clean up stale sessions
                    var staleSessions = _activeSessions
                        .Where(kvp => now - kvp.Value.LastActivity > staleThreshold)
                        .ToList();

                    foreach (var staleSession in staleSessions)
                    {
                        _activeSessions.Remove(staleSession.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Trace.WriteLine($"Error cleaning up stale data: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        private async Task LogErrorAsync(string message)
        {
            // In a real implementation, this would use the logging system
            System.Diagnostics.Trace.WriteLine($"[LicenseManagementIntegration Error] {message}");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Raises the LicenseReleaseTriggered event
        /// </summary>
        protected virtual void OnLicenseReleaseTriggered(LicenseReleaseEventArgs e)
        {
            LicenseReleaseTriggered?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the SessionStateChanged event
        /// </summary>
        protected virtual void OnSessionStateChanged(LicenseSessionEventArgs e)
        {
            SessionStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the IdleDetectionAffectedLicenseUsage event
        /// </summary>
        protected virtual void OnIdleDetectionAffectedLicenseUsage(IdleDetectionLicenseEventArgs e)
        {
            IdleDetectionAffectedLicenseUsage?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the license management integration
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the license management integration
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _licenseCheckTimer?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~LicenseManagementIntegration()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Represents a license user session
    /// </summary>
    public class LicenseUserSession
    {
        /// <summary>
        /// Gets or sets the session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the username
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the license feature
        /// </summary>
        public string Feature { get; set; }

        /// <summary>
        /// Gets or sets the license server
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the login time
        /// </summary>
        public DateTime LoginTime { get; set; }

        /// <summary>
        /// Gets or sets the last activity time
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets or sets whether the session is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets whether the session is idle
        /// </summary>
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets or sets the feature usage information
        /// </summary>
        public LicenseFeatureUsage FeatureUsage { get; set; }

        /// <summary>
        /// Gets the session duration
        /// </summary>
        public TimeSpan SessionDuration => DateTime.UtcNow - LoginTime;

        /// <summary>
        /// Gets the idle duration
        /// </summary>
        public TimeSpan IdleDuration => IsIdle ? DateTime.UtcNow - LastActivity : TimeSpan.Zero;

        /// <summary>
        /// Clones the session
        /// </summary>
        public LicenseUserSession Clone()
        {
            return new LicenseUserSession
            {
                SessionId = SessionId,
                UserName = UserName,
                Feature = Feature,
                Server = Server,
                Port = Port,
                LoginTime = LoginTime,
                LastActivity = LastActivity,
                IsActive = IsActive,
                IsIdle = IsIdle,
                FeatureUsage = FeatureUsage
            };
        }
    }

    /// <summary>
    /// Event arguments for license release events
    /// </summary>
    public class LicenseReleaseEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the release candidate
        /// </summary>
        public LicenseReleaseCandidate Candidate { get; }

        /// <summary>
        /// Gets the release result
        /// </summary>
        public LicenseReleaseOperationResult Result { get; }

        /// <summary>
        /// Gets the event timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseReleaseEventArgs class
        /// </summary>
        public LicenseReleaseEventArgs(LicenseReleaseCandidate candidate, LicenseReleaseOperationResult result, DateTime timestamp)
        {
            Candidate = candidate;
            Result = result;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Event arguments for session state changes
    /// </summary>
    public class LicenseSessionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the current session
        /// </summary>
        public LicenseUserSession CurrentSession { get; }

        /// <summary>
        /// Gets the previous session state
        /// </summary>
        public LicenseUserSession PreviousSession { get; }

        /// <summary>
        /// Gets the detection result that triggered the change
        /// </summary>
        public IdleDetectionResult DetectionResult { get; }

        /// <summary>
        /// Gets the event timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseSessionEventArgs class
        /// </summary>
        public LicenseSessionEventArgs(LicenseUserSession currentSession, LicenseUserSession previousSession,
            IdleDetectionResult detectionResult, DateTime timestamp)
        {
            CurrentSession = currentSession;
            PreviousSession = previousSession;
            DetectionResult = detectionResult;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Event arguments for idle detection affecting license usage
    /// </summary>
    public class IdleDetectionLicenseEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the detection results
        /// </summary>
        public IEnumerable<IdleDetectionResult> DetectionResults { get; }

        /// <summary>
        /// Gets the current sessions
        /// </summary>
        public IEnumerable<LicenseUserSession> CurrentSessions { get; }

        /// <summary>
        /// Gets the event timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the IdleDetectionLicenseEventArgs class
        /// </summary>
        public IdleDetectionLicenseEventArgs(IEnumerable<IdleDetectionResult> detectionResults,
            IEnumerable<LicenseUserSession> currentSessions, DateTime timestamp)
        {
            DetectionResults = detectionResults;
            CurrentSessions = currentSessions;
            Timestamp = timestamp;
        }
    }
}