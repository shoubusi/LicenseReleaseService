using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement.Deployment;
using LicenseReleaseService.VersionManagement.Models;

namespace LicenseReleaseService.Tests.VersionManagement.Deployment
{
    public class VersionDeploymentManagerTests : IDisposable
    {
        private readonly Mock<ILogger<VersionDeploymentManager>> _mockLogger;
        private readonly Mock<IOptions<VersionDeploymentOptions>> _mockOptions;
        private readonly VersionDeploymentOptions _deploymentOptions;
        private readonly VersionDeploymentManager _deploymentManager;
        private readonly ITestOutputHelper _output;

        public VersionDeploymentManagerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<VersionDeploymentManager>>();
            _deploymentOptions = new VersionDeploymentOptions
            {
                Enabled = true,
                MaxConcurrentDeployments = 2,
                DeploymentTimeout = TimeSpan.FromMinutes(30),
                EnableRollback = true,
                EnableDeploymentHistory = true,
                MaxDeploymentHistory = 100,
                ValidateBeforeDeployment = true,
                CleanupAfterDeployment = true,
                RetryOnFailure = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(30)
            };

            _mockOptions = new Mock<IOptions<VersionDeploymentOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_deploymentOptions);

            _deploymentManager = new VersionDeploymentManager(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task StartAsync_WhenNotStarted_ShouldStartSuccessfully()
        {
            // Arrange
            Assert.False(_deploymentManager.IsRunning);

            // Act
            var result = await _deploymentManager.StartAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(_deploymentManager.IsRunning);
            Assert.Equal("Deployment manager started successfully", result.Message);
            _output.WriteLine($"Deployment manager started: {result.Message}");
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyStarted_ShouldReturnAlreadyRunningMessage()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            Assert.True(_deploymentManager.IsRunning);

            // Act
            var result = await _deploymentManager.StartAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Deployment manager already running", result.Message);
            _output.WriteLine($"Deployment manager already running: {result.Message}");
        }

        [Fact]
        public async Task StopAsync_WhenStarted_ShouldStopSuccessfully()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            Assert.True(_deploymentManager.IsRunning);

            // Act
            var result = await _deploymentManager.StopAsync();

            // Assert
            Assert.True(result.Success);
            Assert.False(_deploymentManager.IsRunning);
            Assert.Equal("Deployment manager stopped successfully", result.Message);
            _output.WriteLine($"Deployment manager stopped: {result.Message}");
        }

        [Fact]
        public async Task StopAsync_WhenNotStarted_ShouldReturnNotRunningMessage()
        {
            // Arrange
            Assert.False(_deploymentManager.IsRunning);

            // Act
            var result = await _deploymentManager.StopAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Deployment manager not running", result.Message);
            _output.WriteLine($"Deployment manager not running: {result.Message}");
        }

        [Fact]
        public async Task DeployVersionAsync_WhenNotStarted_ShouldThrowInvalidOperationException()
        {
            // Arrange
            Assert.False(_deploymentManager.IsRunning);
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _deploymentManager.DeployVersionAsync(version, deploymentOptions));

            Assert.Equal("Deployment manager is not running", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task DeployVersionAsync_WhenStarted_ShouldDeploySuccessfully()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions
            {
                TargetEnvironment = "Production",
                SkipValidation = false,
                ForceDeployment = false,
                RollbackOnFailure = true
            };

            // Act
            var result = await _deploymentManager.DeployVersionAsync(version, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.DeployedVersion);
            Assert.Equal(DeploymentStatus.Succeeded, result.Status);
            Assert.NotNull(result.StartTime);
            Assert.NotNull(result.EndTime);
            Assert.True(result.EndTime >= result.StartTime);
            _output.WriteLine($"Version {version} deployed successfully in {result.Duration}");
        }

