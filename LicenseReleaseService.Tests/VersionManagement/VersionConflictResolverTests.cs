using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.VersionManagement;
using LicenseReleaseService.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Comprehensive unit tests for VersionConflictResolver
    /// </summary>
    public class VersionConflictResolverTests
    {
        private readonly Mock<ILogger<VersionConflictResolver>> _mockLogger;
        private readonly ConflictResolutionConfiguration _configuration;
        private readonly VersionConflictResolver _resolver;

        public VersionConflictResolverTests()
        {
            _mockLogger = new Mock<ILogger<VersionConflictResolver>>();
            _configuration = new ConflictResolutionConfiguration
            {
                MaximumVersionSpan = 5,
                MaximumMemoryUsage = 1024 * 1024 * 1024, // 1GB
                MaximumCpuUsage = 80,
                MaximumLicenseServers = 2,
                EnableAutoResolution = true,
                ResolutionTimeout = TimeSpan.FromSeconds(30)
            };

            _resolver = new VersionConflictResolver(_mockLogger.Object, _configuration);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
        {
            // Arrange
            var logger = new Mock<ILogger<VersionConflictResolver>>();
            var config = new ConflictResolutionConfiguration();

            // Act
            var resolver = new VersionConflictResolver(logger.Object, config);

            // Assert
            Assert.NotNull(resolver);
            Assert.NotEmpty(resolver.GetResolutionRules());
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var config = new ConflictResolutionConfiguration();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new VersionConflictResolver(null, config));
            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            var logger = new Mock<ILogger<VersionConflictResolver>>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new VersionConflictResolver(logger.Object, null));
            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithNullVersions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _resolver.DetectVersionConflictsAsync(null));
            Assert.Equal("versions", exception.ParamName);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithNoVersions_ShouldReturnEmptyList()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>();

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.Empty(conflicts);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithInstallationPathConflicts_ShouldDetectCriticalConflict()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    InstallationPath = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    InstallationPath = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS", // Same path
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.NotEmpty(conflicts);
            Assert.Contains(conflicts, c => c.ConflictType == VersionConflictType.InstallationPathConflict);
            Assert.Equal(ConflictSeverity.Critical, conflicts.First().Severity);
            Assert.Contains("sharing installation path", conflicts.First().Description);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithRegistryConflicts_ShouldDetectWarningConflict()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    RegistryKeyPath = @"SOFTWARE\SOLIDWORKS\Applications\SOLIDWORKS",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    RegistryKeyPath = @"SOFTWARE\SOLIDWORKS\Applications\SOLIDWORKS", // Same registry key
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.NotEmpty(conflicts);
            Assert.Contains(conflicts, c => c.ConflictType == VersionConflictType.RegistryConflict);
            Assert.Equal(ConflictSeverity.Warning, conflicts.First(c => c.ConflictType == VersionConflictType.RegistryConflict).Severity);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithLargeVersionSpan_ShouldDetectCompatibilityConflict()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2015", // 9 year span
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.NotEmpty(conflicts);
            var compatibilityConflict = conflicts.FirstOrDefault(c => c.ConflictType == VersionConflictType.VersionCompatibilityConflict);
            Assert.NotNull(compatibilityConflict);
            Assert.Equal(ConflictSeverity.Warning, compatibilityConflict.Severity);
            Assert.Contains("exceeds recommended maximum", compatibilityConflict.Description);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithMixedArchitecture_ShouldDetectResourceConflict()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    Is64Bit = true,
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2020",
                    Is64Bit = false,
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.NotEmpty(conflicts);
            var resourceConflict = conflicts.FirstOrDefault(c => c.ConflictType == VersionConflictType.ResourceConflict);
            Assert.NotNull(resourceConflict);
            Assert.Equal(ConflictSeverity.Information, resourceConflict.Severity);
            Assert.Contains("Mixed architecture", resourceConflict.Description);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithSharedLicenseManager_ShouldDetectLicenseServerConflict()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    LmutilPath = @"C:\SOLIDWORKS\lmutil.exe",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    LmutilPath = @"C:\SOLIDWORKS\lmutil.exe", // Same path
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.NotEmpty(conflicts);
            var licenseConflict = conflicts.FirstOrDefault(c => c.ConflictType == VersionConflictType.LicenseServerConflict);
            Assert.NotNull(licenseConflict);
            Assert.Equal(ConflictSeverity.Warning, licenseConflict.Severity);
            Assert.Contains("sharing license manager", licenseConflict.Description);
        }

        [Fact]
        public async Task DetectVersionConflictsAsync_WithMultipleConflictTypes_ShouldSortBySeverity()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    InstallationPath = @"C:\SOLIDWORKS\SOLIDWORKS",
                    RegistryKeyPath = @"SOFTWARE\SOLIDWORKS\SOLIDWORKS",
                    LmutilPath = @"C:\SOLIDWORKS\lmutil.exe",
                    Is64Bit = true,
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2015",
                    InstallationPath = @"C:\SOLIDWORKS\SOLIDWORKS", // Path conflict
                    RegistryKeyPath = @"SOFTWARE\SOLIDWORKS\SOLIDWORKS", // Registry conflict
                    LmutilPath = @"C:\SOLIDWORKS\lmutil.exe", // License server conflict
                    Is64Bit = false, // Architecture conflict
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var conflicts = await _resolver.DetectVersionConflictsAsync(versions);

            // Assert
            Assert.NotEmpty(conflicts);
            Assert.True(conflicts.Count > 1);

            // First conflict should be the most severe (Critical)
            Assert.Equal(ConflictSeverity.Critical, conflicts[0].Severity);
        }

        [Fact]
        public async Task ResolveConflictsAsync_WithNullConflicts_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _resolver.ResolveConflictsAsync(null));
            Assert.Equal("conflicts", exception.ParamName);
        }

        [Fact]
        public async Task ResolveConflictsAsync_WithEmptyConflicts_ShouldReturnEmptyResults()
        {
            // Arrange
            var conflicts = new List<VersionConflict>();

            // Act
            var results = await _resolver.ResolveConflictsAsync(conflicts);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task ResolveConflictsAsync_WithValidConflicts_ShouldReturnResults()
        {
            // Arrange
            var conflicts = new List<VersionConflict>
            {
                new VersionConflict
                {
                    ConflictId = Guid.NewGuid().ToString(),
                    ConflictType = VersionConflictType.RegistryConflict,
                    Severity = ConflictSeverity.Warning,
                    Description = "Registry conflict",
                    AffectedVersions = new List<string> { "2024", "2023" },
                    DetectedAt = DateTime.UtcNow
                }
            };

            // Act
            var results = await _resolver.ResolveConflictsAsync(conflicts);

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(1, results.Count);
            Assert.Equal(conflicts[0].ConflictId, results[0].ConflictId);
        }

        [Fact]
        public async Task ResolveConflictAsync_WithNullConflict_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _resolver.ResolveConflictAsync(null));
            Assert.Equal("conflict", exception.ParamName);
        }

        [Fact]
        public async Task ResolveConflictAsync_WithNoApplicableRules_ShouldFail()
        {
            // Arrange
            var conflict = new VersionConflict
            {
                ConflictId = Guid.NewGuid().ToString(),
                ConflictType = VersionConflictType.InstallationPathConflict,
                Severity = ConflictSeverity.Critical,
                Description = "Path conflict"
            };

            // Act
            var result = await _resolver.ResolveConflictAsync(conflict);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No applicable resolution rules", result.ErrorMessage);
        }

        [Fact]
        public async Task ResolveConflictAsync_WithRegistryConflict_ShouldAutoResolve()
        {
            // Arrange
            var conflict = new VersionConflict
            {
                ConflictId = Guid.NewGuid().ToString(),
                ConflictType = VersionConflictType.RegistryConflict,
                Severity = ConflictSeverity.Warning,
                Description = "Registry conflict",
                AffectedVersions = new List<string> { "2024", "2023" },
                DetectedAt = DateTime.UtcNow
            };

            // Act
            var result = await _resolver.ResolveConflictAsync(conflict);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(ConflictResolutionStrategy.AutoResolve.ToString(), result.ResolutionStrategy);
            Assert.NotNull(result.ResolutionDescription);
        }

        [Fact]
        public async Task ResolveConflictAsync_WithLicenseServerConflict_ShouldAutoResolve()
        {
            // Arrange
            var conflict = new VersionConflict
            {
                ConflictId = Guid.NewGuid().ToString(),
                ConflictType = VersionConflictType.LicenseServerConflict,
                Severity = ConflictSeverity.Warning,
                Description = "License server conflict",
                AffectedVersions = new List<string> { "2024", "2023" },
                DetectedAt = DateTime.UtcNow
            };

            // Act
            var result = await _resolver.ResolveConflictAsync(conflict);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(ConflictResolutionStrategy.AutoResolve.ToString(), result.ResolutionStrategy);
            Assert.Contains("configuration adjustment", result.ResolutionDescription);
        }

        [Fact]
        public async Task ResolveConflictAsync_WithInstallationPathConflict_ShouldRequireManualIntervention()
        {
            // Arrange
            var conflict = new VersionConflict
            {
                ConflictId = Guid.NewGuid().ToString(),
                ConflictType = VersionConflictType.InstallationPathConflict,
                Severity = ConflictSeverity.Critical,
                Description = "Path conflict",
                AffectedVersions = new List<string> { "2024", "2023" },
                DetectedAt = DateTime.UtcNow
            };

            // Act
            var result = await _resolver.ResolveConflictAsync(conflict);

            // Assert
            Assert.False(result.Success);
            Assert.True(result.RequiresManualIntervention);
            Assert.Contains("manual intervention", result.ErrorMessage);
        }

        [Fact]
        public void AddResolutionRule_WithValidRule_ShouldSucceed()
        {
            // Arrange
            var rule = new ConflictResolutionRule
            {
                RuleId = "test-rule",
                ConflictType = VersionConflictType.ResourceConflict,
                Strategy = ConflictResolutionStrategy.WarnOnly,
                Priority = 1,
                Description = "Test rule",
                IsEnabled = true
            };

            // Act
            _resolver.AddResolutionRule(rule);

            // Assert
            var rules = _resolver.GetResolutionRules();
            Assert.Contains(rules, r => r.RuleId == "test-rule");
        }

        [Fact]
        public void AddResolutionRule_WithNullRule_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                _resolver.AddResolutionRule(null));
            Assert.Equal("rule", exception.ParamName);
        }

        [Fact]
        public void RemoveResolutionRule_WithValidId_ShouldSucceed()
        {
            // Arrange
            var rule = new ConflictResolutionRule
            {
                RuleId = "test-rule",
                ConflictType = VersionConflictType.ResourceConflict,
                Strategy = ConflictResolutionStrategy.WarnOnly,
                Priority = 1,
                Description = "Test rule",
                IsEnabled = true
            };
            _resolver.AddResolutionRule(rule);

            // Act
            var result = _resolver.RemoveResolutionRule("test-rule");

            // Assert
            Assert.True(result);
            var rules = _resolver.GetResolutionRules();
            Assert.DoesNotContain(rules, r => r.RuleId == "test-rule");
        }

        [Fact]
        public void RemoveResolutionRule_WithInvalidId_ShouldReturnFalse()
        {
            // Act
            var result = _resolver.RemoveResolutionRule("invalid-id");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveResolutionRule_WithEmptyId_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _resolver.RemoveResolutionRule(""));
            Assert.Equal("Rule ID cannot be null or empty", exception.ParamName);
        }

        [Fact]
        public void GetResolutionRules_ShouldReturnAllRules()
        {
            // Act
            var rules = _resolver.GetResolutionRules();

            // Assert
            Assert.NotEmpty(rules);
            Assert.True(rules.Count >= 4); // Should have default rules
            Assert.Contains(rules, r => r.ConflictType == VersionConflictType.RegistryConflict);
            Assert.Contains(rules, r => r.ConflictType == VersionConflictType.InstallationPathConflict);
            Assert.Contains(rules, r => r.ConflictType == VersionConflictType.LicenseServerConflict);
            Assert.Contains(rules, r => r.ConflictType == VersionConflictType.VersionCompatibilityConflict);
        }

        [Fact]
        public async Task ValidateVersionCompatibilityAsync_WithNullVersions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _resolver.ValidateVersionCompatibilityAsync(null));
            Assert.Equal("versions", exception.ParamName);
        }

        [Fact]
        public async Task ValidateVersionCompatibilityAsync_WithCompatibleVersions_ShouldReturnValid()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true,
                    Health = VersionHealth.Healthy
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    IsAvailable = true,
                    IsSupported = true,
                    Health = VersionHealth.Healthy
                }
            };

            // Act
            var result = await _resolver.ValidateVersionCompatibilityAsync(versions);

            // Assert
            Assert.True(result.IsValid);
            Assert.True(result.CompatibilityScore > 80);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateVersionCompatibilityAsync_WithUnsupportedVersions_ShouldIncludeWarnings()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = false, // Unsupported
                    Health = VersionHealth.Healthy
                },
                new SolidWorksVersionInfo
                {
                    Version = "2020",
                    IsAvailable = true,
                    IsSupported = true,
                    Health = VersionHealth.Damaged // Unhealthy
                }
            };

            // Act
            var result = await _resolver.ValidateVersionCompatibilityAsync(versions);

            // Assert
            Assert.NotEmpty(result.Warnings);
            Assert.Contains("not supported", result.Warnings[0]);
            Assert.Contains("health issues", result.Warnings[1]);
            Assert.True(result.CompatibilityScore < 90);
        }

        [Fact]
        public async Task ValidateVersionCompatibilityAsync_WithCriticalConflicts_ShouldBeInvalid()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    InstallationPath = @"C:\SOLIDWORKS\SOLIDWORKS",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    InstallationPath = @"C:\SOLIDWORKS\SOLIDWORKS", // Path conflict
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            // Act
            var result = await _resolver.ValidateVersionCompatibilityAsync(versions);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Conflicts);
            Assert.Contains(result.Conflicts, c => c.severity == ConflictSeverity.Critical);
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_WithNullAvailableVersions_ShouldThrowArgumentNullException()
        {
            // Arrange
            var constraints = new VersionConfigurationConstraints();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _resolver.SuggestOptimalConfigurationAsync(null, constraints));
            Assert.Equal("availableVersions", exception.ParamName);
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_WithNullConstraints_ShouldThrowArgumentNullException()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _resolver.SuggestOptimalConfigurationAsync(versions, null));
            Assert.Equal("constraints", exception.ParamName);
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_WithValidVersions_ShouldReturnSuggestion()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2022",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                }
            };

            var constraints = new VersionConfigurationConstraints
            {
                MaximumVersions = 3
            };

            // Act
            var suggestion = await _resolver.SuggestOptimalConfigurationAsync(versions, constraints);

            // Assert
            Assert.NotNull(suggestion);
            Assert.NotNull(suggestion.BestConfiguration);
            Assert.NotEmpty(suggestion.BestConfiguration.SelectedVersions);
            Assert.True(suggestion.BestConfiguration.Score > 0);
            Assert.True(suggestion.TotalConfigurationsEvaluated > 0);
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_WithVersionConstraints_ShouldFilterVersions()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    IsAvailable = true,
                    IsSupported = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2020", // Below minimum
                    IsAvailable = true,
                    IsSupported = true
                }
            };

            var constraints = new VersionConfigurationConstraints
            {
                MinimumVersion = "2022",
                MaximumVersion = "2024"
            };

            // Act
            var suggestion = await _resolver.SuggestOptimalConfigurationAsync(versions, constraints);

            // Assert
            Assert.NotNull(suggestion);
            Assert.NotNull(suggestion.BestConfiguration);

            // Should only include 2023 and 2024
            Assert.DoesNotContain(suggestion.BestConfiguration.SelectedVersions, v => v.Version == "2020");
            Assert.True(suggestion.BestConfiguration.SelectedVersions.Count <= 2);
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_With64BitRequirement_ShouldPrefer64Bit()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = false
                }
            };

            var constraints = new VersionConfigurationConstraints
            {
                Require64Bit = true
            };

            // Act
            var suggestion = await _resolver.SuggestOptimalConfigurationAsync(versions, constraints);

            // Assert
            Assert.NotNull(suggestion);
            Assert.NotNull(suggestion.BestConfiguration);

            // Should only include 64-bit versions
            Assert.All(suggestion.BestConfiguration.SelectedVersions, v => Assert.True(v.Is64Bit));
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_WithNoValidVersions_ShouldReturnNullBestConfiguration()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2020",
                    IsAvailable = false,
                    IsSupported = false
                }
            };

            var constraints = new VersionConfigurationConstraints();

            // Act
            var suggestion = await _resolver.SuggestOptimalConfigurationAsync(versions, constraints);

            // Assert
            Assert.NotNull(suggestion);
            Assert.Null(suggestion.BestConfiguration);
            Assert.Empty(suggestion.AlternativeConfigurations);
        }

        [Fact]
        public async Task SuggestOptimalConfigurationAsync_ShouldGenerateMultipleConfigurationStrategies()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                }
            };

            var constraints = new VersionConfigurationConstraints();

            // Act
            var suggestion = await _resolver.SuggestOptimalConfigurationAsync(versions, constraints);

            // Assert
            Assert.NotNull(suggestion);
            Assert.True(suggestion.TotalConfigurationsEvaluated >= 4); // Should have multiple strategies
            Assert.True(suggestion.AlternativeConfigurations.Count >= 1); // Should have alternatives
        }

        [Fact]
        public void InitializeDefaultRules_ShouldCreateStandardRules()
        {
            // Arrange & Act - constructor already runs in setup
            var rules = _resolver.GetResolutionRules();

            // Assert
            Assert.NotEmpty(rules);

            var registryRule = rules.FirstOrDefault(r => r.ConflictType == VersionConflictType.RegistryConflict);
            Assert.NotNull(registryRule);
            Assert.Equal(ConflictResolutionStrategy.AutoResolve, registryRule.Strategy);
            Assert.Equal(1, registryRule.Priority);

            var pathRule = rules.FirstOrDefault(r => r.ConflictType == VersionConflictType.InstallationPathConflict);
            Assert.NotNull(pathRule);
            Assert.Equal(ConflictResolutionStrategy.ManualIntervention, pathRule.Strategy);

            var licenseRule = rules.FirstOrDefault(r => r.ConflictType == VersionConflictType.LicenseServerConflict);
            Assert.NotNull(licenseRule);
            Assert.Equal(ConflictResolutionStrategy.AutoResolve, licenseRule.Strategy);

            var compatibilityRule = rules.FirstOrDefault(r => r.ConflictType == VersionConflictType.VersionCompatibilityConflict);
            Assert.NotNull(compatibilityRule);
            Assert.Equal(ConflictResolutionStrategy.WarnOnly, compatibilityRule.Strategy);
        }

        [Fact]
        public async Task CalculateCompatibilityScore_ShouldHandleVariousScenarios()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true,
                    Health = VersionHealth.Healthy
                }
            };

            var result = new VersionCompatibilityValidationResult();

            // Act & Assert - Start with perfect score
            var score = await _resolver.ValidateVersionCompatibilityAsync(versions);
            Assert.Equal(100.0, score.CompatibilityScore);

            // Add errors and verify score decreases
            result.Errors.Add("Test error");
            var scoreWithErrors = await Task.Run(() => CalculateCompatibilityScoreTestHelper(result));
            Assert.True(scoreWithErrors < 100.0);
        }

        // Helper method to test CalculateCompatibilityScore logic
        private double CalculateCompatibilityScoreTestHelper(VersionCompatibilityValidationResult result)
        {
            double score = 100.0;
            score -= result.Errors.Count * 20;
            score -= result.Conflicts.Count(c => c.severity == ConflictSeverity.Critical) * 15;
            score -= result.Warnings.Count * 5;
            score -= result.Conflicts.Count(c => c.severity != ConflictSeverity.Critical) * 2;
            return Math.Max(0, score);
        }

        [Fact]
        public async Task ConfigurationStrategies_ShouldGenerateDifferentSelections()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo
                {
                    Version = "2024",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                },
                new SolidWorksVersionInfo
                {
                    Version = "2023",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = false
                },
                new SolidWorksVersionInfo
                {
                    Version = "2022",
                    IsAvailable = true,
                    IsSupported = true,
                    Is64Bit = true
                }
            };

            var constraints = new VersionConfigurationConstraints();

            // Act
            var suggestion = await _resolver.SuggestOptimalConfigurationAsync(versions, constraints);

            // Assert
            Assert.NotNull(suggestion);
            Assert.NotEmpty(suggestion.AlternativeConfigurations);

            // Should have different strategies represented
            var strategies = suggestion.AlternativeConfigurations.Select(c => c.Strategy).Distinct().ToList();
            Assert.True(strategies.Count > 1);
        }
    }

    /// <summary>
    /// Tests for conflict resolution configuration
    /// </summary>
    public class ConflictResolutionConfigurationTests
    {
        [Fact]
        public void ConflictResolutionConfiguration_DefaultValues_ShouldBeReasonable()
        {
            // Arrange & Act
            var config = new ConflictResolutionConfiguration();

            // Assert
            Assert.Equal(5, config.MaximumVersionSpan);
            Assert.Equal(1024 * 1024 * 1024, config.MaximumMemoryUsage);
            Assert.Equal(80, config.MaximumCpuUsage);
            Assert.Equal(2, config.MaximumLicenseServers);
            Assert.Equal(ConflictResolutionStrategy.WarnOnly, config.DefaultStrategy);
            Assert.True(config.EnableAutoResolution);
            Assert.Equal(TimeSpan.FromSeconds(30), config.ResolutionTimeout);
        }

        [Fact]
        public void ConflictResolutionConfiguration_CanModifyValues()
        {
            // Arrange
            var config = new ConflictResolutionConfiguration();

            // Act
            config.MaximumVersionSpan = 10;
            config.MaximumMemoryUsage = 2L * 1024 * 1024 * 1024;
            config.MaximumCpuUsage = 90;
            config.MaximumLicenseServers = 3;
            config.DefaultStrategy = ConflictResolutionStrategy.AutoResolve;
            config.EnableAutoResolution = false;
            config.ResolutionTimeout = TimeSpan.FromMinutes(1);

            // Assert
            Assert.Equal(10, config.MaximumVersionSpan);
            Assert.Equal(2L * 1024 * 1024 * 1024, config.MaximumMemoryUsage);
            Assert.Equal(90, config.MaximumCpuUsage);
            Assert.Equal(3, config.MaximumLicenseServers);
            Assert.Equal(ConflictResolutionStrategy.AutoResolve, config.DefaultStrategy);
            Assert.False(config.EnableAutoResolution);
            Assert.Equal(TimeSpan.FromMinutes(1), config.ResolutionTimeout);
        }
    }

    /// <summary>
    /// Tests for conflict resolution result classes
    /// </summary>
    public class ConflictResolutionResultTests
    {
        [Fact]
        public void VersionConflict_DefaultValues_ShouldBeInitialized()
        {
            // Arrange & Act
            var conflict = new VersionConflict();

            // Assert
            Assert.NotNull(conflict.AffectedVersions);
            Assert.NotNull(conflict.Metrics);
            Assert.NotNull(conflict.ResolutionStrategies);
            Assert.Empty(conflict.AffectedVersions);
            Assert.Empty(conflict.Metrics);
            Assert.Empty(conflict.ResolutionStrategies);
        }

        [Fact]
        public void ConflictResolutionResult_DefaultValues_ShouldBeInitialized()
        {
            // Arrange & Act
            var result = new ConflictResolutionResult();

            // Assert
            Assert.NotNull(result.ResolutionActions);
            Assert.Empty(result.ResolutionActions);
            Assert.False(result.RequiresManualIntervention);
        }

        [Fact]
        public void VersionCompatibilityValidationResult_DefaultValues_ShouldBeInitialized()
        {
            // Arrange & Act
            var result = new VersionCompatibilityValidationResult();

            // Assert
            Assert.NotNull(result.Errors);
            Assert.NotNull(result.Warnings);
            Assert.NotNull(result.Conflicts);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Empty(result.Conflicts);
            Assert.Equal(0, result.CompatibilityScore);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void VersionConfigurationConstraints_DefaultValues_ShouldBeInitialized()
        {
            // Arrange & Act
            var constraints = new VersionConfigurationConstraints();

            // Assert
            Assert.Equal(5, constraints.MaximumVersions);
            Assert.False(constraints.Require64Bit);
            Assert.True(constraints.RequireHealthy);
        }

        [Fact]
        public void VersionConfigurationSuggestion_DefaultValues_ShouldBeInitialized()
        {
            // Arrange & Act
            var suggestion = new VersionConfigurationSuggestion();

            // Assert
            Assert.NotNull(suggestion.AlternativeConfigurations);
            Assert.Empty(suggestion.AlternativeConfigurations);
            Assert.Equal(0, suggestion.TotalConfigurationsEvaluated);
        }

        [Fact]
        public void VersionConfigurationOption_DefaultValues_ShouldBeInitialized()
        {
            // Arrange & Act
            var option = new VersionConfigurationOption();

            // Assert
            Assert.NotNull(option.SelectedVersions);
            Assert.Empty(option.SelectedVersions);
            Assert.Equal(0, option.CompatibilityScore);
            Assert.False(option.HasConflicts);
            Assert.Equal(0, option.Score);
        }
    }
}