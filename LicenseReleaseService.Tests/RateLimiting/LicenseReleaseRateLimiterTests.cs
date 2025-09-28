using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService;

namespace LicenseReleaseService.Tests.RateLimiting
{
    [TestClass]
    public class LicenseReleaseRateLimiterTests
    {
        private LicenseReleaseRateLimiter _rateLimiter;
        private RateLimitConfiguration _testConfig;

        [TestInitialize]
        public void Setup()
        {
            _testConfig = new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 3,
                WindowSize = TimeSpan.FromSeconds(1), // Short window for testing
                CooldownPeriod = TimeSpan.FromSeconds(2),
                BurstSize = 1,
                MaxConsecutiveFailures = 2,
                FailureCooldownPeriod = TimeSpan.FromSeconds(5),
                IsEnabled = true,
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
        public async Task CanReleaseLicenseAsync_WhenWithinLimits_ShouldAllow()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act
            var result1 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result2 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result3 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result1.IsAllowed, "First request should be allowed");
            Assert.IsTrue(result2.IsAllowed, "Second request should be allowed");
            Assert.IsTrue(result3.IsAllowed, "Third request should be allowed");
            Assert.AreEqual("Within rate limits", result1.Reason);
            Assert.AreEqual("Within rate limits", result2.Reason);
            Assert.AreEqual("Within rate limits", result3.Reason);
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenExceedingLimits_ShouldDeny()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act - make 4 requests (limit is 3)
            var result1 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result2 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result3 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result4 = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result1.IsAllowed, "First request should be allowed");
            Assert.IsTrue(result2.IsAllowed, "Second request should be allowed");
            Assert.IsTrue(result3.IsAllowed, "Third request should be allowed");
            Assert.IsFalse(result4.IsAllowed, "Fourth request should be denied");
            Assert.IsTrue(result4.RetryAfter > TimeSpan.Zero, "Should have retry time");
            Assert.IsTrue(result4.Reason.Contains("Rate limit exceeded"), "Should explain rate limit exceeded");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenUsingBurstToken_ShouldAllow()
        {
            // Arrange
            var configWithBurst = new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 2,
                WindowSize = TimeSpan.FromSeconds(1),
                CooldownPeriod = TimeSpan.FromSeconds(2),
                BurstSize = 2,
                IsEnabled = true
            };

