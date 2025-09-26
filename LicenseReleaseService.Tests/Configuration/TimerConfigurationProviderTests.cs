using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for TimerConfigurationProvider
    /// </summary>
    [TestClass]
    public class TimerConfigurationProviderTests
    {
        private ConfigurationManager _mockConfigurationManager;
        private TimerConfigurationProvider _provider;
        private string _testConfigPath;
        private List<TimerConfigurationChangedEventArgs> _configurationEvents;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockConfigurationManager = new ConfigurationManager();
            _configurationEvents = new List<TimerConfigurationChangedEventArgs>();
            _testConfigPath = Path.GetTempFileName();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _provider?.Dispose();
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [TestMethod]
        public void Constructor_ShouldInitializeWithValidParameters()
        {
            // Arrange & Act
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Assert
            Assert.IsNotNull(provider);
            Assert.AreEqual(TimeSpan.FromMinutes(5), provider.CacheDuration);
        }

        [TestMethod]
        public void Constructor_ShouldThrowWithNullConfigurationManager()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new TimerConfigurationProvider(null));
        }

        [TestMethod]
        public void CacheDuration_ShouldValidateRange()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act & Assert - Valid values
            var validDurations = new[]
            {
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromHours(1)
            };

            foreach (var duration in validDurations)
            {
                provider.CacheDuration = duration;
                Assert.AreEqual(duration, provider.CacheDuration);
            }

            // Act & Assert - Invalid values should throw
            var invalidDurations = new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(-1),
                TimeSpan.FromHours(-1)
            };

            foreach (var duration in invalidDurations)
            {
                Assert.ThrowsException<ArgumentException>(() => provider.CacheDuration = duration);
            }
        }

        [TestMethod]
        public void CurrentConfiguration_ShouldReturnDefaultConfiguration()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act
            var config = provider.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual(TimeSpan.FromMinutes(1), config.DefaultInterval);
            Assert.AreEqual(5, config.MaxConsecutiveErrors);
            Assert.AreEqual(1, config.MaxConcurrentExecutions);
            Assert.IsTrue(config.EnableAutoRestart);
            Assert.IsTrue(config.EnableMetrics);
        }

        [TestMethod]
        public void TimerExecutionOptions_ShouldReturnValidOptions()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act
            var options = provider.TimerExecutionOptions;

            // Assert
            Assert.IsNotNull(options);
            Assert.AreEqual(TimeSpan.FromMinutes(1), options.DefaultInterval);
            Assert.AreEqual(5, options.MaxConsecutiveErrors);
            Assert.AreEqual(1, options.MaxConcurrentExecutions);
            Assert.IsTrue(options.EnableAutoRestart);
            Assert.IsTrue(options.EnableMetrics);
        }

        [TestMethod]
        public void ForceReload_ShouldReloadConfiguration()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            var originalConfig = provider.CurrentConfiguration;

            // Act
            provider.ForceReload();
            var reloadedConfig = provider.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(reloadedConfig);
            // Configuration should be the same since we're using the same mock
            Assert.AreEqual(originalConfig.DefaultInterval, reloadedConfig.DefaultInterval);
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldReturnEmptyListForValidConfiguration()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act
            var errors = provider.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldDetectCacheDurationIssues()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.CacheDuration = TimeSpan.FromSeconds(20); // Too short

            // Act
            var errors = provider.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Cache duration should be at least 30 seconds")));
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldDetectLongCacheDuration()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.CacheDuration = TimeSpan.FromHours(2); // Too long

            // Act
            var errors = provider.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Cache duration should not exceed 1 hour")));
        }

        [TestMethod]
        public void GetStatistics_ShouldReturnValidStatistics()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act
            var stats = provider.GetStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(TimeSpan.FromMinutes(5), stats.CacheDuration);
            Assert.IsTrue(stats.IsCacheValid);
            Assert.AreEqual(TimeSpan.FromMinutes(1) * 1000, stats.DefaultIntervalMs);
            Assert.AreEqual(5, stats.MaxConsecutiveErrors);
            Assert.AreEqual(1, stats.MaxConcurrentExecutions);
            Assert.AreEqual(TimeSpan.FromMinutes(5) * 1000, stats.CircuitBreakerCooldownMs);
            Assert.IsTrue(stats.EnableAutoRestart);
            Assert.IsTrue(stats.EnableMetrics);
            Assert.IsTrue(stats.EnableDetailedLogging);
            Assert.IsTrue(stats.EnableCircuitBreaker);
            Assert.IsFalse(stats.EnableAdaptiveScheduling);
            Assert.IsTrue(stats.EnableMemoryMonitoring);
            Assert.IsTrue(stats.EnableCpuMonitoring);
            Assert.IsTrue(stats.EnableThreadMonitoring);
        }

        [TestMethod]
        public void ConfigurationReloadedEvent_ShouldBeRaisedOnReload()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.ConfigurationReloaded += (sender, args) => _configurationEvents.Add(args);

            // Act
            provider.ForceReload();

            // Assert
            Assert.AreEqual(1, _configurationEvents.Count);
            Assert.IsNotNull(_configurationEvents[0].NewConfiguration);
            Assert.IsNotNull(_configurationEvents[0].ReloadTime);
        }

        [TestMethod]
        public void ConfigurationReloadedEvent_ShouldNotBeRaisedForSameConfiguration()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.ConfigurationReloaded += (sender, args) => _configurationEvents.Add(args);

            // Act - Multiple reloads with same configuration
            provider.ForceReload();
            provider.ForceReload();

            // Assert
            // Event should not be raised for the second reload since configuration is the same
            Assert.AreEqual(1, _configurationEvents.Count);
        }

        [TestMethod]
        public void Dispose_ShouldCleanUpResources()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act
            provider.Dispose();

            // Assert
            // Should not throw exception when disposing multiple times
            provider.Dispose();
        }

        [TestMethod]
        public void CacheExpiration_ShouldTriggerReload()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.CacheDuration = TimeSpan.FromMilliseconds(100); // Very short cache

            var originalConfig = provider.CurrentConfiguration;
            provider.ConfigurationReloaded += (sender, args) => _configurationEvents.Add(args);

            // Act
            Thread.Sleep(150); // Wait for cache to expire
            var newConfig = provider.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(newConfig);
            // Configuration should be reloaded (though values may be the same)
            Assert.IsTrue(_configurationEvents.Count >= 1);
        }

        [TestMethod]
        public void ConfigurationProvider_ShouldHandleConfigurationErrorsGracefully()
        {
            // Arrange - Create a mock configuration manager that throws exceptions
            var faultyManager = new FaultyConfigurationManager();
            var provider = new TimerConfigurationProvider(faultyManager);

            // Act
            var config = provider.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(config);
            // Should fall back to default configuration
            Assert.AreEqual(TimeSpan.FromMinutes(1), config.DefaultInterval);
        }

        [TestMethod]
        public void GetStatistics_ShouldShowCacheState()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.CacheDuration = TimeSpan.FromSeconds(1);

            // Act
            var stats1 = provider.GetStatistics();
            Thread.Sleep(1100); // Wait for cache to expire
            var stats2 = provider.GetStatistics();

            // Assert
            Assert.IsTrue(stats1.IsCacheValid);
            Assert.IsFalse(stats2.IsCacheValid); // Cache should have expired
            Assert.IsTrue(stats2.CacheTimeRemaining.TotalSeconds < 0);
        }

        [TestMethod]
        public void TimerConfigurationProvider_ShouldBeThreadSafe()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            var results = new List<TimerConfigurationElement>();
            var threads = new List<Thread>();

            // Act - Create multiple threads that access the configuration concurrently
            for (int i = 0; i < 10; i++)
            {
                var thread = new Thread(() =>
                {
                    var config = provider.CurrentConfiguration;
                    lock (results)
                    {
                        results.Add(config);
                    }
                });
                threads.Add(thread);
            }

            // Start all threads
            foreach (var thread in threads)
            {
                thread.Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.AreEqual(10, results.Count);
            // All configurations should be valid
            foreach (var config in results)
            {
                Assert.IsNotNull(config);
                Assert.AreEqual(TimeSpan.FromMinutes(1), config.DefaultInterval);
            }
        }

        [TestMethod]
        public void ConfigurationProvider_ShouldWorkWithRealConfiguration()
        {
            // Arrange - Create a real configuration file
            var testConfigContent = CreateRealTestConfigContent();
            File.WriteAllText(_testConfigPath, testConfigContent);

            var configMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = _testConfigPath
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            var realManager = new ConfigurationManager();

            var provider = new TimerConfigurationProvider(realManager);

            // Act
            var providerConfig = provider.CurrentConfiguration;
            var stats = provider.GetStatistics();

            // Assert
            Assert.IsNotNull(providerConfig);
            Assert.IsNotNull(stats);
            Assert.AreEqual(TimeSpan.FromMinutes(1), providerConfig.DefaultInterval);
            Assert.IsTrue(stats.ConfigWatcherEnabled);
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldDetectAllValidationIssues()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);
            provider.CacheDuration = TimeSpan.FromSeconds(20); // Too short

            // Act
            var errors = provider.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("Cache duration should be at least 30 seconds")));
        }

        [TestMethod]
        public void TimerExecutionOptions_ShouldMatchCurrentConfiguration()
        {
            // Arrange
            var provider = new TimerConfigurationProvider(_mockConfigurationManager);

            // Act
            var config = provider.CurrentConfiguration;
            var options = provider.TimerExecutionOptions;

            // Assert
            Assert.AreEqual(config.DefaultInterval, options.DefaultInterval);
            Assert.AreEqual(config.MaxConsecutiveErrors, options.MaxConsecutiveErrors);
            Assert.AreEqual(config.MaxConcurrentExecutions, options.MaxConcurrentExecutions);
            Assert.AreEqual(config.CircuitBreakerCooldown, options.CircuitBreakerCooldown);
            Assert.AreEqual(config.EnableAutoRestart, options.EnableAutoRestart);
            Assert.AreEqual(config.EnableExecutionTimeout, options.EnableExecutionTimeout);
            Assert.AreEqual(config.ExecutionTimeout, options.ExecutionTimeout);
            Assert.AreEqual(config.EnableMetrics, options.EnableMetrics);
            Assert.AreEqual(config.EnableDetailedLogging, options.EnableDetailedLogging);
            Assert.AreEqual(config.EnableCircuitBreaker, options.EnableCircuitBreaker);
            Assert.AreEqual(config.MaxExecutionHistory, options.MaxExecutionHistory);
            Assert.AreEqual(config.MinInterval, options.MinInterval);
            Assert.AreEqual(config.MaxInterval, options.MaxInterval);
            Assert.AreEqual(config.SyncTimeout, options.SyncTimeout);
            Assert.AreEqual(config.StopOnUnhandledException, options.StopOnUnhandledException);
            Assert.AreEqual(config.DisposalGracePeriod, options.DisposalGracePeriod);
            Assert.AreEqual(config.PreventExecutionOverlap, options.PreventExecutionOverlap);
        }

        private string CreateRealTestConfigContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""licenseReleaseService"" type=""LicenseReleaseService.Configuration.LicenseReleaseServiceSection, LicenseReleaseService"" />
  </configSections>
  <licenseReleaseService>
    <timer
      defaultInterval=""00:01:00""
      maxConsecutiveErrors=""5""
      maxConcurrentExecutions=""1""
      circuitBreakerCooldown=""00:05:00""
      enableAutoRestart=""true""
      enableExecutionTimeout=""true""
      executionTimeout=""00:05:00""
      enableMetrics=""true""
      enableDetailedLogging=""true""
      enableCircuitBreaker=""true""
      maxExecutionHistory=""100""
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

        /// <summary>
        /// Mock configuration manager that throws exceptions for testing error handling
        /// </summary>
        private class FaultyConfigurationManager : ConfigurationManager
        {
            public override LicenseReleaseServiceSection CurrentConfiguration
            {
                get { throw new ConfigurationErrorsException("Simulated configuration error"); }
            }
        }
    }
}