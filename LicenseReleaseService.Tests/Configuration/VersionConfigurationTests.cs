using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for VersionConfiguration components
    /// </summary>
    [TestClass]
    public class VersionConfigurationTests
    {
        private TestVersionConfigurationManager _testManager;
        private string _testConfigPath;

        [TestInitialize]
        public void TestInitialize()
        {
            // Create a test configuration file
            _testConfigPath = Path.GetTempFileName();
            CreateTestConfigurationFile();
            _testManager = new TestVersionConfigurationManager(_testConfigPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _testManager?.Dispose();

            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        private void CreateTestConfigurationFile()
        {
            var configContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <sectionGroup name=""licenseReleaseService"">
      <section name=""versionConfiguration"" type=""LicenseReleaseService.Configuration.VersionConfigurationSection, LicenseReleaseService"" />
    </sectionGroup>
  </configSections>
  <licenseReleaseService>
    <versionConfiguration>
      <versions>
        <versionConfiguration version=""2020"" licenseServer=""sw2020-server.company.com"" port=""27000"" timeout=""60"" />
        <versionConfiguration version=""2021"" licenseServer=""sw2021-server.company.com"" port=""27001"" timeout=""90"" parentVersion=""2020"" />
        <versionConfiguration version=""2022"" licenseServer=""sw2022-server.company.com"" port=""27002"" timeout=""45"" />
        <versionConfiguration version=""2023"" licenseServer=""sw2023-server.company.com"" port=""27003"" timeout=""30"" />
        <versionConfiguration version=""2024"" licenseServer=""sw2024-server.company.com"" port=""27004"" timeout=""20"" />
        <versionConfiguration version=""2025"" licenseServer=""sw2025-server.company.com"" port=""27005"" timeout=""15"" />
      </versions>
      <featureMappings>
        <featureMapping version=""2020"" featureCode=""sw_standard"" alias=""Standard"" description=""SolidWorks Standard"" />
        <featureMapping version=""2021"" featureCode=""sw_professional"" alias=""Professional"" description=""SolidWorks Professional"" />
        <featureMapping version=""2022"" featureCode=""sw_premium"" alias=""Premium"" description=""SolidWorks Premium"" />
        <featureMapping version=""2023"" featureCode=""sw_simulation"" alias=""Simulation"" description=""SolidWorks Simulation"" />
        <featureMapping version=""2024"" featureCode=""sw_routing"" alias=""Routing"" description=""SolidWorks Routing"" />
        <featureMapping version=""2025"" featureCode=""sw_all"" alias=""All"" description=""All Features"" />
      </featureMappings>
    </versionConfiguration>
  </licenseReleaseService>
</configuration>";

            File.WriteAllText(_testConfigPath, configContent);
        }

        #region VersionConfigurationElement Tests

        [TestMethod]
        public void VersionConfigurationElement_Constructor_ShouldInitializeDefaultValues()
        {
            // Arrange & Act
            var element = new VersionConfigurationElement();

            // Assert
            Assert.AreEqual("2020", element.Version);
            Assert.AreEqual(27000, element.Port);
            Assert.AreEqual(30, element.Timeout);
            Assert.AreEqual(3, element.RetryCount);
            Assert.AreEqual(false, element.IsInheritanceBlocked);
        }

        [TestMethod]
        public void VersionConfigurationElement_Version_ShouldValidateFormat()
        {
            // Arrange
            var element = new VersionConfigurationElement();

            // Act & Assert
            Assert.ThrowsException<ConfigurationErrorsException>(() => element.Version = "invalid");
            Assert.ThrowsException<ConfigurationErrorsException>(() => element.Version = "202");
            Assert.ThrowsException<ConfigurationErrorsException>(() => element.Version = "20223");

            // Valid values
            element.Version = "2020";
            element.Version = "2025";
        }

        [TestMethod]
        public void VersionConfigurationElement_Validate_ShouldCheckRequiredProperties()
        {
            // Arrange
            var element = new VersionConfigurationElement
            {
                LicenseServer = "",
                Port = 0
            };

            // Act
            var errors = element.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("License server cannot be empty")));
            Assert.IsTrue(errors.Any(e => e.Contains("Port must be between 1 and 65535")));
        }

        [TestMethod]
        public void VersionConfigurationElement_Validate_ShouldCheckTimeoutRanges()
        {
            // Arrange
            var element = new VersionConfigurationElement
            {
                Timeout = 0,
                CommandTimeout = 0
            };

            // Act
            var errors = element.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("Timeout must be between 1 and 600")));
            Assert.IsTrue(errors.Any(e => e.Contains("Command timeout must be between 1 and 600")));
        }

        [TestMethod]
        public void VersionConfigurationElement_GetEffectiveTimeout_ShouldApplyInheritance()
        {
            // Arrange
            var parent = new VersionConfigurationElement
            {
                Version = "2020",
                Timeout = 120,
                CommandTimeout = 180
            };

            var child = new VersionConfigurationElement
            {
                Version = "2021",
                ParentVersion = "2020",
                Timeout = 90,
                CommandTimeout = 0 // Should inherit from parent
            };

            // Act
            var effectiveTimeout = child.GetEffectiveTimeout(parent);
            var effectiveCommandTimeout = child.GetEffectiveCommandTimeout(parent);

            // Assert
            Assert.AreEqual(90, effectiveTimeout);
            Assert.AreEqual(180, effectiveCommandTimeout);
        }

        [TestMethod]
        public void VersionConfigurationElement_IsInheritedFrom_ShouldCheckInheritanceChain()
        {
            // Arrange
            var parent = new VersionConfigurationElement { Version = "2020" };
            var child = new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" };
            var unrelated = new VersionConfigurationElement { Version = "2022", ParentVersion = "2019" };

            // Act & Assert
            Assert.IsTrue(child.IsInheritedFrom(parent));
            Assert.IsFalse(unrelated.IsInheritedFrom(parent));
            Assert.IsFalse(parent.IsInheritedFrom(child));
        }

        [TestMethod]
        public void VersionConfigurationElement_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var element = new VersionConfigurationElement
            {
                Version = "2020",
                LicenseServer = "test-server",
                Port = 27000,
                Timeout = 60
            };

            // Act
            var result = element.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("2020"));
            Assert.IsTrue(result.Contains("test-server"));
            Assert.IsTrue(result.Contains("27000"));
            Assert.IsTrue(result.Contains("60"));
        }

        #endregion

        #region VersionConfigurationCollection Tests

        [TestMethod]
        public void VersionConfigurationCollection_Add_ShouldMaintainOrder()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();

            // Act
            collection.Add(new VersionConfigurationElement { Version = "2020" });
            collection.Add(new VersionConfigurationElement { Version = "2021" });
            collection.Add(new VersionConfigurationElement { Version = "2022" });

            // Assert
            Assert.AreEqual(3, collection.Count);
            Assert.AreEqual("2020", collection[0].Version);
            Assert.AreEqual("2021", collection[1].Version);
            Assert.AreEqual("2022", collection[2].Version);
        }

        [TestMethod]
        public void VersionConfigurationCollection_GetByVersion_ShouldReturnCorrectElement()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2020" });
            collection.Add(new VersionConfigurationElement { Version = "2021" });

            // Act
            var result = collection.GetByVersion("2021");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2021", result.Version);
        }

        [TestMethod]
        public void VersionConfigurationCollection_GetByVersion_ShouldReturnNullForNonExistent()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2020" });

            // Act
            var result = collection.GetByVersion("2025");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void VersionConfigurationCollection_GetAllVersions_ShouldReturnAllVersionNumbers()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2020" });
            collection.Add(new VersionConfigurationElement { Version = "2021" });
            collection.Add(new VersionConfigurationElement { Version = "2022" });

            // Act
            var versions = collection.GetAllVersions();

            // Assert
            Assert.AreEqual(3, versions.Count);
            CollectionAssert.Contains(versions, "2020");
            CollectionAssert.Contains(versions, "2021");
            CollectionAssert.Contains(versions, "2022");
        }

        [TestMethod]
        public void VersionConfigurationCollection_GetSortedVersions_ShouldReturnSortedOrder()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2022" });
            collection.Add(new VersionConfigurationElement { Version = "2020" });
            collection.Add(new VersionConfigurationElement { Version = "2021" });

            // Act
            var sorted = collection.GetSortedVersions();

            // Assert
            Assert.AreEqual(3, sorted.Count);
            Assert.AreEqual("2020", sorted[0]);
            Assert.AreEqual("2021", sorted[1]);
            Assert.AreEqual("2022", sorted[2]);
        }

        [TestMethod]
        public void VersionConfigurationCollection_FindRootVersions_ShouldReturnRootElements()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2020", ParentVersion = "" });
            collection.Add(new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" });
            collection.Add(new VersionConfigurationElement { Version = "2022", ParentVersion = "" });

            // Act
            var roots = collection.FindRootVersions();

            // Assert
            Assert.AreEqual(2, roots.Count);
            Assert.IsTrue(roots.Any(v => v.Version == "2020"));
            Assert.IsTrue(roots.Any(v => v.Version == "2022"));
        }

        [TestMethod]
        public void VersionConfigurationCollection_Validate_ShouldDetectCircularReferences()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2020", ParentVersion = "2021" });
            collection.Add(new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" });

            // Act
            var errors = collection.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("circular reference")));
        }

        [TestMethod]
        public void VersionConfigurationCollection_Validate_ShouldDetectDuplicateVersions()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2020" });
            collection.Add(new VersionConfigurationElement { Version = "2020" });

            // Act
            var errors = collection.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("duplicate")));
        }

        [TestMethod]
        public void VersionConfigurationCollection_Validate_ShouldCheckParentExistence()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            collection.Add(new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" });

            // Act
            var errors = collection.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("parent version")));
        }

        [TestMethod]
        public void VersionConfigurationCollection_BuildInheritanceGraph_ShouldBuildCorrectGraph()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            var root = new VersionConfigurationElement { Version = "2020" };
            var child = new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" };

            collection.Add(root);
            collection.Add(child);

            // Act
            var graph = collection.BuildInheritanceGraph();

            // Assert
            Assert.AreEqual(1, graph.Count);
            Assert.IsTrue(graph.ContainsKey(root));
            Assert.AreEqual(1, graph[root].Count);
            Assert.AreEqual(child, graph[root][0]);
        }

        [TestMethod]
        public void VersionConfigurationCollection_GetInheritanceChain_ShouldReturnChain()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            var root = new VersionConfigurationElement { Version = "2020" };
            var child = new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" };
            var grandchild = new VersionConfigurationElement { Version = "2022", ParentVersion = "2021" };

            collection.Add(root);
            collection.Add(child);
            collection.Add(grandchild);

            // Act
            var chain = collection.GetInheritanceChain("2022");

            // Assert
            Assert.AreEqual(3, chain.Count);
            Assert.AreEqual(root, chain[0]);
            Assert.AreEqual(child, chain[1]);
            Assert.AreEqual(grandchild, chain[2]);
        }

        [TestMethod]
        public void VersionConfigurationCollection_ResolveInheritance_ShouldApplyInheritance()
        {
            // Arrange
            var collection = new VersionConfigurationCollection();
            var root = new VersionConfigurationElement { Version = "2020", LicenseServer = "root-server", Timeout = 120 };
            var child = new VersionConfigurationElement { Version = "2021", ParentVersion = "2020", Timeout = 90 };

            collection.Add(root);
            collection.Add(child);

            // Act
            var resolved = collection.ResolveInheritance();

            // Assert
            Assert.AreEqual(2, resolved.Count);
            Assert.AreEqual("root-server", resolved["2020"].LicenseServer);
            Assert.AreEqual("root-server", resolved["2021"].LicenseServer); // Inherited
            Assert.AreEqual(120, resolved["2020"].Timeout);
            Assert.AreEqual(90, resolved["2021"].Timeout); // Override
        }

        #endregion

        #region VersionConfigurationSection Tests

        [TestMethod]
        public void VersionConfigurationSection_Constructor_ShouldInitializeCollections()
        {
            // Arrange & Act
            var section = new VersionConfigurationSection();

            // Assert
            Assert.IsNotNull(section.Versions);
            Assert.IsNotNull(section.FeatureMappings);
            Assert.AreEqual(0, section.Versions.Count);
            Assert.AreEqual(0, section.FeatureMappings.Count);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetByVersion_ShouldReturnCorrectConfig()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021" });

            // Act
            var result = section.GetByVersion("2021");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2021", result.Version);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetByVersionWithFallback_ShouldUseFallback()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021" });

            // Act
            var result = section.GetByVersion("2025", "2020");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2020", result.Version);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetFallbackVersion_ShouldReturnNearestVersion()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2023" });

            // Act
            var fallback = section.GetFallbackVersion("2025");

            // Assert
            Assert.AreEqual("2023", fallback);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetAllConfiguredVersions_ShouldReturnAllVersions()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2022" });

            // Act
            var versions = section.GetAllConfiguredVersions();

            // Assert
            Assert.AreEqual(3, versions.Count);
            CollectionAssert.Contains(versions, "2020");
            CollectionAssert.Contains(versions, "2021");
            CollectionAssert.Contains(versions, "2022");
        }

        [TestMethod]
        public void VersionConfigurationSection_IsVersionSupported_ShouldCheckSupport()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021" });

            // Act & Assert
            Assert.IsTrue(section.IsVersionSupported("2020"));
            Assert.IsTrue(section.IsVersionSupported("2021"));
            Assert.IsFalse(section.IsVersionSupported("2022"));
        }

        [TestMethod]
        public void VersionConfigurationSection_GetSupportedVersions_ShouldReturnSupportedRange()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2025" });

            // Act
            var supported = section.GetSupportedVersions();

            // Assert
            Assert.AreEqual("2020-2025", supported);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetVersionHierarchy_ShouldReturnHierarchy()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" });

            // Act
            var hierarchy = section.GetVersionHierarchy();

            // Assert
            Assert.AreEqual(1, hierarchy.Count);
            Assert.IsTrue(hierarchy.ContainsKey("2020"));
            Assert.AreEqual(1, hierarchy["2020"].Count);
            Assert.AreEqual("2021", hierarchy["2020"][0]);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetCompatibilityMatrix_ShouldReturnMatrix()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021" });

            // Act
            var matrix = section.GetCompatibilityMatrix();

            // Assert
            Assert.AreEqual(2, matrix.Count);
            Assert.IsTrue(matrix.ContainsKey("2020"));
            Assert.IsTrue(matrix.ContainsKey("2021"));
        }

        [TestMethod]
        public void VersionConfigurationSection_GetInheritanceResolvedConfigurations_ShouldResolveInheritance()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            var root = new VersionConfigurationElement { Version = "2020", LicenseServer = "root-server" };
            var child = new VersionConfigurationElement { Version = "2021", ParentVersion = "2020" };

            section.Versions.Add(root);
            section.Versions.Add(child);

            // Act
            var resolved = section.GetInheritanceResolvedConfigurations();

            // Assert
            Assert.AreEqual(2, resolved.Count);
            Assert.AreEqual("root-server", resolved["2020"].LicenseServer);
            Assert.AreEqual("root-server", resolved["2021"].LicenseServer);
        }

        [TestMethod]
        public void VersionConfigurationSection_Validate_ShouldReturnValidationErrors()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020", LicenseServer = "" });

            // Act
            var errors = section.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("License server cannot be empty")));
        }

        [TestMethod]
        public void VersionConfigurationSection_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });
            section.Versions.Add(new VersionConfigurationElement { Version = "2021" });

            // Act
            var result = section.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("VersionConfiguration:"));
            Assert.IsTrue(result.Contains("Versions:"));
            Assert.IsTrue(result.Contains("2020"));
            Assert.IsTrue(result.Contains("2021"));
        }

        [TestMethod]
        public void VersionConfigurationSection_ClearCache_ShouldClearCache()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });

            // Act - First access to populate cache
            var firstAccess = section.GetByVersion("2020");

            // Clear cache
            section.ClearCache();

            // Assert - Should still work after cache clear
            var secondAccess = section.GetByVersion("2020");
            Assert.IsNotNull(secondAccess);
        }

        [TestMethod]
        public void VersionConfigurationSection_GetCacheStatistics_ShouldReturnStats()
        {
            // Arrange
            var section = new VersionConfigurationSection();
            section.Versions.Add(new VersionConfigurationElement { Version = "2020" });

            // Act - Access to populate cache
            section.GetByVersion("2020");

            var stats = section.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Cache Statistics:"));
            Assert.IsTrue(stats.Contains("Hits:"));
            Assert.IsTrue(stats.Contains("Misses:"));
        }

        #endregion

        #region FeatureMappingElement Tests

        [TestMethod]
        public void FeatureMappingElement_Constructor_ShouldInitializeDefaultValues()
        {
            // Arrange & Act
            var element = new FeatureMappingElement();

            // Assert
            Assert.AreEqual("", element.FeatureCode);
            Assert.AreEqual("", element.Version);
            Assert.AreEqual(true, element.IsEnabled);
        }

        [TestMethod]
        public void FeatureMappingElement_Validate_ShouldCheckRequiredProperties()
        {
            // Arrange
            var element = new FeatureMappingElement
            {
                FeatureCode = "",
                Version = ""
            };

            // Act
            var errors = element.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("Feature code cannot be empty")));
            Assert.IsTrue(errors.Any(e => e.Contains("Version cannot be empty")));
        }

        [TestMethod]
        public void FeatureMappingElement_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var element = new FeatureMappingElement
            {
                Version = "2020",
                FeatureCode = "sw_standard",
                Alias = "Standard"
            };

            // Act
            var result = element.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("2020"));
            Assert.IsTrue(result.Contains("sw_standard"));
            Assert.IsTrue(result.Contains("Standard"));
        }

        #endregion

        #region FeatureMappingCollection Tests

        [TestMethod]
        public void FeatureMappingCollection_GetByVersion_ShouldReturnMappingsForVersion()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_pro" });
            collection.Add(new FeatureMappingElement { Version = "2021", FeatureCode = "sw_standard" });

            // Act
            var mappings = collection.GetByVersion("2020");

            // Assert
            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.Any(m => m.FeatureCode == "sw_standard"));
            Assert.IsTrue(mappings.Any(m => m.FeatureCode == "sw_pro"));
        }

        [TestMethod]
        public void FeatureMappingCollection_GetByVersionWithWildcard_ShouldIncludeWildcards()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "*", FeatureCode = "sw_all" });

            // Act
            var mappings = collection.GetByVersion("2020", includeWildcards: true);

            // Assert
            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.Any(m => m.FeatureCode == "sw_standard"));
            Assert.IsTrue(mappings.Any(m => m.FeatureCode == "sw_all"));
        }

        [TestMethod]
        public void FeatureMappingCollection_GetByFeatureCode_ShouldReturnMappingsForFeature()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "2021", FeatureCode = "sw_standard" });

            // Act
            var mappings = collection.GetByFeatureCode("sw_standard");

            // Assert
            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.Any(m => m.Version == "2020"));
            Assert.IsTrue(mappings.Any(m => m.Version == "2021"));
        }

        [TestMethod]
        public void FeatureMappingCollection_GetByAlias_ShouldReturnMappingsForAlias()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard", Alias = "Standard" });
            collection.Add(new FeatureMappingElement { Version = "2021", FeatureCode = "sw_pro", Alias = "Standard" });

            // Act
            var mappings = collection.GetByAlias("Standard");

            // Assert
            Assert.AreEqual(2, mappings.Count);
        }

        [TestMethod]
        public void FeatureMappingCollection_ResolveFeatureCode_ShouldReturnCorrectCode()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard", Alias = "Standard" });

            // Act
            var code = collection.ResolveFeatureCode("2020", "Standard");

            // Assert
            Assert.AreEqual("sw_standard", code);
        }

        [TestMethod]
        public void FeatureMappingCollection_ResolveFeatureCode_ShouldReturnNullForNonExistent()
        {
            // Arrange
            var collection = new FeatureMappingCollection();

            // Act
            var code = collection.ResolveFeatureCode("2020", "NonExistent");

            // Assert
            Assert.IsNull(code);
        }

        [TestMethod]
        public void FeatureMappingCollection_GetAllFeatureCodes_ShouldReturnAllCodes()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "2021", FeatureCode = "sw_pro" });

            // Act
            var codes = collection.GetAllFeatureCodes();

            // Assert
            Assert.AreEqual(2, codes.Count);
            CollectionAssert.Contains(codes, "sw_standard");
            CollectionAssert.Contains(codes, "sw_pro");
        }

        [TestMethod]
        public void FeatureMappingCollection_GetAllAliases_ShouldReturnAllAliases()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard", Alias = "Standard" });
            collection.Add(new FeatureMappingElement { Version = "2021", FeatureCode = "sw_pro", Alias = "Pro" });

            // Act
            var aliases = collection.GetAllAliases();

            // Assert
            Assert.AreEqual(2, aliases.Count);
            CollectionAssert.Contains(aliases, "Standard");
            CollectionAssert.Contains(aliases, "Pro");
        }

        [TestMethod]
        public void FeatureMappingCollection_Validate_ShouldDetectDuplicateMappings()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });

            // Act
            var errors = collection.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("duplicate")));
        }

        [TestMethod]
        public void FeatureMappingCollection_GetWildcardMappings_ShouldReturnWildcards()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "*", FeatureCode = "sw_all" });

            // Act
            var wildcards = collection.GetWildcardMappings();

            // Assert
            Assert.AreEqual(1, wildcards.Count);
            Assert.AreEqual("*", wildcards[0].Version);
        }

        [TestMethod]
        public void FeatureMappingCollection_ResolveInheritance_ShouldApplyInheritance()
        {
            // Arrange
            var collection = new FeatureMappingCollection();
            collection.Add(new FeatureMappingElement { Version = "2020", FeatureCode = "sw_standard" });
            collection.Add(new FeatureMappingElement { Version = "2021", FeatureCode = "sw_standard", ParentVersion = "2020" });

            // Act
            var resolved = collection.ResolveInheritance();

            // Assert
            Assert.AreEqual(2, resolved.Count);
            Assert.IsTrue(resolved.ContainsKey("2020"));
            Assert.IsTrue(resolved.ContainsKey("2021"));
        }

        #endregion
    }

    /// <summary>
    /// Test version configuration manager that allows custom config file path
    /// </summary>
    public class TestVersionConfigurationManager : VersionConfigurationManager
    {
        private readonly string _configPath;

        public TestVersionConfigurationManager(string configPath)
        {
            _configPath = configPath;
            Initialize();
        }

        protected override string GetConfigFilePath()
        {
            return _configPath;
        }

        public new VersionConfigurationSection CurrentConfiguration => base.CurrentConfiguration;
    }
}