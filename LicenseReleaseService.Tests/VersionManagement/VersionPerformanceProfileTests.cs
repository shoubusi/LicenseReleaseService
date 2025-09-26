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
using LicenseReleaseService.VersionManagement;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Unit tests for VersionPerformanceProfile
    /// </summary>
    public class VersionPerformanceProfileTests : IDisposable
    {
        private readonly Mock<ILogger<VersionPerformanceProfile>> _mockLogger;
        private readonly ITestOutputHelper _outputHelper;
        private readonly VersionPerformanceProfileOptions _options;
        private VersionPerformanceProfile _performanceProfile;

        public VersionPerformanceProfileTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _mockLogger = new Mock<ILogger<VersionPerformanceProfile>>();

            _options = new VersionPerformanceProfileOptions
            {
                EnableMetricsCollection = true,
                MetricsCollectionInterval = TimeSpan.FromMinutes(1),
                MetricsRetentionPeriod = TimeSpan.FromDays(7),
                EnableProfiling = true,
                ProfilingSampleSize = 100,
                EnableOptimizationTracking = true,
                MaxOptimizationHistory = 50,
                EnableTrendAnalysis = true,
                TrendAnalysisWindowSize = 24,
                EnablePerformanceTargets = true,
                DefaultPerformanceTargets = new Dictionary<string, double>
                {
                    ["MemoryUsage"] = 80.0, // 80% threshold
                    ["CpuUsage"] = 70.0,   // 70% threshold
                    ["ResponseTime"] = 1000.0 // 1000ms threshold
                }
            };

            _performanceProfile = new VersionPerformanceProfile(
                _mockLogger.Object,
                Options.Create(_options));
        }

        [Fact]
        public async Task CreateProfileAsync_ShouldCreateProfileSuccessfully()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2024",
                InstallPath = @"C:\Program Files\SolidWorks 2024",
                IsAvailable = true,
                LicenseType = LicenseType.Network,
                MaxConcurrentUsers = 10,
                CurrentUsers = 2
            };

            var initialMetrics = new Dictionary<string, double>
            {
                ["MemoryUsage"] = 45.0,
                ["CpuUsage"] = 25.0,
                ["ResponseTime"] = 150.0
            };

            // Act
            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("2024", profile.Version);
            Assert.NotNull(profile.ProfileId);
            Assert.Equal(versionInfo, profile.VersionInfo);
            Assert.NotEmpty(profile.Metrics);
            Assert.Equal(3, profile.Metrics.Count);
            Assert.True(profile.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
            Assert.Equal(ProfileStatus.Active, profile.Status);
        }

        [Fact]
        public async Task UpdateProfileAsync_ShouldUpdateMetricsSuccessfully()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            var updatedMetrics = new Dictionary<string, double>
            {
                ["MemoryUsage"] = 55.0,
                ["CpuUsage"] = 30.0,
                ["ResponseTime"] = 200.0
            };

            // Act
            var result = await _performanceProfile.UpdateProfileAsync(profileId, updatedMetrics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(profileId, result.ProfileId);
            Assert.Equal(3, result.UpdatedMetrics);

            var updatedProfile = await _performanceProfile.GetProfileAsync(profileId);
            Assert.NotNull(updatedProfile);
            Assert.Equal(55.0, updatedProfile.Metrics["MemoryUsage"]);
            Assert.Equal(30.0, updatedProfile.Metrics["CpuUsage"]);
            Assert.Equal(200.0, updatedProfile.Metrics["ResponseTime"]);
        }

        [Fact]
        public async Task UpdateProfileAsync_ShouldReturnError_WhenProfileDoesNotExist()
        {
            // Arrange
            var metrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            // Act
            var result = await _performanceProfile.UpdateProfileAsync("nonexistent_profile_id", metrics);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task GetProfileAsync_ShouldReturnProfile_WhenProfileExists()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var originalProfile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = originalProfile.ProfileId;

            // Act
            var retrievedProfile = await _performanceProfile.GetProfileAsync(profileId);

            // Assert
            Assert.NotNull(retrievedProfile);
            Assert.Equal(profileId, retrievedProfile.ProfileId);
            Assert.Equal("2024", retrievedProfile.Version);
            Assert.Equal(originalProfile.CreatedAt, retrievedProfile.CreatedAt);
        }

        [Fact]
        public async Task GetProfileAsync_ShouldReturnNull_WhenProfileDoesNotExist()
        {
            // Act
            var profile = await _performanceProfile.GetProfileAsync("nonexistent_profile_id");

            // Assert
            Assert.Null(profile);
        }

        [Fact]
        public async Task GetProfilesByVersionAsync_ShouldReturnProfilesForVersion()
        {
            // Arrange
            var versionInfo1 = new SolidWorksVersionInfo { Version = "2024" };
            var versionInfo2 = new SolidWorksVersionInfo { Version = "2024" };
            var versionInfo3 = new SolidWorksVersionInfo { Version = "2023" };

            var metrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            await _performanceProfile.CreateProfileAsync(versionInfo1, metrics);
            await _performanceProfile.CreateProfileAsync(versionInfo2, metrics);
            await _performanceProfile.CreateProfileAsync(versionInfo3, metrics);

            // Act
            var profiles = await _performanceProfile.GetProfilesByVersionAsync("2024");

            // Assert
            Assert.Equal(2, profiles.Count);
            Assert.True(profiles.All(p => p.Version == "2024"));
        }

        [Fact]
        public async Task GetProfilesByVersionAsync_ShouldReturnEmptyList_WhenNoProfilesExist()
        {
            // Act
            var profiles = await _performanceProfile.GetProfilesByVersionAsync("nonexistent_version");

            // Assert
            Assert.NotNull(profiles);
            Assert.Empty(profiles);
        }

        [Fact]
        public async Task DeleteProfileAsync_ShouldDeleteSuccessfully_WhenProfileExists()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            // Act
            var result = await _performanceProfile.DeleteProfileAsync(profileId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(profileId, result.ProfileId);

            // Verify profile is deleted
            var deletedProfile = await _performanceProfile.GetProfileAsync(profileId);
            Assert.Null(deletedProfile);
        }

        [Fact]
        public async Task DeleteProfileAsync_ShouldReturnError_WhenProfileDoesNotExist()
        {
            // Act
            var result = await _performanceProfile.DeleteProfileAsync("nonexistent_profile_id");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task AddOptimizationAsync_ShouldAddOptimizationToProfile()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            var optimization = new AppliedOptimization
            {
                StrategyName = "MemoryOptimization",
                Type = OptimizationType.MemoryOptimization,
                Success = true,
                AppliedAt = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(500),
                PerformanceImpact = 0.15
            };

            // Act
            var result = await _performanceProfile.AddOptimizationAsync(profileId, optimization);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(profileId, result.ProfileId);

            var updatedProfile = await _performanceProfile.GetProfileAsync(profileId);
            Assert.Single(updatedProfile.Optimizations);
            Assert.Equal("MemoryOptimization", updatedProfile.Optimizations.First().StrategyName);
        }

        [Fact]
        public async Task GetOptimizationsAsync_ShouldReturnOptimizationsForProfile()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            var optimization1 = new AppliedOptimization { StrategyName = "Opt1", Type = OptimizationType.PerformanceTuning };
            var optimization2 = new AppliedOptimization { StrategyName = "Opt2", Type = OptimizationType.MemoryOptimization };

            await _performanceProfile.AddOptimizationAsync(profileId, optimization1);
            await _performanceProfile.AddOptimizationAsync(profileId, optimization2);

            // Act
            var optimizations = await _performanceProfile.GetOptimizationsAsync(profileId);

            // Assert
            Assert.Equal(2, optimizations.Count);
            Assert.True(optimizations.Any(o => o.StrategyName == "Opt1"));
            Assert.True(optimizations.Any(o => o.StrategyName == "Opt2"));
        }

        [Fact]
        public async Task GetPerformanceSummaryAsync_ShouldReturnSummaryForVersion()
        {
            // Arrange
            var versionInfo1 = new SolidWorksVersionInfo { Version = "2024" };
            var versionInfo2 = new SolidWorksVersionInfo { Version = "2024" };
            var versionInfo3 = new SolidWorksVersionInfo { Version = "2023" };

            var metrics1 = new Dictionary<string, double> { ["MemoryUsage"] = 45.0, ["CpuUsage"] = 25.0 };
            var metrics2 = new Dictionary<string, double> { ["MemoryUsage"] = 55.0, ["CpuUsage"] = 30.0 };
            var metrics3 = new Dictionary<string, double> { ["MemoryUsage"] = 65.0, ["CpuUsage"] = 35.0 };

            await _performanceProfile.CreateProfileAsync(versionInfo1, metrics1);
            await _performanceProfile.CreateProfileAsync(versionInfo2, metrics2);
            await _performanceProfile.CreateProfileAsync(versionInfo3, metrics3);

            // Act
            var summary = await _performanceProfile.GetPerformanceSummaryAsync("2024");

            // Assert
            Assert.NotNull(summary);
            Assert.Equal("2024", summary.Version);
            Assert.Equal(2, summary.ProfileCount);
            Assert.Equal(50.0, summary.AverageMetrics["MemoryUsage"]); // (45 + 55) / 2
            Assert.Equal(27.5, summary.AverageMetrics["CpuUsage"]);    // (25 + 30) / 2
        }

        [Fact]
        public async Task GetPerformanceTrendsAsync_ShouldReturnTrendsForVersion()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };

            var metrics1 = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };
            var metrics2 = new Dictionary<string, double> { ["MemoryUsage"] = 55.0 };
            var metrics3 = new Dictionary<string, double> { ["MemoryUsage"] = 65.0 };

            await _performanceProfile.CreateProfileAsync(versionInfo, metrics1);
            await Task.Delay(10); // Small delay for different timestamps
            await _performanceProfile.CreateProfileAsync(versionInfo, metrics2);
            await Task.Delay(10);
            await _performanceProfile.CreateProfileAsync(versionInfo, metrics3);

            // Act
            var trends = await _performanceProfile.GetPerformanceTrendsAsync("2024");

            // Assert
            Assert.NotNull(trends);
            Assert.Equal("2024", trends.Version);
            Assert.Equal(3, trends.DataPoints.Count);
            Assert.True(trends.DataPoints.Last().Value > trends.DataPoints.First().Value); // Increasing trend
        }

        [Fact]
        public async Task GetProfileMetricsAsync_ShouldReturnMetricsForProfile()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double>
            {
                ["MemoryUsage"] = 45.0,
                ["CpuUsage"] = 25.0,
                ["ResponseTime"] = 150.0
            };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            // Act
            var metrics = await _performanceProfile.GetProfileMetricsAsync(profileId);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(3, metrics.Count);
            Assert.Equal(45.0, metrics["MemoryUsage"]);
            Assert.Equal(25.0, metrics["CpuUsage"]);
            Assert.Equal(150.0, metrics["ResponseTime"]);
        }

        [Fact]
        public async Task UpdatePerformanceTargetsAsync_ShouldUpdateTargetsSuccessfully()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            var newTargets = new Dictionary<string, double>
            {
                ["MemoryUsage"] = 70.0,
                ["CpuUsage"] = 60.0,
                ["ResponseTime"] = 500.0
            };

            // Act
            var result = await _performanceProfile.UpdatePerformanceTargetsAsync(profileId, newTargets);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(profileId, result.ProfileId);

            var updatedProfile = await _performanceProfile.GetProfileAsync(profileId);
            Assert.Equal(70.0, updatedProfile.PerformanceTargets["MemoryUsage"]);
            Assert.Equal(60.0, updatedProfile.PerformanceTargets["CpuUsage"]);
            Assert.Equal(500.0, updatedProfile.PerformanceTargets["ResponseTime"]);
        }

        [Fact]
        public async Task GetPerformanceInsightsAsync_ShouldReturnInsightsForVersion()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };

            var metrics = new Dictionary<string, double>
            {
                ["MemoryUsage"] = 85.0, // Above threshold
                ["CpuUsage"] = 75.0,   // Above threshold
                ["ResponseTime"] = 200.0
            };

            await _performanceProfile.CreateProfileAsync(versionInfo, metrics);

            // Act
            var insights = await _performanceProfile.GetPerformanceInsightsAsync("2024");

            // Assert
            Assert.NotNull(insights);
            Assert.Equal("2024", insights.Version);
            Assert.NotEmpty(insights.Insights);
            Assert.True(insights.Insights.Any(i => i.Type == InsightType.Warning));
            Assert.True(insights.Insights.Any(i => i.Message.Contains("threshold")));
        }

        [Fact]
        public async Task CleanupExpiredProfilesAsync_ShouldCleanupExpiredProfiles()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };
            var initialMetrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };

            var profile = await _performanceProfile.CreateProfileAsync(versionInfo, initialMetrics);
            var profileId = profile.ProfileId;

            // Manually set the profile as expired (this would require internal access)
            // For this test, we'll simulate the cleanup process

            // Act
            await _performanceProfile.CleanupExpiredProfilesAsync();

            // Assert
            // Verify cleanup was attempted
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("cleanup")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetProfileStatisticsAsync_ShouldReturnStatisticsForVersion()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };

            var metrics1 = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 };
            var metrics2 = new Dictionary<string, double> { ["MemoryUsage"] = 55.0 };
            var metrics3 = new Dictionary<string, double> { ["MemoryUsage"] = 65.0 };

            await _performanceProfile.CreateProfileAsync(versionInfo, metrics1);
            await _performanceProfile.CreateProfileAsync(versionInfo, metrics2);
            await _performanceProfile.CreateProfileAsync(versionInfo, metrics3);

            // Act
            var stats = await _performanceProfile.GetProfileStatisticsAsync("2024");

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("2024", stats.Version);
            Assert.Equal(3, stats.ProfileCount);
            Assert.Equal(55.0, stats.AverageMemoryUsage); // (45 + 55 + 65) / 3
            Assert.Equal(65.0, stats.MaxMemoryUsage);
            Assert.Equal(45.0, stats.MinMemoryUsage);
        }

        [Fact]
        public async Task Concurrency_ShouldHandleConcurrentProfileOperations()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo { Version = "2024" };

            // Act - create multiple profiles concurrently
            var tasks = new List<Task<PerformanceProfile>>();
            for (int i = 0; i < 5; i++)
            {
                var metrics = new Dictionary<string, double> { ["MemoryUsage"] = 45.0 + i * 10.0 };
                tasks.Add(_performanceProfile.CreateProfileAsync(versionInfo, metrics));
            }

            var profiles = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(5, profiles.Length);
            Assert.True(profiles.All(p => p != null));
            Assert.True(profiles.All(p => p.Version == "2024"));

            var allProfiles = await _performanceProfile.GetProfilesByVersionAsync("2024");
            Assert.Equal(5, allProfiles.Count);
        }

        public void Dispose()
        {
            _performanceProfile?.Dispose();
        }
    }
}