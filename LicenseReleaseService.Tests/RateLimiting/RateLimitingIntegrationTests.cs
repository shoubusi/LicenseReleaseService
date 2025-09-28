using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService;

namespace LicenseReleaseService.Tests.RateLimiting
{
    [TestClass]
    public class RateLimitingIntegrationTests
    {
        private LicenseReleaseRateLimiter _rateLimiter;
        private RateLimitConfiguration _testConfig;

        [TestInitialize]
        public void Setup()
        {
            _testConfig = new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 5,
                WindowSize = TimeSpan.FromSeconds(2),
                CooldownPeriod = TimeSpan.FromSeconds(3),
                BurstSize = 2,
                MaxConsecutiveFailures = 3,
                FailureCooldownPeriod = TimeSpan.FromSeconds(5),
                IsEnabled = true,
                CleanupInterval = TimeSpan.FromSeconds(1),
                MaxEntryAge = TimeSpan.FromSeconds(10),
                EnablePerUserLimiting = true,
                EnablePerFeatureLimiting = true,
                EnablePerServerLimiting = true
            };

            _rateLimiter = new LicenseReleaseRateLimiter(_testConfig);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _rateLimiter?.Dispose();
        }

        [TestMethod]
        public async Task ConcurrentRequests_ShouldHandleRaceConditions()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";
            const int concurrentRequests = 10;

            var tasks = new Task<RateLimitResult>[concurrentRequests];
            var results = new RateLimitResult[concurrentRequests];

            // Act - make concurrent requests
            for (int i = 0; i < concurrentRequests; i++)
            {
                tasks[i] = _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < concurrentRequests; i++)
            {
                results[i] = await tasks[i];
            }

            // Assert
            var allowedCount = 0;
            var deniedCount = 0;

            foreach (var result in results)
            {
                if (result.IsAllowed)
                    allowedCount++;
                else
                    deniedCount++;
            }

