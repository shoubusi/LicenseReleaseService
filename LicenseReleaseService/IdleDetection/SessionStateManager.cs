using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Manages session state transitions with hysteresis to prevent state flapping
    /// </summary>
    public class SessionStateManager : IDisposable
    {
        private readonly object _lock = new object();
        private readonly ILogger<SessionStateManager> _logger;
        private readonly SessionStateConfiguration _configuration;
        private readonly ConcurrentDictionary<string, SessionStateEntry> _sessions;
        private readonly Timer _cleanupTimer;
        private bool _isDisposed;
        private bool _isInitialized;
        private long _totalStateTransitions;
        private long _suppressedTransitions;

        #region Events

        /// <summary>
        /// Event raised when session state changes
        /// </summary>
        public event EventHandler<SessionStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Event raised when session is created
        /// </summary>
        public event EventHandler<SessionCreatedEventArgs> SessionCreated;

        /// <summary>
        /// Event raised when session is removed
        /// </summary>
        public event EventHandler<SessionRemovedEventArgs> SessionRemoved;

        /// <summary>
        /// Event raised when state transition is suppressed due to hysteresis
        /// </summary>
        public event EventHandler<StateTransitionSuppressedEventArgs> StateTransitionSuppressed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether the manager is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the configuration
        /// </summary>
        public SessionStateConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets the number of active sessions
        /// </summary>
        public int SessionCount => _sessions.Count;

        /// <summary>
        /// Gets the total number of state transitions
        /// </summary>
        public long TotalStateTransitions => _totalStateTransitions;

        /// <summary>
        /// Gets the number of suppressed state transitions
        /// </summary>
        public long SuppressedTransitions => _suppressedTransitions;

        /// <summary>
        /// Gets the suppression rate
        /// </summary>
        public double SuppressionRate => _totalStateTransitions > 0 ?
            (double)_suppressedTransitions / _totalStateTransitions : 0.0;

        #endregion

        /// <summary>
        /// Initializes a new instance of the SessionStateManager class
        /// </summary>
        /// <param name="configuration">The configuration</param>
        /// <param name="logger">The logger</param>
        public SessionStateManager(
            SessionStateConfiguration configuration,
            ILogger<SessionStateManager> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _sessions = new ConcurrentDictionary<string, SessionStateEntry>();
            _cleanupTimer = new Timer(CleanupExpiredSessions, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Initializes the session state manager
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the initialization operation</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(SessionStateManager));

                if (_isInitialized)
                    return;
            }

            try
            {
                _logger.LogInformation("Initializing SessionStateManager");

                // Load any persisted session state if needed
                await LoadPersistedSessionsAsync(cancellationToken);

                _isInitialized = true;

                _logger.LogInformation("SessionStateManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SessionStateManager");
                throw;
            }
        }

        /// <summary>
        /// Gets or creates a session entry
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="processId">The process identifier</param>
        /// <param name="userName">The user name</param>
        /// <param name="computerName">The computer name</param>
        /// <returns>Session state entry</returns>
        public SessionStateEntry GetOrCreateSession(
            string sessionId,
            int processId,
            string userName,
            string computerName)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(computerName))
                throw new ArgumentNullException(nameof(computerName));

            return _sessions.GetOrAdd(sessionId, key =>
            {
                var entry = new SessionStateEntry
                {
                    SessionId = sessionId,
                    ProcessId = processId,
                    UserName = userName,
                    ComputerName = computerName,
                    State = SessionState.Active,
                    CreatedTime = DateTime.UtcNow,
                    LastStateChangeTime = DateTime.UtcNow,
                    LastActivityTime = DateTime.UtcNow
                };

                _logger.LogDebug("Created new session: {SessionId}", sessionId);
                OnSessionCreated(entry);

                return entry;
            });
        }

        /// <summary>
        /// Updates session state with hysteresis
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="newState">The desired new state</param>
        /// <param name="confidence">The confidence level of the state change</param>
        /// <param name="reason">The reason for the state change</param>
        /// <returns>True if the state was changed, false if suppressed</returns>
        public bool UpdateSessionState(
            string sessionId,
            SessionState newState,
            double confidence,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                _logger.LogWarning("Session not found: {SessionId}", sessionId);
                return false;
            }

            lock (entry.Lock)
            {
                return UpdateSessionStateInternal(entry, newState, confidence, reason);
            }
        }

        /// <summary>
        /// Records activity for a session
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="activityType">The type of activity</param>
        /// <param name="details">Additional activity details</param>
        public void RecordActivity(
            string sessionId,
            string activityType,
            string details = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                _logger.LogWarning("Session not found for activity recording: {SessionId}", sessionId);
                return;
            }

            lock (entry.Lock)
            {
                entry.LastActivityTime = DateTime.UtcNow;

                // Add activity record
                entry.ActivityHistory.Add(new ActivityRecord
                {
                    Timestamp = DateTime.UtcNow,
                    ActivityType = activityType,
                    Details = details
                });

                // Keep only recent activity history
                var cutoffTime = DateTime.UtcNow - _configuration.ActivityHistoryRetention;
                entry.ActivityHistory.RemoveAll(a => a.Timestamp < cutoffTime);

                // If session was idle, consider transitioning back to active
                if (entry.State != SessionState.Active)
                {
                    var idleDuration = DateTime.UtcNow - entry.LastActivityTime;
                    if (idleDuration < _configuration.ActiveThreshold)
                    {
                        UpdateSessionStateInternal(entry, SessionState.Active, 1.0,
                            $"Activity detected: {activityType}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current state of a session
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>Current session state, or null if session not found</returns>
        public SessionState? GetSessionState(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            return _sessions.TryGetValue(sessionId, out var entry) ? entry.State : (SessionState?)null;
        }

        /// <summary>
        /// Gets detailed session information
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>Session information, or null if session not found</returns>
        public SessionInfo GetSessionInfo(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (!_sessions.TryGetValue(sessionId, out var entry))
                return null;

            lock (entry.Lock)
            {
                return new SessionInfo
                {
                    SessionId = entry.SessionId,
                    ProcessId = entry.ProcessId,
                    UserName = entry.UserName,
                    ComputerName = entry.ComputerName,
                    State = entry.State,
                    CreatedTime = entry.CreatedTime,
                    LastStateChangeTime = entry.LastStateChangeTime,
                    LastActivityTime = entry.LastActivityTime,
                    StateChangeCount = entry.StateChangeCount,
                    ActivityHistory = entry.ActivityHistory.ToList(),
                    CurrentStateDuration = DateTime.UtcNow - entry.LastStateChangeTime
                };
            }
        }

        /// <summary>
        /// Gets all active sessions
        /// </summary>
        /// <returns>List of active session information</returns>
        public List<SessionInfo> GetActiveSessions()
        {
            return _sessions.Values
                .Where(s => s.State != SessionState.Inactive)
                .Select(s => GetSessionInfo(s.SessionId))
                .Where(info => info != null)
                .ToList();
        }

        /// <summary>
        /// Gets sessions in a specific state
        /// </summary>
        /// <param name="state">The session state to filter by</param>
        /// <returns>List of session information in the specified state</returns>
        public List<SessionInfo> GetSessionsByState(SessionState state)
        {
            return _sessions.Values
                .Where(s => s.State == state)
                .Select(s => GetSessionInfo(s.SessionId))
                .Where(info => info != null)
                .ToList();
        }

        /// <summary>
        /// Removes a session
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <param name="reason">The reason for removal</param>
        /// <returns>True if the session was removed, false if not found</returns>
        public bool RemoveSession(string sessionId, string reason)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (_sessions.TryRemove(sessionId, out var entry))
            {
                _logger.LogDebug("Removed session: {SessionId}, Reason: {Reason}", sessionId, reason);
                OnSessionRemoved(entry, reason);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes expired sessions
        /// </summary>
        public void RemoveExpiredSessions()
        {
            var expiredThreshold = DateTime.UtcNow - _configuration.SessionExpiration;
            var expiredSessions = _sessions
                .Where kvp => kvp.Value.LastActivityTime < expiredThreshold)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                RemoveSession(sessionId, "Session expired");
            }
        }

        /// <summary>
        /// Gets session statistics
        /// </summary>
        /// <returns>Session statistics</returns>
        public SessionStatistics GetStatistics()
        {
            var stateCounts = _sessions.Values
                .GroupBy(s => s.State)
                .ToDictionary(g => g.Key, g => g.Count());

            return new SessionStatistics
            {
                TotalSessions = _sessions.Count,
                StateCounts = stateCounts,
                TotalStateTransitions = _totalStateTransitions,
                SuppressedTransitions = _suppressedTransitions,
                SuppressionRate = SuppressionRate,
                AverageSessionDuration = CalculateAverageSessionDuration(),
                OldestSessionAge = CalculateOldestSessionAge()
            };
        }

        /// <summary>
        /// Clears all sessions
        /// </summary>
        /// <param name="reason">The reason for clearing</param>
        public void ClearAllSessions(string reason)
        {
            var sessionsToRemove = _sessions.Keys.ToList();
            foreach (var sessionId in sessionsToRemove)
            {
                RemoveSession(sessionId, reason);
            }
        }

        #region Private Methods

        private bool UpdateSessionStateInternal(
            SessionStateEntry entry,
            SessionState newState,
            double confidence,
            string reason)
        {
            // Check if state change is necessary
            if (entry.State == newState)
                return false;

            // Apply hysteresis logic
            if (ShouldSuppressTransition(entry, newState, confidence))
            {
                Interlocked.Increment(ref _suppressedTransitions);
                OnStateTransitionSuppressed(entry, newState, confidence, reason);
                return false;
            }

            // Record state transition
            var previousState = entry.State;
            entry.State = newState;
            entry.LastStateChangeTime = DateTime.UtcNow;
            entry.StateChangeCount++;

            // Add to state history
            entry.StateHistory.Add(new StateTransition
            {
                FromState = previousState,
                ToState = newState,
                Timestamp = DateTime.UtcNow,
                Confidence = confidence,
                Reason = reason
            });

            // Keep only recent state history
            var cutoffTime = DateTime.UtcNow - _configuration.StateHistoryRetention;
            entry.StateHistory.RemoveAll(t => t.Timestamp < cutoffTime);

            Interlocked.Increment(ref _totalStateTransitions);

            _logger.LogDebug("Session state changed: {SessionId}, {PreviousState} -> {NewState}, Reason: {Reason}",
                entry.SessionId, previousState, newState, reason);

            OnStateChanged(entry, previousState, newState, reason);
            return true;
        }

        private bool ShouldSuppressTransition(
            SessionStateEntry entry,
            SessionState newState,
            double confidence)
        {
            var timeSinceLastChange = DateTime.UtcNow - entry.LastStateChangeTime;
            var minimumHoldTime = GetMinimumHoldTime(entry.State);

            // Check if minimum hold time has elapsed
            if (timeSinceLastChange < minimumHoldTime)
            {
                return true;
            }

            // Check confidence threshold
            if (confidence < _configuration.ConfidenceThreshold)
            {
                return true;
            }

            // Check for state transition patterns that indicate flapping
            if (IsStateFlapping(entry))
            {
                return true;
            }

            return false;
        }

        private TimeSpan GetMinimumHoldTime(SessionState state)
        {
            return state switch
            {
                SessionState.Active => _configuration.ActiveStateHoldTime,
                SessionState.Idle => _configuration.IdleStateHoldTime,
                SessionState.LongIdle => _configuration.LongIdleStateHoldTime,
                SessionState.Inactive => _configuration.InactiveStateHoldTime,
                _ => _configuration.DefaultStateHoldTime
            };
        }

        private bool IsStateFlapping(SessionStateEntry entry)
        {
            var recentTransitions = entry.StateHistory
                .Where(t => DateTime.UtcNow - t.Timestamp < _configuration.FlappingDetectionWindow)
                .ToList();

            if (recentTransitions.Count < _configuration.MinimumFlappingTransitions)
                return false;

            // Check if there are rapid oscillations between states
            var stateChanges = recentTransitions.Count;
            var timeSpan = recentTransitions.Max(t => t.Timestamp) - recentTransitions.Min(t => t.Timestamp);
            var transitionRate = stateChanges / timeSpan.TotalMinutes;

            return transitionRate > _configuration.FlappingThreshold;
        }

        private TimeSpan CalculateAverageSessionDuration()
        {
            if (_sessions.Count == 0)
                return TimeSpan.Zero;

            var totalDuration = _sessions.Values
                .Sum(s => (DateTime.UtcNow - s.CreatedTime).TotalSeconds);

            return TimeSpan.FromSeconds(totalDuration / _sessions.Count);
        }

        private TimeSpan CalculateOldestSessionAge()
        {
            if (_sessions.Count == 0)
                return TimeSpan.Zero;

            var oldestSession = _sessions.Values
                .OrderBy(s => s.CreatedTime)
                .FirstOrDefault();

            return oldestSession != null ?
                DateTime.UtcNow - oldestSession.CreatedTime : TimeSpan.Zero;
        }

        private async Task LoadPersistedSessionsAsync(CancellationToken cancellationToken)
        {
            // TODO: Implement session persistence if needed
            // This could load session state from a database or file
            await Task.CompletedTask;
        }

        private void CleanupExpiredSessions(object state)
        {
            try
            {
                RemoveExpiredSessions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired sessions");
            }
        }

        #endregion

        #region Event Raisers

        protected virtual void OnStateChanged(
            SessionStateEntry entry,
            SessionState previousState,
            SessionState newState,
            string reason)
        {
            StateChanged?.Invoke(this, new SessionStateChangedEventArgs(
                entry.SessionId,
                entry.ProcessId,
                previousState,
                newState,
                reason));
        }

        protected virtual void OnSessionCreated(SessionStateEntry entry)
        {
            SessionCreated?.Invoke(this, new SessionCreatedEventArgs
            {
                SessionId = entry.SessionId,
                ProcessId = entry.ProcessId,
                UserName = entry.UserName,
                ComputerName = entry.ComputerName,
                Timestamp = DateTime.UtcNow
            });
        }

        protected virtual void OnSessionRemoved(SessionStateEntry entry, string reason)
        {
            SessionRemoved?.Invoke(this, new SessionRemovedEventArgs
            {
                SessionId = entry.SessionId,
                ProcessId = entry.ProcessId,
                UserName = entry.UserName,
                ComputerName = entry.ComputerName,
                Timestamp = DateTime.UtcNow,
                Reason = reason
            });
        }

        protected virtual void OnStateTransitionSuppressed(
            SessionStateEntry entry,
            SessionState intendedState,
            double confidence,
            string reason)
        {
            StateTransitionSuppressed?.Invoke(this, new StateTransitionSuppressedEventArgs
            {
                SessionId = entry.SessionId,
                ProcessId = entry.ProcessId,
                CurrentState = entry.State,
                IntendedState = intendedState,
                Confidence = confidence,
                Reason = reason,
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
                    _cleanupTimer?.Dispose();
                    ClearAllSessions("Manager disposed");
                }

                _isDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a session state entry
    /// </summary>
    public class SessionStateEntry
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
        /// Gets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets the current session state
        /// </summary>
        public SessionState State { get; set; }

        /// <summary>
        /// Gets the session creation time
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Gets the last state change time
        /// </summary>
        public DateTime LastStateChangeTime { get; set; }

        /// <summary>
        /// Gets the last activity time
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// Gets the number of state changes
        /// </summary>
        public int StateChangeCount { get; set; }

        /// <summary>
        /// Gets the state transition history
        /// </summary>
        public List<StateTransition> StateHistory { get; set; }

        /// <summary>
        /// Gets the activity history
        /// </summary>
        public List<ActivityRecord> ActivityHistory { get; set; }

        /// <summary>
        /// Gets the synchronization lock for the entry
        /// </summary>
        public object Lock { get; }

        /// <summary>
        /// Initializes a new instance of the SessionStateEntry class
        /// </summary>
        public SessionStateEntry()
        {
            StateHistory = new List<StateTransition>();
            ActivityHistory = new List<ActivityRecord>();
            Lock = new object();
            StateChangeCount = 0;
        }
    }

    /// <summary>
    /// Represents a state transition
    /// </summary>
    public class StateTransition
    {
        /// <summary>
        /// Gets the source state
        /// </summary>
        public SessionState FromState { get; set; }

        /// <summary>
        /// Gets the target state
        /// </summary>
        public SessionState ToState { get; set; }

        /// <summary>
        /// Gets the transition timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets the transition reason
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Represents an activity record
    /// </summary>
    public class ActivityRecord
    {
        /// <summary>
        /// Gets the activity timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the activity type
        /// </summary>
        public string ActivityType { get; set; }

        /// <summary>
        /// Gets additional activity details
        /// </summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// Represents session information
    /// </summary>
    public class SessionInfo
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
        /// Gets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets the session state
        /// </summary>
        public SessionState State { get; set; }

        /// <summary>
        /// Gets the session creation time
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Gets the last state change time
        /// </summary>
        public DateTime LastStateChangeTime { get; set; }

        /// <summary>
        /// Gets the last activity time
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// Gets the number of state changes
        /// </summary>
        public int StateChangeCount { get; set; }

        /// <summary>
        /// Gets the current state duration
        /// </summary>
        public TimeSpan CurrentStateDuration { get; set; }

        /// <summary>
        /// Gets the state transition history
        /// </summary>
        public List<StateTransition> StateHistory { get; set; }

        /// <summary>
        /// Gets the activity history
        /// </summary>
        public List<ActivityRecord> ActivityHistory { get; set; }

        /// <summary>
        /// Initializes a new instance of the SessionInfo class
        /// </summary>
        public SessionInfo()
        {
            StateHistory = new List<StateTransition>();
            ActivityHistory = new List<ActivityRecord>();
        }
    }

    /// <summary>
    /// Event arguments for session creation
    /// </summary>
    public class SessionCreatedEventArgs : EventArgs
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
        /// Gets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for session removal
    /// </summary>
    public class SessionRemovedEventArgs : EventArgs
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
        /// Gets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the reason for removal
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Event arguments for suppressed state transitions
    /// </summary>
    public class StateTransitionSuppressedEventArgs : EventArgs
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
        /// Gets the current state
        /// </summary>
        public SessionState CurrentState { get; set; }

        /// <summary>
        /// Gets the intended state
        /// </summary>
        public SessionState IntendedState { get; set; }

        /// <summary>
        /// Gets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets the reason for suppression
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents session statistics
    /// </summary>
    public class SessionStatistics
    {
        /// <summary>
        /// Gets the total number of sessions
        /// </summary>
        public int TotalSessions { get; set; }

        /// <summary>
        /// Gets the count of sessions by state
        /// </summary>
        public Dictionary<SessionState, int> StateCounts { get; set; }

        /// <summary>
        /// Gets the total number of state transitions
        /// </summary>
        public long TotalStateTransitions { get; set; }

        /// <summary>
        /// Gets the number of suppressed state transitions
        /// </summary>
        public long SuppressedTransitions { get; set; }

        /// <summary>
        /// Gets the suppression rate
        /// </summary>
        public double SuppressionRate { get; set; }

        /// <summary>
        /// Gets the average session duration
        /// </summary>
        public TimeSpan AverageSessionDuration { get; set; }

        /// <summary>
        /// Gets the oldest session age
        /// </summary>
        public TimeSpan OldestSessionAge { get; set; }

        /// <summary>
        /// Initializes a new instance of the SessionStatistics class
        /// </summary>
        public SessionStatistics()
        {
            StateCounts = new Dictionary<SessionState, int>();
        }
    }

    /// <summary>
    /// Configuration for session state management
    /// </summary>
    public class SessionStateConfiguration
    {
        /// <summary>
        /// Gets or sets the confidence threshold for state changes
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets the active threshold for considering a session active
        /// </summary>
        public TimeSpan ActiveThreshold { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the session expiration time
        /// </summary>
        public TimeSpan SessionExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the activity history retention period
        /// </summary>
        public TimeSpan ActivityHistoryRetention { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// Gets or sets the state history retention period
        /// </summary>
        public TimeSpan StateHistoryRetention { get; set; } = TimeSpan.FromHours(4);

        /// <summary>
        /// Gets or sets the minimum hold time for active state
        /// </summary>
        public TimeSpan ActiveStateHoldTime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the minimum hold time for idle state
        /// </summary>
        public TimeSpan IdleStateHoldTime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the minimum hold time for long idle state
        /// </summary>
        public TimeSpan LongIdleStateHoldTime { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the minimum hold time for inactive state
        /// </summary>
        public TimeSpan InactiveStateHoldTime { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the default minimum hold time
        /// </summary>
        public TimeSpan DefaultStateHoldTime { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the flapping detection window
        /// </summary>
        public TimeSpan FlappingDetectionWindow { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets or sets the minimum number of transitions to consider flapping
        /// </summary>
        public int MinimumFlappingTransitions { get; set; } = 5;

        /// <summary>
        /// Gets or sets the flapping threshold (transitions per minute)
        /// </summary>
        public double FlappingThreshold { get; set; } = 2.0;
    }
}