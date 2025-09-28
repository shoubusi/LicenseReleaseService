using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Manages adaptive thresholds for activity detection with learning capabilities
    /// </summary>
    public class ActivityThresholdManager : IDisposable
    {
        private readonly ILogger<ActivityThresholdManager> _logger;
        private readonly TimeBasedDetectionConfig _config;
        private readonly object _lock = new object();
        private readonly Dictionary<string, UserThresholdData> _userThresholds = new Dictionary<string, UserThresholdData>();
        private readonly Queue<ActivityHistoryEntry> _activityHistory = new Queue<ActivityHistoryEntry>();
        private bool _isDisposed;

        /// <summary>
        /// Event raised when thresholds are adapted
        /// </summary>
        public event EventHandler<ThresholdAdaptedEventArgs> ThresholdsAdapted;

        /// <summary>
        /// Gets the current effective thresholds
        /// </summary>
        public EffectiveThresholds CurrentThresholds { get; private set; }

        /// <summary>
        /// Gets the adaptation statistics
        /// </summary>
        public AdaptationStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ActivityThresholdManager class
        /// </summary>
        public ActivityThresholdManager(ILogger<ActivityThresholdManager> logger, TimeBasedDetectionConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            CurrentThresholds = new EffectiveThresholds
            {
                WarningThreshold = config.WarningThreshold,
                ImminentThreshold = config.ImminentThreshold,
                CriticalThreshold = config.CriticalThreshold,
                Multiplier = 1.0,
                AdaptationFactor = 1.0
            };

            Statistics = new AdaptationStatistics();
        }

        /// <summary>
        /// Initializes the threshold manager
        /// </summary>
        public void Initialize()
        {
            lock (_lock)
            {
                _logger.LogInformation("Initializing activity threshold manager");
                Statistics = new AdaptationStatistics();
                ResetToDefaults();
            }
        }

        /// <summary>
        /// Processes activity data and adapts thresholds if needed
        /// </summary>
        public void ProcessActivity(ActivityData activity, string userName, string computerName)
        {
            if (!_config.EnableAdaptiveThresholds)
                return;

            lock (_lock)
            {
                try
                {
                    // Add to activity history
                    AddToActivityHistory(activity, userName, computerName);

                    // Get or create user threshold data
                    var userKey = GetUserKey(userName, computerName);
                    var userThreshold = GetUserThresholdData(userKey);

                    // Update user statistics
                    UpdateUserStatistics(userThreshold, activity);

                    // Check if adaptation is needed
                    if (ShouldAdaptThresholds(userThreshold))
                    {
                        AdaptThresholds(userThreshold);
                    }

                    // Update current effective thresholds
                    UpdateEffectiveThresholds(userKey);

                    // Cleanup old history entries
                    CleanupOldHistoryEntries();

                    Statistics.TotalActivitiesProcessed++;
                    Statistics.LastAdaptationTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing activity for threshold adaptation");
                    Statistics.AdaptationErrors++;
                }
            }
        }

        /// <summary>
        /// Gets the effective thresholds for a specific user
        /// </summary>
        public EffectiveThresholds GetUserThresholds(string userName, string computerName)
        {
            lock (_lock)
            {
                var userKey = GetUserKey(userName, computerName);
                var userThreshold = GetUserThresholdData(userKey);

                return CalculateEffectiveThresholds(userThreshold);
            }
        }

        /// <summary>
        /// Gets the detection level based on idle time
        /// </summary>
        public DetectionLevel GetDetectionLevel(TimeSpan idleTime, string userName, string computerName)
        {
            var thresholds = GetUserThresholds(userName, computerName);

            if (idleTime >= thresholds.CriticalThreshold)
                return DetectionLevel.Critical;
            if (idleTime >= thresholds.ImminentThreshold)
                return DetectionLevel.Imminent;
            if (idleTime >= thresholds.WarningThreshold)
                return DetectionLevel.Warning;

            return DetectionLevel.Active;
        }

        /// <summary>
        /// Applies hysteresis to prevent rapid state changes
        /// Summary comment
        public DetectionLevel ApplyHysteresis(DetectionLevel currentLevel, DetectionLevel newLevel, TimeSpan idleTime, string userName, string computerName)
        {
            if (!_config.EnableHysteresis)
                return newLevel;

            // Get user-specific hysteresis data
            var userKey = GetUserKey(userName, computerName);
            var userThreshold = GetUserThresholdData(userKey);

            // Only allow state changes with sufficient margin
            switch (currentLevel)
            {
                case DetectionLevel.Active:
                    // Allow transition to Warning normally
                    break;

                case DetectionLevel.Warning:
                    // Require exceeding Imminent threshold by hysteresis period
                    if (newLevel == DetectionLevel.Imminent)
                    {
                        var imminentWithHysteresis = userThreshold.CurrentImminentThreshold + _config.HysteresisPeriod;
                        if (idleTime < imminentWithHysteresis)
                            return DetectionLevel.Warning;
                    }
                    break;

                case DetectionLevel.Imminent:
                    // Require exceeding Critical threshold by hysteresis period
                    if (newLevel == DetectionLevel.Critical)
                    {
                        var criticalWithHysteresis = userThreshold.CurrentCriticalThreshold + _config.HysteresisPeriod;
                        if (idleTime < criticalWithHysteresis)
                            return DetectionLevel.Imminent;
                    }
                    break;

                case DetectionLevel.Critical:
                    // Require exceeding Release threshold by hysteresis period
                    if (newLevel == DetectionLevel.Release)
                    {
                        var releaseWithHysteresis = userThreshold.CurrentCriticalThreshold + _config.HysteresisPeriod;
                        if (idleTime < releaseWithHysteresis)
                            return DetectionLevel.Critical;
                    }
                    break;

                case DetectionLevel.Release:
                    // Stay in Release state until activity detected
                    break;
            }

            return newLevel;
        }

        /// <summary>
        /// Records a detection event for learning purposes
        /// </summary>
        public void RecordDetectionEvent(DetectionLevel level, TimeSpan idleTime, bool wasCorrect, string userName, string computerName)
        {
            if (!_config.EnableAdaptiveThresholds)
                return;

            lock (_lock)
            {
                var userKey = GetUserKey(userName, computerName);
                var userThreshold = GetUserThresholdData(userKey);

                // Update accuracy statistics
                switch (level)
                {
                    case DetectionLevel.Warning:
                        userThreshold.WarningAccuracy = CalculateRollingAverage(
                            userThreshold.WarningAccuracy, wasCorrect ? 1.0 : 0.0, 10);
                        break;
                    case DetectionLevel.Imminent:
                        userThreshold.ImminentAccuracy = CalculateRollingAverage(
                            userThreshold.ImminentAccuracy, wasCorrect ? 1.0 : 0.0, 10);
                        break;
                    case DetectionLevel.Critical:
                        userThreshold.CriticalAccuracy = CalculateRollingAverage(
                            userThreshold.CriticalAccuracy, wasCorrect ? 1.0 : 0.0, 10);
                        break;
                    case DetectionLevel.Release:
                        userThreshold.ReleaseAccuracy = CalculateRollingAverage(
                            userThreshold.ReleaseAccuracy, wasCorrect ? 1.0 : 0.0, 10);
                        break;
                }

                Statistics.TotalDetectionsRecorded++;
                if (wasCorrect)
                    Statistics.CorrectDetections++;
            }
        }

        /// <summary>
        /// Resets thresholds to default values for a user
        /// </summary>
        public void ResetUserThresholds(string userName, string computerName)
        {
            lock (_lock)
            {
                var userKey = GetUserKey(userName, computerName);
                if (_userThresholds.ContainsKey(userKey))
                {
                    _userThresholds.Remove(userKey);
                    _logger.LogInformation("Reset thresholds for user {UserKey}", userKey);
                }
            }
        }

        /// <summary>
        /// Gets adaptation statistics
        /// </summary>
        public AdaptationStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new AdaptationStatistics
                {
                    TotalAdaptations = Statistics.TotalAdaptations,
                    TotalActivitiesProcessed = Statistics.TotalActivitiesProcessed,
                    TotalDetectionsRecorded = Statistics.TotalDetectionsRecorded,
                    CorrectDetections = Statistics.CorrectDetections,
                    AdaptationErrors = Statistics.AdaptationErrors,
                    LastAdaptationTime = Statistics.LastAdaptationTime,
                    UserCount = _userThresholds.Count,
                    AverageAdaptationConfidence = Statistics.AverageAdaptationConfidence
                };
            }
        }

        /// <summary>
        /// Adds activity to history for learning
        /// </summary>
        private void AddToActivityHistory(ActivityData activity, string userName, string computerName)
        {
            var entry = new ActivityHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                ActivityType = activity.ActivityType,
                Confidence = activity.Confidence,
                UserName = userName,
                ComputerName = computerName,
                Metadata = new Dictionary<string, object>(activity.Metadata)
            };

            _activityHistory.Enqueue(entry);

            // Keep history manageable (last 24 hours)
            while (_activityHistory.Count > 10000)
            {
                _activityHistory.Dequeue();
            }
        }

        /// <summary>
        /// Gets or creates user threshold data
        /// </summary>
        private UserThresholdData GetUserThresholdData(string userKey)
        {
            if (!_userThresholds.TryGetValue(userKey, out var userThreshold))
            {
                userThreshold = new UserThresholdData
                {
                    UserKey = userKey,
                    CreatedTime = DateTime.UtcNow,
                    LastUpdateTime = DateTime.UtcNow,
                    CurrentWarningThreshold = _config.WarningThreshold,
                    CurrentImminentThreshold = _config.ImminentThreshold,
                    CurrentCriticalThreshold = _config.CriticalThreshold,
                    BaseWarningThreshold = _config.WarningThreshold,
                    BaseImminentThreshold = _config.ImminentThreshold,
                    BaseCriticalThreshold = _config.CriticalThreshold
                };

                _userThresholds[userKey] = userThreshold;
                _logger.LogDebug("Created new threshold data for user {UserKey}", userKey);
            }

            return userThreshold;
        }

        /// <summary>
        /// Updates user statistics based on activity
        /// </summary>
        private void UpdateUserStatistics(UserThresholdData userThreshold, ActivityData activity)
        {
            userThreshold.TotalActivities++;
            userThreshold.LastActivityTime = DateTime.UtcNow;

            // Update activity type statistics
            if (!userThreshold.ActivityTypeStats.ContainsKey(activity.ActivityType))
            {
                userThreshold.ActivityTypeStats[activity.ActivityType] = new ActivityTypeStatistics();
            }

            var typeStats = userThreshold.ActivityTypeStats[activity.ActivityType];
            typeStats.Count++;
            typeStats.TotalConfidence += activity.Confidence;
            typeStats.LastSeen = DateTime.UtcNow;

            // Update time-based statistics
            var hourOfDay = DateTime.Now.Hour;
            if (!userThreshold.HourlyActivity.ContainsKey(hourOfDay))
            {
                userThreshold.HourlyActivity[hourOfDay] = 0;
            }
            userThreshold.HourlyActivity[hourOfDay]++;

            // Update day of week statistics
            var dayOfWeek = DateTime.Now.DayOfWeek;
            if (!userThreshold.DailyActivity.ContainsKey(dayOfWeek))
            {
                userThreshold.DailyActivity[dayOfWeek] = 0;
            }
            userThreshold.DailyActivity[dayOfWeek]++;
        }

        /// <summary>
        /// Determines if thresholds should be adapted
        /// </summary>
        private bool ShouldAdaptThresholds(UserThresholdData userThreshold)
        {
            // Check if enough activities have been processed
            if (userThreshold.TotalActivities < 100)
                return false;

            // Check if enough time has passed since last adaptation
            var timeSinceLastAdaptation = DateTime.UtcNow - userThreshold.LastAdaptationTime;
            if (timeSinceLastAdaptation < TimeSpan.FromMinutes(30))
                return false;

            // Check if adaptation conditions are met
            var activityRate = CalculateActivityRate(userThreshold);
            var accuracyScore = CalculateAccuracyScore(userThreshold);

            // Adapt if activity patterns suggest threshold adjustment is needed
            return activityRate < 0.3 || accuracyScore < 0.7;
        }

        /// <summary>
        /// Adapts thresholds based on user behavior patterns
        /// </summary>
        private void AdaptThresholds(UserThresholdData userThreshold)
        {
            try
            {
                var oldThresholds = new EffectiveThresholds
                {
                    WarningThreshold = userThreshold.CurrentWarningThreshold,
                    ImminentThreshold = userThreshold.CurrentImminentThreshold,
                    CriticalThreshold = userThreshold.CurrentCriticalThreshold,
                    Multiplier = userThreshold.Multiplier,
                    AdaptationFactor = userThreshold.AdaptationFactor
                };

                // Calculate adaptation factors
                var activityFactor = CalculateActivityFactor(userThreshold);
                var accuracyFactor = CalculateAccuracyFactor(userThreshold);
                var timeMultiplier = _config.GetEffectiveThresholdMultiplier();

                // Apply learning rate
                var learningRate = _config.AdaptiveLearningRate;
                userThreshold.Multiplier = userThreshold.Multiplier * (1 - learningRate) +
                                          (activityFactor * accuracyFactor * timeMultiplier) * learningRate;

                // Clamp multiplier to allowed range
                userThreshold.Multiplier = Math.Clamp(userThreshold.Multiplier,
                    _config.MinAdaptiveThreshold, _config.MaxAdaptiveThreshold);

                // Apply multiplier to thresholds
                userThreshold.CurrentWarningThreshold = userThreshold.BaseWarningThreshold * userThreshold.Multiplier;
                userThreshold.CurrentImminentThreshold = userThreshold.BaseImminentThreshold * userThreshold.Multiplier;
                userThreshold.CurrentCriticalThreshold = userThreshold.BaseCriticalThreshold * userThreshold.Multiplier;

                userThreshold.LastAdaptationTime = DateTime.UtcNow;
                userThreshold.TotalAdaptations++;

                // Calculate adaptation confidence
                var adaptationConfidence = Math.Abs(userThreshold.Multiplier - oldThresholds.Multiplier) / oldThresholds.Multiplier;
                userThreshold.AdaptationFactor = adaptationConfidence;

                // Update statistics
                Statistics.TotalAdaptations++;
                Statistics.AverageAdaptationConfidence = CalculateRollingAverage(
                    Statistics.AverageAdaptationConfidence, adaptationConfidence, Statistics.TotalAdaptations);

                // Raise adaptation event
                OnThresholdsAdapted(userThreshold, oldThresholds);

                _logger.LogInformation("Adapted thresholds for user {UserKey}: Multiplier={Multiplier:F3}, " +
                    "Warning={Warning:F1}m, Imminent={Imminent:F1}m, Critical={Critical:F1}m",
                    userThreshold.UserKey, userThreshold.Multiplier,
                    userThreshold.CurrentWarningThreshold.TotalMinutes,
                    userThreshold.CurrentImminentThreshold.TotalMinutes,
                    userThreshold.CurrentCriticalThreshold.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adapting thresholds for user {UserKey}", userThreshold.UserKey);
            }
        }

        /// <summary>
        /// Updates current effective thresholds
        /// </summary>
        private void UpdateEffectiveThresholds(string userKey)
        {
            if (_userThresholds.TryGetValue(userKey, out var userThreshold))
            {
                CurrentThresholds = CalculateEffectiveThresholds(userThreshold);
            }
        }

        /// <summary>
        /// Calculates effective thresholds for a user
        /// </summary>
        private EffectiveThresholds CalculateEffectiveThresholds(UserThresholdData userThreshold)
        {
            var timeMultiplier = _config.GetEffectiveThresholdMultiplier();

            return new EffectiveThresholds
            {
                WarningThreshold = userThreshold.CurrentWarningThreshold * timeMultiplier,
                ImminentThreshold = userThreshold.CurrentImminentThreshold * timeMultiplier,
                CriticalThreshold = userThreshold.CurrentCriticalThreshold * timeMultiplier,
                Multiplier = userThreshold.Multiplier * timeMultiplier,
                AdaptationFactor = userThreshold.AdaptationFactor
            };
        }

        /// <summary>
        /// Cleans up old history entries
        /// </summary>
        private void CleanupOldHistoryEntries()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            while (_activityHistory.Count > 0 && _activityHistory.Peek().Timestamp < cutoffTime)
            {
                _activityHistory.Dequeue();
            }
        }

        /// <summary>
        /// Resets thresholds to default values
        /// </summary>
        private void ResetToDefaults()
        {
            CurrentThresholds = new EffectiveThresholds
            {
                WarningThreshold = _config.WarningThreshold,
                ImminentThreshold = _config.ImminentThreshold,
                CriticalThreshold = _config.CriticalThreshold,
                Multiplier = 1.0,
                AdaptationFactor = 1.0
            };
        }

        /// <summary>
        /// Calculates rolling average
        /// </summary>
        private double CalculateRollingAverage(double current, double newValue, int weight)
        {
            return (current * (weight - 1) + newValue) / weight;
        }

        /// <summary>
        /// Calculates activity rate for a user
        /// </summary>
        private double CalculateActivityRate(UserThresholdData userThreshold)
        {
            var timeSpan = DateTime.UtcNow - userThreshold.CreatedTime;
            var hours = timeSpan.TotalHours;

            if (hours <= 0)
                return 1.0;

            return userThreshold.TotalActivities / (hours * 60); // Activities per minute
        }

        /// <summary>
        /// Calculates accuracy score for a user
        /// </summary>
        private double CalculateAccuracyScore(UserThresholdData userThreshold)
        {
            var accuracies = new List<double>()
            {
                userThreshold.WarningAccuracy,
                userThreshold.ImminentAccuracy,
                userThreshold.CriticalAccuracy,
                userThreshold.ReleaseAccuracy
            };

            return accuracies.Where(a => a > 0).DefaultIfEmpty(1.0).Average();
        }

        /// <summary>
        /// Calculates activity factor for adaptation
        /// </summary>
        private double CalculateActivityFactor(UserThresholdData userThreshold)
        {
            var activityRate = CalculateActivityRate(userThreshold);

            // Lower activity rates should increase thresholds (longer idle times)
            if (activityRate < 0.1) // Very low activity
                return 1.5;
            if (activityRate < 0.5) // Low activity
                return 1.2;
            if (activityRate > 2.0) // High activity
                return 0.8;
            if (activityRate > 5.0) // Very high activity
                return 0.6;

            return 1.0;
        }

        /// <summary>
        /// Calculates accuracy factor for adaptation
        /// </summary>
        private double CalculateAccuracyFactor(UserThresholdData userThreshold)
        {
            var accuracyScore = CalculateAccuracyScore(userThreshold);

            // Lower accuracy should reduce multiplier (more conservative)
            if (accuracyScore < 0.5)
                return 0.7;
            if (accuracyScore < 0.7)
                return 0.85;
            if (accuracyScore > 0.9)
                return 1.1;

            return 1.0;
        }

        /// <summary>
        /// Gets user key for threshold storage
        /// </summary>
        private string GetUserKey(string userName, string computerName)
        {
            return $"{userName}@{computerName}";
        }

        /// <summary>
        /// Raises the ThresholdsAdapted event
        /// </summary>
        private void OnThresholdsAdapted(UserThresholdData userThreshold, EffectiveThresholds oldThresholds)
        {
            try
            {
                ThresholdsAdapted?.Invoke(this, new ThresholdAdaptedEventArgs
                {
                    UserKey = userThreshold.UserKey,
                    OldThresholds = oldThresholds,
                    NewThresholds = CalculateEffectiveThresholds(userThreshold),
                    AdaptationTimestamp = DateTime.UtcNow,
                    AdaptationReason = "Pattern-based adaptation",
                    Confidence = userThreshold.AdaptationFactor
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising ThresholdsAdapted event");
            }
        }

        /// <summary>
        /// Disposes the threshold manager
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the threshold manager
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _logger.LogInformation("Disposing activity threshold manager");
                    _userThresholds.Clear();
                    _activityHistory.Clear();
                }

                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Represents effective thresholds with multipliers
    /// </summary>
    public class EffectiveThresholds
    {
        public TimeSpan WarningThreshold { get; set; }
        public TimeSpan ImminentThreshold { get; set; }
        public TimeSpan CriticalThreshold { get; set; }
        public double Multiplier { get; set; }
        public double AdaptationFactor { get; set; }

        public EffectiveThresholds()
        {
            WarningThreshold = TimeSpan.FromMinutes(5);
            ImminentThreshold = TimeSpan.FromMinutes(10);
            CriticalThreshold = TimeSpan.FromMinutes(15);
            Multiplier = 1.0;
            AdaptationFactor = 1.0;
        }
    }

    /// <summary>
    /// Represents user-specific threshold data
    /// </summary>
    public class UserThresholdData
    {
        public string UserKey { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public DateTime LastAdaptationTime { get; set; }
        public long TotalActivities { get; set; }
        public long TotalAdaptations { get; set; }

        public TimeSpan BaseWarningThreshold { get; set; }
        public TimeSpan BaseImminentThreshold { get; set; }
        public TimeSpan BaseCriticalThreshold { get; set; }
        public TimeSpan CurrentWarningThreshold { get; set; }
        public TimeSpan CurrentImminentThreshold { get; set; }
        public TimeSpan CurrentCriticalThreshold { get; set; }

        public double Multiplier { get; set; }
        public double AdaptationFactor { get; set; }

        public double WarningAccuracy { get; set; }
        public double ImminentAccuracy { get; set; }
        public double CriticalAccuracy { get; set; }
        public double ReleaseAccuracy { get; set; }

        public Dictionary<ActivityType, ActivityTypeStatistics> ActivityTypeStats { get; set; }
        public Dictionary<int, int> HourlyActivity { get; set; }
        public Dictionary<DayOfWeek, int> DailyActivity { get; set; }

        public UserThresholdData()
        {
            CreatedTime = DateTime.UtcNow;
            LastUpdateTime = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            LastAdaptationTime = DateTime.UtcNow;
            Multiplier = 1.0;
            AdaptationFactor = 1.0;
            WarningAccuracy = 1.0;
            ImminentAccuracy = 1.0;
            CriticalAccuracy = 1.0;
            ReleaseAccuracy = 1.0;
            ActivityTypeStats = new Dictionary<ActivityType, ActivityTypeStatistics>();
            HourlyActivity = new Dictionary<int, int>();
            DailyActivity = new Dictionary<DayOfWeek, int>();
        }
    }

    /// <summary>
    /// Represents statistics for an activity type
    /// </summary>
    public class ActivityTypeStatistics
    {
        public long Count { get; set; }
        public double TotalConfidence { get; set; }
        public DateTime LastSeen { get; set; }

        public double AverageConfidence => Count > 0 ? TotalConfidence / Count : 0.0;
    }

    /// <summary>
    /// Represents an activity history entry
    /// </summary>
    public class ActivityHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public ActivityType ActivityType { get; set; }
        public double Confidence { get; set; }
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        public ActivityHistoryEntry()
        {
            Timestamp = DateTime.UtcNow;
            Confidence = 1.0;
            Metadata = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents adaptation statistics
    /// </summary>
    public class AdaptationStatistics
    {
        public long TotalAdaptations { get; set; }
        public long TotalActivitiesProcessed { get; set; }
        public long TotalDetectionsRecorded { get; set; }
        public long CorrectDetections { get; set; }
        public long AdaptationErrors { get; set; }
        public DateTime LastAdaptationTime { get; set; }
        public int UserCount { get; set; }
        public double AverageAdaptationConfidence { get; set; }

        public double AccuracyRate => TotalDetectionsRecorded > 0 ?
            (double)CorrectDetections / TotalDetectionsRecorded : 0.0;

        public AdaptationStatistics()
        {
            LastAdaptationTime = DateTime.UtcNow;
            AverageAdaptationConfidence = 1.0;
        }
    }

    /// <summary>
    /// Event arguments for threshold adaptation events
    /// </summary>
    public class ThresholdAdaptedEventArgs : EventArgs
    {
        public string UserKey { get; set; }
        public EffectiveThresholds OldThresholds { get; set; }
        public EffectiveThresholds NewThresholds { get; set; }
        public DateTime AdaptationTimestamp { get; set; }
        public string AdaptationReason { get; set; }
        public double Confidence { get; set; }
    }
}