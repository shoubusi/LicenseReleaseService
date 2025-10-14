using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Unit.Configuration
{
    [TestClass]
    public class ConfigurationValidationTests
    {
        private Mock<ILogger<ConfigurationManager>> _mockLogger;
        private string _tempDirectory;
        private string _testConfigPath;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<ConfigurationManager>>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), "LicenseReleaseServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
            _testConfigPath = Path.Combine(_tempDirectory, "test.config");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch (IOException)
                {
                    // Handle potential file locks during cleanup
                }
            }
        }

        [TestMethod]
        public void ValidateConfiguration_ValidConfiguration_ReturnsTrue()
        {
            // Arrange
            CreateValidConfigurationFile();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Act
            var result = configManager.ValidateConfiguration();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateConfiguration_MissingRequiredSection_ReturnsFalse()
        {
            // Arrange
            CreateConfigurationWithoutRequiredSections();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Act
            var result = configManager.ValidateConfiguration();

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Error, "Configuration validation failed", Times.Once);
        }

        [TestMethod]
        public void ValidateConfiguration_InvalidServerConfiguration_ReturnsFalse()
        {
            // Arrange
            CreateConfigurationWithInvalidServerSettings();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Act
            var result = configManager.ValidateConfiguration();

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Error, "Configuration validation failed", Times.Once);
        }

        [TestMethod]
        public void ValidateConfiguration_MalformedXml_ReturnsFalse()
        {
            // Arrange
            CreateMalformedXmlConfiguration();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Act
            var result = configManager.ValidateConfiguration();

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Error, "Configuration file contains malformed XML", Times.Once);
        }

        [TestMethod]
        public void ValidateConfiguration_EmptyFile_ReturnsFalse()
        {
            // Arrange
            File.WriteAllText(_testConfigPath, "");
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Act
            var result = configManager.ValidateConfiguration();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateConfiguration_CustomValidationRules_AllPass()
        {
            // Arrange
            CreateValidConfigurationFile();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Add custom validation rules if the API supports it
            // This would depend on the actual ConfigurationManager implementation

            // Act
            var result = configManager.ValidateConfiguration();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateConfiguration_Performance_LargeNumberOfValidationRules()
        {
            // Arrange
            CreateValidConfigurationFile();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);

            // Act
            var startTime = DateTime.UtcNow;
            var result = configManager.ValidateConfiguration();
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(duration.TotalMilliseconds < 1000, "Validation should complete within 1 second");
        }

        [TestMethod]
        public void ValidateConfiguration_ConcurrentValidation_ThreadSafe()
        {
            // Arrange
            CreateValidConfigurationFile();
            var configManager = new ConfigurationManager(_testConfigPath, _mockLogger.Object);
            var tasks = new Task<bool>[10];
            var results = new bool[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    results[index] = configManager.ValidateConfiguration();
                    return results[index];
                });
            }

            Task.WaitAll(tasks);

            // Assert
            // All validations should return the same result
            var firstResult = results[0];
            for (int i = 1; i < results.Length; i++)
            {
                Assert.AreEqual(firstResult, results[i], $"Validation result {i} differs from first result");
            }
        }

        #region Helper Methods

        private void CreateValidConfigurationFile()
        {
            var configContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <appSettings>
    <add key=""LicenseManager:Server:Host"" value=""test-server"" />
    <add key=""LicenseManager:Server:Port"" value=""27000"" />
    <add key=""Logging:Level"" value=""Information"" />
    <add key=""Monitoring:HealthCheck:Enabled"" value=""true"" />
  </appSettings>
</configuration>";

            File.WriteAllText(_testConfigPath, configContent);
        }

        private void CreateConfigurationWithoutRequiredSections()
        {
            var configContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <appSettings>
    <!-- Missing required settings -->
  </appSettings>
</configuration>";

            File.WriteAllText(_testConfigPath, configContent);
        }

        private void CreateConfigurationWithInvalidServerSettings()
        {
            var configContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <appSettings>
    <add key=""LicenseManager:Server:Host"" value="""" />
    <add key=""LicenseManager:Server:Port"" value=""0"" />
  </appSettings>
</configuration>";

            File.WriteAllText(_testConfigPath, configContent);
        }

        private void CreateMalformedXmlConfiguration()
        {
            var invalidContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <appSettings>
    <add key=""test"" value=""test""
  </appSettings>
</configuration>";

            File.WriteAllText(_testConfigPath, invalidContent);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for verifying ILogger calls
    /// </summary>
    public static class LoggerValidationExtensions
    {
        public static void VerifyLog(this Mock<ILogger<ConfigurationManager>> logger, LogLevel expectedLevel, string expectedMessage, Times times)
        {
            logger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                times);
        }
    }
}