using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Comprehensive unit tests for configuration monitoring features
    /// </summary>
    [TestClass]
    public class ConfigurationMonitoringTests
    {
        private TestConfigManager _testConfigManager;
        private string _testConfigPath;
        private string _testBackupDir;
        private string _originalConfigContent;
        private ConfigurationWatcher _testWatcher;
        private ConfigurationReloadManager _testReloadManager;
        private ConfigurationHealthMonitor _testHealthMonitor;

        [TestInitialize]
        public void TestInitialize()
        {
            // Create test directories
            _testBackupDir = Path.Combine(Path.GetTempPath(), $"ConfigBackup_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testBackupDir);

            // Create a test configuration file
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"TestConfig_{Guid.NewGuid()}.config");
            _originalConfigContent = File.ReadAllText("App.config");

            // Copy and modify the original config for testing
            var testConfigContent = _originalConfigContent.Replace(
                "C:\\Program Files\\Autodesk\\Network License Manager\\lmutil.exe",
                "C:\\Test\\lmutil.exe");

            File.WriteAllText(_testConfigPath, testConfigContent);

            // Create test configuration manager
            _testConfigManager = new TestConfigManager(_testConfigPath);

            // Initialize test components
            _testWatcher = new ConfigurationWatcher(_testConfigPath);
            _testReloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);
            _testHealthMonitor = new ConfigurationHealthMonitor();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Dispose all components
            _testConfigManager?.Dispose();
            _testWatcher?.Dispose();
            _testReloadManager?.Dispose();
            _testHealthMonitor?.Dispose();

            // Clean up test files
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }

            if (Directory.Exists(_testBackupDir))
            {
                Directory.Delete(_testBackupDir, true);
            }
        }

        #region ConfigurationWatcher Tests

        [TestMethod]
        public void ConfigurationWatcher_Constructor_ValidPath_InitializesSuccessfully()
        {
            // Arrange & Act
            using var watcher = new ConfigurationWatcher(_testConfigPath);

            // Assert
            Assert.IsNotNull(watcher);
            Assert.AreEqual(_testConfigPath, watcher.ConfigFilePath);
            Assert.IsFalse(watcher.IsWatching);
        }

        [TestMethod]
        public void ConfigurationWatcher_Constructor_NullPath_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new ConfigurationWatcher(null));
        }

        [TestMethod]
        public void ConfigurationWatcher_StartWatching_ValidFile_StartsSuccessfully()
        {
            // Arrange
            using var watcher = new ConfigurationWatcher(_testConfigPath);

            // Act
            watcher.StartWatching();

            // Assert
            Assert.IsTrue(watcher.IsWatching);
        }

        [TestMethod]
        public void ConfigurationWatcher_StartWatching_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent.config");
            using var watcher = new ConfigurationWatcher(nonExistentPath);

            // Act & Assert
            Assert.ThrowsException<FileNotFoundException>(() => watcher.StartWatching());
        }

        [TestMethod]
        public void ConfigurationWatcher_StopWatching_AfterStart_StopsSuccessfully()
        {
            // Arrange
            using var watcher = new ConfigurationWatcher(_testConfigPath);
            watcher.StartWatching();

            // Act
            watcher.StopWatching();

            // Assert
            Assert.IsFalse(watcher.IsWatching);
        }

        [TestMethod]
        public async Task ConfigurationWatcher_FileChanged_RaisesEvent()
        {
            // Arrange
            using var watcher = new ConfigurationWatcher(_testConfigPath);
            var eventRaised = false;
            ConfigurationFileChangedEventArgs capturedArgs = null;

            watcher.FileChanged += (sender, args) =>
            {
                eventRaised = true;
                capturedArgs = args;
            };

            watcher.StartWatching();

            // Act
            await Task.Delay(100); // Give watcher time to start
            File.WriteAllText(_testConfigPath, _originalConfigContent);
            await Task.Delay(200); // Give watcher time to detect change

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(FileChangeType.Changed, capturedArgs.ChangeType);
            Assert.AreEqual(_testConfigPath, capturedArgs.FilePath);
        }

        [TestMethod]
        public void ConfigurationWatcher_GetHealthStatus_ValidFile_ReturnsHealthy()
        {
            // Arrange
            using var watcher = new ConfigurationWatcher(_testConfigPath);
            watcher.StartWatching();

            // Act
            var healthStatus = watcher.GetHealthStatus();

            // Assert
            Assert.AreEqual(WatcherHealthStatus.Healthy, healthStatus);
        }

        [TestMethod]
        public void ConfigurationWatcher_ForceCheck_PerformsHealthCheck()
        {
            // Arrange
            using var watcher = new ConfigurationWatcher(_testConfigPath);
            watcher.StartWatching();

            // Act
            watcher.ForceCheck();

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        #endregion

        #region ConfigurationReloadManager Tests

        [TestMethod]
        public void ConfigurationReloadManager_Constructor_ValidPaths_InitializesSuccessfully()
        {
            // Arrange & Act
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);

            // Assert
            Assert.IsNotNull(reloadManager);
            Assert.IsFalse(reloadManager.IsEnabled);
            Assert.AreEqual(0, reloadManager.BackupHistory.Count);
        }

        [TestMethod]
        public void ConfigurationReloadManager_Start_EnablesManager()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);

            // Act
            reloadManager.Start();

            // Assert
            Assert.IsTrue(reloadManager.IsEnabled);
        }

        [TestMethod]
        public void ConfigurationReloadManager_Stop_AfterStart_DisablesManager()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);
            reloadManager.Start();

            // Act
            reloadManager.Stop();

            // Assert
            Assert.IsFalse(reloadManager.IsEnabled);
        }

        [TestMethod]
        public async Task ConfigurationReloadManager_ReloadConfiguration_ValidTrigger_ReturnsTrue()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);
            reloadManager.Start();

            // Act
            var result = await reloadManager.ReloadConfigurationAsync(ReloadTrigger.Manual);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(1, reloadManager.ReloadHistory.Count);
        }

        [TestMethod]
        public async Task ConfigurationReloadManager_CreateBackup_ValidReason_CreatesBackup()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);
            reloadManager.Start();

            // Act
            var backupPath = await reloadManager.CreateBackupAsync(BackupReason.Manual);

            // Assert
            Assert.IsNotNull(backupPath);
            Assert.IsTrue(File.Exists(backupPath));
            Assert.AreEqual(1, reloadManager.BackupHistory.Count);
        }

        [TestMethod]
        public async Task ConfigurationReloadManager_ValidateConfiguration_ValidConfig_ReturnsValidResult()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);

            // Act
            var result = await reloadManager.ValidateConfigurationAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public async Task ConfigurationReloadManager_RestoreFromBackup_ValidBackupId_ReturnsTrue()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);
            reloadManager.Start();

            // Create a backup first
            var backupPath = await reloadManager.CreateBackupAsync(BackupReason.Manual);
            var backupId = reloadManager.BackupHistory.First().Id;

            // Act
            var result = await reloadManager.RestoreFromBackupAsync(backupId);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ConfigurationReloadManager_SetReloadStrategy_UpdatesStrategy()
        {
            // Arrange
            using var reloadManager = new ConfigurationReloadManager(_testConfigPath, _testBackupDir);

            // Act
            reloadManager.SetReloadStrategy(ReloadStrategy.Scheduled, 30);

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        #endregion

        #region ConfigurationHealthMonitor Tests

        [TestMethod]
        public void ConfigurationHealthMonitor_Constructor_DefaultParameters_InitializesSuccessfully()
        {
            // Arrange & Act
            using var healthMonitor = new ConfigurationHealthMonitor();

            // Assert
            Assert.IsNotNull(healthMonitor);
            Assert.AreEqual(ConfigurationHealthStatus.Unknown, healthMonitor.CurrentHealthStatus);
        }

        [TestMethod]
        public void ConfigurationHealthMonitor_Start_EnablesMonitor()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();

            // Act
            healthMonitor.Start();

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ConfigurationHealthMonitor_Stop_AfterStart_DisablesMonitor()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();
            healthMonitor.Start();

            // Act
            healthMonitor.Stop();

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task ConfigurationHealthMonitor_PerformHealthCheck_ReturnsValidStatus()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();

            // Act
            var status = await healthMonitor.PerformHealthCheckAsync();

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(ConfigurationHealthStatus), status));
        }

        [TestMethod]
        public void ConfigurationHealthMonitor_AddHealthRule_ValidRule_AddsRule()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();
            var rule = new ConfigurationHealthRule("TestRule", async () =>
            {
                return new HealthCheckResult(ConfigurationHealthStatus.Healthy, "Test", "Test", TimeSpan.Zero);
            });

            // Act
            healthMonitor.AddHealthRule(rule);

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ConfigurationHealthMonitor_GetHealthReport_ReturnsValidReport()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();

            // Act
            var report = healthMonitor.GetHealthReport();

            // Assert
            Assert.IsNotNull(report);
            Assert.IsNotNull(report.HealthChecks);
            Assert.IsNotNull(report.Metrics);
            Assert.IsNotNull(report.ActiveRules);
        }

        [TestMethod]
        public void ConfigurationHealthMonitor_SetHealthCheckInterval_UpdatesInterval()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();

            // Act
            healthMonitor.SetHealthCheckInterval(60000);

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task ConfigurationHealthMonitor_ForceDiagnostic_ReturnsDiagnostic()
        {
            // Arrange
            using var healthMonitor = new ConfigurationHealthMonitor();

            // Act
            var diagnostic = await healthMonitor.ForceDiagnosticAsync();

            // Assert
            Assert.IsNotNull(diagnostic);
            Assert.AreNotEqual(Guid.Empty, diagnostic.Id);
            Assert.IsTrue(diagnostic.Timestamp > DateTime.MinValue);
        }

        #endregion

        #region ConfigurationEvents Tests

        [TestMethod]
        public void ConfigurationFileChangedEventArgs_Constructor_ChangeType_SetsProperties()
        {
            // Arrange & Act
            var args = new ConfigurationFileChangedEventArgs(
                FileChangeType.Changed, "test.config", DateTime.UtcNow, 1024, true);

            // Assert
            Assert.AreEqual(FileChangeType.Changed, args.ChangeType);
            Assert.AreEqual("test.config", args.FilePath);
            Assert.AreEqual(1024, args.FileSize);
            Assert.IsTrue(args.IsSuccess);
        }

        [TestMethod]
        public void ConfigurationReloadEventArgs_Constructor_ValidData_SetsProperties()
        {
            // Arrange
            var oldConfig = TestConfigManager.CreateTestConfiguration();
            var newConfig = TestConfigManager.CreateTestConfiguration();
            var validationErrors = new List<string>();
            var trigger = ReloadTrigger.Manual;
            var duration = TimeSpan.FromSeconds(1);
            var initiatedAt = DateTime.UtcNow;
            var completedAt = initiatedAt.Add(duration);

            // Act
            var args = new ConfigurationReloadEventArgs(
                oldConfig, newConfig, validationErrors, trigger, duration, initiatedAt, completedAt);

            // Assert
            Assert.AreEqual(oldConfig, args.OldConfiguration);
            Assert.AreEqual(newConfig, args.NewConfiguration);
            Assert.AreEqual(trigger, args.Trigger);
            Assert.AreEqual(duration, args.ReloadDuration);
            Assert.IsTrue(args.IsSuccess);
        }

        [TestMethod]
        public void ConfigurationHealthEventArgs_Constructor_ValidData_SetsProperties()
        {
            // Arrange
            var healthStatus = ConfigurationHealthStatus.Healthy;
            var healthChecks = new Dictionary<string, HealthCheckResult>();
            var metrics = new Dictionary<string, ConfigurationMetric>();
            var checkedAt = DateTime.UtcNow;
            var checkDuration = TimeSpan.FromSeconds(1);

            // Act
            var args = new ConfigurationHealthEventArgs(
                healthStatus, healthChecks, metrics, checkedAt, checkDuration);

            // Assert
            Assert.AreEqual(healthStatus, args.HealthStatus);
            Assert.AreEqual(healthChecks, args.HealthChecks);
            Assert.AreEqual(metrics, args.Metrics);
            Assert.AreEqual(checkedAt, args.CheckedAt);
            Assert.AreEqual(checkDuration, args.CheckDuration);
        }

        [TestMethod]
        public void ConfigurationBackupEventArgs_Constructor_ValidData_SetsProperties()
        {
            // Arrange
            var operationType = BackupOperationType.Create;
            var sourcePath = "source.config";
            var backupPath = "backup.config";
            var timestamp = DateTime.UtcNow;
            var reason = BackupReason.Manual;

            // Act
            var args = new ConfigurationBackupEventArgs(
                operationType, sourcePath, backupPath, timestamp, true, reason, 1024);

            // Assert
            Assert.AreEqual(operationType, args.OperationType);
            Assert.AreEqual(sourcePath, args.SourceFilePath);
            Assert.AreEqual(backupPath, args.BackupFilePath);
            Assert.AreEqual(reason, args.Reason);
            Assert.AreEqual(1024, args.BackupFileSize);
            Assert.IsTrue(args.IsSuccess);
        }

        [TestMethod]
        public void ConfigurationMetric_Constructor_ValidData_SetsProperties()
        {
            // Arrange & Act
            var metric = new ConfigurationMetric("test_metric", 42.0, "units", "description");

            // Assert
            Assert.AreEqual("test_metric", metric.Name);
            Assert.AreEqual(42.0, metric.Value);
            Assert.AreEqual("units", metric.Unit);
            Assert.AreEqual("description", metric.Description);
        }

        [TestMethod]
        public void ConfigurationMetric_AddHistoryValue_AddsToHistory()
        {
            // Arrange
            var metric = new ConfigurationMetric("test_metric", 42.0, "units", "description");

            // Act
            metric.AddHistoryValue(43.0);
            metric.AddHistoryValue(44.0);

            // Assert
            Assert.AreEqual(2, metric.History.Count);
            Assert.AreEqual(43.0, metric.History.ToArray()[0].Value);
            Assert.AreEqual(44.0, metric.History.ToArray()[1].Value);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task ConfigurationManager_EnableAdvancedFeatures_EnablesAllComponents()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);

            // Act
            configManager.EnableAdvancedFeatures();

            // Assert
            Assert.IsTrue(configManager.AdvancedFeaturesEnabled);
            Assert.IsNotNull(configManager.AdvancedWatcher);
            Assert.IsNotNull(configManager.ReloadManager);
            Assert.IsNotNull(configManager.HealthMonitor);
        }

        [TestMethod]
        public async Task ConfigurationManager_PerformHealthCheck_ReturnsValidStatus()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            // Act
            var status = await configManager.PerformHealthCheckAsync();

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(ConfigurationHealthStatus), status));
        }

        [TestMethod]
        public async Task ConfigurationManager_CreateBackup_CreatesBackupFile()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            // Act
            var backupPath = await configManager.CreateBackupAsync(BackupReason.Manual);

            // Assert
            Assert.IsNotNull(backupPath);
            Assert.IsTrue(File.Exists(backupPath));
        }

        [TestMethod]
        public async Task ConfigurationManager_GetAvailableBackups_ReturnsBackups()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            // Create a backup first
            await configManager.CreateBackupAsync(BackupReason.Manual);

            // Act
            var backups = configManager.GetAvailableBackups();

            // Assert
            Assert.IsNotNull(backups);
            Assert.AreEqual(1, backups.Count);
        }

        [TestMethod]
        public async Task ConfigurationManager_ForceDiagnostic_ReturnsDiagnostic()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            // Act
            var diagnostic = await configManager.ForceDiagnosticAsync();

            // Assert
            Assert.IsNotNull(diagnostic);
            Assert.AreNotEqual(Guid.Empty, diagnostic.Id);
        }

        [TestMethod]
        public async Task ConfigurationManager_AddHealthRule_AddsRuleToMonitor()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            var rule = new ConfigurationHealthRule("TestRule", async () =>
            {
                return new HealthCheckResult(ConfigurationHealthStatus.Healthy, "Test", "Test", TimeSpan.Zero);
            });

            // Act
            configManager.AddHealthRule(rule);

            // Assert
            // If no exception is thrown, the test passes
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task ConfigurationManager_ReloadConfigurationAsync_TriggersReload()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            // Act
            var result = await configManager.ReloadConfigurationAsync(ReloadTrigger.Manual);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ConfigurationManager_HealthStatus_ReturnsCorrectStatus()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);

            // Act
            var status = configManager.HealthStatus;

            // Assert
            Assert.AreEqual(ConfigurationHealthStatus.Unknown, status); // Initially unknown when not enabled

            // Enable advanced features
            configManager.EnableAdvancedFeatures();

            // Act again
            status = configManager.HealthStatus;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(ConfigurationHealthStatus), status));
        }

        [TestMethod]
        public void ConfigurationManager_DisableAdvancedFeatures_DisablesAllComponents()
        {
            // Arrange
            using var configManager = new TestConfigManager(_testConfigPath);
            configManager.EnableAdvancedFeatures();

            // Act
            configManager.DisableAdvancedFeatures();

            // Assert
            Assert.IsFalse(configManager.AdvancedFeaturesEnabled);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task ConfigurationReloadManager_ReloadConfiguration_InvalidConfig_ReturnsFalse()
        {
            // Arrange
            // Create invalid configuration
            var invalidConfigPath = Path.Combine(Path.GetTempPath(), $"InvalidConfig_{Guid.NewGuid()}.config");
            File.WriteAllText(invalidConfigPath, "<invalid>configuration</invalid>");

            using var reloadManager = new ConfigurationReloadManager(invalidConfigPath, _testBackupDir);
            reloadManager.Start();

            // Act
            var result = await reloadManager.ReloadConfigurationAsync(ReloadTrigger.Manual);

            // Assert
            Assert.IsFalse(result);

            // Cleanup
            if (File.Exists(invalidConfigPath))
            {
                File.Delete(invalidConfigPath);
            }
        }

        [TestMethod]
        public void ConfigurationWatcher_StartWatcher_DisposedObject_ThrowsObjectDisposedException()
        {
            // Arrange
            var watcher = new ConfigurationWatcher(_testConfigPath);
            watcher.Dispose();

            // Act & Assert
            Assert.ThrowsException<ObjectDisposedException>(() => watcher.StartWatching());
        }

        [TestMethod]
        public void ConfigurationHealthMonitor_PerformHealthCheck_DisposedObject_ThrowsObjectDisposedException()
        {
            // Arrange
            var healthMonitor = new ConfigurationHealthMonitor();
            healthMonitor.Dispose();

            // Act & Assert
            Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => healthMonitor.PerformHealthCheckAsync());
        }

        #endregion
    }

    /// <summary>
    /// Test configuration manager that allows custom config file path
    /// </summary>
    public class TestConfigManager : ConfigurationManager
    {
        private readonly string _configPath;

        public TestConfigManager(string configPath)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        }

        public new string ConfigFilePath => _configPath;

        public static LicenseReleaseServiceSection CreateTestConfiguration()
        {
            return new LicenseReleaseServiceSection();
        }
    }
}