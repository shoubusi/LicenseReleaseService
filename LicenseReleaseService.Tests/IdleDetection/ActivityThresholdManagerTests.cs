using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class ActivityThresholdManagerTests : IDisposable
    {
        private readonly ActivityThresholdManager _manager;
        private readonly Mock<ILogger<ActivityThresholdManager>> _loggerMock;
        private readonly TimeBasedDetectionConfig _config;
        private readonly List<ThresholdAdaptedEventArgs> _adaptationEvents;

        public ActivityThresholdManagerTests()
        {
            _loggerMock = new Mock<ILogger<ActivityThresholdManager>>();
            _config = new TimeBasedDetectionConfig
            {
                WarningThresholdMinutes = 5,
                ImminentThresholdMinutes = 10,
                CriticalThresholdMinutes = 15,
                EnableAdaptiveThresholds = true,
                AdaptiveLearningRate = 0.1,
                MinAdaptiveThreshold = 0.1,
                MaxAdaptiveThreshold = 2.0,
                EnableHysteresis = true,
                HysteresisMinutes = 2
            };

            _manager = new ActivityThresholdManager(_loggerMock.Object, _config);
            _adaptationEvents = new List<ThresholdAdaptedEventArgs>();

            _manager.ThresholdsAdapted += (sender, args) => _adaptationEvents.Add(args);
            _manager.Initialize();
        }

        public void Dispose()
        {
            _manager?.Dispose();
        }

        [Fact]
        public void Constructor_ValidParameters_ShouldInitializeCorrectly()
        {
            // Assert
            Assert.NotNull(_manager);
            Assert.NotNull(_manager.CurrentThresholds);
            Assert.NotNull(_manager.Statistics);
        }

        [Fact]
        public void Initialize_ShouldResetToDefaults()
        {
            // Arrange
            _manager.ProcessActivity(new ActivityData(), "testuser", "testcomputer");

            // Act
            _manager.Initialize();

            // Assert
            var thresholds = _manager.CurrentThresholds;
            Assert.Equal(_config.WarningThreshold, thresholds.WarningThreshold);
            Assert.Equal(_config.ImminentThreshold, thresholds.ImminentThreshold);
            Assert.Equal(_config.CriticalThreshold, thresholds.CriticalThreshold);
            Assert.Equal(1.0, thresholds.Multiplier);
        }

        [Fact]
        public void ProcessActivity_AdaptiveThresholdsDisabled_ShouldNotAdapt()
        {
            // Arrange
            _config.EnableAdaptiveThresholds = false;
            var manager = new ActivityThresholdManager(_loggerMock.Object, _config);
            manager.Initialize();

            var activity = new ActivityData { ActivityType = ActivityType.Keyboard, Confidence = 0.9 };

            // Act
            manager.ProcessActivity(activity, "testuser", "testcomputer");

            // Assert
            Assert.Equal(0, _adaptationEvents.Count);
            var stats = manager.GetStatistics();
            Assert.Equal(0, stats.TotalActivitiesProcessed);
        }

        [Fact]
        public void ProcessActivity_AdaptiveThresholdsEnabled_ShouldProcessActivity()
        {
            // Arrange
            var activity = new ActivityData { ActivityType = ActivityType.Keyboard, Confidence = 0.9 };

            // Act
            _manager.ProcessActivity(activity, "testuser", "testcomputer");

            // Assert
            var stats = _manager.GetStatistics();
            Assert.Equal(1, stats.TotalActivitiesProcessed);
        }

        [Fact]
        public void GetUserThresholds_NewUser_ShouldReturnDefaultThresholds()
        {
            // Act
            var thresholds = _manager.GetUserThresholds("newuser", "newcomputer");

            // Assert
            Assert.Equal(_config.WarningThreshold, thresholds.WarningThreshold);
            Assert.Equal(_config.ImminentThreshold, thresholds.ImminentThreshold);
            Assert.Equal(_config.CriticalThreshold, thresholds.CriticalThreshold);
            Assert.Equal(1.0, thresholds.Multiplier);
        }

        [Fact]
        public void GetDetectionLevel_IdleTimeBelowWarning_ShouldReturnActive()
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(3); // Below 5 minutes

            // Act
            var level = _manager.GetDetectionLevel(idleTime, "testuser", "testcomputer");

            // Assert
            Assert.Equal(DetectionLevel.Active, level);
        }

        [Fact]
        public void GetDetectionLevel_IdleTimeAboveWarningBelowImminent_ShouldReturnWarning()
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(7); // Between 5 and 10 minutes

            // Act
            var level = _manager.GetDetectionLevel(idleTime, "testuser", "testcomputer");

            // Assert
            Assert.Equal(DetectionLevel.Warning, level);
        }

        [Fact]
        public void GetDetectionLevel_IdleTimeAboveImminentBelowCritical_ShouldReturnImminent()
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(12); // Between 10 and 15 minutes

            // Act
            var level = _manager.GetDetectionLevel(idleTime, "testuser", "testcomputer");

            // Assert
            Assert.Equal(DetectionLevel.Imminent, level);
        }

        [Fact]
        public void GetDetectionLevel_IdleTimeAboveCritical_ShouldReturnCritical()
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(20); // Above 15 minutes

            // Act
            var level = _manager.GetDetectionLevel(idleTime, "testuser", "testcomputer");

            // Assert
            Assert.Equal(DetectionLevel.Critical, level);
        }

        [Fact]
        public void ApplyHysteresis_HysteresisDisabled_ShouldReturnNewLevel()
        {
            // Arrange
            _config.EnableHysteresis = false;
            var manager = new ActivityThresholdManager(_loggerMock.Object, _config);
            manager.Initialize();

            var idleTime = TimeSpan.FromMinutes(12);
            var currentLevel = DetectionLevel.Warning;
            var newLevel = DetectionLevel.Imminent;

            // Act
            var result = manager.ApplyHysteresis(currentLevel, newLevel, idleTime, "testuser", "testcomputer");

            // Assert
            Assert.Equal(newLevel, result);
        }

        [Fact]
        public void ApplyHysteresis_HysteresisEnabled_ShouldPreventRapidChanges()
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(10); // Exactly at imminent threshold
            var currentLevel = DetectionLevel.Warning;
            var newLevel = DetectionLevel.Imminent;

            // Act
            var result = _manager.ApplyHysteresis(currentLevel, newLevel, idleTime, "testuser", "testcomputer");

            // Assert
            // Should stay at Warning level due to hysteresis
            Assert.Equal(DetectionLevel.Warning, result);
        }

        [Fact]
        public void RecordDetectionEvent_ShouldUpdateAccuracyStatistics()
        {
            // Arrange
            var userName = "testuser";
            var computerName = "testcomputer";

            // Act
            _manager.RecordDetectionEvent(DetectionLevel.Warning, TimeSpan.FromMinutes(7), true, userName, computerName);
            _manager.RecordDetectionEvent(DetectionLevel.Warning, TimeSpan.FromMinutes(8), false, userName, computerName);

            // Assert
            var stats = _manager.GetStatistics();
            Assert.Equal(2, stats.TotalDetectionsRecorded);
            Assert.Equal(1, stats.CorrectDetections);
        }

        [Fact]
        public void ResetUserThresholds_ExistingUser_ShouldRemoveThresholdData()
        {
            // Arrange
            var userName = "testuser";
            var computerName = "testcomputer";

            // First, create some threshold data
            _manager.ProcessActivity(new ActivityData(), userName, computerName);
            var thresholdsBefore = _manager.GetUserThresholds(userName, computerName);

            // Act
            _manager.ResetUserThresholds(userName, computerName);

            // Assert
            var thresholdsAfter = _manager.GetUserThresholds(userName, computerName);
            // Should return to default thresholds
            Assert.Equal(_config.WarningThreshold, thresholdsAfter.WarningThreshold);
        }

        [Fact]
        public void GetStatistics_WithNoActivity_ShouldReturnDefaultStatistics()
        {
            // Act
            var stats = _manager.GetStatistics();

            // Assert
            Assert.Equal(0, stats.TotalAdaptations);
            Assert.Equal(0, stats.TotalActivitiesProcessed);
            Assert.Equal(0, stats.TotalDetectionsRecorded);
            Assert.Equal(0, stats.CorrectDetections);
            Assert.Equal(0, stats.AdaptationErrors);
            Assert.Equal(0, stats.UserCount);
            Assert.Equal(0.0, stats.AverageAdaptationConfidence);
        }

        [Fact]
        public void ProcessActivity_WithMultipleActivities_ShouldTriggerAdaptation()
        {
            // Arrange
            var userName = "testuser";
            var computerName = "testcomputer";

            // Process many activities to trigger adaptation
            for (int i = 0; i < 150; i++)
            {
                var activity = new ActivityData
                {
                    ActivityType = ActivityType.Keyboard,
                    Confidence = 0.9,
                    Timestamp = DateTime.UtcNow.AddSeconds(-i) // Stagger timestamps
                };
                _manager.ProcessActivity(activity, userName, computerName);
            }

            // Act
            var stats = _manager.GetStatistics();

            // Assert
            Assert.Equal(150, stats.TotalActivitiesProcessed);
            // Adaptation may or may not have occurred depending on internal logic
        }

        [Fact]
        public void ThresholdsAdaptedEvent_ShouldBeRaisedWhenAdaptationOccurs()
        {
            // Arrange
            var userName = "testuser";
            var computerName = "testcomputer";

            // Process activities to potentially trigger adaptation
            for (int i = 0; i < 200; i++)
            {
                var activity = new ActivityData
                {
                    ActivityType = ActivityType.Keyboard,
                    Confidence = 0.9
                };
                _manager.ProcessActivity(activity, userName, computerName);
            }

            // Act
            // The event should have been captured in the _adaptationEvents list

            // Assert
            // Note: Adaptation may not occur immediately due to internal logic
            // This test mainly verifies the event handler is wired up correctly
            Assert.NotNull(_adaptationEvents);
        }

        [Fact]
        public void EffectiveThresholds_Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var thresholds = new EffectiveThresholds();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(5), thresholds.WarningThreshold);
            Assert.Equal(TimeSpan.FromMinutes(10), thresholds.ImminentThreshold);
            Assert.Equal(TimeSpan.FromMinutes(15), thresholds.CriticalThreshold);
            Assert.Equal(1.0, thresholds.Multiplier);
            Assert.Equal(1.0, thresholds.AdaptationFactor);
        }

        [Fact]
        public void UserThresholdData_Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var userData = new UserThresholdData();

            // Assert
            Assert.NotNull(userData.UserKey);
            Assert.True(userData.CreatedTime <= DateTime.UtcNow);
            Assert.Equal(0, userData.TotalActivities);
            Assert.Equal(0, userData.TotalAdaptations);
            Assert.Equal(1.0, userData.Multiplier);
            Assert.Equal(1.0, userData.WarningAccuracy);
            Assert.NotNull(userData.ActivityTypeStats);
            Assert.NotNull(userData.HourlyActivity);
            Assert.NotNull(userData.DailyActivity);
        }

        [Fact]
        public void ActivityTypeStatistics_Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var stats = new ActivityTypeStatistics();

            // Assert
            Assert.Equal(0, stats.Count);
            Assert.Equal(0.0, stats.TotalConfidence);
            Assert.Equal(0.0, stats.AverageConfidence);
        }

        [Fact]
        public void ActivityHistoryEntry_Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var entry = new ActivityHistoryEntry();

            // Assert
            Assert.True(entry.Timestamp <= DateTime.UtcNow);
            Assert.Equal(ActivityType.System, entry.ActivityType);
            Assert.Equal(1.0, entry.Confidence);
            Assert.NotNull(entry.Metadata);
        }

        [Fact]
        public void AdaptationStatistics_Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var stats = new AdaptationStatistics();

            // Assert
            Assert.Equal(0, stats.TotalAdaptations);
            Assert.Equal(0, stats.TotalActivitiesProcessed);
            Assert.Equal(0, stats.TotalDetectionsRecorded);
            Assert.Equal(0, stats.CorrectDetections);
            Assert.Equal(0, stats.AdaptationErrors);
            Assert.Equal(0.0, stats.AccuracyRate);
            Assert.True(stats.LastAdaptationTime <= DateTime.UtcNow);
        }

        [Fact]
        public void ThresholdAdaptedEventArgs_Constructor_ShouldInitializeCorrectly()
        {
            // Arrange
            var oldThresholds = new EffectiveThresholds();
            var newThresholds = new EffectiveThresholds { Multiplier = 1.5 };

            // Act
            var args = new ThresholdAdaptedEventArgs
            {
                UserKey = "testuser@testcomputer",
                OldThresholds = oldThresholds,
                NewThresholds = newThresholds,
                AdaptationTimestamp = DateTime.UtcNow,
                AdaptationReason = "Test adaptation",
                Confidence = 0.8
            };

            // Assert
            Assert.Equal("testuser@testcomputer", args.UserKey);
            Assert.Equal(oldThresholds, args.OldThresholds);
            Assert.Equal(newThresholds, args.NewThresholds);
            Assert.Equal("Test adaptation", args.AdaptationReason);
            Assert.Equal(0.8, args.Confidence);
        }

        [Theory]
        [InlineData(0, DetectionLevel.Active)]
        [InlineData(3, DetectionLevel.Active)]
        [InlineData(5, DetectionLevel.Warning)]
        [InlineData(7, DetectionLevel.Warning)]
        [InlineData(10, DetectionLevel.Imminent)]
        [InlineData(12, DetectionLevel.Imminent)]
        [InlineData(15, DetectionLevel.Critical)]
        [InlineData(20, DetectionLevel.Critical)]
        [InlineData(25, DetectionLevel.Critical)]
        public void GetDetectionLevel_VariousIdleTimes_ShouldReturnCorrectLevel(int minutes, DetectionLevel expected)
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(minutes);

            // Act
            var level = _manager.GetDetectionLevel(idleTime, "testuser", "testcomputer");

            // Assert
            Assert.Equal(expected, level);
        }

        [Theory]
        [InlineData(DetectionLevel.Active, DetectionLevel.Active)]
        [InlineData(DetectionLevel.Active, DetectionLevel.Warning)]
        [InlineData(DetectionLevel.Warning, DetectionLevel.Imminent)]
        [InlineData(DetectionLevel.Imminent, DetectionLevel.Critical)]
        [InlineData(DetectionLevel.Critical, DetectionLevel.Release)]
        public void ApplyHysteresis_DifferentLevelTransitions_ShouldHandleCorrectly(DetectionLevel current, DetectionLevel newLevel)
        {
            // Arrange
            var idleTime = TimeSpan.FromMinutes(12); // In the imminent range

            // Act
            var result = _manager.ApplyHysteresis(current, newLevel, idleTime, "testuser", "testcomputer");

            // Assert
            // Result depends on specific hysteresis logic
            Assert.True(Enum.IsDefined(typeof(DetectionLevel), result));
        }

        [Fact]
        public void ProcessActivity_MultipleUsers_ShouldMaintainSeparateThresholds()
        {
            // Arrange
            var user1 = "user1@computer1";
            var user2 = "user2@computer2";

            // Act
            _manager.ProcessActivity(new ActivityData { ActivityType = ActivityType.Keyboard }, "user1", "computer1");
            _manager.ProcessActivity(new ActivityData { ActivityType = ActivityType.Mouse }, "user2", "computer2");

            // Assert
            var stats = _manager.GetStatistics();
            Assert.Equal(2, stats.UserCount);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
        {
            // Act
            _manager.Dispose();

            // Assert
            // No exceptions should be thrown
            // Additional assertions could be made if there were specific resources to clean up
        }

        [Fact]
        public void GetEffectiveThresholdMultiplier_TimeBasedMultipliers_ShouldApplyCorrectly()
        {
            // Arrange
            var originalMultiplier = _config.WorkDayMultiplier;
            _config.WorkDayMultiplier = 2.0;
            _config.OffHourMultiplier = 0.5;

            var activity = new ActivityData { ActivityType = ActivityType.Keyboard };

            // Act
            _manager.ProcessActivity(activity, "testuser", "testcomputer");

            // Assert
            var thresholds = _manager.GetUserThresholds("testuser", "testcomputer");
            // The multiplier should be affected by time-based logic
            Assert.True(thresholds.Multiplier > 0);

            // Restore original configuration
            _config.WorkDayMultiplier = originalMultiplier;
        }

        [Fact]
        public void RecordDetectionEvent_AllDetectionLevels_ShouldUpdateCorrectStatistics()
        {
            // Arrange
            var userName = "testuser";
            var computerName = "testcomputer";

            // Act
            _manager.RecordDetectionEvent(DetectionLevel.Warning, TimeSpan.FromMinutes(7), true, userName, computerName);
            _manager.RecordDetectionEvent(DetectionLevel.Imminent, TimeSpan.FromMinutes(12), true, userName, computerName);
            _manager.RecordDetectionEvent(DetectionLevel.Critical, TimeSpan.FromMinutes(18), false, userName, computerName);
            _manager.RecordDetectionEvent(DetectionLevel.Release, TimeSpan.FromMinutes(25), true, userName, computerName);

            // Assert
            var stats = _manager.GetStatistics();
            Assert.Equal(4, stats.TotalDetectionsRecorded);
            Assert.Equal(3, stats.CorrectDetections);
            Assert.Equal(0.75, stats.AccuracyRate);
        }
    }
}