            // Should allow regular limit + burst tokens
            Assert.AreEqual(_testConfig.MaxReleasesPerWindow + _testConfig.BurstSize, allowedCount,
                $"Should allow exactly {_testConfig.MaxReleasesPerWindow + _testConfig.BurstSize} requests");
            Assert.AreEqual(concurrentRequests - allowedCount, deniedCount,
                "Remaining requests should be denied");
        }

        [TestMethod]
        public async Task DifferentUsers_ShouldHaveIndependentRateLimits()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const int userCount = 3;
            const int requestsPerUser = _testConfig.MaxReleasesPerWindow + 1;

            var users = new string[userCount];
            for (int i = 0; i < userCount; i++)
            {
                users[i] = $"user{i}";
            }

            // Act - each user makes requests up to limit + 1
            var tasks = new Task[userCount];

            for (int u = 0; u < userCount; u++)
            {
                tasks[u] = Task.Run(async () =>
                {
                    var allowedCount = 0;
                    var deniedCount = 0;

                    for (int r = 0; r < requestsPerUser; r++)
                    {
                        var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, users[u]);
                        if (result.IsAllowed)
                            allowedCount++;
                        else
                            deniedCount++;
                    }

                    return (Allowed: allowedCount, Denied: deniedCount);
                });
            }

            var results = await Task.WhenAll(tasks);

            // Assert - each user should get their full limit
            foreach (var result in results)
            {
                Assert.AreEqual(_testConfig.MaxReleasesPerWindow, result.Allowed,
                    $"Each user should be allowed {_testConfig.MaxReleasesPerWindow} requests");
                Assert.AreEqual(1, result.Denied,
                    "Each user should have exactly 1 request denied");
            }
        }

        [TestMethod]
        public async Task DifferentFeatures_ShouldHaveIndependentRateLimits()
        {
            // Arrange
            const string server = "test-server";
            const string user = "test-user";
            const int featureCount = 3;
            const int requestsPerFeature = _testConfig.MaxReleasesPerWindow + 1;

            var features = new string[featureCount];
            for (int i = 0; i < featureCount; i++)
            {
                features[i] = $"feature{i}";
            }

            // Act - each feature gets requests up to limit + 1
            var tasks = new Task<(int Allowed, int Denied)>[featureCount];

            for (int f = 0; f < featureCount; f++)
            {
                tasks[f] = Task.Run(async () =>
                {
                    var allowedCount = 0;
                    var deniedCount = 0;

                    for (int r = 0; r < requestsPerFeature; r++)
                    {
                        var result = await _rateLimiter.CanReleaseLicenseAsync(server, features[f], user);
                        if (result.IsAllowed)
                            allowedCount++;
                        else
                            deniedCount++;
                    }

                    return (Allowed: allowedCount, Denied: deniedCount);
                });
            }

            var results = await Task.WhenAll(tasks);

            // Assert - each feature should get its full limit
            foreach (var result in results)
            {
                Assert.AreEqual(_testConfig.MaxReleasesPerWindow, result.Allowed,
                    $"Each feature should be allowed {_testConfig.MaxReleasesPerWindow} requests");
                Assert.AreEqual(1, result.Denied,
                    "Each feature should have exactly 1 request denied");
            }
        }

        [TestMethod]
        public async Task DifferentServers_ShouldHaveIndependentRateLimits()
        {
            // Arrange
            const string feature = "test-feature";
            const string user = "test-user";
            const int serverCount = 3;
            const int requestsPerServer = _testConfig.MaxReleasesPerWindow + 1;

            var servers = new string[serverCount];
            for (int i = 0; i < serverCount; i++)
            {
                servers[i] = $"server{i}";
            }

            // Act - each server gets requests up to limit + 1
            var tasks = new Task<(int Allowed, int Denied)>[serverCount];

            for (int s = 0; s < serverCount; s++)
            {
                tasks[s] = Task.Run(async () =>
                {
                    var allowedCount = 0;
                    var deniedCount = 0;

                    for (int r = 0; r < requestsPerServer; r++)
                    {
                        var result = await _rateLimiter.CanReleaseLicenseAsync(servers[s], feature, user);
                        if (result.IsAllowed)
                            allowedCount++;
                        else
                            deniedCount++;
                    }

                    return (Allowed: allowedCount, Denied: deniedCount);
                });
            }

            var results = await Task.WhenAll(tasks);

            // Assert - each server should get its full limit
            foreach (var result in results)
            {
                Assert.AreEqual(_testConfig.MaxReleasesPerWindow, result.Allowed,
                    $"Each server should be allowed {_testConfig.MaxReleasesPerWindow} requests");
                Assert.AreEqual(1, result.Denied,
                    "Each server should have exactly 1 request denied");
            }
        }

        [TestMethod]
        public async Task MixedScenarios_ShouldHandleComplexRateLimiting()
        {
            // Arrange
            var scenarios = new[]
            {
                new { Server = "server1", Feature = "feature1", User = "user1" },
                new { Server = "server1", Feature = "feature1", User = "user2" },
                new { Server = "server1", Feature = "feature2", User = "user1" },
                new { Server = "server2", Feature = "feature1", User = "user1" },
                new { Server = "server2", Feature = "feature2", User = "user2" }
            };

            const int requestsPerScenario = _testConfig.MaxReleasesPerWindow;

            // Act - each scenario makes requests
            var results = new System.Collections.Generic.List<(string Key, int Allowed, int Denied)>();

            foreach (var scenario in scenarios)
            {
                var allowedCount = 0;
                var deniedCount = 0;

                for (int i = 0; i < requestsPerScenario; i++)
                {
                    var result = await _rateLimiter.CanReleaseLicenseAsync(
                        scenario.Server, scenario.Feature, scenario.User);

                    if (result.IsAllowed)
                        allowedCount++;
                    else
                        deniedCount++;
                }

                var key = $"{scenario.Server}|{scenario.Feature}|{scenario.User}";
                results.Add((key, allowedCount, deniedCount));
            }

            // Assert - all scenarios should get their full independent limits
            foreach (var result in results)
            {
                Assert.AreEqual(_testConfig.MaxReleasesPerWindow, result.Allowed,
                    $"Scenario {result.Key} should be allowed {_testConfig.MaxReleasesPerWindow} requests");
                Assert.AreEqual(0, result.Denied,
                    $"Scenario {result.Key} should have no requests denied");
            }
        }

        [TestMethod]
        public async Task BurstHandling_ShouldWorkWithTimeWindows()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Use up regular limit
            for (int i = 0; i < _testConfig.MaxReleasesPerWindow; i++)
            {
                var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
                Assert.IsTrue(result.IsAllowed, $"Regular request {i + 1} should be allowed");
            }

            // Use burst tokens
            for (int i = 0; i < _testConfig.BurstSize; i++)
            {
                var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
                Assert.IsTrue(result.IsAllowed, $"Burst request {i + 1} should be allowed");
            }

            // Act - try one more request, should be denied
            var finalResult = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsFalse(finalResult.IsAllowed, "Final request should be denied");
            Assert.IsTrue(finalResult.RetryAfter > TimeSpan.Zero, "Should have retry time");

            // Wait for window to reset
            await Task.Delay(_testConfig.WindowSize.Add(TimeSpan.FromMilliseconds(100)));

            // Should be allowed again
            var resetResult = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            Assert.IsTrue(resetResult.IsAllowed, "Request should be allowed after window reset");
        }

        [TestMethod]
        public async Task FailureRecovery_ShouldAllowRequestsAfterCooldown()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Trigger failure cooldown
            for (int i = 0; i < _testConfig.MaxConsecutiveFailures; i++)
            {
                await _rateLimiter.RecordFailureAsync(server, feature, user);
            }

            // Act - try to make request, should be denied
            var result1 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            Assert.IsFalse(result1.IsAllowed, "Request should be denied during failure cooldown");

            // Wait for failure cooldown to expire
            await Task.Delay(_testConfig.FailureCooldownPeriod.Add(TimeSpan.FromMilliseconds(100)));

            // Should be allowed now
            var result2 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result2.IsAllowed, "Request should be allowed after failure cooldown expires");
        }

        [TestMethod]
        public async Task Statistics_ShouldTrackRateLimitingActivity()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Make some requests
            for (int i = 0; i < _testConfig.MaxReleasesPerWindow; i++)
            {
                await _rateLimiter.RecordReleaseAsync(server, feature, user);
            }

            // Make some failures
            await _rateLimiter.RecordFailureAsync(server, feature, user);

            // Act
            var stats = _rateLimiter.GetStatistics();

            // Assert
            Assert.AreEqual(1, stats.ActiveStates, "Should have 1 active state");
            Assert.AreEqual(_testConfig.MaxReleasesPerWindow, stats.TotalRequests, "Should track total requests");
            Assert.AreEqual(0, stats.CooldownStates, "Should not be in cooldown yet");
            Assert.AreEqual(0, stats.FailureCooldownStates, "Should not be in failure cooldown yet");
            Assert.AreEqual(_testConfig.IsEnabled, stats.Configuration.IsEnabled, "Should match enabled state");
        }

        [TestMethod]
        public async Task ConfigurationUpdates_ShouldApplyImmediately()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Use up current limit
            for (int i = 0; i < _testConfig.MaxReleasesPerWindow; i++)
            {
                var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
                Assert.IsTrue(result.IsAllowed, $"Request {i + 1} should be allowed");
            }

            // Should be denied now
            var deniedResult = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            Assert.IsFalse(deniedResult.IsAllowed, "Request should be denied after limit reached");

            // Act - update configuration with higher limit
            var newConfig = new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 10,
                WindowSize = TimeSpan.FromSeconds(2),
                CooldownPeriod = TimeSpan.FromSeconds(3),
                BurstSize = 2,
                IsEnabled = true
            };

            await _rateLimiter.UpdateConfigurationAsync(newConfig);

            // Should be allowed now with new limit
            var allowedResult = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(allowedResult.IsAllowed, "Request should be allowed with new configuration");
        }

        [TestMethod]
        public async Task Cleanup_ShouldRemoveExpiredStates()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Create a state
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Verify state exists
            var stats1 = _rateLimiter.GetStatistics();
            Assert.AreEqual(1, stats1.ActiveStates, "Should have 1 active state");

            // Act - set max age to very short and cleanup
            var shortConfig = new RateLimitConfiguration
            {
                MaxEntryAge = TimeSpan.FromMilliseconds(100)
            };

            await _rateLimiter.UpdateConfigurationAsync(shortConfig);
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            await _rateLimiter.CleanupExpiredEntriesAsync();

            var stats2 = _rateLimiter.GetStatistics();

            // Assert
            Assert.AreEqual(0, stats2.ActiveStates, "Should have 0 active states after cleanup");
        }

        [TestMethod]
        public async Task StressTest_ShouldHandleHighLoad()
        {
            // Arrange
            const int requestCount = 1000;
            const int concurrentUsers = 50;
            const string server = "test-server";
            const string feature = "test-feature";

            var random = new Random();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // Stop after 10 seconds

            // Act - create high load from multiple users
            var tasks = new Task[concurrentUsers];

            for (int i = 0; i < concurrentUsers; i++)
            {
                var user = $"user{i}";
                tasks[i] = Task.Run(async () =>
                {
                    var allowedCount = 0;
                    var deniedCount = 0;

                    for (int r = 0; r < requestCount / concurrentUsers; r++)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        try
                        {
                            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user, cts.Token);
                            if (result.IsAllowed)
                            {
                                allowedCount++;
                                // Simulate recording the release
                                await _rateLimiter.RecordReleaseAsync(server, feature, user, cts.Token);
                            }
                            else
                            {
                                deniedCount++;
                                // Simulate delay for rate limited requests
                                await Task.Delay(result.RetryAfter, cts.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }

                    return (Allowed: allowedCount, Denied: deniedCount);
                }, cts.Token);
            }

            var results = await Task.WhenAll(tasks);

            // Assert - system should remain stable
            var totalAllowed = 0;
            var totalDenied = 0;

            foreach (var result in results)
            {
                totalAllowed += result.Allowed;
                totalDenied += result.Denied;
            }

            Assert.IsTrue(totalAllowed > 0, "Should have allowed some requests");
            Assert.IsTrue(totalDenied > 0, "Should have denied some requests");
            Assert.AreEqual(requestCount, totalAllowed + totalDenied, "Total requests should match expected");

            // System should still be functional
            var stats = _rateLimiter.GetStatistics();
            Assert.IsTrue(stats.ActiveStates > 0, "Should have active states");
            Assert.IsTrue(stats.Configuration.IsEnabled, "Rate limiting should still be enabled");
        }
    }
}