using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement.Monitoring;

namespace LicenseReleaseService.Tests.VersionManagement.Monitoring
{
    public class VersionMonitoringServiceTests : IDisposable
    {
        private readonly Mock<ILogger<VersionMonitoringService>> _mockLogger;
        private readonly Mock<IOptions<VersionMonitoringOptions>> _mockOptions;
        private readonly VersionMonitoringOptions _monitoringOptions;
        private readonly VersionMonitoringService _monitoringService;
        private readonly ITestOutputHelper _output;

        public VersionMonitoringServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<VersionMonitoringService>>();
            _monitoringOptions = new VersionMonitoringOptions
            {
                Enabled = true,
                HealthCheckInterval = TimeSpan.FromSeconds(30),
                MetricsCollectionInterval = TimeSpan.FromSeconds(60),
                AlertCheckInterval = TimeSpan.FromMinutes(5),
                EnableHealthMonitoring = true,
                EnableMetricsCollection = true,
                EnableAlertManagement = true,
                EnableEventProcessing = true,
                MaxConcurrentHealthChecks = 5,
                MaxHealthCheckRetries = 3,
                HealthCheckTimeout = TimeSpan.FromMinutes(2),
                AlertSuppressionDuration = TimeSpan.FromMinutes(15),
                MonitoringDataRetentionPeriod = TimeSpan.FromDays(30)
            };

            _mockOptions = new Mock<IOptions<VersionMonitoringOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_monitoringOptions);

            _monitoringService = new VersionMonitoringService(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task StartMonitoringAsync_WhenNotStarted_ShouldStartSuccessfully()
        {
            // Arrange
            Assert.False(_monitoringService.IsMonitoring);

            // Act
            var result = await _monitoringService.StartMonitoringAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(_monitoringService.IsMonitoring);
            Assert.Equal("Monitoring service started successfully", result.Message);
            _output.WriteLine($"Monitoring service started: {result.Message}");
        }

        [Fact]
        public async Task StartMonitoringAsync_WhenAlreadyStarted_ShouldReturnAlreadyRunningMessage()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            Assert.True(_monitoringService.IsMonitoring);

            // Act
            var result = await _monitoringService.StartMonitoringAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Monitoring service already running", result.Message);
            _output.WriteLine($"Monitoring service already running: {result.Message}");
        }

        [Fact]
        public async Task StopMonitoringAsync_WhenStarted_ShouldStopSuccessfully()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            Assert.True(_monitoringService.IsMonitoring);

            // Act
            var result = await _monitoringService.StopMonitoringAsync();

