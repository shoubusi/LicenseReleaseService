using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LicenseReleaseService.VersionManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LicenseReleaseService.Tests.VersionManagement
{
    [TestClass]
    public class SolidWorksVersionInfoTests
    {
        private TestContext _testContext;

        public TestContext TestContext
        {
            get { return _testContext; }
            set { _testContext = value; }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Reset test environment
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Clean up test environment
        }

        [TestMethod]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            string version = "2025";
            string installationPath = @"C:\Program Files\SolidWorks Corp\SolidWorks 2025";

            // Act
            var versionInfo = new SolidWorksVersionInfo(version, installationPath);

            // Assert
            Assert.IsNotNull(versionInfo);
            Assert.AreEqual(version, versionInfo.Version);
            Assert.AreEqual(installationPath, versionInfo.InstallationPath);
            Assert.AreEqual(InstallationStatus.Installed, versionInfo.Status);
            Assert.AreEqual(VersionHealth.Unknown, versionInfo.Health);
            Assert.IsNotNull(versionInfo.Metadata);
            Assert.IsTrue(versionInfo.Metadata.Count == 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullVersion_ShouldThrowArgumentNullException()
        {
            // Arrange
            string installationPath = @"C:\Program Files\SolidWorks Corp\SolidWorks 2025";

            // Act
            var versionInfo = new SolidWorksVersionInfo(null, installationPath);

            // Assert - Exception expected
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullInstallationPath_ShouldThrowArgumentNullException()
        {
            // Arrange
            string version = "2025";

            // Act
            var versionInfo = new SolidWorksVersionInfo(version, null);

            // Assert - Exception expected
        }

        [TestMethod]
        public void Constructor_Default_ShouldInitializeWithDefaultValues()
        {
            // Act
            var versionInfo = new SolidWorksVersionInfo();

            // Assert
            Assert.IsNotNull(versionInfo);
            Assert.AreEqual(InstallationStatus.Unknown, versionInfo.Status);
            Assert.AreEqual(VersionHealth.Unknown, versionInfo.Health);
            Assert.IsNotNull(versionInfo.Metadata);
            Assert.IsTrue(versionInfo.Metadata.Count == 0);
        }

        [TestMethod]
        public void IsAvailable_WithInstalledAndHealthyVersion_ShouldReturnTrue()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Healthy
            };

            // Act & Assert
            Assert.IsTrue(versionInfo.IsAvailable);
        }

        [TestMethod]
        public void IsAvailable_WithInstalledButUnavailableVersion_ShouldReturnFalse()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Unavailable
            };

            // Act & Assert
            Assert.IsFalse(versionInfo.IsAvailable);
        }

        [TestMethod]
        public void IsAvailable_WithNotInstalledVersion_ShouldReturnFalse()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.NotInstalled,
                Health = VersionHealth.Healthy
            };

            // Act & Assert
            Assert.IsFalse(versionInfo.IsAvailable);
        }

        [TestMethod]
        public void IsAvailable_WithCorruptedVersion_ShouldReturnFalse()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Corrupted
            };

            // Act & Assert
            Assert.IsFalse(versionInfo.IsAvailable);
        }

        [TestMethod]
        public void IsSupported_WithSupportedVersion_ShouldReturnTrue()
        {
            // Arrange & Act
            var versionInfo2020 = new SolidWorksVersionInfo("2020", @"C:\Test\SolidWorks 2020");
            var versionInfo2025 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Assert
            Assert.IsTrue(versionInfo2020.IsSupported);
            Assert.IsTrue(versionInfo2025.IsSupported);
        }

        [TestMethod]
        public void IsSupported_WithUnsupportedVersion_ShouldReturnFalse()
        {
            // Arrange & Act
            var versionInfo2019 = new SolidWorksVersionInfo("2019", @"C:\Test\SolidWorks 2019");
            var versionInfo2026 = new SolidWorksVersionInfo("2026", @"C:\Test\SolidWorks 2026");

            // Assert
            Assert.IsFalse(versionInfo2019.IsSupported);
            Assert.IsFalse(versionInfo2026.IsSupported);
        }

        [TestMethod]
        public void IsSupported_WithInvalidVersionFormat_ShouldReturnFalse()
        {
            // Arrange & Act
            var versionInfoInvalid = new SolidWorksVersionInfo("invalid", @"C:\Test\SolidWorks");
            var versionInfoNull = new SolidWorksVersionInfo(null, @"C:\Test\SolidWorks");
            var versionInfoEmpty = new SolidWorksVersionInfo("", @"C:\Test\SolidWorks");

            // Assert
            Assert.IsFalse(versionInfoInvalid.IsSupported);
            Assert.IsFalse(versionInfoNull.IsSupported);
            Assert.IsFalse(versionInfoEmpty.IsSupported);
        }

        [TestMethod]
        public void UpdateHealth_WithNewHealthStatus_ShouldUpdateHealthAndTimestamp()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            var originalTimestamp = versionInfo.LastDetected;

            // Add a small delay to ensure timestamp difference
            System.Threading.Thread.Sleep(10);

            // Act
            versionInfo.UpdateHealth(VersionHealth.Degraded);

            // Assert
            Assert.AreEqual(VersionHealth.Degraded, versionInfo.Health);
            Assert.AreNotEqual(originalTimestamp, versionInfo.LastDetected);
            Assert.IsTrue(versionInfo.LastDetected > originalTimestamp);
        }

        [TestMethod]
        public void Validate_WithValidVersionInfo_ShouldReturnValidResult()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                ExecutablePath = @"C:\Test\SolidWorks 2025\SLDWORKS.exe",
                LmutilPath = @"C:\Test\SolidWorks 2025\lmutil.exe"
            };

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public void Validate_WithMissingVersion_ShouldReturnError()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("", @"C:\Test\SolidWorks 2025");

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.IsTrue(result.Errors[0].Contains("Version cannot be null or empty"));
        }

        [TestMethod]
        public void Validate_WithMissingInstallationPath_ShouldReturnError()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", "");

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.IsTrue(result.Errors[0].Contains("Installation path cannot be null or empty"));
        }

        [TestMethod]
        public void Validate_WithUnsupportedVersion_ShouldReturnWarning()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2019", @"C:\Test\SolidWorks 2019");

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsTrue(result.Warnings[0].Contains("not in the supported range"));
        }

        [TestMethod]
        public void Validate_WithNonexistentInstallationPath_ShouldReturnWarning()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Nonexistent\SolidWorks 2025");

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsTrue(result.Warnings[0].Contains("Installation path does not exist"));
        }

        [TestMethod]
        public void Validate_WithMissingExecutablePath_ShouldReturnWarning()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                ExecutablePath = ""
            };

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsTrue(result.Warnings[0].Contains("Executable path is not specified"));
        }

        [TestMethod]
        public void ToString_ShouldReturnSummaryString()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Healthy
            };

            // Act
            var result = versionInfo.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("SolidWorks 2025"));
            Assert.IsTrue(result.Contains("Installed"));
            Assert.IsTrue(result.Contains("Healthy"));
        }

        [TestMethod]
        public void ToDetailedString_ShouldReturnDetailedInformation()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                FullVersion = "SolidWorks 2025 SP1",
                ExecutablePath = @"C:\Test\SolidWorks 2025\SLDWORKS.exe",
                LmutilPath = @"C:\Test\SolidWorks 2025\lmutil.exe",
                ServicePack = "SP1",
                BuildNumber = "2025.1.0",
                Is64Bit = true,
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Healthy,
                InstallationDate = new DateTime(2025, 1, 15)
            };

            // Act
            var result = versionInfo.ToDetailedString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("SolidWorks 2025"));
            Assert.IsTrue(result.Contains("Full Version: SolidWorks 2025 SP1"));
            Assert.IsTrue(result.Contains("Path: C:\\Test\\SolidWorks 2025"));
            Assert.IsTrue(result.Contains("Executable: C:\\Test\\SolidWorks 2025\\SLDWORKS.exe"));
            Assert.IsTrue(result.Contains("License Manager: C:\\Test\\SolidWorks 2025\\lmutil.exe"));
            Assert.IsTrue(result.Contains("Status: Installed"));
            Assert.IsTrue(result.Contains("Health: Healthy"));
            Assert.IsTrue(result.Contains("Installed: 2025-01-15"));
            Assert.IsTrue(result.Contains("Service Pack: SP1"));
            Assert.IsTrue(result.Contains("Build: 2025.1.0"));
            Assert.IsTrue(result.Contains("Architecture: 64-bit"));
            Assert.IsTrue(result.Contains("Available: True"));
            Assert.IsTrue(result.Contains("Supported: True"));
        }

        [TestMethod]
        public void ToDetailedString_WithMinimalInformation_ShouldReturnBasicDetails()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Act
            var result = versionInfo.ToDetailedString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("SolidWorks 2025"));
            Assert.IsTrue(result.Contains("Path: C:\\Test\\SolidWorks 2025"));
            Assert.IsTrue(result.Contains("Status: Unknown"));
            Assert.IsTrue(result.Contains("Health: Unknown"));
            Assert.IsTrue(result.Contains("Installed: Unknown"));
            Assert.IsTrue(result.Contains("Service Pack: None"));
            Assert.IsTrue(result.Contains("Build: Unknown"));
        }

        [TestMethod]
        public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
        {
            // Arrange
            var versionInfo1 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            var versionInfo2 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Act
            var hash1 = versionInfo1.GetHashCode();
            var hash2 = versionInfo2.GetHashCode();

            // Assert
            Assert.AreEqual(hash1, hash2);
        }

        [TestMethod]
        public void GetHashCode_WithDifferentValues_ShouldReturnDifferentHashCode()
        {
            // Arrange
            var versionInfo1 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            var versionInfo2 = new SolidWorksVersionInfo("2024", @"C:\Test\SolidWorks 2024");

            // Act
            var hash1 = versionInfo1.GetHashCode();
            var hash2 = versionInfo2.GetHashCode();

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void Equals_WithSameVersionInfo_ShouldReturnTrue()
        {
            // Arrange
            var versionInfo1 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            var versionInfo2 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Act & Assert
            Assert.IsTrue(versionInfo1.Equals(versionInfo2));
        }

        [TestMethod]
        public void Equals_WithDifferentVersionInfo_ShouldReturnFalse()
        {
            // Arrange
            var versionInfo1 = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            var versionInfo2 = new SolidWorksVersionInfo("2024", @"C:\Test\SolidWorks 2024");

            // Act & Assert
            Assert.IsFalse(versionInfo1.Equals(versionInfo2));
        }

        [TestMethod]
        public void Equals_WithNullObject_ShouldReturnFalse()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Act & Assert
            Assert.IsFalse(versionInfo.Equals(null));
        }

        [TestMethod]
        public void Equals_WithDifferentObjectType_ShouldReturnFalse()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");
            var otherObject = new object();

            // Act & Assert
            Assert.IsFalse(versionInfo.Equals(otherObject));
        }

        [TestMethod]
        public void PropertySetters_ShouldUpdateValuesCorrectly()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Act
            versionInfo.FullVersion = "SolidWorks 2025 SP2";
            versionInfo.ExecutablePath = @"C:\Test\SolidWorks 2025\SLDWORKS.exe";
            versionInfo.LmutilPath = @"C:\Test\SolidWorks 2025\lmutil.exe";
            versionInfo.ServicePack = "SP2";
            versionInfo.BuildNumber = "2025.2.0";
            versionInfo.Is64Bit = true;
            versionInfo.Status = InstallationStatus.Installed;
            versionInfo.InstallationDate = new DateTime(2025, 2, 15);

            // Assert
            Assert.AreEqual("SolidWorks 2025 SP2", versionInfo.FullVersion);
            Assert.AreEqual(@"C:\Test\SolidWorks 2025\SLDWORKS.exe", versionInfo.ExecutablePath);
            Assert.AreEqual(@"C:\Test\SolidWorks 2025\lmutil.exe", versionInfo.LmutilPath);
            Assert.AreEqual("SP2", versionInfo.ServicePack);
            Assert.AreEqual("2025.2.0", versionInfo.BuildNumber);
            Assert.IsTrue(versionInfo.Is64Bit);
            Assert.AreEqual(InstallationStatus.Installed, versionInfo.Status);
            Assert.AreEqual(new DateTime(2025, 2, 15), versionInfo.InstallationDate);
        }

        [TestMethod]
        public void Metadata_WithAdditions_ShouldStoreKeyValuePairs()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025");

            // Act
            versionInfo.Metadata["Source"] = "Registry";
            versionInfo.Metadata["Architecture"] = "64-bit";
            versionInfo.Metadata["Detected"] = "2025-03-15";

            // Assert
            Assert.AreEqual(3, versionInfo.Metadata.Count);
            Assert.AreEqual("Registry", versionInfo.Metadata["Source"]);
            Assert.AreEqual("64-bit", versionInfo.Metadata["Architecture"]);
            Assert.AreEqual("2025-03-15", versionInfo.Metadata["Detected"]);
        }

        [TestMethod]
        public void ValidationResults_WithMultipleIssues_ShouldCollectAllIssues()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2019", @"C:\Nonexistent\SolidWorks 2019")
            {
                ExecutablePath = "",
                LmutilPath = ""
            };

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.IsTrue(result.Warnings.Count >= 3); // Unsupported version, missing paths, nonexistent directory
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("not in the supported range")));
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("Installation path does not exist")));
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("Executable path is not specified")));
        }

        [TestMethod]
        public void ValidationResults_WithEdgeCaseVersion_ShouldHandleGracefully()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Version = "9999" // Far future version
            };

            // Act
            var result = versionInfo.Validate();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.IsTrue(result.Warnings.Count > 0);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("outside the recommended range")));
        }

        [TestMethod]
        public void VersionHealth_WithVariousStates_ShouldBeAccessibleCorrectly()
        {
            // Arrange
            var healthyVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Healthy
            };

            var degradedVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Degraded
            };

            var unavailableVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Unavailable
            };

            var corruptedVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Corrupted
            };

            // Act & Assert
            Assert.IsTrue(healthyVersion.IsAvailable);
            Assert.IsTrue(degradedVersion.IsAvailable);
            Assert.IsFalse(unavailableVersion.IsAvailable);
            Assert.IsFalse(corruptedVersion.IsAvailable);
        }

        [TestMethod]
        public void InstallationStatus_WithVariousStates_ShouldAffectAvailability()
        {
            // Arrange
            var installedVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Installed,
                Health = VersionHealth.Healthy
            };

            var notInstalledVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.NotInstalled,
                Health = VersionHealth.Healthy
            };

            var corruptedVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Corrupted,
                Health = VersionHealth.Healthy
            };

            var inaccessibleVersion = new SolidWorksVersionInfo("2025", @"C:\Test\SolidWorks 2025")
            {
                Status = InstallationStatus.Inaccessible,
                Health = VersionHealth.Healthy
            };

            // Act & Assert
            Assert.IsTrue(installedVersion.IsAvailable);
            Assert.IsFalse(notInstalledVersion.IsAvailable);
            Assert.IsFalse(corruptedVersion.IsAvailable);
            Assert.IsFalse(inaccessibleVersion.IsAvailable);
        }
    }
}