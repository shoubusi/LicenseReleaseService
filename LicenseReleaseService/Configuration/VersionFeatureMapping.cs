using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Configuration element for feature code mappings
    /// </summary>
    public class FeatureMappingElement : ConfigurationElement
    {
        [ConfigurationProperty("version", IsRequired = true)]
        [StringValidator(MinLength = 4, MaxLength = 4)]
        public string Version
        {
            get { return (string)this["version"]; }
            set { this["version"] = value; }
        }

        [ConfigurationProperty("featureCode", IsRequired = true)]
        [StringValidator(MinLength = 1)]
        public string FeatureCode
        {
            get { return (string)this["featureCode"]; }
            set { this["featureCode"] = value; }
        }

        [ConfigurationProperty("featureName", DefaultValue = "")]
        public string FeatureName
        {
            get { return (string)this["featureName"]; }
            set { this["featureName"] = value; }
        }

        [ConfigurationProperty("description", DefaultValue = "")]
        public string Description
        {
            get { return (string)this["description"]; }
            set { this["description"] = value; }
        }

        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled
        {
            get { return (bool)this["enabled"]; }
            set { this["enabled"] = value; }
        }

        [ConfigurationProperty("priority", DefaultValue = 50)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int Priority
        {
            get { return (int)this["priority"]; }
            set { this["priority"] = value; }
        }

        [ConfigurationProperty("category", DefaultValue = "General")]
        public string Category
        {
            get { return (string)this["category"]; }
            set { this["category"] = value; }
        }

        [ConfigurationProperty("maxLicenses", DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MaxLicenses
        {
            get { return (int)this["maxLicenses"]; }
            set { this["maxLicenses"] = value; }
        }

        [ConfigurationProperty("minLicenses", DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MinLicenses
        {
            get { return (int)this["minLicenses"]; }
            set { this["minLicenses"] = value; }
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

        [ConfigurationProperty("inheritFrom", DefaultValue = "")]
        public string InheritFrom
        {
            get { return (string)this["inheritFrom"]; }
            set { this["inheritFrom"] = value; }
        }

        [ConfigurationProperty("conditions", DefaultValue = "")]
        public string Conditions
        {
            get { return (string)this["conditions"]; }
            set { this["conditions"] = value; }
        }

        [ConfigurationProperty("aliases", DefaultValue = "")]
        public string Aliases
        {
            get { return (string)this["aliases"]; }
            set { this["aliases"] = value; }
        }

        [ConfigurationProperty("metadata", DefaultValue = "")]
        public string Metadata
        {
            get { return (string)this["metadata"]; }
            set { this["metadata"] = value; }
        }

        /// <summary>
        /// Gets the parsed aliases as a list
        /// </summary>
        public List<string> ParsedAliases
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Aliases))
                    return new List<string>();

                return Aliases.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct()
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the parsed conditions as a dictionary
        /// </summary>
        public Dictionary<string, string> ParsedConditions
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Conditions))
                    return new Dictionary<string, string>();

                return Conditions.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c) && c.Contains('='))
                    .Select(c => c.Split(new[] { '=' }, 2))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
            }
        }

        /// <summary>
        /// Gets the parsed metadata as a dictionary
        /// </summary>
        public Dictionary<string, string> ParsedMetadata
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Metadata))
                    return new Dictionary<string, string>();

                return Metadata.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrWhiteSpace(m) && m.Contains(':'))
                    .Select(m => m.Split(new[] { ':' }, 2))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
            }
        }

        /// <summary>
        /// Validates the feature mapping element
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

                // Validate feature code format
                if (!IsValidFeatureCodeFormat(FeatureCode))
                {
                    errors.Add($"Invalid feature code format: {FeatureCode}. Feature codes must be alphanumeric with optional underscores and hyphens");
                }

                // Validate feature code uniqueness (this will be checked at collection level)
                if (string.IsNullOrWhiteSpace(FeatureCode))
                {
                    errors.Add("Feature code cannot be empty");
                }

                // Validate priority range
                if (Priority < 1 || Priority > 100)
                {
                    errors.Add("Priority must be between 1 and 100");
                }

                // Validate license limits
                if (MaxLicenses < 0)
                {
                    errors.Add("Max licenses cannot be negative");
                }

                if (MinLicenses < 0)
                {
                    errors.Add("Min licenses cannot be negative");
                }

                if (MaxLicenses > 0 && MinLicenses > MaxLicenses)
                {
                    errors.Add("Min licenses cannot be greater than max licenses");
                }

                // Validate timeout
                if (Timeout <= 0)
                {
                    errors.Add("Timeout must be greater than zero");
                }

                // Validate retry count
                if (RetryCount < 0)
                {
                    errors.Add("Retry count cannot be negative");
                }

                // Validate category
                if (string.IsNullOrWhiteSpace(Category))
                {
                    errors.Add("Category cannot be empty");
                }

                // Validate inheritance configuration
                if (!string.IsNullOrWhiteSpace(InheritFrom))
                {
                    if (InheritFrom.Equals(FeatureCode, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Feature code cannot inherit from itself");
                    }

                    if (!IsValidFeatureCodeFormat(InheritFrom))
                    {
                        errors.Add($"Invalid inheritFrom feature code format: {InheritFrom}");
                    }
                }

                // Validate aliases format
                foreach (var alias in ParsedAliases)
                {
                    if (!IsValidFeatureCodeFormat(alias))
                    {
                        errors.Add($"Invalid alias format: {alias}");
                    }
                }

                // Validate conditions format
                foreach (var condition in ParsedConditions)
                {
                    if (string.IsNullOrWhiteSpace(condition.Key) || string.IsNullOrWhiteSpace(condition.Value))
                    {
                        errors.Add($"Invalid condition format: {condition.Key}={condition.Value}");
                    }
                }

                // Validate metadata format
                foreach (var metadata in ParsedMetadata)
                {
                    if (string.IsNullOrWhiteSpace(metadata.Key) || string.IsNullOrWhiteSpace(metadata.Value))
                    {
                        errors.Add($"Invalid metadata format: {metadata.Key}:{metadata.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Feature mapping validation error: {ex.Message}");
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

            return Regex.IsMatch(version, @"^20[0-9]{2}$");
        }

        /// <summary>
        /// Validates that a feature code is in the correct format
        /// </summary>
        /// <param name="featureCode">Feature code to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool IsValidFeatureCodeFormat(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
                return false;

            return Regex.IsMatch(featureCode, @"^[A-Za-z0-9_\-]+$");
        }

        /// <summary>
        /// Returns a string representation of the feature mapping
        /// </summary>
        public override string ToString()
        {
            return $"FeatureMapping[Version={Version}, FeatureCode={FeatureCode}, Name={FeatureName}, Category={Category}, Enabled={Enabled}, Priority={Priority}]";
        }

        /// <summary>
        /// Checks if this mapping matches a specific feature code
        /// </summary>
        /// <param name="featureCode">Feature code to check</param>
        /// <returns>True if matches, false otherwise</returns>
        public bool MatchesFeatureCode(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
                return false;

            return FeatureCode.Equals(featureCode, StringComparison.OrdinalIgnoreCase) ||
                   ParsedAliases.Any(alias => alias.Equals(featureCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if this mapping is valid for a specific version
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValidForVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            return Version.Equals(version, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the conditions are met
        /// </summary>
        /// <param name="context">Context dictionary for condition evaluation</param>
        /// <returns>True if conditions are met, false otherwise</returns>
        public bool ConditionsMet(Dictionary<string, string> context)
        {
            if (ParsedConditions.Count == 0)
                return true;

            foreach (var condition in ParsedConditions)
            {
                if (context.TryGetValue(condition.Key, out var contextValue))
                {
                    if (!contextValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a copy of this feature mapping
        /// </summary>
        /// <returns>Copy of the feature mapping</returns>
        public FeatureMappingElement Copy()
        {
            return new FeatureMappingElement
            {
                Version = Version,
                FeatureCode = FeatureCode,
                FeatureName = FeatureName,
                Description = Description,
                Enabled = Enabled,
                Priority = Priority,
                Category = Category,
                MaxLicenses = MaxLicenses,
                MinLicenses = MinLicenses,
                Timeout = Timeout,
                RetryCount = RetryCount,
                InheritFrom = InheritFrom,
                Conditions = Conditions,
                Aliases = Aliases,
                Metadata = Metadata
            };
        }

        /// <summary>
        /// Merges this mapping with another mapping (for inheritance)
        /// </summary>
        /// <param name="parentMapping">Parent mapping to inherit from</param>
        /// <returns>Merged mapping</returns>
        public FeatureMappingElement MergeWith(FeatureMappingElement parentMapping)
        {
            if (parentMapping == null)
                return this;

            var merged = new FeatureMappingElement();

            // Version and FeatureCode cannot be inherited
            merged.Version = Version;
            merged.FeatureCode = FeatureCode;

            // Inherit properties if not set
            merged.FeatureName = FeatureName ?? parentMapping.FeatureName;
            merged.Description = Description ?? parentMapping.Description;
            merged.Category = Category ?? parentMapping.Category;
            merged.Timeout = Timeout != 30 ? Timeout : parentMapping.Timeout;
            merged.RetryCount = RetryCount != 3 ? RetryCount : parentMapping.RetryCount;
            merged.MaxLicenses = MaxLicenses != 0 ? MaxLicenses : parentMapping.MaxLicenses;
            merged.MinLicenses = MinLicenses != 0 ? MinLicenses : parentMapping.MinLicenses;
            merged.Priority = Priority != 50 ? Priority : parentMapping.Priority;
            merged.Conditions = Conditions ?? parentMapping.Conditions;
            merged.Aliases = Aliases ?? parentMapping.Aliases;
            merged.Metadata = Metadata ?? parentMapping.Metadata;

            // Enabled status is not inherited
            merged.Enabled = Enabled;

            return merged;
        }

        /// <summary>
        /// Converts the mapping to a dictionary for easier access
        /// </summary>
        /// <returns>Dictionary representation of the mapping</returns>
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["Version"] = Version,
                ["FeatureCode"] = FeatureCode,
                ["FeatureName"] = FeatureName,
                ["Description"] = Description,
                ["Enabled"] = Enabled,
                ["Priority"] = Priority,
                ["Category"] = Category,
                ["MaxLicenses"] = MaxLicenses,
                ["MinLicenses"] = MinLicenses,
                ["Timeout"] = Timeout,
                ["RetryCount"] = RetryCount,
                ["InheritFrom"] = InheritFrom,
                ["Aliases"] = ParsedAliases,
                ["Conditions"] = ParsedConditions,
                ["Metadata"] = ParsedMetadata
            };
        }
    }

    /// <summary>
    /// Configuration collection for feature mappings
    /// </summary>
    public class FeatureMappingCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Gets the type of the configuration element
        /// </summary>
        protected override ConfigurationElement CreateNewElement()
        {
            return new FeatureMappingElement();
        }

        /// <summary>
        /// Gets the element key for the specified configuration element
        /// </summary>
        /// <param name="element">Configuration element</param>
        /// <returns>Element key</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            var mapping = (FeatureMappingElement)element;
            return $"{mapping.Version}_{mapping.FeatureCode}";
        }

        /// <summary>
        /// Gets or sets the feature mapping at the specified index
        /// </summary>
        /// <param name="index">Index</param>
        /// <returns>Feature mapping element</returns>
        public FeatureMappingElement this[int index]
        {
            get { return (FeatureMappingElement)BaseGet(index); }
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
        /// Gets or sets the feature mapping for the specified version and feature code
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="featureCode">Feature code</param>
        /// <returns>Feature mapping element</returns>
        public FeatureMappingElement this[string version, string featureCode]
        {
            get { return (FeatureMappingElement)BaseGet($"{version}_{featureCode}"); }
        }

        /// <summary>
        /// Adds a feature mapping to the collection
        /// </summary>
        /// <param name="featureMapping">Feature mapping to add</param>
        public void Add(FeatureMappingElement featureMapping)
        {
            BaseAdd(featureMapping);
        }

        /// <summary>
        /// Removes a feature mapping from the collection
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="featureCode">Feature code</param>
        public void Remove(string version, string featureCode)
        {
            BaseRemove($"{version}_{featureCode}");
        }

        /// <summary>
        /// Removes a feature mapping at the specified index
        /// </summary>
        /// <param name="index">Index to remove</param>
        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        /// <summary>
        /// Clears all feature mappings from the collection
        /// </summary>
        public void Clear()
        {
            BaseClear();
        }

        /// <summary>
        /// Gets all enabled feature mappings
        /// </summary>
        /// <returns>List of enabled feature mappings</returns>
        public List<FeatureMappingElement> GetEnabledMappings()
        {
            var result = new List<FeatureMappingElement>();
            foreach (FeatureMappingElement mapping in this)
            {
                if (mapping.Enabled)
                {
                    result.Add(mapping);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets feature mappings for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>List of feature mappings for the specified version</returns>
        public List<FeatureMappingElement> GetMappingsForVersion(string version)
        {
            var result = new List<FeatureMappingElement>();
            foreach (FeatureMappingElement mapping in this)
            {
                if (mapping.IsValidForVersion(version))
                {
                    result.Add(mapping);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets feature mappings for a specific feature code across all versions
        /// </summary>
        /// <param name="featureCode">Feature code</param>
        /// <returns>List of feature mappings for the specified feature code</returns>
        public List<FeatureMappingElement> GetMappingsForFeatureCode(string featureCode)
        {
            var result = new List<FeatureMappingElement>();
            foreach (FeatureMappingElement mapping in this)
            {
                if (mapping.MatchesFeatureCode(featureCode))
                {
                    result.Add(mapping);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets feature mappings by category
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>List of feature mappings in the specified category</returns>
        public List<FeatureMappingElement> GetMappingsByCategory(string category)
        {
            var result = new List<FeatureMappingElement>();
            foreach (FeatureMappingElement mapping in this)
            {
                if (mapping.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(mapping);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets all unique categories
        /// </summary>
        /// <returns>List of unique category names</returns>
        public List<string> GetUniqueCategories()
        {
            var categories = new HashSet<string>();
            foreach (FeatureMappingElement mapping in this)
            {
                categories.Add(mapping.Category);
            }
            return categories.OrderBy(c => c).ToList();
        }

        /// <summary>
        /// Gets feature mappings sorted by priority
        /// </summary>
        /// <returns>List of feature mappings sorted by priority</returns>
        public List<FeatureMappingElement> GetSortedMappings()
        {
            var result = new List<FeatureMappingElement>();
            foreach (FeatureMappingElement mapping in this)
            {
                result.Add(mapping);
            }
            return result.OrderByDescending(m => m.Priority).ToList();
        }

        /// <summary>
        /// Validates all feature mappings in the collection
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> ValidateAll()
        {
            var errors = new List<string>();

            try
            {
                // Check for duplicate feature codes within the same version
                var versionFeatureGroups = this.Cast<FeatureMappingElement>()
                    .GroupBy(m => new { m.Version, FeatureCode = m.FeatureCode.ToLower() })
                    .Where(g => g.Count() > 1);

                foreach (var group in versionFeatureGroups)
                {
                    errors.Add($"Duplicate feature code '{group.Key.FeatureCode}' found in version {group.Key.Version}");
                }

                // Check for conflicting aliases
                var aliasGroups = this.Cast<FeatureMappingElement>()
                    .SelectMany(m => m.ParsedAliases.Select(a => new { m.Version, Alias = a.ToLower(), OriginalFeatureCode = m.FeatureCode }))
                    .GroupBy(x => new { x.Version, x.Alias })
                    .Where(g => g.Count() > 1);

                foreach (var group in aliasGroups)
                {
                    var featureCodes = string.Join(", ", group.Select(x => x.OriginalFeatureCode));
                    errors.Add($"Conflicting alias '{group.Key.Alias}' in version {group.Key.Version} used by feature codes: {featureCodes}");
                }

                // Validate inheritance references
                var allFeatureCodes = this.Cast<FeatureMappingElement>()
                    .Where(m => m.Enabled)
                    .Select(m => m.FeatureCode.ToLower())
                    .ToHashSet();

                foreach (FeatureMappingElement mapping in this)
                {
                    if (!string.IsNullOrWhiteSpace(mapping.InheritFrom) &&
                        !allFeatureCodes.Contains(mapping.InheritFrom.ToLower()))
                    {
                        errors.Add($"Feature code {mapping.FeatureCode} inherits from non-existent feature code: {mapping.InheritFrom}");
                    }
                }

                // Validate individual mappings
                foreach (FeatureMappingElement mapping in this)
                {
                    errors.AddRange(mapping.Validate());
                }

                // Check for circular inheritance
                var circularReferences = FindCircularInheritance();
                errors.AddRange(circularReferences);
            }
            catch (Exception ex)
            {
                errors.Add($"Feature mapping collection validation error: {ex.Message}");
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

            foreach (FeatureMappingElement mapping in this)
            {
                var key = $"{mapping.Version}_{mapping.FeatureCode}";
                if (!visited.Contains(key))
                {
                    if (HasCircularInheritance(mapping.Version, mapping.FeatureCode, visited, recursionStack))
                    {
                        errors.Add($"Circular inheritance detected in feature mapping: {mapping.Version}.{mapping.FeatureCode}");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks if a feature mapping has circular inheritance
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <param name="featureCode">Feature code to check</param>
        /// <param name="visited">Set of visited mappings</param>
        /// <param name="recursionStack">Current recursion stack</param>
        /// <returns>True if circular inheritance detected</returns>
        private bool HasCircularInheritance(string version, string featureCode, HashSet<string> visited, HashSet<string> recursionStack)
        {
            var key = $"{version}_{featureCode}";
            visited.Add(key);
            recursionStack.Add(key);

            var mapping = this[version, featureCode];
            if (mapping != null && !string.IsNullOrWhiteSpace(mapping.InheritFrom))
            {
                var parentKey = $"{version}_{mapping.InheritFrom}";
                if (!visited.Contains(parentKey))
                {
                    if (HasCircularInheritance(version, mapping.InheritFrom, visited, recursionStack))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(parentKey))
                {
                    return true;
                }
            }

            recursionStack.Remove(key);
            return false;
        }

        /// <summary>
        /// Gets statistics about the feature mapping collection
        /// </summary>
        /// <returns>Feature mapping statistics</returns>
        public FeatureMappingStatistics GetStatistics()
        {
            var enabledMappings = GetEnabledMappings();
            var uniqueCategories = GetUniqueCategories();
            var versionStats = new Dictionary<string, int>();
            var categoryStats = new Dictionary<string, int>();

            foreach (FeatureMappingElement mapping in this)
            {
                // Count by version
                if (!versionStats.ContainsKey(mapping.Version))
                {
                    versionStats[mapping.Version] = 0;
                }
                versionStats[mapping.Version]++;

                // Count by category
                if (!categoryStats.ContainsKey(mapping.Category))
                {
                    categoryStats[mapping.Category] = 0;
                }
                categoryStats[mapping.Category]++;
            }

            return new FeatureMappingStatistics
            {
                TotalMappings = Count,
                EnabledMappings = enabledMappings.Count,
                UniqueCategories = uniqueCategories.Count,
                VersionsSupported = versionStats.Count,
                VersionStatistics = versionStats,
                CategoryStatistics = categoryStats
            };
        }
    }

    /// <summary>
    /// Configuration section for feature mappings
    /// </summary>
    public class FeatureMappingSection : ConfigurationSection
    {
        [ConfigurationProperty("mappings", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(FeatureMappingCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public FeatureMappingCollection Mappings
        {
            get { return (FeatureMappingCollection)this["mappings"] ?? new FeatureMappingCollection(); }
        }

        [ConfigurationProperty("enableInheritance", DefaultValue = true)]
        public bool EnableInheritance
        {
            get { return (bool)this["enableInheritance"]; }
            set { this["enableInheritance"] = value; }
        }

        [ConfigurationProperty("defaultTimeout", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 300)]
        public int DefaultTimeout
        {
            get { return (int)this["defaultTimeout"]; }
            set { this["defaultTimeout"] = value; }
        }

        [ConfigurationProperty("defaultRetryCount", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int DefaultRetryCount
        {
            get { return (int)this["defaultRetryCount"]; }
            set { this["defaultRetryCount"] = value; }
        }

        [ConfigurationProperty("enableCaching", DefaultValue = true)]
        public bool EnableCaching
        {
            get { return (bool)this["enableCaching"]; }
            set { this["enableCaching"] = value; }
        }

        [ConfigurationProperty("cacheExpiration", DefaultValue = "00:05:00")]
        public TimeSpan CacheExpiration
        {
            get { return (TimeSpan)this["cacheExpiration"]; }
            set { this["cacheExpiration"] = value; }
        }

        [ConfigurationProperty("enableWildcardMatching", DefaultValue = false)]
        public bool EnableWildcardMatching
        {
            get { return (bool)this["enableWildcardMatching"]; }
            set { this["enableWildcardMatching"] = value; }
        }

        [ConfigurationProperty("wildcardCharacter", DefaultValue = "*")]
        public string WildcardCharacter
        {
            get { return (string)this["wildcardCharacter"]; }
            set { this["wildcardCharacter"] = value; }
        }

        /// <summary>
        /// Gets the mapping for a specific version and feature code
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="featureCode">Feature code</param>
        /// <returns>Feature mapping element or null if not found</returns>
        public FeatureMappingElement GetMapping(string version, string featureCode)
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(featureCode))
                return null;

            return Mappings[version, featureCode];
        }

        /// <summary>
        /// Gets the mapping for a specific version and feature code with inheritance applied
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="featureCode">Feature code</param>
        /// <returns>Feature mapping element with inheritance applied</returns>
        public FeatureMappingElement GetMappingWithInheritance(string version, string featureCode)
        {
            var mapping = GetMapping(version, featureCode);
            if (mapping == null)
                return null;

            if (!EnableInheritance || string.IsNullOrWhiteSpace(mapping.InheritFrom))
                return mapping;

            var parentMapping = GetMappingWithInheritance(version, mapping.InheritFrom);
            return mapping.MergeWith(parentMapping);
        }

        /// <summary>
        /// Gets the best matching mapping for a feature code (supports wildcard matching)
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="featureCode">Feature code to match</param>
        /// <returns>Best matching feature mapping element</returns>
        public FeatureMappingElement GetBestMapping(string version, string featureCode)
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(featureCode))
                return null;

            // Try exact match first
            var exactMapping = GetMappingWithInheritance(version, featureCode);
            if (exactMapping != null && exactMapping.Enabled)
            {
                return exactMapping;
            }

            // Try wildcard matching if enabled
            if (EnableWildcardMatching)
            {
                var wildcardMapping = GetWildcardMapping(version, featureCode);
                if (wildcardMapping != null && wildcardMapping.Enabled)
                {
                    return wildcardMapping;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all mappings for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>List of feature mappings for the specified version</returns>
        public List<FeatureMappingElement> GetMappingsForVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new List<FeatureMappingElement>();

            return Mappings.GetMappingsForVersion(version);
        }

        /// <summary>
        /// Gets all enabled mappings for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>List of enabled feature mappings for the specified version</returns>
        public List<FeatureMappingElement> GetEnabledMappingsForVersion(string version)
        {
            return GetMappingsForVersion(version).Where(m => m.Enabled).ToList();
        }

        /// <summary>
        /// Gets all unique feature codes for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>List of unique feature codes for the specified version</returns>
        public List<string> GetFeatureCodesForVersion(string version)
        {
            return GetMappingsForVersion(version)
                .Select(m => m.FeatureCode)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
        }

        /// <summary>
        /// Gets mappings that match specific conditions
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="context">Context dictionary for condition evaluation</param>
        /// <returns>List of feature mappings that meet the conditions</returns>
        public List<FeatureMappingElement> GetMappingsMatchingConditions(string version, Dictionary<string, string> context)
        {
            return GetMappingsForVersion(version)
                .Where(m => m.Enabled && m.ConditionsMet(context))
                .ToList();
        }

        /// <summary>
        /// Gets mappings by priority range
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="minPriority">Minimum priority (inclusive)</param>
        /// <param name="maxPriority">Maximum priority (inclusive)</param>
        /// <returns>List of feature mappings within the priority range</returns>
        public List<FeatureMappingElement> GetMappingsByPriorityRange(string version, int minPriority, int maxPriority)
        {
            return GetMappingsForVersion(version)
                .Where(m => m.Priority >= minPriority && m.Priority <= maxPriority)
                .OrderByDescending(m => m.Priority)
                .ToList();
        }

        /// <summary>
        /// Validates the feature mapping section
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate individual mappings
                errors.AddRange(Mappings.ValidateAll());

                // Validate default timeout
                if (DefaultTimeout <= 0)
                {
                    errors.Add("Default timeout must be greater than zero");
                }

                if (DefaultTimeout > 300)
                {
                    errors.Add("Default timeout should not exceed 300 seconds");
                }

                // Validate default retry count
                if (DefaultRetryCount < 0)
                {
                    errors.Add("Default retry count cannot be negative");
                }

                if (DefaultRetryCount > 10)
                {
                    errors.Add("Default retry count should not exceed 10");
                }

                // Validate cache settings
                if (EnableCaching && CacheExpiration <= TimeSpan.Zero)
                {
                    errors.Add("Cache expiration must be greater than zero when caching is enabled");
                }

                // Validate wildcard character
                if (EnableWildcardMatching && string.IsNullOrWhiteSpace(WildcardCharacter))
                {
                    errors.Add("Wildcard character cannot be empty when wildcard matching is enabled");
                }

                // Ensure at least one mapping is configured
                if (Mappings.Count == 0)
                {
                    errors.Add("No feature mappings found");
                }

                // Ensure at least one enabled mapping exists
                if (!Mappings.GetEnabledMappings().Any())
                {
                    errors.Add("No enabled feature mappings found");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Feature mapping section validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the feature mapping section
        /// </summary>
        public override string ToString()
        {
            var stats = Mappings.GetStatistics();
            return $"FeatureMapping[Mappings={stats.TotalMappings}, Enabled={stats.EnabledMappings}, Categories={stats.UniqueCategories}, Versions={stats.VersionsSupported}, Inheritance={EnableInheritance}]";
        }

        /// <summary>
        /// Gets a wildcard mapping for a feature code
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="featureCode">Feature code to match</param>
        /// <returns>Best matching wildcard feature mapping element</returns>
        private FeatureMappingElement GetWildcardMapping(string version, string featureCode)
        {
            var mappings = GetMappingsForVersion(version);
            var wildcardMappings = mappings.Where(m => m.FeatureCode.Contains(WildcardCharacter)).ToList();

            // Find the best matching wildcard pattern
            var bestMatch = wildcardMappings
                .Select(m => new { Mapping = m, MatchQuality = CalculateWildcardMatchQuality(m.FeatureCode, featureCode) })
                .Where(x => x.MatchQuality > 0)
                .OrderByDescending(x => x.MatchQuality)
                .FirstOrDefault();

            return bestMatch?.Mapping;
        }

        /// <summary>
        /// Calculates the quality of a wildcard match
        /// </summary>
        /// <param name="pattern">Wildcard pattern</param>
        /// <param name="featureCode">Feature code to match</param>
        /// <returns>Match quality score (higher is better)</returns>
        private int CalculateWildcardMatchQuality(string pattern, string featureCode)
        {
            // Simple wildcard matching - * matches any sequence
            if (pattern == WildcardCharacter)
            {
                return 1; // Lowest quality match
            }

            if (pattern.EndsWith(WildcardCharacter))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                if (featureCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return prefix.Length + 1; // Better match for longer prefixes
                }
            }

            if (pattern.StartsWith(WildcardCharacter))
            {
                var suffix = pattern.Substring(1);
                if (featureCode.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return suffix.Length + 1; // Better match for longer suffixes
                }
            }

            if (pattern.Contains(WildcardCharacter))
            {
                var parts = pattern.Split(new[] { WildcardCharacter }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.All(part => featureCode.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return parts.Sum(part => part.Length); // Quality based on total matched parts length
                }
            }

            return 0; // No match
        }
    }

    /// <summary>
    /// Represents statistics about feature mappings
    /// </summary>
    public class FeatureMappingStatistics
    {
        /// <summary>
        /// Gets or sets the total number of mappings
        /// </summary>
        public int TotalMappings { get; set; }

        /// <summary>
        /// Gets or sets the number of enabled mappings
        /// </summary>
        public int EnabledMappings { get; set; }

        /// <summary>
        /// Gets or sets the number of unique categories
        /// </summary>
        public int UniqueCategories { get; set; }

        /// <summary>
        /// Gets or sets the number of versions supported
        /// </summary>
        public int VersionsSupported { get; set; }

        /// <summary>
        /// Gets or sets the version statistics
        /// </summary>
        public Dictionary<string, int> VersionStatistics { get; set; }

        /// <summary>
        /// Gets or sets the category statistics
        /// </summary>
        public Dictionary<string, int> CategoryStatistics { get; set; }

        /// <summary>
        /// Initializes a new instance of the FeatureMappingStatistics class
        /// </summary>
        public FeatureMappingStatistics()
        {
            VersionStatistics = new Dictionary<string, int>();
            CategoryStatistics = new Dictionary<string, int>();
        }
    }
}