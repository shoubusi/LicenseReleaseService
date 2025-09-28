using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;

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

        [ConfigurationProperty("detectionInterval", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 60)]
        public int DetectionInterval
        {
            get { return (int)this["detectionInterval"]; }
            set { this["detectionInterval"] = value; }
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
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"IdleDetection[Enabled={EnableIdleDetection}, Interval={DetectionInterval}m, TimeBased={TimeBasedDetection}, PingBased={PingBasedDetection}]";
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