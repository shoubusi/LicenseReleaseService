using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Main configuration class for idle detection settings
    /// </summary>
    public class IdleDetectionConfiguration : ConfigurationElement
    {
        [ConfigurationProperty("isEnabled", DefaultValue = true)]
        public bool IsEnabled
        {
            get { return (bool)this["isEnabled"]; }
            set { this["isEnabled"] = value; }
        }

        [ConfigurationProperty("detectionIntervalSeconds", DefaultValue = 60)]
        [IntegerValidator(MinValue = 10, MaxValue = 3600)]
        public int DetectionIntervalSeconds
        {
            get { return (int)this["detectionIntervalSeconds"]; }
            set { this["detectionIntervalSeconds"] = value; }
        }

        [ConfigurationProperty("idleThresholdSeconds", DefaultValue = 900)]
        [IntegerValidator(MinValue = 60, MaxValue = 7200)]
        public int IdleThresholdSeconds
        {
            get { return (int)this["idleThresholdSeconds"]; }
            set { this["idleThresholdSeconds"] = value; }
        }

        [ConfigurationProperty("confidenceThreshold", DefaultValue = 0.7)]
        [DoubleValidator(MinValue = 0.0, MaxValue = 1.0)]
        public double ConfidenceThreshold
        {
            get { return (double)this["confidenceThreshold"]; }
            set { this["confidenceThreshold"] = value; }
        }

        [ConfigurationProperty("priority", DefaultValue = 10)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        [ConfigurationProperty("maxDetectionTimeMs", DefaultValue = 5000)]
        [IntegerValidator(MinValue = 1000, MaxValue = 30000)]
        public int MaxDetectionTimeMs
        {
            get { return (int)this["maxDetectionTimeMs"]; }
            set { this["maxDetectionTimeMs"] = value; }
        }

        [ConfigurationProperty("timeoutSeconds", DefaultValue = 30)]
        [IntegerValidator(MinValue = 5, MaxValue = 300)]
        public int TimeoutSeconds
        {
            get { return (int)this["timeoutSeconds"]; }
            set { this["timeoutSeconds"] = value; }
        }

        [ConfigurationProperty("retryCount", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int RetryCount
        {
            get { return (int)this["retryCount"]; }
            set { this["retryCount"] = value; }
        }

        [ConfigurationProperty("retryDelayMs", DefaultValue = 1000)]
        [IntegerValidator(MinValue = 100, MaxValue = 10000)]
        public int RetryDelayMs
        {
            get { return (int)this["retryDelayMs"]; }
            set { this["retryDelayMs"] = value; }
        }

        [ConfigurationProperty("maxConcurrentOperations", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 50)]
        public int MaxConcurrentOperations
        {
            get { return (int)this["maxConcurrentOperations"]; }
            set { this["maxConcurrentOperations"] = value; }
        }

        [ConfigurationProperty("customParameters")]
        public CustomParametersCollection CustomParameters
        {
            get { return (CustomParametersCollection)this["customParameters"] ?? new CustomParametersCollection(); }
            set { this["customParameters"] = value; }
        }

        /// <summary>
        /// Gets the detection interval as TimeSpan
        /// </summary>
        public TimeSpan DetectionInterval => TimeSpan.FromSeconds(DetectionIntervalSeconds);

        /// <summary>
        /// Gets the idle threshold as TimeSpan
        /// </summary>
        public TimeSpan IdleThreshold => TimeSpan.FromSeconds(IdleThresholdSeconds);

        /// <summary>
        /// Gets the timeout as TimeSpan
        /// </summary>
        public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

        /// <summary>
        /// Gets the retry delay as TimeSpan
        /// </summary>
        public TimeSpan RetryDelay => TimeSpan.FromMilliseconds(RetryDelayMs);

        /// <summary>
        /// Gets the maximum detection time as TimeSpan
        /// </summary>
        public TimeSpan MaxDetectionTime => TimeSpan.FromMilliseconds(MaxDetectionTimeMs);

        /// <summary>
        /// Validates the idle detection configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate interval relationships
                if (DetectionIntervalSeconds <= 0)
                {
                    errors.Add("Detection interval must be greater than zero");
                }

                if (IdleThresholdSeconds <= DetectionIntervalSeconds)
                {
                    errors.Add("Idle threshold must be greater than detection interval");
                }

                if (TimeoutSeconds <= 0)
                {
                    errors.Add("Timeout must be greater than zero");
                }

                if (MaxDetectionTimeMs <= 0)
                {
                    errors.Add("Max detection time must be greater than zero");
                }

                // Validate retry settings
                if (RetryCount < 0)
                {
                    errors.Add("Retry count cannot be negative");
                }

                if (RetryDelayMs <= 0)
                {
                    errors.Add("Retry delay must be greater than zero");
                }

                // Validate concurrent operations
                if (MaxConcurrentOperations <= 0)
                {
                    errors.Add("Max concurrent operations must be greater than zero");
                }

                // Validate confidence threshold
                if (ConfidenceThreshold < 0.0 || ConfidenceThreshold > 1.0)
                {
                    errors.Add("Confidence threshold must be between 0.0 and 1.0");
                }

                // Validate priority
                if (Priority <= 0)
                {
                    errors.Add("Priority must be greater than zero");
                }

                // Validate custom parameters
                if (CustomParameters != null)
                {
                    foreach (CustomParameterElement parameter in CustomParameters)
                    {
                        if (string.IsNullOrWhiteSpace(parameter.Name))
                        {
                            errors.Add("Custom parameter name cannot be empty");
                        }

                        if (parameter.Value == null)
                        {
                            errors.Add($"Custom parameter value cannot be null for parameter: {parameter.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Idle detection configuration validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"IdleDetection[Enabled={IsEnabled}, Interval={DetectionIntervalSeconds}s, Threshold={IdleThresholdSeconds}s, Confidence={ConfidenceThreshold:F2}, Priority={Priority}]";
        }
    }

    /// <summary>
    /// Collection of custom parameters for idle detection
    /// </summary>
    public class CustomParametersCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Creates a new configuration element
        /// </summary>
        protected override ConfigurationElement CreateNewElement()
        {
            return new CustomParameterElement();
        }

        /// <summary>
        /// Gets the element key
        /// </summary>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((CustomParameterElement)element).Name;
        }

        /// <summary>
        /// Gets or sets a custom parameter by name
        /// </summary>
        public new CustomParameterElement this[string name]
        {
            get
            {
                return (CustomParameterElement)BaseGet(name);
            }
            set
            {
                if (BaseGet(name) != null)
                {
                    BaseRemove(name);
                }
                BaseAdd(value);
            }
        }

        /// <summary>
        /// Gets or sets a custom parameter by index
        /// </summary>
        public CustomParameterElement this[int index]
        {
            get
            {
                return (CustomParameterElement)BaseGet(index);
            }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        /// <summary>
        /// Adds a custom parameter to the collection
        /// </summary>
        public void Add(CustomParameterElement parameter)
        {
            BaseAdd(parameter);
        }

        /// <summary>
        /// Removes a custom parameter from the collection
        /// </summary>
        public void Remove(string name)
        {
            BaseRemove(name);
        }

        /// <summary>
        /// Removes a custom parameter at the specified index
        /// </summary>
        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        /// <summary>
        /// Clears all custom parameters from the collection
        /// </summary>
        public void Clear()
        {
            BaseClear();
        }

        /// <summary>
        /// Gets the number of custom parameters in the collection
        /// </summary>
        public new int Count
        {
            get { return base.Count; }
        }

        /// <summary>
        /// Gets all custom parameters as a dictionary
        /// </summary>
        public Dictionary<string, object> ToDictionary()
        {
            var dictionary = new Dictionary<string, object>();
            foreach (CustomParameterElement parameter in this)
            {
                dictionary[parameter.Name] = parameter.Value;
            }
            return dictionary;
        }
    }

    /// <summary>
    /// Configuration element for custom parameters
    /// </summary>
    public class CustomParameterElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        [StringValidator(MinLength = 1)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("value", IsRequired = true)]
        public string Value
        {
            get { return (string)this["value"]; }
            set { this["value"] = value; }
        }

        [ConfigurationProperty("type", DefaultValue = "string")]
        [StringValidator(MinLength = 1)]
        public string Type
        {
            get { return (string)this["type"]; }
            set { this["type"] = value; }
        }

        /// <summary>
        /// Gets the parameter value as the specified type
        /// </summary>
        public object GetTypedValue()
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                return null;
            }

            switch (Type?.ToLowerInvariant())
            {
                case "int":
                case "integer":
                    if (int.TryParse(Value, out var intValue))
                    {
                        return intValue;
                    }
                    break;
                case "bool":
                case "boolean":
                    if (bool.TryParse(Value, out var boolValue))
                    {
                        return boolValue;
                    }
                    break;
                case "double":
                case "decimal":
                    if (double.TryParse(Value, out var doubleValue))
                    {
                        return doubleValue;
                    }
                    break;
                case "timespan":
                    if (TimeSpan.TryParse(Value, out var timespanValue))
                    {
                        return timespanValue;
                    }
                    break;
                case "string":
                default:
                    return Value;
            }

            // Return as string if type conversion fails
            return Value;
        }

        /// <summary>
        /// Returns a string representation of the custom parameter
        /// </summary>
        public override string ToString()
        {
            return $"{Name}={Value} ({Type})";
        }
    }
}