using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using LicenseReleaseService.Models;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Configuration element for time-based idle detection settings
    /// </summary>
    public class TimeBasedDetectionConfig : ConfigurationElement
    {
        [ConfigurationProperty("detectionIntervalSeconds", DefaultValue = 60)]
        [System.Configuration.IntegerValidator(MinValue = 10, MaxValue = 3600)]
        public int DetectionIntervalSeconds
        {
            get { return (int)this["detectionIntervalSeconds"]; }
            set { this["detectionIntervalSeconds"] = value; }
        }

        [ConfigurationProperty("warningThresholdMinutes", DefaultValue = 5)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 120)]
        public int WarningThresholdMinutes
        {
            get { return (int)this["warningThresholdMinutes"]; }
            set { this["warningThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("imminentThresholdMinutes", DefaultValue = 10)]
        [System.Configuration.IntegerValidator(MinValue = 2, MaxValue = 240)]
        public int ImminentThresholdMinutes
        {
            get { return (int)this["imminentThresholdMinutes"]; }
            set { this["imminentThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("criticalThresholdMinutes", DefaultValue = 15)]
        [System.Configuration.IntegerValidator(MinValue = 5, MaxValue = 480)]
        public int CriticalThresholdMinutes
        {
            get { return (int)this["criticalThresholdMinutes"]; }
            set { this["criticalThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("workHourStart", DefaultValue = "09:00")]
        [StringValidator(MinLength = 5, MaxLength = 5)]
        public string WorkHourStart
        {
            get { return (string)this["workHourStart"]; }
            set { this["workHourStart"] = value; }
        }

        [ConfigurationProperty("workHourEnd", DefaultValue = "17:00")]
        [StringValidator(MinLength = 5, MaxLength = 5)]
        public string WorkHourEnd
        {
            get { return (string)this["workHourEnd"]; }
            set { this["workHourEnd"] = value; }
        }

        [ConfigurationProperty("workDayMultiplier", DefaultValue = 1.0)]
        [DoubleValidator(Minimum = 0.1, Maximum = 5.0)]
        public double WorkDayMultiplier
        {
            get { return (double)this["workDayMultiplier"]; }
            set { this["workDayMultiplier"] = value; }
        }

        [ConfigurationProperty("offHourMultiplier", DefaultValue = 0.5)]
        [DoubleValidator(Minimum = 0.1, Maximum = 5.0)]
        public double OffHourMultiplier
        {
            get { return (double)this["offHourMultiplier"]; }
            set { this["offHourMultiplier"] = value; }
        }

        [ConfigurationProperty("hysteresisMinutes", DefaultValue = 2)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 30)]
        public int HysteresisMinutes
        {
            get { return (int)this["hysteresisMinutes"]; }
            set { this["hysteresisMinutes"] = value; }
        }

        [ConfigurationProperty("confidenceThreshold", DefaultValue = 0.7)]
        [DoubleValidator(Minimum = 0.0, Maximum = 1.0)]
        public double ConfidenceThreshold
        {
            get { return (double)this["confidenceThreshold"]; }
            set { this["confidenceThreshold"] = value; }
        }

        [ConfigurationProperty("enableWorkHours", DefaultValue = true)]
        public bool EnableWorkHours
        {
            get { return (bool)this["enableWorkHours"]; }
            set { this["enableWorkHours"] = value; }
        }

        [ConfigurationProperty("enableAdaptiveThresholds", DefaultValue = true)]
        public bool EnableAdaptiveThresholds
        {
            get { return (bool)this["enableAdaptiveThresholds"]; }
            set { this["enableAdaptiveThresholds"] = value; }
        }

        [ConfigurationProperty("enableHysteresis", DefaultValue = true)]
        public bool EnableHysteresis
        {
            get { return (bool)this["enableHysteresis"]; }
            set { this["enableHysteresis"] = value; }
        }

        [ConfigurationProperty("enableKeyboardMonitoring", DefaultValue = true)]
        public bool EnableKeyboardMonitoring
        {
            get { return (bool)this["enableKeyboardMonitoring"]; }
            set { this["enableKeyboardMonitoring"] = value; }
        }

        [ConfigurationProperty("enableMouseMonitoring", DefaultValue = true)]
        public bool EnableMouseMonitoring
        {
            get { return (bool)this["enableMouseMonitoring"]; }
            set { this["enableMouseMonitoring"] = value; }
        }

        [ConfigurationProperty("enableSystemMonitoring", DefaultValue = true)]
        public bool EnableSystemMonitoring
        {
            get { return (bool)this["enableSystemMonitoring"]; }
            set { this["enableSystemMonitoring"] = value; }
        }

        [ConfigurationProperty("enableSolidWorksMonitoring", DefaultValue = true)]
        public bool EnableSolidWorksMonitoring
        {
            get { return (bool)this["enableSolidWorksMonitoring"]; }
            set { this["enableSolidWorksMonitoring"] = value; }
        }

        [ConfigurationProperty("monitoringSampleRateMs", DefaultValue = 100)]
        [System.Configuration.IntegerValidator(MinValue = 10, MaxValue = 1000)]
        public int MonitoringSampleRateMs
        {
            get { return (int)this["monitoringSampleRateMs"]; }
            set { this["monitoringSampleRateMs"] = value; }
        }

        [ConfigurationProperty("maxDetectionTimeMs", DefaultValue = 5000)]
        [System.Configuration.IntegerValidator(MinValue = 1000, MaxValue = 30000)]
        public int MaxDetectionTimeMs
        {
            get { return (int)this["maxDetectionTimeMs"]; }
            set { this["maxDetectionTimeMs"] = value; }
        }

        [ConfigurationProperty("adaptiveLearningRate", DefaultValue = 0.1)]
        [DoubleValidator(Minimum = 0.01, Maximum = 0.5)]
        public double AdaptiveLearningRate
        {
            get { return (double)this["adaptiveLearningRate"]; }
            set { this["adaptiveLearningRate"] = value; }
        }

        [ConfigurationProperty("minAdaptiveThreshold", DefaultValue = 0.1)]
        [DoubleValidator(Minimum = 0.01, Maximum = 1.0)]
        public double MinAdaptiveThreshold
        {
            get { return (double)this["minAdaptiveThreshold"]; }
            set { this["minAdaptiveThreshold"] = value; }
        }

        [ConfigurationProperty("maxAdaptiveThreshold", DefaultValue = 2.0)]
        [DoubleValidator(Minimum = 0.1, Maximum = 10.0)]
        public double MaxAdaptiveThreshold
        {
            get { return (double)this["maxAdaptiveThreshold"]; }
            set { this["maxAdaptiveThreshold"] = value; }
        }

        [ConfigurationProperty("enableGraduatedDetection", DefaultValue = true)]
        public bool EnableGraduatedDetection
        {
            get { return (bool)this["enableGraduatedDetection"]; }
            set { this["enableGraduatedDetection"] = value; }
        }

        [ConfigurationProperty("weekendMultiplier", DefaultValue = 0.3)]
        [DoubleValidator(Minimum = 0.1, Maximum = 5.0)]
        public double WeekendMultiplier
        {
            get { return (double)this["weekendMultiplier"]; }
            set { this["weekendMultiplier"] = value; }
        }

        [ConfigurationProperty("holidaysMultiplier", DefaultValue = 0.2)]
        [DoubleValidator(Minimum = 0.1, Maximum = 5.0)]
        public double HolidaysMultiplier
        {
            get { return (double)this["holidaysMultiplier"]; }
            set { this["holidaysMultiplier"] = value; }
        }

        /// <summary>
        /// Gets the warning threshold as TimeSpan
        /// </summary>
        public TimeSpan WarningThreshold => TimeSpan.FromMinutes(WarningThresholdMinutes);

        /// <summary>
        /// Gets the imminent threshold as TimeSpan
        /// </summary>
        public TimeSpan ImminentThreshold => TimeSpan.FromMinutes(ImminentThresholdMinutes);

        /// <summary>
        /// Gets the critical threshold as TimeSpan
        /// </summary>
        public TimeSpan CriticalThreshold => TimeSpan.FromMinutes(CriticalThresholdMinutes);

        /// <summary>
        /// Gets the hysteresis period as TimeSpan
        /// </summary>
        public TimeSpan HysteresisPeriod => TimeSpan.FromMinutes(HysteresisMinutes);

        /// <summary>
        /// Gets the detection interval as TimeSpan
        /// </summary>
        public TimeSpan DetectionInterval => TimeSpan.FromSeconds(DetectionIntervalSeconds);

        /// <summary>
        /// Gets the monitoring sample rate as TimeSpan
        /// </summary>
        public TimeSpan MonitoringSampleRate => TimeSpan.FromMilliseconds(MonitoringSampleRateMs);

        /// <summary>
        /// Validates the time-based detection configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate threshold progression
                if (WarningThresholdMinutes >= ImminentThresholdMinutes)
                {
                    errors.Add("Warning threshold must be less than imminent threshold");
                }

                if (ImminentThresholdMinutes >= CriticalThresholdMinutes)
                {
                    errors.Add("Imminent threshold must be less than critical threshold");
                }

                // Validate time formats
                if (!TimeSpan.TryParse(WorkHourStart, out _))
                {
                    errors.Add($"Invalid work hour start format: {WorkHourStart}. Expected format: HH:MM");
                }

                if (!TimeSpan.TryParse(WorkHourEnd, out _))
                {
                    errors.Add($"Invalid work hour end format: {WorkHourEnd}. Expected format: HH:MM");
                }

                // Validate multipliers
                if (WorkDayMultiplier <= 0 || OffHourMultiplier <= 0)
                {
                    errors.Add("Work day and off hour multipliers must be greater than zero");
                }

                if (WeekendMultiplier <= 0 || HolidaysMultiplier <= 0)
                {
                    errors.Add("Weekend and holidays multipliers must be greater than zero");
                }

                // Validate confidence threshold
                if (ConfidenceThreshold < 0.0 || ConfidenceThreshold > 1.0)
                {
                    errors.Add("Confidence threshold must be between 0.0 and 1.0");
                }

                // Validate adaptive thresholds
                if (MinAdaptiveThreshold <= 0 || MaxAdaptiveThreshold <= 0)
                {
                    errors.Add("Adaptive thresholds must be greater than zero");
                }

                if (MinAdaptiveThreshold >= MaxAdaptiveThreshold)
                {
                    errors.Add("Min adaptive threshold must be less than max adaptive threshold");
                }

                // Validate learning rate
                if (AdaptiveLearningRate <= 0 || AdaptiveLearningRate > 1.0)
                {
                    errors.Add("Adaptive learning rate must be between 0.01 and 1.0");
                }

                // Validate monitoring settings
                if (MonitoringSampleRateMs <= 0)
                {
                    errors.Add("Monitoring sample rate must be greater than zero");
                }

                if (MaxDetectionTimeMs <= 0)
                {
                    errors.Add("Max detection time must be greater than zero");
                }

                if (MaxDetectionTimeMs < MonitoringSampleRateMs)
                {
                    errors.Add("Max detection time must be greater than or equal to monitoring sample rate");
                }

                // Validate intervals
                if (DetectionIntervalSeconds <= 0)
                {
                    errors.Add("Detection interval must be greater than zero");
                }

                if (HysteresisMinutes < 0)
                {
                    errors.Add("Hysteresis minutes cannot be negative");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Time-based detection validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Gets the effective threshold multiplier based on current time
        /// </summary>
        /// <returns>Threshold multiplier for current time</returns>
        public double GetEffectiveThresholdMultiplier()
        {
            if (!EnableWorkHours)
            {
                return 1.0;
            }

            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            try
            {
                var workStart = TimeSpan.Parse(WorkHourStart);
                var workEnd = TimeSpan.Parse(WorkHourEnd);

                // Check if it's a weekend
                if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
                {
                    return WeekendMultiplier;
                }

                // Check if it's within work hours
                if (currentTime >= workStart && currentTime <= workEnd)
                {
                    return WorkDayMultiplier;
                }

                return OffHourMultiplier;
            }
            catch
            {
                // Default to 1.0 if time parsing fails
                return 1.0;
            }
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"TimeBasedDetection[Warning={WarningThresholdMinutes}m, Imminent={ImminentThresholdMinutes}m, Critical={CriticalThresholdMinutes}m, " +
                   $"WorkHours={EnableWorkHours}, Adaptive={EnableAdaptiveThresholds}, Hysteresis={EnableHysteresis}]";
        }

        /// <summary>
        /// Converts the configuration to an IdleDetectorConfiguration
        /// </summary>
        /// <returns>IdleDetectorConfiguration object</returns>
        public IdleDetectorConfiguration ToIdleDetectorConfiguration()
        {
            return new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = DetectionIntervalSeconds,
                IdleThresholdSeconds = WarningThresholdMinutes * 60,
                ConfidenceThreshold = ConfidenceThreshold,
                IsEnabled = true,
                Priority = 10,
                MaxDetectionTimeMs = MaxDetectionTimeMs,
                TimeoutSeconds = MaxDetectionTimeMs / 1000,
                RetryCount = 3,
                RetryDelayMs = 1000,
                MaxConcurrentOperations = 5,
                CustomParameters = new Dictionary<string, object>
                {
                    ["WarningThresholdMinutes"] = WarningThresholdMinutes,
                    ["ImminentThresholdMinutes"] = ImminentThresholdMinutes,
                    ["CriticalThresholdMinutes"] = CriticalThresholdMinutes,
                    ["WorkHourStart"] = WorkHourStart,
                    ["WorkHourEnd"] = WorkHourEnd,
                    ["WorkDayMultiplier"] = WorkDayMultiplier,
                    ["OffHourMultiplier"] = OffHourMultiplier,
                    ["HysteresisMinutes"] = HysteresisMinutes,
                    ["EnableWorkHours"] = EnableWorkHours,
                    ["EnableAdaptiveThresholds"] = EnableAdaptiveThresholds,
                    ["EnableHysteresis"] = EnableHysteresis,
                    ["EnableKeyboardMonitoring"] = EnableKeyboardMonitoring,
                    ["EnableMouseMonitoring"] = EnableMouseMonitoring,
                    ["EnableSystemMonitoring"] = EnableSystemMonitoring,
                    ["EnableSolidWorksMonitoring"] = EnableSolidWorksMonitoring,
                    ["MonitoringSampleRateMs"] = MonitoringSampleRateMs,
                    ["AdaptiveLearningRate"] = AdaptiveLearningRate,
                    ["MinAdaptiveThreshold"] = MinAdaptiveThreshold,
                    ["MaxAdaptiveThreshold"] = MaxAdaptiveThreshold,
                    ["EnableGraduatedDetection"] = EnableGraduatedDetection,
                    ["WeekendMultiplier"] = WeekendMultiplier,
                    ["HolidaysMultiplier"] = HolidaysMultiplier
                }
            };
        }
    }

    /// <summary>
    /// Defines the graduated detection levels
    /// </summary>
    public enum DetectionLevel
    {
        /// <summary>
        /// No idle time detected
        /// </summary>
        Active,

        /// <summary>
        /// Idle time approaching warning threshold
        /// </summary>
        Warning,

        /// <summary>
        /// Idle time exceeded warning threshold
        /// </summary>
        Imminent,

        /// <summary>
        /// Idle time exceeded imminent threshold
        /// </summary>
        Critical,

        /// <summary>
        /// Idle time exceeded critical threshold
        /// </summary>
        Release
    }

    /// <summary>
    /// Represents activity data for system monitoring
    /// </summary>
    public class TimeBasedActivityData
    {
        /// <summary>
        /// Gets the timestamp of the activity
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the type of activity detected
        /// </summary>
        public ActivityType ActivityType { get; set; }

        /// <summary>
        /// Gets the confidence level of the detection (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets additional metadata about the activity
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimeBasedActivityData class
        /// </summary>
        public TimeBasedActivityData()
        {
            Timestamp = DateTime.UtcNow;
            Confidence = 1.0;
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Returns a string representation of the activity data
        /// </summary>
        public override string ToString()
        {
            return $"Activity: {ActivityType}, Confidence: {Confidence:F2}, Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}";
        }
    }

    /// <summary>
    /// Defines the types of activity that can be monitored
    /// </summary>
    public enum TimeBasedActivityType
    {
        /// <summary>
        /// Keyboard input activity
        /// </summary>
        Keyboard,

        /// <summary>
        /// Mouse movement or click activity
        /// </summary>
        Mouse,

        /// <summary>
        /// System-wide activity (e.g., process activity)
        /// </summary>
        System,

        /// <summary>
        /// SolidWorks-specific activity
        /// </summary>
        SolidWorks,

        /// <summary>
        /// Window focus change activity
        /// </summary>
        WindowFocus,

        /// <summary>
        /// Power state change activity
        /// </summary>
        PowerState,

        /// <summary>
        /// Network activity
        /// </summary>
        Network,

        /// <summary>
        /// User session activity
        /// </summary>
        UserSession
    }
}