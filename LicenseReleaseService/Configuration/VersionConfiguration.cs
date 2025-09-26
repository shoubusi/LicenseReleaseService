using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Configuration element for version-specific settings
    /// </summary>
    public class VersionConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("version", IsRequired = true)]
        [StringValidator(MinLength = 4, MaxLength = 4)]
        public string Version
        {
            get { return (string)this["version"]; }
            set { this["version"] = value; }
        }

        [ConfigurationProperty("licenseServer", DefaultValue = "")]
        public string LicenseServer
        {
            get { return (string)this["licenseServer"]; }
            set { this["licenseServer"] = value; }
        }

        [ConfigurationProperty("port", DefaultValue = 27000)]
        [IntegerValidator(MinValue = 1, MaxValue = 65535)]
        public int Port
        {
            get { return (int)this["port"]; }
            set { this["port"] = value; }
        }

        [ConfigurationProperty("timeout", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 300)]
        public int Timeout
        {
            get { return (int)this["timeout"]; }
            set { this["timeout"] = value; }
        }

        [ConfigurationProperty("retryCount", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int RetryCount
        {
            get { return (int)this["retryCount"]; }
            set { this["retryCount"] = value; }
        }

        [ConfigurationProperty("commandTimeout", DefaultValue = 60)]
        [IntegerValidator(MinValue = 1, MaxValue = 600)]
        public int CommandTimeout
        {
            get { return (int)this["commandTimeout"]; }
            set { this["commandTimeout"] = value; }
        }

        [ConfigurationProperty("maxConcurrentLicenses", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int MaxConcurrentLicenses
        {
            get { return (int)this["maxConcurrentLicenses"]; }
            set { this["maxConcurrentLicenses"] = value; }
        }

        [ConfigurationProperty("releaseDelay", DefaultValue = 1000)]
        [IntegerValidator(MinValue = 0, MaxValue = 10000)]
        public int ReleaseDelay
        {
            get { return (int)this["releaseDelay"]; }
            set { this["releaseDelay"] = value; }
        }

        [ConfigurationProperty("lmutilPath", DefaultValue = "")]
        public string LmutilPath
        {
            get { return (string)this["lmutilPath"]; }
            set { this["lmutilPath"] = value; }
        }

        [ConfigurationProperty("featureCodes", DefaultValue = "")]
        public string FeatureCodes
        {
            get { return (string)this["featureCodes"]; }
            set { this["featureCodes"] = value; }
        }

        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled
        {
            get { return (bool)this["enabled"]; }
            set { this["enabled"] = value; }
        }

        [ConfigurationProperty("priority", DefaultValue = 0)]
        [IntegerValidator(MinValue = 0, MaxValue = 100)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        [ConfigurationProperty("inheritFrom", DefaultValue = "")]
        public string InheritFrom
        {
            get { return (string)this["inheritFrom"]; }
            set { this["inheritFrom"] = value; }
        }

        [ConfigurationProperty("description", DefaultValue = "")]
        public string Description
        {
            get { return (string)this["description"]; }
            set { this["description"] = value; }
        }

        [ConfigurationProperty("enableHealthCheck", DefaultValue = true)]
        public bool EnableHealthCheck
        {
            get { return (bool)this["enableHealthCheck"]; }
            set { this["enableHealthCheck"] = value; }
        }

        [ConfigurationProperty("healthCheckInterval", DefaultValue = 300)]
        [IntegerValidator(MinValue = 30, MaxValue = 3600)]
        public int HealthCheckInterval
        {
            get { return (int)this["healthCheckInterval"]; }
            set { this["healthCheckInterval"] = value; }
        }

        [ConfigurationProperty("validatePath", DefaultValue = true)]
        public bool ValidatePath
        {
            get { return (bool)this["validatePath"]; }
            set { this["validatePath"] = value; }
        }

        [ConfigurationProperty("enableFallback", DefaultValue = true)]
        public bool EnableFallback
        {
            get { return (bool)this["enableFallback"]; }
            set { this["enableFallback"] = value; }
        }

        [ConfigurationProperty("fallbackTimeout", DefaultValue = 5000)]
        [IntegerValidator(MinValue = 1000, MaxValue = 30000)]
        public int FallbackTimeout
        {
            get { return (int)this["fallbackTimeout"]; }
            set { this["fallbackTimeout"] = value; }
        }

        /// <summary>
        /// Gets the parsed feature codes as a list
        /// </summary>
        public List<string> ParsedFeatureCodes
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FeatureCodes))
                    return new List<string>();

                return FeatureCodes.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct()
                    .ToList();
            }
        }

        /// <summary>
        /// Validates the version configuration element
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate version format
                if (!IsValidVersionFormat(Version))
                {
                    errors.Add($"Invalid version format: {Version}. Version must be in YYYY format (e.g., 2023, 2024)");
                }

                // Validate version range
                if (int.TryParse(Version, out int year))
                {
                    if (year < 2020 || year > 2025)
                    {
                        errors.Add($"Version {Version} is outside the supported range (2020-2025)");
                    }
                }

                // Validate license server if specified
                if (!string.IsNullOrWhiteSpace(LicenseServer) && !IsValidServerAddress(LicenseServer))
                {
                    errors.Add($"Invalid license server address: {LicenseServer}");
                }

                // Validate timeout relationships
                if (CommandTimeout < Timeout)
                {
                    errors.Add("Command timeout should be greater than or equal to timeout");
                }

                // Validate retry logic
                if (RetryCount > 0 && Timeout <= 0)
                {
                    errors.Add("Timeout must be greater than zero when retry count is greater than zero");
                }

                // Validate concurrent licenses
                if (MaxConcurrentLicenses < 1)
                {
                    errors.Add("Max concurrent licenses must be at least 1");
                }

                // Validate lmutil path if specified
                if (!string.IsNullOrWhiteSpace(LmutilPath) && !System.IO.File.Exists(LmutilPath))
                {
                    errors.Add($"lmutil.exe not found at: {LmutilPath}");
                }

                // Validate priority range
                if (Priority < 0 || Priority > 100)
                {
                    errors.Add("Priority must be between 0 and 100");
                }

                // Validate fallback settings
                if (EnableFallback && FallbackTimeout < 1000)
                {
                    errors.Add("Fallback timeout must be at least 1000ms when fallback is enabled");
                }

                // Validate health check settings
                if (EnableHealthCheck && HealthCheckInterval < 30)
                {
                    errors.Add("Health check interval must be at least 30 seconds when health check is enabled");
                }

                // Validate inheritance configuration
                if (!string.IsNullOrWhiteSpace(InheritFrom))
                {
                    if (InheritFrom.Equals(Version, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Version cannot inherit from itself");
                    }

                    if (!IsValidVersionFormat(InheritFrom))
                    {
                        errors.Add($"Invalid inheritFrom version format: {InheritFrom}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Version configuration validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Validates that a version string is in the correct format
        /// </summary>
        /// <param name="version">Version string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool IsValidVersionFormat(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(version, @"^20[0-9]{2}$");
        }

        /// <summary>
        /// Validates if the server address format is valid
        /// </summary>
        /// <param name="serverAddress">Server address to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool IsValidServerAddress(string serverAddress)
        {
            if (string.IsNullOrWhiteSpace(serverAddress))
                return false;

            try
            {
                // Check if it's a valid hostname or IP address
                return System.Net.IPAddress.TryParse(serverAddress, out _) ||
                       Uri.CheckHostName(serverAddress) != UriHostNameType.Unknown;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a string representation of the version configuration
        /// </summary>
        public override string ToString()
        {
            return $"VersionConfiguration[Version={Version}, Server={LicenseServer}:{Port}, Timeout={Timeout}s, Enabled={Enabled}, Priority={Priority}, FeatureCodes={ParsedFeatureCodes.Count}]";
        }

        /// <summary>
        /// Converts the configuration to a dictionary for easier access
        /// </summary>
        /// <returns>Dictionary representation of the configuration</returns>
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["Version"] = Version,
                ["LicenseServer"] = LicenseServer,
                ["Port"] = Port,
                ["Timeout"] = Timeout,
                ["RetryCount"] = RetryCount,
                ["CommandTimeout"] = CommandTimeout,
                ["MaxConcurrentLicenses"] = MaxConcurrentLicenses,
                ["ReleaseDelay"] = ReleaseDelay,
                ["LmutilPath"] = LmutilPath,
                ["FeatureCodes"] = ParsedFeatureCodes,
                ["Enabled"] = Enabled,
                ["Priority"] = Priority,
                ["InheritFrom"] = InheritFrom,
                ["Description"] = Description,
                ["EnableHealthCheck"] = EnableHealthCheck,
                ["HealthCheckInterval"] = HealthCheckInterval,
                ["ValidatePath"] = ValidatePath,
                ["EnableFallback"] = EnableFallback,
                ["FallbackTimeout"] = FallbackTimeout
            };
        }

        /// <summary>
        /// Merges this configuration with another configuration (for inheritance)
        /// </summary>
        /// <param name="parentConfig">Parent configuration to inherit from</param>
        /// <returns>Merged configuration</returns>
        public VersionConfigurationElement MergeWith(VersionConfigurationElement parentConfig)
        {
            if (parentConfig == null)
                return this;

            var merged = new VersionConfigurationElement();

            // Version cannot be inherited
            merged.Version = Version;

            // Inherit properties if not set or if explicitly marked for inheritance
            merged.LicenseServer = LicenseServer ?? parentConfig.LicenseServer;
            merged.Port = Port != 27000 ? Port : parentConfig.Port;
            merged.Timeout = Timeout != 30 ? Timeout : parentConfig.Timeout;
            merged.RetryCount = RetryCount != 3 ? RetryCount : parentConfig.RetryCount;
            merged.CommandTimeout = CommandTimeout != 60 ? CommandTimeout : parentConfig.CommandTimeout;
            merged.MaxConcurrentLicenses = MaxConcurrentLicenses != 5 ? MaxConcurrentLicenses : parentConfig.MaxConcurrentLicenses;
            merged.ReleaseDelay = ReleaseDelay != 1000 ? ReleaseDelay : parentConfig.ReleaseDelay;
            merged.LmutilPath = LmutilPath ?? parentConfig.LmutilPath;
            merged.FeatureCodes = FeatureCodes ?? parentConfig.FeatureCodes;
            merged.Enabled = Enabled;
            merged.Priority = Priority != 0 ? Priority : parentConfig.Priority;
            merged.Description = Description ?? parentConfig.Description;
            merged.EnableHealthCheck = EnableHealthCheck;
            merged.HealthCheckInterval = HealthCheckInterval != 300 ? HealthCheckInterval : parentConfig.HealthCheckInterval;
            merged.ValidatePath = ValidatePath;
            merged.EnableFallback = EnableFallback;
            merged.FallbackTimeout = FallbackTimeout != 5000 ? FallbackTimeout : parentConfig.FallbackTimeout;

            return merged;
        }

        /// <summary>
        /// Creates a copy of this configuration
        /// </summary>
        /// <returns>Copy of the configuration</returns>
        public VersionConfigurationElement Copy()
        {
            return new VersionConfigurationElement
            {
                Version = Version,
                LicenseServer = LicenseServer,
                Port = Port,
                Timeout = Timeout,
                RetryCount = RetryCount,
                CommandTimeout = CommandTimeout,
                MaxConcurrentLicenses = MaxConcurrentLicenses,
                ReleaseDelay = ReleaseDelay,
                LmutilPath = LmutilPath,
                FeatureCodes = FeatureCodes,
                Enabled = Enabled,
                Priority = Priority,
                InheritFrom = InheritFrom,
                Description = Description,
                EnableHealthCheck = EnableHealthCheck,
                HealthCheckInterval = HealthCheckInterval,
                ValidatePath = ValidatePath,
                EnableFallback = EnableFallback,
                FallbackTimeout = FallbackTimeout
            };
        }
    }

    /// <summary>
    /// Configuration collection for version-specific settings
    /// </summary>
    public class VersionConfigurationCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Gets the type of the configuration element
        /// </summary>
        protected override ConfigurationElement CreateNewElement()
        {
            return new VersionConfigurationElement();
        }

        /// <summary>
        /// Gets the element key for the specified configuration element
        /// </summary>
        /// <param name="element">Configuration element</param>
        /// <returns>Element key</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((VersionConfigurationElement)element).Version;
        }

        /// <summary>
        /// Gets or sets the version configuration at the specified index
        /// </summary>
        /// <param name="index">Index</param>
        /// <returns>Version configuration element</returns>
        public VersionConfigurationElement this[int index]
        {
            get { return (VersionConfigurationElement)BaseGet(index); }
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
        /// Gets or sets the version configuration for the specified version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>Version configuration element</returns>
        public new VersionConfigurationElement this[string version]
        {
            get { return (VersionConfigurationElement)BaseGet(version); }
        }

        /// <summary>
        /// Adds a version configuration to the collection
        /// </summary>
        /// <param name="versionConfig">Version configuration to add</param>
        public void Add(VersionConfigurationElement versionConfig)
        {
            BaseAdd(versionConfig);
        }

        /// <summary>
        /// Removes a version configuration from the collection
        /// </summary>
        /// <param name="version">Version to remove</param>
        public void Remove(string version)
        {
            BaseRemove(version);
        }

        /// <summary>
        /// Removes a version configuration at the specified index
        /// </summary>
        /// <param name="index">Index to remove</param>
        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        /// <summary>
        /// Clears all version configurations from the collection
        /// </summary>
        public void Clear()
        {
            BaseClear();
        }

        /// <summary>
        /// Gets all enabled version configurations
        /// </summary>
        /// <returns>List of enabled version configurations</returns>
        public List<VersionConfigurationElement> GetEnabledConfigurations()
        {
            var result = new List<VersionConfigurationElement>();
            foreach (VersionConfigurationElement config in this)
            {
                if (config.Enabled)
                {
                    result.Add(config);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets version configurations sorted by priority
        /// </summary>
        /// <returns>List of version configurations sorted by priority</returns>
        public List<VersionConfigurationElement> GetSortedConfigurations()
        {
            var result = new List<VersionConfigurationElement>();
            foreach (VersionConfigurationElement config in this)
            {
                result.Add(config);
            }
            return result.OrderByDescending(c => c.Priority).ToList();
        }

        /// <summary>
        /// Gets version configurations filtered by version range
        /// </summary>
        /// <param name="minVersion">Minimum version (inclusive)</param>
        /// <param name="maxVersion">Maximum version (inclusive)</param>
        /// <returns>List of version configurations in the specified range</returns>
        public List<VersionConfigurationElement> GetConfigurationsInRange(string minVersion, string maxVersion)
        {
            var result = new List<VersionConfigurationElement>();

            if (!int.TryParse(minVersion, out int minYear) || !int.TryParse(maxVersion, out int maxYear))
                return result;

            foreach (VersionConfigurationElement config in this)
            {
                if (int.TryParse(config.Version, out int configYear) &&
                    configYear >= minYear && configYear <= maxYear)
                {
                    result.Add(config);
                }
            }
            return result;
        }

        /// <summary>
        /// Validates all version configurations in the collection
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> ValidateAll()
        {
            var errors = new List<string>();

            try
            {
                // Check for duplicate versions
                var versionGroups = this.Cast<VersionConfigurationElement>()
                    .GroupBy(c => c.Version)
                    .Where(g => g.Count() > 1);

                foreach (var group in versionGroups)
                {
                    errors.Add($"Duplicate version configuration found: {group.Key}");
                }

                // Validate inheritance references
                var allVersions = this.Cast<VersionConfigurationElement>()
                    .Select(c => c.Version)
                    .ToHashSet();

                foreach (VersionConfigurationElement config in this)
                {
                    if (!string.IsNullOrWhiteSpace(config.InheritFrom) &&
                        !allVersions.Contains(config.InheritFrom))
                    {
                        errors.Add($"Version {config.Version} inherits from non-existent version: {config.InheritFrom}");
                    }
                }

                // Validate individual configurations
                foreach (VersionConfigurationElement config in this)
                {
                    errors.AddRange(config.Validate());
                }

                // Check for circular inheritance
                var circularReferences = FindCircularInheritance();
                errors.AddRange(circularReferences);
            }
            catch (Exception ex)
            {
                errors.Add($"Version configuration collection validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Finds circular inheritance references in the collection
        /// </summary>
        /// <returns>List of circular inheritance errors</returns>
        private List<string> FindCircularInheritance()
        {
            var errors = new List<string>();
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (VersionConfigurationElement config in this)
            {
                if (!visited.Contains(config.Version))
                {
                    if (HasCircularInheritance(config.Version, visited, recursionStack))
                    {
                        errors.Add($"Circular inheritance detected involving version: {config.Version}");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks if a version has circular inheritance
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <param name="visited">Set of visited versions</param>
        /// <param name="recursionStack">Current recursion stack</param>
        /// <returns>True if circular inheritance detected</returns>
        private bool HasCircularInheritance(string version, HashSet<string> visited, HashSet<string> recursionStack)
        {
            visited.Add(version);
            recursionStack.Add(version);

            var config = this[version];
            if (config != null && !string.IsNullOrWhiteSpace(config.InheritFrom))
            {
                if (!visited.Contains(config.InheritFrom))
                {
                    if (HasCircularInheritance(config.InheritFrom, visited, recursionStack))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(config.InheritFrom))
                {
                    return true;
                }
            }

            recursionStack.Remove(version);
            return false;
        }
    }

    /// <summary>
    /// Configuration section for version-specific settings
    /// </summary>
    public class VersionConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("versions", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(VersionConfigurationCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public VersionConfigurationCollection Versions
        {
            get { return (VersionConfigurationCollection)this["versions"] ?? new VersionConfigurationCollection(); }
        }

        [ConfigurationProperty("defaultVersion", DefaultValue = "")]
        public string DefaultVersion
        {
            get { return (string)this["defaultVersion"]; }
            set { this["defaultVersion"] = value; }
        }

        [ConfigurationProperty("enableVersionInheritance", DefaultValue = true)]
        public bool EnableVersionInheritance
        {
            get { return (bool)this["enableVersionInheritance"]; }
            set { this["enableVersionInheritance"] = value; }
        }

        [ConfigurationProperty("fallbackToDefault", DefaultValue = true)]
        public bool FallbackToDefault
        {
            get { return (bool)this["fallbackToDefault"]; }
            set { this["fallbackToDefault"] = value; }
        }

        [ConfigurationProperty("autoDetectVersion", DefaultValue = true)]
        public bool AutoDetectVersion
        {
            get { return (bool)this["autoDetectVersion"]; }
            set { this["autoDetectVersion"] = value; }
        }

        [ConfigurationProperty("versionDetectionTimeout", DefaultValue = "00:00:30")]
        public TimeSpan VersionDetectionTimeout
        {
            get { return (TimeSpan)this["versionDetectionTimeout"]; }
            set { this["versionDetectionTimeout"] = value; }
        }

        [ConfigurationProperty("enableVersionCaching", DefaultValue = true)]
        public bool EnableVersionCaching
        {
            get { return (bool)this["enableVersionCaching"]; }
            set { this["enableVersionCaching"] = value; }
        }

        [ConfigurationProperty("cacheExpiration", DefaultValue = "00:05:00")]
        public TimeSpan CacheExpiration
        {
            get { return (TimeSpan)this["cacheExpiration"]; }
            set { this["cacheExpiration"] = value; }
        }

        /// <summary>
        /// Gets the configuration for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>Version configuration element or null if not found</returns>
        public VersionConfigurationElement GetVersionConfiguration(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            return Versions[version];
        }

        /// <summary>
        /// Gets the configuration for a specific version with inheritance applied
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>Version configuration element with inheritance applied</returns>
        public VersionConfigurationElement GetVersionConfigurationWithInheritance(string version)
        {
            var config = GetVersionConfiguration(version);
            if (config == null)
                return null;

            if (!EnableVersionInheritance || string.IsNullOrWhiteSpace(config.InheritFrom))
                return config;

            var parentConfig = GetVersionConfigurationWithInheritance(config.InheritFrom);
            return config.MergeWith(parentConfig);
        }

        /// <summary>
        /// Gets the default version configuration
        /// </summary>
        /// <returns>Default version configuration or null if not set</returns>
        public VersionConfigurationElement GetDefaultVersionConfiguration()
        {
            if (string.IsNullOrWhiteSpace(DefaultVersion))
                return null;

            return GetVersionConfigurationWithInheritance(DefaultVersion);
        }

        /// <summary>
        /// Gets all available versions
        /// </summary>
        /// <returns>List of available version identifiers</returns>
        public List<string> GetAvailableVersions()
        {
            var versions = new List<string>();
            foreach (VersionConfigurationElement config in Versions)
            {
                versions.Add(config.Version);
            }
            return versions;
        }

        /// <summary>
        /// Gets all enabled versions
        /// </summary>
        /// <returns>List of enabled version identifiers</returns>
        public List<string> GetEnabledVersions()
        {
            var versions = new List<string>();
            foreach (VersionConfigurationElement config in Versions)
            {
                if (config.Enabled)
                {
                    versions.Add(config.Version);
                }
            }
            return versions;
        }

        /// <summary>
        /// Validates the version configuration section
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate individual version configurations
                errors.AddRange(Versions.ValidateAll());

                // Validate default version
                if (!string.IsNullOrWhiteSpace(DefaultVersion))
                {
                    if (!Versions.GetEnabledConfigurations().Any(c => c.Version.Equals(DefaultVersion, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"Default version '{DefaultVersion}' is not found or not enabled");
                    }

                    // Validate default version format
                    if (!System.Text.RegularExpressions.Regex.IsMatch(DefaultVersion, @"^20[0-9]{2}$"))
                    {
                        errors.Add($"Invalid default version format: {DefaultVersion}");
                    }
                }

                // Validate timeout settings
                if (VersionDetectionTimeout <= TimeSpan.Zero)
                {
                    errors.Add("Version detection timeout must be greater than zero");
                }

                if (VersionDetectionTimeout > TimeSpan.FromMinutes(5))
                {
                    errors.Add("Version detection timeout should not exceed 5 minutes");
                }

                // Validate cache settings
                if (EnableVersionCaching && CacheExpiration <= TimeSpan.Zero)
                {
                    errors.Add("Cache expiration must be greater than zero when version caching is enabled");
                }

                // Ensure at least one version is configured
                if (Versions.Count == 0)
                {
                    errors.Add("No version configurations found");
                }

                // Ensure at least one enabled version exists
                if (!Versions.GetEnabledConfigurations().Any())
                {
                    errors.Add("No enabled version configurations found");
                }

                // Validate version range coverage
                var enabledVersions = Versions.GetEnabledConfigurations()
                    .Select(c => c.Version)
                    .ToList();

                if (enabledVersions.Count > 0)
                {
                    var minYear = enabledVersions.Select(v => int.Parse(v)).Min();
                    var maxYear = enabledVersions.Select(v => int.Parse(v)).Max();

                    if (maxYear - minYear > 10)
                    {
                        errors.Add($"Version range is too wide: {minYear}-{maxYear}. Consider limiting to supported versions.");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Version configuration section validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the version configuration section
        /// </summary>
        public override string ToString()
        {
            var enabledCount = Versions.GetEnabledConfigurations().Count;
            var totalCount = Versions.Count;

            return $"VersionConfiguration[Default={DefaultVersion}, Enabled={enabledCount}/{totalCount}, Inheritance={EnableVersionInheritance}, AutoDetect={AutoDetectVersion}]";
        }
    }
}