            // Assert
            Assert.True(result.Success);
            Assert.False(_monitoringService.IsMonitoring);
            Assert.Equal("Monitoring service stopped successfully", result.Message);
            _output.WriteLine($"Monitoring service stopped: {result.Message}");
        }

        [Fact]
        public async Task StopMonitoringAsync_WhenNotStarted_ShouldReturnNotRunningMessage()
        {
            // Arrange
            Assert.False(_monitoringService.IsMonitoring);

            // Act
            var result = await _monitoringService.StopMonitoringAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Monitoring service not running", result.Message);
            _output.WriteLine($"Monitoring service not running: {result.Message}");
        }

        [Fact]
        public async Task AddVersionForMonitoringAsync_WithValidVersion_ShouldSucceed()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Act
            var result = await _monitoringService.AddVersionForMonitoringAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.Version);
            Assert.True(result.IsMonitoringEnabled);
            _output.WriteLine($"Version {version} added to monitoring successfully");
        }

        [Fact]
        public async Task AddVersionForMonitoringAsync_WithNullVersion_ShouldThrowArgumentException()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _monitoringService.AddVersionForMonitoringAsync(null!));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task AddVersionForMonitoringAsync_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _monitoringService.AddVersionForMonitoringAsync(string.Empty));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task AddVersionForMonitoringAsync_WhenNotStarted_ShouldThrowInvalidOperationException()
        {
            // Arrange
            Assert.False(_monitoringService.IsMonitoring);
            var version = "2023";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _monitoringService.AddVersionForMonitoringAsync(version));

            Assert.Equal("Monitoring service is not running", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task RemoveVersionFromMonitoringAsync_WithValidVersion_ShouldSucceed()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Add version first
            await _monitoringService.AddVersionForMonitoringAsync(version);

            // Act
            var result = await _monitoringService.RemoveVersionFromMonitoringAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.Version);
            Assert.False(result.IsMonitoringEnabled);
            _output.WriteLine($"Version {version} removed from monitoring successfully");
        }

        [Fact]
        public async Task RemoveVersionFromMonitoringAsync_WithNonexistentVersion_ShouldReturnNotFound()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "nonexistent";

            // Act
            var result = await _monitoringService.RemoveVersionFromMonitoringAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Version is not being monitored", result.Message);
            _output.WriteLine($"Non-existent version removal result: {result.Message}");
        }

        [Fact]
        public async Task GetVersionHealthStatusAsync_WithValidVersion_ShouldReturnStatus()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Add version for monitoring
            await _monitoringService.AddVersionForMonitoringAsync(version);

            // Act
            var result = await _monitoringService.GetVersionHealthStatusAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.Version);
            Assert.NotNull(result.HealthStatus);
            _output.WriteLine($"Health status for {version}: {result.HealthStatus}");
        }

        [Fact]
        public async Task GetVersionHealthStatusAsync_WithNonexistentVersion_ShouldReturnNotFound()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "nonexistent";

            // Act
            var result = await _monitoringService.GetVersionHealthStatusAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Version is not being monitored", result.Message);
            _output.WriteLine($"Non-existent version health status: {result.Message}");
        }

        [Fact]
        public async Task GetMonitoredVersionsAsync_WithMultipleVersions_ShouldReturnAllVersions()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version1 = "2023";
            var version2 = "2024";

            // Add multiple versions
            await _monitoringService.AddVersionForMonitoringAsync(version1);
            await _monitoringService.AddVersionForMonitoringAsync(version2);

            // Act
            var result = await _monitoringService.GetMonitoredVersionsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Versions);
            Assert.True(result.Versions.Count >= 2);
            Assert.Contains(version1, result.Versions);
            Assert.Contains(version2, result.Versions);
            _output.WriteLine($"Retrieved {result.Versions.Count} monitored versions");
        }

        [Fact]
        public async Task GetMonitoredVersionsAsync_WithNoVersions_ShouldReturnEmptyList()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();

            // Act
            var result = await _monitoringService.GetMonitoredVersionsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Versions);
            Assert.Empty(result.Versions);
            _output.WriteLine("No versions being monitored as expected");
        }

        [Fact]
        public async Task GetMonitoringSummaryAsync_ShouldReturnSummary()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version1 = "2023";
            var version2 = "2024";

            // Add versions
            await _monitoringService.AddVersionForMonitoringAsync(version1);
            await _monitoringService.AddVersionForMonitoringAsync(version2);

            // Act
            var result = await _monitoringService.GetMonitoringSummaryAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Summary);
            Assert.True(result.Summary.ContainsKey("total_versions"));
            Assert.True(result.Summary.ContainsKey("healthy_versions"));
            Assert.True(result.Summary.ContainsKey("unhealthy_versions"));
            Assert.True(result.Summary.ContainsKey("warning_versions"));
            Assert.True(result.Summary.ContainsKey("monitoring_uptime_seconds"));
            _output.WriteLine($"Monitoring summary: {result.Summary["total_versions"]} total versions");
        }

        [Fact]
        public async Task ExecuteHealthCheckAsync_WithValidVersion_ShouldExecuteSuccessfully()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Add version for monitoring
            await _monitoringService.AddVersionForMonitoringAsync(version);

            // Act
            var result = await _monitoringService.ExecuteHealthCheckAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.Version);
            Assert.NotNull(result.HealthStatus);
            _output.WriteLine($"Health check executed for {version}: {result.HealthStatus}");
        }

        [Fact]
        public async Task ExecuteHealthCheckAsync_WithNonexistentVersion_ShouldReturnNotFound()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "nonexistent";

            // Act
            var result = await _monitoringService.ExecuteHealthCheckAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Version is not being monitored", result.Message);
            _output.WriteLine($"Non-existent version health check result: {result.Message}");
        }

        [Fact]
        public async Task GetMonitoringConfigurationAsync_ShouldReturnConfiguration()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();

            // Act
            var result = await _monitoringService.GetMonitoringConfigurationAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Configuration);
            Assert.True(result.Configuration.ContainsKey("enabled"));
            Assert.True(result.Configuration.ContainsKey("health_check_interval_seconds"));
            Assert.True(result.Configuration.ContainsKey("metrics_collection_interval_seconds"));
            Assert.True(result.Configuration.ContainsKey("alert_check_interval_seconds"));
            _output.WriteLine($"Monitoring configuration retrieved with {result.Configuration.Count} settings");
        }

        [Fact]
        public async Task UpdateMonitoringConfigurationAsync_WithValidConfiguration_ShouldSucceed()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var newConfiguration = new Dictionary<string, object>
            {
                ["health_check_interval_seconds"] = 60,
                ["metrics_collection_interval_seconds"] = 120,
                ["alert_check_interval_seconds"] = 300
            };

            // Act
            var result = await _monitoringService.UpdateMonitoringConfigurationAsync(newConfiguration);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.UpdatedConfiguration);
            Assert.Equal(60, result.UpdatedConfiguration["health_check_interval_seconds"]);
            Assert.Equal(120, result.UpdatedConfiguration["metrics_collection_interval_seconds"]);
            Assert.Equal(300, result.UpdatedConfiguration["alert_check_interval_seconds"]);
            _output.WriteLine("Monitoring configuration updated successfully");
        }

        [Fact]
        public async Task UpdateMonitoringConfigurationAsync_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _monitoringService.UpdateMonitoringConfigurationAsync(null!));

            Assert.Equal("Value cannot be null. (Parameter 'configuration')", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task GetMonitoringEventsAsync_WithEvents_ShouldReturnEvents()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Add version and trigger some events
            await _monitoringService.AddVersionForMonitoringAsync(version);
            await _monitoringService.ExecuteHealthCheckAsync(version);

            // Act
            var result = await _monitoringService.GetMonitoringEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Events);
            _output.WriteLine($"Retrieved {result.Events.Count} monitoring events");

            // Verify events are ordered by timestamp (most recent first)
            for (int i = 0; i < result.Events.Count - 1; i++)
            {
                Assert.True(result.Events[i].Timestamp >= result.Events[i + 1].Timestamp);
            }
        }

        [Fact]
        public async Task GetMonitoringEventsAsync_WithVersionFilter_ShouldReturnFilteredEvents()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version1 = "2023";
            var version2 = "2024";

            // Add versions and trigger events
            await _monitoringService.AddVersionForMonitoringAsync(version1);
            await _monitoringService.AddVersionForMonitoringAsync(version2);
            await _monitoringService.ExecuteHealthCheckAsync(version1);
            await _monitoringService.ExecuteHealthCheckAsync(version2);

            // Act
            var result = await _monitoringService.GetMonitoringEventsAsync(version1);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Events);
            Assert.All(result.Events, e => Assert.Equal(version1, e.Version));
            _output.WriteLine($"Filtered events for {version1}: {result.Events.Count} events");
        }

        [Fact]
        public async Task GetMonitoringEventsAsync_WithTimeRange_ShouldReturnFilteredEvents()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Add version and trigger events
            await _monitoringService.AddVersionForMonitoringAsync(version);
            await _monitoringService.ExecuteHealthCheckAsync(version);

            var startTime = DateTime.UtcNow.AddMinutes(-1);
            var endTime = DateTime.UtcNow.AddMinutes(1);

            // Act
            var result = await _monitoringService.GetMonitoringEventsAsync(null, startTime, endTime);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Events);
            Assert.All(result.Events, e =>
            {
                Assert.True(e.Timestamp >= startTime);
                Assert.True(e.Timestamp <= endTime);
            });
            _output.WriteLine($"Time-range filtered events: {result.Events.Count} events");
        }

        [Fact]
        public async Task ClearMonitoringEventsAsync_WithVersionFilter_ShouldClearVersionEvents()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version1 = "2023";
            var version2 = "2024";

            // Add versions and trigger events
            await _monitoringService.AddVersionForMonitoringAsync(version1);
            await _monitoringService.AddVersionForMonitoringAsync(version2);
            await _monitoringService.ExecuteHealthCheckAsync(version1);
            await _monitoringService.ExecuteHealthCheckAsync(version2);

            // Get initial event count
            var initialEvents = await _monitoringService.GetMonitoringEventsAsync();
            var initialCount = initialEvents.Events.Count;

            // Act
            var result = await _monitoringService.ClearMonitoringEventsAsync(version1);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.EventsCleared > 0);

            // Verify events for version1 are cleared
            var remainingEvents = await _monitoringService.GetMonitoringEventsAsync(version1);
            Assert.Empty(remainingEvents.Events);

            // Verify events for version2 still exist
            var version2Events = await _monitoringService.GetMonitoringEventsAsync(version2);
            Assert.True(version2Events.Events.Count > 0);

            _output.WriteLine($"Cleared {result.EventsCleared} events for {version1}");
        }

        [Fact]
        public async Task ClearMonitoringEventsAsync_WithoutVersionFilter_ShouldClearAllEvents()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version1 = "2023";
            var version2 = "2024";

            // Add versions and trigger events
            await _monitoringService.AddVersionForMonitoringAsync(version1);
            await _monitoringService.AddVersionForMonitoringAsync(version2);
            await _monitoringService.ExecuteHealthCheckAsync(version1);
            await _monitoringService.ExecuteHealthCheckAsync(version2);

            // Act
            var result = await _monitoringService.ClearMonitoringEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(result.EventsCleared > 0);

            // Verify all events are cleared
            var remainingEvents = await _monitoringService.GetMonitoringEventsAsync();
            Assert.Empty(remainingEvents.Events);

            _output.WriteLine($"Cleared all {result.EventsCleared} events");
        }

        [Fact]
        public async Task EventHandlers_ShouldBeInvokedCorrectly()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            var monitoringStartedInvoked = false;
            var monitoringStoppedInvoked = false;
            var versionAddedInvoked = false;
            var versionRemovedInvoked = false;
            var healthCheckCompletedInvoked = false;
            var monitoringErrorInvoked = false;

            _monitoringService.MonitoringStarted += (sender, args) =>
            {
                monitoringStartedInvoked = true;
                _output.WriteLine("MonitoringStarted event invoked");
            };

            _monitoringService.MonitoringStopped += (sender, args) =>
            {
                monitoringStoppedInvoked = true;
                _output.WriteLine("MonitoringStopped event invoked");
            };

            _monitoringService.VersionAdded += (sender, args) =>
            {
                versionAddedInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"VersionAdded event invoked for {args.Version}");
            };

            _monitoringService.VersionRemoved += (sender, args) =>
            {
                versionRemovedInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"VersionRemoved event invoked for {args.Version}");
            };

            _monitoringService.HealthCheckCompleted += (sender, args) =>
            {
                healthCheckCompletedInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"HealthCheckCompleted event invoked for {args.Version}");
            };

            _monitoringService.MonitoringError += (sender, args) =>
            {
                monitoringErrorInvoked = true;
                _output.WriteLine($"MonitoringError event invoked: {args.Exception.Message}");
            };

            // Act
            await _monitoringService.AddVersionForMonitoringAsync(version);
            await _monitoringService.ExecuteHealthCheckAsync(version);
            await _monitoringService.RemoveVersionFromMonitoringAsync(version);
            await _monitoringService.StopMonitoringAsync();

            // Assert
            Assert.True(monitoringStartedInvoked, "MonitoringStarted event should be invoked");
            Assert.True(monitoringStoppedInvoked, "MonitoringStopped event should be invoked");
            Assert.True(versionAddedInvoked, "VersionAdded event should be invoked");
            Assert.True(versionRemovedInvoked, "VersionRemoved event should be invoked");
            Assert.True(healthCheckCompletedInvoked, "HealthCheckCompleted event should be invoked");
            Assert.False(monitoringErrorInvoked, "MonitoringError event should not be invoked for successful operations");
            _output.WriteLine("All expected events were invoked correctly");
        }

        [Fact]
        public async Task Dispose_WhenCalled_ShouldCleanUpResources()
        {
            // Arrange
            await _monitoringService.StartMonitoringAsync();
            var version = "2023";

            // Add version for monitoring
            await _monitoringService.AddVersionForMonitoringAsync(version);
            Assert.True(_monitoringService.IsMonitoring);

            // Act
            _monitoringService.Dispose();

            // Assert
            // Verify that the service can no longer be used
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _monitoringService.StartMonitoringAsync());

            Assert.Equal("Cannot access a disposed object.\r\nObject name: 'VersionMonitoringService'.", exception.Message);
            _output.WriteLine("Monitoring service disposed successfully");
        }

        public void Dispose()
        {
            _monitoringService?.Dispose();
        }
    }
}