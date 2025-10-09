using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using LicenseReleaseService.Models;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Configuration element for idle detection settings
    /// </summary>
    public class IdleDetectionConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("enableIdleDetection", DefaultValue = true)]
        public bool EnableIdleDetection
        {
            get { return (bool)this["enableIdleDetection"]; }
            set { this["enableIdleDetection"] = value; }
        }

        // Missing property - alias for EnableIdleDetection
        public bool IsEnabled
        {
            get => EnableIdleDetection;
            set => EnableIdleDetection = value;
        }

        [ConfigurationProperty("detectionInterval", DefaultValue = 5)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 60)]
        public int DetectionInterval
        {
            get { return (int)this["detectionInterval"]; }
            set { this["detectionInterval"] = value; }
        }

        // Missing properties
        [ConfigurationProperty("detectionIntervalSeconds", DefaultValue = 300)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 3600)]
        public int DetectionIntervalSeconds
        {
            get { return (int)this["detectionIntervalSeconds"]; }
            set { this["detectionIntervalSeconds"] = value; }
        }

        [ConfigurationProperty("idleThresholdSeconds", DefaultValue = 1800)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 7200)]
        public int IdleThresholdSeconds
        {
            get { return (int)this["idleThresholdSeconds"]; }
            set { this["idleThresholdSeconds"] = value; }
        }

        [ConfigurationProperty("confidenceThreshold", DefaultValue = 0.7)]
        [DoubleValidator(Minimum = 0.0, Maximum = 1.0)]
        public double ConfidenceThreshold
        {
            get { return (double)this["confidenceThreshold"]; }
            set { this["confidenceThreshold"] = value; }
        }

        // Missing properties
        [ConfigurationProperty("priority", DefaultValue = 5)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 10)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        [ConfigurationProperty("maxDetectionTimeMs", DefaultValue = 30000)]
        [System.Configuration.IntegerValidator(MinValue = 1000, MaxValue = 300000)]
        public int MaxDetectionTimeMs
        {
            get { return (int)this["maxDetectionTimeMs"]; }
            set { this["maxDetectionTimeMs"] = value; }
        }

        [ConfigurationProperty("timeoutSeconds", DefaultValue = 30)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 300)]
        public int TimeoutSeconds
        {
            get { return (int)this["timeoutSeconds"]; }
            set { this["timeoutSeconds"] = value; }
        }

        // Additional missing properties
        [ConfigurationProperty("retryCount", DefaultValue = 3)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int RetryCount
        {
            get { return (int)this["retryCount"]; }
            set { this["retryCount"] = value; }
        }

        [ConfigurationProperty("retryDelayMs", DefaultValue = 1000)]
        [System.Configuration.IntegerValidator(MinValue = 100, MaxValue = 10000)]
        public int RetryDelayMs
        {
            get { return (int)this["retryDelayMs"]; }
            set { this["retryDelayMs"] = value; }
        }

        [ConfigurationProperty("maxConcurrentOperations", DefaultValue = 5)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 50)]
        public int MaxConcurrentOperations
        {
            get { return (int)this["maxConcurrentOperations"]; }
            set { this["maxConcurrentOperations"] = value; }
        }

        [ConfigurationProperty("customParameters", DefaultValue = "")]
        public string CustomParameters
        {
            get { return (string)this["customParameters"]; }
            set { this["customParameters"] = value; }
        }

        [ConfigurationProperty("detectors", DefaultValue = "")]
        public string Detectors
        {
            get { return (string)this["detectors"]; }
            set { this["detectors"] = value; }
        }

        [ConfigurationProperty("timeBasedDetection")]
        public TimeBasedDetectionElement TimeBasedDetection
        {
            get { return (TimeBasedDetectionElement)this["timeBasedDetection"] ?? new TimeBasedDetectionElement(); }
            set { this["timeBasedDetection"] = value; }
        }

        [ConfigurationProperty("pingBasedDetection")]
        public PingBasedDetectionElement PingBasedDetection
        {
            get { return (PingBasedDetectionElement)this["pingBasedDetection"] ?? new PingBasedDetectionElement(); }
            set { this["pingBasedDetection"] = value; }
        }

        [ConfigurationProperty("consensus")]
        public ConsensusElement Consensus
        {
            get { return (ConsensusElement)this["consensus"] ?? new ConsensusElement(); }
            set { this["consensus"] = value; }
        }

        [ConfigurationProperty("stateManagement")]
        public StateManagementElement StateManagement
        {
            get { return (StateManagementElement)this["stateManagement"] ?? new StateManagementElement(); }
            set { this["stateManagement"] = value; }
        }

        [ConfigurationProperty("activityMonitoring")]
        public ActivityMonitoringElement ActivityMonitoring
        {
            get { return (ActivityMonitoringElement)this["activityMonitoring"] ?? new ActivityMonitoringElement(); }
            set { this["activityMonitoring"] = value; }
        }

        /// <summary>
        /// Gets or sets the detector name
        /// </summary>
        public string DetectorName { get; set; } = "IdleDetection";

        /// <summary>
        /// Validates the idle detection configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate detection interval
                if (DetectionInterval < 1 || DetectionInterval > 60)
                {
                    errors.Add("Detection interval must be between 1 and 60 minutes");
                }

                // Validate time-based detection configuration
                errors.AddRange(TimeBasedDetection.Validate());

                // Validate ping-based detection configuration
                errors.AddRange(PingBasedDetection.Validate());

                // Validate consensus configuration
                errors.AddRange(Consensus.Validate());

                // Validate state management configuration
                errors.AddRange(StateManagement.Validate());

                // Validate activity monitoring configuration
                errors.AddRange(ActivityMonitoring.Validate());

                // Validate configuration consistency
                if (TimeBasedDetection.IdleThresholdMinutes > StateManagement.MaxIdleDurationMinutes)
                {
                    errors.Add("Time-based idle threshold cannot exceed maximum idle duration");
                }

                if (PingBasedDetection.PingTimeoutMs > TimeBasedDetection.IdleThresholdMinutes * 60 * 1000)
                {
                    errors.Add("Ping timeout should be less than idle threshold");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Idle detection validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Gets a custom parameter value from the CustomParameters string
        /// </summary>
        /// <typeparam name="T">The type of the parameter</typeparam>
        /// <param name="key">The parameter key</param>
        /// <param name="defaultValue">The default value if not found</param>
        /// <returns>The parameter value or default</returns>
        public T GetCustomParameter<T>(string key, T defaultValue = default(T))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CustomParameters))
                {
                    return defaultValue;
                }

                var parameters = CustomParameters.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var param in parameters)
                {
                    var keyValue = param.Split(new[] { '=' }, 2);
                    if (keyValue.Length == 2 && keyValue[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        var value = keyValue[1].Trim();
                        if (typeof(T) == typeof(string))
                        {
                            return (T)(object)value;
                        }
                        if (typeof(T) == typeof(int) && int.TryParse(value, out var intValue))
                        {
                            return (T)(object)intValue;
                        }
                        if (typeof(T) == typeof(double) && double.TryParse(value, out var doubleValue))
                        {
                            return (T)(object)doubleValue;
                        }
                        if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolValue))
                        {
                            return (T)(object)boolValue;
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors and return default value
            }

            return defaultValue;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"IdleDetection[Enabled={EnableIdleDetection}, Interval={DetectionInterval}m, TimeBased={TimeBasedDetection}, PingBased={PingBasedDetection}]";
        }
    }

    /// <summary>
    /// Configuration element for time-based detection settings
    /// </summary>
    public class TimeBasedDetectionElement : ConfigurationElement
    {
        [ConfigurationProperty("idleThresholdMinutes", DefaultValue = 30)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 480)]
        public int IdleThresholdMinutes
        {
            get { return (int)this["idleThresholdMinutes"]; }
            set { this["idleThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("warningThresholdMinutes", DefaultValue = 20)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 480)]
        public int WarningThresholdMinutes
        {
            get { return (int)this["warningThresholdMinutes"]; }
            set { this["warningThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("workHourThresholdMinutes", DefaultValue = 15)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 480)]
        public int WorkHourThresholdMinutes
        {
            get { return (int)this["workHourThresholdMinutes"]; }
            set { this["workHourThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("offHourThresholdMinutes", DefaultValue = 45)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 480)]
        public int OffHourThresholdMinutes
        {
            get { return (int)this["offHourThresholdMinutes"]; }
            set { this["offHourThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("enableAdaptiveDetection", DefaultValue = true)]
        public bool EnableAdaptiveDetection
        {
            get { return (bool)this["enableAdaptiveDetection"]; }
            set { this["enableAdaptiveDetection"] = value; }
        }

        [ConfigurationProperty("workHoursStart", DefaultValue = "09:00")]
        [StringValidator(MinLength = 1)]
        public string WorkHoursStart
        {
            get { return (string)this["workHoursStart"]; }
            set { this["workHoursStart"] = value; }
        }

        [ConfigurationProperty("workHoursEnd", DefaultValue = "17:00")]
        [StringValidator(MinLength = 1)]
        public string WorkHoursEnd
        {
            get { return (string)this["workHoursEnd"]; }
            set { this["workHoursEnd"] = value; }
        }

        [ConfigurationProperty("enableWeekendDetection", DefaultValue = true)]
        public bool EnableWeekendDetection
        {
            get { return (bool)this["enableWeekendDetection"]; }
            set { this["enableWeekendDetection"] = value; }
        }

        [ConfigurationProperty("weekendMultiplier", DefaultValue = 1.5)]
        [DoubleValidator(Minimum = 1.0, Maximum = 3.0)]
        public double WeekendMultiplier
        {
            get { return (double)this["weekendMultiplier"]; }
            set { this["weekendMultiplier"] = value; }
        }

        /// <summary>
        /// Validates the time-based detection configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate threshold relationships
                if (WarningThresholdMinutes >= IdleThresholdMinutes)
                {
                    errors.Add("Warning threshold must be less than idle threshold");
                }

                if (WorkHourThresholdMinutes >= IdleThresholdMinutes)
                {
                    errors.Add("Work hour threshold must be less than idle threshold");
                }

                if (OffHourThresholdMinutes <= IdleThresholdMinutes)
                {
                    errors.Add("Off hour threshold must be greater than idle threshold");
                }

                // Validate work hours format
                if (!TimeSpan.TryParse(WorkHoursStart, out _))
                {
                    errors.Add($"Invalid work hours start format: {WorkHoursStart}");
                }

                if (!TimeSpan.TryParse(WorkHoursEnd, out _))
                {
                    errors.Add($"Invalid work hours end format: {WorkHoursEnd}");
                }

                // Validate weekend multiplier
                if (WeekendMultiplier < 1.0 || WeekendMultiplier > 3.0)
                {
                    errors.Add("Weekend multiplier must be between 1.0 and 3.0");
                }

                // Validate adaptive detection consistency
                if (EnableAdaptiveDetection && (WorkHourThresholdMinutes == IdleThresholdMinutes))
                {
                    errors.Add("Adaptive detection requires different work hour and off hour thresholds");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Time-based detection validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"TimeBased[Idle={IdleThresholdMinutes}m, Warning={WarningThresholdMinutes}m, WorkHours={WorkHourThresholdMinutes}m, OffHours={OffHourThresholdMinutes}m]";
        }
    }

    /// <summary>
    /// Configuration element for ping-based detection settings
    /// </summary>
    public class PingBasedDetectionElement : ConfigurationElement
    {
        [ConfigurationProperty("pingTimeoutMs", DefaultValue = 5000)]
        [System.Configuration.IntegerValidator(MinValue = 1000, MaxValue = 30000)]
        public int PingTimeoutMs
        {
            get { return (int)this["pingTimeoutMs"]; }
            set { this["pingTimeoutMs"] = value; }
        }

        [ConfigurationProperty("documentActivityThresholdMinutes", DefaultValue = 10)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 480)]
        public int DocumentActivityThresholdMinutes
        {
            get { return (int)this["documentActivityThresholdMinutes"]; }
            set { this["documentActivityThresholdMinutes"] = value; }
        }

        [ConfigurationProperty("networkActivityThresholdKb", DefaultValue = 1024)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 10240)]
        public int NetworkActivityThresholdKb
        {
            get { return (int)this["networkActivityThresholdKb"]; }
            set { this["networkActivityThresholdKb"] = value; }
        }

        [ConfigurationProperty("enableNetworkMonitoring", DefaultValue = true)]
        public bool EnableNetworkMonitoring
        {
            get { return (bool)this["enableNetworkMonitoring"]; }
            set { this["enableNetworkMonitoring"] = value; }
        }

        [ConfigurationProperty("enableDocumentMonitoring", DefaultValue = true)]
        public bool EnableDocumentMonitoring
        {
            get { return (bool)this["enableDocumentMonitoring"]; }
            set { this["enableDocumentMonitoring"] = value; }
        }

        [ConfigurationProperty("enableProcessHealthMonitoring", DefaultValue = true)]
        public bool EnableProcessHealthMonitoring
        {
            get { return (bool)this["enableProcessHealthMonitoring"]; }
            set { this["enableProcessHealthMonitoring"] = value; }
        }

        [ConfigurationProperty("maxPingRetries", DefaultValue = 3)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 10)]
        public int MaxPingRetries
        {
            get { return (int)this["maxPingRetries"]; }
            set { this["maxPingRetries"] = value; }
        }

        [ConfigurationProperty("pingRetryDelayMs", DefaultValue = 1000)]
        [System.Configuration.IntegerValidator(MinValue = 100, MaxValue = 10000)]
        public int PingRetryDelayMs
        {
            get { return (int)this["pingRetryDelayMs"]; }
            set { this["pingRetryDelayMs"] = value; }
        }

        [ConfigurationProperty("processResponseThresholdMs", DefaultValue = 3000)]
        [System.Configuration.IntegerValidator(MinValue = 500, MaxValue = 15000)]
        public int ProcessResponseThresholdMs
        {
            get { return (int)this["processResponseThresholdMs"]; }
            set { this["processResponseThresholdMs"] = value; }
        }

        /// <summary>
        /// Validates the ping-based detection configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate timeout settings
                if (PingTimeoutMs < 1000 || PingTimeoutMs > 30000)
                {
                    errors.Add("Ping timeout must be between 1000ms and 30000ms");
                }

                if (ProcessResponseThresholdMs > PingTimeoutMs)
                {
                    errors.Add("Process response threshold must be less than or equal to ping timeout");
                }

                // Validate retry settings
                if (MaxPingRetries < 1 || MaxPingRetries > 10)
                {
                    errors.Add("Max ping retries must be between 1 and 10");
                }

                if (PingRetryDelayMs < 100 || PingRetryDelayMs > 10000)
                {
                    errors.Add("Ping retry delay must be between 100ms and 10000ms");
                }

                // Validate threshold settings
                if (DocumentActivityThresholdMinutes < 1 || DocumentActivityThresholdMinutes > 480)
                {
                    errors.Add("Document activity threshold must be between 1 and 480 minutes");
                }

                if (NetworkActivityThresholdKb < 1 || NetworkActivityThresholdKb > 10240)
                {
                    errors.Add("Network activity threshold must be between 1KB and 10240KB");
                }

                // Validate monitoring consistency
                if (!EnableProcessHealthMonitoring && !EnableDocumentMonitoring && !EnableNetworkMonitoring)
                {
                    errors.Add("At least one monitoring type must be enabled");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Ping-based detection validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"PingBased[Timeout={PingTimeoutMs}ms, Document={DocumentActivityThresholdMinutes}m, Network={NetworkActivityThresholdKb}KB, Process={EnableProcessHealthMonitoring}]";
        }
    }

    /// <summary>
    /// Configuration element for consensus settings
    /// </summary>
    public class ConsensusElement : ConfigurationElement
    {
        [ConfigurationProperty("minimumConfidence", DefaultValue = 0.7)]
        [DoubleValidator(Minimum = 0.0, Maximum = 1.0)]
        public double MinimumConfidence
        {
            get { return (double)this["minimumConfidence"]; }
            set { this["minimumConfidence"] = value; }
        }

        [ConfigurationProperty("requireAllDetectors", DefaultValue = false)]
        public bool RequireAllDetectors
        {
            get { return (bool)this["requireAllDetectors"]; }
            set { this["requireAllDetectors"] = value; }
        }

        [ConfigurationProperty("consecutiveIdleCycles", DefaultValue = 2)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 10)]
        public int ConsecutiveIdleCycles
        {
            get { return (int)this["consecutiveIdleCycles"]; }
            set { this["consecutiveIdleCycles"] = value; }
        }

        [ConfigurationProperty("enableWeightedConsensus", DefaultValue = true)]
        public bool EnableWeightedConsensus
        {
            get { return (bool)this["enableWeightedConsensus"]; }
            set { this["enableWeightedConsensus"] = value; }
        }

        [ConfigurationProperty("timeBasedWeight", DefaultValue = 0.6)]
        [DoubleValidator(Minimum = 0.0, Maximum = 1.0)]
        public double TimeBasedWeight
        {
            get { return (double)this["timeBasedWeight"]; }
            set { this["timeBasedWeight"] = value; }
        }

        [ConfigurationProperty("pingBasedWeight", DefaultValue = 0.4)]
        [DoubleValidator(Minimum = 0.0, Maximum = 1.0)]
        public double PingBasedWeight
        {
            get { return (double)this["pingBasedWeight"]; }
            set { this["pingBasedWeight"] = value; }
        }

        [ConfigurationProperty("confidenceDecayRate", DefaultValue = 0.1)]
        [DoubleValidator(Minimum = 0.0, Maximum = 1.0)]
        public double ConfidenceDecayRate
        {
            get { return (double)this["confidenceDecayRate"]; }
            set { this["confidenceDecayRate"] = value; }
        }

        /// <summary>
        /// Validates the consensus configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate confidence settings
                if (MinimumConfidence < 0.0 || MinimumConfidence > 1.0)
                {
                    errors.Add("Minimum confidence must be between 0.0 and 1.0");
                }

                if (ConsecutiveIdleCycles < 1 || ConsecutiveIdleCycles > 10)
                {
                    errors.Add("Consecutive idle cycles must be between 1 and 10");
                }

                // Validate weighted consensus settings
                if (EnableWeightedConsensus)
                {
                    var totalWeight = TimeBasedWeight + PingBasedWeight;
                    if (Math.Abs(totalWeight - 1.0) > 0.01)
                    {
                        errors.Add("Time-based and ping-based weights must sum to 1.0");
                    }

                    if (TimeBasedWeight < 0.0 || TimeBasedWeight > 1.0)
                    {
                        errors.Add("Time-based weight must be between 0.0 and 1.0");
                    }

                    if (PingBasedWeight < 0.0 || PingBasedWeight > 1.0)
                    {
                        errors.Add("Ping-based weight must be between 0.0 and 1.0");
                    }
                }

                // Validate confidence decay rate
                if (ConfidenceDecayRate < 0.0 || ConfidenceDecayRate > 1.0)
                {
                    errors.Add("Confidence decay rate must be between 0.0 and 1.0");
                }

                // Validate logical consistency
                if (RequireAllDetectors && MinimumConfidence < 1.0)
                {
                    errors.Add("Minimum confidence should be 1.0 when requiring all detectors");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Consensus validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"Consensus[MinConfidence={MinimumConfidence:P0}, RequireAll={RequireAllDetectors}, ConsecutiveCycles={ConsecutiveIdleCycles}]";
        }
    }

    /// <summary>
    /// Configuration element for state management settings
    /// </summary>
    public class StateManagementElement : ConfigurationElement
    {
        [ConfigurationProperty("enableHysteresis", DefaultValue = true)]
        public bool EnableHysteresis
        {
            get { return (bool)this["enableHysteresis"]; }
            set { this["enableHysteresis"] = value; }
        }

        [ConfigurationProperty("hysteresisFactor", DefaultValue = 0.8)]
        [DoubleValidator(Minimum = 0.1, Maximum = 0.9)]
        public double HysteresisFactor
        {
            get { return (double)this["hysteresisFactor"]; }
            set { this["hysteresisFactor"] = value; }
        }

        [ConfigurationProperty("maxIdleDurationMinutes", DefaultValue = 480)]
        [System.Configuration.IntegerValidator(MinValue = 60, MaxValue = 1440)]
        public int MaxIdleDurationMinutes
        {
            get { return (int)this["maxIdleDurationMinutes"]; }
            set { this["maxIdleDurationMinutes"] = value; }
        }

        [ConfigurationProperty("statePersistenceEnabled", DefaultValue = true)]
        public bool StatePersistenceEnabled
        {
            get { return (bool)this["statePersistenceEnabled"]; }
            set { this["statePersistenceEnabled"] = value; }
        }

        [ConfigurationProperty("statePersistenceInterval", DefaultValue = 300)]
        [System.Configuration.IntegerValidator(MinValue = 60, MaxValue = 3600)]
        public int StatePersistenceInterval
        {
            get { return (int)this["statePersistenceInterval"]; }
            set { this["statePersistenceInterval"] = value; }
        }

        [ConfigurationProperty("enableStateRecovery", DefaultValue = true)]
        public bool EnableStateRecovery
        {
            get { return (bool)this["enableStateRecovery"]; }
            set { this["enableStateRecovery"] = value; }
        }

        [ConfigurationProperty("enableUserOverride", DefaultValue = true)]
        public bool EnableUserOverride
        {
            get { return (bool)this["enableUserOverride"]; }
            set { this["enableUserOverride"] = value; }
        }

        [ConfigurationProperty("overrideTimeoutMinutes", DefaultValue = 60)]
        [System.Configuration.IntegerValidator(MinValue = 5, MaxValue = 480)]
        public int OverrideTimeoutMinutes
        {
            get { return (int)this["overrideTimeoutMinutes"]; }
            set { this["overrideTimeoutMinutes"] = value; }
        }

        [ConfigurationProperty("maxConsecutiveOverrides", DefaultValue = 3)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 10)]
        public int MaxConsecutiveOverrides
        {
            get { return (int)this["maxConsecutiveOverrides"]; }
            set { this["maxConsecutiveOverrides"] = value; }
        }

        /// <summary>
        /// Validates the state management configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate hysteresis settings
                if (EnableHysteresis && (HysteresisFactor < 0.1 || HysteresisFactor > 0.9))
                {
                    errors.Add("Hysteresis factor must be between 0.1 and 0.9");
                }

                // Validate timeout settings
                if (MaxIdleDurationMinutes < 60 || MaxIdleDurationMinutes > 1440)
                {
                    errors.Add("Max idle duration must be between 60 and 1440 minutes");
                }

                if (StatePersistenceInterval < 60 || StatePersistenceInterval > 3600)
                {
                    errors.Add("State persistence interval must be between 60 and 3600 seconds");
                }

                if (OverrideTimeoutMinutes < 5 || OverrideTimeoutMinutes > 480)
                {
                    errors.Add("Override timeout must be between 5 and 480 minutes");
                }

                // Validate override settings
                if (MaxConsecutiveOverrides < 1 || MaxConsecutiveOverrides > 10)
                {
                    errors.Add("Max consecutive overrides must be between 1 and 10");
                }

                // Validate persistence consistency
                if (StatePersistenceEnabled && !EnableStateRecovery)
                {
                    errors.Add("State recovery should be enabled when persistence is enabled");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"State management validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"StateManagement[Hysteresis={EnableHysteresis}, MaxDuration={MaxIdleDurationMinutes}m, Persistence={StatePersistenceEnabled}]";
        }
    }

    /// <summary>
    /// Configuration element for activity monitoring settings
    /// </summary>
    public class ActivityMonitoringElement : ConfigurationElement
    {
        [ConfigurationProperty("enableSystemActivityMonitoring", DefaultValue = true)]
        public bool EnableSystemActivityMonitoring
        {
            get { return (bool)this["enableSystemActivityMonitoring"]; }
            set { this["enableSystemActivityMonitoring"] = value; }
        }

        [ConfigurationProperty("systemActivityInterval", DefaultValue = 30)]
        [System.Configuration.IntegerValidator(MinValue = 10, MaxValue = 300)]
        public int SystemActivityInterval
        {
            get { return (int)this["systemActivityInterval"]; }
            set { this["systemActivityInterval"] = value; }
        }

        [ConfigurationProperty("enableFileMonitoring", DefaultValue = true)]
        public bool EnableFileMonitoring
        {
            get { return (bool)this["enableFileMonitoring"]; }
            set { this["enableFileMonitoring"] = value; }
        }

        [ConfigurationProperty("fileMonitoringPaths", DefaultValue = "")]
        public string FileMonitoringPaths
        {
            get { return (string)this["fileMonitoringPaths"]; }
            set { this["fileMonitoringPaths"] = value; }
        }

        [ConfigurationProperty("fileMonitoringFilter", DefaultValue = "*.sld*")]
        public string FileMonitoringFilter
        {
            get { return (string)this["fileMonitoringFilter"]; }
            set { this["fileMonitoringFilter"] = value; }
        }

        [ConfigurationProperty("enableProcessMonitoring", DefaultValue = true)]
        public bool EnableProcessMonitoring
        {
            get { return (bool)this["enableProcessMonitoring"]; }
            set { this["enableProcessMonitoring"] = value; }
        }

        [ConfigurationProperty("processNames", DefaultValue = "SLDWORKS.exe")]
        public string ProcessNames
        {
            get { return (string)this["processNames"]; }
            set { this["processNames"] = value; }
        }

        [ConfigurationProperty("enableNetworkMonitoring", DefaultValue = true)]
        public bool EnableNetworkMonitoring
        {
            get { return (bool)this["enableNetworkMonitoring"]; }
            set { this["enableNetworkMonitoring"] = value; }
        }

        [ConfigurationProperty("networkMonitoringInterval", DefaultValue = 60)]
        [System.Configuration.IntegerValidator(MinValue = 10, MaxValue = 600)]
        public int NetworkMonitoringInterval
        {
            get { return (int)this["networkMonitoringInterval"]; }
            set { this["networkMonitoringInterval"] = value; }
        }

        [ConfigurationProperty("maxMonitoredProcesses", DefaultValue = 50)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 200)]
        public int MaxMonitoredProcesses
        {
            get { return (int)this["maxMonitoredProcesses"]; }
            set { this["maxMonitoredProcesses"] = value; }
        }

        [ConfigurationProperty("maxMonitoredFiles", DefaultValue = 1000)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 10000)]
        public int MaxMonitoredFiles
        {
            get { return (int)this["maxMonitoredFiles"]; }
            set { this["maxMonitoredFiles"] = value; }
        }

        [ConfigurationProperty("enableRealTimeNotifications", DefaultValue = true)]
        public bool EnableRealTimeNotifications
        {
            get { return (bool)this["enableRealTimeNotifications"]; }
            set { this["enableRealTimeNotifications"] = value; }
        }

        [ConfigurationProperty("notificationThrottleMs", DefaultValue = 1000)]
        [System.Configuration.IntegerValidator(MinValue = 100, MaxValue = 10000)]
        public int NotificationThrottleMs
        {
            get { return (int)this["notificationThrottleMs"]; }
            set { this["notificationThrottleMs"] = value; }
        }

        /// <summary>
        /// Validates the activity monitoring configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate monitoring intervals
                if (SystemActivityInterval < 10 || SystemActivityInterval > 300)
                {
                    errors.Add("System activity interval must be between 10 and 300 seconds");
                }

                if (NetworkMonitoringInterval < 10 || NetworkMonitoringInterval > 600)
                {
                    errors.Add("Network monitoring interval must be between 10 and 600 seconds");
                }

                // Validate monitoring limits
                if (MaxMonitoredProcesses < 1 || MaxMonitoredProcesses > 200)
                {
                    errors.Add("Max monitored processes must be between 1 and 200");
                }

                if (MaxMonitoredFiles < 1 || MaxMonitoredFiles > 10000)
                {
                    errors.Add("Max monitored files must be between 1 and 10000");
                }

                // Validate notification settings
                if (NotificationThrottleMs < 100 || NotificationThrottleMs > 10000)
                {
                    errors.Add("Notification throttle must be between 100 and 10000 ms");
                }

                // Validate monitoring consistency
                if (!EnableSystemActivityMonitoring && !EnableFileMonitoring && !EnableProcessMonitoring && !EnableNetworkMonitoring)
                {
                    errors.Add("At least one monitoring type must be enabled");
                }

                // Validate file monitoring settings
                if (EnableFileMonitoring && string.IsNullOrWhiteSpace(FileMonitoringPaths))
                {
                    errors.Add("File monitoring paths must be specified when file monitoring is enabled");
                }

                // Validate process monitoring settings
                if (EnableProcessMonitoring && string.IsNullOrWhiteSpace(ProcessNames))
                {
                    errors.Add("Process names must be specified when process monitoring is enabled");
                }

                // Validate file paths format
                if (EnableFileMonitoring && !string.IsNullOrWhiteSpace(FileMonitoringPaths))
                {
                    var paths = FileMonitoringPaths.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var path in paths)
                    {
                        var trimmedPath = path.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedPath))
                        {
                            errors.Add("Empty file monitoring path found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Activity monitoring validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"ActivityMonitoring[System={EnableSystemActivityMonitoring}, Files={EnableFileMonitoring}, Processes={EnableProcessMonitoring}, Network={EnableNetworkMonitoring}]";
        }
    }
}