        [Fact]
        public async Task DeployVersionAsync_WithNullVersion_ShouldThrowArgumentException()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var deploymentOptions = new VersionDeploymentOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _deploymentManager.DeployVersionAsync(null!, deploymentOptions));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task DeployVersionAsync_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var deploymentOptions = new VersionDeploymentOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _deploymentManager.DeployVersionAsync(string.Empty, deploymentOptions));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task DeployVersionAsync_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "2023";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _deploymentManager.DeployVersionAsync(version, null!));

            Assert.Equal("Value cannot be null. (Parameter 'deploymentOptions')", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task DeployVersionAsync_WhenManagerDisposed_ShouldThrowObjectDisposedException()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            _deploymentManager.Dispose();
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _deploymentManager.DeployVersionAsync(version, deploymentOptions));

            Assert.Equal("Cannot access a disposed object.\r\nObject name: 'VersionDeploymentManager'.", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task RollbackVersionAsync_WithValidVersion_ShouldRollbackSuccessfully()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions();

            // First deploy the version
            await _deploymentManager.DeployVersionAsync(version, deploymentOptions);

            // Act
            var result = await _deploymentManager.RollbackVersionAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.RolledBackVersion);
            Assert.True(result.RollbackSuccessful);
            _output.WriteLine($"Version {version} rolled back successfully in {result.Duration}");
        }

        [Fact]
        public async Task RollbackVersionAsync_WithNullVersion_ShouldThrowArgumentException()
        {
            // Arrange
            await _deploymentManager.StartAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _deploymentManager.RollbackVersionAsync(null!));

            Assert.Equal("Version cannot be null or empty", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task GetDeploymentStatusAsync_WithValidVersion_ShouldReturnStatus()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions();

            // Deploy the version
            await _deploymentManager.DeployVersionAsync(version, deploymentOptions);

            // Act
            var result = await _deploymentManager.GetDeploymentStatusAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.Version);
            Assert.NotNull(result.Status);
            _output.WriteLine($"Deployment status for {version}: {result.Status}");
        }

        [Fact]
        public async Task GetDeploymentStatusAsync_WithInvalidVersion_ShouldReturnNotFound()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "nonexistent";

            // Act
            var result = await _deploymentManager.GetDeploymentStatusAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("No deployment found for version", result.Message);
            _output.WriteLine($"Deployment status for {version}: {result.Message}");
        }

        [Fact]
        public async Task GetDeploymentHistoryAsync_ShouldReturnHistory()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version1 = "2023";
            var version2 = "2024";
            var deploymentOptions = new VersionDeploymentOptions();

            // Deploy multiple versions
            await _deploymentManager.DeployVersionAsync(version1, deploymentOptions);
            await _deploymentManager.DeployVersionAsync(version2, deploymentOptions);

            // Act
            var result = await _deploymentManager.GetDeploymentHistoryAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Deployments);
            Assert.True(result.Deployments.Count >= 2);
            _output.WriteLine($"Deployment history contains {result.Deployments.Count} deployments");

            // Verify deployments are ordered by timestamp (most recent first)
            for (int i = 0; i < result.Deployments.Count - 1; i++)
            {
                Assert.True(result.Deployments[i].StartTime >= result.Deployments[i + 1].StartTime);
            }
        }

        [Fact]
        public async Task GetDeploymentHistoryAsync_WithVersionFilter_ShouldReturnFilteredHistory()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version1 = "2023";
            var version2 = "2024";
            var deploymentOptions = new VersionDeploymentOptions();

            // Deploy multiple versions
            await _deploymentManager.DeployVersionAsync(version1, deploymentOptions);
            await _deploymentManager.DeployVersionAsync(version2, deploymentOptions);

            // Act
            var result = await _deploymentManager.GetDeploymentHistoryAsync(version1);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Deployments);
            Assert.All(result.Deployments, d => Assert.Equal(version1, d.Version));
            _output.WriteLine($"Filtered deployment history for {version1} contains {result.Deployments.Count} deployments");
        }

        [Fact]
        public async Task CancelDeploymentAsync_WithActiveDeployment_ShouldCancelSuccessfully()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions();

            // Start deployment
            var deployTask = _deploymentManager.DeployVersionAsync(version, deploymentOptions);

            // Act - cancel the deployment
            var result = await _deploymentManager.CancelDeploymentAsync(version);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(version, result.CancelledVersion);
            _output.WriteLine($"Deployment cancelled for version {version}");

            // Wait for the deployment task to complete
            await deployTask;
        }

        [Fact]
        public async Task GetDeploymentSummaryAsync_ShouldReturnSummary()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version1 = "2023";
            var version2 = "2024";
            var deploymentOptions = new VersionDeploymentOptions();

            // Deploy multiple versions
            await _deploymentManager.DeployVersionAsync(version1, deploymentOptions);
            await _deploymentManager.DeployVersionAsync(version2, deploymentOptions);

            // Act
            var result = await _deploymentManager.GetDeploymentSummaryAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Summary);
            Assert.True(result.Summary.ContainsKey("total_deployments"));
            Assert.True(result.Summary.ContainsKey("successful_deployments"));
            Assert.True(result.Summary.ContainsKey("failed_deployments"));
            Assert.True(result.Summary.ContainsKey("average_deployment_time_ms"));
            _output.WriteLine($"Deployment summary: {result.Summary["total_deployments"]} total deployments");
        }

        [Fact]
        public async Task ConcurrentDeployments_WithinLimit_ShouldSucceed()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version1 = "2023";
            var version2 = "2024";
            var deploymentOptions = new VersionDeploymentOptions();

            // Act - start multiple concurrent deployments
            var task1 = _deploymentManager.DeployVersionAsync(version1, deploymentOptions);
            var task2 = _deploymentManager.DeployVersionAsync(version2, deploymentOptions);

            // Wait for both to complete
            await Task.WhenAll(task1, task2);

            // Assert
            Assert.True(task1.Result.Success);
            Assert.True(task2.Result.Success);
            _output.WriteLine($"Concurrent deployments succeeded: {version1} and {version2}");
        }

        [Fact]
        public async Task Dispose_WhenCalled_ShouldCleanUpResources()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            Assert.True(_deploymentManager.IsRunning);

            // Act
            _deploymentManager.Dispose();

            // Assert
            // Verify that the manager can no longer be used
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _deploymentManager.StartAsync());

            Assert.Equal("Cannot access a disposed object.\r\nObject name: 'VersionDeploymentManager'.", exception.Message);
            _output.WriteLine("Deployment manager disposed successfully");
        }

        [Fact]
        public async Task EventHandlers_ShouldBeInvokedCorrectly()
        {
            // Arrange
            await _deploymentManager.StartAsync();
            var version = "2023";
            var deploymentOptions = new VersionDeploymentOptions();

            var deploymentStartedInvoked = false;
            var deploymentCompletedInvoked = false;
            var deploymentFailedInvoked = false;
            var deploymentCancelledInvoked = false;

            _deploymentManager.DeploymentStarted += (sender, args) =>
            {
                deploymentStartedInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"DeploymentStarted event invoked for {args.Version}");
            };

            _deploymentManager.DeploymentCompleted += (sender, args) =>
            {
                deploymentCompletedInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"DeploymentCompleted event invoked for {args.Version}");
            };

            _deploymentManager.DeploymentFailed += (sender, args) =>
            {
                deploymentFailedInvoked = true;
                _output.WriteLine($"DeploymentFailed event invoked for {args.Version}");
            };

            _deploymentManager.DeploymentCancelled += (sender, args) =>
            {
                deploymentCancelledInvoked = true;
                Assert.Equal(version, args.Version);
                _output.WriteLine($"DeploymentCancelled event invoked for {args.Version}");
            };

            // Act
            await _deploymentManager.DeployVersionAsync(version, deploymentOptions);

            // Assert
            Assert.True(deploymentStartedInvoked, "DeploymentStarted event should be invoked");
            Assert.True(deploymentCompletedInvoked, "DeploymentCompleted event should be invoked");
            Assert.False(deploymentFailedInvoked, "DeploymentFailed event should not be invoked for successful deployment");
            Assert.False(deploymentCancelledInvoked, "DeploymentCancelled event should not be invoked for successful deployment");
            _output.WriteLine("All expected events were invoked correctly");
        }

        public void Dispose()
        {
            _deploymentManager?.Dispose();
        }
    }
}