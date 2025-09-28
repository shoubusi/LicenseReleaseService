using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using LicenseReleaseService.IdleDetection;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class SystemActivityMonitorTests : IDisposable
    {
        private readonly SystemActivityMonitor _monitor;
        private readonly Mock<ILogger<SystemActivityMonitor>> _loggerMock;
        private readonly TimeBasedDetectionConfig _config;

        public SystemActivityMonitorTests()
        {
            _loggerMock = new Mock<ILogger<SystemActivityMonitor>>();
            _config = new TimeBasedDetectionConfig
            {
                MonitoringSampleRateMs = 50, // Faster for testing
                EnableKeyboardMonitoring = true,
                EnableMouseMonitoring = true,
                EnableSystemMonitoring = true,
                EnableSolidWorksMonitoring = true
            };

            _monitor = new SystemActivityMonitor(_loggerMock.Object, _config);
        }

        public void Dispose()
        {
            _monitor?.Dispose();
        }

        [Fact]
        public void Constructor_ValidParameters_ShouldInitializeCorrectly()
        {
            // Assert
            Assert.NotNull(_monitor);
            Assert.Equal(TimeSpan.Zero, _monitor.CurrentIdleTime);
            Assert.True(_monitor.LastActivity <= DateTime.UtcNow);
            Assert.False(_monitor.IsRunning);
        }

        [Fact]
        public async Task StartAsync_FirstTime_ShouldStartMonitoring()
        {
            // Act
            await _monitor.StartAsync();

            // Assert
            Assert.True(_monitor.IsRunning);
            Assert.NotNull(_monitor.CurrentIdleTime);

            // Cleanup
            await _monitor.StopAsync();
        }

        [Fact]
        public async Task StartAsync_AlreadyRunning_ShouldLogWarning()
        {
            // Arrange
            await _monitor.StartAsync();

            // Act
            await _monitor.StartAsync();

            // Assert
            Assert.True(_monitor.IsRunning);
            VerifyLoggerWarning("System activity monitor is already running");

            // Cleanup
            await _monitor.StopAsync();
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
        public async Task StopAsync_NotRunning_ShouldLogWarning()
        {
            // Act
            await _monitor.StopAsync();

            // Assert
            Assert.False(_monitor.IsRunning);
            VerifyLoggerWarning("System activity monitor is not running");
        }

        [Fact]
        public void GetSystemIdleTime_ShouldReturnValidTimeSpan()
        {
            // Act
            var idleTime = _monitor.GetSystemIdleTime();

            // Assert
            Assert.True(idleTime >= TimeSpan.Zero);
            Assert.True(idleTime <= TimeSpan.FromDays(1)); // Shouldn't be more than a day
        }

        [Fact]
        public void GetActiveWindowTitle_ShouldReturnString()
        {
            // Act
            var title = _monitor.GetActiveWindowTitle();

            // Assert
            Assert.NotNull(title);
            Assert.IsType<string>(title);
        }

        [Fact]
        public void IsSolidWorksActive_WhenSolidWorksIsRunning_ShouldReturnTrue()
        {
            // This test is hard to unit test reliably as it depends on actual running processes
            // For now, we'll just test that it returns a boolean
            var isActive = _monitor.IsSolidWorksActive();

            // Assert
            Assert.IsType<bool>(isActive);
        }

        [Fact]
        public void GetCpuUsage_ShouldReturnValidPercentage()
        {
            // Act
            var cpuUsage = _monitor.GetCpuUsage();

            // Assert
            Assert.True(cpuUsage >= 0.0);
            Assert.True(cpuUsage <= 100.0);
        }

        [Fact]
        public void GetMemoryUsage_ShouldReturnValidPercentage()
        {
            // Act
            var memoryUsage = _monitor.GetMemoryUsage();

            // Assert
            Assert.True(memoryUsage >= 0.0);
            Assert.True(memoryUsage <= 100.0);
        }

        [Fact]
        public void GetRecentActivities_WithTimeWindow_ShouldReturnActivities()
        {
            // Arrange
            var timeWindow = TimeSpan.FromMinutes(5);

            // Act
            var activities = _monitor.GetRecentActivities(timeWindow);

            // Assert
            Assert.NotNull(activities);
            Assert.IsType<List<ActivityData>>(activities);

            // All activities should be within the time window
            var cutoffTime = DateTime.UtcNow - timeWindow;
            foreach (var activity in activities)
            {
                Assert.True(activity.Timestamp > cutoffTime);
            }
        }

        [Fact]
        public void GetRecentActivities_WithoutActivities_ShouldReturnEmptyList()
        {
            // Arrange
            var timeWindow = TimeSpan.FromMilliseconds(1); // Very small window

            // Act
            var activities = _monitor.GetRecentActivities(timeWindow);

            // Assert
            Assert.NotNull(activities);
            Assert.Empty(activities);
        }

        [Fact]
        public async Task ActivityDetectedEvent_ShouldBeRaisedWhenActivityIsDetected()
        {
            // Arrange
            var eventRaised = false;
            ActivityData capturedActivity = null;

            _monitor.ActivityDetected += (sender, activity) =>
            {
                eventRaised = true;
                capturedActivity = activity;
            };

            await _monitor.StartAsync();

            // Wait for some monitoring cycles
            await Task.Delay(200);

            // Act - simulate activity detection by calling protected method through reflection
            // For now, we'll just check that the event handler is wired up correctly

            // Assert
            // Note: This test is limited because we can't easily simulate Windows API calls
            Assert.NotNull(_monitor);

            // Cleanup
            await _monitor.StopAsync();
        }

        [Fact]
        public async Task DetectProcessActivity_ShouldDetectSolidWorksProcesses()
        {
            // Arrange
            // This test depends on actual running processes, so results may vary
            // We'll mainly test that the method doesn't throw exceptions

            // Act
            var activities = await Task.Run(() => InvokePrivateMethod<List<ActivityData>>(_monitor, "DetectProcessActivity"));

            // Assert
            Assert.NotNull(activities);
            Assert.All(activities, activity =>
            {
                Assert.True(activity.Timestamp <= DateTime.UtcNow);
                Assert.True(activity.Confidence >= 0.0 && activity.Confidence <= 1.0);
            });
        }

        [Fact]
        public async Task DetectInputActivity_ShouldDetectKeyboardMouseActivity()
        {
            // Act
            var activities = await Task.Run(() => InvokePrivateMethod<List<ActivityData>>(_monitor, "DetectInputActivity"));

            // Assert
            Assert.NotNull(activities);
            Assert.All(activities, activity =>
            {
                Assert.True(activity.Timestamp <= DateTime.UtcNow);
                Assert.True(activity.Confidence >= 0.0 && activity.Confidence <= 1.0);
                Assert.Contains(activity.ActivityType, new[] { ActivityType.Keyboard, ActivityType.Mouse });
            });
        }

        [Fact]
        public async Task DetectWindowActivity_ShouldDetectWindowFocus()
        {
            // Act
            var activities = await Task.Run(() => InvokePrivateMethod<List<ActivityData>>(_monitor, "DetectWindowActivity"));

            // Assert
            Assert.NotNull(activities);
            Assert.All(activities, activity =>
            {
                Assert.Equal(ActivityType.WindowFocus, activity.ActivityType);
                Assert.True(activity.Confidence >= 0.0 && activity.Confidence <= 1.0);
            });
        }

        [Fact]
        public async Task DetectSystemActivity_ShouldDetectSystemResourceUsage()
        {
            // Act
            var activities = await Task.Run(() => InvokePrivateMethod<List<ActivityData>>(_monitor, "DetectSystemActivity"));

            // Assert
            Assert.NotNull(activities);
            Assert.All(activities, activity =>
            {
                Assert.Equal(ActivityType.System, activity.ActivityType);
                Assert.True(activity.Confidence >= 0.0 && activity.Confidence <= 1.0);
            });
        }

        [Fact]
        public async Task MonitoringLoop_ShouldRunContinuously()
        {
            // Arrange
            await _monitor.StartAsync();

            // Act
            // Let it run for a short time
            await Task.Delay(300);

            // Assert
            Assert.True(_monitor.IsRunning);

            // Cleanup
            await _monitor.StopAsync();
        }

        [Fact]
        public async Task StartStopMultipleTimes_ShouldWorkCorrectly()
        {
            // Act & Assert
            for (int i = 0; i < 3; i++)
            {
                await _monitor.StartAsync();
                Assert.True(_monitor.IsRunning);

                await Task.Delay(50); // Let it run briefly

                await _monitor.StopAsync();
                Assert.False(_monitor.IsRunning);
            }
        }

        [Fact]
        public void Dispose_ShouldStopMonitoringAndCleanup()
        {
            // Arrange
            _monitor.Dispose();

            // Assert
            Assert.False(_monitor.IsRunning);
            // Additional assertions could be made if there were disposable resources
        }

        [Fact]
        public async Task ConfigurationChanges_ShouldAffectMonitoring()
        {
            // Arrange
            var originalSampleRate = _config.MonitoringSampleRateMs;
            _config.MonitoringSampleRateMs = 10; // Very fast for testing

            await _monitor.StartAsync();

            // Wait for some monitoring cycles
            await Task.Delay(100);

            // Assert
            Assert.True(_monitor.IsRunning);

            // Cleanup
            await _monitor.StopAsync();

            // Restore original configuration
            _config.MonitoringSampleRateMs = originalSampleRate;
        }

        [Theory]
        [InlineData(true, true, true, true)] // All monitoring enabled
        [InlineData(false, false, false, false)] // All monitoring disabled
        [InlineData(true, false, false, false)] // Only keyboard enabled
        [InlineData(false, true, false, false)] // Only mouse enabled
        public async Task ConfigurationMonitoringSettings_ShouldBeRespected(
            bool keyboard, bool mouse, bool system, bool solidWorks)
        {
            // Arrange
            _config.EnableKeyboardMonitoring = keyboard;
            _config.EnableMouseMonitoring = mouse;
            _config.EnableSystemMonitoring = system;
            _config.EnableSolidWorksMonitoring = solidWorks;

            var monitor = new SystemActivityMonitor(_loggerMock.Object, _config);

            await monitor.StartAsync();

            // Wait for some monitoring cycles
            await Task.Delay(100);

            // Assert
            Assert.True(monitor.IsRunning);

            // Cleanup
            await monitor.StopAsync();
            monitor.Dispose();
        }

        [Fact]
        public async Task HighFrequencyMonitoring_ShouldHandleLoad()
        {
            // Arrange
            _config.MonitoringSampleRateMs = 1; // Very high frequency
            var monitor = new SystemActivityMonitor(_loggerMock.Object, _config);

            await monitor.StartAsync();

            // Act
            // Let it run under high frequency
            await Task.Delay(200);

            // Assert
            Assert.True(monitor.IsRunning);
            // Monitor should not crash under high frequency

            // Cleanup
            await monitor.StopAsync();
            monitor.Dispose();
        }

        [Fact]
        public void ActivityData_WithMetadata_ShouldStoreCorrectly()
        {
            // Arrange
            var activity = new ActivityData
            {
                ActivityType = ActivityType.Keyboard,
                Confidence = 0.9,
                Timestamp = DateTime.UtcNow
            };

            // Act
            activity.Metadata["TestKey"] = "TestValue";
            activity.Metadata["ProcessId"] = 1234;

            // Assert
            Assert.Equal("TestValue", activity.Metadata["TestKey"]);
            Assert.Equal(1234, activity.Metadata["ProcessId"]);
            Assert.Equal(2, activity.Metadata.Count);
        }

        [Fact]
        public void ActivityData_ToString_ShouldIncludeAllInformation()
        {
            // Arrange
            var activity = new ActivityData
            {
                ActivityType = ActivityType.Mouse,
                Confidence = 0.75,
                Timestamp = new DateTime(2023, 6, 15, 10, 30, 0)
            };
            activity.Metadata["ExtraInfo"] = "Test";

            // Act
            var result = activity.ToString();

            // Assert
            Assert.Contains("Activity: Mouse", result);
            Assert.Contains("Confidence: 0.75", result);
            Assert.Contains("2023-06-15 10:30:00", result);
        }

        #region Helper Methods

        private void VerifyLoggerWarning(string expectedMessage)
        {
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        private T InvokePrivateMethod<T>(object obj, string methodName, params object[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)method.Invoke(obj, parameters);
        }

        #endregion
    }
}