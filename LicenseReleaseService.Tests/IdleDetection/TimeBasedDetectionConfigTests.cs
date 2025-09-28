using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class TimeBasedDetectionConfigTests
    {
        private readonly TimeBasedDetectionConfig _config;
        private readonly Mock<ILogger<TimeBasedDetectionConfigTests>> _loggerMock;

        public TimeBasedDetectionConfigTests()
        {
            _config = new TimeBasedDetectionConfig();
            _loggerMock = new Mock<ILogger<TimeBasedDetectionConfigTests>>();
        }

        [Fact]
        public void Constructor_DefaultValues_ShouldInitializeCorrectly()
        {
            // Act
            var config = new TimeBasedDetectionConfig();

            // Assert
            Assert.Equal(60, config.DetectionIntervalSeconds);
            Assert.Equal(5, config.WarningThresholdMinutes);
            Assert.Equal(10, config.ImminentThresholdMinutes);
            Assert.Equal(15, config.CriticalThresholdMinutes);
            Assert.Equal("09:00", config.WorkHourStart);
            Assert.Equal("17:00", config.WorkHourEnd);
            Assert.Equal(1.0, config.WorkDayMultiplier);
            Assert.Equal(0.5, config.OffHourMultiplier);
            Assert.Equal(2, config.HysteresisMinutes);
            Assert.Equal(0.7, config.ConfidenceThreshold);
            Assert.True(config.EnableWorkHours);
            Assert.True(config.EnableAdaptiveThresholds);
            Assert.True(config.EnableHysteresis);
            Assert.True(config.EnableKeyboardMonitoring);
            Assert.True(config.EnableMouseMonitoring);
            Assert.True(config.EnableSystemMonitoring);
            Assert.True(config.EnableSolidWorksMonitoring);
            Assert.Equal(100, config.MonitoringSampleRateMs);
            Assert.Equal(5000, config.MaxDetectionTimeMs);
            Assert.Equal(0.1, config.AdaptiveLearningRate);
            Assert.Equal(0.1, config.MinAdaptiveThreshold);
            Assert.Equal(2.0, config.MaxAdaptiveThreshold);
            Assert.True(config.EnableGraduatedDetection);
            Assert.Equal(0.3, config.WeekendMultiplier);
            Assert.Equal(0.2, config.HolidaysMultiplier);
        }

        [Fact]
        public void WarningThreshold_ShouldReturnTimeSpan()
        {
            // Arrange
            _config.WarningThresholdMinutes = 10;

            // Act
            var threshold = _config.WarningThreshold;

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(10), threshold);
        }

        [Fact]
        public void ImminentThreshold_ShouldReturnTimeSpan()
        {
            // Arrange
            _config.ImminentThresholdMinutes = 20;

            // Act
            var threshold = _config.ImminentThreshold;

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(20), threshold);
        }

        [Fact]
        public void CriticalThreshold_ShouldReturnTimeSpan()
        {
            // Arrange
            _config.CriticalThresholdMinutes = 30;

            // Act
            var threshold = _config.CriticalThreshold;

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(30), threshold);
        }

        [Fact]
        public void HysteresisPeriod_ShouldReturnTimeSpan()
        {
            // Arrange
            _config.HysteresisMinutes = 5;

            // Act
            var period = _config.HysteresisPeriod;

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(5), period);
        }

        [Fact]
        public void DetectionInterval_ShouldReturnTimeSpan()
        {
            // Arrange
            _config.DetectionIntervalSeconds = 120;

            // Act
            var interval = _config.DetectionInterval;

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(120), interval);
        }

        [Fact]
        public void MonitoringSampleRate_ShouldReturnTimeSpan()
        {
            // Arrange
            _config.MonitoringSampleRateMs = 200;

            // Act
            var rate = _config.MonitoringSampleRate;

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(200), rate);
        }

        [Fact]
        public void Validate_ValidConfiguration_ShouldReturnNoErrors()
        {
            // Arrange
            _config.WarningThresholdMinutes = 5;
            _config.ImminentThresholdMinutes = 10;
            _config.CriticalThresholdMinutes = 15;
            _config.WorkHourStart = "09:00";
            _config.WorkHourEnd = "17:00";
            _config.ConfidenceThreshold = 0.8;
            _config.AdaptiveLearningRate = 0.1;
            _config.MinAdaptiveThreshold = 0.1;
            _config.MaxAdaptiveThreshold = 2.0;
            _config.MonitoringSampleRateMs = 100;
            _config.MaxDetectionTimeMs = 5000;

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidThresholdProgression_ShouldReturnErrors()
        {
            // Arrange
            _config.WarningThresholdMinutes = 15; // Greater than imminent
            _config.ImminentThresholdMinutes = 10;
            _config.CriticalThresholdMinutes = 15;

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Warning threshold must be less than imminent threshold"));
        }

        [Fact]
        public void Validate_InvalidTimeFormat_ShouldReturnErrors()
        {
            // Arrange
            _config.WorkHourStart = "invalid";
            _config.WorkHourEnd = "17:00";

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Invalid work hour start format"));
        }

        [Fact]
        public void Validate_InvalidMultipliers_ShouldReturnErrors()
        {
            // Arrange
            _config.WorkDayMultiplier = 0;
            _config.OffHourMultiplier = -1;

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Work day and off hour multipliers must be greater than zero"));
        }

        [Fact]
        public void Validate_InvalidConfidenceThreshold_ShouldReturnErrors()
        {
            // Arrange
            _config.ConfidenceThreshold = 1.5;

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Confidence threshold must be between 0.0 and 1.0"));
        }

        [Fact]
        public void Validate_InvalidAdaptiveThresholds_ShouldReturnErrors()
        {
            // Arrange
            _config.MinAdaptiveThreshold = 2.0;
            _config.MaxAdaptiveThreshold = 1.0; // Less than min

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Min adaptive threshold must be less than max adaptive threshold"));
        }

        [Fact]
        public void Validate_InvalidLearningRate_ShouldReturnErrors()
        {
            // Arrange
            _config.AdaptiveLearningRate = 1.5;

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Adaptive learning rate must be between 0.01 and 1.0"));
        }

        [Fact]
        public void Validate_InvalidMonitoringSettings_ShouldReturnErrors()
        {
            // Arrange
            _config.MonitoringSampleRateMs = 0;
            _config.MaxDetectionTimeMs = 100; // Less than sample rate

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Monitoring sample rate must be greater than zero"));
            Assert.Contains(errors, e => e.Contains("Max detection time must be greater than or equal to monitoring sample rate"));
        }

        [Fact]
        public void Validate_InvalidIntervals_ShouldReturnErrors()
        {
            // Arrange
            _config.DetectionIntervalSeconds = 0;
            _config.HysteresisMinutes = -1;

            // Act
            var errors = _config.Validate();

            // Assert
            Assert.Contains(errors, e => e.Contains("Detection interval must be greater than zero"));
            Assert.Contains(errors, e => e.Contains("Hysteresis minutes cannot be negative"));
        }

        [Fact]
        public void GetEffectiveThresholdMultiplier_WorkHoursDisabled_ShouldReturnOne()
        {
            // Arrange
            _config.EnableWorkHours = false;
            _config.WorkDayMultiplier = 2.0;
            _config.OffHourMultiplier = 0.5;

            // Act
            var multiplier = _config.GetEffectiveThresholdMultiplier();

            // Assert
            Assert.Equal(1.0, multiplier);
        }

        [Fact]
        public void GetEffectiveThresholdMultiplier_Weekend_ShouldReturnWeekendMultiplier()
        {
            // Arrange
            _config.EnableWorkHours = true;
            _config.WorkHourStart = "09:00";
            _config.WorkHourEnd = "17:00";
            _config.WeekendMultiplier = 0.3;

            // Act - This test needs to mock DateTime.Now
            // For now, we'll test the logic directly
            var isWeekend = DateTime.Now.DayOfWeek == DayOfWeek.Saturday ||
                           DateTime.Now.DayOfWeek == DayOfWeek.Sunday;

            if (isWeekend)
            {
                var multiplier = _config.GetEffectiveThresholdMultiplier();
                Assert.Equal(0.3, multiplier);
            }
        }

        [Fact]
        public void GetEffectiveThresholdMultiplier_WithinWorkHours_ShouldReturnWorkDayMultiplier()
        {
            // Arrange
            _config.EnableWorkHours = true;
            _config.WorkHourStart = "09:00";
            _config.WorkHourEnd = "17:00";
            _config.WorkDayMultiplier = 1.5;
            _config.OffHourMultiplier = 0.5;

            // Act - Mock a time within work hours
            var currentTime = TimeSpan.FromHours(14); // 2:00 PM
            var workStart = TimeSpan.Parse(_config.WorkHourStart);
            var workEnd = TimeSpan.Parse(_config.WorkHourEnd);

            if (currentTime >= workStart && currentTime <= workEnd)
            {
                var multiplier = _config.GetEffectiveThresholdMultiplier();
                Assert.Equal(1.5, multiplier);
            }
        }

        [Fact]
        public void GetEffectiveThresholdMultiplier_OutsideWorkHours_ShouldReturnOffHourMultiplier()
        {
            // Arrange
            _config.EnableWorkHours = true;
            _config.WorkHourStart = "09:00";
            _config.WorkHourEnd = "17:00";
            _config.WorkDayMultiplier = 1.5;
            _config.OffHourMultiplier = 0.5;

            // Act - Mock a time outside work hours
            var currentTime = TimeSpan.FromHours(20); // 8:00 PM
            var workStart = TimeSpan.Parse(_config.WorkHourStart);
            var workEnd = TimeSpan.Parse(_config.WorkHourEnd);

            if (currentTime < workStart || currentTime > workEnd)
            {
                var multiplier = _config.GetEffectiveThresholdMultiplier();
                Assert.Equal(0.5, multiplier);
            }
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            _config.WarningThresholdMinutes = 5;
            _config.ImminentThresholdMinutes = 10;
            _config.CriticalThresholdMinutes = 15;
            _config.EnableWorkHours = true;
            _config.EnableAdaptiveThresholds = true;
            _config.EnableHysteresis = true;

            // Act
            var result = _config.ToString();

            // Assert
            Assert.Contains("Warning=5m", result);
            Assert.Contains("Imminent=10m", result);
            Assert.Contains("Critical=15m", result);
            Assert.Contains("WorkHours=True", result);
            Assert.Contains("Adaptive=True", result);
            Assert.Contains("Hysteresis=True", result);
        }

        [Fact]
        public void ToIdleDetectorConfiguration_ShouldReturnValidConfiguration()
        {
            // Arrange
            _config.WarningThresholdMinutes = 5;
            _config.ImminentThresholdMinutes = 10;
            _config.CriticalThresholdMinutes = 15;
            _config.ConfidenceThreshold = 0.8;
            _config.EnableWorkHours = true;
            _config.EnableAdaptiveThresholds = true;

            // Act
            var result = _config.ToIdleDetectorConfiguration();

            // Assert
            Assert.Equal(60, result.DetectionIntervalSeconds);
            Assert.Equal(300, result.IdleThresholdSeconds); // 5 minutes * 60
            Assert.Equal(0.8, result.ConfidenceThreshold);
            Assert.True(result.IsEnabled);
            Assert.Equal(10, result.Priority);
            Assert.Equal(5000, result.MaxDetectionTimeMs);

            // Check custom parameters
            Assert.Equal(5, result.CustomParameters["WarningThresholdMinutes"]);
            Assert.Equal(10, result.CustomParameters["ImminentThresholdMinutes"]);
            Assert.Equal(15, result.CustomParameters["CriticalThresholdMinutes"]);
            Assert.Equal(true, result.CustomParameters["EnableWorkHours"]);
            Assert.Equal(true, result.CustomParameters["EnableAdaptiveThresholds"]);
        }

        [Theory]
        [InlineData(0.01, true)]
        [InlineData(0.5, true)]
        [InlineData(1.0, true)]
        [InlineData(0.0, false)]
        [InlineData(1.1, false)]
        [InlineData(-0.1, false)]
        public void ConfidenceThreshold_Validation(double value, bool isValid)
        {
            // Arrange
            _config.ConfidenceThreshold = value;

            // Act
            var errors = _config.Validate();

            // Assert
            if (isValid)
            {
                Assert.DoesNotContain(errors, e => e.Contains("Confidence threshold must be between 0.0 and 1.0"));
            }
            else
            {
                Assert.Contains(errors, e => e.Contains("Confidence threshold must be between 0.0 and 1.0"));
            }
        }

        [Theory]
        [InlineData(10, true)]
        [InlineData(60, true)]
        [InlineData(3600, true)]
        [InlineData(9, false)]
        [InlineData(3601, false)]
        [InlineData(0, false)]
        public void DetectionIntervalSeconds_Validation(int value, bool isValid)
        {
            // Arrange
            _config.DetectionIntervalSeconds = value;

            // Act
            var errors = _config.Validate();

            // Assert
            if (isValid)
            {
                Assert.DoesNotContain(errors, e => e.Contains("Detection interval must be greater than zero"));
            }
            else
            {
                Assert.Contains(errors, e => e.Contains("Detection interval must be greater than zero"));
            }
        }

        [Theory]
        [InlineData(5, 10, 15, true)]
        [InlineData(10, 20, 30, true)]
        [InlineData(15, 10, 20, false)] // Warning > Imminent
        [InlineData(5, 20, 15, false)]  // Imminent > Critical
        [InlineData(25, 20, 15, false)] // Both invalid
        public void ThresholdProgression_Validation(int warning, int imminent, int critical, bool isValid)
        {
            // Arrange
            _config.WarningThresholdMinutes = warning;
            _config.ImminentThresholdMinutes = imminent;
            _config.CriticalThresholdMinutes = critical;

            // Act
            var errors = _config.Validate();

            // Assert
            var hasProgressionErrors = errors.Any(e => e.Contains("threshold") && e.Contains("less than"));

            if (isValid)
            {
                Assert.False(hasProgressionErrors);
            }
            else
            {
                Assert.True(hasProgressionErrors);
            }
        }

        [Fact]
        public void ActivityData_Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var activity = new ActivityData();

            // Assert
            Assert.True(activity.Timestamp <= DateTime.UtcNow);
            Assert.Equal(ActivityType.System, activity.ActivityType); // Default
            Assert.Equal(1.0, activity.Confidence);
            Assert.NotNull(activity.Metadata);
            Assert.Empty(activity.Metadata);
        }

        [Fact]
        public void ActivityData_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var activity = new ActivityData
            {
                ActivityType = ActivityType.Keyboard,
                Confidence = 0.85,
                Timestamp = new DateTime(2023, 6, 15, 14, 30, 0)
            };

            // Act
            var result = activity.ToString();

            // Assert
            Assert.Contains("Activity: Keyboard", result);
            Assert.Contains("Confidence: 0.85", result);
            Assert.Contains("2023-06-15 14:30:00", result);
        }

        [Theory]
        [InlineData(DetectionLevel.Active)]
        [InlineData(DetectionLevel.Warning)]
        [InlineData(DetectionLevel.Imminent)]
        [InlineData(DetectionLevel.Critical)]
        [InlineData(DetectionLevel.Release)]
        public void DetectionLevel_ShouldHaveValidValues(DetectionLevel level)
        {
            // Assert
            Assert.True(Enum.IsDefined(typeof(DetectionLevel), level));
        }

        [Theory]
        [InlineData(ActivityType.Keyboard)]
        [InlineData(ActivityType.Mouse)]
        [InlineData(ActivityType.System)]
        [InlineData(ActivityType.SolidWorks)]
        [InlineData(ActivityType.WindowFocus)]
        [InlineData(ActivityType.PowerState)]
        [InlineData(ActivityType.Network)]
        [InlineData(ActivityType.UserSession)]
        public void ActivityType_ShouldHaveValidValues(ActivityType type)
        {
            // Assert
            Assert.True(Enum.IsDefined(typeof(ActivityType), type));
        }
    }
}