            var limiter = new LicenseReleaseRateLimiter(configWithBurst);
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act - make 4 requests (2 regular + 2 burst)
            var result1 = await limiter.CanReleaseLicenseAsync(server, feature, user);
            var result2 = await limiter.CanReleaseLicenseAsync(server, feature, user);
            var result3 = await limiter.CanReleaseLicenseAsync(server, feature, user);
            var result4 = await limiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result1.IsAllowed, "First request should be allowed");
            Assert.IsTrue(result2.IsAllowed, "Second request should be allowed");
            Assert.IsTrue(result3.IsAllowed, "Third request should use burst token");
            Assert.IsTrue(result4.IsAllowed, "Fourth request should use burst token");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenBurstExhausted_ShouldDeny()
        {
            // Arrange
            var configWithBurst = new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 2,
                WindowSize = TimeSpan.FromSeconds(1),
                CooldownPeriod = TimeSpan.FromSeconds(2),
                BurstSize = 1,
                IsEnabled = true
            };

            var limiter = new LicenseReleaseRateLimiter(configWithBurst);
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act - make 4 requests (2 regular + 1 burst + 1 denied)
            var result1 = await limiter.CanReleaseLicenseAsync(server, feature, user);
            var result2 = await limiter.CanReleaseLicenseAsync(server, feature, user);
            var result3 = await limiter.CanReleaseLicenseAsync(server, feature, user);
            var result4 = await limiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result1.IsAllowed, "First request should be allowed");
            Assert.IsTrue(result2.IsAllowed, "Second request should be allowed");
            Assert.IsTrue(result3.IsAllowed, "Third request should use burst token");
            Assert.IsFalse(result4.IsAllowed, "Fourth request should be denied - burst exhausted");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenInCooldown_ShouldDeny()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Use up the rate limit
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Act - try one more request to trigger cooldown
            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsFalse(result.IsAllowed, "Request should be denied during cooldown");
            Assert.IsTrue(result.RetryAfter > TimeSpan.Zero, "Should have retry time");
            Assert.IsTrue(result.Reason.Contains("Rate limited"), "Should explain rate limited");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenInFailureCooldown_ShouldDeny()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Record failures to trigger failure cooldown
            await _rateLimiter.RecordFailureAsync(server, feature, user);
            await _rateLimiter.RecordFailureAsync(server, feature, user);

            // Act - try to make a request
            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsFalse(result.IsAllowed, "Request should be denied during failure cooldown");
            Assert.IsTrue(result.RetryAfter > TimeSpan.Zero, "Should have retry time");
            Assert.IsTrue(result.Reason.Contains("consecutive failures"), "Should explain consecutive failures");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenDisabled_ShouldAlwaysAllow()
        {
            // Arrange
            var disabledConfig = new RateLimitConfiguration { IsEnabled = false };
            var disabledLimiter = new LicenseReleaseRateLimiter(disabledConfig);
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act - make many requests
            var result1 = await disabledLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result2 = await disabledLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result3 = await disabledLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result4 = await disabledLimiter.CanReleaseLicenseAsync(server, feature, user);
            var result5 = await disabledLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result1.IsAllowed, "First request should be allowed");
            Assert.IsTrue(result2.IsAllowed, "Second request should be allowed");
            Assert.IsTrue(result3.IsAllowed, "Third request should be allowed");
            Assert.IsTrue(result4.IsAllowed, "Fourth request should be allowed");
            Assert.IsTrue(result5.IsAllowed, "Fifth request should be allowed");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_WhenWindowExpires_ShouldReset()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Use up the rate limit
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Wait for window to expire
            await Task.Delay(_testConfig.WindowSize.Add(TimeSpan.FromMilliseconds(100)));

            // Act
            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result.IsAllowed, "Request should be allowed after window reset");
        }

        [TestMethod]
        public async Task CanReleaseLicenseAsync_DifferentKeys_ShouldHaveSeparateLimits()
        {
            // Arrange
            const string server1 = "server1";
            const string server2 = "server2";
            const string feature = "test-feature";
            const string user1 = "user1";
            const string user2 = "user2";

            // Act - use up limit for user1
            var result1 = await _rateLimiter.CanReleaseLicenseAsync(server1, feature, user1);
            var result2 = await _rateLimiter.CanReleaseLicenseAsync(server1, feature, user1);
            var result3 = await _rateLimiter.CanReleaseLicenseAsync(server1, feature, user1);

            // user2 should still have full limit available
            var result4 = await _rateLimiter.CanReleaseLicenseAsync(server2, feature, user2);

            // Assert
            Assert.IsTrue(result1.IsAllowed, "User1 first request should be allowed");
            Assert.IsTrue(result2.IsAllowed, "User1 second request should be allowed");
            Assert.IsTrue(result3.IsAllowed, "User1 third request should be allowed");
            Assert.IsTrue(result4.IsAllowed, "User2 first request should be allowed");
        }

        [TestMethod]
        public async Task RecordReleaseAsync_WhenSuccessful_ShouldUpdateState()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Check status
            var status = await _rateLimiter.GetRateLimitStatusAsync(server, feature);

            // Assert
            Assert.AreEqual(1, status.RequestCount, "Request count should be 1");
            Assert.AreEqual(_testConfig.MaxReleasesPerWindow - 1, status.RemainingRequests,
                "Remaining requests should be decreased");
        }

        [TestMethod]
        public async Task RecordFailureAsync_WhenFailed_ShouldUpdateFailureCount()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Act
            await _rateLimiter.RecordFailureAsync(server, feature, user);
            await _rateLimiter.RecordFailureAsync(server, feature, user);

            // Try to make a request - should be denied due to failure cooldown
            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsFalse(result.IsAllowed, "Request should be denied due to failure cooldown");
            Assert.IsTrue(result.Reason.Contains("consecutive failures"), "Should mention consecutive failures");
        }

        [TestMethod]
        public async Task ResetRateLimitAsync_ShouldClearState()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Use up the rate limit
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Act
            await _rateLimiter.ResetRateLimitAsync(server, feature);

            // Try to make a request - should be allowed
            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result.IsAllowed, "Request should be allowed after reset");
        }

        [TestMethod]
        public async Task UpdateConfigurationAsync_ShouldUpdateLimits()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Use up current limit
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Act - update configuration with higher limit
            var newConfig = new RateLimitConfiguration
            {
                MaxReleasesPerWindow = 5,
                WindowSize = TimeSpan.FromSeconds(1),
                CooldownPeriod = TimeSpan.FromSeconds(2),
                BurstSize = 1,
                IsEnabled = true
            };

            await _rateLimiter.UpdateConfigurationAsync(newConfig);

            // Try to make a request - should be allowed with new limit
            var result = await _rateLimiter.CanReleaseLicenseAsync(server, feature, user);

            // Assert
            Assert.IsTrue(result.IsAllowed, "Request should be allowed with new configuration");
        }

        [TestMethod]
        public async Task GetRateLimitStatusAsync_ShouldReturnCorrectStatus()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";

            // Act
            var status = await _rateLimiter.GetRateLimitStatusAsync(server, feature);

            // Assert
            Assert.AreEqual(_testConfig.MaxReleasesPerWindow, status.MaxRequests, "Max requests should match config");
            Assert.AreEqual(_testConfig.MaxReleasesPerWindow, status.RemainingRequests, "Initially should have full remaining");
            Assert.AreEqual(0, status.RequestCount, "Initially no requests made");
            Assert.AreEqual(TimeSpan.Zero, status.CooldownRemaining, "Initially no cooldown");
        }

        [TestMethod]
        public void GetConfiguration_ShouldReturnCurrentConfiguration()
        {
            // Act
            var config = _rateLimiter.GetConfiguration();

            // Assert
            Assert.AreEqual(_testConfig.MaxReleasesPerWindow, config.MaxReleasesPerWindow);
            Assert.AreEqual(_testConfig.WindowSize, config.WindowSize);
            Assert.AreEqual(_testConfig.CooldownPeriod, config.CooldownPeriod);
            Assert.AreEqual(_testConfig.BurstSize, config.BurstSize);
            Assert.AreEqual(_testConfig.IsEnabled, config.IsEnabled);
        }

        [TestMethod]
        public async Task CleanupExpiredEntriesAsync_ShouldRemoveOldStates()
        {
            // Arrange
            const string server = "test-server";
            const string feature = "test-feature";
            const string user = "test-user";

            // Create some states
            await _rateLimiter.RecordReleaseAsync(server, feature, user);

            // Act - cleanup immediately (shouldn't remove recent entries)
            await _rateLimiter.CleanupExpiredEntriesAsync();

            var stats = _rateLimiter.GetStatistics();

            // Assert
            Assert.AreEqual(1, stats.ActiveStates, "Should still have active state");
        }

        [TestMethod]
        public void RateLimitConfiguration_Validate_ShouldThrowOnInvalidConfig()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                var config = new RateLimitConfiguration { MaxReleasesPerWindow = 0 };
                config.Validate();
            }, "Should throw on zero max releases");

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                var config = new RateLimitConfiguration { WindowSize = TimeSpan.Zero };
                config.Validate();
            }, "Should throw on zero window size");

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                var config = new RateLimitConfiguration { BurstSize = 5, MaxReleasesPerWindow = 3 };
                config.Validate();
            }, "Should throw when burst size exceeds max releases");
        }

        [TestMethod]
        public void RateLimitConfiguration_GetRateLimitKey_ShouldGenerateCorrectKeys()
        {
            // Arrange
            var config = new RateLimitConfiguration
            {
                EnablePerServerLimiting = true,
                EnablePerFeatureLimiting = true,
                EnablePerUserLimiting = true
            };

            // Act & Assert
            Assert.AreEqual("server:test1|feature:feat1|user:user1",
                config.GetRateLimitKey("test1", "feat1", "user1"));

            config.EnablePerUserLimiting = false;
            Assert.AreEqual("server:test1|feature:feat1",
                config.GetRateLimitKey("test1", "feat1", "user1"));

            config.EnablePerFeatureLimiting = false;
            Assert.AreEqual("server:test1",
                config.GetRateLimitKey("test1", "feat1", "user1"));

            config.EnablePerServerLimiting = false;
            Assert.AreEqual("global",
                config.GetRateLimitKey("test1", "feat1", "user1"));
        }

        [TestMethod]
        public void RateLimitState_RecordFailure_ShouldTriggerCooldownOnThreshold()
        {
            // Arrange
            var state = RateLimitState.Create("test-key", DateTime.UtcNow, 1);
            const int maxFailures = 2;

            // Act
            var triggered1 = state.RecordFailure(DateTime.UtcNow, maxFailures);
            var triggered2 = state.RecordFailure(DateTime.UtcNow, maxFailures);

            // Assert
            Assert.IsFalse(triggered1, "First failure should not trigger cooldown");
            Assert.IsTrue(triggered2, "Second failure should trigger cooldown");
            Assert.IsTrue(state.IsInFailureCooldown, "Should be in failure cooldown");
        }

        [TestMethod]
        public void RateLimitState_RefillBurstTokens_ShouldRefillOverTime()
        {
            // Arrange
            var state = RateLimitState.Create("test-key", DateTime.UtcNow.AddSeconds(-10), 5);
            state.BurstTokens = 0;

            // Act
            state.RefillBurstTokens(DateTime.UtcNow, 5, TimeSpan.FromSeconds(10));

            // Assert
            Assert.AreEqual(5, state.BurstTokens, "Should refill all burst tokens after full window");
        }
    }
}