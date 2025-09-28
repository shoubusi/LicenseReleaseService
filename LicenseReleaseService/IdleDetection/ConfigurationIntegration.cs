using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using AppConfiguration = LicenseReleaseService.Configuration.ConfigurationManager;
using LicenseReleaseService.Models;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Integrates idle detection configuration with the main configuration system
    /// </summary>
    public class ConfigurationIntegration : IDisposable
    {
        private readonly AppConfiguration _configurationManager;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private IdleDetectionConfigurationElement _currentConfig;
        private Dictionary<string, IdleDetectorConfiguration> _detectorConfigs;

        /// <summary>
        /// Event raised when idle detection configuration changes
        /// </summary>
        public event EventHandler<IdleDetectionConfigurationChangedEventArgs> ConfigurationChanged;

        /// <summary>
        /// Gets the current idle detection configuration
        /// </summary>
        public IdleDetectionConfigurationElement CurrentConfiguration
        {
            get
            {
                lock (_lock)
                {
                    return _currentConfig ?? LoadConfigurationInternal();
                }
            }
        }

        /// <summary>
        /// Gets the detector configurations dictionary
        /// </summary>
        public IReadOnlyDictionary<string, IdleDetectorConfiguration> DetectorConfigurations
        {
            get
            {
                lock (_lock)
                {
                    if (_detectorConfigs == null)
                    {
                        _detectorConfigs = LoadDetectorConfigurations();
                    }
                    return _detectorConfigs.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets whether idle detection is enabled
        /// </summary>
        public bool IsIdleDetectionEnabled => CurrentConfiguration?.EnableIdleDetection ?? false;

        /// <summary>
        /// Gets the detection interval
        /// </summary>
        public TimeSpan DetectionInterval => TimeSpan.FromMinutes(CurrentConfiguration?.DetectionInterval ?? 5);

        /// <summary>
        /// Gets the default idle threshold
        /// </summary>
        public TimeSpan DefaultIdleThreshold => TimeSpan.FromMinutes(CurrentConfiguration?.TimeBasedDetection?.IdleThresholdMinutes ?? 30);

        /// <summary>
        /// Gets the confidence threshold
        /// </summary>
        public double ConfidenceThreshold => CurrentConfiguration?.Consensus?.MinimumConfidence ?? 0.7;

        /// <summary>
        /// Initializes a new instance of the ConfigurationIntegration class
        /// </summary>
        public ConfigurationIntegration()
        {
            _configurationManager = LicenseReleaseService.Configuration.ConfigurationManager.Instance;
            _configurationManager.ConfigurationChanged += OnMainConfigurationChanged;

            // Load initial configuration
            _currentConfig = LoadConfigurationInternal();
            _detectorConfigs = LoadDetectorConfigurations();
        }

        /// <summary>
        /// Initializes a new instance of the ConfigurationIntegration class with a specific configuration manager
        /// </summary>
        public ConfigurationIntegration(AppConfiguration configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _configurationManager.ConfigurationChanged += OnMainConfigurationChanged;

            // Load initial configuration
            _currentConfig = LoadConfigurationInternal();
            _detectorConfigs = LoadDetectorConfigurations();
        }

        /// <summary>
        /// Gets a specific detector configuration by name
        /// </summary>
        public IdleDetectorConfiguration GetDetectorConfiguration(string detectorName)
        {
            if (string.IsNullOrWhiteSpace(detectorName))
            {
                throw new ArgumentException("Detector name cannot be null or whitespace", nameof(detectorName));
            }

            lock (_lock)
            {
                if (_detectorConfigs == null)
                {
                    _detectorConfigs = LoadDetectorConfigurations();
                }

                return _detectorConfigs.TryGetValue(detectorName, out var config) ? config : null;
            }
        }

        /// <summary>
        /// Gets all enabled detector configurations
        /// </summary>
        public IEnumerable<IdleDetectorConfiguration> GetEnabledDetectorConfigurations()
        {
            lock (_lock)
            {
                if (_detectorConfigs == null)
                {
                    _detectorConfigs = LoadDetectorConfigurations();
                }

                return _detectorConfigs.Values.Where(config => config.IsEnabled).ToList();
            }
        }

        /// <summary>
        /// Gets detector configurations ordered by priority
        /// </summary>
        public IEnumerable<IdleDetectorConfiguration> GetDetectorConfigurationsByPriority()
        {
            lock (_lock)
            {
                if (_detectorConfigs == null)
                {
                    _detectorConfigs = LoadDetectorConfigurations();
                }

                return _detectorConfigs.Values
                    .OrderByDescending(config => config.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Validates the current idle detection configuration
        /// </summary>
        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();

            try
            {
                var config = CurrentConfiguration;
                if (config == null)
                {
                    errors.Add("Idle detection configuration is not available");
                    return errors;
                }

                // Validate main configuration
                errors.AddRange(config.Validate());

                // Validate detector configurations
                foreach (var detectorConfig in DetectorConfigurations.Values)
                {
                    var detectorErrors = detectorConfig.Validate();
                    if (detectorErrors.Count > 0)
                    {
                        errors.AddRange(detectorErrors.Select(e => $"{detectorConfig.DetectorName}: {e}"));
                    }
                }

                // Validate relationships between configurations
                if (config.DetectionIntervalSeconds >= config.IdleThresholdSeconds)
                {
                    errors.Add("Detection interval must be less than idle threshold");
                }

                if (config.ConfidenceThreshold < 0.0 || config.ConfidenceThreshold > 1.0)
                {
                    errors.Add("Confidence threshold must be between 0.0 and 1.0");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Configuration validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Reloads the idle detection configuration
        /// </summary>
        public void ReloadConfiguration()
        {
            lock (_lock)
            {
                var oldConfig = _currentConfig;
                var oldDetectorConfigs = _detectorConfigs;

                try
                {
                    _currentConfig = LoadConfigurationInternal();
                    _detectorConfigs = LoadDetectorConfigurations();

                    // Validate new configuration
                    var errors = ValidateConfiguration();
                    if (errors.Count > 0)
                    {
                        // Rollback on validation failure
                        _currentConfig = oldConfig;
                        _detectorConfigs = oldDetectorConfigs;
                        throw new ConfigurationErrorsException($"Configuration validation failed: {string.Join("; ", errors)}");
                    }

                    // Raise configuration changed event
                    OnConfigurationChanged(new IdleDetectionConfigurationChangedEventArgs(
                        oldConfig, _currentConfig, oldDetectorConfigs, _detectorConfigs, errors));
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    _currentConfig = oldConfig;
                    _detectorConfigs = oldDetectorConfigs;
                    throw new ConfigurationErrorsException($"Failed to reload idle detection configuration: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Gets the effective configuration for a specific detector
        /// </summary>
        public IdleDetectorConfiguration GetEffectiveDetectorConfiguration(string detectorName)
        {
            var baseConfig = GetDetectorConfiguration(detectorName);
            if (baseConfig == null)
            {
                return null;
            }

            // Apply global settings as defaults
            var effectiveConfig = new IdleDetectorConfiguration
            {
                IsEnabled = baseConfig.IsEnabled,
                DetectionIntervalSeconds = baseConfig.DetectionIntervalSeconds > 0 ?
                    baseConfig.DetectionIntervalSeconds : CurrentConfiguration?.DetectionIntervalSeconds ?? 60,
                IdleThresholdSeconds = baseConfig.IdleThresholdSeconds > 0 ?
                    baseConfig.IdleThresholdSeconds : CurrentConfiguration?.IdleThresholdSeconds ?? 900,
                ConfidenceThreshold = baseConfig.ConfidenceThreshold >= 0 ?
                    baseConfig.ConfidenceThreshold : CurrentConfiguration?.ConfidenceThreshold ?? 0.7,
                Priority = baseConfig.Priority > 0 ?
                    baseConfig.Priority : CurrentConfiguration?.Priority ?? 10,
                MaxDetectionTimeMs = baseConfig.MaxDetectionTimeMs > 0 ?
                    baseConfig.MaxDetectionTimeMs : CurrentConfiguration?.MaxDetectionTimeMs ?? 5000,
                TimeoutSeconds = baseConfig.TimeoutSeconds > 0 ?
                    baseConfig.TimeoutSeconds : CurrentConfiguration?.TimeoutSeconds ?? 30,
                RetryCount = baseConfig.RetryCount >= 0 ?
                    baseConfig.RetryCount : CurrentConfiguration?.RetryCount ?? 3,
                RetryDelayMs = baseConfig.RetryDelayMs > 0 ?
                    baseConfig.RetryDelayMs : CurrentConfiguration?.RetryDelayMs ?? 1000,
                MaxConcurrentOperations = baseConfig.MaxConcurrentOperations > 0 ?
                    baseConfig.MaxConcurrentOperations : CurrentConfiguration?.MaxConcurrentOperations ?? 5,
                CustomParameters = MergeCustomParameters(baseConfig.CustomParameters, CurrentConfiguration?.CustomParameters)
            };

            return effectiveConfig;
        }

        /// <summary>
        /// Gets a configuration summary
        /// </summary>
        public string GetConfigurationSummary()
        {
            try
            {
                var config = CurrentConfiguration;
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("Idle Detection Configuration Summary:");
                sb.AppendLine($"  Enabled: {IsIdleDetectionEnabled}");
                sb.AppendLine($"  Detection Interval: {DetectionInterval.TotalSeconds:F1} seconds");
                sb.AppendLine($"  Default Idle Threshold: {DefaultIdleThreshold.TotalMinutes:F1} minutes");
                sb.AppendLine($"  Confidence Threshold: {ConfidenceThreshold:F2}");
                sb.AppendLine($"  Configured Detectors: {DetectorConfigurations.Count}");
                sb.AppendLine($"  Enabled Detectors: {GetEnabledDetectorConfigurations().Count()}");

                if (DetectorConfigurations.Count > 0)
                {
                    sb.AppendLine("  Detector Configurations:");
                    foreach (var detectorConfig in GetDetectorConfigurationsByPriority())
                    {
                        sb.AppendLine($"    - {detectorConfig.DetectorName}: Enabled={detectorConfig.IsEnabled}, " +
                                     $"Priority={detectorConfig.Priority}, " +
                                     $"Threshold={TimeSpan.FromSeconds(detectorConfig.IdleThresholdSeconds).TotalMinutes:F1}m");
                    }
                }

                var validationErrors = ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    sb.AppendLine($"  Validation Errors: {validationErrors.Count}");
                    foreach (var error in validationErrors.Take(3))
                    {
                        sb.AppendLine($"    - {error}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Failed to get configuration summary: {ex.Message}";
            }
        }

        /// <summary>
        /// Loads the idle detection configuration from the main configuration
        /// </summary>
        private IdleDetectionConfigurationElement LoadConfigurationInternal()
        {
            try
            {
                var mainConfig = _configurationManager.CurrentConfiguration;
                if (mainConfig == null)
                {
                    throw new ConfigurationErrorsException("Main configuration is not available");
                }

                // Get the idle detection section from the main configuration
                var idleDetectionSection = mainConfig.Sections["idleDetection"] as IdleDetectionConfigurationElement;
                if (idleDetectionSection == null)
                {
                    // Return default configuration if section doesn't exist
                    return GetDefaultConfiguration();
                }

                return idleDetectionSection;
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Failed to load idle detection configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads detector configurations
        /// </summary>
        private Dictionary<string, IdleDetectorConfiguration> LoadDetectorConfigurations()
        {
            var configs = new Dictionary<string, IdleDetectorConfiguration>();

            try
            {
                var mainConfig = CurrentConfiguration;
                if (mainConfig == null)
                {
                    return configs;
                }

                if (mainConfig.Detectors != null)
                {
                    foreach (IdleDetectorConfigurationElement detectorElement in mainConfig.Detectors)
                    {
                        var detectorConfig = ConvertToIdleDetectorConfiguration(detectorElement);
                        configs[detectorConfig.DetectorName] = detectorConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Failed to load detector configurations: {ex.Message}", ex);
            }

            return configs;
        }

        /// <summary>
        /// Converts a detector configuration element to an IdleDetectorConfiguration
        /// </summary>
        private IdleDetectorConfiguration ConvertToIdleDetectorConfiguration(IdleDetectorConfigurationElement element)
        {
            return new IdleDetectorConfiguration
            {
                DetectorName = element.DetectorName,
                IsEnabled = element.IsEnabled,
                DetectionIntervalSeconds = element.DetectionIntervalSeconds,
                IdleThresholdSeconds = element.IdleThresholdSeconds,
                ConfidenceThreshold = element.ConfidenceThreshold,
                Priority = element.Priority,
                MaxDetectionTimeMs = element.MaxDetectionTimeMs,
                TimeoutSeconds = element.TimeoutSeconds,
                RetryCount = element.RetryCount,
                RetryDelayMs = element.RetryDelayMs,
                MaxConcurrentOperations = element.MaxConcurrentOperations,
                CustomParameters = element.CustomParameters?.ToDictionary() ?? new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Merges custom parameters from base and fallback configurations
        /// </summary>
        private Dictionary<string, object> MergeCustomParameters(
            CustomParametersCollection baseParams,
            CustomParametersCollection fallbackParams)
        {
            var merged = new Dictionary<string, object>();

            // Add fallback parameters first
            if (fallbackParams != null)
            {
                foreach (CustomParameterElement param in fallbackParams)
                {
                    merged[param.Name] = param.GetTypedValue();
                }
            }

            // Override with base parameters
            if (baseParams != null)
            {
                foreach (CustomParameterElement param in baseParams)
                {
                    merged[param.Name] = param.GetTypedValue();
                }
            }

            return merged;
        }

        /// <summary>
        /// Gets the default configuration
        /// </summary>
        private IdleDetectionConfigurationElement GetDefaultConfiguration()
        {
            return new IdleDetectionConfigurationElement
            {
                IsEnabled = false,
                DetectionIntervalSeconds = 60,
                IdleThresholdSeconds = 900,
                ConfidenceThreshold = 0.7,
                Priority = 10,
                MaxDetectionTimeMs = 5000,
                TimeoutSeconds = 30,
                RetryCount = 3,
                RetryDelayMs = 1000,
                MaxConcurrentOperations = 5
            };
        }

        /// <summary>
        /// Handles main configuration changes
        /// </summary>
        private void OnMainConfigurationChanged(object sender, ConfigurationReloadEventArgs e)
        {
            try
            {
                ReloadConfiguration();
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application
                System.Diagnostics.Trace.WriteLine($"Failed to reload idle detection configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the ConfigurationChanged event
        /// </summary>
        protected virtual void OnConfigurationChanged(IdleDetectionConfigurationChangedEventArgs e)
        {
            ConfigurationChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the configuration integration
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the configuration integration
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Unsubscribe from events
                    if (_configurationManager != null)
                    {
                        _configurationManager.ConfigurationChanged -= OnMainConfigurationChanged;
                    }
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ConfigurationIntegration()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Event arguments for idle detection configuration changes
    /// </summary>
    public class IdleDetectionConfigurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old configuration
        /// </summary>
        public IdleDetectionConfigurationElement OldConfiguration { get; }

        /// <summary>
        /// Gets the new configuration
        /// </summary>
        public IdleDetectionConfigurationElement NewConfiguration { get; }

        /// <summary>
        /// Gets the old detector configurations
        /// </summary>
        public IReadOnlyDictionary<string, IdleDetectorConfiguration> OldDetectorConfigurations { get; }

        /// <summary>
        /// Gets the new detector configurations
        /// </summary>
        public IReadOnlyDictionary<string, IdleDetectorConfiguration> NewDetectorConfigurations { get; }

        /// <summary>
        /// Gets the validation errors
        /// </summary>
        public List<string> ValidationErrors { get; }

        /// <summary>
        /// Gets whether the change was successful
        /// </summary>
        public bool IsSuccess => ValidationErrors.Count == 0;

        /// <summary>
        /// Initializes a new instance of the IdleDetectionConfigurationChangedEventArgs class
        /// </summary>
        public IdleDetectionConfigurationChangedEventArgs(
            IdleDetectionConfigurationElement oldConfig,
            IdleDetectionConfigurationElement newConfig,
            IReadOnlyDictionary<string, IdleDetectorConfiguration> oldDetectorConfigs,
            IReadOnlyDictionary<string, IdleDetectorConfiguration> newDetectorConfigs,
            List<string> validationErrors)
        {
            OldConfiguration = oldConfig;
            NewConfiguration = newConfig;
            OldDetectorConfigurations = oldDetectorConfigs;
            NewDetectorConfigurations = newDetectorConfigs;
            ValidationErrors = validationErrors ?? new List<string>();
        }
    }

    /// <summary>
    /// Configuration element for idle detection settings (main section)
    /// </summary>
    public class IdleDetectionMainConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("isEnabled", DefaultValue = true)]
        public bool IsEnabled
        {
            get { return (bool)this["isEnabled"]; }
            set { this["isEnabled"] = value; }
        }

        [ConfigurationProperty("detectionIntervalSeconds", DefaultValue = 60)]
        [System.Configuration.IntegerValidator(MinValue = 10, MaxValue = 3600)]
        public int DetectionIntervalSeconds
        {
            get { return (int)this["detectionIntervalSeconds"]; }
            set { this["detectionIntervalSeconds"] = value; }
        }

        [ConfigurationProperty("idleThresholdSeconds", DefaultValue = 900)]
        [System.Configuration.IntegerValidator(MinValue = 60, MaxValue = 7200)]
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

        [ConfigurationProperty("priority", DefaultValue = 10)]
        [System.Configuration.IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        [ConfigurationProperty("maxDetectionTimeMs", DefaultValue = 5000)]
        [System.Configuration.IntegerValidator(MinValue = 1000, MaxValue = 30000)]
        public int MaxDetectionTimeMs
        {
            get { return (int)this["maxDetectionTimeMs"]; }
            set { this["maxDetectionTimeMs"] = value; }
        }

        [ConfigurationProperty("timeoutSeconds", DefaultValue = 30)]
        [System.Configuration.IntegerValidator(MinValue = 5, MaxValue = 300)]
        public int TimeoutSeconds
        {
            get { return (int)this["timeoutSeconds"]; }
            set { this["timeoutSeconds"] = value; }
        }

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

        [ConfigurationProperty("detectors")]
        public IdleDetectorConfigurationCollection Detectors
        {
            get { return (IdleDetectorConfigurationCollection)this["detectors"] ?? new IdleDetectorConfigurationCollection(); }
            set { this["detectors"] = value; }
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
        /// Validates the idle detection configuration element
        /// </summary>
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

                // Validate time settings
                if (MaxDetectionTimeMs <= 0)
                {
                    errors.Add("Max detection time must be greater than zero");
                }

                if (TimeoutSeconds <= 0)
                {
                    errors.Add("Timeout must be greater than zero");
                }

                if (RetryDelayMs <= 0)
                {
                    errors.Add("Retry delay must be greater than zero");
                }

                // Validate retry settings
                if (RetryCount < 0)
                {
                    errors.Add("Retry count cannot be negative");
                }

                // Validate concurrent operations
                if (MaxConcurrentOperations <= 0)
                {
                    errors.Add("Max concurrent operations must be greater than zero");
                }

                // Validate detector configurations
                if (Detectors != null)
                {
                    foreach (IdleDetectorConfigurationElement detector in Detectors)
                    {
                        var detectorErrors = detector.Validate();
                        errors.AddRange(detectorErrors.Select(e => $"{detector.DetectorName}: {e}"));
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Idle detection element validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"IdleDetection[Enabled={IsEnabled}, Interval={DetectionIntervalSeconds}s, Threshold={IdleThresholdSeconds}s, Confidence={ConfidenceThreshold:F2}, Detectors={Detectors?.Count ?? 0}]";
        }
    }

    /// <summary>
    /// Collection of idle detector configurations
    /// </summary>
    public class IdleDetectorConfigurationCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new IdleDetectorConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((IdleDetectorConfigurationElement)element).DetectorName;
        }

        public new IdleDetectorConfigurationElement this[string name]
        {
            get
            {
                return (IdleDetectorConfigurationElement)BaseGet(name);
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

        public IdleDetectorConfigurationElement this[int index]
        {
            get
            {
                return (IdleDetectorConfigurationElement)BaseGet(index);
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

        public void Add(IdleDetectorConfigurationElement detector)
        {
            BaseAdd(detector);
        }

        public void Remove(string name)
        {
            BaseRemove(name);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Clear()
        {
            BaseClear();
        }

        public new int Count
        {
            get { return base.Count; }
        }
    }

    /// <summary>
    /// Configuration element for individual idle detector settings
    /// </summary>
    public class IdleDetectorConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("detectorName", IsRequired = true)]
        [StringValidator(MinLength = 1)]
        public string DetectorName
        {
            get { return (string)this["detectorName"]; }
            set { this["detectorName"] = value; }
        }

        [ConfigurationProperty("isEnabled", DefaultValue = true)]
        public bool IsEnabled
        {
            get { return (bool)this["isEnabled"]; }
            set { this["isEnabled"] = value; }
        }

        [ConfigurationProperty("detectionIntervalSeconds", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 3600)]
        public int DetectionIntervalSeconds
        {
            get { return (int)this["detectionIntervalSeconds"]; }
            set { this["detectionIntervalSeconds"] = value; }
        }

        [ConfigurationProperty("idleThresholdSeconds", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 7200)]
        public int IdleThresholdSeconds
        {
            get { return (int)this["idleThresholdSeconds"]; }
            set { this["idleThresholdSeconds"] = value; }
        }

        [ConfigurationProperty("confidenceThreshold", DefaultValue = -1.0)]
        [DoubleValidator(Minimum = -1.0, Maximum = 1.0)]
        public double ConfidenceThreshold
        {
            get { return (double)this["confidenceThreshold"]; }
            set { this["confidenceThreshold"] = value; }
        }

        [ConfigurationProperty("priority", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 100)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        [ConfigurationProperty("maxDetectionTimeMs", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 30000)]
        public int MaxDetectionTimeMs
        {
            get { return (int)this["maxDetectionTimeMs"]; }
            set { this["maxDetectionTimeMs"] = value; }
        }

        [ConfigurationProperty("timeoutSeconds", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 300)]
        public int TimeoutSeconds
        {
            get { return (int)this["timeoutSeconds"]; }
            set { this["timeoutSeconds"] = value; }
        }

        [ConfigurationProperty("retryCount", DefaultValue = -1)]
        [System.Configuration.IntegerValidator(MinValue = -1, MaxValue = 10)]
        public int RetryCount
        {
            get { return (int)this["retryCount"]; }
            set { this["retryCount"] = value; }
        }

        [ConfigurationProperty("retryDelayMs", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 10000)]
        public int RetryDelayMs
        {
            get { return (int)this["retryDelayMs"]; }
            set { this["retryDelayMs"] = value; }
        }

        [ConfigurationProperty("maxConcurrentOperations", DefaultValue = 0)]
        [System.Configuration.IntegerValidator(MinValue = 0, MaxValue = 50)]
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
        /// Validates the detector configuration element
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate detector name
                if (string.IsNullOrWhiteSpace(DetectorName))
                {
                    errors.Add("Detector name cannot be empty");
                }

                // Validate that non-negative values are properly set
                if (DetectionIntervalSeconds < 0)
                {
                    errors.Add("Detection interval seconds cannot be negative");
                }

                if (IdleThresholdSeconds < 0)
                {
                    errors.Add("Idle threshold seconds cannot be negative");
                }

                if (MaxDetectionTimeMs < 0)
                {
                    errors.Add("Max detection time cannot be negative");
                }

                if (TimeoutSeconds < 0)
                {
                    errors.Add("Timeout seconds cannot be negative");
                }

                if (RetryDelayMs < 0)
                {
                    errors.Add("Retry delay cannot be negative");
                }

                if (MaxConcurrentOperations < 0)
                {
                    errors.Add("Max concurrent operations cannot be negative");
                }

                // Validate confidence threshold range
                if (ConfidenceThreshold < -1.0 || ConfidenceThreshold > 1.0)
                {
                    errors.Add("Confidence threshold must be between -1.0 and 1.0");
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
                errors.Add($"Detector configuration validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the detector configuration
        /// </summary>
        public override string ToString()
        {
            return $"Detector[{DetectorName}, Enabled={IsEnabled}, Priority={Priority}]";
        }
    }
}