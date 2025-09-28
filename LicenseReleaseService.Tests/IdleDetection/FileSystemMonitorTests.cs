using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class FileSystemMonitorTests : IDisposable
    {
        private readonly Mock<ILogger<FileSystemMonitor>> _mockLogger;
        private readonly FileSystemMonitorConfig _config;
        private readonly FileSystemMonitor _monitor;
        private readonly string _testDirectory;
        private readonly string _tempDirectory;

        public FileSystemMonitorTests()
        {
            _mockLogger = new Mock<ILogger<FileSystemMonitor>>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileSystemMonitorTest_" + Guid.NewGuid().ToString());
            _tempDirectory = Path.Combine(Path.GetTempPath(), "TempTest_" + Guid.NewGuid().ToString());

            Directory.CreateDirectory(_testDirectory);
            Directory.CreateDirectory(_tempDirectory);

            _config = new FileSystemMonitorConfig
            {
                MonitoringInterval = 500,
                IncludeSubdirectories = true,
                MaxFileSizeBytes = 1024 * 1024 * 10, // 10MB
                EnableDetailedLogging = true,
                MonitoredExtensions = new List<string> { ".sldprt", ".sldasm", ".slddrw" },
                SolidWorksDirectories = new List<string> { _testDirectory },
                TempDirectories = new List<string> { _tempDirectory },
                EventBufferSize = 100,
                MaxDirectoriesToMonitor = 10,
                EnableNetworkDriveMonitoring = false,
                EnableChangeTracking = true,
                EnableAccessTracking = true
            };

            _monitor = new FileSystemMonitor(_mockLogger.Object, _config);
        }

        public void Dispose()
        {
            _monitor.Dispose();
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, true);
                if (Directory.Exists(_tempDirectory))
                    Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeMonitor()
        {
            // Arrange & Act
            var monitor = new FileSystemMonitor(_mockLogger.Object, _config);

            // Assert
            Assert.NotNull(monitor);
            Assert.False(monitor.IsRunning);
            Assert.NotNull(monitor.Statistics);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FileSystemMonitor(null, _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FileSystemMonitor(_mockLogger.Object, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotRunning_ShouldStartMonitoring()
        {
            // Arrange
            var fileActivityDetected = false;
            _monitor.FileActivityDetected += (s, e) => fileActivityDetected = true;

            // Act
            await _monitor.StartAsync();

            // Assert
            Assert.True(_monitor.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartAgain()
        {
            // Arrange
            await _monitor.StartAsync();

            // Act
            await _monitor.StartAsync();

            // Assert
            Assert.True(_monitor.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopMonitoring()
        {
            // Arrange
            await _monitor.StartAsync();

            // Act
            await _monitor.StopAsync();

            // Assert
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldNotStopAgain()
        {
            // Arrange
            await _monitor.StartAsync();
            await _monitor.StopAsync();

            // Act
            await _monitor.StopAsync();

            // Assert
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public void GetRecentFileActivities_WithNoActivity_ShouldReturnEmptyList()
        {
            // Arrange & Act
            var activities = _monitor.GetRecentFileActivities(TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(activities);
            Assert.Empty(activities);
        }

        [Fact]
        public void GetRecentFileActivities_WithTimeWindow_ShouldReturnActivitiesWithinWindow()
        {
            // Arrange
            var oldActivity = new FileActivityData
            {
                ActivityType = FileActivityType.FileWrite,
                Confidence = 0.8,
                Timestamp = DateTime.UtcNow.AddMinutes(-10)
            };

            var recentActivity = new FileActivityData
            {
                ActivityType = FileActivityType.FileWrite,
                Confidence = 0.9,
                Timestamp = DateTime.UtcNow.AddMinutes(-1)
            };

            // Add activities directly (simulating internal state)
            var method = typeof(FileSystemMonitor).GetMethod("StoreFileActivity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(_monitor, new object[] { oldActivity });
            method.Invoke(_monitor, new object[] { recentActivity });

            // Act
            var activities = _monitor.GetRecentFileActivities(TimeSpan.FromMinutes(5));

            // Assert
            Assert.Single(activities);
            Assert.Equal(recentActivity.Timestamp, activities.First().Timestamp);
        }

        [Fact]
        public void GetProcessFileActivities_WithValidProcess_ShouldReturnProcessActivities()
        {
            // Arrange
            var processId = 1234;
            var processActivity = new FileActivityData
            {
                ActivityType = FileActivityType.FileWrite,
                Confidence = 0.8,
                Timestamp = DateTime.UtcNow,
                ProcessId = processId
            };

            var otherActivity = new FileActivityData
            {
                ActivityType = FileActivityType.FileRead,
                Confidence = 0.7,
                Timestamp = DateTime.UtcNow,
                ProcessId = 5678
            };

            // Add activities directly (simulating internal state)
            var method = typeof(FileSystemMonitor).GetMethod("StoreFileActivity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(_monitor, new object[] { processActivity });
            method.Invoke(_monitor, new object[] { otherActivity });

            // Act
            var activities = _monitor.GetProcessFileActivities(processId, TimeSpan.FromMinutes(1));

            // Assert
            Assert.Single(activities);
            Assert.Equal(processId, activities.First().ProcessId);
        }

        [Fact]
        public void GetProcessFileActivities_WithInvalidProcess_ShouldReturnEmptyList()
        {
            // Arrange & Act
            var activities = _monitor.GetProcessFileActivities(9999, TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(activities);
            Assert.Empty(activities);
        }

        [Fact]
        public void GetMonitoredDirectories_ShouldReturnConfiguredDirectories()
        {
            // Arrange & Act
            var directories = _monitor.GetMonitoredDirectories();

            // Assert
            Assert.NotNull(directories);
            Assert.Contains(_testDirectory, directories);
            Assert.Contains(_tempDirectory, directories);
        }

        [Fact]
        public void GetStatistics_ShouldReturnCurrentStatistics()
        {
            // Arrange & Act
            var stats = _monitor.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("FileSystemMonitor", stats.ComponentName);
            Assert.True(stats.StartTime <= DateTime.UtcNow);
        }

        [Fact]
        public async Task MonitorDirectoriesAsync_WithNonexistentDirectory_ShouldCreateDirectory()
        {
            // Arrange
            var nonexistentDir = Path.Combine(Path.GetTempPath(), "Nonexistent_" + Guid.NewGuid().ToString());
            _config.SolidWorksDirectories.Add(nonexistentDir);

            var monitor = new FileSystemMonitor(_mockLogger.Object, _config);

            // Act
            await monitor.StartAsync();

            // Assert
            Assert.True(Directory.Exists(nonexistentDir));
        }

        [Fact]
        public void CalculateActivityConfidence_WithSolidWorksFile_ShouldReturnHighConfidence()
        {
            // Arrange
            var fileName = "test.sldprt";
            var fileChangeType = FileChangeType.Changed;
            var fileSize = 1024L;

            // Act
            var confidence = _monitor.CalculateActivityConfidence(fileName, fileChangeType, fileSize);

            // Assert
            Assert.True(confidence > 0.7);
        }

        [Fact]
        public void CalculateActivityConfidence_WithNonSolidWorksFile_ShouldReturnLowConfidence()
        {
            // Arrange
            var fileName = "test.txt";
            var fileChangeType = FileChangeType.Changed;
            var fileSize = 1024L;

            // Act
            var confidence = _monitor.CalculateActivityConfidence(fileName, fileChangeType, fileSize);

            // Assert
            Assert.True(confidence < 0.3);
        }

        [Fact]
        public void CalculateActivityConfidence_WithLargeFile_ShouldReturnMediumConfidence()
        {
            // Arrange
            var fileName = "largefile.tmp";
            var fileChangeType = FileChangeType.Created;
            var fileSize = 5 * 1024 * 1024; // 5MB

            // Act
            var confidence = _monitor.CalculateActivityConfidence(fileName, fileChangeType, fileSize);

            // Assert
            Assert.True(confidence >= 0.3 && confidence < 0.7);
        }

        [Fact]
        public void IsSolidWorksFile_WithSolidWorksExtension_ShouldReturnTrue()
        {
            // Arrange
            var fileName = "assembly.sldasm";

            // Act
            var result = _monitor.IsSolidWorksFile(fileName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsSolidWorksFile_WithNonSolidWorksExtension_ShouldReturnFalse()
        {
            // Arrange
            var fileName = "document.docx";

            // Act
            var result = _monitor.IsSolidWorksFile(fileName);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSolidWorksFile_WithTempFile_ShouldReturnTrue()
        {
            // Arrange
            var fileName = "temp_~1.sldprt.tmp";

            // Act
            var result = _monitor.IsSolidWorksFile(fileName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void GetFileActivityType_WithCreatedFile_ShouldReturnFileCreate()
        {
            // Arrange
            var changeType = FileChangeType.Created;

            // Act
            var activityType = _monitor.GetFileActivityType(changeType);

            // Assert
            Assert.Equal(FileActivityType.FileCreate, activityType);
        }

        [Fact]
        public void GetFileActivityType_WithChangedFile_ShouldReturnFileWrite()
        {
            // Arrange
            var changeType = FileChangeType.Changed;

            // Act
            var activityType = _monitor.GetFileActivityType(changeType);

            // Assert
            Assert.Equal(FileActivityType.FileWrite, activityType);
        }

        [Fact]
        public void GetFileActivityType_WithDeletedFile_ShouldReturnFileDelete()
        {
            // Arrange
            var changeType = FileChangeType.Deleted;

            // Act
            var activityType = _monitor.GetFileActivityType(changeType);

            // Assert
            Assert.Equal(FileActivityType.FileDelete, activityType);
        }

        [Fact]
        public void GetFileActivityType_WithRenamedFile_ShouldReturnFileRename()
        {
            // Arrange
            var changeType = FileChangeType.Renamed;

            // Act
            var activityType = _monitor.GetFileActivityType(changeType);

            // Assert
            Assert.Equal(FileActivityType.FileRename, activityType);
        }

        [Fact]
        public void Dispose_ShouldStopMonitoringAndDisposeResources()
        {
            // Arrange
            _monitor.StartAsync().Wait();

            // Act
            _monitor.Dispose();

            // Assert
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public async Task OnFileSystemChanged_WithSolidWorksFile_ShouldRaiseFileActivityDetected()
        {
            // Arrange
            await _monitor.StartAsync();
            var eventRaised = false;
            FileActivityData capturedData = null;

            _monitor.FileActivityDetected += (s, e) =>
            {
                eventRaised = true;
                capturedData = e;
            };

            var testFile = Path.Combine(_testDirectory, "test.sldprt");
            File.WriteAllText(testFile, "test content");

            // Wait for file system event to be processed
            await Task.Delay(1000);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(capturedData);
            Assert.Equal(FileActivityType.FileCreate, capturedData.ActivityType);
            Assert.True(capturedData.Confidence > 0.7);
        }

        [Fact]
        public void Configuration_ShouldValidateSettings()
        {
            // Arrange
            var invalidConfig = new FileSystemMonitorConfig
            {
                MonitoringInterval = -1,
                MaxFileSizeBytes = -1
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => invalidConfig.Validate());
        }

        [Fact]
        public void Configuration_WithValidSettings_ShouldNotThrow()
        {
            // Arrange
            var validConfig = new FileSystemMonitorConfig
            {
                MonitoringInterval = 1000,
                MaxFileSizeBytes = 1024 * 1024,
                MonitoredExtensions = new List<string> { ".sldprt" },
                SolidWorksDirectories = new List<string> { _testDirectory }
            };

            // Act & Assert
            Assert.DoesNotThrow(() => validConfig.Validate());
        }
    }
}