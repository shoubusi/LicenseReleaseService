using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Tests.Integration
{
    /// <summary>
    /// Tests for caching behavior validation of the LicenseQueryEngine
    /// </summary>
    [TestClass]
    public class CachingBehaviorValidationTests
    {
        private ICacheManager _cacheManager;
        private IProcessExecutor _processExecutor;
        private LmstatOutputParser _outputParser;
        private LicenseQueryEngine _queryEngine;
        private TestProcessExecutor _testProcessExecutor;
        private TestCacheManager _testCacheManager;
        private string _lmstatOutput;

        [TestInitialize]
        public async Task TestInitialize()
        {
            _testCacheManager = new TestCacheManager();
            _testProcessExecutor = new TestProcessExecutor();
            _outputParser = new LmstatOutputParser();

            _cacheManager = _testCacheManager;
            _processExecutor = _testProcessExecutor;

            _queryEngine = new LicenseQueryEngine(_cacheManager, _processExecutor, _outputParser);
            _queryEngine.Options = LicenseQueryOptions.DefaultSolidWorksOptions();
            _queryEngine.Options.EnableCaching = true;
            _queryEngine.Options.CacheExpiration = TimeSpan.FromSeconds(30); // Short expiration for testing
            _queryEngine.Options.MaxRetries = 2;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(5);

            // Load test data
            _lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            // Set up default mock result
            SetupMockResults();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _queryEngine?.ClearAllCacheAsync().GetAwaiter().GetResult();
            _testCacheManager?.Clear();
            _testProcessExecutor?.Reset();
        }

        [TestMethod]
        public async Task CacheHit_WithValidCache_ShouldReturnCachedData()
        {
            // Arrange
            var server = "cache-test-server";
            var port = 27000;

            // Prime the cache
            var initialResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            Assert.IsNotNull(initialResult);
            Assert.IsTrue(initialResult.IsServerUp);

            var initialMetrics = _queryEngine.GetPerformanceMetrics();
            var initialCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Act - make additional calls that should hit cache
            var cachedResults = new List<LicenseServerStatus>();
            for (int i = 0; i < 5; i++)
            {
                var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                cachedResults.Add(result);
            }

            var finalMetrics = _queryEngine.GetPerformanceMetrics();
            var finalCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Assert
            Assert.AreEqual(1, initialCallCount, "Should have called lmstat once initially");
            Assert.AreEqual(1, finalCallCount, "Should not have called lmstat again for cached requests");
            Assert.AreEqual(5, cachedResults.Count, "Should have 5 cached results");

            // All cached results should be identical to the initial result
            foreach (var cachedResult in cachedResults)
            {
                Assert.AreSame(initialResult, cachedResult, "Should return exact same cached object");
                Assert.AreEqual(initialResult.Server, cachedResult.Server);
                Assert.AreEqual(initialResult.Port, cachedResult.Port);
                Assert.AreEqual(initialResult.IsServerUp, cachedResult.IsServerUp);
            }

            // Metrics should reflect cache hits
            Assert.AreEqual(5, finalMetrics.CachedQueries - initialMetrics.CachedQueries);

            Console.WriteLine($"Cache hit validation:");
            Console.WriteLine($"  Initial calls: {initialCallCount}, Final calls: {finalCallCount}");
            Console.WriteLine($"  Cached queries: {finalMetrics.CachedQueries - initialMetrics.CachedQueries}");
            Console.WriteLine($"  Cache hit ratio: {finalMetrics.CacheHitRatio:P2}");
        }

        [TestMethod]
        public async Task CacheMiss_WithExpiredCache_ShouldRefreshData()
        {
            // Arrange
            var server = "cache-expiration-server";
            var port = 27000;

            // Use very short cache expiration for testing
            _queryEngine.Options.CacheExpiration = TimeSpan.FromMilliseconds(100);

            // Prime the cache
            var initialResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var initialCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Wait for cache to expire
            await Task.Delay(200);

            // Act - should trigger cache miss and refresh
            var refreshedResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var finalCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Assert
            Assert.AreEqual(1, initialCallCount, "Should have called lmstat once initially");
            Assert.AreEqual(2, finalCallCount, "Should have called lmstat again after cache expiration");
            Assert.IsNotNull(refreshedResult);
            Assert.AreNotSame(initialResult, refreshedResult, "Should return new object after cache refresh");

            // Results should have same core data but be different objects
            Assert.AreEqual(initialResult.Server, refreshedResult.Server);
            Assert.AreEqual(initialResult.Port, refreshedResult.Port);
            Assert.AreEqual(initialResult.IsServerUp, refreshedResult.IsServerUp);

            Console.WriteLine($"Cache miss validation:");
            Console.WriteLine($"  Initial calls: {initialCallCount}, Final calls: {finalCallCount}");
            Console.WriteLine($"  Cache expiration: 100ms, Wait time: 200ms");
            Console.WriteLine($"  Successfully refreshed cache after expiration");
        }

        [TestMethod]
        public async Task CacheInvalidation_WithExplicitInvalidate_ShouldClearCache()
        {
            // Arrange
            var server = "cache-invalidation-server";
            var port = 27000;

            // Prime multiple cache entries
            await _queryEngine.QueryLicenseStatusAsync(server, port);
            await _queryEngine.QueryFeaturesAsync(server, port);
            await _queryEngine.QueryFeatureAsync(server, port, "solidworks");

            var initialCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");
            var initialFeatureCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-f solidworks -c {server}@{port}");

            // Act - invalidate server cache
            await _queryEngine.InvalidateServerCacheAsync(server, port);

            // Try to query again - should go to server
            var afterInvalidationResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var finalCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Assert
            Assert.AreEqual(1, initialCallCount, "Should have called lmstat once initially");
            Assert.AreEqual(2, finalCallCount, "Should have called lmstat again after invalidation");
            Assert.IsNotNull(afterInvalidationResult);
            Assert.AreNotEqual(initialCallCount, finalCallCount, "Call count should increase after invalidation");

            Console.WriteLine($"Cache invalidation validation:");
            Console.WriteLine($"  Initial server calls: {initialCallCount}");
            Console.WriteLine($"  Final server calls: {finalCallCount}");
            Console.WriteLine($"  Cache invalidated successfully");
        }

        [TestMethod]
        public async Task FeatureCache_WithSpecificFeature_ShouldCacheSeparately()
        {
            // Arrange
            var server = "feature-cache-server";
            var port = 27000;
            var feature = "solidworks";

            // Query general server status
            var serverResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var serverCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Query specific feature
            var featureResult = await _queryEngine.QueryFeatureAsync(server, port, feature);
            var featureCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-f {feature} -c {server}@{port}");

            // Act - query feature again (should hit cache)
            var cachedFeatureResult = await _queryEngine.QueryFeatureAsync(server, port, feature);
            var finalFeatureCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-f {feature} -c {server}@{port}");

            // Assert
            Assert.AreEqual(1, serverCallCount, "Should have called lmstat once for server status");
            Assert.AreEqual(1, featureCallCount, "Should have called lmstat once for feature");
            Assert.AreEqual(1, finalFeatureCallCount, "Should not call lmstat again for cached feature");

            Assert.IsNotNull(featureResult);
            Assert.AreSame(featureResult, cachedFeatureResult, "Should return same cached feature object");
            Assert.AreEqual(feature, featureResult.Name);

            Console.WriteLine($"Feature cache validation:");
            Console.WriteLine($"  Server calls: {serverCallCount}");
            Console.WriteLine($"  Feature calls: {featureCallCount} (initial), {finalFeatureCallCount} (final)");
            Console.WriteLine($"  Feature cached successfully");
        }

        [TestMethod]
        public async Task CacheConsistency_WithConcurrentAccess_ShouldMaintainConsistency()
        {
            // Arrange
            var server = "concurrent-cache-server";
            var port = 27000;
            const int concurrentThreads = 20;
            const int operationsPerThread = 10;

            // Prime cache
            await _queryEngine.QueryLicenseStatusAsync(server, port);
            var initialCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Act - concurrent cache access
            var results = new List<LicenseServerStatus>();
            var exceptions = new List<Exception>();

            var tasks = Enumerable.Range(0, concurrentThreads)
                .Select(async threadIndex =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        try
                        {
                            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                            lock (results)
                            {
                                results.Add(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }
                });

            await Task.WhenAll(tasks);
            var finalCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"Should have no exceptions, got {exceptions.Count}");
            Assert.AreEqual(concurrentThreads * operationsPerThread, results.Count, "Should have all results");
            Assert.AreEqual(1, initialCallCount, "Should have called lmstat once initially");
            Assert.AreEqual(1, finalCallCount, "Should not call lmstat again due to caching");

            // All results should be identical (same cached object)
            var firstResult = results.First();
            foreach (var result in results)
            {
                Assert.AreSame(firstResult, result, "All concurrent accesses should return same cached object");
                Assert.AreEqual(firstResult.Server, result.Server);
                Assert.AreEqual(firstResult.Port, result.Port);
            }

            Console.WriteLine($"Concurrent cache consistency validation:");
            Console.WriteLine($"  Threads: {concurrentThreads}, Operations per thread: {operationsPerThread}");
            Console.WriteLine($"  Total results: {results.Count}, Exceptions: {exceptions.Count}");
            Console.WriteLine($"  Server calls: {finalCallCount} (should be 1)");
            Console.WriteLine($"  All results consistent: {results.All(r => ReferenceEquals(r, firstResult))}");
        }

        [TestMethod]
        public async Task CacheMemoryUsage_WithLargeDataset_ShouldStayWithinBounds()
        {
            // Arrange
            var server = "memory-cache-server";
            var port = 27000;
            var largeOutput = GenerateLargeLmstatOutput(100, 50); // 100 features, 50 users each

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = largeOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Measure baseline memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(true);

            // Act - cache large dataset multiple times
            var results = new List<LicenseServerStatus>();
            for (int i = 0; i < 10; i++)
            {
                var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                results.Add(result);
            }

            // Measure memory after caching
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            Assert.AreEqual(10, results.Count, "Should have 10 results");
            Assert.IsTrue(memoryIncrease < 50 * 1024 * 1024, $"Memory increase {memoryIncrease / 1024 / 1024:F2}MB should be less than 50MB");

            // All results should be the same cached object
            var firstResult = results.First();
            for (int i = 1; i < results.Count; i++)
            {
                Assert.AreSame(firstResult, results[i], $"Result {i} should be same cached object");
            }

            // Verify the cached data is substantial
            Assert.IsTrue(firstResult.FeatureDetails.Count > 50, $"Should have many features cached, got {firstResult.FeatureDetails.Count}");
            Assert.IsTrue(firstResult.TotalUsers > 1000, $"Should have many users cached, got {firstResult.TotalUsers}");

            Console.WriteLine($"Cache memory usage validation:");
            Console.WriteLine($"  Dataset size: ~{firstResult.FeatureDetails.Count} features, ~{firstResult.TotalUsers} users");
            Console.WriteLine($"  Memory increase: {memoryIncrease / 1024 / 1024:F2}MB");
            Console.WriteLine($"  All results cached: {results.Count} identical objects");
        }

        [TestMethod]
        public async Task CacheExpiration_WithPreciseTiming_ShouldExpireExactly()
        {
            // Arrange
            var server = "timing-cache-server";
            var port = 27000;
            var expirationMs = 200; // 200ms expiration for precise testing

            _queryEngine.Options.CacheExpiration = TimeSpan.FromMilliseconds(expirationMs);

            // Prime cache and note exact time
            var startTime = DateTime.Now;
            var initialResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var initialCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Query immediately (should hit cache)
            var cachedResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var afterCacheCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Wait for just before expiration
            await Task.Delay(expirationMs - 50);
            var beforeExpirationResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var beforeExpirationCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Wait for after expiration
            await Task.Delay(100); // Total wait: expirationMs - 50 + 100 = expirationMs + 50
            var afterExpirationResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var afterExpirationCallCount = _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}");

            // Assert
            Assert.AreEqual(1, initialCallCount, "Initial call should happen");
            Assert.AreEqual(1, afterCacheCallCount, "Cached call should not increment count");
            Assert.AreEqual(1, beforeExpirationCallCount, "Before expiration should still use cache");
            Assert.AreEqual(2, afterExpirationCallCount, "After expiration should call server again");

            Assert.AreSame(initialResult, cachedResult, "Immediate cache hit should return same object");
            Assert.AreSame(initialResult, beforeExpirationResult, "Before expiration should return same object");
            Assert.AreNotSame(initialResult, afterExpirationResult, "After expiration should return new object");

            var totalTime = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"Cache expiration timing validation:");
            Console.WriteLine($"  Expiration set to: {expirationMs}ms");
            Console.WriteLine($"  Total test time: {totalTime:F0}ms");
            Console.WriteLine($"  Cache worked before expiration: {ReferenceEquals(initialResult, beforeExpirationResult)}");
            Console.WriteLine($"  Cache refreshed after expiration: {ReferenceEquals(initialResult, afterExpirationResult)}");
        }

        [TestMethod]
        public async Task CacheClear_WithFullClear_ShouldRemoveAllData()
        {
            // Arrange
            var servers = new[] { "server1", "server2", "server3" };
            var ports = new[] { 27000, 27001, 27002 };

            // Populate cache with multiple servers and features
            foreach (var (server, port) in servers.Zip(ports, (s, p) => (s, p)))
            {
                await _queryEngine.QueryLicenseStatusAsync(server, port);
                await _queryEngine.QueryFeatureAsync(server, port, "solidworks");
                await _queryEngine.QueryActiveUsersAsync(server, port);
            }

            var initialMetrics = _queryEngine.GetPerformanceMetrics();
            var initialCachedQueries = initialMetrics.CachedQueries;

            // Act - clear all cache
            await _queryEngine.ClearAllCacheAsync();

            // Query again - should all go to server
            var refreshResults = new List<LicenseServerStatus>();
            foreach (var (server, port) in servers.Zip(ports, (s, p) => (s, p)))
            {
                var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                refreshResults.Add(result);
            }

            var finalMetrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(servers.Length * 3, initialCachedQueries, "Should have cached queries from initial population");
            Assert.AreEqual(servers.Length, refreshResults.Count, "Should have results from all servers after refresh");

            // Cache should have been cleared, so cached count should not increase much
            var cachedQueryIncrease = finalMetrics.CachedQueries - initialCachedQueries;
            Assert.IsTrue(cachedQueryIncrease <= servers.Length, "Cached queries should not increase significantly after clear");

            Console.WriteLine($"Cache clear validation:");
            Console.WriteLine($"  Initial cached queries: {initialCachedQueries}");
            Console.WriteLine($"  After clear cached queries: {finalMetrics.CachedQueries}");
            Console.WriteLine($"  Cached query increase: {cachedQueryIncrease}");
            Console.WriteLine($"  Successfully cleared all cache entries");
        }

        [TestMethod]
        public async Task CachePerformance_WithHighFrequencyAccess_ShouldBeFast()
        {
            // Arrange
            var server = "performance-cache-server";
            var port = 27000;
            const int testRuns = 1000;

            // Prime cache
            await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Act - high frequency cache access
            var responseTimes = new List<long>();
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < testRuns; i++)
            {
                var queryStopwatch = Stopwatch.StartNew();
                await _queryEngine.QueryLicenseStatusAsync(server, port);
                queryStopwatch.Stop();
                responseTimes.Add(queryStopwatch.ElapsedMilliseconds);
            }

            stopwatch.Stop();

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            var averageResponseTime = responseTimes.Average();
            var maxResponseTime = responseTimes.Max();
            var minResponseTime = responseTimes.Min();
            var throughput = testRuns / (stopwatch.ElapsedMilliseconds / 1000.0);

            // Cache access should be very fast
            Assert.IsTrue(averageResponseTime < 5, $"Average cache response time {averageResponseTime:F2}ms should be less than 5ms");
            Assert.IsTrue(maxResponseTime < 50, $"Max cache response time {maxResponseTime:F2}ms should be less than 50ms");
            Assert.IsTrue(throughput > 1000, $"Throughput {throughput:F2} queries/sec should be > 1000");

            // Should have very high cache hit ratio
            Assert.IsTrue(metrics.CacheHitRatio > 0.99, $"Cache hit ratio {metrics.CacheHitRatio:P2} should be > 99%");

            Console.WriteLine($"Cache performance validation:");
            Console.WriteLine($"  Test runs: {testRuns}");
            Console.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Average response: {averageResponseTime:F3}ms");
            Console.WriteLine($"  Min response: {minResponseTime:F3}ms");
            Console.WriteLine($"  Max response: {maxResponseTime:F3}ms");
            Console.WriteLine($"  Throughput: {throughput:F0} queries/sec");
            Console.WriteLine($"  Cache hit ratio: {metrics.CacheHitRatio:P2}");
        }

        [TestMethod]
        public async Task CacheStatistics_WithTrackingEnabled_ShouldReportCorrectly()
        {
            // Arrange
            var server = "statistics-cache-server";
            var port = 27000;

            _queryEngine.ResetPerformanceMetrics();

            // Act - mix of cache hits and misses
            // First call (miss)
            await _queryEngine.QueryLicenseStatusAsync(server, port);
            var stats1 = _queryEngine.GetPerformanceMetrics();

            // Next calls (hits)
            for (int i = 0; i < 5; i++)
            {
                await _queryEngine.QueryLicenseStatusAsync(server, port);
            }
            var stats2 = _queryEngine.GetPerformanceMetrics();

            // Wait for expiration and call again (miss)
            await Task.Delay(350); // Cache expires after 300ms (30s in setup overridden to 30ms in other tests)
            await _queryEngine.QueryLicenseStatusAsync(server, port);
            var stats3 = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(1, stats1.TotalQueries, "First stats should have 1 total query");
            Assert.AreEqual(1, stats1.SuccessfulQueries, "First stats should have 1 successful query");
            Assert.AreEqual(0, stats1.CachedQueries, "First stats should have 0 cached queries");
            Assert.AreEqual(0, stats1.CacheHitRatio, "First stats should have 0 cache hit ratio");

            Assert.AreEqual(6, stats2.TotalQueries, "Second stats should have 6 total queries");
            Assert.AreEqual(6, stats2.SuccessfulQueries, "Second stats should have 6 successful queries");
            Assert.AreEqual(5, stats2.CachedQueries, "Second stats should have 5 cached queries");
            Assert.IsTrue(Math.Abs(stats2.CacheHitRatio - 5.0/6.0) < 0.001, "Cache hit ratio should be 5/6");

            Assert.AreEqual(7, stats3.TotalQueries, "Third stats should have 7 total queries");
            Assert.AreEqual(7, stats3.SuccessfulQueries, "Third stats should have 7 successful queries");
            Assert.AreEqual(5, stats3.CachedQueries, "Third stats should have 5 cached queries (after cache miss)");

            Console.WriteLine($"Cache statistics validation:");
            Console.WriteLine($"  After 1 call (1 miss): Total={stats1.TotalQueries}, Cached={stats1.CachedQueries}, HitRatio={stats1.CacheHitRatio:P2}");
            Console.WriteLine($"  After 6 calls (1 miss, 5 hits): Total={stats2.TotalQueries}, Cached={stats2.CachedQueries}, HitRatio={stats2.CacheHitRatio:P2}");
            Console.WriteLine($"  After 7 calls (2 misses, 5 hits): Total={stats3.TotalQueries}, Cached={stats3.CachedQueries}, HitRatio={stats3.CacheHitRatio:P2}");
        }

        [TestMethod]
        public async Task CacheWithDifferentDataTypes_ShouldCacheAllTypesCorrectly()
        {
            // Arrange
            var server = "multi-type-cache-server";
            var port = 27000;

            // Act - cache different types of data
            var serverStatus = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var features = await _queryEngine.QueryFeaturesAsync(server, port);
            var activeUsers = await _queryEngine.QueryActiveUsersAsync(server, port);
            var statistics = await _queryEngine.QueryUsageStatisticsAsync(server, port);
            var health = await _queryEngine.CheckServerHealthAsync(server, port);

            // Query again to test cache hits
            var cachedServerStatus = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var cachedFeatures = await _queryEngine.QueryFeaturesAsync(server, port);
            var cachedActiveUsers = await _queryEngine.QueryActiveUsersAsync(server, port);
            var cachedStatistics = await _queryEngine.QueryUsageStatisticsAsync(server, port);
            var cachedHealth = await _queryEngine.CheckServerHealthAsync(server, port);

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            // Should have minimal process executor calls (most should come from cache)
            var totalCalls = _testProcessExecutor.GetTotalCallCount();
            Assert.IsTrue(totalCalls <= 5, $"Should have <=5 process calls, got {totalCalls}");

            // All cached results should be identical to original results
            Assert.AreSame(serverStatus, cachedServerStatus, "Server status should be cached");
            Assert.AreEqual(features.Count, cachedFeatures.Count, "Features should be cached");
            Assert.AreEqual(activeUsers.Keys.Count, cachedActiveUsers.Keys.Count, "Active users should be cached");

            // Statistics should be calculated but based on cached data
            Assert.AreEqual(serverStatus.TotalLicenses, statistics.TotalLicenses);
            Assert.AreEqual(serverStatus.LicensesInUse, statistics.LicensesInUse);

            // Health check should be fast (uses cached server status)
            Assert.IsTrue(health.ResponseTimeMs < 1000, "Health check should be fast with cached data");

            // Should have good cache hit ratio
            Assert.IsTrue(metrics.CacheHitRatio > 0.5, $"Cache hit ratio should be > 50%, got {metrics.CacheHitRatio:P2}");

            Console.WriteLine($"Multi-type cache validation:");
            Console.WriteLine($"  Process calls: {totalCalls}");
            Console.WriteLine($"  Total queries: {metrics.TotalQueries}");
            Console.WriteLine($"  Cached queries: {metrics.CachedQueries}");
            Console.WriteLine($"  Cache hit ratio: {metrics.CacheHitRatio:P2}");
            Console.WriteLine($"  All data types cached successfully");
        }

        private string GenerateLargeLmstatOutput(int featureCount, int usersPerFeature)
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine("License server status: 27000@memory-cache-server");
            output.AppendLine("    License file(s) on memory-cache-server:");
            output.AppendLine("    SOLIDWORKS: license server UP (MASTER) v11.16.2");
            output.AppendLine();
            output.AppendLine("Vendor daemon status (on memory-cache-server):");
            output.AppendLine("    solidworks: UP v11.16.2");
            output.AppendLine();

            var random = new Random(42);

            for (int f = 1; f <= featureCount; f++)
            {
                var featureName = $"feature{f}";
                var totalLicenses = random.Next(50, 200);
                var usedLicenses = random.Next(0, totalLicenses);

                output.AppendLine($"Users of {featureName}:  (Total of {totalLicenses} licenses issued;  Total of {usedLicenses} licenses in use)");

                for (int u = 1; u <= usedLicenses; u++)
                {
                    var username = $"user{random.Next(1, 5000):D4}";
                    var workstation = $"workstation{random.Next(1, 200):D3}";
                    var version = $"v{random.Next(2020, 2024)}.{random.Next(1, 10)}";
                    var handle = random.Next(1, 1000);

                    output.AppendLine($"\"{username}\" {workstation} ({version}) (memory-cache-server/27000 {handle}), start Mon 9/25 {random.Next(1, 23):D2}:{random.Next(0, 59):D2}");
                }

                output.AppendLine();
            }

            return output.ToString();
        }

        private void SetupMockResults()
        {
            _testProcessExecutor.SetMockResult("lmutil.exe", "-c license-server@27000", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            // Set up for multiple servers
            var servers = new[] { "cache-test-server", "cache-expiration-server", "cache-invalidation-server",
                "feature-cache-server", "concurrent-cache-server", "memory-cache-server", "timing-cache-server",
                "performance-cache-server", "statistics-cache-server", "multi-type-cache-server" };

            foreach (var server in servers)
            {
                _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@27000", new ProcessExecutionResult
                {
                    ExitCode = 0,
                    Output = _lmstatOutput.Replace("license-server", server),
                    ExecutionTime = TimeSpan.FromMilliseconds(50),
                    Success = true
                });

                _testProcessExecutor.SetMockResult("lmutil.exe", $"-f solidworks -c {server}@27000", new ProcessExecutionResult
                {
                    ExitCode = 0,
                    Output = _lmstatOutput.Replace("license-server", server),
                    ExecutionTime = TimeSpan.FromMilliseconds(50),
                    Success = true
                });
            }
        }
    }
}