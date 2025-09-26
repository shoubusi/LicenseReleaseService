using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for TimerConfigurationElement
    /// </summary>
    [TestClass]
    public class TimerConfigurationElementTests
    {
        private TimerConfigurationElement _element;
        private string _testConfigPath;
        private string _originalConfigContent;

        [TestInitialize]
        public void TestInitialize()
        {
            _element = new TimerConfigurationElement();
            _originalConfigContent = File.ReadAllText("App.config");
            _testConfigPath = Path.GetTempFileName();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [TestMethod]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var element = new TimerConfigurationElement();

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(1), element.DefaultInterval);
            Assert.AreEqual(5, element.MaxConsecutiveErrors);
            Assert.AreEqual(1, element.MaxConcurrentExecutions);
            Assert.AreEqual(TimeSpan.FromMinutes(5), element.CircuitBreakerCooldown);
            Assert.AreEqual(true, element.EnableAutoRestart);
            Assert.AreEqual(true, element.EnableExecutionTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(5), element.ExecutionTimeout);
            Assert.AreEqual(true, element.EnableMetrics);
            Assert.AreEqual(true, element.EnableDetailedLogging);
            Assert.AreEqual(true, element.EnableCircuitBreaker);
            Assert.AreEqual(100, element.MaxExecutionHistory);
            Assert.AreEqual(TimeSpan.FromSeconds(1), element.MinInterval);
            Assert.AreEqual(TimeSpan.FromDays(1), element.MaxInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(30), element.SyncTimeout);
            Assert.AreEqual(false, element.StopOnUnhandledException);
            Assert.AreEqual(TimeSpan.FromSeconds(5), element.DisposalGracePeriod);
            Assert.AreEqual(true, element.PreventExecutionOverlap);
            Assert.AreEqual(TimeSpan.FromSeconds(10), element.StartupDelay);
            Assert.AreEqual(TimeSpan.FromSeconds(30), element.ShutdownTimeout);
            Assert.AreEqual(60, element.HealthCheckInterval);
            Assert.AreEqual(30, element.MetricsCollectionInterval);
            Assert.AreEqual(false, element.EnableAdaptiveScheduling);
            Assert.AreEqual(80, element.AdaptiveThreshold);
            Assert.AreEqual(TimeSpan.FromMinutes(5), element.AdaptiveCooldown);
            Assert.AreEqual(true, element.EnableMemoryMonitoring);
            Assert.AreEqual(52428800, element.MemoryThreshold);
            Assert.AreEqual(true, element.EnableCpuMonitoring);
            Assert.AreEqual(80, element.CpuThreshold);
            Assert.AreEqual(true, element.EnableThreadMonitoring);
            Assert.AreEqual(10, element.MaxThreadPoolThreads);
        }

        [TestMethod]
        public void MaxConsecutiveErrors_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 5, 10, 100 };
            var invalidValues = new[] { 0, -1, 101 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxConsecutiveErrors = value;
                Assert.AreEqual(value, _element.MaxConsecutiveErrors);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxConsecutiveErrors = value);
            }
        }

        [TestMethod]
        public void MaxConcurrentExecutions_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 5, 25, 50 };
            var invalidValues = new[] { 0, -1, 51 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxConcurrentExecutions = value;
                Assert.AreEqual(value, _element.MaxConcurrentExecutions);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxConcurrentExecutions = value);
            }
        }

        [TestMethod]
        public void MaxExecutionHistory_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 0, 50, 100, 5000, 10000 };
            var invalidValues = new[] { -1, 10001 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxExecutionHistory = value;
                Assert.AreEqual(value, _element.MaxExecutionHistory);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxExecutionHistory = value);
            }
        }

        [TestMethod]
        public void HealthCheckInterval_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 10, 60, 300, 3600 };
            var invalidValues = new[] { 0, -1, 9, 3601 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.HealthCheckInterval = value;
                Assert.AreEqual(value, _element.HealthCheckInterval);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.HealthCheckInterval = value);
            }
        }

        [TestMethod]
        public void MetricsCollectionInterval_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 5, 30, 300, 600 };
            var invalidValues = new[] { 0, -1, 4, 601 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MetricsCollectionInterval = value;
                Assert.AreEqual(value, _element.MetricsCollectionInterval);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MetricsCollectionInterval = value);
            }
        }

        [TestMethod]
        public void AdaptiveThreshold_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 50, 75, 90, 95 };
            var invalidValues = new[] { 49, -1, 96, 100 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.AdaptiveThreshold = value;
                Assert.AreEqual(value, _element.AdaptiveThreshold);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.AdaptiveThreshold = value);
            }
        }

        [TestMethod]
        public void MemoryThreshold_ShouldValidateRange()
        {
            // Arrange
            var validValues = new long[] { 1048576, 52428800, 1073741824 };
            var invalidValues = new long[] { 1048575, -1, 1073741825 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MemoryThreshold = value;
                Assert.AreEqual(value, _element.MemoryThreshold);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MemoryThreshold = value);
            }
        }

        [TestMethod]
        public void CpuThreshold_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 50, 75, 90, 100 };
            var invalidValues = new[] { 49, -1, 101 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.CpuThreshold = value;
                Assert.AreEqual(value, _element.CpuThreshold);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.CpuThreshold = value);
            }
        }

        [TestMethod]
        public void MaxThreadPoolThreads_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 10, 50, 100 };
            var invalidValues = new[] { 0, -1, 101 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxThreadPoolThreads = value;
                Assert.AreEqual(value, _element.MaxThreadPoolThreads);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxThreadPoolThreads = value);
            }
        }

        [TestMethod]
        public void Validate_ShouldReturnEmptyListForValidConfiguration()
        {
            // Arrange
            var element = CreateValidTimerConfigurationElement();

            // Act
            var errors = element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_ShouldDetectIntervalBoundaryIssues()
        {
            // Arrange
            _element.MinInterval = TimeSpan.FromMinutes(2);
            _element.MaxInterval = TimeSpan.FromMinutes(1); // Less than min interval

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Min interval cannot be greater than max interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectDefaultIntervalBoundaryIssues()
        {
            // Arrange
            _element.DefaultInterval = TimeSpan.FromSeconds(30);
            _element.MinInterval = TimeSpan.FromMinutes(1); // Greater than default

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Default interval cannot be less than min interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectDefaultIntervalExceedsMax()
        {
            // Arrange
            _element.DefaultInterval = TimeSpan.FromMinutes(10);
            _element.MaxInterval = TimeSpan.FromMinutes(5); // Less than default

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Default interval cannot be greater than max interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectTimeoutRelationshipIssues()
        {
            // Arrange
            _element.ExecutionTimeout = TimeSpan.FromMinutes(2);
            _element.DefaultInterval = TimeSpan.FromMinutes(5); // Greater than execution timeout

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Execution timeout should be greater than or equal to default interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectMetricsIntervalRelationshipIssues()
        {
            // Arrange
            _element.MetricsCollectionInterval = 60;
            _element.HealthCheckInterval = 30; // Less than metrics interval

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Metrics collection interval should be less than or equal to health check interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectAdaptiveSchedulingDependencyIssues()
        {
            // Arrange
            _element.EnableAdaptiveScheduling = true;
            _element.EnableMetrics = false;

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Metrics must be enabled when adaptive scheduling is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectMemoryMonitoringDependencyIssues()
        {
            // Arrange
            _element.EnableMemoryMonitoring = true;
            _element.EnableMetrics = false;

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Metrics must be enabled when memory monitoring is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectCpuMonitoringDependencyIssues()
        {
            // Arrange
            _element.EnableCpuMonitoring = true;
            _element.EnableMetrics = false;

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Metrics must be enabled when CPU monitoring is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectThreadMonitoringDependencyIssues()
        {
            // Arrange
            _element.EnableThreadMonitoring = true;
            _element.EnableMetrics = false;

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Metrics must be enabled when thread monitoring is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectCircuitBreakerDependencyIssues()
        {
            // Arrange
            _element.EnableCircuitBreaker = true;
            _element.MaxConsecutiveErrors = 0; // Invalid when circuit breaker is enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max consecutive errors must be at least 1 when circuit breaker is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectExecutionOverlapPreventionIssues()
        {
            // Arrange
            _element.PreventExecutionOverlap = true;
            _element.MaxConcurrentExecutions = 2; // Greater than 1 when overlap prevention is enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max concurrent executions must be 1 when execution overlap prevention is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStartupDelayLimitIssues()
        {
            // Arrange
            _element.StartupDelay = TimeSpan.FromMinutes(6); // Exceeds 5 minutes

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Startup delay should not exceed 5 minutes")));
        }

        [TestMethod]
        public void Validate_ShouldDetectShutdownTimeoutLimitIssues()
        {
            // Arrange
            _element.ShutdownTimeout = TimeSpan.FromMinutes(11); // Exceeds 10 minutes

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Shutdown timeout should not exceed 10 minutes")));
        }

        [TestMethod]
        public void Validate_ShouldDetectMemoryThresholdLimitIssues()
        {
            // Arrange
            _element.MemoryThreshold = 2147483648; // 2GB, exceeds recommended limit

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Memory threshold exceeds recommended limit of 1GB")));
        }

        [TestMethod]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            _element.DefaultInterval = TimeSpan.FromMinutes(2);
            _element.MaxConsecutiveErrors = 3;
            _element.EnableMetrics = false;
            _element.EnableDetailedLogging = false;

            // Act
            var result = _element.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("TimerConfiguration["));
            Assert.IsTrue(result.Contains("DefaultInterval=120000.00ms"));
            Assert.IsTrue(result.Contains("MaxConsecutiveErrors=3"));
            Assert.IsTrue(result.Contains("EnableMetrics=False"));
            Assert.IsTrue(result.Contains("EnableDetailedLogging=False"));
        }

        [TestMethod]
        public void ToTimerExecutionOptions_ShouldConvertCorrectly()
        {
            // Arrange
            _element.DefaultInterval = TimeSpan.FromMinutes(2);
            _element.MaxConsecutiveErrors = 3;
            _element.CircuitBreakerCooldown = TimeSpan.FromMinutes(10);
            _element.EnableAutoRestart = false;
            _element.EnableMetrics = false;
            _element.MaxExecutionHistory = 50;

            // Act
            var options = _element.ToTimerExecutionOptions();

            // Assert
            Assert.AreEqual(_element.DefaultInterval, options.DefaultInterval);
            Assert.AreEqual(_element.MaxConsecutiveErrors, options.MaxConsecutiveErrors);
            Assert.AreEqual(_element.CircuitBreakerCooldown, options.CircuitBreakerCooldown);
            Assert.AreEqual(_element.EnableAutoRestart, options.EnableAutoRestart);
            Assert.AreEqual(_element.EnableMetrics, options.EnableMetrics);
            Assert.AreEqual(_element.MaxExecutionHistory, options.MaxExecutionHistory);
            Assert.AreEqual(_element.MaxConcurrentExecutions, options.MaxConcurrentExecutions);
            Assert.AreEqual(_element.EnableExecutionTimeout, options.EnableExecutionTimeout);
            Assert.AreEqual(_element.ExecutionTimeout, options.ExecutionTimeout);
            Assert.AreEqual(_element.EnableDetailedLogging, options.EnableDetailedLogging);
            Assert.AreEqual(_element.EnableCircuitBreaker, options.EnableCircuitBreaker);
            Assert.AreEqual(_element.MinInterval, options.MinInterval);
            Assert.AreEqual(_element.MaxInterval, options.MaxInterval);
            Assert.AreEqual(_element.SyncTimeout, options.SyncTimeout);
            Assert.AreEqual(_element.StopOnUnhandledException, options.StopOnUnhandledException);
            Assert.AreEqual(_element.DisposalGracePeriod, options.DisposalGracePeriod);
            Assert.AreEqual(_element.PreventExecutionOverlap, options.PreventExecutionOverlap);
        }

        [TestMethod]
        public void ToTimerExecutionOptions_ShouldHandleAllProperties()
        {
            // Arrange
            var element = CreateValidTimerConfigurationElement();

            // Act
            var options = element.ToTimerExecutionOptions();

            // Assert
            Assert.AreEqual(element.DefaultInterval, options.DefaultInterval);
            Assert.AreEqual(element.MaxConsecutiveErrors, options.MaxConsecutiveErrors);
            Assert.AreEqual(element.MaxConcurrentExecutions, options.MaxConcurrentExecutions);
            Assert.AreEqual(element.CircuitBreakerCooldown, options.CircuitBreakerCooldown);
            Assert.AreEqual(element.EnableAutoRestart, options.EnableAutoRestart);
            Assert.AreEqual(element.EnableExecutionTimeout, options.EnableExecutionTimeout);
            Assert.AreEqual(element.ExecutionTimeout, options.ExecutionTimeout);
            Assert.AreEqual(element.EnableMetrics, options.EnableMetrics);
            Assert.AreEqual(element.EnableDetailedLogging, options.EnableDetailedLogging);
            Assert.AreEqual(element.EnableCircuitBreaker, options.EnableCircuitBreaker);
            Assert.AreEqual(element.MaxExecutionHistory, options.MaxExecutionHistory);
            Assert.AreEqual(element.MinInterval, options.MinInterval);
            Assert.AreEqual(element.MaxInterval, options.MaxInterval);
            Assert.AreEqual(element.SyncTimeout, options.SyncTimeout);
            Assert.AreEqual(element.StopOnUnhandledException, options.StopOnUnhandledException);
            Assert.AreEqual(element.DisposalGracePeriod, options.DisposalGracePeriod);
            Assert.AreEqual(element.PreventExecutionOverlap, options.PreventExecutionOverlap);
        }

        [TestMethod]
        public void ConfigurationIntegration_ShouldLoadFromConfigFile()
        {
            // Arrange - Create a test config file with timer settings
            var testConfigContent = CreateTestConfigContent();
            File.WriteAllText(_testConfigPath, testConfigContent);

            // Act - Load configuration from test file
            var configMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = _testConfigPath
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            var section = config.GetSection("licenseReleaseService") as LicenseReleaseServiceSection;

            // Assert
            Assert.IsNotNull(section);
            Assert.IsNotNull(section.Timer);
            Assert.AreEqual(TimeSpan.FromMinutes(2), section.Timer.DefaultInterval);
            Assert.AreEqual(3, section.Timer.MaxConsecutiveErrors);
            Assert.AreEqual(false, section.Timer.EnableMetrics);
            Assert.AreEqual(false, section.Timer.EnableDetailedLogging);
            Assert.AreEqual(50, section.Timer.MaxExecutionHistory);
        }

        [TestMethod]
        public void Validation_ShouldWorkWithConfigurationManager()
        {
            // Arrange - Create a test config with invalid settings
            var invalidConfigContent = CreateInvalidTestConfigContent();
            File.WriteAllText(_testConfigPath, invalidConfigContent);

            // Act - Load and validate configuration
            var configMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = _testConfigPath
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            var section = config.GetSection("licenseReleaseService") as LicenseReleaseServiceSection;

            // Assert
            Assert.IsNotNull(section);
            var errors = section.Validate();
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("Default interval cannot be less than min interval")));
        }

        [TestMethod]
        public void TimeSpans_ShouldHandleEdgeCases()
        {
            // Arrange
            var edgeCases = new[]
            {
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(1),
                TimeSpan.FromHours(1),
                TimeSpan.FromDays(1)
            };

            // Act & Assert
            foreach (var timeSpan in edgeCases)
            {
                _element.DefaultInterval = timeSpan;
                Assert.AreEqual(timeSpan, _element.DefaultInterval);
            }
        }

        [TestMethod]
        public void BooleanProperties_ShouldHandleTrueFalse()
        {
            // Arrange
            var booleanProperties = new[]
            {
                nameof(TimerConfigurationElement.EnableAutoRestart),
                nameof(TimerConfigurationElement.EnableExecutionTimeout),
                nameof(TimerConfigurationElement.EnableMetrics),
                nameof(TimerConfigurationElement.EnableDetailedLogging),
                nameof(TimerConfigurationElement.EnableCircuitBreaker),
                nameof(TimerConfigurationElement.StopOnUnhandledException),
                nameof(TimerConfigurationElement.PreventExecutionOverlap),
                nameof(TimerConfigurationElement.EnableAdaptiveScheduling),
                nameof(TimerConfigurationElement.EnableMemoryMonitoring),
                nameof(TimerConfigurationElement.EnableCpuMonitoring),
                nameof(TimerConfigurationElement.EnableThreadMonitoring)
            };

            // Act & Assert
            foreach (var propertyName in booleanProperties)
            {
                var property = _element.GetType().GetProperty(propertyName);
                Assert.IsNotNull(property);

                // Test true
                property.SetValue(_element, true);
                Assert.AreEqual(true, property.GetValue(_element));

                // Test false
                property.SetValue(_element, false);
                Assert.AreEqual(false, property.GetValue(_element));
            }
        }

        private TimerConfigurationElement CreateValidTimerConfigurationElement()
        {
            return new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(5),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(5),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 100,
                MinInterval = TimeSpan.FromSeconds(1),
                MaxInterval = TimeSpan.FromDays(1),
                SyncTimeout = TimeSpan.FromSeconds(30),
                StopOnUnhandledException = false,
                DisposalGracePeriod = TimeSpan.FromSeconds(5),
                PreventExecutionOverlap = true,
                StartupDelay = TimeSpan.FromSeconds(10),
                ShutdownTimeout = TimeSpan.FromSeconds(30),
                HealthCheckInterval = 60,
                MetricsCollectionInterval = 30,
                EnableAdaptiveScheduling = false,
                AdaptiveThreshold = 80,
                AdaptiveCooldown = TimeSpan.FromMinutes(5),
                EnableMemoryMonitoring = true,
                MemoryThreshold = 52428800,
                EnableCpuMonitoring = true,
                CpuThreshold = 80,
                EnableThreadMonitoring = true,
                MaxThreadPoolThreads = 10
            };
        }

        private string CreateTestConfigContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""licenseReleaseService"" type=""LicenseReleaseService.Configuration.LicenseReleaseServiceSection, LicenseReleaseService"" />
  </configSections>
  <licenseReleaseService>
    <timer
      defaultInterval=""00:02:00""
      maxConsecutiveErrors=""3""
      maxConcurrentExecutions=""1""
      circuitBreakerCooldown=""00:05:00""
      enableAutoRestart=""true""
      enableExecutionTimeout=""true""
      executionTimeout=""00:05:00""
      enableMetrics=""false""
      enableDetailedLogging=""false""
      enableCircuitBreaker=""true""
      maxExecutionHistory=""50""
      minInterval=""00:00:01""
      maxInterval=""1.00:00:00""
      syncTimeout=""00:00:30""
      stopOnUnhandledException=""false""
      disposalGracePeriod=""00:00:05""
      preventExecutionOverlap=""true""
      startupDelay=""00:00:10""
      shutdownTimeout=""00:00:30""
      healthCheckInterval=""60""
      metricsCollectionInterval=""30""
      enableAdaptiveScheduling=""false""
      adaptiveThreshold=""80""
      adaptiveCooldown=""00:05:00""
      enableMemoryMonitoring=""true""
      memoryThreshold=""52428800""
      enableCpuMonitoring=""true""
      cpuThreshold=""80""
      enableThreadMonitoring=""true""
      maxThreadPoolThreads=""10""
    />
  </licenseReleaseService>
</configuration>";
        }

        private string CreateInvalidTestConfigContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""licenseReleaseService"" type=""LicenseReleaseService.Configuration.LicenseReleaseServiceSection, LicenseReleaseService"" />
  </configSections>
  <licenseReleaseService>
    <timer
      defaultInterval=""00:00:30""
      maxConsecutiveErrors=""0""
      maxConcurrentExecutions=""0""
      circuitBreakerCooldown=""00:00:00""
      enableAutoRestart=""true""
      enableExecutionTimeout=""true""
      executionTimeout=""00:00:00""
      enableMetrics=""true""
      enableDetailedLogging=""true""
      enableCircuitBreaker=""true""
      maxExecutionHistory=""-1""
      minInterval=""00:01:00""
      maxInterval=""00:00:30""
      syncTimeout=""00:00:00""
      stopOnUnhandledException=""false""
      disposalGracePeriod=""00:00:00""
      preventExecutionOverlap=""true""
      startupDelay=""00:06:00""
      shutdownTimeout=""00:11:00""
      healthCheckInterval=""0""
      metricsCollectionInterval=""0""
      enableAdaptiveScheduling=""true""
      adaptiveThreshold=""40""
      adaptiveCooldown=""00:00:00""
      enableMemoryMonitoring=""true""
      memoryThreshold=""1048575""
      enableCpuMonitoring=""true""
      cpuThreshold=""40""
      enableThreadMonitoring=""true""
      maxThreadPoolThreads=""0""
    />
  </licenseReleaseService>
</configuration>";
        }
    }
}