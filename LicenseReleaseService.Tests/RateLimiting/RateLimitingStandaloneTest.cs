using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LicenseReleaseService.Tests.RateLimiting
{
    /// <summary>
    /// Standalone test class for validating rate limiting functionality
    /// </summary>
    public class RateLimitingStandaloneTest
    {
        public static async Task<bool> RunBasicTests()
        {
            Console.WriteLine("Running basic rate limiting tests...");

            try
            {
                // Test 1: Basic rate limiting
                Console.WriteLine("Test 1: Basic rate limiting");
                var limiter = new LicenseReleaseRateLimiter(new RateLimitConfiguration
                {
                    MaxReleasesPerWindow = 3,
                    WindowSize = TimeSpan.FromSeconds(1),
                    CooldownPeriod = TimeSpan.FromSeconds(2),
                    BurstSize = 1,
                    IsEnabled = true
                });

                const string server = "test-server";
                const string feature = "test-feature";
                const string user = "test-user";

                // Should allow first 3 requests
                for (int i = 0; i < 3; i++)
                {
                    var result = await limiter.CanReleaseLicenseAsync(server, feature, user);
                    if (!result.IsAllowed)
                    {
                        Console.WriteLine($"FAILED: Request {i + 1} should be allowed but was denied: {result.Reason}");
                        return false;
                    }
                }

                // Should deny 4th request
                var deniedResult = await limiter.CanReleaseLicenseAsync(server, feature, user);
                if (deniedResult.IsAllowed)
                {
                    Console.WriteLine("FAILED: 4th request should be denied but was allowed");
                    return false;
                }

                Console.WriteLine("✓ Basic rate limiting test passed");

                // Test 2: Different users have separate limits
                Console.WriteLine("Test 2: Per-user rate limiting");
                var user2Result = await limiter.CanReleaseLicenseAsync(server, feature, "user2");
                if (!user2Result.IsAllowed)
                {
                    Console.WriteLine("FAILED: Different user should have separate limit but was denied");
                    return false;
                }

                Console.WriteLine("✓ Per-user rate limiting test passed");

                // Test 3: Cooldown functionality
                Console.WriteLine("Test 3: Cooldown functionality");
                // Use up limit for user3
                await limiter.RecordReleaseAsync(server, feature, "user3");
                await limiter.RecordReleaseAsync(server, feature, "user3");
                await limiter.RecordReleaseAsync(server, feature, "user3");

                // Try one more to trigger cooldown
                var cooldownResult = await limiter.CanReleaseLicenseAsync(server, feature, "user3");
                if (cooldownResult.IsAllowed)
                {
                    Console.WriteLine("FAILED: Should trigger cooldown but request was allowed");
                    return false;
                }

                if (cooldownResult.RetryAfter <= TimeSpan.Zero)
                {
                    Console.WriteLine("FAILED: Should have positive retry time during cooldown");
                    return false;
                }

                Console.WriteLine("✓ Cooldown functionality test passed");

                // Test 4: Configuration validation
                Console.WriteLine("Test 4: Configuration validation");
                try
                {
                    var invalidConfig = new RateLimitConfiguration { MaxReleasesPerWindow = 0 };
                    invalidConfig.Validate();
                    Console.WriteLine("FAILED: Should throw exception for invalid configuration");
                    return false;
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }

                Console.WriteLine("✓ Configuration validation test passed");

                // Test 5: Burst functionality
                Console.WriteLine("Test 5: Burst functionality");
                var burstLimiter = new LicenseReleaseRateLimiter(new RateLimitConfiguration
                {
                    MaxReleasesPerWindow = 2,
                    WindowSize = TimeSpan.FromSeconds(1),
                    CooldownPeriod = TimeSpan.FromSeconds(2),
                    BurstSize = 2,
                    IsEnabled = true
                });

                // Use regular limit
                await burstLimiter.RecordReleaseAsync(server, feature, "burstUser");
                await burstLimiter.RecordReleaseAsync(server, feature, "burstUser");

                // Should allow burst requests
                var burst1 = await burstLimiter.CanReleaseLicenseAsync(server, feature, "burstUser");
                var burst2 = await burstLimiter.CanReleaseLicenseAsync(server, feature, "burstUser");

                if (!burst1.IsAllowed || !burst2.IsAllowed)
                {
                    Console.WriteLine("FAILED: Burst requests should be allowed");
                    return false;
                }

                // Should deny after burst exhausted
                var burst3 = await burstLimiter.CanReleaseLicenseAsync(server, feature, "burstUser");
                if (burst3.IsAllowed)
                {
                    Console.WriteLine("FAILED: Should deny after burst exhausted");
                    return false;
                }

                Console.WriteLine("✓ Burst functionality test passed");

                // Test 6: Statistics
                Console.WriteLine("Test 6: Statistics tracking");
                var stats = limiter.GetStatistics();
                if (stats.TotalStates < 1)
                {
                    Console.WriteLine("FAILED: Should track at least one state");
                    return false;
                }

                Console.WriteLine($"✓ Statistics test passed: {stats.TotalStates} states tracked");

                Console.WriteLine("\n🎉 All basic rate limiting tests passed!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public static void TestConfigurationKeyGeneration()
        {
            Console.WriteLine("Testing configuration key generation...");

            var config = new RateLimitConfiguration
            {
                EnablePerServerLimiting = true,
                EnablePerFeatureLimiting = true,
                EnablePerUserLimiting = true
            };

            var key1 = config.GetRateLimitKey("server1", "feature1", "user1");
            var expected1 = "server:server1|feature:feature1|user:user1";

            if (key1 != expected1)
            {
                Console.WriteLine($"FAILED: Expected '{expected1}', got '{key1}'");
                return;
            }

            config.EnablePerUserLimiting = false;
            var key2 = config.GetRateLimitKey("server1", "feature1", "user1");
            var expected2 = "server:server1|feature:feature1";

            if (key2 != expected2)
            {
                Console.WriteLine($"FAILED: Expected '{expected2}', got '{key2}'");
                return;
            }

            config.EnablePerFeatureLimiting = false;
            var key3 = config.GetRateLimitKey("server1", "feature1", "user1");
            var expected3 = "server:server1";

            if (key3 != expected3)
            {
                Console.WriteLine($"FAILED: Expected '{expected3}', got '{key3}'");
                return;
            }

            config.EnablePerServerLimiting = false;
            var key4 = config.GetRateLimitKey("server1", "feature1", "user1");
            var expected4 = "global";

            if (key4 != expected4)
            {
                Console.WriteLine($"FAILED: Expected '{expected4}', got '{key4}'");
                return;
            }

            Console.WriteLine("✓ Configuration key generation test passed");
        }

        public static async Task RunAllTests()
        {
            Console.WriteLine("=== Rate Limiting System Standalone Tests ===\n");

            TestConfigurationKeyGeneration();
            Console.WriteLine();

            var success = await RunBasicTests();

            Console.WriteLine($"\n=== Test Results ===");
            Console.WriteLine($"Overall: {(success ? "✅ PASSED" : "❌ FAILED")}");
        }
    }
}